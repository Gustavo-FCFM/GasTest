using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ============================================================
// GA_HeroicLeap  (definitiva del Comandante — Salto heroico)
//
// Se apunta la zona con el marcador del suelo (mantener R → soltar), igual que el
// Destello del Clérigo: el destino seguro (piso, que quepa, sin cruzar paredes) lo
// resuelve GA_Teleport. Lo que cambia es el viaje:
//
//   1. Despega: el clip de la habilidad (AnimationClip, el comienzo de un salto).
//   2. A los JumpDelay segundos APARECE en el destino (no es un salto físico: es un
//      teletransporte con animación) y aterriza (LandClip).
//   3. Golpea a los enemigos en ImpactRadius alrededor del punto de llegada (la lista de
//      efectos de la habilidad).
//   4. Clava la bandera: BannerAbility, una zona (GA_ContinuousAoE) que nace fija en el
//      punto de llegada y daña a los enemigos / potencia a los aliados mientras dura.
//
// Los VFX "en el impacto" salen solo en la llegada, a los pies.
// ============================================================
[CreateAssetMenu(fileName = "GA_HeroicLeap", menuName = "GAS/Specific Abilities/Fighter/Commander/Heroic Leap")]
public class GA_HeroicLeap : GA_Teleport
{
    [Section(AbilitySection.Shape)]
    [Tooltip("Radio del golpe alrededor del punto de llegada. También conviene ponerlo en " +
             "Marker Radius, así el marcador muestra a quién va a alcanzar.")]
    public float ImpactRadius = 3f;

    [Section(AbilitySection.Timing)]
    [Tooltip("Segundos desde que despega hasta que aparece en el destino.")]
    public float JumpDelay = 0.35f;

    [Tooltip("Segundos que queda ocupado después de aterrizar, antes de poder actuar.")]
    public float LandRecovery = 0.5f;

    [Section("Bandera")]
    [Tooltip("La zona que se clava al llegar. Nace FIJA en el punto de llegada (ponerle " +
             "FollowOwner apagado) y sin animación propia.")]
    public GA_ContinuousAoE BannerAbility;

    [Section(AbilitySection.Animation)]
    [Tooltip("Clip del aterrizaje (el despegue es el AnimationClip de la habilidad).")]
    public AnimationClip LandClip;

    // A diferencia del teletransporte, este sí golpea al llegar.
    public override bool UsesTargetLayer => true;
    public override bool UsesHitEffects  => true;
    public override float VisualAreaRadius => ImpactRadius;

    // Para el RPC de animación de los pasos: 0 = despegue, 1 = aterrizaje.
    public override AnimationClip GetStepAnimationClip(int sequenceIndex, int stepIndex)
        => stepIndex == 1 ? LandClip : AnimationClip;

    protected override void ExecuteTeleport(PlayerController pc, NetworkAbilitySystemComponent netAsc,
                                            Vector3 origin, Vector3 landing, Vector3 faceDir)
    {
        // El despegue. Los demás lo reciben por el aviso normal de la activación.
        if (pc != null) pc.PlayAnimation(this);

        OwnerASC.StartAbilityCoroutine(LeapRoutine(pc, netAsc, landing, faceDir));
    }

    private IEnumerator LeapRoutine(PlayerController pc, NetworkAbilitySystemComponent netAsc,
                                    Vector3 landing, Vector3 faceDir)
    {
        if (JumpDelay > 0f) yield return new WaitForSeconds(JumpDelay);
        if (OwnerASC == null) yield break;

        // Si lo mataron en el despegue, no llega.
        if (OwnerASC.HasTag(EGameplayTag.State_Dead))
        {
            EndAbility();
            yield break;
        }

        // Aparece en el destino (el dueño mueve su propio transform).
        if (netAsc != null)  netAsc.ServerTeleportOwnerTo(landing, faceDir);
        else if (pc != null) pc.TeleportTo(landing, faceDir);

        BroadcastImpactVFX(landing);

        PlayLandAnimation(pc, netAsc);
        HitAround(landing);
        PlantBanner(landing);

        if (LandRecovery > 0f) yield return new WaitForSeconds(LandRecovery);
        EndAbility();
    }

    // El aterrizaje en todas las pantallas: el RPC de los pasos de combo lo manda al dueño
    // remoto y a los demás, pero saltea al personaje propio del host, que se anima acá.
    private void PlayLandAnimation(PlayerController pc, NetworkAbilitySystemComponent netAsc)
    {
        if (LandClip == null) return;

        if (pc != null && (!pc.IsSpawned || pc.IsOwner))
            pc.PlayActionClip(LandClip, 1f, AnimationTriggerName, AnimationID);

        if (netAsc != null) netAsc.ServerBroadcastComboStepAnimation(this, 0, 1, this);
    }

    private void HitAround(Vector3 center)
    {
        if (ImpactRadius <= 0f) return;

        var done = new HashSet<AbilitySystemComponent>();
        bool first = true;
        foreach (Collider c in Physics.OverlapSphere(center, ImpactRadius, TargetLayer))
        {
            AbilitySystemComponent target = c.GetComponentInParent<AbilitySystemComponent>();
            if (target == null || !done.Add(target)) continue;
            if (!IsEnemy(target) || target.HasTag(EGameplayTag.State_Dead)) continue;

            ApplyHitEffects(target, firstHit: first);
            BroadcastHitVFX(target, withSound: false);
            first = false;
        }
    }

    // La bandera se clona y se ejecuta como un paso de combo: sin cooldown propio (el de
    // la definitiva ya se cobró) y con el template como identidad para la red (su VFX).
    private void PlantBanner(Vector3 point)
    {
        if (BannerAbility == null) return;

        GA_ContinuousAoE banner = Instantiate(BannerAbility);
        banner.Initialize(OwnerASC);
        banner.SourceTemplate = BannerAbility;
        banner.CooldownEffect = null;
        banner.CostEffect     = null;
        banner.DisableCharges();
        banner.ActivateAt(point);
    }

    public override void DrawGizmos(Transform origin)
    {
        base.DrawGizmos(origin);
        if (origin == null) return;

        // El golpe, dibujado en el tope del alcance hacia adelante.
        Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(origin.position + origin.forward * MaxRange, ImpactRadius);
    }

#if UNITY_EDITOR
    public override void DrawSceneHandles(Transform origin)
    {
        base.DrawSceneHandles(origin);
        if (origin == null) return;

        AbilityHandles.Radius(this, "Impact Radius", origin.position + origin.forward * MaxRange, ref ImpactRadius,
                              AbilityHandles.RadiusColor, origin.right);
    }
#endif

    // =========================================================
    // DATOS VIEJOS (solo para pasarlos a las listas; ver GameplayAbility.UpgradeLegacyData)
    // =========================================================

    [SerializeField, HideInInspector] private GameplayEffect ImpactDamageEffect;
    [SerializeField, HideInInspector] private List<GameplayEffect> ImpactEffects;

    // El destello del salto salía a los pies, donde cae (el del teletransporte, en el pecho).
    protected override Vector3 LegacyFlashOffset => Vector3.zero;

    protected override void OnUpgradeLegacyData(ref bool changed)
    {
        base.OnUpgradeLegacyData(ref changed);

        UpgradeEffect(ref ImpactDamageEffect, EEffectWhen.OnHit, EEffectTarget.Enemies, ref changed);
        UpgradeEffects(ImpactEffects, EEffectWhen.OnHit, EEffectTarget.Enemies, ref changed);
    }
}
