using UnityEngine;
using System.Collections.Generic;

// ============================================================
// GA_Blink  (Golpe mortal del Pícaro)
//
// Habilidad de objetivo único: elige al enemigo que el jugador tiene en la
// mira (el más centrado dentro de un ángulo/rango), se teletransporta a su
// ESPALDA y le inflige daño en base a un porcentaje de su vida faltante (eso
// lo configura el GameplayEffect vía Modifier.UseTargetHealthScaling). Si el
// golpe MATA al objetivo, reinicia su propio cooldown.
//
// Como toda GameplayAbility del proyecto, Activate() corre en el servidor
// (autoridad de daño). El teletransporte del CharacterController, en cambio,
// tiene que ejecutarse en el proceso dueño — se delega en
// NetworkAbilitySystemComponent.ServerBlinkOwnerTo (mismo patrón que el salto).
// ============================================================
[CreateAssetMenu(fileName = "GA_Blink", menuName = "GAS/Rogue/Blink Strike")]
public class GA_Blink : GameplayAbility
{
    [Header("Selección de Objetivo")]
    [Tooltip("Alcance máximo para buscar al enemigo objetivo.")]
    public float MaxRange = 12f;
    [Tooltip("Ángulo máximo (grados) entre la mira y el enemigo para que cuente como objetivo.")]
    public float SelectionAngle = 25f;

    [Header("Teletransporte")]
    [Tooltip("A qué distancia por detrás del enemigo aparece el jugador.")]
    public float BehindDistance = 1.5f;

    [Header("Efectos")]
    [Tooltip("Daño a aplicar. Para que pegue según la vida faltante del objetivo, " +
             "en su Modifier activá UseTargetHealthScaling (MissingHealth) y ajustá el coeficiente.")]
    public GameplayEffect DamageEffect;
    // Efectos EXTRA que se aplican al objetivo además del daño (heridas, marcar, etc.). Opcional.
    public List<GameplayEffect> AdditionalEffects;

    [Header("Visuales")]
    public GameObject ImpactVFX;

    // Valida, elige objetivo, teletransporta detrás de él, aplica el daño y
    // (si lo mata) reinicia el cooldown.
    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate())
        {
            Debug.Log("[GA_Blink] Activate abortado: CanActivate() = false (en cooldown, sin maná, o tag bloqueante).");
            return;
        }

        Debug.Log("[GA_Blink] Activate (servidor). Buscando objetivo...");
        AbilitySystemComponent target = FindTarget();

        // Sin objetivo válido: no gastamos cooldown/costo. Liberamos el estado
        // "atacando" del dueño (EndAbility avisa al host y a los clientes) y salimos.
        if (target == null)
        {
            Debug.LogWarning("[GA_Blink] SIN OBJETIVO — no encontró un enemigo dentro del ángulo/rango de la mira. " +
                             "Revisá: (1) TargetLayer del GA_Blink = la capa de los personajes, (2) MaxRange, (3) SelectionAngle.");
            EndAbility();
            return;
        }

        Debug.Log($"[GA_Blink] Objetivo elegido: '{target.name}' (equipo {target.TeamID}). Comprometiendo habilidad.");
        CommitAbility();

        // Punto a la espalda del objetivo (según hacia dónde MIRA el objetivo),
        // manteniendo su altura para no aparecer flotando o clavado en el piso.
        Vector3 behind = target.transform.position - target.transform.forward * BehindDistance;
        behind.y = target.transform.position.y;
        Vector3 faceDir = target.transform.position - behind;

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();

        // El teletransporte se ejecuta en el proceso dueño (CC client-authoritative).
        Debug.Log($"[GA_Blink] Teletransportando a {behind} (detrás de '{target.name}'). netAsc={(netAsc != null)}, pc={(pc != null)}.");
        if (netAsc != null) netAsc.ServerBlinkOwnerTo(behind, faceDir);
        else if (pc != null) pc.TeleportTo(behind, faceDir); // fallback sin red

        if (pc != null) pc.PlayAnimation(AnimationTriggerName, AnimationID);

        // Daño (con autoridad de servidor). Guardamos si el objetivo ya estaba
        // muerto para saber si fue ESTA habilidad la que lo mató.
        float hpBefore = target.GetAttributeValue(EAttributeType.Health);
        bool wasDead = target.HasTag(EGameplayTag.State_Dead);
        if (DamageEffect != null) target.ApplyGameplayEffect(DamageEffect, OwnerASC);
        else Debug.LogWarning("[GA_Blink] DamageEffect es null — el blink no hace daño (asignalo en el GA).");
        ApplyEffectsTo(AdditionalEffects, target);
        ChargeUltimate();
        Debug.Log($"[GA_Blink] Daño aplicado a '{target.name}': vida {hpBefore} -> {target.GetAttributeValue(EAttributeType.Health)}.");

        Vector3 hitPos = target.transform.position + Vector3.up;
        if (netAsc != null) netAsc.ServerPlayAbilityVFX(this, hitPos);
        else PlayImpactVFX(hitPos);

        // Si el golpe mató al objetivo, reiniciar el cooldown (dejar la habilidad
        // lista de nuevo). ApplyGameplayEffect es síncrono, así que el tag de
        // muerte ya está puesto si lo mató.
        bool killed = !wasDead && target.HasTag(EGameplayTag.State_Dead);
        if (killed && CooldownEffect != null && CooldownEffect.GrantedTags.Count > 0)
            OwnerASC.ReduceCooldownByTag(CooldownEffect.GrantedTags[0], 99999f);

        EndAbility();
    }

    // Elige al enemigo más alineado con la mira del jugador, dentro de MaxRange y
    // SelectionAngle. En el servidor, GetAimPoint() usa el NetworkAimPoint que el
    // dueño envió con el input (ver ServerActivateAbility).
    private AbilitySystemComponent FindTarget()
    {
        if (OwnerASC == null) return null;

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        Vector3 origin   = OwnerASC.transform.position;
        Vector3 aimPoint = pc != null ? pc.GetAimPoint(MaxRange) : origin + OwnerASC.transform.forward * MaxRange;

        Vector3 aimDir = aimPoint - origin; aimDir.y = 0;
        if (aimDir.sqrMagnitude < 0.0001f) aimDir = OwnerASC.transform.forward;
        aimDir.Normalize();

        Collider[] cols = Physics.OverlapSphere(origin, MaxRange, TargetLayer);
        float threshold = Mathf.Cos(SelectionAngle * Mathf.Deg2Rad); // umbral: dentro del ángulo

        Debug.Log($"[GA_Blink] FindTarget: origin={origin}, aimPoint={aimPoint}, aimDir={aimDir}. " +
                  $"OverlapSphere(rango={MaxRange}, TargetLayer.value={TargetLayer.value}) encontró {cols.Length} colliders. " +
                  $"Umbral de alineación (cos {SelectionAngle}°)={threshold:F3}.");

        if (cols.Length == 0)
            Debug.LogWarning("[GA_Blink] 0 colliders — casi seguro el TargetLayer del GA no incluye la capa de los personajes.");

        AbilitySystemComponent best = null;
        float bestAlign = threshold;

        foreach (var c in cols)
        {
            AbilitySystemComponent asc = c.GetComponentInParent<AbilitySystemComponent>();
            if (asc == null) continue;

            bool enemy = IsEnemy(asc);
            bool dead  = asc.HasTag(EGameplayTag.State_Dead);
            if (!enemy || dead)
            {
                Debug.Log($"[GA_Blink]  · descartado '{asc.name}': esEnemigo={enemy}, muerto={dead} (mi equipo={OwnerASC.TeamID}, su equipo={asc.TeamID}).");
                continue;
            }

            Vector3 toTarget = asc.transform.position - origin; toTarget.y = 0;
            if (toTarget.sqrMagnitude < 0.0001f) continue;

            float align = Vector3.Dot(aimDir, toTarget.normalized);
            Debug.Log($"[GA_Blink]  · candidato '{asc.name}': alineación={align:F3} (necesita > {threshold:F3}). {(align > bestAlign ? "→ mejor hasta ahora" : "no pasa/no mejora")}");
            if (align > bestAlign) { bestAlign = align; best = asc; } // el más centrado en la mira
        }

        Debug.Log($"[GA_Blink] FindTarget resultado: {(best != null ? "'" + best.name + "'" : "NINGUNO")}.");
        return best;
    }

    // Instancia ImpactVFX en el punto de impacto. La llama cada peer con su
    // propia copia (ver NetworkAbilitySystemComponent.ServerPlayAbilityVFX).
    public override void PlayImpactVFX(Vector3 position)
    {
        if (ImpactVFX == null) return;
        GameObject vfx = Instantiate(ImpactVFX, position, Quaternion.identity);
        Destroy(vfx, 2.0f);
    }

    // Vista previa del alcance de selección en el Editor.
    public override void DrawGizmos(Transform origin)
    {
        if (origin == null) return;
        Gizmos.color = new Color(0.6f, 0.1f, 0.8f, 0.9f);
        Gizmos.DrawWireSphere(origin.position, MaxRange);
    }
}
