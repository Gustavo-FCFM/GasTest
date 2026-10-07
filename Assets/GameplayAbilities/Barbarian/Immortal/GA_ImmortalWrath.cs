using UnityEngine;
using System.Collections.Generic;

// ============================================================
// GA_ImmortalWrath
//
// Ultimate de resurrección: solo se puede activar estando MUERTO
// (sobreescribe CanActivate). Revive con 1 de vida, se aplica los efectos
// "al activarse" (el buff de inmortalidad), y hace una explosión en área
// alrededor del punto de reaparición (los efectos "al golpear"). La dispara
// PlayerController automáticamente al morir, si está disponible.
// ============================================================
[CreateAssetMenu(fileName = "GA_ImmortalWrath", menuName = "GAS/Specific Abilities/Barbarian/Immortal/Immortal Wrath")]
public class GA_ImmortalWrath : GameplayAbility
{
    [Section(AbilitySection.Shape)]
    [Tooltip("Radio de la explosión alrededor del punto de reaparición.")]
    public float AbilityRadius = 3f;

    public override float VisualAreaRadius => AbilityRadius;
    public override bool SupportsVisualTiming(EVisualWhen when) => true;

    // Se cobra estando MUERTO: un buff aplicado en CommitAbility se perdería al revivir.
    // Los efectos "al activarse" se aplican a mano, después de revivir.
    protected override bool ApplyActivationEffectsOnCommit => false;

    // Solo se puede activar si el personaje está muerto (y no en
    // cooldown) — al revés de cualquier otra habilidad.
    public override bool CanActivate()
    {
        if (OwnerASC == null) return false;

        if (!OwnerASC.HasTag(EGameplayTag.State_Dead)) return false;

        if (CooldownEffect != null && CooldownEffect.GrantedTags.Count > 0)
            if (OwnerASC.HasTag(CooldownEffect.GrantedTags[0])) return false;

        return true;
    }

    // Revive al dueño con 1 de vida, le aplica lo "al activarse", y daña a los enemigos
    // dentro de AbilityRadius.
    public override void Activate()
    {
        if (!IsServer) return;

        CommitAbility();

        if (OwnerASC != null)
        {
            OwnerASC.Revive();   // con el kit fresco: revivir para seguir pegando, no para huir
            OwnerASC.SetCurrentAttributeValue(EAttributeType.Health, 1f);

            ApplyActivationEffects();

            Vector3 center = OwnerASC.transform.position;
            BroadcastImpactVFX(center);

            // Un personaje puede tener varios colliders: cada uno recibe la explosión una vez.
            var seen = new HashSet<AbilitySystemComponent>();
            foreach (Collider hit in Physics.OverlapSphere(center, AbilityRadius, TargetLayer))
            {
                AbilitySystemComponent targetASC = hit.GetComponentInParent<AbilitySystemComponent>();
                if (targetASC == null || !IsEnemy(targetASC) || !seen.Add(targetASC)) continue;

                ApplyHitEffects(targetASC, firstHit: seen.Count == 1);
                BroadcastHitVFX(targetASC, withSound: false);
            }

            PlayerController pc = OwnerASC.GetComponent<PlayerController>();
            if (pc != null) pc.PlayAnimation(this);
        }

        EndAbility();
    }

    public override void DrawGizmos(Transform origin)
    {
        if (origin == null) return;
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.3f);
        Gizmos.DrawSphere(origin.position, AbilityRadius);
    }

#if UNITY_EDITOR
    public override void DrawSceneHandles(Transform origin)
    {
        if (origin == null) return;
        AbilityHandles.Radius(this, "Ability Radius", origin.position, ref AbilityRadius,
                              AbilityHandles.RadiusColor, origin.right);
    }
#endif

    // =========================================================
    // DATOS VIEJOS (solo para pasarlos a las listas; ver GameplayAbility.UpgradeLegacyData)
    // =========================================================

    // Se llamaba "InmortalBuffEffect".
    [UnityEngine.Serialization.FormerlySerializedAs("InmortalBuffEffect")]
    [SerializeField, HideInInspector] private GameplayEffect ImmortalBuffEffect;
    [SerializeField, HideInInspector] private GameplayEffect ExplosionDamageEffect;

    protected override void OnUpgradeLegacyData(ref bool changed)
    {
        base.OnUpgradeLegacyData(ref changed);

        UpgradeEffect(ref ImmortalBuffEffect, EEffectWhen.OnActivate, EEffectTarget.Self, ref changed);
        UpgradeEffect(ref ExplosionDamageEffect, EEffectWhen.OnHit, EEffectTarget.Enemies, ref changed);
    }
}
