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
//    Modifier.UseTargetHealthScaling (MissingHealth, coeficiente negativo), cargado
//    en la lista de efectos como "Al golpear → Enemigos".
//  - El teletransporte usa NetworkAbilitySystemComponent.ServerTeleportOwnerTo
//    (el transform es client-authoritative: lo ejecuta el dueño).
//  - Volver a invisible es una entrada "Al matar → El lanzador" con el GE de
//    invisibilidad.
//
// Como toda GameplayAbility, Activate() corre en el servidor.
// ============================================================
[CreateAssetMenu(fileName = "GA_MarkedForDeath", menuName = "GAS/Specific Abilities/Rogue/Assassin/Marked For Death")]
public class GA_MarkedForDeath : TargetImpactAbility, IGroundTargetAbility
{
    [Section(AbilitySection.Shape)]
    [Tooltip("Distancia máxima a la que se puede marcar la zona.")]
    public float MaxRange = 15f;
    [Tooltip("Radio de la zona: a quiénes alcanza el daño.")]
    public float ZoneRadius = 5f;

    // IGroundTargetAbility: mantener el botón muestra la "X" en el suelo siguiendo
    // la mira, y al soltar se lanza sobre esa zona. Ver UI_GroundTargetIndicator.
    public float MaxTargetRange => MaxRange;
    public float TargetRadius   => ZoneRadius;
    public bool  UsesGroundTarget => true; // esta habilidad SIEMPRE se apunta

    public override float VisualAreaRadius => ZoneRadius;
    public override bool SupportsVisualTiming(EVisualWhen when) => true;

    // Valida (incluye el requisito de estar invisible, vía ActivationRequiredTags),
    // teletransporta a la zona y le aplica la lista de efectos a todos los enemigos
    // dentro (con lo de "al matar" si mata a alguno).
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

        BroadcastImpactVFX(zoneCenter);

        // Daño a todos los enemigos de la zona (autoridad de servidor). ApplyHitEffects
        // es síncrono: si el golpe mata, aplica en el acto lo de "al matar" (la
        // invisibilidad de vuelta).
        Collider[] cols = Physics.OverlapSphere(zoneCenter, ZoneRadius, TargetLayer);
        var seen = new HashSet<AbilitySystemComponent>();

        foreach (var c in cols)
        {
            AbilitySystemComponent target = c.GetComponentInParent<AbilitySystemComponent>();
            if (target == null || ReferenceEquals(target, OwnerASC) || !IsEnemy(target) || !seen.Add(target)) continue;
            if (target.HasTag(EGameplayTag.State_Dead)) continue;

            ApplyHitEffects(target, firstHit: seen.Count == 1);
            ChargeUltimate();

            BroadcastHitVFX(target, withSound: false);
        }

        EndAbility();
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

#if UNITY_EDITOR
    public override void DrawSceneHandles(Transform origin)
    {
        if (origin == null) return;

        AbilityHandles.Distance(this, "Max Range", origin.position, origin.forward, ref MaxRange,
                                AbilityHandles.DistanceColor);
        AbilityHandles.Radius(this, "Zone Radius", origin.position + origin.forward * MaxRange, ref ZoneRadius,
                              AbilityHandles.RadiusColor, origin.right);
    }
#endif

    // =========================================================
    // DATOS VIEJOS (solo para pasarlos a las listas; ver GameplayAbility.UpgradeLegacyData)
    // =========================================================

    [SerializeField, HideInInspector] private GameplayEffect DamageEffect;
    [SerializeField, HideInInspector] private List<GameplayEffect> AdditionalEffects;
    [SerializeField, HideInInspector] private GameplayEffect InvisibilityOnKillEffect;

    protected override void OnUpgradeLegacyData(ref bool changed)
    {
        base.OnUpgradeLegacyData(ref changed);

        UpgradeEffect(ref DamageEffect, EEffectWhen.OnHit, EEffectTarget.Enemies, ref changed);
        UpgradeEffects(AdditionalEffects, EEffectWhen.OnHit, EEffectTarget.Enemies, ref changed);
        UpgradeEffect(ref InvisibilityOnKillEffect, EEffectWhen.OnKill, EEffectTarget.Self, ref changed);
    }
}
