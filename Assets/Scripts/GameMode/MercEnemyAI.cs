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
    public float AttackRange = 2.2f;
    public float AttackCooldown = 1.6f;

    [Tooltip("Efecto de daño que aplica al golpear. Podés reusar el GE_EnemyDamage de la jam.")]
    public GameplayEffect DamageEffect;

    [Tooltip("Daño plano si no asignaste un GameplayEffect arriba.")]
    public float FallbackDamage = 8f;

    [Tooltip("Opcional: en vez de pegar a melee, activa esta habilidad (enemigos a distancia).")]
    public GameplayAbility AbilityToUse;

    [Tooltip("VFX en el jugador al recibir el golpe (opcional).")]
    public GameObject HitVFX;

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
            // Equipo 0 = neutral: hostil para todos, y todos hostiles para él. Es
            // justo lo que queremos de un NPC en un modo de tres equipos.
            _asc.TeamID = 0;
            _asc.OnDeath += HandleDeath;

            if (AbilityToUse != null) _runtimeAbility = _asc.GrantAbility(AbilityToUse);

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
            _agent.stoppingDistance = Mathf.Max(0.5f, AttackRange - 0.6f);
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

        float distance = Vector3.Distance(transform.position, _target.transform.position);

        if (distance > AttackRange)
        {
            _agent.isStopped = false;
            _agent.SetDestination(_target.transform.position);
            return;
        }

        _agent.isStopped = true;
        FaceTarget(_target.transform.position);

        if (Time.time >= _lastAttackTime + AttackCooldown) Attack();
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
    private void Attack()
    {
        _lastAttackTime = Time.time;

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
            Quaternion.LookRotation(dir), Time.deltaTime * 8f);
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
