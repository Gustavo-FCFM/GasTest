using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Qué hace un bot en una pelea. Sale de la clase que tiene puesta (ver ResolveRole),
// y se puede forzar desde el inspector para probar.
public enum EBotRole
{
    Auto,      // se deduce de la clase base
    Bruiser,   // Bárbaro: se le tira encima y no lo suelta
    Assassin,  // Pícaro: espera a que esté débil y recién ahí entra
    Support,   // Paladín: se queda con los suyos y los sostiene
}

// ============================================================
// BotController
//
// Un jugador manejado por la máquina. Sirve para tener con quién probar —el modo
// espectador, una grabación, el balance— sin juntar nueve personas.
//
// CORRE SOLO EN EL SERVIDOR. El personaje de un bot es el MISMO prefab del jugador,
// spawneado sin dueño (ver NetworkGameManager.ServerSpawnBot): el servidor queda como
// autoridad y por eso puede moverlo y activarle habilidades sin pasar por ningún RPC.
// Para los clientes es un jugador más — lo ven, le pegan, les pega, suma puntos.
//
// POR QUÉ NO REUSA EL PlayerController: todo su Update arranca con "if (!IsOwner)
// return;". Un bot no tiene dueño, así que ese camino nunca corre. Lo que hace este
// script es ocupar ese lugar: mover (con NavMeshAgent, igual que MercEnemyAI), mirar,
// animar y pedir habilidades — pero pidiéndolas por ServerActivateAbility, que es el
// MISMO punto de entrada server-side que usa un jugador de verdad. Así los bots pagan
// cooldowns, energía y tags igual que todos: no hacen trampa.
//
// LAS PRIORIDADES, de más urgente a menos:
//   1. Si un ENEMIGO lleva la carga, matarlo. Todo lo demás espera.
//   2. Si la llevo YO, correr al punto de entrega.
//   3. Si la lleva un COMPAÑERO, escoltarlo.
//   4. Si está libre en el piso, ir a buscarla.
//   5. Si no hay carga, pelear con lo que haya cerca (los NPCs dan experiencia).
//
// Dentro de eso, cada rol se mueve distinto: ver DecideCombatPosition.
// ============================================================
[RequireComponent(typeof(PlayerController))]
public class BotController : MonoBehaviour
{
    [Header("Rol")]
    [Tooltip("Auto lo deduce de la clase base que tenga puesta. Forzalo solo para probar.")]
    public EBotRole RoleOverride = EBotRole.Auto;

    [Header("Percepción")]
    [Tooltip("Hasta dónde ve enemigos. Es generoso a propósito: la arena mide 42 m de radio " +
             "y con poca vista los bots se quedan dando vueltas en su base.")]
    public float VisionRadius = 35f;

    [Tooltip("La capa 7 (Character), donde viven jugadores y NPCs.")]
    public LayerMask CharacterLayer = 1 << 7;

    [Tooltip("Cada cuánto vuelve a decidir a quién pegarle y adónde ir. Bajarlo los hace " +
             "más reactivos y más caros; 0.25 alcanza y sobra.")]
    public float ThinkInterval = 0.25f;

    [Header("Distancias de pelea")]
    [Tooltip("A qué distancia pega el Bárbaro. Es el alcance de un ataque cuerpo a cuerpo.")]
    public float MeleeRange = 2.6f;
    [Tooltip("El Pícaro se retira a curarse cuando su vida baja de esta fracción.")]
    [Range(0.05f, 0.9f)] public float AssassinRetreatBelowHealth = 0.35f;

    [Tooltip("Y vuelve a la pelea cuando se recuperó hasta esta otra. La diferencia entre " +
             "las dos evita que entre y salga sin parar en el borde exacto.")]
    [Range(0.1f, 1f)] public float AssassinReturnAboveHealth = 0.75f;

    [Tooltip("A qué distancia sigue el Paladín a su compañero cuando no hay a quién pegarle.")]
    public float SupportFollowDistance = 6f;

    [Tooltip("Cuánto sostiene una habilidad de MANTENER (el escudo del Paladín) antes de " +
             "soltarla. Un bot no suelta botones: sin esto se quedaría con el escudo " +
             "levantado para siempre y no podría volver a usarlo.")]
    public float HoldSeconds = 1.6f;

    [Header("Que no parezcan estatuas")]
    [Tooltip("Cuánto rodea al enemigo en cada decisión, en grados. En 0 se quedan de frente " +
             "como antes. Cada bot gira para su lado y cambia de sentido cada tanto.")]
    public float StrafeDegreesPerThink = 22f;

    [Tooltip("Cada cuántos segundos, más o menos, un bot cambia el sentido en que rodea.")]
    public float StrafeFlipSeconds = 3.5f;

    [Tooltip("Cuánto se despega del enemigo MIENTRAS su ataque básico está en cooldown, en " +
             "múltiplos del alcance. Es lo que hace que peguen y se salgan en vez de " +
             "quedarse plantados comiendo golpes.")]
    public float BackOffRangeMult = 2.1f;

    [Header("Ritmo")]
    [Tooltip("Mínimo entre dos ataques básicos. El cooldown real lo pone la habilidad; " +
             "esto solo evita machacar CanActivate() sesenta veces por segundo.")]
    public float PrimaryInterval = 0.6f;

    [Tooltip("Mínimo entre dos intentos de habilidad (Q/E/R). Si están en cooldown, no pasa nada.")]
    public float AbilityInterval = 1.2f;

    // --- referencias, resueltas en el servidor ---
    private PlayerController              _pc;
    private AbilitySystemComponent        _asc;
    private NetworkAbilitySystemComponent _netASC;
    private NavMeshAgent                  _agent;
    private Animator                      _anim;

    private int   _teamId;
    private float _thinkTimer;
    private float _nextPrimaryAt;
    private float _nextAbilityAt;
    private bool  _subclassChosen;

    private AbilitySystemComponent _target;
    private bool _retreating;             // el Pícaro está volviendo a curarse
    private IHoldAbility _held;           // habilidad de MANTENER en curso (el escudo)
    private float _heldUntil;
    private Vector3 _destination;

    private float _strafeSign = 1f;   // hacia qué lado rodea este bot
    private float _strafeFlipAt;      // cuándo cambia de sentido

    private bool      _dashing;      // corriendo un dash: el agente no manda mientras dure
    private Coroutine _dashRoutine;

    // Se reusa entre búsquedas para no generar basura: la percepción corre 4 veces por
    // segundo por bot, y con nueve bots eso es mucho array descartado.
    private readonly Collider[] _hits = new Collider[32];

    // =========================================================
    // ARRANQUE
    // =========================================================

    // Hasta que el NetworkGameManager no lo inicialice, este componente no hace nada.
    // No hay OnStartServer porque NO es un NetworkBehaviour — ver la nota de arriba.
    private bool _ready;

    // La llama el NetworkGameManager justo después de spawnear el bot, y es lo único que
    // enciende este cerebro.
    //
    // POR QUÉ NO ES UN NetworkBehaviour: se agrega con AddComponent en runtime, y un
    // NetworkBehaviour agregado así NUNCA queda registrado en el NetworkObject (su lista
    // se arma en el editor). El resultado era que IsServerInitialized tiraba
    // NullReference: reventaba el bucle que spawnea los bots —de tres, entraba uno— y
    // después reventaba en cada frame. Como este script no manda ni recibe RPCs, no
    // necesita ser uno: le alcanza con correr en el servidor, que es quien lo crea.
    public void ServerInitialize(int teamId)
    {
        _teamId = teamId;

        _pc     = GetComponent<PlayerController>();
        _asc    = GetComponent<AbilitySystemComponent>();
        _netASC = GetComponent<NetworkAbilitySystemComponent>();
        _anim   = _pc != null ? _pc.characterAnimator : GetComponentInChildren<Animator>();

        if (_teamId == 0 && _netASC != null) _teamId = _netASC.NetTeamID;

        SetupAgent();

        // Arrancan escalonados: si los nueve piensan en el mismo frame se nota el tirón.
        _thinkTimer = Random.Range(0f, ThinkInterval);
        _ready      = true;
    }

    // El NavMeshAgent se agrega EN RUNTIME y solo en el servidor: el prefab del jugador
    // no lo trae (un jugador de verdad se mueve con el CharacterController y su input).
    // Los clientes no lo necesitan — les llega el transform ya movido.
    private void SetupAgent()
    {
        _agent = GetComponent<NavMeshAgent>();
        if (_agent == null) _agent = gameObject.AddComponent<NavMeshAgent>();

        _agent.radius           = 0.3f;   // los personajes miden ~0.6 de ancho
        _agent.height           = 2f;
        _agent.speed            = ResolveMoveSpeed();
        _agent.angularSpeed     = 720f;
        _agent.acceleration     = 30f;
        _agent.stoppingDistance = 0.4f;
        _agent.autoBraking      = false;  // frenar de golpe en cada punto los hace ver dubitativos

        // La rotación la maneja este script: un bot tiene que MIRAR a su objetivo aunque
        // se esté moviendo de costado, igual que un jugador mira hacia donde apunta.
        _agent.updateRotation = false;

        WarpToNavMesh();
    }

    private float ResolveMoveSpeed()
    {
        float s = _asc != null ? _asc.GetAttributeValue(EAttributeType.MovSpeed) : 0f;
        return s > 0f ? s : 5f;
    }

    // Pone el agente sobre el NavMesh más cercano. Hace falta al spawnear y después de
    // cada respawn: las bases están fuera del muro y el punto exacto puede caer en un
    // borde sin navegación.
    private void WarpToNavMesh()
    {
        if (_agent == null) return;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 6f, NavMesh.AllAreas))
            _agent.Warp(hit.position);
    }

    // La llama PlayerController.ServerTeleportBot: sin esto el agente cree que sigue en
    // el punto de muerte y vuelve caminando desde allá.
    public void OnServerTeleported() => WarpToNavMesh();

    // =========================================================
    // DASH
    // =========================================================

    // La llama NetworkAbilitySystemComponent.ServerStartDash cuando el personaje NO tiene
    // dueño, o sea cuando es un bot.
    //
    // En un jugador el dash lo ejecuta su CharacterController, que choca con las paredes.
    // Un bot no lo usa (su Update es de dueño), así que acá se avanza a mano pero PEGADO
    // AL NAVMESH: el mismo mapa que dice por dónde puede caminar dice hasta dónde puede
    // deslizarse, y así el dash no lo mete dentro de un muro ni lo tira fuera del mapa.
    public void ServerDash(Vector3 velocity, float duration, bool faceVelocity)
    {
        if (!_ready || duration <= 0f) return;

        if (_dashRoutine != null) StopCoroutine(_dashRoutine);
        _dashRoutine = StartCoroutine(DashRoutine(velocity, duration, faceVelocity));
    }

    private IEnumerator DashRoutine(Vector3 velocity, float duration, bool faceVelocity)
    {
        _dashing = true;

        if (_agent != null && _agent.enabled && _agent.isOnNavMesh) _agent.isStopped = true;

        // El dash del pícaro puede ser DIRECCIONAL (esquiva hacia el costado sin girar el
        // cuerpo): faceVelocity en false respeta eso, igual que en un jugador.
        if (faceVelocity)
        {
            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flat.normalized);
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            Vector3 next = transform.position + velocity * Time.deltaTime;

            if (NavMesh.SamplePosition(next, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
                transform.position = hit.position;
            else
                break;   // se topó con el borde de lo navegable: ahí termina el impulso

            elapsed += Time.deltaTime;
            yield return null;
        }

        _dashing     = false;
        _dashRoutine = null;
        WarpToNavMesh();
    }

    // =========================================================
    // CICLO
    // =========================================================

    private void Update()
    {
        if (!_ready || _pc == null || _asc == null) return;

        // Muerto: se queda quieto hasta que el respawn lo levante. El agente se frena
        // para que el cadáver no siga empujando a los vivos.
        if (_asc.HasTag(EGameplayTag.State_Dead))
        {
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh) _agent.isStopped = true;
            DriveAnimator(Vector3.zero);
            return;
        }

        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.isStopped = false;
            _agent.speed     = ResolveMoveSpeed();   // cambia con buffs y con la carga
        }

        ReleaseHeldAbility();
        TryChooseSubclass();

        _thinkTimer -= Time.deltaTime;
        if (_thinkTimer <= 0f)
        {
            _thinkTimer = ThinkInterval;
            Think();
        }

        if (!_dashing) Steer();   // durante el dash manda el impulso, no el agente
        Fight();
        DriveAnimator(_agent != null && _agent.enabled ? _agent.velocity : Vector3.zero);
    }

    // Suelta la habilidad de MANTENER que esté abierta. Un jugador la suelta al soltar el
    // botón; un bot la sostiene el tiempo que diga HoldSeconds y la cierra solo. Sin esto
    // el escudo del Paladín se quedaba levantado para siempre y no volvía a salir.
    private void ReleaseHeldAbility()
    {
        if (_held == null || Time.time < _heldUntil) return;

        if (_held.IsHolding) _held.EndHold();
        _held = null;
    }

    // Al llegar al nivel máximo, un jugador elige subclase en un menú. El bot elige una
    // AL AZAR entre las que tenga su clase — que es lo que se pidió, y de paso hace que
    // dos partidas grabadas no se vean iguales.
    private void TryChooseSubclass()
    {
        if (_subclassChosen || _pc.CurrentClassDef == null) return;

        List<CharacterClassDefinition> subs = _pc.CurrentClassDef.AvailableSubclasses;
        if (subs == null || subs.Count == 0) return;

        if (_asc.GetAttributeValue(EAttributeType.Level) < _asc.MaxLevel) return;

        CharacterClassDefinition chosen = subs[Random.Range(0, subs.Count)];
        if (chosen == null) return;

        _subclassChosen = true;
        _pc.ServerEquipClassAsBot(chosen);
        Debug.Log($"[Bots] {name} evolucionó a {chosen.ClassName}.");
    }

    // =========================================================
    // DECISIÓN
    // =========================================================

    private void Think()
    {
        MercenariesGameMode gm  = MercenariesGameMode.Instance;
        MercObjective       obj = gm != null ? gm.ActiveObjective : MercObjective.Instance;

        // 0 · PREPARACIÓN: nadie sale de su base.
        //
        // A un jugador lo frenan las rejas, porque su CharacterController choca con
        // ellas. Un bot camina sobre el NavMesh y las atraviesa como si no existieran
        // —se los veía saliendo al mapa con el reloj todavía en preparación—, así que la
        // regla hay que aplicarla acá.
        if (gm != null && gm.State == EMatchState.Warmup)
        {
            MercTeamBase home = gm.GetBase(_teamId);
            _destination = home != null ? home.SafeRoomWorldCenter : transform.position;
            _target      = null;
            return;
        }

        _retreating = EvaluateRetreat();
        _target     = FindBestTarget();

        // 1 · El Pícaro herido se va a curar y no se distrae con nada.
        if (_retreating && ResolveRole() == EBotRole.Assassin)
        {
            _destination = ResolveHealSpot(gm);
            _target      = null;
            return;
        }

        // 2 · La carga manda sobre todo lo demás.
        if (obj != null)
        {
            if (obj.IsCarried)
            {
                AbilitySystemComponent carrier = obj.ServerCarrier;

                if (carrier == _asc)
                {
                    // La llevo yo: derecho al punto de entrega, sin distraerme.
                    MercTeamBase home = gm != null ? gm.GetBase(_teamId) : null;
                    if (home != null) { _destination = home.DeliveryWorldPoint; return; }
                }
                else if (obj.CarrierTeam == _teamId)
                {
                    // La lleva un compañero: escoltarlo. Peleo con lo que se acerque,
                    // pero sin alejarme de él.
                    if (carrier != null)
                    {
                        _destination = PositionNear(carrier.transform.position, SupportFollowDistance);
                        return;
                    }
                }
                else if (carrier != null)
                {
                    // La lleva un enemigo: ES el objetivo, por encima de cualquier otro.
                    _target      = carrier;
                    _destination = DecideCombatPosition(carrier);
                    return;
                }
            }
            else
            {
                // Libre en el piso: se levanta con solo acercarse.
                _destination = obj.WorldPosition;
                return;
            }
        }

        // 3 · Sin carga en juego: pelear con lo que haya.
        if (_target != null)
        {
            _destination = DecideCombatPosition(_target);
            return;
        }

        // 4 · Nada a la vista. El Paladín se queda con los suyos; el resto va al centro.
        if (ResolveRole() == EBotRole.Support)
        {
            AbilitySystemComponent ally = FindNearestAlly();
            if (ally != null)
            {
                _destination = PositionNear(ally.transform.position, SupportFollowDistance * 0.5f);
                return;
            }
        }

        _destination = ResolveIdleDestination(gm);
    }

    // Retirarse y volver tienen umbrales DISTINTOS a propósito: con uno solo, el Pícaro
    // entraba y salía sin parar bailando alrededor del valor exacto.
    private bool EvaluateRetreat()
    {
        float hp = HealthFraction(_asc);

        return _retreating ? hp < AssassinReturnAboveHealth
                           : hp <= AssassinRetreatBelowHealth;
    }

    // Dónde se cura: su propia sala segura, que es donde la vida vuelve al máximo. Es la
    // misma regla que para un jugador — el que está herido vuelve a la base.
    private Vector3 ResolveHealSpot(MercenariesGameMode gm)
    {
        MercTeamBase home = gm != null ? gm.GetBase(_teamId) : null;
        return home != null ? home.SafeRoomWorldCenter : transform.position;
    }

    // Dónde pararse para pelear con este objetivo. Es lo único que distingue a un rol de
    // otro en el movimiento.
    private Vector3 DecideCombatPosition(AbilitySystemComponent target)
    {
        Vector3 targetPos = target.transform.position;

        switch (ResolveRole())
        {
            case EBotRole.Support:
            {
                // El Paladín se pone ENTRE su compañero y quien lo está atacando: así el
                // cuerpo (y el escudo) le tapan el camino, y como sus ataques son los que
                // curan, queda igual a distancia de pegar. Este no rodea: su posición es
                // el punto, no un adorno.
                AbilitySystemComponent ally = FindNearestAlly();
                if (ally == null) return OrbitPoint(targetPos, MeleeRange * 0.85f);

                Vector3 toAlly = ally.transform.position - targetPos;
                toAlly.y = 0f;
                if (toAlly.sqrMagnitude < 0.01f) return OrbitPoint(targetPos, MeleeRange * 0.85f);

                return targetPos + toAlly.normalized * (MeleeRange * 0.85f);
            }

            default:
            {
                // Bárbaro y Pícaro: encima cuando pueden pegar, y AFUERA mientras el
                // ataque está en cooldown. Quedarse plantado comiendo golpes sin poder
                // responder es lo que los hacía ver como muñecos.
                float ring = PrimaryReady() ? MeleeRange * 0.8f : MeleeRange * BackOffRangeMult;
                return OrbitPoint(targetPos, ring);
            }
        }
    }

    // ¿Puede pegar ya? Se mira el cooldown REAL de la habilidad, no mi propio
    // temporizador: así el "entrar y salir" coincide con lo que el jugador ve pasar.
    private bool PrimaryReady()
    {
        GameplayAbility primary = _pc != null ? _pc.PrimaryAttackAbility : null;
        return primary != null && Time.time >= _nextPrimaryAt && primary.CanActivate();
    }

    // Un punto a "radius" del centro, pero RODEANDO: el ángulo avanza un poco en cada
    // decisión en vez de quedarse en la línea recta enemigo-bot.
    //
    // Sin esto los nueve bots se paraban de frente, quietos, en fila — que es exactamente
    // lo que delata a una máquina. Cada uno rodea para su lado y cambia de sentido cada
    // tantos segundos, así que dos bots contra el mismo enemigo no se mueven en bloque.
    private Vector3 OrbitPoint(Vector3 center, float radius)
    {
        if (Time.time >= _strafeFlipAt)
        {
            _strafeSign   = Random.value < 0.5f ? -1f : 1f;
            _strafeFlipAt = Time.time + StrafeFlipSeconds * Random.Range(0.6f, 1.6f);
        }

        Vector3 away = transform.position - center;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = -transform.forward;

        away = Quaternion.Euler(0f, _strafeSign * StrafeDegreesPerThink, 0f) * away.normalized;
        return center + away * radius;
    }

    // Un punto a "distance" del centro, del lado en el que ya estoy. Sin rodeo: lo usan
    // el que escolta al portador y el Paladín que sigue a un compañero, donde girar
    // alrededor no aporta nada.
    private Vector3 PositionNear(Vector3 center, float distance)
    {
        Vector3 away = transform.position - center;
        away.y = 0f;

        if (away.sqrMagnitude < 0.01f) away = transform.forward;
        return center + away.normalized * distance;
    }

    private Vector3 ResolveIdleDestination(MercenariesGameMode gm)
    {
        if (gm != null && gm.ObjectiveSpawnPoint != null) return gm.ObjectiveSpawnPoint.position;
        return Vector3.zero;
    }

    // =========================================================
    // PERCEPCIÓN
    // =========================================================

    // El enemigo más conveniente a la vista. "Conveniente" es el más cercano, salvo que
    // haya un jugador: un jugador vale más que un NPC porque da más experiencia y porque
    // es contra quien hay que pelear.
    private AbilitySystemComponent FindBestTarget()
    {
        int count = Physics.OverlapSphereNonAlloc(
            transform.position, VisionRadius, _hits, CharacterLayer, QueryTriggerInteraction.Ignore);

        AbilitySystemComponent best = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            AbilitySystemComponent other = _hits[i].GetComponentInParent<AbilitySystemComponent>();
            if (other == null || other == _asc) continue;
            if (other.HasTag(EGameplayTag.State_Dead)) continue;
            if (_asc.IsAllyOf(other, includeSelf: false)) continue;

            float dist = Vector3.Distance(transform.position, other.transform.position);

            // Un jugador cuenta como si estuviera 8 m más cerca de lo que está. No es
            // exacto a propósito: si el NPC te está pegando en la cara, seguís con el NPC.
            bool isPlayer = other.GetComponent<PlayerController>() != null;
            float score = isPlayer ? dist - 8f : dist;

            if (score >= bestScore) continue;
            bestScore = score;
            best      = other;
        }

        return best;
    }

    private AbilitySystemComponent FindNearestAlly()
    {
        int count = Physics.OverlapSphereNonAlloc(
            transform.position, VisionRadius, _hits, CharacterLayer, QueryTriggerInteraction.Ignore);

        AbilitySystemComponent best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            AbilitySystemComponent other = _hits[i].GetComponentInParent<AbilitySystemComponent>();
            if (other == null || other == _asc) continue;
            if (other.HasTag(EGameplayTag.State_Dead)) continue;
            if (!_asc.IsAllyOf(other, includeSelf: false)) continue;

            // Solo compañeros JUGADORES: seguir a un NPC aliado no tiene sentido.
            if (other.GetComponent<PlayerController>() == null) continue;

            float dist = Vector3.Distance(transform.position, other.transform.position);
            if (dist >= bestDist) continue;
            bestDist = dist;
            best     = other;
        }

        return best;
    }

    private float HealthFraction(AbilitySystemComponent asc)
    {
        if (asc == null) return 1f;

        float max = asc.GetAttributeValue(EAttributeType.MaxHealth);
        if (max <= 0f) return 1f;
        return Mathf.Clamp01(asc.GetAttributeValue(EAttributeType.Health) / max);
    }

    // =========================================================
    // MOVIMIENTO Y MIRADA
    // =========================================================

    private void Steer()
    {
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;

        _agent.SetDestination(_destination);

        // El NavMesh de la arena tiene cortes: escalones más altos de lo que el agente
        // puede subir, y bordes donde el horneado no llegó. Cuando el destino queda del
        // otro lado, el agente calcula un camino PARCIAL, llega hasta el borde y se
        // planta ahí para siempre — que es exactamente lo que se veía.
        //
        // Cuando eso pasa se apunta al punto ALCANZABLE más cercano al destino. No lo
        // resuelve del todo (si de verdad no hay paso, no hay paso), pero el bot se sigue
        // moviendo y rodea en vez de quedarse clavado contra un escalón.
        if (!_agent.pathPending && _agent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            if (NavMesh.SamplePosition(_destination, out NavMeshHit hit, 12f, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);
        }

        // Mirar al objetivo si lo hay; si no, hacia donde voy. Un bot que camina de
        // espaldas a su enemigo se ve roto, y encima las habilidades salen para atrás:
        // casi todas usan la rotación del cuerpo como dirección.
        Vector3 look = _target != null
            ? _target.transform.position - transform.position
            : _agent.velocity;

        look.y = 0f;
        if (look.sqrMagnitude < 0.01f) return;

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, Quaternion.LookRotation(look.normalized), 720f * Time.deltaTime);
    }

    // El Animator del jugador lo maneja su dueño (PlayerController.UpdateAnimations, que
    // arranca con IsOwner). Un bot no tiene dueño, así que sus parámetros los escribe
    // acá el servidor — y viajan a los clientes por el NetworkAnimator del prefab.
    private void DriveAnimator(Vector3 velocity)
    {
        if (_anim == null) return;

        Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
        _anim.SetFloat("Speed", flat.magnitude, 0.1f, Time.deltaTime);

        float max = Mathf.Max(0.1f, ResolveMoveSpeed());
        Vector3 local = transform.InverseTransformDirection(flat) / max;
        _anim.SetFloat("MoveX", local.x, 0.1f, Time.deltaTime);
        _anim.SetFloat("MoveY", local.z, 0.1f, Time.deltaTime);
    }

    // =========================================================
    // PELEA
    // =========================================================

    private void Fight()
    {
        if (_target == null || _netASC == null) return;
        if (_target.HasTag(EGameplayTag.State_Dead)) { _target = null; return; }

        float dist = Vector3.Distance(transform.position, _target.transform.position);

        // Punto de mira: el pecho, no los pies. Las habilidades que trazan un rayo desde
        // el personaje fallarían contra el piso.
        Vector3 aim     = _target.transform.position + Vector3.up * 1.2f;
        Vector3 moveDir = transform.forward;

        EBotRole role = ResolveRole();

        if (Time.time >= _nextAbilityAt && dist <= VisionRadius)
        {
            if (TryAbilities(role, dist, aim, moveDir))
                _nextAbilityAt = Time.time + AbilityInterval;
        }

        if (Time.time >= _nextPrimaryAt && dist <= MeleeRange * 1.3f)
        {
            if (Activate(EAbilityInput.PrimaryAttack, aim, moveDir))
                _nextPrimaryAt = Time.time + PrimaryInterval;
        }
    }

    // En qué orden prueba sus habilidades cada rol. No hay nada más fino que esto a
    // propósito: el GAS ya rechaza lo que no se puede usar (cooldown, energía, tags), así
    // que alcanza con proponer en un orden que tenga sentido y dejar que el sistema filtre.
    private bool TryAbilities(EBotRole role, float dist, Vector3 aim, Vector3 moveDir)
    {
        switch (role)
        {
            case EBotRole.Support:
            {
                // Los ataques del Paladín son los que CURAN, así que pega siempre que
                // puede: quedarse a raya era justamente lo que lo volvía inútil.
                //
                // El escudo (una habilidad de MANTENER) sale cuando el enemigo ya está
                // encima, que es cuando sirve para tapar al compañero.
                bool covering = dist <= MeleeRange * 1.6f;

                if (Activate(EAbilityInput.Action1, aim, moveDir)) return true;
                if (covering && Activate(EAbilityInput.SecondaryAttack, aim, moveDir)) return true;
                if (Activate(EAbilityInput.Action2, aim, moveDir)) return true;

                bool trouble = HealthFraction(_asc) < 0.6f || AllyInTrouble();
                if (trouble && Activate(EAbilityInput.Action3, aim, moveDir)) return true;
                return false;
            }

            default:
            {
                // Bárbaro y Pícaro: todo lo que esté listo, sale. La definitiva primero
                // porque es la que decide.
                if (Activate(EAbilityInput.Action3, aim, moveDir)) return true;
                if (Activate(EAbilityInput.Action1, aim, moveDir)) return true;
                if (Activate(EAbilityInput.Action2, aim, moveDir)) return true;
                return false;
            }
        }
    }

    // ¿Algún compañero a la vista está en problemas? Es lo que destraba la definitiva del
    // Paladín: guardarla para cuando de verdad hace falta.
    private bool AllyInTrouble()
    {
        AbilitySystemComponent ally = FindNearestAlly();
        return ally != null && HealthFraction(ally) < 0.6f;
    }

    // Pide una habilidad por el MISMO camino que un jugador de verdad: el servidor valida
    // cooldown, costo y tags, y replica la animación a todos. Si el slot está vacío o la
    // habilidad no se puede usar, no pasa nada.
    private bool Activate(EAbilityInput slot, Vector3 aim, Vector3 moveDir)
    {
        GameplayAbility ability = FindAbility(slot);
        if (ability == null || !ability.CanActivate()) return false;

        // Saltos, dashes y teletransportes los ejecuta la conexión DUEÑA (TargetRpc + el
        // Update del PlayerController). Un bot no tiene dueño: activarlas le cobraría el
        // cooldown sin moverlo un centímetro, y FishNet avisaría en cada intento.
        if (ability.MovesThroughOwner) return false;

        _netASC.ServerActivateAbility(slot, aim, moveDir);

        // Una habilidad de MANTENER queda abierta hasta que alguien suelta el botón, y un
        // bot no tiene botones. Se la anota para soltarla sola (ver Update).
        if (ability is IHoldAbility hold)
        {
            _held      = hold;
            _heldUntil = Time.time + HoldSeconds;
        }

        return true;
    }

    // Las instancias vivas están en el PlayerController, que es donde EquipCharacterClass
    // las deja repartidas por slot.
    private GameplayAbility FindAbility(EAbilityInput slot)
    {
        switch (slot)
        {
            case EAbilityInput.PrimaryAttack:   return _pc.PrimaryAttackAbility;
            case EAbilityInput.SecondaryAttack: return _pc.AimAbility;
            case EAbilityInput.Action1:         return _pc.AbilityQ;
            case EAbilityInput.Action2:         return _pc.AbilityE;
            case EAbilityInput.Action3:         return _pc.AbilityR;
            case EAbilityInput.Movement:        return _pc.MovementAbility;
            default:                            return null;
        }
    }

    // =========================================================
    // ROL
    // =========================================================

    // El rol sale de CUÁL DE LAS CLASES BASE desciende la clase que tiene puesta, usando
    // el orden de MainBaseClasses del prefab del jugador: 0 Bárbaro, 1 Pícaro, 2 Paladín.
    //
    // Se deduce en vez de guardarse en el asset de la clase para no obligar a tocar los
    // doce ScriptableObjects (tres base + nueve subclases) — y porque así una subclase
    // nueva hereda el rol de su rama sin que nadie se acuerde de configurarla.
    private EBotRole ResolveRole()
    {
        if (RoleOverride != EBotRole.Auto) return RoleOverride;

        switch (ResolveBaseClassIndex())
        {
            case 0:  return EBotRole.Bruiser;
            case 1:  return EBotRole.Assassin;
            case 2:  return EBotRole.Support;
            default: return EBotRole.Bruiser;
        }
    }

    private int ResolveBaseClassIndex()
    {
        if (_pc == null || _pc.MainBaseClasses == null || _pc.CurrentClassDef == null) return -1;

        for (int i = 0; i < _pc.MainBaseClasses.Length; i++)
        {
            if (DescendsFrom(_pc.MainBaseClasses[i], _pc.CurrentClassDef)) return i;
        }
        return -1;
    }

    // ¿"target" es "root" o alguna de sus subclases (a cualquier profundidad)?
    private static bool DescendsFrom(CharacterClassDefinition root, CharacterClassDefinition target)
    {
        if (root == null || target == null) return false;
        if (root == target) return true;

        if (root.AvailableSubclasses == null) return false;
        foreach (CharacterClassDefinition sub in root.AvailableSubclasses)
            if (DescendsFrom(sub, target)) return true;

        return false;
    }
}
