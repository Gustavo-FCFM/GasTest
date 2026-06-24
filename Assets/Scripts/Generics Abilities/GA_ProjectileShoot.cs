using UnityEngine;
using System.Collections;

[CreateAssetMenu(fileName = "GA_ProjectileShoot", menuName = "GAS/Generics/Projectile Shoot")]
public class GA_ProjectileShoot : GameplayAbility
{
    [Header("Configuración del Proyectil")]
    public GameObject ProjectilePrefab;
    public float      LaunchForce  = 20f;
    public Vector3    SpawnOffset  = new Vector3(0.5f, 1.5f, 1.0f);
    public bool       AddSpin      = true;

    [Header("Efectos al Impactar")]
    public GameplayEffect InstantDamageEffect;
    public GameplayEffect DurationEffect;

    [Header("Sincronización")]
    public float SpawnDelay = 0.4f;

    [Header("Visuales")]
    public GameObject ImpactVFX;

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
                pc.PlayAnimation(AnimationTriggerName, AnimationID);
            }
            else
            {
                Animator anim = OwnerASC.GetComponent<Animator>();
                if (anim == null) anim = OwnerASC.GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    anim.SetInteger("ActionID", AnimationID);
                    anim.SetTrigger(AnimationTriggerName);
                }
            }

            OwnerASC.StartAbilityCoroutine(ShootSequence());
        }
    }

    private IEnumerator ShootSequence()
    {
        float speedMultiplier = 1f;
        float atkSpeedStat = OwnerASC.GetAttributeValue(EAttributeType.AtkSpeed);
        if (atkSpeedStat > 0) speedMultiplier = 1f / atkSpeedStat;

        yield return new WaitForSeconds(SpawnDelay / speedMultiplier);

        SpawnProjectile();

        float backswingTime = 0.5f;
        yield return new WaitForSeconds(backswingTime / speedMultiplier);

        EndAbility();
    }

    private void SpawnProjectile()
    {
        Vector3    spawnPos       = OwnerASC.transform.TransformPoint(SpawnOffset);
        Quaternion spawnRot       = Quaternion.identity;
        Vector3    launchDirection = Vector3.forward;

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();

        if (pc != null)
        {
            Vector3 targetPoint = pc.GetAimPoint(100f);
            launchDirection = (targetPoint - spawnPos).normalized;
            spawnRot        = Quaternion.LookRotation(launchDirection);
        }
        else
        {
            launchDirection = OwnerASC.transform.forward;
            spawnRot        = OwnerASC.transform.rotation;
        }

        GameObject newProjectile = Instantiate(ProjectilePrefab, spawnPos, spawnRot);

        GC_Projectile projectileScript = newProjectile.GetComponent<GC_Projectile>();
        if (projectileScript != null)
        {
            projectileScript.Initialize(InstantDamageEffect, DurationEffect, OwnerASC, LaunchForce, UltimateChargeAmount, ImpactVFX);

            if (pc != null)
            {
                GameObject currentWeapon = pc.GetCurrentMainWeapon();
                if (currentWeapon != null) projectileScript.OverrideVisuals(currentWeapon);
            }
        }

        Rigidbody rb = newProjectile.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = launchDirection * LaunchForce;
            if (AddSpin) rb.AddRelativeTorque(Vector3.right * 1000f);
        }
    }
}