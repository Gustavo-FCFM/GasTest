using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "GA_ContinuousAoE", menuName = "GAS/Generics/Continuous AoE")]
public class GA_ContinuousAoE : GameplayAbility
{
    [Header("Configuración de Área")]
    public float Radius = 4f;
    public float TotalDuration = 5f;
    public float TickInterval = 0.5f; // Daño cada medio segundo
    
    [Header("Comportamiento")]
    [Tooltip("Si es TRUE, el área te sigue (Torbellino). Si es FALSE, se queda donde casteaste (Charco).")]
    public bool FollowOwner = true; 
    public LayerMask TargetLayer;

    [Header("Efectos Visuales")]
    public GameObject VisualPrefab; // El efecto de tornado/partículas

    [Header("Efecto a Aplicar")]
    public GameplayEffect PeriodicEffect; // El daño por tick (debe ser Instantáneo)

    public override void Activate()
    {
        if (!CanActivate()) return;

        // 1. Coste y Cooldown
        if (CostEffect != null) OwnerASC.ApplyGameplayEffect(CostEffect, this);
        if (CooldownEffect != null) OwnerASC.ApplyGameplayEffect(CooldownEffect, this);

        // 2. Iniciar la lógica en el ASC
        if (OwnerASC != null)
        {
            PlayerController pc = OwnerASC.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.PlayAnimation(AnimationTriggerName, AnimationID);
            }
            OwnerASC.StartAbilityCoroutine(AreaRoutine());
        }
    }

    private IEnumerator AreaRoutine()
    {
        float timeElapsed = 0f;
        Vector3 spawnPoint = OwnerASC.transform.position; // Punto inicial
        GameObject vfxInstance = null;

        // 3. Spawnear visuales
        if (VisualPrefab != null)
        {
            vfxInstance = Instantiate(VisualPrefab, spawnPoint, Quaternion.identity);
            if (FollowOwner) vfxInstance.transform.SetParent(OwnerASC.transform); // Hacer hijo para que siga
        }

        // 4. Bucle de Daño
        while (timeElapsed < TotalDuration)
        {
            // Determinar el centro del área
            Vector3 center = FollowOwner ? OwnerASC.transform.position : spawnPoint;

            // Detectar enemigos
            Collider[] hits = Physics.OverlapSphere(center, Radius, TargetLayer);
            
            foreach (var hit in hits)
            {
                AbilitySystemComponent targetASC = hit.GetComponentInParent<AbilitySystemComponent>();
                if (targetASC != null && targetASC != OwnerASC)
                {
                    targetASC.ApplyGameplayEffect(PeriodicEffect, OwnerASC);
                    ChargeUltimate();
                }
            }

            // Esperar al siguiente tick
            yield return new WaitForSeconds(TickInterval);
            timeElapsed += TickInterval;
        }

        // 5. Limpieza
        if (vfxInstance != null) Destroy(vfxInstance);
        EndAbility();
    }
}