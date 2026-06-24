using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "GA_LeapAttack", menuName = "GAS/Generics/Leap Attack")]
public class GA_LeapAttack : GameplayAbility
{
    [Header("Configuración del Salto")]
    public float JumpVelocity = 15f;
    public float ForwardForce = 5f;

    [Header("Efectos")]
    public GameplayEffect DamageEffect;
    public GameplayEffect CrowdControlEffect;

    [Header("Visuales")]
    public GameObject ImpactVFX;

    public override void Activate()
    {
        if (!IsServer) return;   // ← NUEVO
        if (!CanActivate()) return;

        CommitAbility();

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.PlayAnimation(AnimationTriggerName, AnimationID);
            pc.ExecuteLeap(this, JumpVelocity, ForwardForce);
            Debug.Log($"Salto Furioso Iniciado: Fuerza Vertical: {JumpVelocity}");
        }

        EndAbility();
    }

    // Este método lo llama el PlayerController al aterrizar
    public void ExecuteImpactCheck()
    {
        Vector3 impactCenter = OwnerASC.transform.position;

        if (ImpactVFX != null)
        {
            GameObject vfx = Instantiate(ImpactVFX, impactCenter, Quaternion.identity);
            float vfxScale  = AbilityRadius * 2f;
            vfx.transform.localScale = new Vector3(vfxScale, vfxScale, vfxScale);
            Destroy(vfx, 2.0f);
        }

        Collider[] hitColliders = Physics.OverlapSphere(impactCenter, AbilityRadius, TargetLayer);
        HashSet<AbilitySystemComponent> enemiesHit = new HashSet<AbilitySystemComponent>();

        foreach (var hitCollider in hitColliders)
        {
            AbilitySystemComponent targetASC = hitCollider.GetComponentInParent<AbilitySystemComponent>();
            if (targetASC != null && IsEnemy(targetASC) && !enemiesHit.Contains(targetASC))
            {
                if (DamageEffect != null)        targetASC.ApplyGameplayEffect(DamageEffect, OwnerASC);
                ChargeUltimate();
                if (CrowdControlEffect != null)  targetASC.ApplyGameplayEffect(CrowdControlEffect, OwnerASC);
                enemiesHit.Add(targetASC);
            }
        }
    }
}