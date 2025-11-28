using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "GA_LeapAttack", menuName = "GAS/Generics/Leap Attack")]
public class GA_LeapAttack : GameplayAbility
{
    [Header("Configuración del Salto")]
    public float JumpVelocity = 15f; 
    public float ForwardForce = 5f; // Fuerza horizontal
    
    [Header("Configuración de Impacto")]
    [Tooltip("Radio de detección al aterrizar.")]
    public float ImpactRadius = 3f; 
    public LayerMask TargetLayer;

    [Header("Efectos")]
    public GameplayEffect DamageEffect; 
    public GameplayEffect CrowdControlEffect; // Ralentización/Aturdimiento

    public override void Activate()
    {
        if (!CanActivate()) return;
        CommitAbility();

        // 1. Aplicar Coste
        if (CostEffect != null) OwnerASC.ApplyGameplayEffect(CostEffect, this);

        // 2. Ejecutar Salto Físico (Pide al PlayerController que inicie el salto)
        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.ExecuteLeap(this, JumpVelocity, ForwardForce);
            Debug.Log($"Salto Furioso Iniciado: Fuerza Vertical: {JumpVelocity}");

            // NOTA: Para el chequeo de impacto, necesitarás un componente MonoBehaviour
            // auxiliar en el jugador que inicie una Coroutine para chequear la colisión 
            // SÓLO cuando toque el suelo después de este salto (para evitar el daño instantáneo).
        }

        // 3. Finalizar (El cooldown comienza, el daño real se aplica al aterrizar)
        EndAbility(); 
    }
    
    // (Este método se llamaría desde la Coroutine del MonoBehaviour auxiliar del jugador al aterrizar)
    public void ExecuteImpactCheck()
    {
        Vector3 impactCenter = OwnerASC.transform.position;
        Collider[] hitColliders = Physics.OverlapSphere(impactCenter, ImpactRadius, TargetLayer);
        
        HashSet<AbilitySystemComponent> enemiesHit = new HashSet<AbilitySystemComponent>();
        
        foreach (var hitCollider in hitColliders)
        {
            AbilitySystemComponent targetASC = hitCollider.GetComponentInParent<AbilitySystemComponent>();
            if (targetASC != null && targetASC != OwnerASC && !enemiesHit.Contains(targetASC))
            {
                if (DamageEffect != null) targetASC.ApplyGameplayEffect(DamageEffect, OwnerASC);
                ChargeUltimate();
                if (CrowdControlEffect != null) targetASC.ApplyGameplayEffect(CrowdControlEffect, OwnerASC);
                enemiesHit.Add(targetASC);
            }
        }
    }
}