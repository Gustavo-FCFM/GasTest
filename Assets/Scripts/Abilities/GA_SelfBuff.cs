using UnityEngine;

[CreateAssetMenu(fileName = "GA_SelfBuff", menuName = "GAS/Generics/Self Buff")]
public class GA_Buff : GameplayAbility
{
    [Header("Buff Settings")]
    public GameplayEffect BuffEffect; // El efecto a aplicar (ej: GE_Enfurecer)

    public override void Activate()
    {
        if (!CanActivate()) return;

        // 1. Coste
        if (CostEffect != null)
        {
            OwnerASC.ApplyGameplayEffect(CostEffect, this);
        }

        // 2. Aplicar el Buff
        if (BuffEffect != null)
        {
            Debug.Log($"Aplicando Buff: {BuffEffect.name} a {OwnerASC.name}");
            OwnerASC.ApplyGameplayEffect(BuffEffect, OwnerASC);
        }
        else
        {
            Debug.LogWarning("GA_Buff activado sin un BuffEffect asignado.");
        }

        // 3. Cooldown y Fin
        // Para buffs, el cooldown suele ser estático (definido en el CooldownEffect)
        if (CooldownEffect != null)
        {
            OwnerASC.ApplyGameplayEffect(CooldownEffect, this);
        }

        EndAbility();
    }
}