using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

// Fases de la partida. Warmup = todos encerrados en su base esperando; Playing =
// partida en curso; Ended = ya hay ganador (o se acabó el tiempo).
public enum EMatchState { Warmup = 0, Playing = 1, Ended = 2 }

// Avisos que el servidor manda a TODAS las pantallas. Viajan como enum + números
// (nunca como texto): el texto lo arma cada cliente en UI_MatchAnnouncer, así se
// puede cambiar la redacción sin tocar la red.
public enum EMatchAnnouncement
{
    MatchStarted,        // arrancó la partida
    ObjectiveSpawned,    // apareció el Objetivo en el centro
    ObjectiveTaken,      // un equipo lo levantó       (Team = quién)
    ObjectiveDropped,    // se cayó al piso            (Team = quién lo tenía)
    ObjectiveDelivered,  // entregado en una base      (Team = quién, Extra = puntos que lleva)
    ObjectiveReturning,  // el próximo Objetivo llega en Extra segundos
    TeamWiped,           // aniquilaron a un equipo    (Team = el equipo caído)
    TeamLevelUp,         // un equipo subió de nivel   (Team, Extra = nivel nuevo)
    MatchEnded,          // fin de partida             (Team = ganador, 0 = empate)
}

// ============================================================
// MercenariesGameMode
//
// El cerebro del modo principal "Mercenarios" (3c3c3 PvEvP). Vive en la ESCENA,
// sobre el mismo GameObject que NetworkGameManager (comparte su NetworkObject), y
// TODA su lógica corre en el servidor: acá se decide cuándo aparece el Objetivo,
// quién puntúa, cuánta experiencia gana cada equipo y cuándo se acaba la partida.
// Los clientes solo LEEN el estado sincronizado para dibujarlo (ver UI_MercenariesHUD).
//
// Lo que administra:
//   · FASES: Preparación (todos en su base) → Partida → Fin (+ reinicio automático).
//   · OBJETIVO: aparece en el centro pasado un tiempo, y vuelve a aparecer unos
//     segundos después de cada entrega (ver MercObjective).
//   · PUNTAJE: 2 entregas ganan la partida.
//   · PROGRESIÓN COMPARTIDA: cada equipo tiene UNA sola bolsa de experiencia. Todo
//     lo que gana cualquiera de sus tres jugadores va ahí, y el NIVEL sale de esa
//     bolsa — no del progreso individual. Ver la sección EXPERIENCIA COMPARTIDA.
//   · WIPES: avisa a todas las pantallas cuando un equipo queda entero en el piso.
//
// POR QUÉ LA EXPERIENCIA ES DEL EQUIPO Y NO DEL JUGADOR: el jugador puede cambiar de
// clase en su base cuando quiera, y con progreso individual eso significaba volver a
// nivel 1 (EquipCharacterClass con resetProgress reinicia los atributos). Con la
// bolsa compartida, el nivel es una propiedad del EQUIPO: al cambiar de clase el
// servidor le vuelve a subir el nivel al toque, así que cambiar no cuesta nada.
// ============================================================
public class MercenariesGameMode : NetworkBehaviour
{
    public const int TeamCount = 3;

    // Acceso global (hay uno solo por escena). Lo usan MercObjective, MercTeamBase,
    // los enemigos y la UI.
    public static MercenariesGameMode Instance { get; private set; }

    // Aviso del servidor recibido en ESTE cliente. Estático porque la UI puede existir
    // antes de que el objeto de red esté listo (el HUD vive en la escena).
    public static event Action<EMatchAnnouncement, int, int> OnAnnouncement;

    // =========================================================
    // CONFIGURACIÓN
    // =========================================================

    [Header("Ritmo de la partida")]
    [Tooltip("Segundos de preparación antes de que arranque. Los equipos esperan en su base.")]
    public float WarmupSeconds = 30f;

    [Tooltip("Duración máxima de la partida. Si nadie llegó a los puntos necesarios, gana quien vaya arriba.")]
    public float MatchDurationSeconds = 900f; // 15 min

    [Tooltip("Entregas necesarias para ganar.")]
    public int PointsToWin = 2;

    [Tooltip("Segundos tras el arranque hasta que aparece el PRIMER Objetivo.")]
    public float FirstObjectiveDelay = 60f;

    [Tooltip("Segundos entre una entrega y la aparición del siguiente Objetivo.")]
    public float ObjectiveRespawnDelay = 30f;

    [Tooltip("Segundos que tarda un jugador en reaparecer en su base tras morir.")]
    public float RespawnSeconds = 5f;

    [Tooltip("Al terminar, reiniciar la partida sola (cómodo para demos y pruebas).")]
    public bool AutoRestart = true;
    public float RestartDelay = 15f;

    [Header("Objetivo")]
    [Tooltip("Prefab del Objetivo (la Bolsa de oro). Necesita NetworkObject + MercObjective.")]
    public GameObject ObjectivePrefab;

    [Tooltip("Dónde aparece el Objetivo. Normalmente el centro del escenario.")]
    public Transform ObjectiveSpawnPoint;

    [Header("Equipos")]
    [Tooltip("Las tres bases. Cada una trae su sala segura, su punto de entrega y sus puntos de aparición.")]
    public MercTeamBase[] TeamBases = new MercTeamBase[TeamCount];

    [Tooltip("Color de cada equipo (1, 2, 3). Lo usan el HUD, los avisos y el marcador del Objetivo.")]
    public Color[] TeamColors =
    {
        new Color(0.90f, 0.25f, 0.25f), // 1 · rojo
        new Color(0.30f, 0.85f, 0.35f), // 2 · verde
        new Color(0.30f, 0.60f, 1.00f), // 3 · azul
    };

    [Header("Experiencia compartida (bolsa por equipo)")]
    [Tooltip("Nivel máximo. Tiene que coincidir con el MaxLevel del AbilitySystemComponent del jugador.")]
    public int MaxTeamLevel = 3;

    [Tooltip("Experiencia para pasar de nivel 1 a 2, de 2 a 3, etc. Un valor por salto.")]
    public float[] XpPerLevel = { 100f, 150f };

    [Tooltip("Experiencia que la bolsa del equipo gana por cada NPC derrotado.")]
    public float XpPerNpcKill = 15f;

    [Tooltip("Experiencia por derribar a un JUGADOR enemigo. Vale bastante más que un NPC a propósito.")]
    public float XpPerPlayerTakedown = 40f;

    [Tooltip("Minuto en el que un equipo que no hizo NADA llega igual al nivel máximo. Es la red de " +
             "seguridad para que nadie se quede atrás: la experiencia pasiva se calcula sola a partir " +
             "de esto. Matando NPCs y jugadores se llega bastante antes (alrededor del minuto 5).")]
    public float MinutesToGuaranteedMaxLevel = 10f;

    [Tooltip("Experiencia pasiva por segundo. En 0 (por defecto) se calcula sola con el minuto de " +
             "arriba; ponele un valor si querés fijarla a mano.")]
    public float PassiveXpPerSecondOverride = 0f;

    [Header("Reglas")]
    [Tooltip("El cambio de clase (tecla C) solo se permite dentro de la sala segura del propio equipo.")]
    public bool ClassChangeOnlyInSafeRoom = true;

    // =========================================================
    // ESTADO SINCRONIZADO
    // Los clientes LEEN esto; solo el servidor escribe.
    // =========================================================

    private readonly SyncVar<int> _netState  = new SyncVar<int>((int)EMatchState.Warmup);
    private readonly SyncVar<int> _netWinner = new SyncVar<int>(0);

    // Segundos que le quedan a la fase actual. Se manda cada MEDIO segundo y el
    // cliente lo descuenta solo entre paquetes (ver PhaseTimeRemaining): así el reloj
    // del HUD corre suave sin gastar red en un float por frame.
    private readonly SyncVar<float> _netPhaseTime = new SyncVar<float>(0f);
    private float _phaseTimeReceivedAt;

    // Segundos hasta el próximo Objetivo (0 = ya está en juego). Mismo criterio.
    private readonly SyncVar<float> _netObjectiveEta = new SyncVar<float>(0f);
    private float _objectiveEtaReceivedAt;

    // Un valor por equipo, SIEMPRE con TeamCount entradas (índice 0 = equipo 1).
    private readonly SyncList<int>   _netScores = new SyncList<int>();
    private readonly SyncList<int>   _netLevels = new SyncList<int>();
    private readonly SyncList<float> _netXpNorm = new SyncList<float>();

    // =========================================================
    // ESTADO SOLO-SERVIDOR
    // =========================================================

    private readonly float[] _teamXp    = new float[TeamCount];
    private readonly int[]   _teamLevel = new int[TeamCount];
    private readonly int[]   _teamScore = new int[TeamCount];
    private readonly bool[]  _teamWiped = new bool[TeamCount]; // para no repetir el aviso

    private float _phaseTimer;      // cuenta atrás de la fase actual
    private float _objectiveTimer;  // cuenta atrás hasta el próximo Objetivo
    private bool  _objectivePending;
    private float _slowTickTimer;

    private MercObjective _activeObjective;

    // Jugadores en partida (se rescanean solos, ver RefreshPlayers).
    private readonly List<PlayerController> _players = new List<PlayerController>();

    // =========================================================
    // LECTURA PÚBLICA (la usa la UI, en cualquier peer)
    // =========================================================

    public EMatchState State => (EMatchState)_netState.Value;
    public int WinnerTeam    => _netWinner.Value;

    // Reloj de la fase: el valor sincronizado menos lo que pasó desde que llegó.
    public float PhaseTimeRemaining =>
        Mathf.Max(0f, _netPhaseTime.Value - (Time.time - _phaseTimeReceivedAt));

    // Cuánto se lleva jugado desde que arrancó la partida (0 durante la preparación).
    // Lo usan los campamentos para decidir qué enemigos ya se desbloquearon.
    //
    // En el servidor sale del reloj propio y no del sincronizado: el SyncVar se publica
    // cada medio segundo y su corrección local depende de un callback que en un servidor
    // sin cliente nunca corre.
    public float MatchElapsedSeconds
    {
        get
        {
            if (State != EMatchState.Playing) return 0f;
            float remaining = IsServerInitialized ? _phaseTimer : PhaseTimeRemaining;
            return Mathf.Max(0f, MatchDurationSeconds - remaining);
        }
    }

    // Segundos hasta el próximo Objetivo. 0 = ya hay uno en juego (o todavía no aplica).
    public float ObjectiveEta =>
        Mathf.Max(0f, _netObjectiveEta.Value - (Time.time - _objectiveEtaReceivedAt));

    public int GetScore(int team) => IsValidTeam(team) && _netScores.Count == TeamCount ? _netScores[team - 1] : 0;
    public int GetLevel(int team) => IsValidTeam(team) && _netLevels.Count == TeamCount ? _netLevels[team - 1] : 1;
    public float GetXpNormalized(int team) => IsValidTeam(team) && _netXpNorm.Count == TeamCount ? _netXpNorm[team - 1] : 0f;

    public Color GetTeamColor(int team)
    {
        if (TeamColors == null || TeamColors.Length == 0) return Color.white;
        int i = Mathf.Clamp(team - 1, 0, TeamColors.Length - 1);
        return TeamColors[i];
    }

    // Nombre visible de un equipo. Un solo lugar para cambiarlo.
    public static string TeamName(int team) => $"EQUIPO {team}";

    public static bool IsValidTeam(int team) => team >= 1 && team <= TeamCount;

    // El Objetivo que está en juego ahora (null si no hay). Lo usa el marcador del HUD.
    public MercObjective ActiveObjective => _activeObjective != null ? _activeObjective : MercObjective.Instance;

    // Base de un equipo (null si no está configurada).
    public MercTeamBase GetBase(int team)
    {
        if (TeamBases == null) return null;
        foreach (var b in TeamBases)
            if (b != null && b.TeamID == team) return b;
        return null;
    }

    // Dónde aparece (y reaparece) un jugador de este equipo. Lo consulta
    // NetworkGameManager en vez de repartir puntos de spawn al azar.
    public Transform GetTeamSpawnPoint(int team)
    {
        MercTeamBase b = GetBase(team);
        return b != null ? b.GetSpawnPoint() : null;
    }

    // =========================================================
    // CICLO DE VIDA
    // =========================================================

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

        // Las listas tienen que existir con sus tres entradas ANTES de que un cliente
        // las lea: el HUD pregunta por el equipo 3 desde el primer frame.
        _netScores.Clear(); _netLevels.Clear(); _netXpNorm.Clear();
        for (int i = 0; i < TeamCount; i++)
        {
            _netScores.Add(0);
            _netLevels.Add(1);
            _netXpNorm.Add(0f);
            _teamLevel[i] = 1;
        }

        ServerValidateSetup();
        ServerBeginWarmup();
    }

    // Avisa apenas arranca el servidor si falta algo que va a hacer fallar la partida
    // en silencio veinte segundos después. Es barato y ahorra sesiones enteras de
    // "¿por qué no aparece el Objetivo?" — que casi siempre es una de estas tres cosas.
    [Server]
    private void ServerValidateSetup()
    {
        if (ObjectivePrefab == null)
            Debug.LogError("[Mercenarios] SIN PREFAB DEL OBJETIVO: el modo va a correr pero el Objetivo " +
                           "nunca va a aparecer. Asignalo en el MercenariesGameMode.");
        else if (ObjectivePrefab.GetComponent<NetworkObject>() == null)
            Debug.LogError("[Mercenarios] El prefab del Objetivo no tiene NetworkObject — el servidor no " +
                           "lo va a poder crear.");

        if (ObjectiveSpawnPoint == null)
            Debug.LogWarning("[Mercenarios] Sin ObjectiveSpawnPoint: el Objetivo va a aparecer en la posición " +
                             "del NetworkGameManager.");

        int basesOk = 0;
        if (TeamBases != null)
            foreach (var b in TeamBases) if (b != null) basesOk++;

        if (basesOk < TeamCount)
            Debug.LogError($"[Mercenarios] Solo hay {basesOk} de {TeamCount} bases asignadas. Sin base, un equipo " +
                           "no tiene dónde aparecer ni dónde entregar el Objetivo.");

        Debug.Log($"[Mercenarios] Listo. El primer Objetivo aparece a los " +
                  $"{WarmupSeconds + FirstObjectiveDelay:F0} s ({WarmupSeconds:F0} de preparación + " +
                  $"{FirstObjectiveDelay:F0} de espera).");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // Anotamos CUÁNDO llegó cada reloj para poder descontarlo localmente.
        _netPhaseTime.OnChange    += (p, n, s) => _phaseTimeReceivedAt    = Time.time;
        _netObjectiveEta.OnChange += (p, n, s) => _objectiveEtaReceivedAt = Time.time;
        _phaseTimeReceivedAt    = Time.time;
        _objectiveEtaReceivedAt = Time.time;
    }

    private void Update()
    {
        if (!IsServerInitialized) return;

        float dt = Time.deltaTime;

        // --- reloj de la fase ---
        _phaseTimer = Mathf.Max(0f, _phaseTimer - dt);

        // --- tareas lentas (2 veces por segundo alcanza y sobra) ---
        _slowTickTimer += dt;
        bool slowTick = _slowTickTimer >= 0.5f;
        if (slowTick) _slowTickTimer = 0f;

        switch (State)
        {
            case EMatchState.Warmup:
                if (slowTick) { PublishPhaseTime(); RefreshPlayers(); }
                if (_phaseTimer <= 0f) ServerStartMatch();
                break;

            case EMatchState.Playing:
                TickObjective(dt, slowTick);
                if (slowTick)
                {
                    PublishPhaseTime();
                    RefreshPlayers();
                    ServerTickSharedExperience();
                    ServerSyncPlayerLevels();
                    ServerCheckWipes();
                }
                if (_phaseTimer <= 0f) ServerEndMatch(ResolveLeader());
                break;

            case EMatchState.Ended:
                if (slowTick) PublishPhaseTime();
                if (AutoRestart && _phaseTimer <= 0f) ServerRestartMatch();
                break;
        }
    }

    private void PublishPhaseTime() => _netPhaseTime.Value = _phaseTimer;

    // =========================================================
    // FASES
    // =========================================================

    [Server]
    private void ServerBeginWarmup()
    {
        _netState.Value = (int)EMatchState.Warmup;
        _phaseTimer     = Mathf.Max(1f, WarmupSeconds);
        PublishPhaseTime();

        _objectivePending      = false;
        _netObjectiveEta.Value = 0f;

        Debug.Log($"[Mercenarios] Preparación: {WarmupSeconds:F0}s.");
    }

    [Server]
    private void ServerStartMatch()
    {
        _netState.Value = (int)EMatchState.Playing;
        _phaseTimer     = Mathf.Max(30f, MatchDurationSeconds);
        PublishPhaseTime();

        ScheduleObjective(FirstObjectiveDelay);
        Announce(EMatchAnnouncement.MatchStarted, 0, 0);

        Debug.Log("[Mercenarios] ¡Partida iniciada!");
    }

    [Server]
    private void ServerEndMatch(int winnerTeam)
    {
        _netState.Value  = (int)EMatchState.Ended;
        _netWinner.Value = winnerTeam;
        _phaseTimer      = Mathf.Max(3f, RestartDelay);
        PublishPhaseTime();

        DespawnObjective();
        _objectivePending      = false;
        _netObjectiveEta.Value = 0f;

        Announce(EMatchAnnouncement.MatchEnded, winnerTeam, 0);
        Debug.Log($"[Mercenarios] Fin de partida. Ganador: {(winnerTeam > 0 ? TeamName(winnerTeam) : "empate")}.");
    }

    // Deja todo como al principio: puntajes, bolsas de experiencia y niveles. Los
    // jugadores conservan su clase, pero se los reequipa para que vuelvan a nivel 1
    // (el nivel no "baja" solo: reequipar es lo único que recalcula los stats base).
    [Server]
    public void ServerRestartMatch()
    {
        for (int i = 0; i < TeamCount; i++)
        {
            _teamScore[i] = 0;
            _teamXp[i]    = 0f;
            _teamLevel[i] = 1;
            _teamWiped[i] = false;
            _netScores[i] = 0;
            _netLevels[i] = 1;
            _netXpNorm[i] = 0f;
        }

        RefreshPlayers();
        foreach (PlayerController pc in _players)
        {
            if (pc == null) continue;
            AbilitySystemComponent asc = pc.GetComponent<AbilitySystemComponent>();
            if (asc == null) continue;

            if (pc.CurrentClassDef != null) pc.EquipCharacterClass(pc.CurrentClassDef, resetProgress: true);
            asc.Revive();

            Transform sp = GetTeamSpawnPoint(asc.TeamID);
            NetworkAbilitySystemComponent netASC = pc.GetComponent<NetworkAbilitySystemComponent>();
            if (sp != null && netASC != null) netASC.ServerTeleportOwnerTo(sp.position, sp.forward);
        }

        ServerBeginWarmup();
    }

    // Quién va ganando (0 si hay empate). Se usa si se acaba el tiempo.
    private int ResolveLeader()
    {
        int best = 0, bestScore = -1; bool tie = false;
        for (int i = 0; i < TeamCount; i++)
        {
            if (_teamScore[i] > bestScore) { bestScore = _teamScore[i]; best = i + 1; tie = false; }
            else if (_teamScore[i] == bestScore) tie = true;
        }
        return tie ? 0 : best;
    }

    // =========================================================
    // OBJETIVO
    // =========================================================

    [Server]
    private void ScheduleObjective(float delay)
    {
        _objectivePending      = true;
        _objectiveTimer        = Mathf.Max(0f, delay);
        _netObjectiveEta.Value = _objectiveTimer;

        if (delay > 1f) Announce(EMatchAnnouncement.ObjectiveReturning, 0, Mathf.RoundToInt(delay));
    }

    [Server]
    private void TickObjective(float dt, bool slowTick)
    {
        if (!_objectivePending) return;

        _objectiveTimer -= dt;
        if (slowTick) _netObjectiveEta.Value = Mathf.Max(0f, _objectiveTimer);

        if (_objectiveTimer > 0f) return;

        _objectivePending      = false;
        _netObjectiveEta.Value = 0f;
        SpawnObjective();
    }

    [Server]
    private void SpawnObjective()
    {
        if (ObjectivePrefab == null)
        {
            Debug.LogError("[Mercenarios] Sin ObjectivePrefab asignado — el Objetivo nunca va a aparecer.");
            return;
        }

        DespawnObjective();

        Vector3 pos = ObjectiveSpawnPoint != null ? ObjectiveSpawnPoint.position : transform.position;
        GameObject go = Instantiate(ObjectivePrefab, pos, Quaternion.identity);
        ServerManager.Spawn(go);

        _activeObjective = go.GetComponent<MercObjective>();
        if (_activeObjective != null) _activeObjective.ServerInitialize(this, pos);
        else Debug.LogError("[Mercenarios] El prefab del Objetivo no tiene el componente MercObjective — " +
                            "se creó una caja que no se puede levantar ni entregar.");

        Announce(EMatchAnnouncement.ObjectiveSpawned, 0, 0);
        Debug.Log($"[Mercenarios] Objetivo en juego, en {pos}.");
    }

    // Atajo para probar sin esperar el reloj: con el juego corriendo, clic derecho en
    // el componente → esta opción. Solo tiene efecto en el servidor (o en el host).
    [ContextMenu("DEBUG · Hacer aparecer el Objetivo ya")]
    private void DebugSpawnObjectiveNow()
    {
        if (!Application.isPlaying || !IsServerInitialized)
        {
            Debug.LogWarning("[Mercenarios] Esto solo funciona con la partida corriendo y siendo servidor/host.");
            return;
        }

        // Si todavía estamos en preparación, arrancamos la partida: un Objetivo con la
        // partida sin empezar no lo podría puntuar nadie.
        if (State != EMatchState.Playing) ServerStartMatch();

        // Y se reprograma para "ya". Va por el camino normal (ScheduleObjective →
        // TickObjective → SpawnObjective) a propósito: si lo spawneáramos a mano acá,
        // quedaría además el que ya estaba agendado y se pisarían entre ellos.
        ScheduleObjective(0.1f);
    }

    [Server]
    private void DespawnObjective()
    {
        if (_activeObjective == null) return;

        _activeObjective.ServerReleaseCarrier();
        if (_activeObjective.IsSpawned) ServerManager.Despawn(_activeObjective.gameObject);
        _activeObjective = null;
    }

    // La llama MercObjective cuando alguien lo levanta.
    [Server]
    public void ServerNotifyObjectiveTaken(int team)
        => Announce(EMatchAnnouncement.ObjectiveTaken, team, 0);

    // La llama MercObjective cuando se cae al piso (soltado o por muerte del portador).
    [Server]
    public void ServerNotifyObjectiveDropped(int team)
        => Announce(EMatchAnnouncement.ObjectiveDropped, team, 0);

    // La llama MercObjective al entregarlo en la base de 'team'.
    [Server]
    public void ServerScoreObjective(int team)
    {
        if (!IsValidTeam(team) || State != EMatchState.Playing) return;

        int i = team - 1;
        _teamScore[i]++;
        _netScores[i] = _teamScore[i];

        Announce(EMatchAnnouncement.ObjectiveDelivered, team, _teamScore[i]);

        DespawnObjective();

        if (_teamScore[i] >= PointsToWin) { ServerEndMatch(team); return; }

        ScheduleObjective(ObjectiveRespawnDelay);
    }

    // =========================================================
    // EXPERIENCIA COMPARTIDA
    //
    // Cada equipo tiene UNA bolsa. Todo lo que gana cualquiera de sus jugadores cae
    // ahí, y el nivel de los TRES sale de esa bolsa. Como el nivel es del equipo y no
    // de la persona, cambiar de clase en la base no cuesta progreso: el servidor le
    // vuelve a subir el nivel al que acaba de cambiar (ver ServerSyncPlayerLevels).
    // =========================================================

    // Experiencia total para llegar al nivel máximo (suma de todos los saltos).
    public float TotalXpForMaxLevel
    {
        get
        {
            float total = 0f;
            if (XpPerLevel != null)
                for (int i = 0; i < XpPerLevel.Length && i < MaxTeamLevel - 1; i++) total += XpPerLevel[i];
            return Mathf.Max(1f, total);
        }
    }

    // Experiencia pasiva por segundo. Por defecto sale sola de
    // MinutesToGuaranteedMaxLevel: es el piso que garantiza que NADIE se quede en
    // nivel 1 aunque no pelee.
    public float PassiveXpPerSecond
    {
        get
        {
            if (PassiveXpPerSecondOverride > 0f) return PassiveXpPerSecondOverride;
            float seconds = Mathf.Max(30f, MinutesToGuaranteedMaxLevel * 60f);
            return TotalXpForMaxLevel / seconds;
        }
    }

    [Server]
    private void ServerTickSharedExperience()
    {
        // Este tick corre 2 veces por segundo (ver Update).
        float amount = PassiveXpPerSecond * 0.5f;
        for (int team = 1; team <= TeamCount; team++) ServerAddTeamXp(team, amount);
    }

    // Suma experiencia a la bolsa de un equipo y sube su nivel si corresponde.
    [Server]
    public void ServerAddTeamXp(int team, float amount, bool announce = true)
    {
        if (!IsValidTeam(team) || amount <= 0f) return;
        if (State != EMatchState.Playing) return;

        int i = team - 1;
        if (_teamLevel[i] >= MaxTeamLevel) return;

        _teamXp[i] += amount;

        int newLevel = LevelFromXp(_teamXp[i]);
        if (newLevel > _teamLevel[i])
        {
            _teamLevel[i] = newLevel;
            _netLevels[i] = newLevel;
            ServerApplyTeamLevel(team);
            if (announce) Announce(EMatchAnnouncement.TeamLevelUp, team, newLevel);
        }

        _netXpNorm[i] = XpProgressWithinLevel(_teamXp[i], _teamLevel[i]);
    }

    // Nivel que corresponde a una cantidad de experiencia acumulada.
    private int LevelFromXp(float xp)
    {
        int level = 1;
        float remaining = xp;
        if (XpPerLevel != null)
        {
            for (int i = 0; i < XpPerLevel.Length && level < MaxTeamLevel; i++)
            {
                float need = Mathf.Max(1f, XpPerLevel[i]);
                if (remaining < need) break;
                remaining -= need;
                level++;
            }
        }
        return Mathf.Min(level, MaxTeamLevel);
    }

    // Progreso 0..1 DENTRO del nivel actual (para la barrita del HUD).
    private float XpProgressWithinLevel(float xp, int level)
    {
        if (level >= MaxTeamLevel) return 1f;

        float consumed = 0f;
        for (int i = 0; i < level - 1 && XpPerLevel != null && i < XpPerLevel.Length; i++)
            consumed += Mathf.Max(1f, XpPerLevel[i]);

        float need = (XpPerLevel != null && level - 1 < XpPerLevel.Length)
            ? Mathf.Max(1f, XpPerLevel[level - 1]) : 100f;

        return Mathf.Clamp01((xp - consumed) / need);
    }

    // Una baja: el equipo del que remató se lleva la experiencia. La llama
    // NetworkAbilitySystemComponent.AwardKillExperience, que es el único punto del
    // core que sabe quién dio el golpe final (LastAttacker).
    [Server]
    public void ServerAwardKill(AbilitySystemComponent killer, AbilitySystemComponent victim)
    {
        if (killer == null || victim == null) return;

        bool victimIsPlayer = victim.GetComponent<PlayerController>() != null;
        float amount = victimIsPlayer ? XpPerPlayerTakedown : XpPerNpcKill;

        // Un NPC puede valer más que el resto (un jefe no da lo mismo que un fantasma):
        // si trae su propia recompensa, esa manda sobre el valor general del modo.
        if (!victimIsPlayer)
        {
            MercEnemyAI enemy = victim.GetComponent<MercEnemyAI>();
            if (enemy != null && enemy.ExperienceReward > 0f) amount = enemy.ExperienceReward;
        }

        ServerAddTeamXp(killer.TeamID, amount);
    }

    // Le pone a cada jugador del equipo el nivel de la bolsa. Es idempotente: si ya
    // lo tiene no hace nada, así que se puede llamar todo lo seguido que haga falta.
    [Server]
    private void ServerApplyTeamLevel(int team)
    {
        int level = _teamLevel[Mathf.Clamp(team - 1, 0, TeamCount - 1)];

        foreach (PlayerController pc in _players)
        {
            if (pc == null) continue;
            AbilitySystemComponent asc = pc.GetComponent<AbilitySystemComponent>();
            if (asc == null || asc.TeamID != team) continue;
            asc.SetLevelTo(level);
        }
    }

    // Repasa a TODOS los jugadores y les deja el nivel de su equipo. Corre 2 veces por
    // segundo, y es lo que hace que cambiar de clase (que reinicia el personaje a nivel
    // 1) no cueste nada: al instante siguiente vuelve al nivel del equipo.
    //
    // De paso publica la bolsa del equipo en los atributos Exp/MaxExp del jugador, así
    // la barra de experiencia del HUD personal muestra el progreso COMPARTIDO.
    [Server]
    private void ServerSyncPlayerLevels()
    {
        foreach (PlayerController pc in _players)
        {
            if (pc == null) continue;
            AbilitySystemComponent asc = pc.GetComponent<AbilitySystemComponent>();
            if (asc == null || !IsValidTeam(asc.TeamID)) continue;

            int i = asc.TeamID - 1;
            asc.SetLevelTo(_teamLevel[i]);

            float need  = (XpPerLevel != null && _teamLevel[i] - 1 < XpPerLevel.Length)
                ? Mathf.Max(1f, XpPerLevel[_teamLevel[i] - 1]) : 100f;
            float shown = _netXpNorm[i] * need;

            if (!Mathf.Approximately(asc.GetAttributeValue(EAttributeType.MaxExp), need))
                asc.SetCurrentAttributeValue(EAttributeType.MaxExp, need);
            if (Mathf.Abs(asc.GetAttributeValue(EAttributeType.Exp) - shown) > 0.5f)
                asc.SetCurrentAttributeValue(EAttributeType.Exp, shown);
        }
    }

    // =========================================================
    // JUGADORES Y WIPES
    // =========================================================

    // Rescanea los jugadores de la escena. Se hace por barrido y no con altas/bajas
    // explícitas a propósito: los personajes los crea NetworkGameManager y los destruye
    // la desconexión, y un barrido de 9 objetos dos veces por segundo es más barato que
    // mantener suscripciones que se pueden desincronizar.
    [Server]
    private void RefreshPlayers()
    {
        _players.Clear();
        _players.AddRange(FindObjectsByType<PlayerController>(FindObjectsSortMode.None));
    }

    // Cuántos jugadores de un equipo están vivos ahora mismo.
    public int CountAlive(int team)
    {
        int alive = 0;
        foreach (PlayerController pc in _players)
        {
            if (pc == null) continue;
            AbilitySystemComponent asc = pc.GetComponent<AbilitySystemComponent>();
            if (asc == null || asc.TeamID != team) continue;
            if (!asc.HasTag(EGameplayTag.State_Dead)) alive++;
        }
        return alive;
    }

    // Cuántos jugadores tiene un equipo (vivos o no).
    public int CountMembers(int team)
    {
        int n = 0;
        foreach (PlayerController pc in _players)
        {
            if (pc == null) continue;
            AbilitySystemComponent asc = pc.GetComponent<AbilitySystemComponent>();
            if (asc != null && asc.TeamID == team) n++;
        }
        return n;
    }

    // Avisa una sola vez por wipe: se rearma cuando alguien de ese equipo revive.
    [Server]
    private void ServerCheckWipes()
    {
        for (int team = 1; team <= TeamCount; team++)
        {
            int i = team - 1;
            if (CountMembers(team) == 0) { _teamWiped[i] = false; continue; }

            bool wiped = CountAlive(team) == 0;
            if (wiped && !_teamWiped[i]) Announce(EMatchAnnouncement.TeamWiped, team, 0);
            _teamWiped[i] = wiped;
        }
    }

    // =========================================================
    // AVISOS A TODAS LAS PANTALLAS
    // =========================================================

    [Server]
    public void Announce(EMatchAnnouncement type, int team, int extra)
        => ObserversAnnounce(type, team, extra);

    [ObserversRpc]
    private void ObserversAnnounce(EMatchAnnouncement type, int team, int extra)
        => OnAnnouncement?.Invoke(type, team, extra);
}
