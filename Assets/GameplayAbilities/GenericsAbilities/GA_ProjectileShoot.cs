using UnityEngine;
using System.Collections;
using FishNet;
using FishNet.Object;

// ============================================================
// GA_ProjectileShoot
//
// Dispara un proyectil físico (GC_Projectile) hacia el punto de mira
// del dueño. Esta habilidad solo se encarga de crear/lanzar el
// proyectil — toda la lógica de impacto (daño, VFX) vive en
// GC_Projectile, que recibe los datos necesarios en Initialize().
// ============================================================
[CreateAssetMenu(fileName = "GA_ProjectileShoot", menuName = "GAS/Generics/Projectile Shoot")]
public class GA_ProjectileShoot : GameplayAbility
{
    [Header("Configuración del Proyectil")]
    public GameObject ProjectilePrefab;
    // Velocidad de salida del proyectil.
    public float      LaunchForce  = 20f;
    // Punto de salida, en espacio local del dueño (ej: la mano).
    public Vector3    SpawnOffset  = new Vector3(0.5f, 1.5f, 1.0f);
    // Si el proyectil gira sobre sí mismo mientras vuela (solo estético).
    public bool       AddSpin      = true;

    [Header("Efectos al Impactar")]
    // Daño instantáneo al golpear.
    public GameplayEffect InstantDamageEffect;
    // Efecto con duración que además se le aplica al golpear (ej: veneno).
    public GameplayEffect DurationEffect;

    [Header("Sincronización")]
    // Espera entre activar la habilidad y que el proyectil realmente
    // salga (para sincronizar con la animación de disparo).
    public float SpawnDelay = 0.4f;

    [Header("Visuales")]
    public GameObject ImpactVFX;

    // Valida, reproduce la animación de disparo (con o sin PlayerController)
    // y arranca la secuencia de disparo.
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

    // Espera SpawnDelay (ajustado por velocidad de ataque), suelta el
    // proyectil, espera el remate de la animación, y termina.
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

    // Instancia el proyectil, lo spawnea en red, lo inicializa con los
    // datos de esta habilidad, y le da su velocidad inicial hacia el
    // punto de mira del dueño.
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

        // Esto corre en el servidor. Un Instantiate() normal solo crea el
        // objeto en ESTE proceso — nunca llega a los clientes (por eso el
        // proyectil nunca se veía para el dueño remoto). Hay que spawnearlo
        // en red igual que cualquier otro NetworkObject.
        NetworkObject projectileNob = newProjectile.GetComponent<NetworkObject>();
        if (projectileNob != null)
            InstanceFinder.ServerManager.Spawn(projectileNob);
        else
            Debug.LogWarning("[GA_ProjectileShoot] El ProjectilePrefab no tiene NetworkObject — no se va a replicar a los clientes.");

        GC_Projectile projectileScript = newProjectile.GetComponent<GC_Projectile>();
        if (projectileScript != null)
        {
            // El swap visual (cubo -> arma real) lo resuelve GC_Projectile
            // internamente en CADA peer a partir de quién disparó (ver
            // _shooterNob ahí) — llamarlo acá de nuevo solo pasaría en el
            // servidor y duplicaría el arma clonada en esa copia.
            //
            // Le pasamos 'this' para que, al impactar (siempre en el
            // servidor), el proyectil pueda pedirle a NetworkASC que
            // reproduzca ImpactVFX en todos los peers vía PlayImpactVFX().
            projectileScript.Initialize(InstantDamageEffect, DurationEffect, OwnerASC, LaunchForce, UltimateChargeAmount, ImpactVFX, this);
        }

        Rigidbody rb = newProjectile.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = launchDirection * LaunchForce;
            if (AddSpin) rb.AddRelativeTorque(Vector3.right * 1000f);
        }
    }

    // Instancia ImpactVFX en el punto de impacto. Llamado por GC_Projectile
    // al impactar (siempre desde el servidor, vía
    // NetworkAbilitySystemComponent.ServerPlayAbilityVFX) — y replicado a
    // cada cliente con su propia copia local de esta misma habilidad.
    public override void PlayImpactVFX(Vector3 position)
    {
        if (ImpactVFX == null) return;
        GameObject vfx = Instantiate(ImpactVFX, position, Quaternion.identity);
        Destroy(vfx, 1.0f);
    }
}
