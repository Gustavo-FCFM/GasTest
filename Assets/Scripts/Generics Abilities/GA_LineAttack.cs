using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "GA_LineAttack", menuName = "GAS/Generics/Line Attack")]
public class GA_LineAttack : GameplayAbility
{
    [Header("Configuración de Línea")]
    public float Length = 5f;
    public float Width  = 2f;

    [Header("Efectos")]
    public GameplayEffect DamageEffect;
    public float DamageDelay = 0.4f;

    public GameObject HitVFX;

    public override void Activate()
    {
        if (!IsServer) return;   // ← NUEVO
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

        PerformDamage();

        yield return new WaitForSeconds(0.5f / speedMultiplier);

        EndAbility();
    }

    private void PerformDamage()
    {
        Vector3 origin    = OwnerASC.transform.position;
        Vector3 direction = OwnerASC.transform.forward;
        Vector3 center    = origin + (direction * (Length / 2));
        Vector3 halfExtents = new Vector3(Width / 2, 1, Length / 2);

        Collider[] hits = Physics.OverlapBox(center, halfExtents, OwnerASC.transform.rotation, TargetLayer);
        HashSet<AbilitySystemComponent> enemiesHit = new HashSet<AbilitySystemComponent>();

        foreach (var hit in hits)
        {
            AbilitySystemComponent targetASC = hit.GetComponentInParent<AbilitySystemComponent>();
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