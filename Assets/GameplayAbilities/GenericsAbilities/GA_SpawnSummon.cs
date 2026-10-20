using UnityEngine;

// ============================================================
// GA_SpawnSummon  (Invocar — genérico, 20 de octubre de 2026)
//
// Pone una INVOCACIÓN sin escribir código: un prefab con Entity_Summon (una variante de las
// plantillas Summon_Base o SummonZone_Base: el muro de fuego, el elemental, el señuelo).
//
//   · Dónde: donde apunta la mira (mantener muestra el marcador en el piso, soltar la pone),
//     a los pies del lanzador o delante de él. Siempre apoyada en el piso
//     (Entity_Summon.GroundUnderAim / GroundNear: nunca arriba de un techo) y mirando hacia
//     donde mira el lanzador.
//   · "Máximo a la vez": cuántas puede tener el lanzador (al poner otra, se va la más vieja).
//     0 = lo que diga el prefab.
//   · Cobra costo y cooldown al ponerla; sus VFX "en el impacto" salen donde aparece. Puede
//     canalizar (sección Canalizar) como cualquier habilidad.
//
// Lo que la invocación HACE (cuánto dura, su zona, su vida) está en su prefab, no acá.
// ============================================================
[CreateAssetMenu(fileName = "GA_SpawnSummon", menuName = "GAS/Generics/Spawn Summon")]
public class GA_SpawnSummon : GameplayAbility, IGroundTargetAbility
{
    public enum EWhere
    {
        [InspectorName("Donde apunta (con marcador)")] AtAim,
        [InspectorName("A los pies del lanzador")]     AtCaster,
        [InspectorName("Delante del lanzador")]        InFront,
    }

    [Section("Invocación")]
    [Tooltip("El prefab que aparece: una variante de Summon_Base (con vida) o de SummonZone_Base " +
             "(solo zona), con su Entity_Summon.")]
    [Label("Prefab de la invocación")]
    public GameObject SummonPrefab;

    [Tooltip("Dónde aparece.\n\n" +
             "· Donde apunta: mantener el botón muestra el marcador en el piso; soltar la pone ahí.\n" +
             "· A los pies del lanzador.\n" +
             "· Delante del lanzador, a 'Distancia adelante'.")]
    [Label("Dónde")]
    public EWhere Where = EWhere.AtAim;

    [ShowIf(nameof(Where), EWhere.AtAim)]
    [Tooltip("Hasta dónde se puede poner, desde el lanzador.")]
    [Label("Alcance máximo (m)")]
    public float MaxRange = 15f;

    [ShowIf(nameof(Where), EWhere.AtAim)]
    [Tooltip("Tamaño del marcador del piso. 0 = el radio de la zona del prefab (su Summon_Zone).")]
    [Label("Radio del marcador (m; 0 = el de su zona)")]
    public float MarkerRadius = 0f;

    [ShowIf(nameof(Where), EWhere.InFront)]
    [Label("Distancia adelante (m)")]
    public float FrontDistance = 2f;

    [Tooltip("Cuántas puede tener el lanzador a la vez: al poner otra, se va la más vieja. " +
             "0 = el 'Máximo por dueño' del prefab.")]
    [Label("Máximo a la vez (0 = el del prefab)")]
    public int MaxAlive = 0;

    // IGroundTargetAbility: el marcador, solo si se pone donde apunta.
    public float MaxTargetRange   => MaxRange;
    public float TargetRadius     => MarkerRadius > 0f ? MarkerRadius : PrefabZoneRadius();
    public bool  UsesGroundTarget => Where == EWhere.AtAim;

    // No busca personajes ni golpea: invoca. Sus VFX "en el impacto" salen donde aparece.
    public override bool UsesTargetLayer => false;
    public override bool UsesHitEffects  => false;
    public override bool SupportsVisualTiming(EVisualWhen when)
        => when == EVisualWhen.OnCast || when == EVisualWhen.OnImpact;

    private float PrefabZoneRadius()
    {
        Summon_Zone zone = SummonPrefab != null ? SummonPrefab.GetComponent<Summon_Zone>() : null;
        return zone != null ? zone.Radius : 1f;
    }

    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate() || OwnerASC == null || SummonPrefab == null) { EndAbility(); return; }

        Transform owner = OwnerASC.transform;
        PlayerController pc = owner.GetComponent<PlayerController>();
        Vector3 forward = owner.forward; forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 spot;
        switch (Where)
        {
            case EWhere.AtCaster:
                spot = Entity_Summon.GroundNear(owner.position, owner.position.y);
                break;
            case EWhere.InFront:
                spot = Entity_Summon.GroundNear(owner.position + forward * FrontDistance, owner.position.y);
                break;
            default:
                // Donde apunta, recortado al alcance (en el plano), y apoyado en el piso.
                Vector3 aim  = pc != null ? pc.GetAimPoint(MaxRange) : owner.position + forward * MaxRange;
                Vector3 flat = aim - owner.position; flat.y = 0f;
                if (flat.magnitude > MaxRange)
                    aim = owner.position + flat.normalized * MaxRange + Vector3.up * (aim.y - owner.position.y);
                spot = Entity_Summon.GroundUnderAim(aim, owner.position + Vector3.up * 1.6f);
                break;
        }

        CommitAbility();

        Entity_Summon summon = Entity_Summon.Spawn(SummonPrefab, OwnerASC, spot, Quaternion.LookRotation(forward),
                                                   MaxAlive > 0 ? MaxAlive : -1);
        if (summon != null) BroadcastImpactVFX(spot);

        if (pc != null)
        {
            pc.RotateToAim();
            pc.PlayAnimation(this);
        }

        EndAbility();
    }

    public override void DrawGizmos(Transform origin)
    {
        if (origin == null) return;
        Gizmos.color = new Color(0.6f, 0.4f, 1f, 0.6f);
        if (Where == EWhere.AtAim) Gizmos.DrawWireSphere(origin.position, MaxRange);
        else
        {
            Vector3 at = Where == EWhere.InFront ? origin.position + origin.forward * FrontDistance : origin.position;
            Gizmos.DrawWireSphere(at, Mathf.Max(0.5f, TargetRadius));
        }
    }
}
