using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ARCHIVO: GA_ConeAttack.cs
[CreateAssetMenu(fileName = "GA_ConeAttack", menuName = "GAS/Generics/Cone Attack")]
public class GA_ConeAttack : GameplayAbility
{
    [Header("Configuración del Cono")]
    public float Range = 2.5f;

    [Header("Efectos")]
    public GameplayEffect DamageEffect;
    public float DamageDelay = 0.3f;

    public GameObject HitVFX;

    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;

        CommitAbility();

        if (OwnerASC != null)
        {
            PlayerController pc = OwnerASC.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.RotateToAim();
                pc.PlayAnimation(AnimationTriggerName, AnimationID);
            }
            OwnerASC.StartAbilityCoroutine(AttackSequence());
        }
    }

    private IEnumerator AttackSequence()
    {
        float speedMultiplier = 1f;
        float atkSpeedStat = OwnerASC.GetAttributeValue(EAttributeType.AtkSpeed);
        if (atkSpeedStat > 0) speedMultiplier = 1f / atkSpeedStat;

        if (DamageDelay > 0)
            yield return new WaitForSeconds(DamageDelay / speedMultiplier);

        PerformDetectionAndDamage();

        yield return new WaitForSeconds(0.5f / speedMultiplier);

        EndAbility();
    }

    private void PerformDetectionAndDamage()
    {
        Collider[] potentialTargets = Physics.OverlapSphere(OwnerASC.transform.position, Range, TargetLayer);
        HashSet<AbilitySystemComponent> enemiesHit = new HashSet<AbilitySystemComponent>();

        foreach (var targetCollider in potentialTargets)
        {
            Vector3 directionToTarget = (targetCollider.transform.position - OwnerASC.transform.position).normalized;
            directionToTarget.y = 0;
            float angleToTarget = Vector3.Angle(OwnerASC.transform.forward, directionToTarget);

            if (angleToTarget < ConeAngle / 2f)
            {
                AbilitySystemComponent targetASC = targetCollider.GetComponentInParent<AbilitySystemComponent>();
                if (targetASC != null && IsEnemy(targetASC) && !enemiesHit.Contains(targetASC))
                {
                    if (DamageEffect != null) targetASC.ApplyGameplayEffect(DamageEffect, OwnerASC);
                    ChargeUltimate();
                    enemiesHit.Add(targetASC);

                    if (HitVFX != null)
                    {
                        Vector3 hitPos = targetASC.transform.position + Vector3.up;
                        GameObject hitInstance = Instantiate(HitVFX, hitPos, Quaternion.identity);
                        Destroy(hitInstance, 2.0f);
                    }
                }
            }
        }
    }
}