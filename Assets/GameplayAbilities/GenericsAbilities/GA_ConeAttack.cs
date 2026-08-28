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

    [Tooltip("El barrido se inclina hacia donde apunta la CÁMARA (arriba o abajo), en vez de " +
             "salir siempre horizontal.\n\n" +
             "El giro horizontal NO cambia: lo sigue dando el cuerpo, que acompaña la mira en " +
             "vivo durante todo el swing. Lo único que se toma de la mira es la INCLINACIÓN.\n\n" +
             "Apagado, el cono ignora por completo si el objetivo está arriba o abajo — el " +
             "comportamiento de siempre.")]
    public bool UseVerticalAim = false;

    [Tooltip("Desde DÓNDE sale el cono, en espacio local del dueño (X = derecha, Y = arriba, " +
             "Z = adelante).\n\n" +
             "El pivote está a los PIES, así que por defecto el cono se mide desde el suelo: un " +
             "enemigo parado en una rampa por encima tuyo puede quedar fuera del ángulo aunque " +
             "lo tengas enfrente. Subir la Y al pecho (1.2-1.5) hace que el ángulo se mida desde " +
             "donde de verdad sale el golpe.\n\n" +
             "Importa MÁS con puntería vertical encendida, porque ahí el ángulo se mide en 3D.\n\n" +
             "En CERO (el default) sale del pivote, como se comportó siempre.")]
    public Vector3 OriginOffset = Vector3.zero;

    [Header("Efectos")]
    // Efecto que se le aplica a cada enemigo golpeado.
    public GameplayEffect DamageEffect;
    // Efectos EXTRA que se aplican a cada enemigo golpeado, además del daño
    // (ralentizar, heridas, marcar, etc.). Opcional.
    public List<GameplayEffect> AdditionalEffects;

    [Tooltip("Efectos que esta habilidad le aplica a los ALIADOS que alcance. El daño y los " +
             "AdditionalEffects van a los enemigos; esta lista, a los aliados.\n\n" +
             "VACÍA (lo normal) = la habilidad ignora por completo a los aliados. En cuanto tenga " +
             "algo, empieza a considerarlos objetivos válidos: es lo que convierte un ataque normal " +
             "en uno que daña enemigos Y cura aliados a su paso (Castigo divino del Paladín).")]
    // FormerlySerializedAs: se llamó "AllyEffects" y vivía en GameplayAbility. Unity
    // serializa por NOMBRE, así que mientras el campo siga llamándose TargetEffects los
    // assets ya configurados conservan su valor al bajarlo a esta clase.
    [UnityEngine.Serialization.FormerlySerializedAs("AllyEffects")]
    public List<GameplayEffect> TargetEffects;

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
        Vector3 origin = OwnerASC.transform.TransformPoint(OriginOffset);

        Collider[] potentialTargets = Physics.OverlapSphere(origin, Range, TargetLayer);
        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();

        // Hacia dónde apunta el cono. Se resuelve UNA vez por frame de impacto, no por
        // objetivo: en un barrido escalonado, cada golpe usa la dirección de SU momento.
        Vector3 attackDirection = ResolveAttackDirection(UseVerticalAim);

        foreach (var targetCollider in potentialTargets)
        {
            Vector3 directionToTarget = (targetCollider.transform.position - origin).normalized;

            // Sin puntería vertical se aplasta todo al plano horizontal — el
            // comportamiento de siempre: el cono ignora si el objetivo está arriba o
            // abajo, y solo importa el ángulo visto desde el cielo.
            //
            // CON puntería vertical hay que dejar de aplastar: si comparáramos una
            // dirección inclinada contra un objetivo aplastado, apuntar hacia arriba
            // haría fallar todo. Recién acá el ángulo pasa a medirse en 3D de verdad, y
            // por eso empieza a importar mirar arriba o abajo.
            if (!UseVerticalAim) directionToTarget.y = 0;

            float angleToTarget = Vector3.Angle(attackDirection, directionToTarget);

            if (angleToTarget >= ConeAngle / 2f) continue;

            AbilitySystemComponent targetASC = targetCollider.GetComponentInParent<AbilitySystemComponent>();
            if (targetASC == null || targetsHit.Contains(targetASC)) continue;

            // Reparte según afiliación: daño a enemigos, TargetEffects a aliados (solo
            // si la habilidad los tiene configurados — si no, devuelve false y el
            // aliado se saltea, que es el comportamiento clásico).
            if (!ApplyAffiliationEffects(targetASC, DamageEffect, TargetEffects)) continue;

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

        Vector3 center  = origin.TransformPoint(OriginOffset);
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
