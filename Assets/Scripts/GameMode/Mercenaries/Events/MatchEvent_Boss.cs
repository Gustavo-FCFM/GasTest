using FishNet.Object;
using UnityEngine;
using UnityEngine.AI;

// ============================================================
// MatchEvent_Boss
//
// El JEFE de mitad de partida (11 de noviembre de 2026, pedido de Gustavo): a "Empieza a los (s)" aparece
// UN jefe en una guarida (BossLair; en el Cementerio, la capilla del centro). Es el jefe de siempre
// ("Tipo de enemigo", del catálogo) más fuerte ("Efecto para el jefe": más vida y daño) y más grande.
//   · Con "Se ve a través de las paredes" lo ven los tres equipos mientras vive (la silueta de la Marca
//     del cazador). Apagado desde el 12 de noviembre de 2026: a Gustavo no le gustó.
//   · El equipo que lo mata recibe "Bendición" (cada jugador vivo, por "Dura la bendición (s)"; se pierde
//     al morir), además de la experiencia normal del jefe. "Experiencia extra" quedó en 0 (12 de
//     noviembre de 2026): a los 7 minutos los equipos ya son nivel 3.
//   · Si nadie lo mata en "Se va a los (s)", se va (sin darle nada a nadie).
//   · Con "Se repite cada (s)" mayor que 0 vuelve a aparecer ese tiempo después de irse o morir.
// Tres avisos: apareció, lo mató tal equipo, se fue (BossSpawned / BossSlain / BossLeft).
//
// Todo en el servidor. El jefe es un NPC normal en red (MercEnemyAI): su vida, su muerte y su silueta
// viajan por los caminos de siempre; este evento no sincroniza nada propio.
// ============================================================
public class MatchEvent_Boss : MatchEvent
{
    [Header("El jefe")]
    [Tooltip("Qué enemigo del catálogo (MercEnemyCatalog) aparece.")]
    [Label("Tipo de enemigo")]
    public EMercEnemyType BossType = EMercEnemyType.Boss;

    [Tooltip("Lo que recibe al aparecer para ser el jefe del evento (más vida y daño). Tiene que durar mucho. " +
             "Vacío = el jefe normal.")]
    [Label("Efecto para el jefe")]
    public GameplayEffect EmpowerEffect;

    [Tooltip("Cuánto más grande que el jefe normal.")]
    [Label("Tamaño (×)")]
    public float SizeMultiplier = 1.4f;

    [Tooltip("Que los tres equipos lo vean a través de las paredes mientras vive.")]
    [Label("Se ve a través de las paredes")]
    public bool RevealToAll = false;

    [Header("Tiempos")]
    [Tooltip("Si nadie lo mata en este tiempo, se va sin darle nada a nadie. 0 = se queda hasta que lo maten.")]
    [Label("Se va a los (s; 0 = nunca)")]
    public float LeaveAfterSeconds = 180f;

    [Tooltip("Cuánto después de morir o irse vuelve a aparecer. 0 = una sola vez por partida.")]
    [Label("Se repite cada (s; 0 = una vez)")]
    public float RepeatSeconds = 0f;

    [Header("Premio")]
    [Tooltip("El buff para cada jugador vivo del equipo que lo mata. Tiene que ser de tipo Buff: así al morir se pierde.")]
    [Label("Bendición")]
    public GameplayEffect Blessing;

    [Label("Dura la bendición (s)")]
    public float BlessingSeconds = 180f;

    [Tooltip("Además de la experiencia que da el jefe al morir (la de su prefab).")]
    [Label("Experiencia extra (exp)")]
    public float BonusXp = 0f;

    // =========================================================
    // SERVIDOR
    // =========================================================

    private MercEnemyAI _boss;
    private NetworkAbilitySystemComponent _bossNet;

    // El jefe del evento si está vivo (solo en el servidor). Lo usan los bots para ir a pelearlo.
    public static MatchEvent_Boss Instance { get; private set; }
    public MercEnemyAI ActiveBoss => _boss != null && !_boss.IsDead ? _boss : null;

    private void Awake()     => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }
    private float _aliveTimer;
    private float _revealTimer;
    private float _repeatTimer = -1f;   // -1 = no hay otro programado
    private bool  _warned;

    protected override void Reset()
    {
        base.Reset();
        StartAt = 420f;   // a los 7 minutos, en una partida de 15
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        MercenariesGameMode.OnServerKill += HandleKill;
        ServerClear();
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        MercenariesGameMode.OnServerKill -= HandleKill;
    }

    protected override void OnEventReset() => ServerClear();

    protected override void OnEventEnd() => ServerClear();

    protected override void OnEventBegin() => ServerSpawnBoss();

    protected override void OnEventTick(float deltaTime)
    {
        if (_boss == null || _boss.IsDead)
        {
            if (_boss != null) _boss = null;   // murió por otro lado (o lo despawnearon)

            if (_repeatTimer >= 0f)
            {
                _repeatTimer -= deltaTime;
                if (_repeatTimer <= 0f) { _repeatTimer = -1f; ServerSpawnBoss(); }
            }
            return;
        }

        // Que lo vean los tres equipos: una revelación corta que se renueva sola.
        if (RevealToAll && _bossNet != null)
        {
            _revealTimer -= deltaTime;
            if (_revealTimer <= 0f)
            {
                _revealTimer = 0.5f;
                for (int team = 1; team <= MercenariesGameMode.TeamCount; team++) _bossNet.ServerRevealToTeam(team, 1f);
            }
        }

        _aliveTimer += deltaTime;
        if (LeaveAfterSeconds > 0f && _aliveTimer >= LeaveAfterSeconds)
        {
            Debug.Log("[Jefe] Nadie lo mató a tiempo: se va.");
            MercEnemyAI leaving = _boss;
            _boss = null;
            _bossNet = null;
            leaving.ServerDismiss();
            Announce(EMatchAnnouncement.BossLeft, 0);
            ScheduleRepeat();
        }
    }

    [Server]
    private void ServerSpawnBoss()
    {
        GameObject prefab = MercEnemyCatalog.Instance != null ? MercEnemyCatalog.Instance.GetPrefab(BossType) : null;
        if (prefab == null || BossLair.All.Count == 0)
        {
            if (!_warned)
                Debug.LogWarning(prefab == null
                    ? $"[Jefe] El catálogo de enemigos no tiene el tipo {BossType}: no aparece el jefe."
                    : "[Jefe] El evento está pero la escena no tiene guarida (BossLair): no aparece el jefe.");
            _warned = true;
            return;
        }

        BossLair lair = BossLair.All[Random.Range(0, BossLair.All.Count)];
        Vector3 position = lair.transform.position;
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 6f, NavMesh.AllAreas)) position = hit.position;

        GameObject go = Instantiate(prefab, position, lair.transform.rotation);
        go.transform.localScale *= Mathf.Max(0.1f, SizeMultiplier);   // antes de spawnear: viaja con él
        go.name = prefab.name + " (Evento)";
        ServerManager.Spawn(go);

        _boss    = go.GetComponent<MercEnemyAI>();
        _bossNet = go.GetComponent<NetworkAbilitySystemComponent>();
        _aliveTimer  = 0f;
        _revealTimer = 0f;

        AbilitySystemComponent asc = _boss != null ? _boss.Asc : go.GetComponent<AbilitySystemComponent>();
        if (asc != null && EmpowerEffect != null)
        {
            asc.ApplyGameplayEffect(EmpowerEffect);
            // Nace lleno, con la vida máxima nueva.
            asc.SetCurrentAttributeValue(EAttributeType.Health, asc.GetAttributeValue(EAttributeType.MaxHealth));
        }

        Announce(EMatchAnnouncement.BossSpawned, 0);
        Debug.Log($"[Jefe] Apareció '{go.name}' en la guarida '{lair.name}' ({position}), " +
                  $"vida {(asc != null ? asc.GetAttributeValue(EAttributeType.MaxHealth) : 0f):0}.");
    }

    // Una baja: si fue el jefe, el equipo que lo mató recibe el premio.
    [Server]
    private void HandleKill(AbilitySystemComponent killer, AbilitySystemComponent victim)
    {
        if (_boss == null || victim == null || victim != _boss.Asc) return;
        _boss = null;
        _bossNet = null;
        ScheduleRepeat();

        int team = killer != null ? killer.TeamID : 0;
        if (!MercenariesGameMode.IsValidTeam(team))
        {
            Debug.Log("[Jefe] Murió sin que lo matara un equipo: nadie recibe el premio.");
            return;
        }

        MercenariesGameMode gm = MercenariesGameMode.Instance;
        int blessed = 0;
        if (gm != null)
        {
            if (Blessing != null)
            {
                foreach (PlayerController pc in gm.Players)
                {
                    if (pc == null) continue;
                    AbilitySystemComponent asc = pc.GetComponent<AbilitySystemComponent>();
                    if (asc == null || asc.TeamID != team || asc.HasTag(EGameplayTag.State_Dead)) continue;
                    asc.ApplyGameplayEffect(Blessing, null, BlessingSeconds);
                    blessed++;
                }
            }
            if (BonusXp > 0f) gm.ServerAddTeamXp(team, BonusXp);
        }

        Announce(EMatchAnnouncement.BossSlain, team, Mathf.Max(1, Mathf.RoundToInt(BlessingSeconds / 60f)));
        Debug.Log($"[Jefe] {MercenariesGameMode.TeamName(team)} lo mató: bendición para {blessed} jugadores y {BonusXp} de experiencia extra.");
    }

    private void ScheduleRepeat()
    {
        _repeatTimer = RepeatSeconds > 0f ? RepeatSeconds : -1f;
    }

    private static void Announce(EMatchAnnouncement type, int team, int extra = 0)
    {
        MercenariesGameMode gm = MercenariesGameMode.Instance;
        if (gm != null) gm.Announce(type, team, extra);
    }

    // Partida nueva o terminada: si quedó un jefe, se va sin darle nada a nadie.
    [Server]
    private void ServerClear()
    {
        if (_boss != null && !_boss.IsDead) _boss.ServerDismiss();
        _boss = null;
        _bossNet = null;
        _repeatTimer = -1f;
        _warned = false;
    }
}
