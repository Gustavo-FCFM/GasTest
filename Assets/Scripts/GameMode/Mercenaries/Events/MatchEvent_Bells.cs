using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

// ============================================================
// MatchEvent_Bells
//
// Las CAMPANAS de las torres (11 de noviembre de 2026, pedido de Gustavo). Un jugador vivo que GOLPEA
// una campana lista (MatchBell) con cualquier habilidad (de cerca, de lejos, una zona) la toca, y entonces:
//   · Su equipo ve a través de las paredes a cada enemigo que esté a "Revela a (m)" de la campana
//     (medido en el plano: cuenta también el que está abajo de la torre), por "Revela por (s)". Es la
//     misma silueta que la Marca del cazador (NetworkAbilitySystemComponent.ServerRevealToTeam).
//   · A los invisibles de ese radio les pone "Efecto contra invisibles" (GE_Revealed: los enemigos los
//     ven igual) por el mismo tiempo. La campana es la contra del Asesino y del Shinobi.
//   · Todo el mapa se entera: el aviso (EMatchAnnouncement.BellRung) y el sonido de la campana.
//   · La campana descansa "Descansa (s)" antes de poder volver a tocarse.
// El golpe le llega por su StrikeTarget (no recibe daño ni carga la definitiva de quien pega). Los NPCs
// no la tocan y no se revelan; los bots no le pegan (no es enemiga de nadie).
//
// Todo lo decide el servidor. Lo que se sincroniza es hasta qué tick descansa cada campana (en el orden
// de MatchBell.All) y si el evento ya empezó; el sonido y el balanceo llegan por una ObserversRpc.
// ============================================================
public class MatchEvent_Bells : MatchEvent
{
    public static MatchEvent_Bells Instance { get; private set; }

    [Header("Al tocarla")]
    [Tooltip("Hasta dónde revela enemigos, desde la campana, medido en el plano (sin importar la altura).")]
    [Label("Revela a (m)")]
    public float RevealRadius = 25f;

    [Tooltip("Cuánto los ve el equipo que la tocó.")]
    [Label("Revela por (s)")]
    public float RevealSeconds = 4f;

    [Tooltip("Lo que reciben los invisibles del radio para que se los vea (GE_Revealed: da State_AlwaysVisible). " +
             "Dura lo mismo que \"Revela por\". Vacío = los invisibles siguen sin verse.")]
    [Label("Efecto contra invisibles")]
    public GameplayEffect InvisibleRevealEffect;

    [Tooltip("Cuánto tarda una campana en poder volver a tocarse.")]
    [Label("Descansa (s)")]
    public float CooldownSeconds = 45f;

    // =========================================================
    // ESTADO SINCRONIZADO (lo escribe solo el servidor)
    // =========================================================

    private readonly SyncVar<bool>  _open      = new SyncVar<bool>(false);   // el evento empezó
    private readonly SyncList<uint> _readyTick = new SyncList<uint>();      // por campana: lista desde este tick

    // =========================================================
    // LECTURA (cualquier pantalla)
    // =========================================================

    public bool IsReady(int index)
    {
        if (!_open.Value || index < 0 || index >= _readyTick.Count || TimeManager == null) return false;
        return TimeManager.Tick >= _readyTick[index];
    }

    // Cuántos segundos le faltan a esa campana (0 = lista o no aplica).
    public float SecondsUntilReady(int index)
    {
        if (!_open.Value || index < 0 || index >= _readyTick.Count || TimeManager == null) return 0f;
        uint now = TimeManager.Tick, ready = _readyTick[index];
        return ready > now ? (float)TimeManager.TicksToTime(ready - now) : 0f;
    }

    // =========================================================
    // CICLO DE VIDA
    // =========================================================

    protected override void Reset()
    {
        base.Reset();
        StartAt = 0f;
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        // Sesión nueva en el mismo Play: nada de la partida anterior.
        ServerClose();
    }

    // =========================================================
    // SERVIDOR
    // =========================================================

    private float _scanTimer;
    private bool  _warnedNoBells;

    protected override void OnEventReset() => ServerClose();

    protected override void OnEventEnd() => ServerClose();

    protected override void OnEventBegin()
    {
        ServerResizeList();
        for (int i = 0; i < _readyTick.Count; i++) if (_readyTick[i] != 0) _readyTick[i] = 0;
        _open.Value = true;

        if (MatchBell.All.Count == 0 && !_warnedNoBells)
        {
            Debug.LogWarning("[Campanas] El evento está pero el mapa no tiene ninguna campana (MatchBell).");
            _warnedNoBells = true;
        }
    }

    protected override void OnEventTick(float deltaTime)
    {
        // Por si el mapa cambia de campanas (una por lugar, en el orden de MatchBell.All).
        _scanTimer -= deltaTime;
        if (_scanTimer > 0f) return;
        _scanTimer = 1f;
        ServerResizeList();
    }

    // La golpearon (lo avisa MatchBell, que escucha su StrikeTarget). Suena si está lista y quien pegó
    // es un jugador de un equipo, vivo.
    [Server]
    public void ServerStrike(MatchBell bell, AbilitySystemComponent source)
    {
        if (!IsServerInitialized || !_open.Value || bell == null || source == null) return;
        if (!MercenariesGameMode.IsValidTeam(source.TeamID) || source.HasTag(EGameplayTag.State_Dead)) return;

        ServerResizeList();
        int index = bell.Index;
        if (!IsReady(index)) return;

        MercenariesGameMode gm = MercenariesGameMode.Instance;
        if (gm != null) ServerRing(gm, index, bell, source.TeamID);
    }

    [Server]
    private void ServerRing(MercenariesGameMode gm, int index, MatchBell bell, int team)
    {
        _readyTick[index] = TimeManager.Tick + TimeManager.TimeToTicks(Mathf.Max(1f, CooldownSeconds), TickRounding.RoundUp);

        // Los enemigos del radio, en el plano.
        Vector3 center = bell.transform.position;
        float radiusSq = RevealRadius * RevealRadius;
        int revealed = 0;

        foreach (PlayerController pc in gm.Players)
        {
            if (pc == null) continue;
            AbilitySystemComponent asc = pc.GetComponent<AbilitySystemComponent>();
            if (asc == null || asc.TeamID == team || asc.HasTag(EGameplayTag.State_Dead)) continue;

            Vector3 d = pc.transform.position - center;
            d.y = 0f;
            if (d.sqrMagnitude > radiusSq) continue;

            NetworkAbilitySystemComponent net = pc.GetComponent<NetworkAbilitySystemComponent>();
            if (net != null) net.ServerRevealToTeam(team, RevealSeconds);
            if (InvisibleRevealEffect != null && asc.HasTag(EGameplayTag.Status_Invisible))
                asc.ApplyGameplayEffect(InvisibleRevealEffect, null, RevealSeconds);
            revealed++;
        }

        ObserversRing(index);
        gm.Announce(EMatchAnnouncement.BellRung, team, revealed);
        Debug.Log($"[Campanas] {MercenariesGameMode.TeamName(team)} tocó la campana '{bell.name}': {revealed} enemigos revelados.");
    }

    [ObserversRpc]
    private void ObserversRing(int index)
    {
        MatchBell bell = MatchBell.ByIndex(index);
        if (bell != null) bell.PlayRing();
    }

    // Una entrada por campana del mapa.
    [Server]
    private void ServerResizeList()
    {
        int count = MatchBell.All.Count;
        while (_readyTick.Count < count) _readyTick.Add(0);
        while (_readyTick.Count > count) _readyTick.RemoveAt(_readyTick.Count - 1);
    }

    [Server]
    private void ServerClose()
    {
        _open.Value = false;
        _scanTimer  = 0f;
        _readyTick.Clear();
    }
}
