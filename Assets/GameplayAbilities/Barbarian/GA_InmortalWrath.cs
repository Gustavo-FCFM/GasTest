using UnityEngine;

[CreateAssetMenu(fileName = "GA_InmortalWrath", menuName = "GAS/Abilities/Inmortal/Inmortal Wrath")]
public class GA_InmortalWrath : GameplayAbility
{
    [Header("Configuración Inmortal Wrath")]
    [Tooltip("El efecto que otorga Status_Inmortal temporalmente")]
    public GameplayEffect InmortalBuffEffect; 
    
    [Tooltip("El daño de área al explotar")]
    public GameplayEffect ExplosionDamageEffect;

    // Sobrescribimos las reglas base de activación
    public override bool CanActivate()
    {
        if (OwnerASC == null) return false;

        // 1. ¡Solo se puede activar si estás MUERTO!
        if (!OwnerASC.HasTag(EGameplayTag.State_Dead)) 
        {
            return false; 
        }

        // 2. Checar Cooldown normal
        if (CooldownEffect != null && CooldownEffect.GrantedTags.Count > 0)
        {
            EGameplayTag cooldownTag = CooldownEffect.GrantedTags[0];
            if (OwnerASC.HasTag(cooldownTag)) return false;
        }

        return true; // Si está muerto y no tiene cooldown, ¡Adelante!
    }

    public override void Activate()
    {
        // Paga el coste y entra en Cooldown
        CommitAbility(); 

        if (OwnerASC != null)
        {
            // 1. Revivir mecánicamente (Le quita el tag State_Dead y pone vida al maximo)
            OwnerASC.Revive();

            // 2. Sobrescribir la vida a 1 
            OwnerASC.SetCurrentAttributeValue(EAttributeType.Health, 1f);

            // 3. Aplicar el buff de Inmortalidad
            if (InmortalBuffEffect != null)
            {
                OwnerASC.ApplyGameplayEffect(InmortalBuffEffect, OwnerASC);
            }

            // 4. Explosión de Daño en Área (Usando el AbilityRadius de la clase base)
            Collider[] hits = Physics.OverlapSphere(OwnerASC.transform.position, AbilityRadius, TargetLayer);
            foreach (Collider hit in hits)
            {
                AbilitySystemComponent targetASC = hit.GetComponentInParent<AbilitySystemComponent>();
                if (targetASC != null && targetASC != OwnerASC)
                {
                    if (ExplosionDamageEffect != null)
                    {
                        targetASC.ApplyGameplayEffect(ExplosionDamageEffect, OwnerASC);
                    }
                }
            }

            // 5. Animación de resurgir / grito
            PlayerController pc = OwnerASC.GetComponent<PlayerController>();
            if (pc != null) pc.PlayAnimation(AnimationTriggerName, AnimationID);
        }

        EndAbility();
    }
}