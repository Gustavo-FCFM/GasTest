using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ============================================================
// GA_ConeAttack
//
// Ataque cuerpo a cuerpo en forma de cono frente al dueño: detecta
// a quien esté dentro de un radio (Range) y un ángulo (ConeAngle),
// y le aplica la lista de efectos de la habilidad (GameplayAbility.Effects).
// Pensada para ataques primarios tipo hacha/espada.
//
// A los ALIADOS solo los tiene en cuenta si la lista trae algo para ellos: así un
// ataque normal los ignora, y uno con una curación a aliados daña enemigos y cura
// amigos a su paso (Castigo divino del Paladín).
// ============================================================
[CreateAssetMenu(fileName = "GA_ConeAttack", menuName = "GAS/Generics/Cone Attack")]
public class GA_ConeAttack : GameplayAbility
{
    [Section(AbilitySection.Shape)]
    [Tooltip("Alcance del cono, en metros, desde el punto de salida.")]
    public float Range = 2.5f;

    [Range(0f, 360f)]
    [Tooltip("Apertura del cono, en grados (el ángulo completo). 90 = un cuarto de círculo.")]
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

        // Interrumpido por otra habilidad (ver GameplayAbility.IsInterruptible): ni golpe
        // ni EndAbility — el fin lo manda la habilidad que lo cortó.
        if (WasCancelled) yield break;

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

        // El alcance crece con MeleeRangeBonus (el Avatar del Guardián).
        Collider[] potentialTargets = Physics.OverlapSphere(origin, Range * MeleeReach, TargetLayer);

        // Del más cercano al más lejano: así "el primer golpe" (los efectos al primer
        // golpe) cae en el que de verdad está adelante, y no en uno al azar
        // (OverlapSphere no devuelve los colliders en ningún orden garantizado).
        System.Array.Sort(potentialTargets, (a, b) =>
            (a.transform.position - origin).sqrMagnitude.CompareTo((b.transform.position - origin).sqrMagnitude));

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

            // La lista de efectos reparte según afiliación: a los enemigos siempre, a los
            // aliados solo si hay algo para ellos (si no, devuelve false y se saltea).
            if (!ApplyHitEffects(targetASC, firstHit: targetsHit.Count == 0)) continue;

            // La carga de ultimate y el gancho son parte del GOLPE: solo corresponden
            // cuando lo alcanzado es un enemigo.
            if (IsEnemy(targetASC))
            {
                ChargeUltimate();
                OnEnemyHit(targetASC);
            }

            targetsHit.Add(targetASC);

            // Instantiate() acá solo crearía el VFX en el proceso que corre esta
            // habilidad (el servidor): BroadcastHitVFX lo reproduce en todos los peers.
            BroadcastHitVFX(targetASC);
        }
    }

    // Gancho para que una habilidad concreta reaccione a cada ENEMIGO alcanzado (la
    // Quemadura santa del Clérigo de la Luz lo marca). Corre en el servidor.
    protected virtual void OnEnemyHit(AbilitySystemComponent enemy) { }

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

#if UNITY_EDITOR
    public override void DrawSceneHandles(Transform origin)
    {
        if (origin == null) return;

        Vector3 center = origin.TransformPoint(OriginOffset);
        AbilityHandles.Distance(this, "Range", center, origin.forward, ref Range, AbilityHandles.DistanceColor);
        AbilityHandles.Angle(this, "Cone Angle", center, origin.forward, Range, ref ConeAngle, AbilityHandles.AngleColor);
        AbilityHandles.Offset(this, "Origin Offset", origin, ref OriginOffset, AbilityHandles.OffsetColor);
    }
#endif

    // =========================================================
    // DATOS VIEJOS (solo para pasarlos a las listas; ver GameplayAbility.UpgradeLegacyData)
    // =========================================================

    [SerializeField, HideInInspector] private GameplayEffect DamageEffect;
    [SerializeField, HideInInspector] private List<GameplayEffect> AdditionalEffects;
    // Se llamó "AllyEffects" y vivía en GameplayAbility.
    [UnityEngine.Serialization.FormerlySerializedAs("AllyEffects")]
    [SerializeField, HideInInspector] private List<GameplayEffect> TargetEffects;
    [SerializeField, HideInInspector] private GameObject HitVFX;

    protected override void OnUpgradeLegacyData(ref bool changed)
    {
        base.OnUpgradeLegacyData(ref changed);

        UpgradeEffect(ref DamageEffect, EEffectWhen.OnHit, EEffectTarget.Enemies, ref changed);
        UpgradeEffects(AdditionalEffects, EEffectWhen.OnHit, EEffectTarget.Enemies, ref changed);
        UpgradeEffects(TargetEffects, EEffectWhen.OnHit, EEffectTarget.Allies, ref changed);
        UpgradeVisual(ref HitVFX, new AbilityVisual { When = EVisualWhen.OnHit, Offset = Vector3.up, DestroyTime = 2f },
                      ref changed);
    }
}
