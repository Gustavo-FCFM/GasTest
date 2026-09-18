using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;
using UnityEngine.AI;

// ============================================================
// MercEnemyAI
//
// Los NPCs que plagan el escenario del modo Mercenarios (los fantasmas de la game
// jam). Son el "PvE" del PvEvP: no son una amenaza real, están para que haya algo que
// hacer entre pelea y pelea y para repartir experiencia a las bolsas de los equipos.
//
// Es la versión EN RED del EnemyAI de la jam, que era de un jugador solo (buscaba al
// PlayerController con FindFirstObjectByType y corría en todas las máquinas). Acá:
//
//   · TODA la decisión (a quién persigo, cuándo pego) corre SOLO en el servidor. Los
//     clientes ven el resultado por el NetworkTransform del prefab y la vida por el
//     NetworkAbilitySystemComponent.
//   · La detección es CORTA a propósito (el diseño pide que no salten encima de los
//     jugadores que pasan cerca): fuera de ese radio ni se enteran, y si los sacás de
//     su zona vuelven solos a su puesto.
//   · La experiencia de matarlo NO se reparte acá: la baja viaja por el camino normal
//     del core (LastAttacker → NetworkASC.AwardKillExperience), que se la deja a la
//     bolsa del EQUIPO del que remató.
//
// El prefab necesita: NetworkObject, NetworkTransform, AbilitySystemComponent,
// NetworkAbilitySystemComponent, NavMeshAgent y este script.
// ============================================================
[RequireComponent(typeof(AbilitySystemComponent))]
public class MercEnemyAI : NetworkBehaviour
{
    // ============================================================
    // EL EQUIPO DE LOS MONSTRUOS
    //
    // Los NPCs son un CUARTO bando, no "neutrales". Antes iban con TeamID 0, que en el
    // core significa "hostil para todos y aliado de nadie", y eso traía dos problemas
    // que solo se ven cuando el enemigo hace algo más que pegar:
    //
    //   · Un aura de buff para los suyos NO alcanzaba a nadie. IsAllyOf devuelve false
    //     apenas uno de los dos es 0, así que el jefe se buffeaba solo.
    //   · Un AoE de daño lastimaba a sus propios fantasmas, porque IsEnemyOf devuelve
    //     true contra cualquiera si uno de los dos es 0.
    //
    // Con un equipo propio (4), los monstruos son aliados entre ellos y enemigos de los
    // tres equipos de jugadores, sin tocar una línea del core. El 4 queda fuera del
    // rango 1..3 del modo, así que ni puntúa, ni suma experiencia, ni aparece en el
    // marcador.
    // ============================================================
    public const int MonsterTeamId = 4;

    // Una habilidad de la rotación, con su propio reloj y su propio alcance. Un jefe con
    // una sola habilidad cada diez segundos es un fantasma lento; lo que lo hace pelea es
    // tener dos o tres cosas que hace en momentos distintos.
    [System.Serializable]
    public class ExtraAbility
    {
        public GameplayAbility Ability;

        [Tooltip("Segundos entre usos de ESTA habilidad. Es independiente de la cadencia " +
                 "general del enemigo y del cooldown propio del asset.")]
        public float Cooldown = 10f;

        [Tooltip("Alcance propio. En 0 usa el Attack Range del enemigo. Sirve para el golpe " +
                 "de área que solo tiene sentido cuando lo tenés encima.")]
        public float Range = 0f;

        [Tooltip("Usarla solo cuando al enemigo le quede ESTA fracción de vida o menos " +
                 "(0.5 = de la mitad para abajo). En 1 se puede usar siempre. Es la forma " +
                 "barata de que un jefe tenga 'segunda fase'.")]
        [Range(0.05f, 1f)] public float UseBelowHealthPercent = 1f;

        [Tooltip("Esperar este tanto desde que aparece antes de usarla por primera vez.")]
        public float InitialDelay = 3f;
    }

    [Header("Percepción")]
    [Tooltip("A qué distancia detecta a un jugador. Corto a propósito: no queremos que " +
             "todo el mapa se le venga encima al que pasa corriendo.")]
    public float DetectionRadius = 9f;

    [Tooltip("Cuánto se puede alejar de su puesto persiguiendo. Pasado eso, vuelve.")]
    public float LeashRadius = 18f;

    [Tooltip("Cada cuánto vuelve a buscar objetivo (segundos). No hace falta cada frame.")]
    public float RetargetInterval = 0.4f;

    [Tooltip("Capa donde viven los personajes. En este proyecto es 'Character' (7).")]
    public LayerMask CharacterLayer = 1 << 7;

    [Header("Combate")]
    [Tooltip("Desde qué distancia puede atacar. Un fantasma a melee: 2. Un mago: 10.")]
    public float AttackRange = 2.2f;

    public float AttackCooldown = 1.6f;

    [Tooltip("Distancia que trata de MANTENER con su objetivo. En 0 se le tira encima (melee); " +
             "con un valor, retrocede si lo tiene más cerca que eso (magos y jefes a distancia).")]
    public float KeepDistance = 0f;

    [Tooltip("Qué tan rápido gira para encarar. Importa más de lo que parece en los enemigos a " +
             "distancia: el proyectil sale hacia ADELANTE, así que si todavía no terminó de girar, " +
             "el tiro se va a cualquier lado.")]
    public float TurnSpeed = 10f;

    [Tooltip("Cuánto puede estar desalineado y aun así disparar, en grados. Hasta que no encara, " +
             "no ataca.")]
    public float AimTolerance = 15f;

    [Tooltip("No atacar si hay una pared en el medio. Con la arena a tres alturas, sin esto los " +
             "magos disparan a través de la meseta y de los tablados.")]
    public bool RequireLineOfSight = true;

    [Tooltip("Qué cuenta como pared para la línea de tiro. Por defecto todo MENOS los personajes " +
             "(si no, un fantasma delante de otro le taparía el tiro).")]
    public LayerMask SightBlockers = ~(1 << 7);

    [Tooltip("Altura de los 'ojos' desde donde se traza la línea de tiro.")]
    public float EyeHeight = 1.4f;

    [Tooltip("Efecto de daño que aplica al golpear. Podés reusar el GE_EnemyDamage de la jam.")]
    public GameplayEffect DamageEffect;

    [Tooltip("Daño plano si no asignaste un GameplayEffect arriba.")]
    public float FallbackDamage = 8f;

    [Tooltip("Opcional: en vez de pegar a melee, activa esta habilidad (enemigos a distancia). " +
             "Es su ataque BÁSICO, el que repite todo el tiempo.")]
    public GameplayAbility AbilityToUse;

    [Tooltip("Habilidades extra con su propio tiempo de espera: la rotación de un jefe. Cuando " +
             "una está lista y en rango, se usa EN LUGAR del ataque básico de ese turno. Se " +
             "revisan en orden, así que poné arriba la más importante.")]
    public List<ExtraAbility> ExtraAbilities = new List<ExtraAbility>();

    [Tooltip("VFX en el jugador al recibir el golpe (opcional).")]
    public GameObject HitVFX;

    [Header("Recompensa")]
    [Tooltip("Experiencia que le da a la bolsa del equipo que lo mate. En 0 usa el valor general " +
             "del modo de juego (XpPerNpcKill). Se pone un número acá cuando este enemigo vale " +
             "más que el resto — un jefe no puede dar lo mismo que un fantasma.")]
    public float ExperienceReward = 0f;

    [Header("Muerte")]
    [Tooltip("Segundos entre que muere y desaparece (deja ver la animación de muerte).")]
    public float DespawnDelay = 2f;

    [Header("Animación")]
    [Tooltip("Parámetro float del Animator con la velocidad de movimiento. Vacío = no se toca.")]
    public string SpeedParameter = "Speed";
    [Tooltip("Trigger del Animator al atacar. Vacío = no se toca.")]
    public string AttackTrigger = "Attack";

    // --- referencias ---
    private AbilitySystemComponent _asc;
    private NavMeshAgent _agent;
    private Animator _animator;

    // --- estado servidor ---
    private Vector3 _homePosition;
    private AbilitySystemComponent _target;
    private GameplayAbility _runtimeAbility;
    private GameplayAbility[] _extraRuntime;   // instancias otorgadas de la rotación
    private float[] _extraNextUse;             // cuándo vuelve a estar lista cada una
    private GameplayEffect _runtimeDamage;
    private float _retargetTimer;
    private float _lastAttackTime;
    private bool _dead;

    // --- estado de presentación (todos los peers) ---
    private Vector3 _lastVisualPos;
    private float _visualSpeed;

    // El spawner que lo creó, para que sepa que tiene que reponerlo.
    [System.NonSerialized] public MercEnemySpawner Spawner;

    private void Awake()
    {
        _asc      = GetComponent<AbilitySystemComponent>();
        _agent    = GetComponent<NavMeshAgent>();
        _animator = GetComponentInChildren<Animator>();
        _lastVisualPos = transform.position;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        _homePosition = transform.position;

        if (_asc != null)
        {
            _asc.TeamID = MonsterTeamId;
            _asc.OnDeath += HandleDeath;

            if (AbilityToUse != null) _runtimeAbility = _asc.GrantAbility(AbilityToUse);
            GrantExtraAbilities();

            // El ASC carga sus atributos en Awake, o sea ANTES de que este objeto
            // exista en la red: esos primeros valores no dispararon ninguna
            // sincronización y los clientes verían al fantasma con la barra de vida en
            // CERO hasta el primer golpe. Un volcado completo acá lo arregla. (Al
            // jugador no le pasa porque su clase se equipa después de spawnear, y ese
            // camino ya reinicializa los atributos.)
            NetworkAbilitySystemComponent netASC = GetComponent<NetworkAbilitySystemComponent>();
            if (netASC != null) netASC.SyncAllAttributesToNet();
        }

        if (_agent != null)
        {
            _agent.enabled = true;

            // La distancia la manejamos NOSOTROS (ver ServerTick), así que el agente no
            // tiene que frenar por su cuenta. Con un stoppingDistance grande —lo que
            // pedía el alcance de un mago— el agente daba por "llegado" cualquier destino
            // cercano y la retirada para mantener distancia no se movía ni un metro.
            _agent.stoppingDistance = 0f;
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // En los clientes la posición llega por el NetworkTransform: un NavMeshAgent
        // activo pelearía contra ella (y encima cada cliente calcularía su propio
        // camino). Solo el servidor navega.
        if (!IsServerInitialized && _agent != null) _agent.enabled = false;
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        if (_asc != null) _asc.OnDeath -= HandleDeath;
        if (_runtimeDamage != null) Destroy(_runtimeDamage);
    }

    private void Update()
    {
        UpdateVisuals();
        if (!IsServerInitialized || _dead) return;
        ServerTick();
    }

    // =========================================================
    // SERVIDOR
    // =========================================================

    [Server]
    private void ServerTick()
    {
        if (_asc == null || _agent == null || !_agent.isOnNavMesh) return;

        // Aturdido/enraizado: se queda quieto (mismo criterio que el EnemyAI de la jam).
        if (_asc.HasTag(EGameplayTag.State_Stunned) || _asc.HasTag(EGameplayTag.State_Rooted))
        {
            _agent.isStopped = true;
            return;
        }

        _agent.speed = Mathf.Max(0.5f, _asc.GetAttributeValue(EAttributeType.MovSpeed));

        _retargetTimer -= Time.deltaTime;
        if (_retargetTimer <= 0f)
        {
            _retargetTimer = RetargetInterval;
            _target = FindTarget();
        }

        // Sin objetivo: volver al puesto.
        if (_target == null)
        {
            float toHome = Vector3.Distance(transform.position, _homePosition);
            if (toHome > 1.5f) { _agent.isStopped = false; _agent.SetDestination(_homePosition); }
            else _agent.isStopped = true;
            return;
        }

        Vector3 targetPos = _target.transform.position;
        float distance    = Vector3.Distance(transform.position, targetPos);

        // Se frena un poco ANTES del alcance máximo: parado justo en el límite, el más
        // mínimo movimiento del jugador lo deja fuera de rango y el enemigo se pasa la
        // pelea entrando y saliendo sin llegar a atacar nunca.
        float stopDistance = AttackRange * 0.9f;

        if (distance > stopDistance)
        {
            // Lejos: perseguir.
            _agent.isStopped = false;
            _agent.SetDestination(targetPos);
        }
        else if (KeepDistance > 0f && distance < KeepDistance)
        {
            // Demasiado cerca y es de los que pelean a distancia: retroceder sin dejar de
            // encararlo. Es lo que hace que un mago se sienta un mago y no un fantasma
            // que dispara.
            Vector3 away = transform.position + (transform.position - targetPos).normalized * 4f;
            _agent.isStopped = false;
            _agent.SetDestination(away);
        }
        else
        {
            _agent.isStopped = true;
        }

        // Encara SIEMPRE que lo tenga a tiro, aunque se esté moviendo: el proyectil sale
        // hacia adelante y con retardo, así que girar es parte de apuntar.
        FaceTarget(targetPos);

        if (Time.time < _lastAttackTime + AttackCooldown) return;
        if (distance > AttackRange)                       return;
        if (!IsAimingAt(targetPos))                       return;
        if (!HasLineOfSight(_target))                     return;

        Attack(distance);
    }

    // ¿Está lo bastante encarado como para que el tiro salga hacia el objetivo?
    private bool IsAimingAt(Vector3 targetPos)
    {
        Vector3 toTarget = targetPos - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.01f) return true;

        return Vector3.Angle(transform.forward, toTarget.normalized) <= AimTolerance;
    }

    // Línea de tiro despejada. Se traza de ojos a pecho (no de pies a pies): con los
    // pivotes en el piso, una línea a ras del suelo choca contra el primer escalón y el
    // enemigo no dispararía nunca.
    private bool HasLineOfSight(AbilitySystemComponent target)
    {
        if (!RequireLineOfSight || target == null) return true;

        Vector3 from = transform.position + Vector3.up * EyeHeight;
        Vector3 to   = target.transform.position + Vector3.up * 1.2f;

        return !Physics.Linecast(from, to, SightBlockers, QueryTriggerInteraction.Ignore);
    }

    // El jugador vivo más cercano dentro del radio de detección, siempre que
    // perseguirlo no lo saque de su zona.
    [Server]
    private AbilitySystemComponent FindTarget()
    {
        AbilitySystemComponent best = null;
        float bestDistance = float.MaxValue;

        Collider[] hits = Physics.OverlapSphere(transform.position, DetectionRadius,
                                                CharacterLayer, QueryTriggerInteraction.Collide);
        foreach (Collider col in hits)
        {
            if (col == null) continue;

            AbilitySystemComponent asc = col.GetComponentInParent<AbilitySystemComponent>();
            if (asc == null || asc == _asc) continue;
            if (asc.GetComponent<PlayerController>() == null) continue;   // solo jugadores
            if (asc.HasTag(EGameplayTag.State_Dead)) continue;
            if (asc.HasTag(EGameplayTag.Status_SafeZone)) continue;       // en su base no se los toca
            if (asc.HasTag(EGameplayTag.Status_Invisible)) continue;

            // Correa: si para alcanzarlo tendría que irse lejos de su puesto, lo ignora.
            if (Vector3.Distance(_homePosition, asc.transform.position) > LeashRadius) continue;

            float d = Vector3.Distance(transform.position, asc.transform.position);
            if (d < bestDistance) { bestDistance = d; best = asc; }
        }

        return best;
    }

    [Server]
    private void Attack(float distanceToTarget)
    {
        _lastAttackTime = Time.time;

        // Primero la rotación: si hay una habilidad especial lista, este turno se la
        // lleva ella y el ataque básico se saltea. Así un jefe alterna en vez de repetir.
        if (TryUseExtraAbility(distanceToTarget)) return;

        if (!string.IsNullOrEmpty(AttackTrigger)) ObserversPlayAttack();

        // Enemigo a distancia: deja que su habilidad haga todo (proyectil, VFX, etc.).
        if (_runtimeAbility != null)
        {
            if (_runtimeAbility.CanActivate()) _runtimeAbility.Activate();
            return;
        }

        if (_target == null) return;

        _target.ApplyGameplayEffect(ResolveDamageEffect(), _asc);

        if (HitVFX != null) ObserversSpawnHitVfx(_target.transform.position + Vector3.up);
    }

    // =========================================================
    // ROTACIÓN DE HABILIDADES
    // =========================================================

    [Server]
    private void GrantExtraAbilities()
    {
        if (ExtraAbilities == null || ExtraAbilities.Count == 0) return;

        _extraRuntime = new GameplayAbility[ExtraAbilities.Count];
        _extraNextUse = new float[ExtraAbilities.Count];

        for (int i = 0; i < ExtraAbilities.Count; i++)
        {
            ExtraAbility extra = ExtraAbilities[i];
            if (extra == null || extra.Ability == null) continue;

            _extraRuntime[i] = _asc.GrantAbility(extra.Ability);
            // El retardo inicial evita que el jefe salude con su golpe más fuerte en el
            // mismo segundo en que aparece, cuando nadie lo vio venir.
            _extraNextUse[i] = Time.time + Mathf.Max(0f, extra.InitialDelay);
        }
    }

    // Busca la primera habilidad de la rotación que esté lista y en rango. Se recorren en
    // ORDEN a propósito: es la forma de decir "esta es la prioritaria" sin inventar un
    // sistema de pesos que nadie va a querer configurar.
    [Server]
    private bool TryUseExtraAbility(float distanceToTarget)
    {
        if (_extraRuntime == null) return false;

        float healthPercent = HealthPercent();

        for (int i = 0; i < _extraRuntime.Length; i++)
        {
            GameplayAbility ability = _extraRuntime[i];
            if (ability == null) continue;

            ExtraAbility extra = ExtraAbilities[i];
            if (Time.time < _extraNextUse[i]) continue;
            if (healthPercent > extra.UseBelowHealthPercent) continue;

            float range = extra.Range > 0f ? extra.Range : AttackRange;
            if (distanceToTarget > range) continue;

            if (!ability.CanActivate()) continue;

            ability.Activate();
            _extraNextUse[i] = Time.time + Mathf.Max(0.5f, extra.Cooldown);
            return true;
        }

        return false;
    }

    // Fracción de vida que le queda (1 = intacto). La usa la "segunda fase" de la rotación.
    private float HealthPercent()
    {
        if (_asc == null) return 1f;

        float max = _asc.GetAttributeValue(EAttributeType.MaxHealth);
        if (max <= 0f) return 1f;

        return Mathf.Clamp01(_asc.GetAttributeValue(EAttributeType.Health) / max);
    }

    // Golpe a melee: si no le pusiste un GameplayEffect, se arma uno en código con el
    // daño plano. Es Hidden e instantáneo, así que no necesita estar registrado.
    private GameplayEffect ResolveDamageEffect()
    {
        if (DamageEffect != null) return DamageEffect;
        if (_runtimeDamage != null) return _runtimeDamage;

        _runtimeDamage = ScriptableObject.CreateInstance<GameplayEffect>();
        _runtimeDamage.name       = "GE_GolpeNPC(runtime)";
        _runtimeDamage.Duration   = 0f;
        _runtimeDamage.EffectType = GameplayEffect.EEffectType.Hidden;
        _runtimeDamage.Modifiers  = new System.Collections.Generic.List<Modifier>
        {
            new Modifier
            {
                Attribute = EAttributeType.Health,
                Type      = Modifier.EModificationType.Add,
                Magnitude = -Mathf.Abs(FallbackDamage),
            }
        };
        return _runtimeDamage;
    }

    [Server]
    private void FaceTarget(Vector3 targetPos)
    {
        Vector3 dir = targetPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;

        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(dir), Time.deltaTime * TurnSpeed);
    }

    // =========================================================
    // MUERTE
    // =========================================================

    // La experiencia NO se reparte acá: NetworkAbilitySystemComponent.HandleDeath ya
    // corre en el servidor con LastAttacker cargado y la manda a la bolsa del equipo
    // que remató (ver MercenariesGameMode.ServerAwardKill).
    [Server]
    private void HandleDeath()
    {
        if (_dead) return;
        _dead = true;

        if (_agent != null && _agent.enabled) _agent.isStopped = true;
        if (Spawner != null) Spawner.ServerNotifyEnemyDied(this);

        Invoke(nameof(ServerDespawnSelf), Mathf.Max(0.1f, DespawnDelay));
    }

    [Server]
    private void ServerDespawnSelf()
    {
        if (IsSpawned) ServerManager.Despawn(gameObject);
    }

    // =========================================================
    // PRESENTACIÓN (todos los peers)
    // =========================================================

    // La velocidad para el Animator se deduce de cuánto se movió el transform, no del
    // NavMeshAgent: en los clientes el agente está apagado, y así los dos lados usan
    // exactamente el mismo cálculo.
    private void UpdateVisuals()
    {
        if (_animator == null) return;

        float dt = Time.deltaTime;
        if (dt > 0f)
        {
            float instant = Vector3.Distance(transform.position, _lastVisualPos) / dt;
            _visualSpeed  = Mathf.Lerp(_visualSpeed, instant, dt * 8f);
            _lastVisualPos = transform.position;
        }

        if (!string.IsNullOrEmpty(SpeedParameter) && HasParameter(SpeedParameter))
            _animator.SetFloat(SpeedParameter, _visualSpeed);
    }

    [ObserversRpc(RunLocally = true)]
    private void ObserversPlayAttack()
    {
        if (_animator != null && HasParameter(AttackTrigger)) _animator.SetTrigger(AttackTrigger);
    }

    [ObserversRpc(RunLocally = true)]
    private void ObserversSpawnHitVfx(Vector3 position)
    {
        if (HitVFX == null) return;
        GameObject vfx = Instantiate(HitVFX, position, Quaternion.identity);
        Destroy(vfx, 1.5f);
    }

    // El Animator del fantasma puede no tener el parámetro (los prefabs de la jam
    // varían). Sin este chequeo, Unity llena la consola de warnings por cada golpe.
    private bool HasParameter(string parameterName)
    {
        if (_animator == null || string.IsNullOrEmpty(parameterName)) return false;
        foreach (var p in _animator.parameters)
            if (p.name == parameterName) return true;
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, DetectionRadius);
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(Application.isPlaying ? _homePosition : transform.position, LeashRadius);
    }
}
