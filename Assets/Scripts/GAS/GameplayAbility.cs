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
    
    // --- NUEVO: El ID específico de esta habilidad ---
    [Tooltip("1=Melee, 2=Proyectil, 3=Salto, 4=Area/Ulti")]
    public int AnimationID = 1;

    protected AbilitySystemComponent OwnerASC;

    public void Initialize(AbilitySystemComponent asc)
    {
        OwnerASC = asc;
    }

    public virtual bool CanActivate()
    {
        if (OwnerASC == null) return false;

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

    // --- NUEVO: LLAMA A ESTO AL PRINCIPIO DE ACTIVATE() ---
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