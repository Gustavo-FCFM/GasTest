using UnityEngine;
using System.Collections.Generic; // Necesario para HashSet

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class GC_Projectile : MonoBehaviour
{
    private GameplayEffect damageEffect; // El efecto de Duration=0
    private GameplayEffect durationEffect;
    private AbilitySystemComponent sourceASC;
    private float lifeTime = 5f;
    private float ultChargeAmount = 0f;

    // Lista para recordar a quién ya golpeamos y no repetir daño en el mismo frame
    private HashSet<AbilitySystemComponent> enemiesHit = new HashSet<AbilitySystemComponent>();

    public void Initialize(GameplayEffect damage, GameplayEffect durationEffect, AbilitySystemComponent source, float speed,float ultCharge)
    {
        damageEffect = damage;
        this.durationEffect = durationEffect;
        sourceASC = source;
        ultChargeAmount = ultCharge;
        // Destruir por tiempo si no choca con nada (evita basura en el nivel)
        Destroy(gameObject, lifeTime);
    }

    // Usamos OnTriggerEnter porque marcamos "Is Trigger" en el collider
    private void OnTriggerEnter(Collider other)
    {
        // 1. Ignorar colisiones con quien lo disparó (Yo mismo)
        if (sourceASC != null && other.gameObject == sourceASC.gameObject) return;

        // 2. Ignorar otros Triggers (ej: zonas de captura, checkpoints, la bolsa de oro)
        // Si chocamos con algo que también es un Trigger, lo atravesamos sin hacer nada.
        if (other.isTrigger) return;

        // 3. Intentar detectar si es un Personaje (Tiene ASC)
        AbilitySystemComponent targetASC = other.GetComponentInParent<AbilitySystemComponent>();

        if (targetASC != null)
        {
            // --- ES UN ENEMIGO/JUGADOR ---
            
            // Verificamos si ya lo golpeamos antes (para no aplicar efecto 2 veces al mismo)
            if (!enemiesHit.Contains(targetASC))
            {
                // Aplicar efecto
                if (damageEffect != null)
                {
                    // A. Aplicar Daño (Instantáneo)
                    targetASC.ApplyGameplayEffect(damageEffect, sourceASC);
                }
                if (durationEffect != null)
                {
                    // B. Aplicar Slow/Ralentización (Duración)
                    targetASC.ApplyGameplayEffect(durationEffect, sourceASC);
                }
                if (sourceASC != null && ultChargeAmount > 0)
                {
                    // "Ability.Cooldown.Ultimate" es el Tag que definimos para tu Ulti
                    sourceASC.ReduceCooldownByTag(EGameplayTag.Ability_Cooldown_Ultimate, ultChargeAmount);
                }
                // Registrar en la lista
                enemiesHit.Add(targetASC);
            }

            // ¡IMPORTANTE! NO destruimos el objeto aquí.
            // Dejamos que siga viajando para golpear al siguiente enemigo detrás.
        }
        else
        {
            // --- ES UNA PARED / SUELO / OBSTÁCULO ---
            
            // Si no tiene ASC y no es un trigger, asumimos que es entorno sólido.
            Debug.Log("Choque con pared/suelo. Destruyendo proyectil.");
            Destroy(gameObject);
        }
    }
}