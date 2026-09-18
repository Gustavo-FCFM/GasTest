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

    [Header("Colisión del salto y el dash")]
    [Tooltip("Contra qué choca un bot mientras salta o dashea. Todo MENOS la capa 7 " +
             "(Character): los personajes no se frenan entre ellos, igual que en un dash normal.")]
    public LayerMask CollisionMask = ~(1 << 7);

    [Tooltip("Radio de la esfera que barre el camino. Un poco menos que el ancho del " +
             "personaje, para no engancharse en cada esquina.")]
    public float CollisionProbeRadius = 0.35f;

    [Tooltip("A qué altura del pie se hace el barrido, para no chocar contra el propio suelo.")]
    public float CollisionProbeHeight = 0.9f;

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

    [Tooltip("El mismo mínimo, pero MIENTRAS SE ACERCAN. Más corto a propósito: es lo que " +
             "hace que tiren el hacha en el camino en vez de guardarse todo para el cuerpo " +
             "a cuerpo.")]
    public float ApproachAbilityInterval = 0.45f;

    [Tooltip("Mínimo entre dos INVOCACIONES (los tótems del Chamán). Mucho más largo que el " +
             "resto: el cooldown de la habilidad deja invocar seguido, pero un bot que la " +
             "usa apenas puede llena el mapa de tótems y no se ve como juega una persona.")]
    public float SummonInterval = 10f;

    [Header("Criterio")]
    [Tooltip("Radio con el que se cuenta si hay VARIOS enemigos juntos. Decide si vale la " +
             "pena un área, un salto encima del grupo, o la definitiva del Bárbaro.")]
    public float AoeRadius = 5f;

    [Tooltip("Por debajo de esta vida, el Pícaro considera que puede EJECUTAR y suelta su " +
             "definitiva.")]
    [Range(0.05f, 0.9f)] public float ExecuteBelowHealth = 0.4f;

    [Tooltip("Por debajo de esta vida se considera que alguien está EN PELIGRO. Es lo que " +
             "hace que el Paladín deje de pegar y pase a curar, escudar y soltar su " +
             "definitiva.")]
    [Range(0.05f, 0.9f)] public float AllyDangerHealth = 0.3f;

    [Tooltip("Vida a partir de la cual el Paladín SOSTIENE a alguien: escudos y curaciones " +
             "de objetivo. Más alta que AllyDangerHealth a propósito — el Escudo de fe se " +
             "pone ANTES de que la cosa se ponga fea; la definitiva se guarda para cuando ya " +
             "lo está.")]
    [Range(0.1f, 1f)] public float AllySupportHealth = 0.5f;

    [Tooltip("Cuántos segundos de combate trabado antes de gastar un buff. Sin esto se los " +
             "gastan caminando, apenas salen de cooldown, y no los tienen cuando importan.")]
    public float BuffAfterSeconds = 1.5f;

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
    private float _nextSummonAt;
    private bool  _subclassChosen;

    private AbilitySystemComponent _target;
    private bool _retreating;   // el Pícaro está volviendo a curarse
    private bool _chasing;      // persiguiendo al portador de la carga: nada de rodear
    private bool _delivering;   // llevo la carga: correr al punto de entrega
    private IHoldAbility _held;           // habilidad de MANTENER en curso (el escudo)
    private float _heldUntil;
    private Vector3 _destination;

    private float _engagedSince = -99f;   // desde cuándo estoy trabado en combate
    private bool  _targetFleeing;         // el objetivo se está alejando
    private float _lastTargetDistance;
    private float _lastHealth = -1f;      // para saber si me están curando
    private int   _lostChecks;            // decisiones seguidas fuera de lugar

    // Cuántas decisiones seguidas hay que estar perdido antes del rescate. A cuatro por
    // segundo, seis son un segundo y medio: suficiente para descartar un tropiezo.
    private const int LostChecksBeforeRescue = 6;

    private float _strafeSign = 1f;   // hacia qué lado rodea este bot
    private float _strafeFlipAt;      // cuándo cambia de sentido

    private bool      _dashing;      // corriendo un dash: el agente no manda mientras dure
    private Coroutine _dashRoutine;
    private Coroutine _leapRoutine;


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

        // Y las habilidades también, con más margen. Sin esto el equipo entero saltaba EN
        // EL MISMO INSTANTE al abrirse las puertas: todos tenían todo listo y la misma
        // regla los disparaba a la vez. Un poco de desfasaje ya los hace ver distintos.
        _nextAbilityAt = Time.time + Random.Range(0f, 2f);
        _nextSummonAt  = Time.time + Random.Range(0f, SummonInterval * 0.5f);
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

    // Corta el salto o el dash que estuviera corriendo y deja todo en un estado sano.
    //
    // StopCoroutine mata la rutina EN SU YIELD, o sea a mitad de camino: sin esto, una
    // segunda habilidad de movimiento la interrumpía con el agente apagado y las banderas
    // puestas, y nadie las volvía a limpiar.
    private void CancelMovementRoutines()
    {
        if (_dashRoutine != null) { StopCoroutine(_dashRoutine); _dashRoutine = null; }
        if (_leapRoutine != null) { StopCoroutine(_leapRoutine); _leapRoutine = null; }

        _dashing = false;
        RestoreAgent();
    }

    // Deja al agente de vuelta en condiciones de mover al bot. Lo llaman el salto y el
    // dash al terminar, y el propio Update como red de seguridad: cualquier camino que
    // apague el agente y no lo encienda deja al bot congelado, y ese fue exactamente el
    // bug de "de vez en cuando se quedan parados sin hacer nada".
    private void RestoreAgent()
    {
        if (_agent == null) return;

        if (!_agent.enabled) _agent.enabled = true;
        if (_agent.isOnNavMesh) _agent.isStopped = false;
    }

    // Mueve al bot RESPETANDO las paredes.
    //
    // Mueve al bot RESPETANDO las paredes, con un barrido PROPIO.
    //
    // La primera versión usaba CharacterController.Move, que es lo que frena a un jugador.
    // No alcanzó: un bot terminó a 64 m del centro y 12 m bajo el piso en pleno salto. El
    // CharacterController tiene estado que otras partes del juego tocan —excludeLayers lo
    // manipula el dash, y TeleportTo lo apaga y lo prende—, así que no es algo en lo que
    // este script pueda confiar.
    //
    // Un SphereCast no depende de nada de eso: barre el camino que se va a recorrer y, si
    // hay algo, deja al bot justo antes. Es lo mismo que hace el CharacterController por
    // dentro, pero acá el estado lo controlamos nosotros.
    private void MoveWithCollision(Vector3 delta)
    {
        float distance = delta.magnitude;
        if (distance < 0.0001f) return;

        Vector3 dir    = delta / distance;
        Vector3 origin = transform.position + Vector3.up * CollisionProbeHeight;

        // SphereCastAll y no SphereCast: hay que poder SALTEAR lo que no me frena a mí —la
        // pared de mi propia sala segura— y quedarme con lo primero que sí. Con un solo
        // hit, toparse con la pared de casa habría dejado pasar la pared de atrás.
        RaycastHit[] hits = Physics.SphereCastAll(origin, CollisionProbeRadius, dir,
                                                  distance + 0.1f, CollisionMask,
                                                  QueryTriggerInteraction.Ignore);

        float nearest = float.MaxValue;
        bool  blocked = false;

        foreach (RaycastHit hit in hits)
        {
            MercSafeRoomBarrier barrier = hit.collider.GetComponentInParent<MercSafeRoomBarrier>();
            if (barrier != null && barrier.TeamID == _teamId) continue;   // la de casa no frena

            if (hit.distance >= nearest) continue;
            nearest = hit.distance;
            blocked = true;
        }

        if (blocked)
        {
            transform.position += dir * Mathf.Max(0f, nearest - 0.1f);
            return;
        }

        transform.position += delta;
    }

    // Red de seguridad después de un salto o un dash: si el bot quedó fuera de lo
    // navegable —empujado por un impulso raro, o colado por un hueco— se lo devuelve a su
    // base. Uno perdido en el vacío no vuelve solo: se queda cayendo para siempre.
    // ¿Sigue en un lugar donde el juego quiere que esté?
    //
    // El invariante bueno es el NAVMESH: todo lo que el nivel considera transitable —la
    // arena, las rampas, los pasillos y las bases, que están afuera del muro— está
    // horneado. Estar lejos de él significa estar arriba de una pared, en el aire, o
    // afuera del mundo.
    //
    // Se suma un tope de altura porque el techo invisible tiene tres huecos hacia las
    // bases (MercArenaBounds.OpenTowardBases): por ahí se puede salir sin tocar nada.
    private bool IsWhereItShouldBe()
    {
        if (!NavMesh.SamplePosition(transform.position, out _, 3f, NavMesh.AllAreas))
            return false;

        MercArenaBounds bounds = ResolveBounds();
        if (bounds == null) return true;

        return transform.position.y <= bounds.CeilingHeight + 3f;
    }

    private MercArenaBounds _bounds;
    private bool            _boundsChecked;

    private MercArenaBounds ResolveBounds()
    {
        if (_boundsChecked) return _bounds;

        _boundsChecked = true;
        _bounds = FindFirstObjectByType<MercArenaBounds>();
        return _bounds;
    }

    private void RecoverIfLost()
    {
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 6f, NavMesh.AllAreas))
        {
            if (_agent != null && _agent.enabled) _agent.Warp(hit.position);
            return;
        }

        MercenariesGameMode gm = MercenariesGameMode.Instance;
        Transform spawn = gm != null ? gm.GetTeamSpawnPoint(_teamId) : null;

        // El detalle importa: sin saber DÓNDE y HACIENDO QUÉ se escapan, taparlo es
        // adivinar. Si esto aparece siempre con "saltando", el agujero está en el arco.
        Debug.LogWarning($"[Bots] {name} quedó fuera del mapa en {transform.position} " +
                         $"(saltando o dasheando: {_dashing}). Lo devuelvo a su base.");
        if (_pc != null) _pc.ServerTeleportBot(spawn != null ? spawn.position : Vector3.zero,
                                               transform.forward);
    }

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

        CancelMovementRoutines();
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
            // Igual que el salto: por el CharacterController, que es lo que hace que el
            // impulso se frene contra una pared en vez de atravesarla.
            MoveWithCollision(velocity * Time.deltaTime);

            elapsed += Time.deltaTime;
            yield return null;
        }

        _dashing     = false;
        _dashRoutine = null;
        RestoreAgent();
        WarpToNavMesh();
    }

    // =========================================================
    // SALTO
    // =========================================================

    // La llama NetworkAbilitySystemComponent.ServerStartLeap cuando no hay dueño.
    //
    // En un jugador el salto lo aplica su CharacterController y una corutina espera a que
    // vuelva a tocar el piso para resolver el impacto. Un bot no usa el CharacterController,
    // así que acá se simula el arco a mano: sube, cae, y al aterrizar avisa para que el
    // servidor resuelva el golpe en el mismo lugar donde cayó.
    // Calcula con qué velocidad horizontal saltar para CAER DONDE QUIERE.
    //
    // Un jugador apunta el salto y cae ahí. Un bot que solo se impulsa hacia adelante con
    // toda la fuerza de la habilidad recorre, con los números del Bárbaro (subida 15,
    // empuje 15), unos 46 metros — más que el radio de la arena, que es 42. O sea que un
    // salto la cruzaba entera y aterrizaba afuera.
    //
    // Acá se resuelve el tiro: se mide el tiempo de vuelo, se apunta al objetivo, y se
    // recorta a lo que la habilidad permite Y a un punto donde de verdad se pueda estar
    // parado. Si no hay ninguno, se salta en el lugar.
    private Vector3 AimLeapVelocity(Vector3 abilityVelocity, float upVelocity)
    {
        float gravity  = Mathf.Abs(Physics.gravity.y);
        float flight   = 2f * upVelocity / gravity;
        float maxSpeed = abilityVelocity.magnitude;

        if (flight < 0.05f || maxSpeed < 0.01f) return abilityVelocity;

        Vector3 landing = _target != null ? _target.transform.position : _destination;

        Vector3 to = landing - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.01f) return Vector3.zero;

        // Ni más lejos que el objetivo, ni más de lo que la habilidad da.
        float distance = Mathf.Min(to.magnitude, maxSpeed * flight);
        Vector3 wanted = transform.position + to.normalized * distance;

        // Y que el punto exista: si ahí no hay piso navegable, no se salta hacia allá.
        if (!NavMesh.SamplePosition(wanted, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            return Vector3.zero;

        Vector3 corrected = hit.position - transform.position;
        corrected.y = 0f;
        return corrected / flight;
    }

    public void ServerLeap(Vector3 horizontalVelocity, float upVelocity, System.Action onLanded)
    {
        if (!_ready) { onLanded?.Invoke(); return; }

        CancelMovementRoutines();
        _leapRoutine = StartCoroutine(LeapRoutine(AimLeapVelocity(horizontalVelocity, upVelocity),
                                                  upVelocity, onLanded));
    }

    private IEnumerator LeapRoutine(Vector3 horizontalVelocity, float upVelocity, System.Action onLanded)
    {
        _dashing = true;

        // El agente se APAGA, no se pausa: un NavMeshAgent encendido pega el transform al
        // suelo en cada frame, y el salto no tendría altura.
        // El agente se APAGA, no se pausa: encendido pega el transform al suelo en cada
        // frame y el salto no tendría altura. Se vuelve a encender SIEMPRE al terminar —
        // no "si estaba encendido": si un segundo salto interrumpía al primero, la rutina
        // nueva veía el agente ya apagado, concluía que no había que encenderlo, y el bot
        // se quedaba parado para siempre.
        if (_agent != null) _agent.enabled = false;

        float gravity  = Mathf.Abs(Physics.gravity.y);
        float vy       = upVelocity;
        float elapsed  = 0f;
        float startY   = transform.position.y;

        // Dos topes de seguridad, y el de tiempo se calcula del arco REAL.
        //
        // Estaba fijo en 2.5 s, pero el salto del Bárbaro dura 3.06 (dos veces la velocidad
        // de subida sobre la gravedad): se cortaba SIEMPRE a mitad de la bajada, con el bot
        // todavía a varios metros del piso. Ahí corría la red de contención, no encontraba
        // NavMesh cerca —porque estaba en el aire— y lo mandaba a la base. Los bárbaros
        // desaparecían en pleno salto sin que nadie los tocara.
        float maxFlight = 2f * upVelocity / gravity * 1.6f + 0.5f;
        const float maxFall = 12f;

        while (elapsed < maxFlight && transform.position.y > startY - maxFall)
        {
            vy -= gravity * Time.deltaTime;

            MoveWithCollision((horizontalVelocity + Vector3.up * vy) * Time.deltaTime);

            // Aterrizó. NO se pregunta por el CharacterController: su estado lo tocan
            // otras partes del juego y no se le puede creer (ver MoveWithCollision). Un
            // rayo corto hacia abajo es la respuesta directa.
            if (vy < 0f && Physics.Raycast(transform.position + Vector3.up * 0.4f, Vector3.down,
                                           0.7f, CollisionMask, QueryTriggerInteraction.Ignore))
                break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        RestoreAgent();

        // Al aterrizar solo se le avisa al agente dónde quedó. NADA de mandarlo a la base:
        // un salto que salió bien termina donde el bot quiso, y devolverlo era peor que el
        // problema que intentaba resolver.
        WarpToNavMesh();

        _dashing     = false;
        _leapRoutine = null;

        onLanded?.Invoke();
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

        // Si NO estoy en pleno salto o dash, el agente tiene que estar encendido. Ver
        // RestoreAgent: es la red que atrapa cualquier camino que lo haya dejado apagado.
        if (!_dashing) RestoreAgent();

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

            // Contención, antes de decidir nada: si ya está fuera del mundo, decidir a
            // quién pegarle no sirve de nada. Se saltea en pleno salto o dash, que es
            // cuando estar en el aire —lejos del NavMesh— es legítimo.
            // La red de contención, solo para el que se perdió DE VERDAD: hay que estar
            // fuera de lugar varias decisiones seguidas. Con un chequeo instantáneo, un bot
            // que pasaba un instante por un borde raro terminaba en su base sin motivo.
            if (_dashing || IsWhereItShouldBe()) _lostChecks = 0;
            else if (++_lostChecks >= LostChecksBeforeRescue) { _lostChecks = 0; RecoverIfLost(); }

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
        TrackCombatContext();

        // 1 · El Pícaro herido se va a curar y no se distrae con nada.
        if (_retreating && ResolveRole() == EBotRole.Assassin)
        {
            _destination = ResolveHealSpot(gm);
            _target      = null;
            return;
        }

        // 2 · La carga manda sobre todo lo demás. Es la única forma de ganar, así que
        //     cuando está en juego el resto de la partida deja de importar.
        _chasing = false;

        if (obj != null)
        {
            if (obj.IsCarried)
            {
                AbilitySystemComponent carrier = obj.ServerCarrier;

                if (carrier == _asc)
                {
                    // La llevo YO: derecho al punto de entrega, sin distraerme con nadie.
                    // Se limpia el objetivo para no frenarse a pelear en el camino — el que
                    // lleva la carga corre, no se queda cambiando golpes.
                    MercTeamBase home = gm != null ? gm.GetBase(_teamId) : null;
                    if (home != null)
                    {
                        _destination = home.DeliveryWorldPoint;
                        _target      = null;
                        _delivering  = true;
                        return;
                    }
                }
                else if (obj.CarrierTeam == _teamId)
                {
                    // La lleva un COMPAÑERO: escoltarlo, y pelear con lo que se le acerque
                    // A ÉL, no con lo que me quede cómodo a mí. Es la diferencia entre un
                    // equipo que protege al portador y tres bots que van cada uno a lo suyo.
                    if (carrier != null)
                    {
                        AbilitySystemComponent threat = FindEnemyNear(carrier.transform.position,
                                                                     SupportFollowDistance * 2f);
                        if (threat != null)
                        {
                            _target      = threat;
                            _chasing     = true;
                            _destination = DecideCombatPosition(threat);
                            return;
                        }

                        _destination = PositionNear(carrier.transform.position, SupportFollowDistance);
                        return;
                    }
                }
                else if (carrier != null)
                {
                    // La lleva un ENEMIGO: es EL objetivo, por encima de cualquier otro y a
                    // cualquier distancia. Nada de rodear ni despegarse esperando el
                    // cooldown: acá se le va encima y se lo intercepta.
                    _target      = carrier;
                    _chasing     = true;
                    _destination = PositionNear(carrier.transform.position, MeleeRange * 0.7f);
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

        _delivering = false;

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

        // Si LO ESTÁN CURANDO, se queda. Antes bajaba al 35 %, decidía volver a la base, y
        // seguía yendo aunque el Paladín le devolviera la vida en el camino: la decisión
        // solo se revisaba contra el umbral de vuelta (75 %). Que la vida SUBA ya es la
        // señal de que no hace falta irse.
        bool healing = _lastHealth >= 0f && hp > _lastHealth + 0.005f;
        _lastHealth  = hp;

        if (_retreating && healing && hp > AssassinRetreatBelowHealth) return false;

        // Retirarse y volver tienen umbrales DISTINTOS a propósito: con uno solo, el Pícaro
        // entraba y salía sin parar bailando alrededor del valor exacto.
        return _retreating ? hp < AssassinReturnAboveHealth
                           : hp <= AssassinRetreatBelowHealth;
    }

    // Actualiza lo que las reglas de habilidad consultan: si estoy trabado en combate desde
    // hace rato, y si el objetivo se está escapando.
    private void TrackCombatContext()
    {
        if (_target == null)
        {
            _engagedSince       = Time.time;   // sin objetivo, el reloj de "trabado" arranca de cero
            _targetFleeing      = false;
            _lastTargetDistance = -1f;
            return;
        }

        float dist = Vector3.Distance(transform.position, _target.transform.position);

        if (dist > MeleeRange * 1.6f) _engagedSince = Time.time;

        // Se compara contra la medición anterior: si se está alejando, es momento de lo
        // que llega de lejos.
        _targetFleeing      = _lastTargetDistance >= 0f && dist > _lastTargetDistance + 0.25f;
        _lastTargetDistance = dist;
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

            case EBotRole.Assassin:
            {
                // El Pícaro pega POR DETRÁS: su daño a la espalda es mayor (ver
                // BackstabDamageModifier), así que rodear hasta quedar atrás del objetivo
                // no es un adorno, es su forma de pelear.
                if (_chasing) return PositionNear(targetPos, MeleeRange * 0.7f);

                if (!PrimaryReady()) return OrbitPoint(targetPos, MeleeRange * BackOffRangeMult);

                Vector3 behind = targetPos - target.transform.forward * (MeleeRange * 0.75f);
                return NavMesh.SamplePosition(behind, out NavMeshHit hit, 3f, NavMesh.AllAreas)
                     ? hit.position
                     : OrbitPoint(targetPos, MeleeRange * 0.8f);
            }

            default:
            {
                // Bárbaro: encima cuando puede pegar, y AFUERA mientras el ataque está en
                // cooldown. Quedarse plantado comiendo golpes sin poder responder es lo
                // que los hacía ver como muñecos.
                // Persiguiendo al portador de la carga NO se rodea ni se espera el
                // cooldown: se lo intercepta. Bailar alrededor mientras se escapa con la
                // carga es perder la partida con estilo.
                if (_chasing) return PositionNear(targetPos, MeleeRange * 0.7f);

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
    // Corrige un destino que caiga dentro de la sala segura de OTRO equipo.
    //
    // Ahí adentro el enemigo es intocable y se cura, así que entrar no sirve de nada — y
    // desde que las bases expulsan intrusos, un bot que insista queda rebotando contra el
    // borde. Mejor que se plante justo afuera y espere a que salga.
    private Vector3 AvoidEnemySafeRooms(Vector3 destination)
    {
        MercenariesGameMode gm = MercenariesGameMode.Instance;
        if (gm == null) return destination;

        for (int team = 1; team <= MercenariesGameMode.TeamCount; team++)
        {
            if (team == _teamId) continue;

            MercTeamBase b = gm.GetBase(team);
            if (b == null || !b.IsInsideSafeRoom(destination)) continue;

            // Se queda en el borde, del lado por el que venía.
            Vector3 away = destination - b.SafeRoomWorldCenter;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = transform.position - b.SafeRoomWorldCenter;
            if (away.sqrMagnitude < 0.01f) away = Vector3.forward;

            float radius = Mathf.Max(b.SafeRoomSize.x, b.SafeRoomSize.z) * 0.5f + 2f;
            return b.SafeRoomWorldCenter + away.normalized * radius;
        }

        return destination;
    }

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

            // Y SOBRE EL QUE APOSTÉ va primero: el Pirata le pega más fuerte (ver
            // GamblePassive), así que ignorarlo es tirar su pasiva a la basura. El
            // descuento es grande para que gane a cualquier otro a la vista.
            if (other.HasTag(EGameplayTag.Status_Gambled)) score -= 40f;

            if (score >= bestScore) continue;
            bestScore = score;
            best      = other;
        }

        return best;
    }

    // El enemigo más cercano a UN PUNTO que no es el mío. Lo usa la escolta: el que hay
    // que sacar de encima es el que está cerca del portador, no el que me queda cómodo.
    private AbilitySystemComponent FindEnemyNear(Vector3 center, float radius)
    {
        int count = Physics.OverlapSphereNonAlloc(
            center, radius, _hits, CharacterLayer, QueryTriggerInteraction.Ignore);

        AbilitySystemComponent best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            AbilitySystemComponent other = _hits[i].GetComponentInParent<AbilitySystemComponent>();
            if (other == null || other == _asc) continue;
            if (other.HasTag(EGameplayTag.State_Dead)) continue;
            if (_asc.IsAllyOf(other, includeSelf: false)) continue;

            float dist = Vector3.Distance(center, other.transform.position);
            if (dist >= bestDist) continue;
            bestDist = dist;
            best     = other;
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

        _agent.SetDestination(AvoidEnemySafeRooms(_destination));

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
        if (_netASC == null) return;

        // LLEVANDO LA CARGA no hay a quién pegarle, pero SÍ hay algo que hacer: gastar la
        // habilidad de movimiento para llegar antes al punto de entrega. Es lo que haría
        // cualquiera con la carga en la mano.
        if (_delivering)
        {
            if (Time.time >= _nextAbilityAt &&
                Activate(EAbilityInput.Movement, _destination + Vector3.up, DirectionTo(_destination)))
                _nextAbilityAt = Time.time + AbilityInterval;
            return;
        }

        if (_target == null) return;
        if (_target.HasTag(EGameplayTag.State_Dead)) { _target = null; return; }

        float dist = Vector3.Distance(transform.position, _target.transform.position);

        // Punto de mira: el pecho, no los pies. Las habilidades que trazan un rayo desde
        // el personaje fallarían contra el piso.
        Vector3 aim     = _target.transform.position + Vector3.up * 1.2f;
        Vector3 moveDir = transform.forward;

        EBotRole role = ResolveRole();

        if (Time.time >= _nextAbilityAt && dist <= VisionRadius)
        {
            // Lejos prueban más seguido: así castigan mientras se acercan en vez de
            // llegar con todo intacto y recién ahí soltarlo. Persiguiendo al portador, lo
            // mismo — cada segundo cuenta.
            bool approaching = _chasing || dist > MeleeRange * 1.6f;

            if (TryAbilities(role, dist, aim, moveDir))
                _nextAbilityAt = Time.time + (approaching ? ApproachAbilityInterval : AbilityInterval);
        }

        if (Time.time >= _nextPrimaryAt && dist <= MeleeRange * 1.3f)
        {
            if (Activate(EAbilityInput.PrimaryAttack, aim, moveDir))
                _nextPrimaryAt = Time.time + PrimaryInterval;
        }
    }

    private Vector3 DirectionTo(Vector3 point)
    {
        Vector3 d = point - transform.position;
        d.y = 0f;
        return d.sqrMagnitude > 0.01f ? d.normalized : transform.forward;
    }

    // Qué ES una habilidad, para poder decidir CUÁNDO usarla.
    //
    // No hace falta marcar nada a mano en los assets: el TIPO ya lo dice. Las genéricas del
    // proyecto están partidas justo por comportamiento —GA_SelfBuff, GA_ProjectileShoot,
    // GA_InstantAoE, GA_Dash— así que alcanza con mirar de cuál hereda.
    private enum EAbilityKind { Melee, Ranged, Aoe, Buff, Movement }

    private static EAbilityKind Classify(GameplayAbility a)
    {
        if (a is GA_SelfBuff || a is GA_TagSwitch || a is GA_ShieldBlock) return EAbilityKind.Buff;

        // El Golpe final del Inmortal no hereda de ninguna genérica —es su propia clase—
        // pero barre un área por delante: se juzga como área, o sea con varios enemigos
        // juntos. El Molinete sí hereda de GA_ContinuousAoE y entra solo más abajo.
        if (a is GA_FinalBlow) return EAbilityKind.Aoe;

        if (a is GA_Dash || a is GA_LeapAttack || a is GA_Blink || a is GA_HeroicInterception)
            return EAbilityKind.Movement;

        if (a is GA_ProjectileShoot || a is GA_HitscanShot) return EAbilityKind.Ranged;

        if (a is GA_InstantAoE || a is GA_ContinuousAoE || a is GA_ConeAttack || a is GA_LineAttack)
            return EAbilityKind.Aoe;

        return EAbilityKind.Melee;
    }

    // Los slots que se revisan en combate, en el orden en que se proponen. El básico va
    // aparte (ver Fight): ese se usa SIEMPRE que se pueda.
    private static readonly EAbilityInput[] CombatSlots =
    {
        EAbilityInput.Action3,          // la definitiva primero: si corresponde, es la que decide
        EAbilityInput.Movement,
        EAbilityInput.SecondaryAttack,
        EAbilityInput.Action1,
        EAbilityInput.Action2,
    };

    // Recorre TODO el kit y usa lo que tenga sentido AHORA. La regla de cada habilidad sale
    // de dos cosas: qué es (Classify) y qué rol juega este bot.
    private bool TryAbilities(EBotRole role, float dist, Vector3 aim, Vector3 moveDir)
    {
        foreach (EAbilityInput slot in CombatSlots)
        {
            GameplayAbility ability = FindAbility(slot);
            if (ability == null) continue;

            if (!ShouldUse(ability, slot, role, dist)) continue;
            if (Activate(slot, aim, moveDir)) return true;
        }

        return false;
    }

    // El criterio, habilidad por habilidad. Está escrito como lo diría un jugador.
    private bool ShouldUse(GameplayAbility ability, EAbilityInput slot, EBotRole role, float dist)
    {
        bool engaged = dist <= MeleeRange * 1.6f;

        // CASOS CON NOMBRE PROPIO. Van primero porque su momento no se deduce del tipo: son
        // habilidades pensadas para una jugada concreta, y tratarlas como "un área más" o
        // "un buff más" las desperdicia.
        switch (ability)
        {
            // La auto-revivida del Inmortal la dispara la MUERTE (ver
            // PlayerController.HandlePlayerDeath). Gastarla a mano lo deja muriendo de
            // verdad la próxima vez.
            case GA_ImmortalWrath _:
                return false;

            // El señuelo del Ilusionista: se planta cuando alguien está por pegarle, para
            // que le peguen a la copia. Antes salía en cualquier momento y la arena se
            // llenaba de señuelos parados.
            case GA_ExactCopy _:
                return _target != null && dist <= MeleeRange * 1.4f;

            // La fiesta de copias rinde con el equipo junto: cada compañero cerca es una
            // copia más.
            case GA_CopyParty _:
                return AlliesNear(transform.position, AoeRadius * 1.6f) >= 2;

            // El cañoneo del Pirata es daño en área: contra dos o más.
            case GA_CannonBarrage _:
                return EnemiesNear(_target.transform.position, AoeRadius) >= 2;

            // La marca del Asesino es para REMATAR: un jugador con poca vida en el punto.
            case GA_MarkedForDeath _:
                return _target != null
                    && _target.GetComponent<PlayerController>() != null
                    && HealthFraction(_target) <= ExecuteBelowHealth;

            // EL ESCUDO se levanta para DEFENDER, no porque sí. Bloquear también cura, así
            // que es la forma de sostener al equipo sin dejar de estar delante — pero
            // mientras nadie corre peligro conviene PEGAR, porque sus ataques son los que
            // curan. Y además hay que tener al enemigo encima: un escudo contra nadie no
            // hace nada y queda en cooldown.
            case GA_ShieldBlock _:
                return SomeoneInDanger() && dist <= MeleeRange * 1.8f;

            // Las de OBJETIVO dicen a quién apuntan, así que la regla sale del asset:
            // a un ALIADO son curaciones, escudos y protecciones —van cuando alguien está
            // herido—; a un ENEMIGO son marcas, y van sobre el que estoy peleando.
            case GA_Target t:
            {
                if (t.Targets != GA_Target.ETargetSide.Allies) return _target != null;

                // Un escudo o una curación se ponen ANTES (50 %); la definitiva se guarda
                // para la emergencia de verdad (30 %).
                float threshold = slot == EAbilityInput.Action3 ? AllyDangerHealth : AllySupportHealth;
                return SomeoneBelow(threshold);
            }
        }

        // UN ATURDIMIENTO vale contra UNO SOLO: frenar a alguien es tan útil como pegarle,
        // y guardarlo esperando a que se junten dos es desperdiciarlo. Se detecta por el
        // efecto que aplica, no por el nombre de la habilidad.
        if (AppliesStun(ability)) return _target != null;

        // LA DEFINITIVA (R) se juzga por ROL: cada clase la tiene para algo distinto y
        // usarla apenas sale de cooldown es tirarla.
        if (slot == EAbilityInput.Action3)
        {
            switch (role)
            {
                case EBotRole.Support:  return SomeoneInDanger();
                case EBotRole.Assassin: return HealthFraction(_target) <= ExecuteBelowHealth;
                default:                return EnemiesNear(_target.transform.position, AoeRadius) >= 2;
            }
        }

        switch (Classify(ability))
        {
            case EAbilityKind.Buff:
                return ShouldUseBuff(ability, engaged);

            case EAbilityKind.Ranged:
                // Antes del contacto, o cuando el otro se está yendo: es justo cuando un
                // ataque a distancia rinde y el cuerpo a cuerpo no llega. Y con poca vida,
                // siempre — pegar de lejos es lo único que se puede hacer sin morirse.
                return !engaged || _targetFleeing || HealthFraction(_asc) < 0.4f;

            case EAbilityKind.Aoe:
                // Vale la pena con dos o más juntos; con uno solo, el básico alcanza.
                return EnemiesNear(_target.transform.position, AoeRadius) >= 2;

            case EAbilityKind.Movement:
                return ShouldUseMovement(ability, role, dist);

            default:
                return engaged;   // lo cuerpo a cuerpo, en contacto
        }
    }

    // No todos los buffs se usan en el mismo momento, y NO hace falta marcarlos a mano: se
    // mira lo que el efecto HACE.
    //
    //  · Da Status_Invisible  → es un sigilo: se usa ANTES de entrar, para acercarse. Con
    //    la regla general (trabado en combate) el Asesino no lo usaba nunca, que es
    //    justamente lo contrario de para qué existe.
    //  · Sube la vida         → es una curación de emergencia: solo con poca vida.
    //  · Cualquier otro       → buff de pelea: trabado y después de un momento, para no
    //    gastarlo caminando apenas sale de cooldown.
    private bool ShouldUseBuff(GameplayAbility ability, bool engaged)
    {
        GameplayEffect effect = ability is GA_SelfBuff buff ? buff.BuffEffect : null;

        if (GrantsTag(effect, EGameplayTag.Status_Invisible))
            return !engaged && _target != null;

        if (HealsHealth(effect))
            return HealthFraction(_asc) <= 0.45f;

        // Un buff de pelea se usa cuando HAY pelea: con un enemigo cerca, no despues de
        // aguantar un rato trabado. El Castigo del Paladin es justo eso — carga el proximo
        // ataque, asi que llega tarde si espera a estar peleando hace segundo y medio.
        if (_target != null && Vector3.Distance(transform.position, _target.transform.position) <= MeleeRange * 3f)
            return true;

        return engaged && Time.time - _engagedSince >= BuffAfterSeconds;
    }

    // ¿Hay alguien de los míos en problemas? Es lo que hace que el Paladín deje de pegar y
    // pase a sostener: sus curaciones y escudos no se gastan "por si acaso".
    private bool SomeoneInDanger() => SomeoneBelow(AllyDangerHealth);

    // ¿Yo o el compañero más cercano estamos por debajo de esta fracción de vida? El
    // umbral lo pone quien pregunta: sostener empieza antes que la emergencia.
    private bool SomeoneBelow(float fraction)
    {
        if (HealthFraction(_asc) <= fraction) return true;

        AbilitySystemComponent ally = FindNearestAlly();
        return ally != null && HealthFraction(ally) <= fraction;
    }

    // ¿Esta habilidad ATURDE? Se mira el efecto que aplica, no el nombre: así la Presencia
    // conquistadora entra sola, y cualquier aturdimiento nuevo también.
    private static bool AppliesStun(GameplayAbility ability)
    {
        switch (ability)
        {
            case GA_InstantAoE aoe: return AnyGrants(aoe.EffectsToApply, EGameplayTag.State_Stunned);
            case GA_Target t:       return AnyGrants(t.TargetEffects,    EGameplayTag.State_Stunned);
            default:                return false;
        }
    }

    private static bool AnyGrants(List<GameplayEffect> effects, EGameplayTag tag)
    {
        if (effects == null) return false;

        foreach (GameplayEffect e in effects)
            if (GrantsTag(e, tag)) return true;

        return false;
    }

    private static bool GrantsTag(GameplayEffect effect, EGameplayTag tag)
        => effect != null && effect.GrantedTags != null && effect.GrantedTags.Contains(tag);

    private static bool HealsHealth(GameplayEffect effect)
    {
        if (effect == null || effect.Modifiers == null) return false;

        foreach (Modifier m in effect.Modifiers)
            if (m != null && m.Attribute == EAttributeType.Health && m.Magnitude > 0f) return true;

        return false;
    }

    // Cuántos COMPAÑEROS jugadores hay alrededor de un punto. Lo usa la fiesta de copias.
    private int AlliesNear(Vector3 center, float radius)
    {
        int count = Physics.OverlapSphereNonAlloc(
            center, radius, _hits, CharacterLayer, QueryTriggerInteraction.Ignore);

        int allies = 0;
        for (int i = 0; i < count; i++)
        {
            AbilitySystemComponent other = _hits[i].GetComponentInParent<AbilitySystemComponent>();
            if (other == null || other == _asc) continue;
            if (other.HasTag(EGameplayTag.State_Dead)) continue;
            if (!_asc.IsAllyOf(other, includeSelf: false)) continue;
            if (other.GetComponent<PlayerController>() == null) continue;

            allies++;
        }

        return allies;
    }

    // La habilidad de movimiento es la que más cambia de sentido según la clase.
    private bool ShouldUseMovement(GameplayAbility ability, EBotRole role, float dist)
    {
        // ¿HAY TECHO? Un salto sube unos 11 metros con los números del Bárbaro. Bajo el
        // techo de una base, o de cualquier cosa con altura, el bot se estampa contra él y
        // termina encajado entre el piso y lo de arriba — que es exactamente lo que pasó
        // cuando el equipo entero saltó al abrirse las puertas.
        if (ability is GA_LeapAttack leap && !HasHeadroomFor(leap)) return false;

        // Escapar mientras me retiro a curarme, y llegar antes con la carga: eso vale para
        // cualquiera (lo de la carga lo dispara Fight aparte).
        if (_retreating) return true;

        switch (role)
        {
            case EBotRole.Support:
            {
                // La Intervención existe para llegar hasta el compañero en problemas y
                // ponerse delante. Sin alguien a quien socorrer, no se gasta.
                AbilitySystemComponent ally = FindNearestAlly();
                if (ally == null) return false;

                return HealthFraction(ally) < 0.6f
                    && Vector3.Distance(transform.position, ally.transform.position) > SupportFollowDistance;
            }

            case EBotRole.Assassin:
                // Acercarse de golpe para entrar, o rematar al que se escapa.
                return dist > MeleeRange * 2.5f || _targetFleeing;

            default:
            {
                // El salto del Bárbaro es un ataque en área con desplazamiento: se usa para
                // CAER SOBRE VARIOS, o para entrar cuando el otro está a tiro.
                //
                // El tope de distancia importa: sin él, apenas se abrían las puertas TODOS
                // saltaban a la vez hacia un enemigo que estaba en la otra punta del mapa.
                float reach = ability is GA_LeapAttack l ? LeapReach(l) : MeleeRange * 6f;
                if (dist > reach) return false;

                return EnemiesNear(_target.transform.position, AoeRadius) >= 2
                    || dist > MeleeRange * 3f;
            }
        }
    }

    // Hasta dónde llega ese salto: velocidad de avance por el tiempo que dura en el aire.
    private static float LeapReach(GA_LeapAttack leap)
    {
        float gravity = Mathf.Abs(Physics.gravity.y);
        return leap.ForwardForce * (2f * leap.JumpVelocity / gravity);
    }

    // ¿Tiene lugar arriba para el arco completo? Se mide contra la altura máxima real del
    // salto, no contra un número inventado.
    private bool HasHeadroomFor(GA_LeapAttack leap)
    {
        float gravity = Mathf.Abs(Physics.gravity.y);
        float apex    = leap.JumpVelocity * leap.JumpVelocity / (2f * gravity);

        return !Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.up,
                                apex, CollisionMask, QueryTriggerInteraction.Ignore);
    }


    // Cuántos enemigos hay alrededor de un punto. Es lo que distingue "vale la pena el área"
    // de "gastar la definitiva contra un fantasma suelto".
    private int EnemiesNear(Vector3 center, float radius)
    {
        int count = Physics.OverlapSphereNonAlloc(
            center, radius, _hits, CharacterLayer, QueryTriggerInteraction.Ignore);

        int enemies = 0;
        for (int i = 0; i < count; i++)
        {
            AbilitySystemComponent other = _hits[i].GetComponentInParent<AbilitySystemComponent>();
            if (other == null || other == _asc) continue;
            if (other.HasTag(EGameplayTag.State_Dead)) continue;
            if (_asc.IsAllyOf(other, includeSelf: false)) continue;

            enemies++;
        }

        return enemies;
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

        // Lo único que un bot todavía no puede ejecutar. Ver GameplayAbility.MovesThroughOwner.
        if (ability.MovesThroughOwner) return false;

        // Las de RUEDA (los tótems del Chamán) no se activan con Activate(): esperan que
        // alguien elija una opción del menú circular. Un bot no abre menús, así que elige
        // una al azar — que además hace que dos partidas no se vean iguales.
        //
        // Sin esto los chamanes nunca invocaban nada: la habilidad se activaba, no pasaba
        // nada, y el cooldown se pagaba igual.
        if (ability is IRadialMenuAbility radial)
        {
            // Con su propio ritmo, mucho más lento: ver SummonInterval.
            if (Time.time < _nextSummonAt) return false;

            int options = radial.RadialIcons != null ? radial.RadialIcons.Length : 0;
            if (options <= 0) return false;

            _nextSummonAt = Time.time + SummonInterval;

            radial.ActivateWithSelection(Random.Range(0, options), GroundAimPoint(aim, radial.MaxRadialRange));

            // La animación no viaja sola por este camino (el del jugador la manda aparte).
            _netASC.ServerBroadcastAbilityAnimation(ability);
            return true;
        }

        // Las de ZONA leen el punto de mira como cualquier otra, pero ese punto tiene que
        // estar EN EL SUELO y dentro de su alcance: apuntando al pecho del enemigo a 30 m,
        // la zona caía en el aire o fuera de rango.
        if (ability is IGroundTargetAbility ground && ground.UsesGroundTarget)
            aim = GroundAimPoint(aim, ground.MaxTargetRange);

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

    // Un punto EN EL SUELO hacia donde apunto, recortado al alcance de la habilidad. Es lo
    // que espera cualquier habilidad apuntada al piso: zonas, tótems, muros.
    private Vector3 GroundAimPoint(Vector3 aim, float maxRange)
    {
        Vector3 flat = aim - transform.position;
        flat.y = 0f;

        if (maxRange > 0f && flat.magnitude > maxRange) flat = flat.normalized * maxRange;

        Vector3 wanted = transform.position + flat;
        return NavMesh.SamplePosition(wanted, out NavMeshHit hit, 4f, NavMesh.AllAreas)
             ? hit.position
             : wanted;
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
