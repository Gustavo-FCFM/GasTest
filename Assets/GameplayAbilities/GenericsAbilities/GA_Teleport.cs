using UnityEngine;

// ============================================================
// GA_Teleport  (genérico — "Destello" del Clérigo, estilo Misty Step, y cualquier
// salto instantáneo a un lugar elegido)
//
// A DÓNDE (la mira es la guía):
//   · Si la mira toca un PISO, apareces ahí.
//   · Si toca una PARED o la parte de abajo de una plataforma, se busca primero el
//     piso de ARRIBA de esa cosa (subirse a la plataforma que estás mirando) y, si no
//     hay, el piso de ABAJO, del lado tuyo.
//   · Si solo apunta al AIRE, apareces en el piso más cercano debajo de ese punto.
//   Todo dentro de MaxRange medido desde el personaje.
//
//   Con DirectionalTeleport (la regla del dash direccional): caminando de lado o hacia
//   atrás, en vez de la mira se usa la dirección del movimiento, lo más lejos que se
//   pueda sobre piso, y el personaje sigue mirando al frente.
//
// LO QUE LA HACE SEGURA:
//   · No cruza las paredes INVISIBLES (límites de la arena, paredes de las salas: capa
//     Ignore Raycast) ni lo que la mira no pueda atravesar (rejas, muros).
//   · Nunca aterriza en el aire ni adentro de algo: se pega al piso y se prueba que
//     quepa una cápsula del tamaño del personaje. Si el lugar elegido no sirve, se
//     retrocede hacia el origen hasta encontrar uno que sí.
//   · Si no encuentra ningún lugar válido, no gasta nada: ni cooldown ni carga.
//
// El movimiento lo ejecuta el DUEÑO (el transform es client-authoritative): se le
// pide por ServerTeleportOwnerTo, igual que el Blink y la Intercepción.
// ============================================================
[CreateAssetMenu(fileName = "GA_Teleport", menuName = "GAS/Generics/Teleport")]
public class GA_Teleport : GameplayAbility, IGroundTargetAbility
{
    [Section(AbilitySection.Shape)]
    [Tooltip("Distancia máxima (en el plano) desde el personaje hasta el destino.")]
    public float MaxRange = 12f;

    [ShowIf(nameof(DirectionalTeleport), false)]
    [Tooltip("Radio del marcador en el suelo (solo visual, sin Directional).")]
    public float MarkerRadius = 1f;

    [Section(AbilitySection.Movement)]
    [Tooltip("Apagado: se marca el lugar manteniendo el botón y al soltar apareces ahí.\n" +
             "Prendido: sale al APRETAR, con la misma regla que el dash direccional. Si " +
             "caminas hacia donde miras (o estás quieto), vas a donde miras; si caminas de " +
             "lado o hacia atrás, te vas en esa dirección lo más lejos que se pueda, sin " +
             "dejar de mirar al frente. Ideal para escapar hacia atrás de quien te pega.")]
    public bool DirectionalTeleport = false;

    [Tooltip("Solo con Directional: qué tan alineado con la mira tiene que ir el movimiento " +
             "para contar como 'hacia adelante'. 0.5 ≈ incluye las diagonales hacia adelante.")]
    [Range(0f, 1f)]
    [ShowIf(nameof(DirectionalTeleport))]
    public float ForwardDot = 0.5f;

    public float MaxTargetRange   => MaxRange;
    public float TargetRadius     => MarkerRadius;
    // Direccional sale al apretar, sin marcador: es una esquiva, tiene que ser inmediata.
    public bool  UsesGroundTarget => !DirectionalTeleport;

    [Section("Aterrizaje seguro", startCollapsed: true)]
    [Tooltip("Capas del escenario: piso y obstáculos. Por defecto todo menos los personajes " +
             "(7). Las paredes invisibles (Ignore Raycast, 2) frenan pero nunca cuentan como piso.")]
    public LayerMask EnvironmentMask = ~(1 << 7);

    [Tooltip("Tamaño del personaje para probar que quepa en el destino.")]
    public float BodyRadius = 0.4f;
    public float BodyHeight = 1.8f;

    [Tooltip("Qué tan plano tiene que ser algo para ser PISO (la Y de su normal). 0.6 ≈ " +
             "hasta unos 53° de pendiente.")]
    [Range(0f, 1f)]
    public float WalkableNormalY = 0.6f;

    [Tooltip("Si la mira toca una pared o una plataforma por abajo: hasta cuánto más ARRIBA " +
             "del punto se busca el piso de encima (subirse a la plataforma).")]
    public float LedgeSearchUp = 3f;

    [Tooltip("Hasta cuánto más ABAJO del punto apuntado se busca el piso (apuntando al aire, " +
             "o bajo una pared).")]
    public float FloorSearchDown = 25f;

    [Tooltip("Cuánto retrocede por intento cuando el destino no sirve.")]
    public float BackoffStep = 0.5f;

    [Tooltip("Solo para el retroceso y el direccional: cuánto más abajo puede estar el piso " +
             "de cada punto que se prueba. Si no hay piso a esa distancia, ese punto no vale.")]
    public float MaxDrop = 4f;

    // Los VFX "en el impacto" salen en el punto de salida y en el de llegada (el destello
    // de antes, FlashVFX). No busca personajes con TargetLayer ni golpea a nadie.
    public override bool UsesTargetLayer => false;
    public override bool UsesHitEffects  => false;
    public override bool SupportsVisualTiming(EVisualWhen when) => when != EVisualWhen.OnHit || UsesHitEffects;

    private const int InvisibleWalls = 1 << 2;   // Ignore Raycast: límites y paredes de salas
    private int FloorMask => EnvironmentMask & ~InvisibleWalls;
    private int BlockMask => EnvironmentMask | InvisibleWalls;

    // =========================================================
    // ACTIVACIÓN
    // =========================================================

    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();

        Vector3 origin = OwnerASC.transform.position;
        Vector3 chest  = origin + Vector3.up * (BodyHeight * 0.5f);

        // La mira: desde la cámara del dueño (el servidor la recibe con el pedido).
        Vector3 eye    = pc != null ? pc.GetAimOrigin() : chest;
        Vector3 far    = pc != null ? pc.GetAimPoint(500f) : chest + OwnerASC.transform.forward * 500f;
        Vector3 aimDir = (far - eye).sqrMagnitude > 0.0001f ? (far - eye).normalized : OwnerASC.transform.forward;
        Vector3 aimFlat = new Vector3(aimDir.x, 0f, aimDir.z);

        // ¿Hacia la mira, o hacia donde camina (direccional)?
        bool    towardAim = true;
        Vector3 moveDir   = Vector3.zero;
        if (DirectionalTeleport && pc != null)
        {
            Vector3 move = pc.GetMoveDirection(); move.y = 0f;
            if (move.sqrMagnitude > 0.0001f)
            {
                moveDir   = move.normalized;
                towardAim = aimFlat.sqrMagnitude > 0.0001f &&
                            Vector3.Dot(moveDir, aimFlat.normalized) >= ForwardDot;
            }
        }

        bool found = towardAim
            ? TryResolveAimLanding(origin, chest, eye, aimDir, out Vector3 landing)
            : TryWalkBack(origin, origin + moveDir * MaxRange, out landing);

        if (!found)
        {
            // Sin lugar válido: no se cobra nada. EndAbility destraba al dueño.
            EndAbility();
            return;
        }

        CommitAbility();

        // Hacia la mira: se encara hacia donde se fue. De lado o hacia atrás: se sigue
        // mirando al frente (a quien te pegaba), como la esquiva del dash.
        Vector3 faceDir = towardAim ? landing - origin : aimFlat;
        faceDir.y = 0f;
        if (faceDir.sqrMagnitude < 0.0001f) faceDir = OwnerASC.transform.forward;

        ExecuteTeleport(pc, netAsc, origin, landing, faceDir);
    }

    // El viaje en sí, con el destino ya resuelto y cobrado: destello en la salida y en
    // la llegada, mover al dueño y animar. Una subclase lo reemplaza para hacer otra cosa
    // con el mismo destino seguro (el Salto heroico del Comandante: salta, aparece, golpea
    // y clava su bandera). Tiene que terminar con EndAbility().
    protected virtual void ExecuteTeleport(PlayerController pc, NetworkAbilitySystemComponent netAsc,
                                           Vector3 origin, Vector3 landing, Vector3 faceDir)
    {
        // Los VFX "en el impacto" se piden a los PIES: la altura la pone el Offset de cada
        // entrada (el destello de siempre va con (0, 1, 0), a la altura del pecho).
        BroadcastImpactVFX(origin);

        if (netAsc != null)  netAsc.ServerTeleportOwnerTo(landing, faceDir);
        else if (pc != null) pc.TeleportTo(landing, faceDir);

        BroadcastImpactVFX(landing);

        if (pc != null) pc.PlayAnimation(this);

        EndAbility();
    }

    // =========================================================
    // A DÓNDE MIRA
    // =========================================================

    private bool TryResolveAimLanding(Vector3 origin, Vector3 chest, Vector3 eye, Vector3 aimDir,
                                      out Vector3 landing)
    {
        landing = origin;

        // El rayo arranca a la altura del personaje sobre la línea de la mira (lo que hay
        // entre la cámara y el personaje no cuenta) y llega un poco más allá del alcance,
        // para poder encontrar el piso de algo que está justo en el borde.
        float   skip  = Mathf.Max(0f, Vector3.Dot(chest - eye, aimDir));
        Vector3 start = eye + aimDir * skip;
        float   reach = MaxRange * 1.5f + 2f;

        Vector3 candidate;
        bool    hasCandidate;

        if (Physics.Raycast(start, aimDir, out RaycastHit hit, reach, BlockMask, QueryTriggerInteraction.Ignore))
        {
            bool invisible = ((1 << hit.collider.gameObject.layer) & InvisibleWalls) != 0;

            if (!invisible && hit.normal.y >= WalkableNormalY)
            {
                // Tocó un piso: ahí.
                candidate    = hit.point;
                hasCandidate = true;
            }
            else
            {
                // Una pared, la parte de abajo de una plataforma, o una pared invisible.
                // Primero el piso de ENCIMA (subirse a lo que mira), salvo que sea una
                // pared invisible: esas no tienen "arriba" al que subirse.
                hasCandidate = false;
                candidate    = hit.point;

                if (!invisible)
                {
                    Vector3 into = new Vector3(aimDir.x, 0f, aimDir.z).normalized * (BodyRadius + 0.2f);
                    Vector3 above = hit.point + into + Vector3.up * LedgeSearchUp;
                    if (!Physics.CheckSphere(above, 0.1f, FloorMask, QueryTriggerInteraction.Ignore) &&
                        FloorBelow(above, LedgeSearchUp + 0.5f, out Vector3 top) && top.y >= hit.point.y - 0.1f)
                    {
                        candidate    = top;
                        hasCandidate = true;
                    }
                }

                // Si no, el piso de ABAJO, de este lado del obstáculo.
                if (!hasCandidate)
                {
                    Vector3 back = hit.point - aimDir * (BodyRadius + 0.3f);
                    hasCandidate = FloorBelow(back, FloorSearchDown, out candidate);
                }
            }
        }
        else
        {
            // Al aire: el piso más cercano debajo del punto apuntado, a todo el alcance.
            Vector3 air = start + aimDir * Mathf.Min(reach, MaxRange + 1f);
            hasCandidate = FloorBelow(air, FloorSearchDown, out candidate);
        }

        if (!hasCandidate) return false;

        // Acotado al alcance en el plano.
        Vector3 flat = candidate - origin; flat.y = 0f;
        if (flat.magnitude > MaxRange)
        {
            Vector3 clamped = origin + flat.normalized * MaxRange;
            clamped.y = candidate.y + 1f;
            if (!FloorBelow(clamped, MaxDrop + 1f, out candidate)) return TryWalkBack(origin, clamped, out landing);
        }

        if (IsValidLanding(origin, chest, candidate))
        {
            landing = candidate;
            return true;
        }

        // No sirve tal cual (no cabe, o habría que cruzar una pared invisible): lo más
        // cerca que se pueda en esa dirección.
        return TryWalkBack(origin, candidate + Vector3.up * 0.5f, out landing);
    }

    // =========================================================
    // RETROCESO: lo más lejos que se pueda sobre piso, en línea recta
    // =========================================================

    private bool TryWalkBack(Vector3 origin, Vector3 wanted, out Vector3 landing)
    {
        landing = origin;

        Vector3 flat = wanted - origin; flat.y = 0f;
        if (flat.magnitude > MaxRange) wanted = origin + flat.normalized * MaxRange + Vector3.up * (wanted.y - origin.y);

        // Nada de atravesar muros ni paredes invisibles: línea desde el pecho.
        Vector3 chest  = origin + Vector3.up * (BodyHeight * 0.5f);
        Vector3 target = new Vector3(wanted.x, Mathf.Max(wanted.y, origin.y) + BodyHeight * 0.5f, wanted.z);

        if (Physics.Linecast(chest, target, out RaycastHit wall, BlockMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 dir = (target - chest).normalized;
            target = wall.point - dir * (BodyRadius + 0.2f);
        }

        Vector3 back   = chest - target; back.y = 0f;
        float   length = back.magnitude;
        Vector3 step   = length > 0.001f ? back / length * BackoffStep : Vector3.zero;

        for (float travelled = 0f; travelled <= length + 0.001f; travelled += BackoffStep)
        {
            if (FloorBelow(target, MaxDrop + BodyHeight, out Vector3 feet) && IsValidLanding(origin, chest, feet))
            {
                landing = feet;
                return true;
            }
            target += step;
        }
        return false;
    }

    // =========================================================
    // PRUEBAS
    // =========================================================

    // El piso debajo de 'point' (que sea piso: plano, y no una pared invisible).
    private bool FloorBelow(Vector3 point, float maxDistance, out Vector3 feet)
    {
        feet = point;
        if (!Physics.Raycast(point + Vector3.up * 0.2f, Vector3.down, out RaycastHit ground, maxDistance + 0.2f,
                             FloorMask, QueryTriggerInteraction.Ignore))
            return false;
        if (ground.normal.y < WalkableNormalY) return false;

        feet = ground.point + Vector3.up * 0.05f;
        return true;
    }

    // Un destino vale si: se movió algo de verdad, el personaje cabe parado ahí, y no
    // hay una pared invisible entre él y el destino (límites de la arena, salas).
    private bool IsValidLanding(Vector3 origin, Vector3 chest, Vector3 feet)
    {
        Vector3 moved = feet - origin; moved.y = 0f;
        if (moved.magnitude < 0.75f && Mathf.Abs(feet.y - origin.y) < 0.5f) return false;

        Vector3 bottom = feet + Vector3.up * (BodyRadius + 0.1f);
        Vector3 top    = feet + Vector3.up * (BodyHeight - BodyRadius);
        if (Physics.CheckCapsule(bottom, top, BodyRadius, BlockMask, QueryTriggerInteraction.Ignore)) return false;

        return !Physics.Linecast(chest, feet + Vector3.up * (BodyHeight * 0.5f), InvisibleWalls,
                                 QueryTriggerInteraction.Ignore);
    }

    public override void DrawGizmos(Transform origin)
    {
        if (origin == null) return;
        Gizmos.color = new Color(1f, 0.95f, 0.5f, 0.9f);
        Gizmos.DrawWireSphere(origin.position, MaxRange);
    }

#if UNITY_EDITOR
    public override void DrawSceneHandles(Transform origin)
    {
        if (origin == null) return;
        AbilityHandles.Distance(this, "Max Range", origin.position, origin.forward, ref MaxRange,
                                AbilityHandles.DistanceColor);
    }
#endif

    // =========================================================
    // DATOS VIEJOS (solo para pasarlos a las listas; ver GameplayAbility.UpgradeLegacyData)
    // =========================================================

    [SerializeField, HideInInspector] private GameObject FlashVFX;

    // A qué altura salía el destello: el teletransporte lo pedía a la altura del pecho; el
    // Salto heroico, en el piso donde cae.
    protected virtual Vector3 LegacyFlashOffset => Vector3.up;

    protected override void OnUpgradeLegacyData(ref bool changed)
    {
        base.OnUpgradeLegacyData(ref changed);

        UpgradeVisual(ref FlashVFX, new AbilityVisual
        {
            When = EVisualWhen.OnImpact, Offset = LegacyFlashOffset, DestroyTime = 2f,
        }, ref changed);
    }
}
