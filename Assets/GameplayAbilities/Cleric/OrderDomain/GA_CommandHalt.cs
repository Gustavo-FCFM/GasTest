using UnityEngine;
using System.Collections;

// ============================================================
// GA_CommandHalt  (Canalizar divinidad: Aturdir — Clérigo, Dominio del orden)
//
// "Selecciona a un enemigo a alcance medio: por un corto periodo, si impacta un ataque
// contra un aliado, queda aturdido un tiempo."
//
// Marca al enemigo de la mira. Mientras la marca dure, la PRIMERA vez que ese enemigo
// le pega a un aliado del Clérigo (o al Clérigo mismo), recibe StunEffect y la marca
// se consume. Si no le pega a nadie, la marca se va sola al terminar su duración.
//
// Es el mismo esquema que el Enemigo jurado del Paladín (GA_SwornEnemy), al revés:
// aquel escucha los golpes que el marcado RECIBE (OnTookDamage); este, los que DA
// (OnDealtDamage). Por eso es un script y no un GA_Target: el aturdido depende de a
// quién golpea el marcado, y eso un GameplayEffect no lo sabe.
//
// Una marca a la vez: volver a lanzarla sobre otro suelta la anterior.
// ============================================================
[CreateAssetMenu(fileName = "GA_CommandHalt", menuName = "GAS/Cleric/Command Halt")]
public class GA_CommandHalt : TargetImpactAbility
{
    [Header("Selección de Enemigo")]
    [Tooltip("Alcance máximo para buscar al enemigo a marcar. 10 m = alcance medio.")]
    public float MaxRange = 10f;

    [Tooltip("Ángulo máximo (grados) entre la mira y el enemigo para que cuente como objetivo.")]
    public float SelectionAngle = 30f;

    [Header("La Marca")]
    [Tooltip("Efecto CON DURACIÓN que lleva el marcado (el ícono y la duración de la vigilancia). " +
             "Su Duration manda: la marca vigila exactamente ese tiempo.")]
    public GameplayEffect MarkEffect;

    [Tooltip("Lo que recibe el marcado si le pega a un aliado (GE_Stun).")]
    public GameplayEffect StunEffect;

    // Enemigo marcado ahora mismo. NonSerialized: estado de runtime por instancia otorgada.
    [System.NonSerialized] private AbilitySystemComponent _marked;

    // =========================================================
    // ACTIVACIÓN
    // =========================================================

    // Sin enemigo en la mira no se activa (ni anima, ni gasta): igual que el Enemigo jurado.
    public override bool CanActivate()
    {
        if (!base.CanActivate()) return false;
        return FindBestTargetInAim(MaxRange, SelectionAngle, ETargetAffiliation.Enemies) != null;
    }

    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;

        AbilitySystemComponent target = FindBestTargetInAim(MaxRange, SelectionAngle, ETargetAffiliation.Enemies);
        if (target == null)
        {
            EndAbility();
            return;
        }

        if (MarkEffect == null || StunEffect == null)
        {
            Debug.LogWarning($"[{AbilityName}] le falta MarkEffect o StunEffect: no se lanza.");
            EndAbility();
            return;
        }

        CommitAbility();

        ClearMark();   // una marca a la vez

        target.ApplyGameplayEffect(MarkEffect, OwnerASC);
        _marked = target;
        _marked.OnDealtDamage += HandleMarkedDealtDamage;

        OwnerASC.StartAbilityCoroutine(MarkRoutine(MarkEffect.Duration));

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.RotateToAim();
            pc.PlayAnimation(this);
        }

        PlayImpactVFXOnTarget(target);

        EndAbility();
    }

    // =========================================================
    // LA MARCA
    // =========================================================

    // Vigila mientras dure la marca; la corta antes si el marcado o el Clérigo mueren.
    private IEnumerator MarkRoutine(float duration)
    {
        AbilitySystemComponent watched = _marked;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // Ya se consumió (le pegó a un aliado) o se re-marcó a otro: esta vigilancia terminó.
            if (_marked == null || _marked != watched || OwnerASC == null) yield break;
            if (_marked.HasTag(EGameplayTag.State_Dead) || OwnerASC.HasTag(EGameplayTag.State_Dead)) break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (_marked == watched) ClearMark();
    }

    // Suelta la marca: desuscribe el gancho y le saca el efecto. Idempotente.
    private void ClearMark()
    {
        if (_marked == null) return;

        _marked.OnDealtDamage -= HandleMarkedDealtDamage;
        if (MarkEffect != null) _marked.RemoveEffectsByDefinition(MarkEffect);
        _marked = null;
    }

    // El marcado le pegó a alguien. Si es aliado del Clérigo (o él mismo): aturdido, y
    // la marca se consume. Corre en el servidor (el pipeline de daño es server-side).
    private void HandleMarkedDealtDamage(AbilitySystemComponent victim, float damageDealt)
    {
        if (victim == null || OwnerASC == null || _marked == null) return;
        if (!OwnerASC.IsAllyOf(victim, includeSelf: true)) return;

        AbilitySystemComponent stunned = _marked;
        ClearMark();
        stunned.ApplyGameplayEffect(StunEffect, OwnerASC);
    }

    // =========================================================
    // VISUALES Y GIZMOS
    // =========================================================

    public override void DrawGizmos(Transform origin)
    {
        if (origin == null) return;
        Gizmos.color = new Color(0.4f, 0.6f, 1f, 0.9f);
        Gizmos.DrawWireSphere(origin.position, MaxRange);
    }
}
