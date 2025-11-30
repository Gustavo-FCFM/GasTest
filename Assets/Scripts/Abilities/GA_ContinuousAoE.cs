using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "GA_ContinuousAoE", menuName = "GAS/Generics/Continuous AoE")]
public class GA_ContinuousAoE : GameplayAbility
{
    [Header("Configuración de Área")]
    public float Radius = 4f;
    public float TotalDuration = 5f;
    public float TickInterval = 0.5f; 
    
    [Header("Comportamiento")]
    public bool FollowOwner = true; 
    //public LayerMask TargetLayer;

    [Header("Efectos Visuales")]
    public GameObject VisualPrefab; 

    // --- NUEVO: AJUSTE DE TAMAÑO ---
    [Tooltip("Multiplicador para ajustar el tamaño de la partícula al radio. Prueba con 1.0, 2.0 (diámetro) o 0.5 según el asset.")]
    public float VisualScaleMultiplier = 2.0f; // 2.0 suele funcionar bien si el asset base mide 1 metro
    // -------------------------------

    [Header("Lista de Efectos")]
    public List<GameplayEffect> EffectsToApply;

    [Header("Sincronización")]
    public float StartDelay = 0.5f; 

    public override void Activate()
    {
        if (!CanActivate()) return;
        CommitAbility();

        if (OwnerASC != null)
        {
            PlayerController pc = OwnerASC.GetComponent<PlayerController>(); 
            if (pc != null) pc.PlayAnimation(AnimationTriggerName, AnimationID);
            
            OwnerASC.StartAbilityCoroutine(AoESequence());
        }
    }

    private IEnumerator AoESequence()
    {
        float speedMultiplier = 1f;
        float atkSpeedStat = OwnerASC.GetAttributeValue(EAttributeType.AtkSpeed);
        if (atkSpeedStat > 0) speedMultiplier = 1f / atkSpeedStat;

        if (StartDelay > 0)
        {
            yield return new WaitForSeconds(StartDelay / speedMultiplier);
        }

        yield return OwnerASC.StartCoroutine(AreaRoutine()); 

        EndAbility();
    }

    private IEnumerator AreaRoutine()
    {
        float timeElapsed = 0f;
        Vector3 spawnPoint = OwnerASC.transform.position; 
        GameObject vfxInstance = null;

        if (VisualPrefab != null)
        {
            vfxInstance = Instantiate(VisualPrefab, spawnPoint, Quaternion.identity);
            
            // --- APLICAR ESCALA DINÁMICA ---
            // Calculamos el tamaño final basado en el Radio de la habilidad
            float finalScale = Radius * VisualScaleMultiplier;
            vfxInstance.transform.localScale = new Vector3(finalScale, finalScale, finalScale);
            // -------------------------------

            if (FollowOwner) vfxInstance.transform.SetParent(OwnerASC.transform); 
        }

        while (timeElapsed < TotalDuration)
        {
            Vector3 center = FollowOwner ? OwnerASC.transform.position : spawnPoint;
            
            // Debug visual para ver el tamaño real de la lógica vs la partícula
            // (Solo se ve en la ventana Scene si tienes Gizmos activados)
            // Debug.DrawRay(center, Vector3.up * 5, Color.yellow, 0.5f);

            Collider[] hits = Physics.OverlapSphere(center, Radius, TargetLayer);
            
            foreach (var hit in hits)
            {
                AbilitySystemComponent targetASC = hit.GetComponentInParent<AbilitySystemComponent>();
                
                if (targetASC != null)
                {
                    if (EffectsToApply != null)
                    {
                        foreach (var effect in EffectsToApply)
                        {
                            if (effect != null) targetASC.ApplyGameplayEffect(effect, OwnerASC);
                        }
                    }
                    
                    if (OwnerASC.CompareTag("Player")) ChargeUltimate();
                }
            }

            yield return new WaitForSeconds(TickInterval);
            timeElapsed += TickInterval;
        }

        if (vfxInstance != null) Destroy(vfxInstance);
    }
}