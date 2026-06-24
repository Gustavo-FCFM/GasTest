using UnityEngine;
using System.Collections;

[CreateAssetMenu(fileName = "GA_Frenzy", menuName = "GAS/Abilities/Berserker/Frenzy")]
public class GA_Frenzy : GameplayAbility
{
    [Header("Configuración Frenesí")]
    public GameplayEffect BuffEffect;

    [Tooltip("Porcentaje de vida faltante que se convierte en escudo (0.5 = 50%)")]
    public float ShieldPercentage = 0.5f;

    public override void Activate()
    {
        if (!IsServer) return;

        CommitAbility();

        if (OwnerASC != null)
        {
            float maxHealth     = OwnerASC.GetAttributeValue(EAttributeType.MaxHealth);
            float currentHealth = OwnerASC.GetAttributeValue(EAttributeType.Health);
            float missingHealth = maxHealth - currentHealth;
            float shieldAmount  = missingHealth * ShieldPercentage;

            if (BuffEffect != null)
                OwnerASC.ApplyGameplayEffect(BuffEffect, this);

            float duration = BuffEffect != null ? BuffEffect.Duration : 10f;
            OwnerASC.StartAbilityCoroutine(ShieldRoutine(shieldAmount, duration));

            PlayerController pc = OwnerASC.GetComponent<PlayerController>();
            if (pc != null) pc.PlayAnimation(AnimationTriggerName, AnimationID);
        }

        EndAbility();
    }

    private IEnumerator ShieldRoutine(float shieldAmount, float duration)
    {
        float currentShield = OwnerASC.GetAttributeValue(EAttributeType.Shield);
        OwnerASC.SetCurrentAttributeValue(EAttributeType.Shield, currentShield + shieldAmount);
        Debug.Log($"Frenesí: +{shieldAmount} escudo por {duration}s.");

        yield return new WaitForSeconds(duration);

        float shieldLeft = OwnerASC.GetAttributeValue(EAttributeType.Shield);
        OwnerASC.SetCurrentAttributeValue(EAttributeType.Shield, Mathf.Max(0, shieldLeft - shieldAmount));
        Debug.Log("Frenesí terminó: escudo retirado.");
    }
}