using UnityEngine;
using System.Collections;

[CreateAssetMenu(fileName = "GA_FinalBlow", menuName = "GAS/Abilities/Inmortal/Final Blow")]
public class GA_FinalBlow : GameplayAbility
{
    [Header("Configuración Golpe Final")]
    [Tooltip("Tiempo en segundos que el jugador debe esperar antes del golpe")]
    public float ChargeTime = 1.5f;
    [Tooltip("Escudo que recibe MIENTRAS carga")]
    public float ShieldAmount = 100f;
    
    [Header("Efectos a Aplicar (No ejecutados)")]
    public GameplayEffect DamageEffect;
    public GameplayEffect StunEffect;
    
    /*[Header("Hitbox (Rectángulo)")]
    [Tooltip("Mitad del tamaño de la caja (X=Ancho, Y=Alto, Z=Profundidad)")]
    public Vector3 HitboxHalfExtents = new Vector3(2f, 1f, 3f); 
    [Tooltip("Distancia hacia adelante desde el jugador donde se ubica el centro del rectángulo")]
    public float HitboxOffsetZ = 3f; */

    public override void Activate()
    {
        CommitAbility(); // Inicia el Cooldown y gasta el coste

        if (OwnerASC != null)
        {
            // Arrancamos la corrutina que maneja la carga y el ataque
            OwnerASC.StartAbilityCoroutine(ChargeRoutine());
        }
        else
        {
            EndAbility();
        }
    }

    private IEnumerator ChargeRoutine()
    {
        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        
        // 1. Dar escudo fijo durante la carga
        float currentShield = OwnerASC.GetAttributeValue(EAttributeType.Shield);
        OwnerASC.SetCurrentAttributeValue(EAttributeType.Shield, currentShield + ShieldAmount);

        // Opcional: Reproducir una animación de "Cargando" aquí si la tienes.

        float timer = 0f;
        bool wasInterrupted = false;

        // 2. Bucle de canalización (Carga)
        while (timer < ChargeTime)
        {
            // A. Revisar interrupciones (Aturdimiento, Silencio o Muerte)
            if (OwnerASC.HasTag(EGameplayTag.State_Stunned) || 
                OwnerASC.HasTag(EGameplayTag.State_Silenced) ||
                OwnerASC.HasTag(EGameplayTag.State_Dead))
            {
                wasInterrupted = true;
                break; // Rompemos el bucle de carga
            }

            // B. Permitir girar al jugador hacia donde apunta la mira
            if (pc != null) pc.RotateToAim();

            timer += Time.deltaTime;
            yield return null; // Esperamos al siguiente frame
        }

        // 3. Limpiar el escudo otorgado en la carga (quitamos lo que no se destruyó)
        float shieldLeft = OwnerASC.GetAttributeValue(EAttributeType.Shield);
        float newShield = Mathf.Max(0, shieldLeft - ShieldAmount);
        OwnerASC.SetCurrentAttributeValue(EAttributeType.Shield, newShield);

        // 4. Si fuimos interrumpidos, cancelamos el ataque
        if (wasInterrupted)
        {
            Debug.Log("¡Golpe Final Interrumpido!");
            EndAbility();
            yield break; // Salimos de la corrutina
        }

        // 5. Si llegamos hasta aquí, el ataque fue un ÉXITO
        if (pc != null) pc.PlayAnimation(AnimationTriggerName, AnimationID);
        
        // Calculamos el centro geométrico del rectángulo frente al jugador
        Vector3 hitboxCenter = pc.transform.position + pc.transform.forward * HitboxOffsetZ;

        // Detectar a todos los colliders dentro del rectángulo usando las físicas de Unity
        Collider[] hitColliders = Physics.OverlapBox(hitboxCenter, HitboxHalfExtents, pc.transform.rotation, TargetLayer);

        foreach (Collider hit in hitColliders)
        {
            AbilitySystemComponent targetASC = hit.GetComponent<AbilitySystemComponent>();
            if (targetASC != null && targetASC != OwnerASC) // Que no nos peguemos a nosotros mismos
            {
                float targetHealth = targetASC.GetAttributeValue(EAttributeType.Health);
                float targetMaxHealth = targetASC.GetAttributeValue(EAttributeType.MaxHealth);

                // --- MECÁNICA DE EJECUCIÓN (5% de vida) ---
                if (targetMaxHealth > 0 && (targetHealth / targetMaxHealth) <= 0.05f)
                {
                    Debug.Log($"¡{hit.name} fue EJECUTADO por el Inmortal!");
                    
                    // Instakill: Llevamos su vida a 0. Tu método SetCurrentAttributeValue
                    // ya tiene la lógica de llamar a Die() automáticamente si llega a 0 o menos.
                    targetASC.SetCurrentAttributeValue(EAttributeType.Health, 0); 
                }
                else
                {
                    // Golpe normal: Aplicamos el daño masivo y el aturdimiento configurados en el inspector
                    if (DamageEffect != null) targetASC.ApplyGameplayEffect(DamageEffect, OwnerASC);
                    if (StunEffect != null) targetASC.ApplyGameplayEffect(StunEffect, OwnerASC);
                }
            }
        }

        EndAbility(); // Libera el bloqueo de isAttacking en el PlayerController
    }
}
