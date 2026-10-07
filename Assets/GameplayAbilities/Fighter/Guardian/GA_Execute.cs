using UnityEngine;

// ============================================================
// GA_Execute  (habilidad extra del Guardián — Ejecución)
//
// Un golpe hacia adelante (un cono angosto) cuyo daño crece con la vida que le FALTA al
// enemigo: eso lo hace el propio efecto de daño (modificador con TargetHealthMode
// MissingHealth), sin código. Lo de acá es lo que pasa si el golpe lo MATA:
//   · se reinicia el tiempo de reutilización (se puede encadenar otra ejecución), y
//   · el Guardián se cura una fracción de la vida máxima del ejecutado.
// ============================================================
[CreateAssetMenu(fileName = "GA_Execute", menuName = "GAS/Specific Abilities/Fighter/Guardian/Execute")]
public class GA_Execute : GA_ConeAttack
{
    [Section("Al ejecutar")]
    [Tooltip("Fracción de la vida MÁXIMA del ejecutado que se cura el Guardián (0.2 = 20 %).")]
    [Range(0f, 1f)]
    public float HealFractionOfVictimMaxHealth = 0.2f;

    [Tooltip("Si matar reinicia el tiempo de reutilización.")]
    public bool ResetCooldownOnKill = true;

    // Corre en el servidor DESPUÉS de aplicar el daño (ver GA_ConeAttack), así que acá ya
    // se sabe si lo mató.
    protected override void OnEnemyHit(AbilitySystemComponent enemy)
    {
        if (enemy == null || OwnerASC == null) return;
        if (!enemy.HasTag(EGameplayTag.State_Dead) && enemy.GetAttributeValue(EAttributeType.Health) > 0f) return;

        if (ResetCooldownOnKill && CooldownEffect != null)
            OwnerASC.RemoveEffectsByDefinition(CooldownEffect);

        float heal = enemy.GetAttributeValue(EAttributeType.MaxHealth) * HealFractionOfVictimMaxHealth;
        if (heal <= 0f) return;

        float before = OwnerASC.GetAttributeValue(EAttributeType.Health);
        float max    = OwnerASC.GetAttributeValue(EAttributeType.MaxHealth);
        OwnerASC.SetCurrentAttributeValue(EAttributeType.Health, Mathf.Min(max, before + heal));

        float healed = OwnerASC.GetAttributeValue(EAttributeType.Health) - before;
        if (healed > 0f) OwnerASC.ShowCombatNumber(healed, ECombatNumber.Heal);
    }
}
