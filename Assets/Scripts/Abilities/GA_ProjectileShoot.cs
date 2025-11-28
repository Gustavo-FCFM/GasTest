using UnityEngine;

[CreateAssetMenu(fileName = "GA_ProjectileShoot", menuName = "GAS/Generics/Projectile Shoot")]
public class GA_ProjectileShoot : GameplayAbility
{
    [Header("Configuración del Proyectil")]
    public GameObject ProjectilePrefab; 
    public float LaunchForce = 20f;     
    
    [Tooltip("Posición relativa al jugador (X=Lado, Y=Altura, Z=Adelante). Ajusta Z para que no nazca dentro de tu cuerpo.")]
    public Vector3 SpawnOffset = new Vector3(0.5f, 1.5f, 1.0f); 
    
    [Header("Física Extra")]
    [Tooltip("Si true, añade un giro al objeto al lanzarlo (efecto de hacha girando).")]
    public bool AddSpin = true;

    [Header("Efectos al Impactar")]
    [Tooltip("El efecto que se aplica al instante (Ej: GE_Damage_Instant)")]
    public GameplayEffect InstantDamageEffect; 

    [Tooltip("El efecto que aplica un estado duradero (Ej: GE_Slow_Duration)")]
    public GameplayEffect DurationEffect; 

    public override void Activate()
    {
        if (!CanActivate()) return;
        CommitAbility();

        // 1. Pagar Coste
        if (CostEffect != null) OwnerASC.ApplyGameplayEffect(CostEffect, this);

        // 2. OBTENER DIRECCIÓN DE LA CÁMARA (MIRADA)
        Transform cameraTransform = Camera.main.transform;
        
        // Calculamos dónde nace (Usamos el cuerpo como base, pero sumamos el offset)
        // Nota: Usamos el cuerpo para la posición base, pero la rotación del disparo viene de la cámara
        Vector3 spawnPos = OwnerASC.transform.TransformPoint(SpawnOffset);
        
        // La rotación inicial del proyectil será igual a la de la cámara (mirando al frente)
        Quaternion spawnRot = cameraTransform.rotation;

        // 3. Crear el Proyectil
        GameObject newProjectile = Instantiate(ProjectilePrefab, spawnPos, spawnRot);

        // 4. Inicializar lógica GAS
        GC_Projectile projectileScript = newProjectile.GetComponent<GC_Projectile>();
        if (projectileScript != null)
        {
            // El proyectil necesita aplicar AMBOS efectos
            projectileScript.Initialize(InstantDamageEffect, DurationEffect, OwnerASC, LaunchForce, UltimateChargeAmount);
        }

        // 5. APLICAR FUERZA FÍSICA
        Rigidbody rb = newProjectile.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // IMPULSO: Usamos cameraTransform.forward para que vaya hacia donde miras
            rb.linearVelocity = cameraTransform.forward * LaunchForce;
            
            // Opcional: Añadir giro (Torque) para que parezca un hacha lanzada
            if (AddSpin)
            {
                // Gira en el eje X local (hacia adelante)
                rb.AddRelativeTorque(Vector3.right * 1000f); 
            }
        }

        EndAbility();
    }
}