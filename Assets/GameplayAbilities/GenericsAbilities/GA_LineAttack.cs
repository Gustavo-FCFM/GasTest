using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ============================================================
// GA_LineAttack
//
// Ataque cuerpo a cuerpo en forma de caja rectangular frente al
// dueño (Length x Width): detecta enemigos dentro de esa caja y les
// aplica DamageEffect. Pensada para ataques de arma larga/lanza que
// pegan en línea recta en vez de en cono.
// ============================================================
[CreateAssetMenu(fileName = "GA_LineAttack", menuName = "GAS/Generics/Line Attack")]
public class GA_LineAttack : GameplayAbility
{
    [Header("Configuración de Línea")]
    // Largo de la caja de detección, hacia adelante desde el dueño.
    public float Length = 5f;
    // Ancho de la caja de detección.
    public float Width  = 2f;

    [Header("Efectos")]
    public GameplayEffect DamageEffect;
    // Efectos EXTRA que se aplican a cada enemigo golpeado, además del daño
    // (ralentizar, heridas, marcar, etc.). Opcional.
    public List<GameplayEffect> AdditionalEffects;

    public GameObject HitVFX;

    // Valida, rota al dueño hacia el punto de mira, cobra costo/cooldown
    // y arranca la secuencia de ataque.
    public override void Activate()
    {
        if (!IsServer) return;   // ← NUEVO
        if (!CanActivate()) return;

        CommitAbility();

        if (OwnerASC != null)
        {
            PlayerController pc = OwnerASC.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.RotateToAim();
                pc.PlayAnimation(this);
            }
            OwnerASC.StartAbilityCoroutine(AttackSequence());
        }
    }

    // Resuelve el golpe en el/los frames de impacto que marca el clip, espera el
    // remate de la animación, y termina.
    private IEnumerator AttackSequence()
    {
        float speedMultiplier = 1f;
        float atkSpeedStat = OwnerASC.GetAttributeValue(EAttributeType.AtkSpeed);
        if (atkSpeedStat > 0) speedMultiplier = 1f / atkSpeedStat;

        // El timing lo maneja GameplayAbility: si el clip tiene varios eventos de
        // impacto, esto se convierte solo en una estocada de varios tiempos.
        yield return HitTimingRoutine(PerformDamage);

        yield return new WaitForSeconds(0.5f / speedMultiplier);

        EndAbility();
    }

    // Arma la caja (Length x Width) frente al dueño y le aplica daño a
    // todos los enemigos que caigan dentro, reproduciendo el VFX de golpe
    // en todos los peers.
    //
    // enemiesHit lo provee HitTimingRoutine y se COMPARTE entre los frames de impacto
    // del mismo golpe: así no se le pega dos veces al mismo enemigo.
    private void PerformDamage(HashSet<AbilitySystemComponent> enemiesHit)
    {
        Vector3 origin    = OwnerASC.transform.position;
        Vector3 direction = OwnerASC.transform.forward;
        Vector3 center    = origin + (direction * (Length / 2));
        Vector3 halfExtents = new Vector3(Width / 2, 1, Length / 2);

        Collider[] hits = Physics.OverlapBox(center, halfExtents, OwnerASC.transform.rotation, TargetLayer);
        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();

        foreach (var hit in hits)
        {
            AbilitySystemComponent targetASC = hit.GetComponentInParent<AbilitySystemComponent>();
            if (targetASC != null && IsEnemy(targetASC) && !enemiesHit.Contains(targetASC))
            {
                if (DamageEffect != null) targetASC.ApplyGameplayEffect(DamageEffect, OwnerASC);
                ApplyEffectsTo(AdditionalEffects, targetASC);
                ChargeUltimate();
                enemiesHit.Add(targetASC);

                // Instantiate() acá solo se vería en el proceso servidor —
                // ServerPlayAbilityVFX lo reproduce en todos los peers.
                Vector3 hitPos = targetASC.transform.position + Vector3.up;
                if (netAsc != null) netAsc.ServerPlayAbilityVFX(this, hitPos);
                else PlayImpactVFX(hitPos);
            }
        }
    }

    // Instancia HitVFX en la posición de impacto. La llama cada peer con
    // su propia copia (ver ServerPlayAbilityVFX).
    public override void PlayImpactVFX(Vector3 position)
    {
        if (HitVFX == null) return;
        GameObject hitInstance = Instantiate(HitVFX, position, Quaternion.identity);
        Destroy(hitInstance, 2.0f);
    }

    // Dibuja el mismo box que usa PerformDamage() (OverlapBox), con la
    // misma posición/rotación/tamaño reales.
    public override void DrawGizmos(Transform origin)
    {
        if (origin == null) return;

        Vector3 center      = origin.position + origin.forward * (Length / 2f);
        Vector3 halfExtents = new Vector3(Width / 2f, 1f, Length / 2f);

        Matrix4x4 prevMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, origin.rotation, Vector3.one);

        Gizmos.color = new Color(0.1f, 0.6f, 1f, 0.25f);
        Gizmos.DrawCube(Vector3.zero, halfExtents * 2f);
        Gizmos.color = new Color(0.1f, 0.6f, 1f, 1f);
        Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);

        Gizmos.matrix = prevMatrix;
    }
}
