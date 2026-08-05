using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ============================================================
// GA_FinalBlow
//
// Golpe cargado: se enraíza y gana un escudo temporal mientras
// carga (ChargeTime), se puede interrumpir con CC o al morir, y al
// completarse golpea en una caja frente al dueño — si el objetivo
// está por debajo del 5% de vida lo EJECUTA directo, si no le aplica
// daño y aturdimiento normales.
// ============================================================
[CreateAssetMenu(fileName = "GA_FinalBlow", menuName = "GAS/Specific Abilities/Immortal/Final Blow")]
public class GA_FinalBlow : GameplayAbility
{
    [Header("Configuración Golpe Final")]
    public float ChargeTime = 1.5f;

    [Header("Efectos")]
    [Tooltip("Escudo que recibe MIENTRAS carga. Debe ser un GE con duración y un modificador " +
             "de Shield (Add); se lo aplica a sí mismo. La duración la fuerza ChargeTime, así " +
             "que no hace falta igualarla en el GE. Si lo interrumpen, se retira antes de tiempo.")]
    public GameplayEffect ChargeShieldEffect;
    public GameplayEffect DamageEffect;
    public GameplayEffect StunEffect;

    [Header("Hitbox del Golpe")]
    // Qué tan lejos del dueño está el centro de la caja de golpe.
    public float   HitboxOffsetZ     = 1.5f;
    // Mitad del tamaño de la caja de golpe en cada eje.
    public Vector3 HitboxHalfExtents = new Vector3(1f, 1f, 1f);

    // Valida, cobra costo/cooldown y arranca la carga.
    public override void Activate()
    {
        if (!IsServer) return;

        CommitAbility();

        if (OwnerASC != null)
            OwnerASC.StartAbilityCoroutine(ChargeRoutine());
        else
            EndAbility();
    }

    // Enraíza al dueño y le da el escudo mientras rota hacia el punto de
    // mira durante ChargeTime segundos (se interrumpe si lo aturden,
    // silencian o muere). Al completarse sin interrupción, resuelve el
    // golpe: ejecuta a objetivos con <=5% de vida, o aplica daño/stun normal.
    private IEnumerator ChargeRoutine()
    {
        PlayerController pc = OwnerASC.GetComponent<PlayerController>();

        // Escudo mientras carga: se lo aplica A SÍ MISMO como cualquier otro GE.
        // El sistema de escudos temporales lo otorga ahora y lo retira solo al
        // expirar; le pasamos ChargeTime como duración para tener una sola fuente
        // de verdad (no hay que igualar el Duration del GE).
        if (ChargeShieldEffect != null)
            OwnerASC.ApplyGameplayEffect(ChargeShieldEffect, OwnerASC, ChargeTime);

        OwnerASC.AddTag(EGameplayTag.State_Rooted);

        float timer = 0f;
        bool  wasInterrupted = false;

        while (timer < ChargeTime)
        {
            if (OwnerASC.HasTag(EGameplayTag.State_Stunned)  ||
                OwnerASC.HasTag(EGameplayTag.State_Silenced) ||
                OwnerASC.HasTag(EGameplayTag.State_Dead))
            {
                wasInterrupted = true;
                break;
            }

            if (pc != null) pc.RotateToAim();
            timer += UnityEngine.Time.deltaTime;
            yield return null;
        }

        OwnerASC.RemoveTag(EGameplayTag.State_Rooted);

        if (wasInterrupted)
        {
            // Se cortó la carga: el escudo no debe sobrevivir al golpe fallido.
            if (ChargeShieldEffect != null) OwnerASC.RemoveEffectsByDefinition(ChargeShieldEffect);
            EndAbility();
            yield break;
        }

        if (pc != null) pc.PlayAnimation(this);

        Vector3    hitboxCenter = pc.transform.position + pc.transform.forward * HitboxOffsetZ;
        Collider[] hitColliders = Physics.OverlapBox(hitboxCenter, HitboxHalfExtents, pc.transform.rotation, TargetLayer);

        HashSet<AbilitySystemComponent> enemiesHit = new HashSet<AbilitySystemComponent>();

        foreach (Collider hit in hitColliders)
        {
            // GetComponentInParent (no GetComponent): el collider puede estar en un
            // hijo del personaje (ej. NPCs), igual que en el resto de las habilidades.
            AbilitySystemComponent targetASC = hit.GetComponentInParent<AbilitySystemComponent>();
            if (targetASC != null && IsEnemy(targetASC) && enemiesHit.Add(targetASC))
            {
                float targetHealth    = targetASC.GetAttributeValue(EAttributeType.Health);
                float targetMaxHealth = targetASC.GetAttributeValue(EAttributeType.MaxHealth);

                if (targetMaxHealth > 0 && (targetHealth / targetMaxHealth) <= 0.05f)
                {
                    targetASC.SetCurrentAttributeValue(EAttributeType.Health, 0);
                }
                else
                {
                    if (DamageEffect != null) targetASC.ApplyGameplayEffect(DamageEffect, OwnerASC);
                    if (StunEffect   != null) targetASC.ApplyGameplayEffect(StunEffect,   OwnerASC);
                }
            }
        }

        EndAbility();
    }

    // Dibuja el mismo box que usa ChargeRoutine() (OverlapBox), con el
    // mismo offset/tamaño reales.
    public override void DrawGizmos(Transform origin)
    {
        if (origin == null) return;

        Vector3 center = origin.position + origin.forward * HitboxOffsetZ;

        Matrix4x4 prevMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, origin.rotation, Vector3.one);

        Gizmos.color = new Color(0.7f, 0f, 1f, 0.25f);
        Gizmos.DrawCube(Vector3.zero, HitboxHalfExtents * 2f);
        Gizmos.color = new Color(0.7f, 0f, 1f, 1f);
        Gizmos.DrawWireCube(Vector3.zero, HitboxHalfExtents * 2f);

        Gizmos.matrix = prevMatrix;
    }
}
