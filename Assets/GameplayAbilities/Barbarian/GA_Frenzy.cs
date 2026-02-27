using UnityEngine;
using System.Collections;

[CreateAssetMenu(fileName = "GA_Frenzy", menuName = "GAS/Abilities/Berserker/Frenzy")]
public class GA_Frenzy : GameplayAbility
{
    [Header("Configuración Frenesí")]
    [Tooltip("El GameplayEffect que da el Tag Status_Frenzy y los bufos de daño/velocidad")]
    public GameplayEffect BuffEffect; 
    
    [Tooltip("Porcentaje de vida faltante que se convierte en escudo (0.5 = 50%)")]
    public float ShieldPercentage = 0.5f;

    public override void Activate()
    {
        CommitAbility(); // Paga coste y cooldown

        if (OwnerASC != null)
        {
            // 1. Calcular la vida faltante
            float maxHealth = OwnerASC.GetAttributeValue(EAttributeType.MaxHealth);
            float currentHealth = OwnerASC.GetAttributeValue(EAttributeType.Health);
            float missingHealth = maxHealth - currentHealth;

            // 2. Calcular el escudo (50% de la vida faltante)
            float shieldAmount = missingHealth * ShieldPercentage;

            // 3. Aplicar el efecto base (Frenzy) que da el Tag y los stats
            if (BuffEffect != null)
            {
                OwnerASC.ApplyGameplayEffect(BuffEffect, this);
            }

            // 4. Iniciar la cuenta regresiva para quitar el escudo temporal
            // Usamos la duración configurada en el ScriptableObject del efecto (10s)
            float duration = BuffEffect != null ? BuffEffect.Duration : 10f;
            OwnerASC.StartAbilityCoroutine(ShieldRoutine(shieldAmount, duration));
            
            // 5. Reproducir Animación
            PlayerController pc = OwnerASC.GetComponent<PlayerController>();
            if (pc != null) pc.PlayAnimation(AnimationTriggerName, AnimationID);
        }

        // Liberamos el combate para que el jugador pueda seguir atacando
        EndAbility(); 
    }

    private IEnumerator ShieldRoutine(float shieldAmount, float duration)
    {
        // Añadir el escudo directamente a la pool actual
        float currentShield = OwnerASC.GetAttributeValue(EAttributeType.Shield);
        OwnerASC.SetCurrentAttributeValue(EAttributeType.Shield, currentShield + shieldAmount);

        Debug.Log($"Frenesí activado: +{shieldAmount} de Escudo por {duration}s.");

        // Esperar el tiempo exacto que dura el bufo
        yield return new WaitForSeconds(duration);

        // Lógica de limpieza: Le restamos el escudo que le dimos, pero usamos Mathf.Max 
        // para asegurar que no le quitemos escudo extra (por si acaso recibió daño o ganó otro escudo)
        float shieldLeft = OwnerASC.GetAttributeValue(EAttributeType.Shield);
        float newShield = Mathf.Max(0, shieldLeft - shieldAmount);
        
        OwnerASC.SetCurrentAttributeValue(EAttributeType.Shield, newShield);
        Debug.Log("Frenesí terminó: Escudo temporal retirado.");
    }
}