using UnityEngine;
using System.Collections.Generic;

// ============================================================
// GA_MarkedForDeath  (Marcado para morir — ultimate del Asesino)
//
// Solo se puede lanzar ESTANDO INVISIBLE (se configura con
// ActivationRequiredTags = [Status_Invisible] en el asset, no está hardcodeado).
// El jugador marca una zona con la mira, se teletransporta a su centro, y todos
// los enemigos dentro reciben daño en base a su vida faltante. Si mata al menos a
// uno, el jugador vuelve a hacerse invisible.
//
// Casi todo sale de piezas que ya existen:
//  - El daño "= vida faltante" lo hace el GameplayEffect con
//    Modifier.UseTargetHealthScaling (MissingHealth, coeficiente negativo).
//  - El teletransporte usa NetworkAbilitySystemComponent.ServerTeleportOwnerTo
//    (el transform es client-authoritative: lo ejecuta el dueño).
//  - Volver a invisible es simplemente re-aplicarse el GE de invisibilidad.
//
// Como toda GameplayAbility, Activate() corre en el servidor.
// ============================================================
[CreateAssetMenu(fileName = "GA_MarkedForDeath", menuName = "GAS/Specific Abilities/Assassin/Marked For Death")]
public class GA_MarkedForDeath : GameplayAbility, IGroundTargetAbility
{
    [Header("Zona Objetivo")]
    [Tooltip("Distancia máxima a la que se puede marcar la zona.")]
    public float MaxRange = 15f;
    [Tooltip("Radio de la zona: a quiénes alcanza el daño.")]
    public float ZoneRadius = 5f;

    // IGroundTargetAbility: mantener el botón muestra la "X" en el suelo siguiendo
    // la mira, y al soltar se lanza sobre esa zona. Ver UI_GroundTargetIndicator.
    public float MaxTargetRange => MaxRange;
    public float TargetRadius   => ZoneRadius;
    public bool  UsesGroundTarget => true; // esta habilidad SIEMPRE se apunta

    [Header("Efectos")]
    [Tooltip("Daño a cada enemigo de la zona. Para que pegue por su vida faltante, en su " +
             "Modifier activá UseTargetHealthScaling (MissingHealth) con coeficiente NEGATIVO " +
             "(ej: -1 = el 100% de la vida faltante).")]
    public GameplayEffect DamageEffect;
    [Tooltip("Efectos EXTRA para cada enemigo de la zona. Opcional.")]
    public List<GameplayEffect> AdditionalEffects;

    [Tooltip("Se re-aplica al propio jugador si MATÓ al menos a un enemigo con esta " +
             "habilidad (el GE de invisibilidad del Asesino).")]
    public GameplayEffect InvisibilityOnKillEffect;

    [Header("Visuales")]
    public GameObject ImpactVFX;

    // Valida (incluye el requisito de estar invisible, vía ActivationRequiredTags),
    // teletransporta a la zona, daña a todos los enemigos dentro, y re-invisibiliza
    // si mató a alguno.
    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;

        CommitAbility();

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();

        // Centro de la zona: el punto que el jugador marcó con la mira, acotado a
        // MaxRange para que no pueda teletransportarse al otro lado del mapa.
        Vector3 origin = OwnerASC.transform.position;
        Vector3 zoneCenter = pc != null ? pc.GetAimPoint(MaxRange) : origin + OwnerASC.transform.forward * MaxRange;

        Vector3 toZone = zoneCenter - origin;
        if (toZone.magnitude > MaxRange) zoneCenter = origin + toZone.normalized * MaxRange;

        // Teletransporte al centro (lo ejecuta el dueño; el transform es suyo).
        Vector3 faceDir = toZone; faceDir.y = 0;
        if (netAsc != null)  netAsc.ServerTeleportOwnerTo(zoneCenter, faceDir);
        else if (pc != null) pc.TeleportTo(zoneCenter, faceDir); // fallback sin red

        if (pc != null) pc.PlayAnimation(this);

        // Daño a todos los enemigos de la zona (autoridad de servidor).
        Collider[] cols = Physics.OverlapSphere(zoneCenter, ZoneRadius, TargetLayer);
        var seen   = new HashSet<AbilitySystemComponent>();
        bool killedAny = false;

        foreach (var c in cols)
        {
            AbilitySystemComponent target = c.GetComponentInParent<AbilitySystemComponent>();
            if (target == null || ReferenceEquals(target, OwnerASC) || !IsEnemy(target) || !seen.Add(target)) continue;
            if (target.HasTag(EGameplayTag.State_Dead)) continue;

            if (DamageEffect != null) target.ApplyGameplayEffect(DamageEffect, OwnerASC);
            ApplyEffectsTo(AdditionalEffects, target);
            ChargeUltimate();

            // ApplyGameplayEffect es síncrono: si lo mató, el tag ya está puesto.
            if (target.HasTag(EGameplayTag.State_Dead)) killedAny = true;

            Vector3 hitPos = target.transform.position + Vector3.up;
            if (netAsc != null) netAsc.ServerPlayAbilityVFX(this, hitPos);
            else PlayImpactVFX(hitPos);
        }

        // "Si al menos un enemigo muere, el jugador se vuelve invisible."
        if (killedAny && InvisibilityOnKillEffect != null)
            OwnerASC.ApplyGameplayEffect(InvisibilityOnKillEffect, OwnerASC);

        EndAbility();
    }

    // Instancia ImpactVFX en cada impacto. La llama cada peer con su propia copia
    // (ver NetworkAbilitySystemComponent.ServerPlayAbilityVFX).
    public override void PlayImpactVFX(Vector3 position)
    {
        if (ImpactVFX == null) return;
        GameObject vfx = Instantiate(ImpactVFX, position, Quaternion.identity);
        Destroy(vfx, 2f);
    }

    // Vista previa: alcance de marcado y tamaño de la zona (dibujada al frente,
    // ya que en el Editor no sabemos a dónde va a apuntar el jugador).
    public override void DrawGizmos(Transform origin)
    {
        if (origin == null) return;

        Gizmos.color = new Color(0.9f, 0.1f, 0.3f, 0.9f);
        Gizmos.DrawWireSphere(origin.position, MaxRange);

        Vector3 preview = origin.position + origin.forward * MaxRange;
        Gizmos.color = new Color(0.9f, 0.1f, 0.3f, 0.25f);
        Gizmos.DrawSphere(preview, ZoneRadius);
    }
}
