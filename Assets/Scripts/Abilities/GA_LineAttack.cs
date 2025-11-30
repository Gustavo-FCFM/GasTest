using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "GA_LineAttack", menuName = "GAS/Generics/Line Attack")]
public class GA_LineAttack : GameplayAbility
{
    [Header("Configuración de Línea")]
    public float Length = 5f; 
    public float Width = 2f;   
    //public LayerMask TargetLayer;

    [Header("Efectos")]
    public GameplayEffect DamageEffect;

    [Header("Animación")]
    //public string AnimationTriggerName = "AttackTrigger";
    public float DamageDelay = 0.4f; // Tiempo para que baje el arma
    [Header("Visuales (Juice)")]
    public GameObject SwingVFX; // El rastro del golpe vertical
    
    [Tooltip("Ajusta la rotación. Para golpes verticales suele ser (0, 0, 0) o (90, 0, 0).")]
    public Vector3 SwingRotationOffset = new Vector3(0, 0, 0);
    
    [Tooltip("Ajusta el tamaño.")]
    public Vector3 SwingScale = Vector3.one;
    
    [Tooltip("Retraso para que el efecto salga justo cuando el arma baja.")]
    public float VisualDelay = 0.2f;
    
    public GameObject HitVFX;

    public override void Activate()
    {
        if (!CanActivate()) return;
        CommitAbility();

        if (OwnerASC != null)
        {
            PlayerController pc = OwnerASC.GetComponent<PlayerController>();
            if (pc != null) 
            {
                pc.RotateToAim();
                pc.PlayAnimation(AnimationTriggerName, AnimationID);
            }
            // Usamos corutina para esperar el golpe
            OwnerASC.StartAbilityCoroutine(AttackSequence());
        }
    }

    private IEnumerator AttackSequence()
    {
        float speedMultiplier = 1f;
        float atkSpeedStat = OwnerASC.GetAttributeValue(EAttributeType.AtkSpeed);
        if (atkSpeedStat > 0) speedMultiplier = 1f / atkSpeedStat;

        // --- 1. SPAWN SWING VFX (Con Delay) ---
        if (VisualDelay > 0)
        {
            yield return new WaitForSeconds(VisualDelay / speedMultiplier);
        }

        if (SwingVFX != null && OwnerASC != null)
        {
            // Para un ataque lineal, el efecto suele salir un poco más adelante
            Vector3 spawnPos = OwnerASC.transform.position + (OwnerASC.transform.forward * 1.5f) + (Vector3.up * 1.0f);
            
            Quaternion baseRotation = OwnerASC.transform.rotation;
            Quaternion offsetRotation = Quaternion.Euler(SwingRotationOffset);
            Quaternion finalRotation = baseRotation * offsetRotation;

            GameObject vfx = Instantiate(SwingVFX, spawnPos, finalRotation, OwnerASC.transform);
            vfx.transform.localScale = SwingScale;
            Destroy(vfx, 1.0f);
        }
        // --------------------------------------

        // Esperar el resto del tiempo para el daño
        float remainingDelay = DamageDelay - VisualDelay;
        if (remainingDelay > 0)
        {
            yield return new WaitForSeconds(remainingDelay / speedMultiplier);
        }

        PerformDamage();

        // 2. Backswing (Recuperación)
        yield return new WaitForSeconds(0.5f / speedMultiplier);

        EndAbility();
    }

    private void PerformDamage()
    {
        Vector3 origin = OwnerASC.transform.position;
        Vector3 direction = OwnerASC.transform.forward;
        Vector3 center = origin + (direction * (Length / 2)); 
        Vector3 halfExtents = new Vector3(Width / 2, 1, Length / 2); 

        Collider[] hits = Physics.OverlapBox(center, halfExtents, OwnerASC.transform.rotation, TargetLayer);
        HashSet<AbilitySystemComponent> enemiesHit = new HashSet<AbilitySystemComponent>();

        foreach (var hit in hits)
        {
            AbilitySystemComponent targetASC = hit.GetComponentInParent<AbilitySystemComponent>();
            if (targetASC != null && targetASC != OwnerASC && !enemiesHit.Contains(targetASC))
            {
                if (DamageEffect != null) targetASC.ApplyGameplayEffect(DamageEffect, OwnerASC);
                ChargeUltimate();
                enemiesHit.Add(targetASC);
                
                if (HitVFX != null)
                {
                    Vector3 hitPos = targetASC.transform.position + Vector3.up; // En el pecho del enemigo
                    GameObject hitInstance = Instantiate(HitVFX, hitPos, Quaternion.identity);
                    Destroy(hitInstance, 2.0f);
                }
            }
        }
    }
}