using UnityEngine;
using System.Collections.Generic;

public abstract class GameplayAbility : ScriptableObject
{
    [Header("Configuración General")]
    public string AbilityName = "New Ability";
    public Sprite AbilityIcon;

    [Header("Costes")]
    public GameplayEffect CostEffect;

    [Header("Cooldown")]
    public GameplayEffect CooldownEffect;
    
    [Header("Cooldown Dinámico")]
    public bool UseAttackSpeedAsCooldown = false;

    [Header("Ultimate Charge")]
    public float UltimateChargeAmount = 0f;

    [Header("Bloqueos")]
    public List<EGameplayTag> ActivationBlockedTags;
    [Header("Animación")]
    public string AnimationTriggerName = "AttackTrigger"; 
    
    [Tooltip("1=Melee, 2=Proyectil, 3=Salto, 4=Extra")]
    public int AnimationID = 1;
    [Header("Configuración de Área (Opcional)")]
    [Tooltip("Radio de efecto, rango de ataque o tamaño de impacto.")]
    public float AbilityRadius = 3f; 
    [Tooltip("Capa de objetivos a afectar (Enemigos, Aliados, etc).")]
    public LayerMask TargetLayer;
    
    public enum EHitboxShape { Sphere, Box, Cone }
    
    [Header("Forma de la Hitbox (Gizmos)")]
    public EHitboxShape HitboxShape = EHitboxShape.Sphere;
    
    [Tooltip("Distancia hacia adelante desde el jugador donde se ubica el centro de la Hitbox")]
    public float HitboxOffsetZ = 1.5f;
    
    [Tooltip("Solo para 'Box': Mitad del tamaño de la caja (X=Ancho, Y=Alto, Z=Profundidad)")]
    public Vector3 HitboxHalfExtents = new Vector3(1f, 1f, 1f);
    
    [Tooltip("Solo para 'Cone': Ángulo de apertura en grados (ej. 45 o 90)")]
    [Range(0f, 360f)]
    public float ConeAngle = 90f;
    
    // --- LISTA DE VISUALES MULTIPLES ---
    [System.Serializable]
    public struct AbilityVisual
    {
        [Tooltip("El Prefab de la partícula o efecto.")]
        public GameObject VFXPrefab;
        
        [Tooltip("Tiempo a esperar desde que se activa la habilidad para mostrarlo.")]
        public float Delay;
        
        [Tooltip("Offset relativo al jugador (X, Y, Z).")]
        public Vector3 Offset;
        
        [Tooltip("Rotación extra para orientar el efecto (ej. 90 en X para acostarlo).")]
        public Vector3 RotationOffset;

        [Tooltip("Controla el tamaño del efecto (1,1,1 es el normal).")]
        public Vector3 Scale;

        [Tooltip("¿El efecto persigue al jugador (True) o se queda en el lugar donde apareció (False)?")]
        public bool AttachToOwner;
        
        [Tooltip("Tiempo en segundos para destruirlo (0 = no destruir automáticamente).")]
        public float DestroyTime;

        [Tooltip("Si eliges un Tag, el efecto vivirá hasta que el jugador pierda este Tag (Ignora el DestroyTime).")]
        public EGameplayTag EndWithTag;
    }

    [Header("Visuales de Habilidad (Automáticos)")]
    public List<AbilityVisual> VisualsSequence;
    // --------------------------------------------------
    protected AbilitySystemComponent OwnerASC;

    public void Initialize(AbilitySystemComponent asc)
    {
        OwnerASC = asc;
    }

    public virtual bool CanActivate()
    {
        if (OwnerASC == null) return false;
        if (OwnerASC.HasTag(EGameplayTag.State_Dead)) return false;
        // 1. Bloqueos
        if (ActivationBlockedTags != null)
        {
            foreach (EGameplayTag blockedTag in ActivationBlockedTags)
            {
                if (OwnerASC.HasTag(blockedTag)) return false;
            }
        }

        // 2. Cooldown
        if (CooldownEffect != null && CooldownEffect.GrantedTags.Count > 0)
        {
            EGameplayTag cooldownTag = CooldownEffect.GrantedTags[0];
            if (OwnerASC.HasTag(cooldownTag)) return false;
        }

        // 3. Costes
        if (CostEffect != null)
        {
            if (!OwnerASC.CanAffordGameplayEffect(CostEffect)) return false;
        }

        return true;
    }

    public abstract void Activate();

    // Paga el coste y EMPIEZA el cooldown inmediatamente
    protected void CommitAbility()
    {
        if (OwnerASC == null) return;

        // 1. Aplicar Coste
        if (CostEffect != null)
        {
            OwnerASC.ApplyGameplayEffect(CostEffect, this);
        }

        // 2. Aplicar Cooldown AHORA (al inicio)
        if (CooldownEffect != null)
        {
            float finalCooldown = -1f;
            if (UseAttackSpeedAsCooldown)
            {
                float currentAtkSpeed = OwnerASC.GetAttributeValue(EAttributeType.AtkSpeed);
                if (currentAtkSpeed > 0) finalCooldown = currentAtkSpeed;
            }
            OwnerASC.ApplyGameplayEffect(CooldownEffect, this, finalCooldown);
        }

        // 3. --- NUEVO: LANZAR SECUENCIA DE VISUALES MULTIPLES ---
        if (VisualsSequence != null && VisualsSequence.Count > 0)
        {
            OwnerASC.StartAbilityCoroutine(PlayVisualsSequence());
        }
    }

    // La corrutina que procesa la lista
    private System.Collections.IEnumerator PlayVisualsSequence()
    {
        // Obtenemos el multiplicador de velocidad de ataque (si aplica)
        float speedMultiplier = 1f;
        float atkSpeedStat = OwnerASC.GetAttributeValue(EAttributeType.AtkSpeed);
        if (atkSpeedStat > 0) speedMultiplier = 1f / atkSpeedStat;

        // Recorremos la lista de visuales
        foreach (var visualConfig in VisualsSequence)
        {
            if (visualConfig.VFXPrefab == null) continue;

            // Esperar el delay configurado (ajustado por la velocidad de ataque)
            if (visualConfig.Delay > 0)
            {
                yield return new WaitForSeconds(visualConfig.Delay / speedMultiplier);
            }

            // --- INSTANCIAR ---
            Vector3 spawnPos = OwnerASC.transform.position + OwnerASC.transform.TransformDirection(visualConfig.Offset);
            
            Quaternion baseRotation = OwnerASC.transform.rotation;
            Quaternion finalRotation = baseRotation * Quaternion.Euler(visualConfig.RotationOffset);

            GameObject vfxInstance;

            if (visualConfig.AttachToOwner)
            {
                vfxInstance = Instantiate(visualConfig.VFXPrefab, spawnPos, finalRotation, OwnerASC.transform);
            }
            else
            {
                vfxInstance = Instantiate(visualConfig.VFXPrefab, spawnPos, finalRotation);
            }

            // Ajustar escala si se definió una diferente a 0,0,0
            if (visualConfig.Scale != Vector3.zero)
            {
                vfxInstance.transform.localScale = visualConfig.Scale;
            }
            else
            {
                vfxInstance.transform.localScale = Vector3.one; // Por defecto
            }

            // Destrucción automática
            // Si configuraste un Tag, el efecto espera a que ese Tag desaparezca
            if (visualConfig.EndWithTag != EGameplayTag.None)
            {
                OwnerASC.StartAbilityCoroutine(DestroyVfxWhenTagRemoved(vfxInstance, visualConfig.EndWithTag));
            }
            // Si no hay Tag, usamos el tiempo normal (si es mayor a 0)
            else if (visualConfig.DestroyTime > 0)
            {
                Destroy(vfxInstance, visualConfig.DestroyTime);
            }
        }
    }
    // Corrutina que vigila el Tag del jugador
    private System.Collections.IEnumerator DestroyVfxWhenTagRemoved(GameObject vfx, EGameplayTag tagToMonitor)
    {
        // Esperamos 1 frame para asegurarnos de que el efecto/buff ya aplicó el Tag al jugador
        yield return null; 

        // Mientras el jugador exista, tenga el Tag, y el efecto visual no haya sido destruido por otra cosa...
        while (OwnerASC != null && OwnerASC.HasTag(tagToMonitor) && vfx != null)
        {
            yield return null; // Esperamos al siguiente frame
        }

        // Si el bucle se rompe, significa que perdió el Tag. ¡Destruimos el visual!
        if (vfx != null)
        {
            Destroy(vfx);
        }
    }
    
    public virtual void EndAbility()
    {
        if (OwnerASC != null)
        {
            // Buscamos al PlayerController para decirle "Ya terminé, suelta el bloqueo"
            PlayerController pc = OwnerASC.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.FinishAttack(); // <--- ESTO ARREGLA QUE SE TRABEN LAS OTRAS HABILIDADES
            }
        }
    }

    protected void ChargeUltimate()
    {
        if (UltimateChargeAmount > 0 && OwnerASC != null)
        {
            OwnerASC.ReduceCooldownByTag(EGameplayTag.Ability_Cooldown_Ultimate, UltimateChargeAmount);
        }
    }
}