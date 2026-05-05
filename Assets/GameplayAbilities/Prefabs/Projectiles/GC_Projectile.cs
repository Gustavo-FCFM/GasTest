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
    // Variable privada para guardar el prefab de explosión
    private GameObject impactVfxPrefab;

    // Lista para recordar a quién ya golpeamos y no repetir daño en el mismo frame
    private HashSet<AbilitySystemComponent> enemiesHit = new HashSet<AbilitySystemComponent>();

    public void Initialize(GameplayEffect damage, GameplayEffect durationEffect, AbilitySystemComponent source, float speed,float ultCharge,GameObject impactVFX)
    {
        damageEffect = damage;
        this.durationEffect = durationEffect;
        sourceASC = source;
        ultChargeAmount = ultCharge;
        impactVfxPrefab = impactVFX; // Guardamos la referencia
        // Destruir por tiempo si no choca con nada (evita basura en el nivel)
        Destroy(gameObject, lifeTime);
    }

    // Usamos OnTriggerEnter porque marcamos "Is Trigger" en el collider
    private void OnTriggerEnter(Collider other)
    {
        if (sourceASC != null && other.gameObject == sourceASC.gameObject) return;
        if (other.isTrigger) return;

        AbilitySystemComponent targetASC = other.GetComponentInParent<AbilitySystemComponent>();
        
        // 1. Instanciar VFX si existe (Independiente de lo que golpeemos)
        if (impactVfxPrefab != null)
        {
            GameObject vfx = Instantiate(impactVfxPrefab, transform.position, Quaternion.identity);
            Destroy(vfx, 1.0f);
        }

        // 2. ¿Es un personaje?
        if (targetASC != null)
        {
            // SISTEMA DE EQUIPOS (AFILIACIÓN LÓGICA)
            bool isEnemy = false;
            
            if (sourceASC.TeamID == 0 || targetASC.TeamID == 0) isEnemy = true;
            else if (sourceASC.TeamID != targetASC.TeamID) isEnemy = true;

            // A) Es enemigo y no lo hemos golpeado antes
            if (isEnemy)
            {
                if (!enemiesHit.Contains(targetASC))
                {
                    if (damageEffect != null) targetASC.ApplyGameplayEffect(damageEffect, sourceASC);
                    if (durationEffect != null) targetASC.ApplyGameplayEffect(durationEffect, sourceASC);
                    if (sourceASC != null && ultChargeAmount > 0)
                    {
                        sourceASC.ReduceCooldownByTag(EGameplayTag.Ability_Cooldown_Ultimate, ultChargeAmount);
                    }
                    enemiesHit.Add(targetASC);
                }
                
                // NOTA: Si quieres que el proyectil se DESTRUYA al golpear a un enemigo 
                // en lugar de atravesarlo, descomenta la siguiente línea:
                // Destroy(gameObject);
            }
            // B) Es aliado
            else
            {
                // Si es aliado, no hacemos daño.
                // El proyectil lo atraviesa por defecto porque no lo destruimos aquí.
            }
        }
        else
        {
            // 3. ¿Es una pared / entorno sólido?
            // Si llegamos aquí, no tiene ASC y no es trigger.
            Destroy(gameObject);
        }
    }
    public void OverrideVisuals(GameObject weaponModel)
    {
        if (weaponModel == null) return;

        // 1. LIMPIAR VISUALES VIEJOS
        // Si el proyectil tenía una malla por defecto (ej: esfera), la apagamos o destruimos sus gráficos
        MeshRenderer defaultMR = GetComponent<MeshRenderer>();
        if (defaultMR) defaultMR.enabled = false;
        
        // También apagamos cualquier hijo visual que tuviera por defecto
        foreach (Transform child in transform)
        {
            // Ojo: No borres el propio script o componentes de física si están en hijos
            // Lo mejor es tener un hijo llamado "Visuals" en tu prefab de proyectil y borrar ese.
            // Para simplificar, asumiremos que instanciamos el arma como un nuevo hijo visual.
        }

        // 2. CLONAR EL ARMA REAL
        // Instanciamos una copia del modelo que tiene el jugador en la mano
        GameObject weaponClone = Instantiate(weaponModel, transform);
        
        // 3. AJUSTAR POSICIÓN
        weaponClone.transform.localPosition = Vector3.zero;
        weaponClone.transform.localRotation = Quaternion.Euler(0f,90f,0f); // O la rotación que necesites para que apunte bien
        
        // Opcional: Ajustar escala si es necesario
        // weaponClone.transform.localScale = weaponModel.transform.localScale;

        // 4. LIMPIEZA DE COMPONENTES DEL CLON
        // El arma original podría tener scripts de colisión o lógica que no queremos en el proyectil.
        // Quitamos colliders extra para que no interfieran con el del proyectil principal.
        var colliders = weaponClone.GetComponentsInChildren<Collider>();
        foreach (var col in colliders) Destroy(col);
    }
}