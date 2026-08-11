using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ============================================================
// GA_ConeAttack
//
// Ataque cuerpo a cuerpo en forma de cono frente al dueño: detecta
// enemigos dentro de un radio (Range) y un ángulo (ConeAngle
// heredado de GameplayAbility), y les aplica DamageEffect. Pensada
// para ataques primarios tipo hacha/espada.
// ============================================================
[CreateAssetMenu(fileName = "GA_ConeAttack", menuName = "GAS/Generics/Cone Attack")]
public class GA_ConeAttack : GameplayAbility
{
    [Header("Configuración del Cono")]
    // Radio del cono de detección, desde la posición del dueño.
    public float Range = 2.5f;

    [Range(0f, 360f)]
    // Ángulo de apertura del cono de detección.
    public float ConeAngle = 90f;

    [Header("Efectos")]
    // Efecto que se le aplica a cada enemigo golpeado.
    public GameplayEffect DamageEffect;
    // Efectos EXTRA que se aplican a cada enemigo golpeado, además del daño
    // (ralentizar, heridas, marcar, etc.). Opcional.
    public List<GameplayEffect> AdditionalEffects;

    // VFX que aparece en cada enemigo golpeado.
    public GameObject HitVFX;

    // Valida, rota al dueño hacia el punto de mira, cobra costo/cooldown
    // y arranca la secuencia de ataque.
    public override void Activate()
    {
        if (!IsServer) return;
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
        // impacto, esto se convierte solo en un barrido escalonado.
        yield return HitTimingRoutine(PerformDetectionAndDamage);

        yield return new WaitForSeconds(0.5f / speedMultiplier);

        EndAbility();
    }

    // Busca personajes dentro de Range (esfera) y filtra por ángulo contra
    // ConeAngle; a cada uno que pase el filtro le aplica lo que le corresponda
    // según su afiliación y reproduce el VFX de golpe en todos los peers.
    //
    // targetsHit lo provee HitTimingRoutine y se COMPARTE entre los frames de impacto
    // del mismo swing: así un barrido escalonado no le pega dos veces al mismo objetivo.
    private void PerformDetectionAndDamage(HashSet<AbilitySystemComponent> targetsHit)
    {
        Collider[] potentialTargets = Physics.OverlapSphere(OwnerASC.transform.position, Range, TargetLayer);
        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();

        foreach (var targetCollider in potentialTargets)
        {
            Vector3 directionToTarget = (targetCollider.transform.position - OwnerASC.transform.position).normalized;
            directionToTarget.y = 0;
            float angleToTarget = Vector3.Angle(OwnerASC.transform.forward, directionToTarget);

            if (angleToTarget >= ConeAngle / 2f) continue;

            AbilitySystemComponent targetASC = targetCollider.GetComponentInParent<AbilitySystemComponent>();
            if (targetASC == null || targetsHit.Contains(targetASC)) continue;

            // Reparte según afiliación: daño a enemigos, AllyEffects a aliados (solo
            // si la habilidad los tiene configurados — si no, devuelve false y el
            // aliado se saltea, que es el comportamiento clásico).
            if (!ApplyAffiliationEffects(targetASC, DamageEffect)) continue;

            // Los efectos extra y la carga de ultimate son parte del GOLPE: solo
            // corresponden cuando lo alcanzado es un enemigo.
            if (IsEnemy(targetASC))
            {
                ApplyEffectsTo(AdditionalEffects, targetASC);
                ChargeUltimate();
            }

            targetsHit.Add(targetASC);

            // Instantiate() acá solo crearía el VFX en el proceso que
            // corre esta habilidad (el servidor) — un cliente remoto
            // nunca lo vería. ServerPlayAbilityVFX lo reproduce en el
            // servidor y le avisa a los demás peers.
            Vector3 hitPos = targetASC.transform.position + Vector3.up;
            if (netAsc != null) netAsc.ServerPlayAbilityVFX(this, hitPos);
            else PlayImpactVFX(hitPos);
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

    // Dibuja el cono real: mismo Range/ConeAngle que usa
    // PerformDetectionAndDamage() (OverlapSphere + filtro de ángulo).
    public override void DrawGizmos(Transform origin)
    {
        if (origin == null) return;

        Gizmos.color = new Color(1f, 0.55f, 0f, 1f);

        Vector3 center  = origin.position;
        float   halfAng = ConeAngle / 2f;
        const int segments = 24;

        Vector3 prevPoint = center + Quaternion.Euler(0, -halfAng, 0) * origin.forward * Range;
        Gizmos.DrawLine(center, prevPoint);

        for (int i = 1; i <= segments; i++)
        {
            float   angle = -halfAng + (ConeAngle * i / segments);
            Vector3 point = center + Quaternion.Euler(0, angle, 0) * origin.forward * Range;
            Gizmos.DrawLine(prevPoint, point);
            prevPoint = point;
        }

        Gizmos.DrawLine(center, prevPoint);
    }
}
