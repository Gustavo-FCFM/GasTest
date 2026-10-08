using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ============================================================
// GA_InstantAoE
//
// Golpe de área de UNA sola aplicación: revisa quién está dentro del radio y le
// aplica la lista de efectos una vez. Es la versión instantánea de
// GA_ContinuousAoE (misma configuración: área, efectos, dónde se despliega), sin
// duración ni ticks. Pensada para explosiones, ondas expansivas, bombas, etc.
//
// DÓNDE SE DESPLIEGA (DeployMode): sobre el propio dueño, o en la ZONA APUNTADA con
// la retícula (mantener el botón → marcador en el suelo → soltar, ver
// IGroundTargetAbility).
//
// StartDelay sirve para sincronizar el impacto con la animación (el golpe cae recién
// después de ese tiempo). El punto de la zona se resuelve al ACTIVAR, así que apuntar
// a otro lado durante el delay no cambia dónde cae.
//
// A QUIÉN AFECTA lo dice cada entrada de la lista (enemigos, aliados, todos). El VFX
// del estallido es una entrada "En el impacto" de la lista de VFX, con "Calzar con el
// área" para que mida lo mismo que el radio.
//
// GOLPES EXTRA POR ACUMULACIÓN (ExtraHitsPerStack): con las acumulaciones que leyó al
// activarse (sección Acumulaciones), repite el golpe — los Cortes devastadores del Samurái
// cortan una vez más por cada acumulación de Artes marciales.
//
// Si en cambio querés una zona que PERSISTA aplicando efectos cada tanto (charco de
// veneno, aura, la zona de los Cañones del Pirata), usá GA_ContinuousAoE.
// ============================================================
[CreateAssetMenu(fileName = "GA_InstantAoE", menuName = "GAS/Generics/Instant AoE")]
public class GA_InstantAoE : GameplayAbility, IGroundTargetAbility
{
    [Section(AbilitySection.Shape)]
    [Tooltip("Radio del área, en metros.")]
    public float Radius = 4f;

    [Tooltip("AtOwner: el área estalla sobre el dueño. AtReticle: se apunta con el marcador " +
             "en el suelo (mantener → apuntar → soltar) y cae ahí.")]
    public GA_ContinuousAoE.EAoEDeploy DeployMode = GA_ContinuousAoE.EAoEDeploy.AtOwner;

    [ShowIf(nameof(DeployMode), GA_ContinuousAoE.EAoEDeploy.AtReticle)]
    [Tooltip("Alcance máximo al que se puede lanzar la zona.")]
    public float MaxRange = 15f;

    // IGroundTargetAbility: valores del marcador del suelo (solo se muestra si
    // DeployMode es AtReticle).
    public float MaxTargetRange   => MaxRange;
    public float TargetRadius     => Radius;
    public bool  UsesGroundTarget => DeployMode == GA_ContinuousAoE.EAoEDeploy.AtReticle;

    [Section(AbilitySection.Timing)]
    [Tooltip("Espera antes de que el golpe caiga (para acompañar la animación). Se ajusta " +
             "por la velocidad de ataque, igual que en las demás habilidades.")]
    public float StartDelay = 0f;

    [Tooltip("Golpes EXTRA por cada acumulación leída (sección Acumulaciones). Con 1: sin " +
             "acumulaciones, un golpe; con 4, cinco (los Cortes devastadores del Samurái). Cada " +
             "golpe extra repite la animación y vuelve a aplicar los efectos 'al activarse' (la " +
             "curación por corte). 0 = siempre un solo golpe.")]
    public int ExtraHitsPerStack = 0;

    [ShowIf(nameof(ExtraHitsPerStack), ShowIfAttribute.Positive)]
    [Tooltip("Segundos entre un golpe y el siguiente (se ajusta con la velocidad de ataque).")]
    public float RepeatInterval = 0.3f;

    public override float VisualAreaRadius => Radius;
    public override bool SupportsVisualTiming(EVisualWhen when) => true;

    // Valida, cobra costo/cooldown y programa el impacto.
    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;

        CommitAbility();

        if (OwnerASC == null) return;

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();

        // El centro se resuelve ACÁ, al activar (ver nota de cabecera sobre StartDelay).
        Vector3 center = ResolveCenter(pc);

        if (pc != null) pc.PlayAnimation(this);

        // Un golpe, más los extra por las acumulaciones que leyó CommitAbility.
        int hits = 1 + Mathf.Max(0, StackSnapshot) * Mathf.Max(0, ExtraHitsPerStack);

        if (StartDelay > 0f || hits > 1) OwnerASC.StartAbilityCoroutine(ImpactRoutine(center, hits));
        else                             { Detonate(center); EndAbility(); }
    }

    // Punto donde cae el área: el dueño, o la zona apuntada (recortada a MaxRange
    // para que coincida con la vista previa del marcador).
    private Vector3 ResolveCenter(PlayerController pc)
    {
        Vector3 origin = OwnerASC.transform.position;
        if (DeployMode == GA_ContinuousAoE.EAoEDeploy.AtOwner) return origin;

        Vector3 center = pc != null ? pc.GetAimPoint(MaxRange)
                                    : origin + OwnerASC.transform.forward * MaxRange;

        Vector3 toZone = center - origin;
        if (toZone.magnitude > MaxRange) center = origin + toZone.normalized * MaxRange;
        return center;
    }

    // Espera StartDelay (ajustado por velocidad de ataque) y detona; si hay golpes extra
    // (ExtraHitsPerStack), los encadena cada RepeatInterval. Sobre el dueño, cada golpe sale
    // de donde está AHORA (se puede mover entre corte y corte). Aturdido o muerto, corta.
    private IEnumerator ImpactRoutine(Vector3 center, int hits)
    {
        float speedMultiplier = 1f;
        float atkSpeedStat = OwnerASC.GetAttributeValue(EAttributeType.AtkSpeed);
        if (atkSpeedStat > 0) speedMultiplier = 1f / atkSpeedStat;

        if (StartDelay > 0f) yield return new WaitForSeconds(StartDelay / speedMultiplier);

        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();

        for (int i = 0; i < hits; i++)
        {
            if (i > 0)
            {
                yield return new WaitForSeconds(RepeatInterval / speedMultiplier);

                if (OwnerASC == null) yield break;
                if (OwnerASC.HasTag(EGameplayTag.State_Dead) || OwnerASC.HasTag(EGameplayTag.State_Stunned)) break;

                if (DeployMode == GA_ContinuousAoE.EAoEDeploy.AtOwner) center = OwnerASC.transform.position;

                // Cada golpe extra: su animación (en todas las pantallas, también el dueño:
                // no lo pudo anticipar) y sus efectos "al activarse" (la curación por corte).
                if (netAsc != null) netAsc.ServerPlayAbilityAnimationOnAll(this);
                ApplyActivationEffects();
            }

            Detonate(center);
        }

        EndAbility();
    }

    // Aplica los efectos UNA vez a cada objetivo válido dentro del radio y reproduce
    // el VFX de impacto en todos los peers.
    private void Detonate(Vector3 center)
    {
        BroadcastImpactVFX(center);

        Collider[] hits = Physics.OverlapSphere(center, Radius, TargetLayer);
        var seen = new HashSet<AbilitySystemComponent>();
        bool firstEnemy = true;

        foreach (var hit in hits)
        {
            AbilitySystemComponent targetASC = hit.GetComponentInParent<AbilitySystemComponent>();
            // Un mismo personaje puede tener varios colliders: sin este filtro le
            // aplicaríamos los efectos más de una vez (y esto es de UNA sola aplicación).
            if (targetASC == null || !seen.Add(targetASC)) continue;

            bool enemy = IsEnemy(targetASC);
            if (!ApplyHitEffects(targetASC, firstHit: enemy && firstEnemy)) continue;

            // El sonido ya sonó una vez en el estallido: acá solo los VFX de golpe.
            BroadcastHitVFX(targetASC, withSound: false);

            if (!enemy) continue;
            firstEnemy = false;

            OnTargetHit(targetASC);
            if (OwnerASC.CompareTag("Player")) ChargeUltimate();
        }
    }

    // Gancho para que una habilidad concreta reaccione a cada enemigo alcanzado
    // (ej: los Cañones del Pirata, que además le apuestan a quien golpean).
    protected virtual void OnTargetHit(AbilitySystemComponent target) { }

    // Vista previa del área en el Editor.
    public override void DrawGizmos(Transform origin)
    {
        if (origin == null) return;

        Vector3 center = DeployMode == GA_ContinuousAoE.EAoEDeploy.AtReticle
            ? origin.position + origin.forward * MaxRange
            : origin.position;

        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.35f);
        Gizmos.DrawSphere(center, Radius);
    }

#if UNITY_EDITOR
    public override void DrawSceneHandles(Transform origin)
    {
        if (origin == null) return;

        Vector3 center = origin.position;
        if (DeployMode == GA_ContinuousAoE.EAoEDeploy.AtReticle)
        {
            AbilityHandles.Distance(this, "Max Range", origin.position, origin.forward, ref MaxRange,
                                    AbilityHandles.DistanceColor);
            center = origin.position + origin.forward * MaxRange;
        }
        AbilityHandles.Radius(this, "Radius", center, ref Radius, AbilityHandles.RadiusColor, origin.right);
    }
#endif

    // =========================================================
    // DATOS VIEJOS (solo para pasarlos a las listas; ver GameplayAbility.UpgradeLegacyData)
    // =========================================================

    [SerializeField, HideInInspector] private GA_ContinuousAoE.EAoETarget Targets = GA_ContinuousAoE.EAoETarget.Enemies;
    [SerializeField, HideInInspector] private List<GameplayEffect> EffectsToApply;
    [SerializeField, HideInInspector] private GameObject VisualPrefab;
    [SerializeField, HideInInspector] private float VisualScaleMultiplier = 2f;
    [SerializeField, HideInInspector] private float VisualLifetime = 2f;

    protected override void OnUpgradeLegacyData(ref bool changed)
    {
        base.OnUpgradeLegacyData(ref changed);

        UpgradeEffects(EffectsToApply, EEffectWhen.OnHit, GA_ContinuousAoE.ToEffectTarget(Targets), ref changed);
        UpgradeVisual(ref VisualPrefab, new AbilityVisual
        {
            When = EVisualWhen.OnImpact, MatchAreaSize = true,
            AreaSizeMultiplier = VisualScaleMultiplier, DestroyTime = VisualLifetime,
        }, ref changed);
    }
}
