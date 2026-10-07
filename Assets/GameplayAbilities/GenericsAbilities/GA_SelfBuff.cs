using UnityEngine;
using System.Collections.Generic;

// ============================================================
// GA_SelfBuff
//
// Habilidad de buff: se aplica a sí mismo uno o varios GameplayEffect. Pensada para
// habilidades tipo "Grito de guerra", "Postura defensiva" o "Enfurecer".
//
// LOS EFECTOS van en la lista de la habilidad como "Al activarse → El lanzador" (los
// aplica CommitAbility). Antes eran BuffEffect + AdditionalEffects: los assets viejos
// se pasan solos a la lista.
//
// VISUALES: usá la lista de VFX con entradas "Al lanzar" (soporta varios VFX, delays,
// offsets y fin por tag — ideal para un aura que dure lo mismo que el buff, con
// EndWithTag = el tag que otorga el efecto).
// ============================================================
[CreateAssetMenu(fileName = "GA_SelfBuff", menuName = "GAS/Generics/Self Buff")]
public class GA_SelfBuff : GameplayAbility
{
    // Un buff propio no busca a nadie ni golpea: el Inspector esconde TargetLayer y marca
    // cualquier entrada que no sea "al activarse".
    public override bool UsesTargetLayer => false;
    public override bool UsesHitEffects  => false;

    // Valida, cobra costo/cooldown (con eso se aplican los efectos "al activarse" y los VFX
    // "al lanzar") y anima.
    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;

        if (!HasEffects(EEffectWhen.OnActivate))
            Debug.LogWarning($"[{AbilityName}] es un buff propio sin ningún efecto 'Al activarse': no hace nada.");

        CommitAbility();

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        if (pc != null) pc.PlayAnimation(this);

        EndAbility();
    }

    // =========================================================
    // DATOS VIEJOS (solo para pasarlos a las listas; ver GameplayAbility.UpgradeLegacyData)
    // =========================================================

    [SerializeField, HideInInspector] private GameplayEffect BuffEffect;
    [SerializeField, HideInInspector] private List<GameplayEffect> AdditionalEffects;

    protected override void OnUpgradeLegacyData(ref bool changed)
    {
        base.OnUpgradeLegacyData(ref changed);

        UpgradeEffect(ref BuffEffect, EEffectWhen.OnActivate, EEffectTarget.Self, ref changed);
        UpgradeEffects(AdditionalEffects, EEffectWhen.OnActivate, EEffectTarget.Self, ref changed);
    }
}
