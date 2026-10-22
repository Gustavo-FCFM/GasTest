using UnityEngine;

// ============================================================
// GA_ArcaneProjection  (Proyección arcana — la E de la Abjuración, 22 de octubre de 2026)
//
// Un GA_Target sobre un aliado (25 m, sin autocasteo: sin aliado en la mira no sale ni cobra) que
// le pide a la pasiva del Mago (ArcaneProtectionPassive, en su PassiveBehaviorsPrefab) que lo
// proyecte: el escudo inicial, el efecto de la proyección y las copias del clic izquierdo. Dura
// hasta que muere el Mago, muere el aliado o el aliado se queda sin ese escudo; relanzarla sobre
// otro aliado la mueve. Su lista de efectos puede quedar vacía (o llevar algo más para el aliado).
// ============================================================
[CreateAssetMenu(fileName = "GA_ArcaneProjection", menuName = "GAS/Specific Abilities/Wizard/Arcane Projection")]
public class GA_ArcaneProjection : GA_Target
{
    protected override bool NeedsHitEffects => false;

    protected override void OnTargetChosen(AbilitySystemComponent target)
    {
        ArcaneProtectionPassive passive = OwnerASC != null
            ? OwnerASC.GetComponentInChildren<ArcaneProtectionPassive>() : null;

        if (passive != null) passive.ProjectOnto(target);
        else Debug.LogWarning($"[{AbilityName}] el dueño no tiene ArcaneProtectionPassive (va en el " +
                              "PassiveBehaviorsPrefab de la clase): no se proyecta nada.");
    }
}
