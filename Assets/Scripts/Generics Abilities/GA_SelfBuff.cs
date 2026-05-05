using UnityEngine;

// Asegúrate de que el nombre del archivo coincida con el de la clase (GA_SelfBuff.cs -> class GA_SelfBuff)
[CreateAssetMenu(fileName = "GA_SelfBuff", menuName = "GAS/Generics/Self Buff")]
public class GA_SelfBuff : GameplayAbility 
{
    [Header("Buff Settings")]
    public GameplayEffect BuffEffect; // El efecto a aplicar (ej: GE_Enfurecer)
    
    [Header("Visuales")]
    public GameObject ParticlePrefab; 
    public Vector3 ParticleOffset; 

    public override void Activate()
    {
        // 1. Chequeo inicial
        if (!CanActivate()) return;

        // 2. COBRAR Y COOLDOWN (Automático desde el padre)
        CommitAbility();

        // 3. APLICAR EL BUFF (La lógica única de esta habilidad)
        if (BuffEffect != null)
        {
            // Debug.Log($"Aplicando Buff: {BuffEffect.name} a {OwnerASC.name}");
            OwnerASC.ApplyGameplayEffect(BuffEffect, OwnerASC);
        }
        else
        {
            Debug.LogWarning("GA_SelfBuff activado sin un BuffEffect asignado.");
        }

        // 4. ANIMACIÓN Y PARTICULAS
        if (OwnerASC != null)
        {
            // A. Animación (Grito)
            PlayerController pc = OwnerASC.GetComponent<PlayerController>();
            if (pc != null) 
            {
                // Usamos el ID y Trigger del padre
                pc.PlayAnimation(AnimationTriggerName, AnimationID);
            }

            // B. Partículas
            if (ParticlePrefab != null)
            {
                // 1. Guardamos la referencia al objeto creado
                GameObject vfxInstance = Instantiate(ParticlePrefab, OwnerASC.transform.position + ParticleOffset, Quaternion.identity, OwnerASC.transform);
                
                // 2. Lógica de autodestrucción
                if (BuffEffect != null && BuffEffect.Duration > 0)
                {
                    // Si el buff dura 10 segundos, las partículas mueren a los 10 segundos
                    Destroy(vfxInstance, BuffEffect.Duration);
                }
                else
                {
                    // Si el buff es instantáneo o infinito, ponemos un tiempo de seguridad (ej: 2s)
                    // o confiamos en que el prefab tenga su propio "Stop Action: Destroy"
                    Destroy(vfxInstance, 2f); 
                }
            }
        }

        // 5. FINALIZAR (Libera el semáforo 'isAttacking')
        EndAbility();
    }
}