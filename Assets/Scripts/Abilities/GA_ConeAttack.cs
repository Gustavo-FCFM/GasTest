using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "GA_ConeAttack", menuName = "GAS/Generics/Cone Attack")]
public class GA_ConeAttack : GameplayAbility
{
    [Header("Configuración del Cono")]
    public float Range = 2.5f; 
    [Range(0, 360)]
    public float ConeAngle = 90f; 
    //public LayerMask TargetLayer;

    [Header("Efectos")]
    public GameplayEffect DamageEffect;
    public float DamageDelay = 0.3f; // Tiempo base hasta el impacto (a velocidad normal)
    [Header("Visuales (Juice)")]
    public GameObject SwingVFX; // Rastro del hacha (Slash)
    [Tooltip("Ajusta esto si el efecto sale chueco. Prueba (90, 0, 0) para acostarlo.")]
    public Vector3 SwingRotationOffset = new Vector3(0, 0, 0);
    [Tooltip("Controla el tamaño del efecto. Pon (0.5, 0.5, 0.5) para reducirlo a la mitad.")]
    public Vector3 SwingScale = Vector3.one;
    [Tooltip("Tiempo de espera para que aparezca el efecto visual (Slash).")]
    public float VisualDelay = 0.1f;
    public GameObject HitVFX;


    public override void Activate()
    {
        if (!CanActivate()) return;

        // 1. COBRAR Y COOLDOWN INMEDIATO
        CommitAbility(); 

        // 2. ANIMACIÓN Y SECUENCIA
        if (OwnerASC != null)
        {
            PlayerController pc = OwnerASC.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.RotateToAim();
                pc.PlayAnimation(AnimationTriggerName, AnimationID);
            }
            
            // Iniciamos la corutina usando el ASC como motor
            OwnerASC.StartAbilityCoroutine(AttackSequence());
        }
    }

    private IEnumerator AttackSequence()
    {
        float speedMultiplier = 1f;
        float atkSpeedStat = OwnerASC.GetAttributeValue(EAttributeType.AtkSpeed);
        if (atkSpeedStat > 0) speedMultiplier = 1f / atkSpeedStat;

        // --- 1. SPAWN SWING VFX CON DELAY ---
        if (VisualDelay > 0)
        {
            yield return new WaitForSeconds(VisualDelay / speedMultiplier);
        }
        
        if (SwingVFX != null && OwnerASC != null)
        {
            Vector3 spawnPos = OwnerASC.transform.position + (OwnerASC.transform.forward * 0.3f) + (Vector3.up * -0.3f);
            
            Quaternion baseRotation = OwnerASC.transform.rotation;
            Quaternion offsetRotation = Quaternion.Euler(SwingRotationOffset);
            Quaternion finalRotation = baseRotation * offsetRotation;

            GameObject vfx = Instantiate(SwingVFX, spawnPos, finalRotation, OwnerASC.transform);
            vfx.transform.localScale = SwingScale;
            Destroy(vfx, 1.0f); 
        }
        // ------------------------------------

        // Esperamos el resto del tiempo hasta el impacto
        // (Restamos el VisualDelay que ya esperamos para que el DamageDelay sea preciso desde el inicio)
        float remainingDelay = DamageDelay - VisualDelay;
        if (remainingDelay > 0)
        {
            yield return new WaitForSeconds(remainingDelay / speedMultiplier);
        }

        PerformDetectionAndDamage();

        float backswingTime = 0.5f; 
        yield return new WaitForSeconds(backswingTime / speedMultiplier);

        EndAbility();
    }

    private void PerformDetectionAndDamage()
    {
        // 1. Detectar todo lo que esté en el radio (Esfera)
        Collider[] potentialTargets = Physics.OverlapSphere(OwnerASC.transform.position, Range, TargetLayer);
        HashSet<AbilitySystemComponent> enemiesHit = new HashSet<AbilitySystemComponent>();

        foreach (var targetCollider in potentialTargets)
        {
            // Dirección hacia el enemigo
            Vector3 directionToTarget = (targetCollider.transform.position - OwnerASC.transform.position).normalized;
            directionToTarget.y = 0; // Ignoramos altura para el ángulo plano
            
            // Usamos el frente del personaje para calcular el ángulo
            float angleToTarget = Vector3.Angle(OwnerASC.transform.forward, directionToTarget);

            // 2. Filtrar por ángulo (Cono)
            if (angleToTarget < ConeAngle / 2f)
            {
                AbilitySystemComponent targetASC = targetCollider.GetComponentInParent<AbilitySystemComponent>();

                // 3. Validar objetivo (Que tenga vida, que no sea yo, que no lo haya golpeado ya)
                if (targetASC != null && targetASC != OwnerASC && !enemiesHit.Contains(targetASC))
                {
                    // A. Aplicar Daño
                    if (DamageEffect != null)
                    {
                        targetASC.ApplyGameplayEffect(DamageEffect, OwnerASC);
                    }

                    // B. Cargar Ultimate (Método del padre GameplayAbility)
                    ChargeUltimate();

                    // C. Registrar golpe
                    enemiesHit.Add(targetASC);
                    
                    if (HitVFX != null)
                    {
                        // Instanciamos el efecto en el pecho del enemigo (target.position + up)
                        Vector3 hitPos = targetASC.transform.position + Vector3.up;
                        GameObject hitInstance = Instantiate(HitVFX, hitPos, Quaternion.identity);
                        Destroy(hitInstance, 2.0f);
                    }
                }
            }
        }
    }
}