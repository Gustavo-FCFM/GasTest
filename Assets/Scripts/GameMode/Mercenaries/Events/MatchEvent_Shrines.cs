using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

// Los cuatro beneficios de los santuarios. Viajan por la red como número: agregar nuevos al FINAL.
public enum EShrineType
{
    [InspectorName("Daño")]      Damage,
    [InspectorName("Curación")]  Healing,
    [InspectorName("Defensa")]   Defense,
    [InspectorName("Velocidad")] Speed,
}

// Lo que da un santuario de cierto tipo.
[System.Serializable]
public class ShrineBlessing
{
    [Label("Tipo")]
    public EShrineType Type;

    [Tooltip("El buff para cada jugador vivo del equipo que lo captura. Tiene que ser de tipo Buff: así al " +
             "morir se pierde solo. Lo que dura lo pone \"Dura el beneficio (s)\".")]
    [Label("Efecto")]
    public GameplayEffect Effect;

    [Tooltip("El color del santuario de este tipo: el cristal, el borde de la zona, el marcador y el título.")]
    [Label("Color")]
    public Color Color = Color.white;
}

// ============================================================
// MatchEvent_Shrines
//
// Los SANTUARIOS (11 de noviembre de 2026, pedido de Gustavo): un "rey de la montaña". Cada cierto
// tiempo se activa UNO, al azar entre los del mapa (MatchShrine), con un beneficio al azar: daño,
// curación, defensa o velocidad. Alrededor aparece una zona negra y todas las pantallas ven cuánto
// lleva cada equipo.
//
// CÓMO SE CAPTURA (las cuentas están en ShrineCaptureRules):
//   · Cada segundo con alguien vivo dentro: +1 % para ese equipo.
//   · Cada NPC que muere dentro de la zona: +5 % para el equipo que lo mató.
//   · Cada jugador que muere dentro de la zona: +10 % para el equipo que lo mató (lo que cuenta es
//     dónde muere: un Explorador desde un balcón también suma).
//   · Nadie llega a 100 con otro equipo vivo dentro: se frena en 99.
//   · El primero en llegar a 100 lo captura: cada jugador vivo de su equipo recibe el beneficio por
//     3 minutos (se pierde al morir) y la bolsa del equipo +75 de experiencia.
// Después de una captura, el siguiente santuario se activa un minuto más tarde.
//
// LOS NPCs DE LA ZONA (11 de noviembre de 2026, también pedido de Gustavo): mientras un santuario está
// activo salen enemigos extra (el campamento "ExtraEnemies" de cada santuario, ver MatchShrine), y todo
// NPC que esté DENTRO de la zona recibe "Efecto para los NPCs" (+50 % de vida máxima y +2 de ataque):
// se le pone al entrar, se le quita al salir o cuando el santuario se apaga. La vida máxima que gana
// también se le cura al entrar, una sola vez por santuario (así entrar y salir no los cura).
//
// Todo lo decide el servidor. Las pantallas leen lo sincronizado (cuál, de qué tipo, cuánto lleva cada
// equipo, quién está dentro y cuánto falta para el próximo): UI_ShrineHud, el marcador del santuario
// (UI_ObjectiveMarker) y la zona de cada MatchShrine.
// ============================================================
public class MatchEvent_Shrines : MatchEvent
{
    public static MatchEvent_Shrines Instance { get; private set; }

    private const int TeamCount = MercenariesGameMode.TeamCount;

    [Header("Ritmo")]
    [Tooltip("Después de una captura, cuánto tarda en activarse el siguiente.")]
    [Label("Siguiente santuario tras (s)")]
    public float SecondsBetween = 60f;

    [Tooltip("Si nadie lo captura en este tiempo, se apaga y viene otro. En 0 queda activo hasta que lo capturen.")]
    [Label("Activo como máximo (s; 0 = sin tope)")]
    public float MaxActiveSeconds = 0f;

    [Header("Captura")]
    [Label("Por segundo dentro (%)")]
    public int PercentPerSecond = 1;

    [Label("Por NPC eliminado en la zona (%)")]
    public int PercentPerNpcKill = 5;

    [Label("Por jugador eliminado en la zona (%)")]
    public int PercentPerPlayerKill = 10;

    [Tooltip("Hasta dónde llega un equipo mientras haya otro equipo vivo dentro.")]
    [Label("Tope con otro equipo dentro (%)")]
    public int ContestedCap = 99;

    [Header("Premio")]
    [Label("Experiencia para el equipo (exp)")]
    public float CaptureXp = 75f;

    [Label("Dura el beneficio (s)")]
    public float BlessingSeconds = 180f;

    [Tooltip("Un beneficio por tipo.")]
    [Label("Beneficios")]
    public ShrineBlessing[] Blessings = new ShrineBlessing[0];

    [Header("NPCs en la zona")]
    [Tooltip("Lo que reciben los NPCs mientras están dentro de la zona del santuario activo (más vida, " +
             "más daño, y su aura). Tiene que ser de tipo Buff y durar mucho: se les quita a mano al salir. " +
             "Vacío = los NPCs no cambian.")]
    [Label("Efecto para los NPCs")]
    public GameplayEffect NpcEmpowerEffect;

    [Tooltip("Prendido: solo los enemigos extra del santuario. Apagado: cualquier NPC que esté dentro de " +
             "la zona (también los del campamento de al lado).")]
    [Label("Solo los enemigos del santuario")]
    public bool EmpowerOnlyShrineEnemies = false;

    // =========================================================
    // ESTADO SINCRONIZADO (lo escribe solo el servidor)
    // =========================================================

    private readonly SyncVar<int> _activeIndex = new SyncVar<int>(-1);   // en MatchShrine.All; -1 = ninguno
    private readonly SyncVar<int> _type        = new SyncVar<int>(0);    // EShrineType
    private readonly SyncVar<int> _inside      = new SyncVar<int>(0);    // un bit por equipo con alguien dentro
    private readonly SyncList<int> _progress   = new SyncList<int>();    // % por equipo (índice 0 = equipo 1)

    // Segundos hasta el próximo santuario (0 = hay uno activo o no aplica). Se manda cada medio segundo y
    // cada pantalla lo descuenta sola entre paquetes, como el reloj del marcador.
    private readonly SyncVar<float> _nextEta   = new SyncVar<float>(0f);
    private float _nextEtaReceivedAt;

    // =========================================================
    // LECTURA (cualquier pantalla)
    // =========================================================

    public MatchShrine ActiveShrine => MatchShrine.ByIndex(_activeIndex.Value);
    public bool IsActive => ActiveShrine != null;
    public EShrineType ActiveType => (EShrineType)_type.Value;

    public int GetProgress(int team) =>
        MercenariesGameMode.IsValidTeam(team) && _progress.Count == TeamCount ? _progress[team - 1] : 0;

    public bool IsTeamInside(int team) => MercenariesGameMode.IsValidTeam(team) && (_inside.Value & (1 << team)) != 0;

    // Dos equipos o más dentro: nadie puede pasar de 99.
    public bool IsContested
    {
        get
        {
            int count = 0;
            for (int team = 1; team <= TeamCount; team++) if (IsTeamInside(team)) count++;
            return count >= 2;
        }
    }

    // Cuánto falta para el próximo santuario (0 = hay uno activo, o no aplica).
    public float NextShrineEta
    {
        get
        {
            float eta = Mathf.Max(0f, _nextEta.Value - (Time.time - _nextEtaReceivedAt));
            if (eta > 0f || IsActive) return eta;

            // Antes del primero: lo que falta para "Empieza a los (s)". Sale del reloj de la partida, que
            // todas las máquinas tienen, así que sirve también al que entra tarde.
            MercenariesGameMode gm = MercenariesGameMode.Instance;
            return gm != null && gm.State == EMatchState.Playing ? Mathf.Max(0f, StartAt - gm.MatchElapsedSeconds) : 0f;
        }
    }

    public ShrineBlessing GetBlessing(EShrineType type)
    {
        if (Blessings != null)
            foreach (ShrineBlessing b in Blessings)
                if (b != null && b.Type == type) return b;
        return null;
    }

    public Color GetColor(EShrineType type)
    {
        ShrineBlessing b = GetBlessing(type);
        return b != null ? b.Color : Color.white;
    }

    // El nombre del tipo en el idioma de esta pantalla ("DAÑO", "CURACIÓN"…).
    public static string TypeName(EShrineType type)
    {
        switch (type)
        {
            case EShrineType.Damage:  return Loc.T("shrine.damage");
            case EShrineType.Healing: return Loc.T("shrine.healing");
            case EShrineType.Defense: return Loc.T("shrine.defense");
            case EShrineType.Speed:   return Loc.T("shrine.speed");
            default:                  return type.ToString();
        }
    }

    // =========================================================
    // CICLO DE VIDA
    // =========================================================

    protected override void Reset()
    {
        base.Reset();
        StartAt = 60f;
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
        _progress.Clear();
        for (int i = 0; i < TeamCount; i++) _progress.Add(0);
        ServerDeactivate();
        _nextEta.Value = 0f;

        MercenariesGameMode.OnServerKill += HandleKill;
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        MercenariesGameMode.OnServerKill -= HandleKill;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        _nextEta.OnChange += (previous, next, asServer) => _nextEtaReceivedAt = Time.time;
        _nextEtaReceivedAt = Time.time;
    }

    // =========================================================
    // SERVIDOR
    // =========================================================

    private readonly ShrineCaptureRules _rules = new ShrineCaptureRules(TeamCount);
    private readonly bool[] _teamInside = new bool[TeamCount];

    private bool  _waiting;          // entre un santuario y el siguiente
    private float _nextTimer;        // cuenta atrás hasta el siguiente
    private float _secondTimer;      // junta los segundos dentro de la zona
    private float _activeTimer;      // cuánto lleva activo el de ahora
    private float _publishTimer;
    private int   _lastIndex = -1;
    private int   _lastType  = -1;
    private bool  _warnedNoShrines;

    // NPCs con el efecto puesto ahora, y los que ya recibieron su curación de entrada con este santuario.
    private readonly HashSet<AbilitySystemComponent> _empowered = new HashSet<AbilitySystemComponent>();
    private readonly HashSet<AbilitySystemComponent> _healedOnEntry = new HashSet<AbilitySystemComponent>();
    private readonly List<AbilitySystemComponent> _leaving = new List<AbilitySystemComponent>();
    private float _npcTimer;

    protected override void OnEventReset()
    {
        ServerDeactivate();
        _waiting   = false;
        _lastIndex = -1;
        _lastType  = -1;
        _nextEta.Value = 0f;
    }

    protected override void OnEventBegin() => ServerActivateNext();

    protected override void OnEventEnd()
    {
        ServerDeactivate();
        _waiting = false;
        _nextEta.Value = 0f;
    }

    protected override void OnEventTick(float deltaTime)
    {
        if (_waiting)
        {
            _nextTimer -= deltaTime;
            _publishTimer -= deltaTime;
            if (_publishTimer <= 0f)
            {
                _publishTimer = 0.5f;
                _nextEta.Value = Mathf.Max(0f, _nextTimer);
            }
            if (_nextTimer <= 0f) ServerActivateNext();
            return;
        }

        MatchShrine shrine = ActiveShrine;
        if (shrine == null) return;

        ServerRefreshInside(shrine);

        // Los NPCs de la zona, cuatro veces por segundo (alcanza: caminan despacio).
        _npcTimer -= deltaTime;
        if (_npcTimer <= 0f)
        {
            _npcTimer = 0.25f;
            ServerRefreshNpcs(shrine);
        }

        // Un punto por segundo para cada equipo con alguien dentro.
        _secondTimer += deltaTime;
        while (_secondTimer >= 1f)
        {
            _secondTimer -= 1f;
            for (int i = 0; i < TeamCount; i++)
            {
                if (!_teamInside[i]) continue;
                if (ServerAdd(i, PercentPerSecond)) return;   // capturado: ya no hay santuario
            }
        }

        _activeTimer += deltaTime;
        if (MaxActiveSeconds > 0f && _activeTimer >= MaxActiveSeconds)
        {
            Debug.Log("[Santuarios] Nadie lo capturó a tiempo: viene otro.");
            ServerWaitForNext();
        }
    }

    // Prende uno al azar (distinto del anterior), con un beneficio al azar (distinto del anterior).
    [Server]
    private void ServerActivateNext()
    {
        _waiting = false;
        _nextEta.Value = 0f;

        int count = MatchShrine.All.Count;
        if (count == 0)
        {
            if (!_warnedNoShrines)
                Debug.LogWarning("[Santuarios] El evento está pero el mapa no tiene ningún santuario (MatchShrine).");
            _warnedNoShrines = true;
            return;
        }

        int index = Random.Range(0, count);
        if (count > 1 && index == _lastIndex) index = (index + Random.Range(1, count)) % count;
        _lastIndex = index;

        MatchShrine shrine = MatchShrine.ByIndex(index);
        EShrineType type = PickType(shrine);
        _lastType = (int)type;

        _rules.ContestedCap = ContestedCap;
        _rules.Clear();
        while (_progress.Count < TeamCount) _progress.Add(0);
        for (int i = 0; i < TeamCount; i++) if (_progress[i] != 0) _progress[i] = 0;
        for (int i = 0; i < TeamCount; i++) _teamInside[i] = false;
        _inside.Value = 0;
        _secondTimer  = 0f;
        _activeTimer  = 0f;

        _type.Value       = (int)type;
        _activeIndex.Value = index;

        MercenariesGameMode gm = MercenariesGameMode.Instance;
        if (gm != null) gm.Announce(EMatchAnnouncement.ShrineActivated, 0, (int)type);
        Debug.Log($"[Santuarios] Activo: '{shrine.name}' en {shrine.transform.position}, tipo {type}.");
    }

    private EShrineType PickType(MatchShrine shrine)
    {
        if (shrine.Type != MatchShrine.EChoice.Random) return (EShrineType)((int)shrine.Type - 1);

        int count = Blessings != null ? Blessings.Length : 0;
        if (count == 0) return EShrineType.Damage;

        int pick = Random.Range(0, count);
        if (count > 1 && Blessings[pick] != null && (int)Blessings[pick].Type == _lastType)
            pick = (pick + Random.Range(1, count)) % count;
        return Blessings[pick] != null ? Blessings[pick].Type : EShrineType.Damage;
    }

    // Quién tiene a alguien vivo dentro de la zona (bots incluidos).
    [Server]
    private void ServerRefreshInside(MatchShrine shrine)
    {
        for (int i = 0; i < TeamCount; i++) _teamInside[i] = false;

        MercenariesGameMode gm = MercenariesGameMode.Instance;
        if (gm != null)
        {
            foreach (PlayerController pc in gm.Players)
            {
                if (pc == null) continue;
                AbilitySystemComponent asc = pc.GetComponent<AbilitySystemComponent>();
                if (asc == null || !MercenariesGameMode.IsValidTeam(asc.TeamID)) continue;
                if (asc.HasTag(EGameplayTag.State_Dead)) continue;
                if (shrine.Contains(pc.transform.position)) _teamInside[asc.TeamID - 1] = true;
            }
        }

        int mask = 0;
        for (int i = 0; i < TeamCount; i++) if (_teamInside[i]) mask |= 1 << (i + 1);
        if (_inside.Value != mask) _inside.Value = mask;
    }

    // Una baja: si el que murió estaba dentro de la zona, suma para el equipo del que lo mató.
    [Server]
    private void HandleKill(AbilitySystemComponent killer, AbilitySystemComponent victim)
    {
        MatchShrine shrine = ActiveShrine;
        if (shrine == null || _waiting || killer == null || victim == null) return;

        int team = killer.TeamID;
        if (!MercenariesGameMode.IsValidTeam(team)) return;          // un NPC que mata no suma
        if (!shrine.Contains(victim.transform.position)) return;

        // El que murió ya está muerto: si era el último rival dentro, la zona queda libre.
        ServerRefreshInside(shrine);

        bool wasPlayer = victim.GetComponent<PlayerController>() != null;
        ServerAdd(team - 1, wasPlayer ? PercentPerPlayerKill : PercentPerNpcKill);
    }

    // Suma, sincroniza y captura si llegó a 100. Devuelve true si capturó.
    [Server]
    private bool ServerAdd(int teamIndex, int amount)
    {
        bool captured = _rules.Add(teamIndex, amount, _teamInside);
        if (_progress.Count == TeamCount && _progress[teamIndex] != _rules.Get(teamIndex))
            _progress[teamIndex] = _rules.Get(teamIndex);

        if (captured) ServerCapture(teamIndex + 1);
        return captured;
    }

    [Server]
    private void ServerCapture(int team)
    {
        EShrineType type = ActiveType;
        ShrineBlessing blessing = GetBlessing(type);
        MercenariesGameMode gm = MercenariesGameMode.Instance;

        int blessed = 0;
        if (gm != null && blessing != null && blessing.Effect != null)
        {
            foreach (PlayerController pc in gm.Players)
            {
                if (pc == null) continue;
                AbilitySystemComponent asc = pc.GetComponent<AbilitySystemComponent>();
                if (asc == null || asc.TeamID != team || asc.HasTag(EGameplayTag.State_Dead)) continue;
                asc.ApplyGameplayEffect(blessing.Effect, null, BlessingSeconds);
                blessed++;
            }
        }
        else if (blessing == null || blessing.Effect == null)
        {
            Debug.LogWarning($"[Santuarios] El tipo {type} no tiene efecto cargado en \"Beneficios\": se capturó sin beneficio.");
        }

        if (gm != null)
        {
            gm.ServerAddTeamXp(team, CaptureXp);
            gm.Announce(EMatchAnnouncement.ShrineCaptured, team, (int)type);
        }

        Debug.Log($"[Santuarios] {MercenariesGameMode.TeamName(team)} capturó el santuario de {type}: " +
                  $"beneficio para {blessed} jugadores y {CaptureXp} de experiencia.");
        ServerWaitForNext();
    }

    // Apaga el de ahora y empieza la cuenta atrás del siguiente.
    [Server]
    private void ServerWaitForNext()
    {
        ServerDeactivate();
        _waiting      = true;
        _nextTimer    = Mathf.Max(1f, SecondsBetween);
        _publishTimer = 0f;
        _nextEta.Value = _nextTimer;
    }

    [Server]
    private void ServerDeactivate()
    {
        _activeIndex.Value = -1;
        _inside.Value = 0;
        _rules.Clear();
        for (int i = 0; i < TeamCount; i++) _teamInside[i] = false;
        for (int i = 0; i < _progress.Count; i++) if (_progress[i] != 0) _progress[i] = 0;
        ServerReleaseAllNpcs();
    }

    // =========================================================
    // LOS NPCs DE LA ZONA
    // =========================================================

    // Pone el efecto a los NPCs vivos que están dentro y se lo quita a los que salieron o murieron.
    [Server]
    private void ServerRefreshNpcs(MatchShrine shrine)
    {
        if (NpcEmpowerEffect == null) return;

        // Primero los que ya no cuentan: destruidos, muertos o fuera de la zona.
        _leaving.Clear();
        foreach (AbilitySystemComponent asc in _empowered)
            if (asc == null || asc.HasTag(EGameplayTag.State_Dead) || !shrine.Contains(asc.transform.position))
                _leaving.Add(asc);
        foreach (AbilitySystemComponent asc in _leaving)
        {
            if (asc != null) asc.RemoveEffectsByDefinition(NpcEmpowerEffect);
            _empowered.Remove(asc);
        }

        // Después los que están dentro y todavía no lo tienen.
        foreach (MercEnemyAI enemy in MercEnemyAI.ServerAll)
        {
            if (enemy == null || enemy.IsDead) continue;
            if (EmpowerOnlyShrineEnemies && !shrine.OwnsEnemy(enemy)) continue;

            AbilitySystemComponent asc = enemy.Asc;
            if (asc == null || _empowered.Contains(asc) || asc.HasTag(EGameplayTag.State_Dead)) continue;
            if (!shrine.Contains(enemy.transform.position)) continue;

            float maxBefore = asc.GetAttributeValue(EAttributeType.MaxHealth);
            asc.ApplyGameplayEffect(NpcEmpowerEffect);
            _empowered.Add(asc);

            // La vida máxima que ganó (sea un % o un número fijo) también se le cura, pero una vez por
            // santuario: así un fantasma lleno entra lleno, y entrar y salir no lo va curando.
            float gained = asc.GetAttributeValue(EAttributeType.MaxHealth) - maxBefore;
            if (gained > 0f && _healedOnEntry.Add(asc))
                asc.SetCurrentAttributeValue(EAttributeType.Health, asc.GetAttributeValue(EAttributeType.Health) + gained);
        }
    }

    // Se apagó el santuario: todos vuelven a la normalidad.
    [Server]
    private void ServerReleaseAllNpcs()
    {
        if (NpcEmpowerEffect != null)
            foreach (AbilitySystemComponent asc in _empowered)
                if (asc != null) asc.RemoveEffectsByDefinition(NpcEmpowerEffect);

        _empowered.Clear();
        _healedOnEntry.Clear();
    }
}
