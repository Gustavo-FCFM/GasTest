using UnityEngine;

[CreateAssetMenu(fileName = "GA_SelfBuff", menuName = "GAS/Generics/Self Buff")]
public class GA_SelfBuff : GameplayAbility
{
    [Header("Buff Settings")]
    public GameplayEffect BuffEffect;

    [Header("Visuales")]
    public GameObject ParticlePrefab;
    public Vector3    ParticleOffset;

    public override void Activate()
    {
        if (!IsServer) return;   // ← NUEVO
        if (!CanActivate()) return;

        CommitAbility();

        if (BuffEffect != null)
            OwnerASC.ApplyGameplayEffect(BuffEffect, OwnerASC);
        else
            Debug.LogWarning("GA_SelfBuff activado sin un BuffEffect asignado.");

        if (OwnerASC != null)
        {
            PlayerController pc = OwnerASC.GetComponent<PlayerController>();
            if (pc != null) pc.PlayAnimation(AnimationTriggerName, AnimationID);

            if (ParticlePrefab != null)
            {
                GameObject vfxInstance = Instantiate(ParticlePrefab,
                    OwnerASC.transform.position + ParticleOffset,
                    Quaternion.identity,
                    OwnerASC.transform);

                float destroyTime = (BuffEffect != null && BuffEffect.Duration > 0) ? BuffEffect.Duration : 2f;
                Destroy(vfxInstance, destroyTime);
            }
        }

        EndAbility();
    }
}