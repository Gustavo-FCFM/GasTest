using UnityEngine;
using System.Collections;

[CreateAssetMenu(fileName = "GA_FinalBlow", menuName = "GAS/Abilities/Inmortal/Final Blow")]
public class GA_FinalBlow : GameplayAbility
{
    [Header("Configuración Golpe Final")]
    public float ChargeTime   = 1.5f;
    public float ShieldAmount = 100f;

    [Header("Efectos")]
    public GameplayEffect DamageEffect;
    public GameplayEffect StunEffect;

    public override void Activate()
    {
        if (!IsServer) return;

        CommitAbility();

        if (OwnerASC != null)
            OwnerASC.StartAbilityCoroutine(ChargeRoutine());
        else
            EndAbility();
    }

    private IEnumerator ChargeRoutine()
    {
        PlayerController pc = OwnerASC.GetComponent<PlayerController>();

        float currentShield = OwnerASC.GetAttributeValue(EAttributeType.Shield);
        OwnerASC.SetCurrentAttributeValue(EAttributeType.Shield, currentShield + ShieldAmount);
        OwnerASC.AddTag(EGameplayTag.State_Rooted);

        float timer = 0f;
        bool  wasInterrupted = false;

        while (timer < ChargeTime)
        {
            if (OwnerASC.HasTag(EGameplayTag.State_Stunned)  ||
                OwnerASC.HasTag(EGameplayTag.State_Silenced) ||
                OwnerASC.HasTag(EGameplayTag.State_Dead))
            {
                wasInterrupted = true;
                break;
            }

            if (pc != null) pc.RotateToAim();
            timer += UnityEngine.Time.deltaTime;
            yield return null;
        }

        float shieldLeft = OwnerASC.GetAttributeValue(EAttributeType.Shield);
        OwnerASC.SetCurrentAttributeValue(EAttributeType.Shield, Mathf.Max(0, shieldLeft - ShieldAmount));
        OwnerASC.RemoveTag(EGameplayTag.State_Rooted);

        if (wasInterrupted)
        {
            Debug.Log("¡Golpe Final Interrumpido!");
            EndAbility();
            yield break;
        }

        if (pc != null) pc.PlayAnimation(AnimationTriggerName, AnimationID);

        Vector3    hitboxCenter = pc.transform.position + pc.transform.forward * HitboxOffsetZ;
        Collider[] hitColliders = Physics.OverlapBox(hitboxCenter, HitboxHalfExtents, pc.transform.rotation, TargetLayer);

        foreach (Collider hit in hitColliders)
        {
            AbilitySystemComponent targetASC = hit.GetComponent<AbilitySystemComponent>();
            if (targetASC != null && IsEnemy(targetASC))
            {
                float targetHealth    = targetASC.GetAttributeValue(EAttributeType.Health);
                float targetMaxHealth = targetASC.GetAttributeValue(EAttributeType.MaxHealth);

                if (targetMaxHealth > 0 && (targetHealth / targetMaxHealth) <= 0.05f)
                {
                    Debug.Log($"¡{hit.name} fue EJECUTADO!");
                    targetASC.SetCurrentAttributeValue(EAttributeType.Health, 0);
                }
                else
                {
                    if (DamageEffect != null) targetASC.ApplyGameplayEffect(DamageEffect, OwnerASC);
                    if (StunEffect   != null) targetASC.ApplyGameplayEffect(StunEffect,   OwnerASC);
                }
            }
        }

        EndAbility();
    }
}