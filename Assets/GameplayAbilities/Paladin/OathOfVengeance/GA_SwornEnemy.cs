using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ============================================================
// GA_SwornEnemy  (Canalizar divinidad: Enemigo jurado — Paladín / Juramento de la venganza)
//
// Marca al enemigo apuntado por un tiempo. Mientras la marca dure:
//   · el marcado recibe MÁS DAÑO (eso lo hace el propio GE de la marca, con un
//     modificador de Vulnerability — no hace falta código);
//   · y cada vez que un ALIADO del Paladín lo golpea, ese aliado SE CURA.
//
// Lo segundo es lo único que no se puede expresar con un GameplayEffect: la
// curación no le pasa a quien tiene la marca, sino a QUIEN LO GOLPEA, y eso
// depende de quién sea el atacante. Por eso esta habilidad existe como script en
// vez de ser un GA_Target configurado.
//
// CÓMO SE ENTERA: se suscribe al evento OnTookDamage del ENEMIGO marcado, que se
// dispara en el servidor con el atacante como parámetro. Es el mismo gancho que usa
// la Copia exacta del Ilusionista para reaccionar a quien la golpea.
//
// Una marca a la vez: volver a lanzarla sobre otro objetivo cancela la anterior.
// Es lo que se espera de un "Canalizar divinidad" con tiempo de reutilización, y
// evita tener que llevar una lista de marcas vivas.
// ============================================================
[CreateAssetMenu(fileName = "GA_SwornEnemy", menuName = "GAS/Specific Abilities/Vengeance/Sworn Enemy")]
public class GA_SwornEnemy : GameplayAbility
{
    [Header("Selección de Enemigo")]
    [Tooltip("Alcance máximo para buscar al enemigo a marcar.")]
    public float MaxRange = 10f;

    [Tooltip("Ángulo máximo (grados) entre la mira y el enemigo para que cuente como objetivo.")]
    public float SelectionAngle = 30f;

    [Header("La Marca")]
    [Tooltip("Efecto CON DURACIÓN que se le aplica al enemigo marcado. Acá va el modificador de " +
             "Vulnerability (recibe más daño) y el tag Status_SwornEnemy.\n\n" +
             "Su Duration es la que manda: la vigilancia de golpes dura exactamente lo mismo.")]
    public GameplayEffect MarkEffect;

    [Header("Curación a los Aliados")]
    [Tooltip("Efectos que recibe CADA aliado que golpee al marcado. Normalmente una curación " +
             "instantánea. El Paladín también cuenta como aliado de sí mismo, así que él también " +
             "se cura al golpearlo.")]
    public List<GameplayEffect> AllyRewardEffects;

    [Tooltip("Tiempo mínimo entre dos curaciones al MISMO aliado, en segundos. Sin esto, una clase " +
             "de ataques rápidos se curaría muchísimo más que una de golpes lentos por el simple " +
             "hecho de pegar más veces.")]
    public float RewardCooldownPerAlly = 0.5f;

    [Header("Visuales")]
    public GameObject ImpactVFX;

    // Objetivo marcado ahora mismo, para poder desuscribirnos al re-marcar o al
    // expirar. NonSerialized: estado de runtime por instancia otorgada.
    [System.NonSerialized] private AbilitySystemComponent _marked;
    // Cuándo se curó por última vez cada aliado, para el estrangulador de arriba.
    [System.NonSerialized] private Dictionary<AbilitySystemComponent, float> _lastReward;

    // =========================================================
    // ACTIVACIÓN
    // =========================================================

    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;

        AbilitySystemComponent target = FindBestTargetInAim(
            MaxRange, SelectionAngle, ETargetAffiliation.Enemies);

        // Sin enemigo a la vista no se gasta nada: ni costo, ni cooldown, ni carga.
        if (target == null)
        {
            EndAbility();
            return;
        }

        CommitAbility();

        if (MarkEffect == null)
        {
            Debug.LogWarning($"[{AbilityName}] no tiene MarkEffect: no hay marca que aplicar ni " +
                             $"duración de la que colgar la curación a los aliados.");
            EndAbility();
            return;
        }

        // Una marca a la vez: soltamos la anterior antes de poner la nueva.
        ClearMark();

        target.ApplyGameplayEffect(MarkEffect, OwnerASC);

        _marked = target;
        _lastReward = new Dictionary<AbilitySystemComponent, float>();
        _marked.OnTookDamage += HandleMarkedTookDamage;

        OwnerASC.StartAbilityCoroutine(MarkRoutine(MarkEffect.Duration));

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();

        if (pc != null)
        {
            pc.RotateToAim();
            pc.PlayAnimation(this);
        }

        Vector3 vfxPos = target.transform.position + Vector3.up;
        if (netAsc != null) netAsc.ServerPlayAbilityVFX(this, vfxPos);
        else PlayImpactVFX(vfxPos);

        EndAbility();
    }

    // =========================================================
    // LA MARCA
    // =========================================================

    // Mantiene la vigilancia mientras dure la marca, y la corta antes si el objetivo
    // muere o si el Paladín deja de estar en condiciones de sostenerla.
    private IEnumerator MarkRoutine(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (_marked == null || OwnerASC == null) break;
            if (_marked.HasTag(EGameplayTag.State_Dead)) break;
            if (OwnerASC.HasTag(EGameplayTag.State_Dead)) break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        ClearMark();
    }

    // Suelta la marca actual: desuscribe el gancho y retira el efecto del objetivo.
    // Idempotente — la llaman el fin de la corutina, el re-lanzamiento y el cierre.
    private void ClearMark()
    {
        if (_marked == null) return;

        _marked.OnTookDamage -= HandleMarkedTookDamage;

        // Retiramos la marca a mano en vez de esperar a que expire sola: si la
        // cortamos antes de tiempo (el Paladín murió, o volvió a marcar a otro), el
        // enemigo no debería seguir recibiendo daño aumentado.
        if (MarkEffect != null) _marked.RemoveEffectsByDefinition(MarkEffect);

        _marked = null;
        _lastReward = null;
    }

    // Alguien golpeó al enemigo marcado. Si es un aliado del Paladín, se cura.
    //
    // Corre en el servidor (OnTookDamage se dispara desde el pipeline de daño), así
    // que aplicar efectos acá es seguro y se sincroniza por los canales normales.
    private void HandleMarkedTookDamage(AbilitySystemComponent attacker)
    {
        if (attacker == null || OwnerASC == null) return;

        // includeSelf: el Paladín se cura al golpear a su propio marcado.
        if (!OwnerASC.IsAllyOf(attacker, includeSelf: true)) return;

        // Un aliado no se cura dos veces seguidas por pegar rápido.
        if (_lastReward != null)
        {
            if (_lastReward.TryGetValue(attacker, out float last) &&
                Time.time - last < RewardCooldownPerAlly) return;

            _lastReward[attacker] = Time.time;
        }

        ApplyEffectsTo(AllyRewardEffects, attacker);
    }

    // =========================================================
    // VISUALES Y GIZMOS
    // =========================================================

    public override void PlayImpactVFX(Vector3 position)
    {
        if (ImpactVFX == null) return;
        GameObject vfx = Instantiate(ImpactVFX, position, Quaternion.identity);
        Destroy(vfx, 2.0f);
    }

    public override void DrawGizmos(Transform origin)
    {
        if (origin == null) return;
        Gizmos.color = new Color(0.9f, 0.75f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(origin.position, MaxRange);
    }
}
