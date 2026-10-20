using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.AI;

// ============================================================
// Entity_Companion  (las mascotas del Maestro de bestias — 14 de octubre de 2026)
//
// Un compañero animal (Lobo, Oso, Búho) que pelea junto a su Explorador. Toda la decisión
// corre en el SERVIDOR, como los fantasmas (MercEnemyAI); los demás lo ven por el
// NetworkTransform y su vida por el NetworkAbilitySystemComponent. Lo crea y lo saca el
// CompanionManager del dueño.
//
// CÓMO ELIGE A QUIÉN PEGAR (pedido de Gustavo):
//   · PASIVO: cada AttackInterval segundos ataca solo a un enemigo dentro de su rango
//     (PassiveRange, medido desde el DUEÑO): primero jugadores, y el más cercano al dueño.
//     El Búho busca primero a los invisibles. Pegarle lo pone en cooldown.
//   · ORDEN: cuando el Explorador le pega a alguien, el compañero se olvida del cooldown y
//     corre al doble de velocidad a pegarle a ese, aunque esté fuera de su rango. Si estaba
//     fuera, después de ese golpe lo suelta y vuelve a merodear.
//   · DEFENDER: el que le pega al Explorador pasa a ser su objetivo (como una orden).
//   · Se queda con su objetivo mientras siga en rango; si se va, merodea y busca otro.
//   · No ataca invisibles (salvo el Búho, que los prioriza), ni a nadie en su sala segura.
//
// CÓMO SE MUEVE:
//   · Lobo y Oso por el piso (NavMeshAgent). Si el objetivo está en una pared o saltando
//     alto, se quedan debajo esperando; si el Explorador le pega a otro, van a ese.
//   · El Oso se pone DELANTE del Explorador, entre él y el enemigo más cercano (sin
//     taparle la vista), y los fantasmas lo prefieren si cerca no hay otro jugador.
//   · El Búho no busca camino: flota sobre el Explorador, APARECE junto a su objetivo
//     para el picotazo y vuelve.
//   · Si queda lejos del dueño o trabado, reaparece a su lado.
//
// IRA PRIMAL (la definitiva del Maestro de bestias, 15 de octubre de 2026): mientras el dueño
// tiene Status_PrimalFury, el CompanionManager saca a los tres, ataca cada uno el doble de
// rápido (Ira primal: entre ataques) y su presencia suma su versión extra (la misma otra vez:
// la Presencia bestial vale doble).
//
// SU DAÑO ES DEL EXPLORADOR: sus efectos se aplican con el dueño como autor (escalan con
// su ataque, la baja es suya y le carga la definitiva). El CompanionManager ignora esos
// golpes al escuchar "el Explorador pegó", si no la mascota se daría órdenes sola.
//
// Desde el 20 de octubre de 2026 es una invocación (Entity_Summon): el equipo, la regla de
// enemigo, irse si el Explorador se va o cambia de clase, y su propia muerte (resuelta fuera
// del daño, en Update) los resuelve la base. Su "Modelo" es el de la base. Lo de cuál está
// afuera, la Ira primal, la vida guardada y las órdenes sigue en el CompanionManager.
//
// El prefab: NetworkObject, NetworkTransform, AbilitySystemComponent,
// NetworkAbilitySystemComponent, este script y (Lobo y Oso) NavMeshAgent y un collider en
// la capa Character. El Búho, sin collider: no se le puede pegar.
// ============================================================
[RequireComponent(typeof(AbilitySystemComponent))]
public class Entity_Companion : Entity_Summon
{
    [Header("Vida")]
    [Tooltip("Su vida máxima, como fracción de la del Explorador al aparecer (0.5 = la mitad).")]
    [Label("Vida (0 a 1 de la del dueño)")]
    public float HealthFraction = 0.5f;

    [Tooltip("No se le puede hacer daño (el Búho). Igual conviene que su prefab no tenga collider.")]
    [Label("Indestructible")]
    public bool Indestructible;

    [Header("Moverse")]
    [Tooltip("Metros por segundo caminando.")]
    [Label("Velocidad (m/s)")]
    public float MoveSpeed = 6f;

    [Tooltip("Velocidad mientras va a cumplir una orden (el Explorador le pegó a alguien) o a defenderlo.")]
    [Label("Velocidad al ir a una orden (×)")]
    public float ChaseMultiplier = 2f;

    [Tooltip("Dónde se queda junto al dueño cuando no pelea, en el espacio del dueño (X = derecha, " +
             "Y = arriba, Z = adelante). El Búho flota: su Y es la altura.")]
    [Label("Lugar junto al dueño (m)")]
    public Vector3 FollowOffset = new Vector3(1.5f, 0f, -2.5f);

    [Tooltip("Se pone DELANTE del dueño, entre él y el enemigo más cercano (el Oso).")]
    [Label("Se pone delante del dueño")]
    public bool GuardOwner;

    [Tooltip("No camina: flota junto al dueño y para atacar APARECE junto al objetivo (el Búho).")]
    [Label("Ataca apareciendo")]
    public bool BlinkAttack;

    [Tooltip("Si queda más lejos que esto del dueño (sin estar persiguiendo a nadie), reaparece a su lado.")]
    [Label("Reaparece junto al dueño a (m)")]
    public float TeleportDistance = 25f;

    [Tooltip("Hasta qué distancia del dueño acepta una orden. Más lejos, la ignora: no llegaría nunca.")]
    [Label("Alcance de las órdenes (m)")]
    public float CommandRange = 40f;

    [Header("Merodear (sin pelear)")]
    [Tooltip("Sin nadie a quien pegar, da vueltas por la zona del dueño en vez de quedarse pegado " +
             "detrás de él (el Lobo). No vale para el que se pone delante (Oso) ni para el que flota (Búho).")]
    [Label("Merodea alrededor del dueño")]
    public bool Roam = true;

    [Tooltip("Hasta qué distancia del dueño elige los puntos a donde ir.")]
    [Label("Merodear: radio (m)")]
    public float RoamRadius = 6f;

    [Tooltip("Los puntos quedan al menos a esta distancia del dueño: no se le mete entre los pies.")]
    [Label("Merodear: distancia mínima (m)")]
    public float RoamMinRadius = 2.5f;

    [Tooltip("Velocidad al merodear, como fracción de la normal (camina, no corre).")]
    [Label("Merodear: velocidad (×)")]
    public float RoamSpeedFactor = 0.6f;

    [Tooltip("Segundos que se queda quieto al llegar a cada punto (al azar entre los dos).")]
    [Label("Merodear: pausa mínima (s)")]
    public float RoamPauseMin = 1f;

    [Label("Merodear: pausa máxima (s)")]
    public float RoamPauseMax = 3f;

    [Header("Saltar entre pisos")]
    [Tooltip("Si el camino por el piso da mucha vuelta o no llega (otro piso, una plataforma), SALTA " +
             "derecho al punto, si está a esta distancia horizontal o menos.")]
    [Label("Salto: distancia máxima (m)")]
    public float JumpRange = 14f;

    [Tooltip("Cuánto más arriba o abajo puede saltar.")]
    [Label("Salto: altura máxima (m)")]
    public float JumpHeight = 9f;

    [Tooltip("Salta si el camino por el piso es más largo que la línea recta × esto (más unos metros).")]
    [Label("Salto: si el camino da más vuelta que (×)")]
    public float JumpDetourFactor = 1.8f;

    [Label("Salto: espera entre saltos (s)")]
    public float JumpCooldown = 1.5f;

    [Header("Atacar")]
    [Tooltip("Hasta dónde ataca solo, medido desde el DUEÑO (Lobo 20, Oso 12, Búho 30).")]
    [Label("Rango pasivo (m)")]
    public float PassiveRange = 20f;

    [Tooltip("Segundos entre un golpe y el siguiente.")]
    [Label("Entre ataques (s)")]
    public float AttackInterval = 1.2f;

    [Tooltip("A qué distancia (horizontal) del objetivo puede pegarle.")]
    [Label("Alcance del golpe (m)")]
    public float AttackReach = 2.2f;

    [Tooltip("Cuánto más arriba o abajo que él puede estar el objetivo para alcanzarlo. Uno en " +
             "una pared o saltando más alto no lo alcanza: lo espera abajo.")]
    [Label("Alto que alcanza (m)")]
    public float ReachHeight = 2.5f;

    [Tooltip("Lo que le aplica al golpeado, con el Explorador como autor (daño, heridas, " +
             "aturdido...). Cada entrada puede tener su espera por enemigo.")]
    [Label("Efectos del golpe")]
    public List<AbilityEffect> AttackEffects = new List<AbilityEffect>();

    [Tooltip("Ataca a los invisibles, y PRIMERO a ellos (el Búho).")]
    [Label("Caza invisibles")]
    public bool HuntsInvisible;

    [Tooltip("Segundos que el equipo del dueño ve al golpeado a través de las paredes. 0 = no lo revela.")]
    [Label("Revela al golpeado (s)")]
    public float RevealSeconds;

    [Tooltip("Cuánto se acuerda de quien le pegó al dueño para defenderlo.")]
    [Label("Defiende durante (s)")]
    public float DefendMemory = 4f;

    [Tooltip("VFX en el golpeado (opcional).")]
    [Label("VFX del golpe")]
    public GameObject HitVFX;

    [Header("Aullido (Lobo)")]
    [Tooltip("Si muere un enemigo al que mordió hace poco, los aliados cerca reciben esto (se acumula).")]
    [Label("Efecto del aullido")]
    public GameplayEffect HowlEffect;

    [Label("Radio del aullido (m)")]
    public float HowlRadius = 10f;

    [Tooltip("Cuánto tiempo después de morderlo cuenta su muerte para aullar.")]
    [Label("Mordido hace menos de (s)")]
    public float HowlMemory = 5f;

    [Header("Presencia bestial")]
    [Tooltip("Lo que reciben los aliados cerca del compañero O del Explorador (los dos dan lo " +
             "mismo y no se suma: es un GE que se refresca).")]
    [Label("Efecto de presencia")]
    public GameplayEffect PresenceEffect;

    [Label("Radio de presencia (m)")]
    public float PresenceRadius = 8f;

    [Header("Ira primal (la definitiva)")]
    [Tooltip("Durante la Ira primal, el tiempo entre ataques se multiplica por esto. 0.5 = ataca " +
             "el doble de rápido.")]
    [Label("Ira primal: entre ataques (×)")]
    public float FuryAttackIntervalMultiplier = 0.5f;

    [Tooltip("Durante la Ira primal, además de su presencia, los mismos aliados reciben este efecto " +
             "(una copia oculta de la presencia: así vale doble). Vacío = la presencia no sube.")]
    [Label("Ira primal: presencia extra")]
    public GameplayEffect FuryPresenceEffect;

    [Header("Provocar (Oso)")]
    [Tooltip("Los fantasmas lo atacan a él si cerca no hay otro jugador que su dueño.")]
    [Label("Los NPCs lo prefieren")]
    public bool TauntsNpcs;

    [Header("Referencias")]
    [Label("Capa de personajes")]
    public LayerMask CharacterLayer = 1 << 7;

    // Quién es su dueño, para todas las pantallas (el HUD del dueño busca a su compañero). Lo
    // sincroniza la base. Su "Modelo" (Visual, de la base) se adelanta un poco en cada golpe;
    // vacío = el primer hijo.
    public NetworkObject OwnerObject => SummonerObject;

    // Qué opción de la rueda es (0 = la primera), para todas las pantallas: con la Ira primal hay
    // tres del mismo dueño y el HUD tiene que encontrar al elegido.
    private readonly SyncVar<int> _optionIndex = new SyncVar<int>(-1);
    public int OptionIndex => _optionIndex.Value;

    // El Explorador (solo servidor). Es el dueño de la invocación.
    public AbilitySystemComponent OwnerASC => Summoner;
    public AbilitySystemComponent ASC => _asc;

    private AbilitySystemComponent _asc;
    private NavMeshAgent           _agent;
    private CompanionManager       _manager;

    // --- servidor ---
    private AbilitySystemComponent _target;
    private bool  _commanded;            // va a cumplir una orden (corre más rápido)
    private float _attackReadyAt;
    private AbilitySystemComponent _defendFrom;
    private float _defendUntil;
    private float _blinkReturnAt;        // el Búho vuelve junto al dueño a esta hora
    private float _presenceTimer;
    private float _stuckTimer;
    private Vector3 _lastPosition;
    private bool  _dead;
    private bool  _leaping;
    private Vector3 _leapFrom, _leapTo;
    private float _leapStart, _leapDuration, _leapHeight;
    private float _nextPathCheck, _nextLeapAt;
    private bool    _hasRoamPoint;
    private Vector3 _roamPoint;
    private float   _roamWaitUntil;
    private readonly Dictionary<AbilitySystemComponent, float> _bitten = new Dictionary<AbilitySystemComponent, float>();
    private readonly Collider[] _hits = new Collider[48];
    private readonly HashSet<AbilitySystemComponent> _seen = new HashSet<AbilitySystemComponent>();

    // --- todas las pantallas: el tirón del golpe ---
    private Vector3 _visualRest;
    private float   _lungeUntil;

    protected override void Awake()
    {
        base.Awake();
        _asc   = GetComponent<AbilitySystemComponent>();
        _agent = GetComponent<NavMeshAgent>();
        if (Visual == null && transform.childCount > 0) Visual = transform.GetChild(0);
        if (Visual != null) _visualRest = Visual.localPosition;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        // La posición llega por el NetworkTransform: un agente activo pelearía contra ella.
        if (!IsServerInitialized && _agent != null) _agent.enabled = false;
    }

    // Lo llama el CompanionManager en el servidor, justo después de crearlo con
    // Entity_Summon.Spawn (que ya le dio el dueño y el equipo). healthFraction: con cuánta vida
    // sale (la que tenía guardada si se lo cambió herido; 1 = llena). optionIndex: qué opción de
    // la rueda es.
    [Server]
    public void Begin(CompanionManager manager, float healthFraction = 1f, int optionIndex = -1)
    {
        AbilitySystemComponent owner = OwnerASC;
        if (owner == null) return;
        _optionIndex.Value = optionIndex;
        _manager = manager;

        NetworkAbilitySystemComponent net = GetComponent<NetworkAbilitySystemComponent>();

        float max = Indestructible ? 100f
                  : Mathf.Max(1f, owner.GetAttributeValue(EAttributeType.MaxHealth) * HealthFraction);
        float delta = max - _asc.GetAttributeValue(EAttributeType.MaxHealth);
        if (!Mathf.Approximately(delta, 0f)) _asc.UpgradeAttribute(EAttributeType.MaxHealth, delta);
        _asc.SetCurrentAttributeValue(EAttributeType.Health, Mathf.Max(1f, max * Mathf.Clamp01(healthFraction)));

        if (Indestructible) _asc.AddTag(EGameplayTag.Status_Immunity);
        if (TauntsNpcs)     _asc.AddTag(EGameplayTag.Status_Taunting);

        // La vida nueva (la de su dueño), a todas las pantallas.
        if (net != null) net.SyncAllAttributesToNet();

        if (_agent != null && !BlinkAttack)
        {
            _agent.enabled          = true;
            _agent.stoppingDistance = 0f;
            _agent.acceleration     = 40f;
            _agent.angularSpeed     = 720f;
            _agent.autoBraking      = true;
        }
        _lastPosition  = transform.position;
        _attackReadyAt = Time.time + 0.5f;
    }

    // =========================================================
    // ÓRDENES (las da el CompanionManager)
    // =========================================================

    // El Explorador le pegó a 'victim': va a pegarle ya, corriendo.
    [Server]
    public void Command(AbilitySystemComponent victim)
    {
        if (!IsValidEnemy(victim)) return;
        // Muy lejos del dueño no llegaría nunca (y antes se quedaba en un bucle de correr y
        // reaparecer junto al dueño): esa orden se ignora.
        if (FlatDistance(OwnerASC.transform.position, victim.transform.position) > CommandRange) return;
        _target        = victim;
        _commanded     = true;
        _attackReadyAt = Time.time;   // se olvida del cooldown
    }

    // Le pegaron al Explorador: el que le pegó pasa a ser su objetivo.
    [Server]
    public void Defend(AbilitySystemComponent attacker)
    {
        if (!IsValidEnemy(attacker)) return;
        _defendFrom  = attacker;
        _defendUntil = Time.time + DefendMemory;
        if (_target == null || !_commanded)
        {
            _target    = attacker;
            _commanded = true;
        }
    }

    // =========================================================
    // SERVIDOR
    // =========================================================

    protected override void Update()
    {
        base.Update();
        TickLunge();
        if (!IsLiveOnServer || _dead || OwnerASC == null) return;
        if (OwnerASC.HasTag(EGameplayTag.State_Dead)) return;   // el manager lo saca

        TickPresence();
        TickHowl();

        ValidateTarget();
        if (_target == null) AcquireTarget();

        bool stopped = _asc.HasTag(EGameplayTag.State_Stunned) || _asc.HasTag(EGameplayTag.State_Rooted);

        if (BlinkAttack) { TickBlinker(stopped); return; }

        if (_leaping) { TickLeap(); return; }

        if (stopped) { Halt(); return; }
        if (_target != null) ChaseAndAttack();
        else                 FollowOwner();
        TeleportIfLost();
    }

    private bool IsValidEnemy(AbilitySystemComponent other)
    {
        if (other == null || OwnerASC == null || other == _asc || other == OwnerASC) return false;
        if (!IsEnemy(other)) return false;   // la regla de la base (la del ASC)
        if (other.HasTag(EGameplayTag.State_Dead) || other.HasTag(EGameplayTag.Status_SafeZone)) return false;
        if (other.HasTag(EGameplayTag.Status_Immunity) && other.GetComponent<Entity_Companion>() != null) return false;
        if (!HuntsInvisible && other.IsHiddenFromEnemies) return false;
        return true;
    }

    private void ValidateTarget()
    {
        if (_target == null) return;

        bool drop = !IsValidEnemy(_target);
        float fromOwner = FlatDistance(OwnerASC.transform.position, _target.transform.position);

        // Sin orden, se queda con él solo mientras siga en su rango; con orden, hasta un tope.
        if (!_commanded && fromOwner > PassiveRange * 1.15f) drop = true;
        if (_commanded && fromOwner > CommandRange) drop = true;

        if (drop) { _target = null; _commanded = false; }
    }

    private void AcquireTarget()
    {
        // Defender va antes que todo.
        if (_defendFrom != null && Time.time < _defendUntil && IsValidEnemy(_defendFrom))
        {
            _target    = _defendFrom;
            _commanded = true;
            return;
        }

        // El ataque pasivo espera su cooldown.
        if (Time.time < _attackReadyAt) return;
        _target    = FindPassiveTarget();
        _commanded = false;
    }

    // El mejor enemigo en el rango pasivo: primero los invisibles (si los caza), después
    // los jugadores, y entre iguales el más cercano al DUEÑO.
    private AbilitySystemComponent FindPassiveTarget()
    {
        Vector3 center = OwnerASC.transform.position;
        int count = Physics.OverlapSphereNonAlloc(center, PassiveRange, _hits, CharacterLayer, QueryTriggerInteraction.Collide);

        AbilitySystemComponent best = null;
        float bestScore = float.MaxValue;
        _seen.Clear();
        for (int i = 0; i < count; i++)
        {
            AbilitySystemComponent other = _hits[i] != null ? _hits[i].GetComponentInParent<AbilitySystemComponent>() : null;
            if (other == null || !_seen.Add(other) || !IsValidEnemy(other)) continue;

            float score = FlatDistance(center, other.transform.position);
            if (other.GetComponent<PlayerController>() != null) score -= 1000f;
            if (HuntsInvisible && other.HasTag(EGameplayTag.Status_Invisible)) score -= 2000f;

            if (score < bestScore) { bestScore = score; best = other; }
        }
        return best;
    }

    private void ChaseAndAttack()
    {
        Vector3 targetPos = _target.transform.position;
        float flat = FlatDistance(transform.position, targetPos);
        float dy   = targetPos.y - transform.position.y;
        bool inReach = flat <= AttackReach && Mathf.Abs(dy) <= ReachHeight;

        if (inReach) { Halt(); Face(targetPos); }
        else         MoveTo(targetPos, MoveSpeed * (_commanded ? ChaseMultiplier : 1f));

        if (inReach && Time.time >= _attackReadyAt) Attack();
    }

    private void Attack()
    {
        _attackReadyAt = Time.time + AttackInterval * (OwnerInFury ? FuryAttackIntervalMultiplier : 1f);

        bool outOfRange = FlatDistance(OwnerASC.transform.position, _target.transform.position) > PassiveRange;
        AbilitySystemComponent victim = _target;

        ApplyAttack(victim);
        ObserversAttack(victim.transform.position + Vector3.up);

        // Tras una orden fuera de su rango, vuelve a merodear; dentro, se queda con él.
        if (_commanded && outOfRange) _target = null;
        _commanded = false;
    }

    private void ApplyAttack(AbilitySystemComponent victim)
    {
        if (_manager != null) _manager.BeginPetHit();
        try
        {
            foreach (AbilityEffect e in AttackEffects)
            {
                if (e.Effect == null || e.When != EEffectWhen.OnHit) continue;
                if (e.OnlyIfTargetHas != EGameplayTag.None && !victim.HasTag(e.OnlyIfTargetHas)) continue;
                if (e.PerTargetCooldown > 0f && !victim.TryStartEffectCooldown(this, e.Effect, OwnerASC, e.PerTargetCooldown)) continue;

                if (e.ApplyTo == EEffectTarget.Self) OwnerASC.ApplyGameplayEffect(e.Effect, OwnerASC);
                else                                 victim.ApplyGameplayEffect(e.Effect, OwnerASC);
            }
        }
        finally
        {
            if (_manager != null) _manager.EndPetHit();
        }

        if (RevealSeconds > 0f)
        {
            NetworkAbilitySystemComponent victimNet = victim.GetComponent<NetworkAbilitySystemComponent>();
            if (victimNet != null && victimNet.IsServerInitialized) victimNet.ServerRevealToTeam(OwnerASC.TeamID, RevealSeconds);
        }

        _bitten[victim] = Time.time;
    }

    // La vida que le queda, de 0 a 1 (la guarda el manager al cambiarlo por otro).
    public float HealthFraction01
    {
        get
        {
            float max = _asc.GetAttributeValue(EAttributeType.MaxHealth);
            return max > 0f ? Mathf.Clamp01(_asc.GetAttributeValue(EAttributeType.Health) / max) : 1f;
        }
    }

    // Sin objetivo: junto al dueño (el Oso, delante; el Lobo merodea; los demás, a un costado y atrás).
    private void FollowOwner()
    {
        if (Roam && !GuardOwner) { RoamAroundOwner(); return; }

        Transform owner = OwnerASC.transform;
        Vector3 goal = GuardOwner ? GuardPoint() : OwnerPoint(FollowOffset);

        float toOwner = FlatDistance(transform.position, owner.position);
        float speed   = MoveSpeed * (toOwner > 8f ? 1.5f : 1f);

        if (FlatDistance(transform.position, goal) > 1.2f) MoveTo(goal, speed);
        else { Halt(); Face(transform.position + owner.forward); }
    }

    // Da vueltas por la zona del dueño: elige un punto a su alrededor, camina hasta ahí, se
    // queda un rato y elige otro. Si el dueño se aleja, corre a alcanzarlo.
    private void RoamAroundOwner()
    {
        Vector3 ownerPos = OwnerASC.transform.position;
        float toOwner = FlatDistance(transform.position, ownerPos);

        // Quedó lejos: lo alcanza corriendo y elige un punto nuevo al llegar.
        if (toOwner > RoamRadius + 4f)
        {
            _hasRoamPoint = false;
            MoveTo(ownerPos, MoveSpeed * 1.5f);
            return;
        }

        // El punto quedó lejos del dueño (se movió): otro.
        if (_hasRoamPoint && FlatDistance(_roamPoint, ownerPos) > RoamRadius * 1.3f) _hasRoamPoint = false;

        if (!_hasRoamPoint)
        {
            if (Time.time < _roamWaitUntil) { Halt(); return; }
            _hasRoamPoint = TryPickRoamPoint(ownerPos, out _roamPoint);
            if (!_hasRoamPoint) { Halt(); return; }
        }

        if (FlatDistance(transform.position, _roamPoint) > 0.8f)
        {
            MoveTo(_roamPoint, MoveSpeed * RoamSpeedFactor);
            return;
        }

        // Llegó: una pausa mirando hacia afuera, y después otro punto.
        Halt();
        _hasRoamPoint  = false;
        _roamWaitUntil = Time.time + Random.Range(RoamPauseMin, Mathf.Max(RoamPauseMin, RoamPauseMax));
    }

    private bool TryPickRoamPoint(Vector3 ownerPos, out Vector3 point)
    {
        for (int i = 0; i < 6; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            float r = Random.Range(RoamMinRadius, Mathf.Max(RoamMinRadius, RoamRadius));
            Vector3 p = ownerPos + new Vector3(dir.x, 0f, dir.y) * r;
            if (_agent != null && _agent.enabled)
            {
                if (NavMesh.SamplePosition(p, out NavMeshHit hit, 2f, NavMesh.AllAreas) &&
                    Mathf.Abs(hit.position.y - ownerPos.y) < 2.5f)
                { point = hit.position; return true; }
            }
            else { point = p; return true; }
        }
        point = ownerPos;
        return false;
    }

    // Delante del dueño, hacia el enemigo más cercano (o hacia donde mira), corrido a un costado
    // para no taparle la vista.
    private Vector3 GuardPoint()
    {
        Transform owner = OwnerASC.transform;
        Vector3 dir = owner.forward;

        AbilitySystemComponent threat = NearestEnemyPlayer(owner.position, 20f);
        if (threat != null) dir = threat.transform.position - owner.position;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward;

        Vector3 side = Vector3.Cross(Vector3.up, dir);
        return owner.position + dir * 3f + side * 1.2f;
    }

    private AbilitySystemComponent NearestEnemyPlayer(Vector3 center, float radius)
    {
        int count = Physics.OverlapSphereNonAlloc(center, radius, _hits, CharacterLayer, QueryTriggerInteraction.Collide);
        AbilitySystemComponent best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            AbilitySystemComponent other = _hits[i] != null ? _hits[i].GetComponentInParent<AbilitySystemComponent>() : null;
            if (other == null || other.GetComponent<PlayerController>() == null || !IsValidEnemy(other)) continue;
            float d = FlatDistance(center, other.transform.position);
            if (d < bestDist) { bestDist = d; best = other; }
        }
        return best;
    }

    // Un punto en el espacio del dueño, girado solo por su yaw.
    private Vector3 OwnerPoint(Vector3 offset)
    {
        Transform owner = OwnerASC.transform;
        Quaternion yaw = Quaternion.Euler(0f, owner.eulerAngles.y, 0f);
        return owner.position + yaw * offset;
    }

    // =========================================================
    // MOVERSE (por el piso)
    // =========================================================

    private void MoveTo(Vector3 point, float speed)
    {
        if (_agent != null && _agent.enabled)
        {
            if (!_agent.isOnNavMesh) { WarpNear(OwnerASC.transform.position); return; }

            // Un objetivo en una pared o en el aire: el punto del piso más cercano debajo.
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, 4f, NavMesh.AllAreas)) point = hit.position;
            else if (Physics.Raycast(point + Vector3.up * 0.5f, Vector3.down, out RaycastHit ground, 30f, ~(1 << 7 | 1 << 2), QueryTriggerInteraction.Ignore) &&
                     NavMesh.SamplePosition(ground.point, out hit, 4f, NavMesh.AllAreas))
                point = hit.position;

            _agent.speed     = speed;
            _agent.isStopped = false;
            _agent.SetDestination(point);

            // Cada tanto: ¿el camino da mucha vuelta o no llega? Entonces salta derecho.
            if (Time.time >= _nextPathCheck && !_agent.pathPending)
            {
                _nextPathCheck = Time.time + 0.4f;
                if (ShouldLeap(point)) StartLeap(point);
            }
            return;
        }

        // Sin NavMesh: derecho hacia el punto, apoyado en el piso.
        Vector3 next = Vector3.MoveTowards(transform.position, new Vector3(point.x, transform.position.y, point.z), speed * Time.deltaTime);
        if (Physics.Raycast(next + Vector3.up * 2f, Vector3.down, out RaycastHit floor, 6f, ~(1 << 7 | 1 << 2), QueryTriggerInteraction.Ignore))
            next.y = floor.point.y;
        Face(point);
        transform.position = next;
    }

    private void Halt()
    {
        if (_agent != null && _agent.enabled && _agent.isOnNavMesh) _agent.isStopped = true;
    }

    private void Face(Vector3 point)
    {
        Vector3 dir = point - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 12f);
    }

    // Lejos del dueño, o trabado sin avanzar: reaparece a su lado.
    private void TeleportIfLost()
    {
        Vector3 ownerPos = OwnerASC.transform.position;
        float fromOwner = FlatDistance(transform.position, ownerPos);

        // Persiguiendo a alguien puede alejarse (hasta el alcance de las órdenes): reaparecer junto
        // al dueño en medio de la persecución lo dejaba en un bucle sin pegar nunca.
        if (_target == null && fromOwner > TeleportDistance) { WarpNear(ownerPos); return; }
        if (_target != null && fromOwner > CommandRange + 10f)
        {
            _target = null; _commanded = false;
            WarpNear(ownerPos);
            return;
        }

        bool tryingToMove = _agent != null && _agent.enabled && _agent.isOnNavMesh && !_agent.isStopped &&
                            _agent.remainingDistance > 1.5f;
        float moved = (transform.position - _lastPosition).magnitude;
        _lastPosition = transform.position;

        _stuckTimer = tryingToMove && moved < 0.02f ? _stuckTimer + Time.deltaTime : 0f;
        if (_stuckTimer > 2f)
        {
            _stuckTimer = 0f;
            if (_target != null) { _target = null; _commanded = false; }
            WarpNear(ownerPos);
        }
    }

    // =========================================================
    // SALTAR (a otro piso, a una plataforma)
    //
    // El NavMesh no une los pisos de la arena: el camino daba toda la vuelta o no llegaba. Si
    // el camino es mucho más largo que la línea recta (o no llega), y el punto está a tiro y
    // con el aire libre en el medio, salta derecho: un arco sin agente (el NetworkTransform lo
    // lleva a todos) y cae sobre el NavMesh del otro lado.
    // =========================================================

    private bool ShouldLeap(Vector3 destination)
    {
        if (Time.time < _nextLeapAt) return false;

        Vector3 from = transform.position;
        float flat = FlatDistance(from, destination);
        float dy   = Mathf.Abs(destination.y - from.y);
        if (flat > JumpRange || dy > JumpHeight || (flat < 1.5f && dy < 1f)) return false;

        bool unreachable = _agent.pathStatus != NavMeshPathStatus.PathComplete;
        float straight   = Vector3.Distance(from, destination);
        bool detour      = PathLength(_agent.path) > straight * JumpDetourFactor + 4f;
        if (!unreachable && !detour) return false;

        return ArcIsClear(from, destination, LeapHeightFor(from, destination));
    }

    private static float PathLength(NavMeshPath path)
    {
        if (path == null || path.corners.Length < 2) return 0f;
        float length = 0f;
        for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        return length;
    }

    // Cuánto sube el arco por encima de la línea entre los dos puntos.
    private static float LeapHeightFor(Vector3 from, Vector3 to) => 1.2f + Mathf.Abs(to.y - from.y) * 0.5f;

    // Que no atraviese un techo ni una pared: dos tramos, hasta la cima del arco y de ahí abajo.
    private static bool ArcIsClear(Vector3 from, Vector3 to, float height)
    {
        int mask = ~((1 << 7) | (1 << 2));
        Vector3 a    = from + Vector3.up * 0.6f;
        Vector3 b    = to + Vector3.up * 0.6f;
        Vector3 apex = (a + b) * 0.5f + Vector3.up * height;
        return !Physics.Linecast(a, apex, mask, QueryTriggerInteraction.Ignore) &&
               !Physics.Linecast(apex, b, mask, QueryTriggerInteraction.Ignore);
    }

    private void StartLeap(Vector3 destination)
    {
        Vector3 landing = destination;
        if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 2f, NavMesh.AllAreas)) landing = hit.position;

        _leapFrom     = transform.position;
        _leapTo       = landing;
        _leapStart    = Time.time;
        _leapDuration = Mathf.Clamp(FlatDistance(_leapFrom, _leapTo) / 14f, 0.35f, 0.8f);
        _leapHeight   = LeapHeightFor(_leapFrom, _leapTo);
        _leaping      = true;
        _stuckTimer   = 0f;

        if (_agent != null) _agent.enabled = false;   // en el aire no hay NavMesh
    }

    private void TickLeap()
    {
        float t = Mathf.Clamp01((Time.time - _leapStart) / _leapDuration);
        Vector3 p = Vector3.Lerp(_leapFrom, _leapTo, t) + Vector3.up * (4f * _leapHeight * t * (1f - t));
        Face(_leapTo);
        transform.position = p;
        if (t < 1f) return;

        _leaping      = false;
        _nextLeapAt   = Time.time + JumpCooldown;
        _lastPosition = _leapTo;
        if (_agent != null)
        {
            _agent.enabled = true;
            _agent.Warp(_leapTo);
        }
    }

    private void WarpNear(Vector3 ownerPos)
    {
        Vector3 spot = ownerPos + (Quaternion.Euler(0f, OwnerASC.transform.eulerAngles.y, 0f) * new Vector3(1.5f, 0f, -1.5f));
        if (_agent != null && _agent.enabled)
        {
            if (NavMesh.SamplePosition(spot, out NavMeshHit hit, 5f, NavMesh.AllAreas) ||
                NavMesh.SamplePosition(ownerPos, out hit, 5f, NavMesh.AllAreas))
            {
                _agent.Warp(hit.position);
                return;
            }
        }
        transform.position = spot;
    }

    // =========================================================
    // EL BÚHO: flota junto al dueño y aparece junto al objetivo
    // =========================================================

    private void TickBlinker(bool stopped)
    {
        if (_blinkReturnAt > 0f && Time.time < _blinkReturnAt) return;   // está picoteando
        _blinkReturnAt = 0f;

        // Flota sobre el hombro del dueño (suave: en la pantalla de todos se ve volar).
        Vector3 hover = OwnerPoint(FollowOffset);
        transform.position = Vector3.Lerp(transform.position, hover, 1f - Mathf.Exp(-8f * Time.deltaTime));
        Face(transform.position + OwnerASC.transform.forward);

        if (stopped || _target == null || Time.time < _attackReadyAt) return;

        // Aparece un poco delante y arriba del objetivo, del lado del dueño.
        Vector3 targetPos = _target.transform.position;
        Vector3 back = OwnerASC.transform.position - targetPos;
        back.y = 0f;
        back = back.sqrMagnitude > 0.01f ? back.normalized : Vector3.forward;
        transform.position = targetPos + back * 1.2f + Vector3.up * 1.6f;
        Face(targetPos);

        Attack();
        _blinkReturnAt = Time.time + 0.45f;
    }

    // =========================================================
    // PRESENCIA BESTIAL Y AULLIDO
    // =========================================================

    // Cada medio segundo: los aliados cerca del compañero o del dueño reciben la presencia.
    private void TickPresence()
    {
        if (PresenceEffect == null) return;
        _presenceTimer -= Time.deltaTime;
        if (_presenceTimer > 0f) return;
        _presenceTimer = 0.5f;

        _seen.Clear();
        GivePresenceAround(transform.position);
        GivePresenceAround(OwnerASC.transform.position);
        // El compañero también la lleva (el Búho no tiene collider: no sale en la búsqueda).
        if (_seen.Add(_asc) && !_asc.HasTag(EGameplayTag.State_Dead)) GivePresence(_asc);
    }

    // La presencia, y con la Ira primal también la extra (vale doble).
    private void GivePresence(AbilitySystemComponent ally)
    {
        ally.ApplyGameplayEffect(PresenceEffect, OwnerASC);
        if (FuryPresenceEffect != null && OwnerInFury) ally.ApplyGameplayEffect(FuryPresenceEffect, OwnerASC);
    }

    private bool OwnerInFury => OwnerASC != null && OwnerASC.HasTag(EGameplayTag.Status_PrimalFury);

    private void GivePresenceAround(Vector3 center)
    {
        int count = Physics.OverlapSphereNonAlloc(center, PresenceRadius, _hits, CharacterLayer, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            AbilitySystemComponent other = _hits[i] != null ? _hits[i].GetComponentInParent<AbilitySystemComponent>() : null;
            if (other == null || !_seen.Add(other)) continue;
            if (!OwnerASC.IsAllyOf(other, includeSelf: true) || other.HasTag(EGameplayTag.State_Dead)) continue;
            GivePresence(other);
        }
    }

    // Murió alguien que mordió hace poco: aúlla y los aliados cerca pegan más.
    private readonly List<AbilitySystemComponent> _expired = new List<AbilitySystemComponent>();
    private void TickHowl()
    {
        if (_bitten.Count == 0) return;
        _expired.Clear();
        bool howl = false;
        foreach (var pair in _bitten)
        {
            if (pair.Key == null || Time.time - pair.Value > HowlMemory) { _expired.Add(pair.Key); continue; }
            if (pair.Key.HasTag(EGameplayTag.State_Dead)) { howl = true; _expired.Add(pair.Key); }
        }
        foreach (var gone in _expired) _bitten.Remove(gone);

        if (!howl || HowlEffect == null) return;

        int count = Physics.OverlapSphereNonAlloc(transform.position, HowlRadius, _hits, CharacterLayer, QueryTriggerInteraction.Collide);
        _seen.Clear();
        for (int i = 0; i < count; i++)
        {
            AbilitySystemComponent other = _hits[i] != null ? _hits[i].GetComponentInParent<AbilitySystemComponent>() : null;
            if (other == null || !_seen.Add(other)) continue;
            if (!OwnerASC.IsAllyOf(other, includeSelf: true) || other.HasTag(EGameplayTag.State_Dead)) continue;
            other.ApplyGameplayEffect(HowlEffect, OwnerASC);
        }
        ObserversHowl();
    }

    // =========================================================
    // MUERTE
    // =========================================================

    // Su vida llegó a 0 (la base lo avisa en Update, fuera del cálculo del daño): el manager le
    // empieza la espera para volver y el cuerpo se va un poco después.
    protected override void OnOwnDeath()
    {
        if (_dead) return;
        _dead = true;
        Halt();
        if (_manager != null) _manager.OnCompanionDied(this);
        Invoke(nameof(ServerDespawn), 1.2f);
    }

    // Si el Explorador muere, NO se va solo ("Si el dueño muere: se queda"): lo guarda el
    // CompanionManager, que antes anota la vida que le quedaba.

    // =========================================================
    // LO QUE VEN TODOS
    // =========================================================

    [ObserversRpc(RunLocally = true)]
    private void ObserversAttack(Vector3 hitPoint)
    {
        _lungeUntil = Time.time + 0.25f;
        if (HitVFX == null) return;
        GameObject vfx = Instantiate(HitVFX, hitPoint, Quaternion.identity);
        Destroy(vfx, 2f);
    }

    [ObserversRpc(RunLocally = true)]
    private void ObserversHowl()
    {
        // Sin animación todavía: un salto corto del modelo hacia arriba.
        _lungeUntil = Time.time + 0.4f;
    }

    // El modelo se adelanta y vuelve en cada golpe (mientras no haya animaciones).
    private void TickLunge()
    {
        if (Visual == null) return;
        float left = _lungeUntil - Time.time;
        float t = left > 0f ? Mathf.Sin(Mathf.Clamp01(left / 0.25f) * Mathf.PI) : 0f;
        Visual.localPosition = _visualRest + new Vector3(0f, 0.1f * t, 0.45f * t);
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
