using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "GA_LeapAttack", menuName = "GAS/Generics/Leap Attack")]
public class GA_LeapAttack : GameplayAbility
{
    [Header("Configuración del Salto")]
    public float JumpVelocity = 15f; 
    public float ForwardForce = 5f; // Fuerza horizontal
    
    /*[Header("Configuración de Impacto")]
    [Tooltip("Radio de detección al aterrizar.")]
    public float ImpactRadius = 3f; 
    public LayerMask TargetLayer;*/

    [Header("Efectos")]
    public GameplayEffect DamageEffect; 
    public GameplayEffect CrowdControlEffect; // Ralentización/Aturdimiento

    [Header("Visuales")]
    public GameObject ImpactVFX; //  Arrastra aquí el efecto de grieta o explosión de polvo

    public override void Activate()
    {
        if (!CanActivate()) return;
        CommitAbility();

        // 2. Ejecutar Salto Físico (Pide al PlayerController que inicie el salto)
        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.PlayAnimation(AnimationTriggerName, AnimationID);
            pc.ExecuteLeap(this, JumpVelocity, ForwardForce);
            Debug.Log($"Salto Furioso Iniciado: Fuerza Vertical: {JumpVelocity}");

            // NOTA: El PlayerController llamará a ExecuteImpactCheck al aterrizar
        }

        EndAbility(); 
    }
    
    // Este método lo llama el PlayerController cuando isGrounded es true
    public void ExecuteImpactCheck()
    {
        Vector3 impactCenter = OwnerASC.transform.position;

        // --- NUEVO: SPAWN VFX AL ATERRIZAR ---
        if (ImpactVFX != null)
        {
            // Lo instanciamos en los pies del jugador
            GameObject vfx = Instantiate(ImpactVFX, impactCenter, Quaternion.identity);
            
            float vfxScale = AbilityRadius * 2f; 
            vfx.transform.localScale = new Vector3(vfxScale, vfxScale, vfxScale);

            Destroy(vfx, 2.0f); // Limpieza automática
        }
        // --------------------------------------

        Collider[] hitColliders = Physics.OverlapSphere(impactCenter, AbilityRadius, TargetLayer);
        
        HashSet<AbilitySystemComponent> enemiesHit = new HashSet<AbilitySystemComponent>();
        
        foreach (var hitCollider in hitColliders)
        {
            AbilitySystemComponent targetASC = hitCollider.GetComponentInParent<AbilitySystemComponent>();
            if (targetASC != null && targetASC != OwnerASC && !enemiesHit.Contains(targetASC))
            {
                if (DamageEffect != null) targetASC.ApplyGameplayEffect(DamageEffect, OwnerASC);
                ChargeUltimate(); // Cargar ulti por cada enemigo aplastado
                if (CrowdControlEffect != null) targetASC.ApplyGameplayEffect(CrowdControlEffect, OwnerASC);
                enemiesHit.Add(targetASC);
            }
        }
    }
}