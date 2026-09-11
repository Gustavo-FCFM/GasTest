using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;

// Una fila de la sala de espera: quién es, con qué equipo y clase entra, y si ya
// dijo que está listo. Viaja entera en la SyncList, así que solo lleva tipos que
// FishNet serializa solo (int / string / bool).
//
// ClassIndex es la posición dentro de UI_LobbyMenu.SelectableClasses, no dentro de
// AllClasses del PlayerController: acá todavía NO hay personaje del cual sacar esa
// lista. Vale igual en todos los peers porque el array es el mismo asset de la misma
// escena en el mismo build — el mismo razonamiento por el que AllClasses funciona.
public struct LobbyEntry
{
    public int    ClientId;
    public string PlayerName;
    public int    Team;        // 1..3 · 0 = todavía sin elegir
    public int    ClassIndex;  // -1 = todavía sin elegir
    public bool   Ready;
    public bool   Spectator;   // mira la partida; no ocupa lugar ni frena el arranque
}

// Por qué el servidor rechazó un pedido. Viaja como enum y el texto lo arma el
// cliente, igual que los avisos de MercenariesGameMode: cambiar la redacción o
// traducir el juego no toca la red.
public enum ELobbyRejection
{
    None,
    NameTaken,   // ya hay alguien con ese nombre
    TeamFull,    // el equipo llegó a MaxPlayersPerTeam
}

// ============================================================
// LobbyManager
//
// La sala de espera COMPARTIDA: quién está conectado, en qué equipo, con qué clase y
// si ya está listo. Vive en la escena sobre el mismo GameObject que
// NetworkGameManager (comparte su NetworkObject), igual que MercenariesGameMode.
//
// POR QUÉ HACE FALTA: hasta ahora el menú de entrada era puramente LOCAL. Cada uno
// elegía nombre, equipo y clase a ciegas y mandaba un SpawnRequestBroadcast; nadie
// veía a los demás. Con nueve personas eso significa nombres repetidos, equipos de
// cinco contra uno y gente que entra sin haber elegido clase — justo lo que hace
// imposible sacarle algo a un playtest.
//
// EL SERVIDOR MANDA, como en todo el proyecto: los clientes PIDEN (ServerRpc) y solo
// dibujan lo que vuelve por la SyncList. Nadie se auto-asigna un equipo lleno ni se
// declara listo por su cuenta.
//
// POR QUÉ ServerRpc Y NO BROADCAST: SpawnRequestBroadcast tiene que ser broadcast
// porque en ese momento el cliente todavía no tiene NetworkObject propio. Este
// componente, en cambio, es un objeto de ESCENA: existe e inicializa en todos los
// peers apenas se conectan, así que un ServerRpc con RequireOwnership = false llega
// perfectamente y encima trae la conexión de quien lo mandó sin que haya que confiar
// en un id que venga dentro del mensaje.
//
// LOS ESPECTADORES viven acá y no en un sistema aparte: son una fila más con
// Spectator = true. No cuentan para el cupo de los equipos ni frenan el arranque.
// ============================================================
public class LobbyManager : NetworkBehaviour
{
    public const int TeamCount = 3;

    // Uno solo por escena. Lo usan el menú de entrada, el panel de la sala y el modo
    // de juego para consultar si ya se puede empezar.
    public static LobbyManager Instance { get; private set; }

    [Header("Cupos")]
    [Tooltip("Cuántos jugadores entran por equipo. 0 = sin límite (útil para probar de a dos).")]
    public int MaxPlayersPerTeam = 3;

    [Header("Arranque")]
    [Tooltip("Si la partida espera a que TODOS los jugadores estén listos antes de arrancar la " +
             "preparación. Apagado = arranca por reloj como antes, sin importar quién falte.")]
    public bool RequireAllReady = true;

    [Tooltip("Mínimo de jugadores (sin contar espectadores) para que el gate de 'todos listos' " +
             "pueda darse por cumplido. En 1 alcanza con vos para probar solo.")]
    public int MinPlayersToStart = 1;

    // El HOST apreto Start. Es lo que destraba la preparacion: antes arrancaba sola en
    // cuanto todos estaban listos, y eso le sacaba al host la decision de esperar a
    // alguien que todavia no se conecto.
    private readonly SyncVar<bool> _netStarted = new SyncVar<bool>();
    public bool MatchStarted => _netStarted.Value;

    // Si ESTE proceso es el host. La UI lo usa para mostrar el boton de Start solo a
    // quien puede apretarlo.
    public static bool LocalIsHost => InstanceFinder.IsHostStarted;
    // La sala entera. Es lo único que viaja: el panel de la sala se dibuja leyendo
    // esto, sin un solo RPC extra.
    private readonly SyncList<LobbyEntry> _entries = new SyncList<LobbyEntry>();

    // Avisa a la UI local que la lista cambió, para no tener que redibujar cada frame.
    public static event System.Action OnLobbyChanged;

    // Rechazo recibido por ESTE cliente (nombre repetido, equipo lleno). Lo escucha el
    // menú de entrada para mostrar el aviso.
    public static event System.Action<ELobbyRejection> OnRejected;

    // =========================================================
    // CICLO DE VIDA
    // =========================================================

    // Estado de red en variables PROPIAS, no en las propiedades de FishNet.
    //
    // IsSpawned, IsClientInitialized y LocalConnection TIRAN NullReference si el
    // NetworkObject todavia no asocio este behaviour — pasa mientras la escena carga, y
    // tambien si la lista NetworkBehaviours del NetworkObject quedo sin poblar. La UI
    // consulta la sala CADA FRAME, asi que no puede apoyarse en propiedades que
    // revientan: se anota el estado a mano en OnStartClient/OnStopClient, que son los
    // unicos momentos en los que leerlas es seguro.
    private bool _netReady;
    private int  _localClientId = -1;

    // Si ya se puede hablar con la sala. La UI la consulta cada frame, por eso NO puede
    // usar IsSpawned: esa propiedad TIRA EXCEPCION antes de que el objeto se inicialice,
    // y un chequeo de null no la salva.
    public bool IsLobbyReady => _netReady;

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        // Un servidor que arranca DE NUEVO en la misma sesión (Desconectar → Iniciar Host)
        // hereda el estado del anterior: MatchStarted en true y las filas de gente que ya
        // no está. Con eso la partida "arrancaba" sola, sin sala, y nadie spawneaba. La
        // sala nace vacía cada vez que levanta el servidor.
        _netStarted.Value = false;
        _entries.Clear();

        // Al desconectarse alguien hay que sacarlo de la sala, o su fila queda ahí para
        // siempre y el gate de "todos listos" no se cumple nunca porque espera a un
        // fantasma. Es el modo de fallo más molesto de una sala de espera.
        ServerManager.OnRemoteConnectionState += HandleRemoteConnectionState;
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        ServerManager.OnRemoteConnectionState -= HandleRemoteConnectionState;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        _entries.OnChange += HandleEntriesChanged;

        // Estado de red en variables PROPIAS: ver el comentario de TryGetLocalEntry.
        _netReady      = true;
        _localClientId = LocalConnection != null ? LocalConnection.ClientId : -1;

        OnLobbyChanged?.Invoke(); // la lista ya puede venir poblada al conectarse
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        _entries.OnChange -= HandleEntriesChanged;

        _netReady      = false;
        _localClientId = -1;
    }

    private void HandleEntriesChanged(SyncListOperation op, int index,
                                      LobbyEntry oldItem, LobbyEntry newItem, bool asServer)
    {
        // En host el callback llega dos veces (servidor y cliente); con una alcanza
        // para redibujar.
        if (asServer && IsClientInitialized) return;
        OnLobbyChanged?.Invoke();
    }

    private void HandleRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Stopped)
            ServerRemove(conn.ClientId);
    }

    // =========================================================
    // LECTURA (vale en cualquier peer)
    // =========================================================

    public IReadOnlyList<LobbyEntry> Entries => _entries;

    public bool TryGetEntry(int clientId, out LobbyEntry entry)
    {
        for (int i = 0; i < _entries.Count; i++)
            if (_entries[i].ClientId == clientId) { entry = _entries[i]; return true; }

        entry = default;
        return false;
    }

    // La fila de ESTE cliente, si ya se anoto.
    //
    // El guard va ANTES de tocar LocalConnection: esa propiedad de FishNet TIRA una
    // excepcion si el NetworkBehaviour todavia no se inicializo, asi que compararla
    // contra null no protege nada — revienta al LEERLA. Y la UI la consulta cada frame,
    // incluso antes de que haya conexion.
    public bool TryGetLocalEntry(out LobbyEntry entry)
    {
        entry = default;
        if (!_netReady || _localClientId < 0) return false;

        return TryGetEntry(_localClientId, out entry);
    }

    // Cuántos jugadores (sin espectadores) hay en un equipo.
    public int CountInTeam(int team)
    {
        int n = 0;
        for (int i = 0; i < _entries.Count; i++)
            if (!_entries[i].Spectator && _entries[i].Team == team) n++;
        return n;
    }

    public bool IsTeamFull(int team)
        => MaxPlayersPerTeam > 0 && CountInTeam(team) >= MaxPlayersPerTeam;

    // Nombre ya usado por OTRO. Se compara sin distinguir mayúsculas ni espacios de
    // los bordes: "Gus" y "gus " son el mismo nombre para cualquiera que los lea en
    // pantalla, y esa es la confusión que queremos evitar.
    public bool IsNameTaken(string playerName, int exceptClientId)
    {
        if (string.IsNullOrWhiteSpace(playerName)) return false;

        string wanted = playerName.Trim();
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].ClientId == exceptClientId) continue;
            if (string.Equals(_entries[i].PlayerName?.Trim(), wanted,
                              System.StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    // Cuántos jugadores de verdad hay anotados (los espectadores no cuentan).
    public int PlayerCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < _entries.Count; i++) if (!_entries[i].Spectator) n++;
            return n;
        }
    }

    // True si la partida puede arrancar: hay suficientes jugadores y TODOS los que no
    // son espectadores están listos. Con RequireAllReady apagado siempre da true, así
    // la escena de pruebas y las demos rápidas no dependen del gate.
    public bool AllReady
    {
        get
        {
            if (!RequireAllReady) return true;

            int players = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Spectator) continue;
                if (!_entries[i].Ready) return false;
                players++;
            }
            return players >= Mathf.Max(1, MinPlayersToStart);
        }
    }

    // =========================================================
    // PEDIDOS DEL CLIENTE
    // =========================================================

    // Anotarse (o cambiar lo elegido). El servidor valida nombre y cupo; si algo no da,
    // responde con el motivo y NO toca la sala.
    // =========================================================
    // BOTS
    // =========================================================
    //
    // Un bot es una fila más de la sala, con dos marcas: ClientId NEGATIVO y Ready
    // siempre en true.
    //
    // El id negativo es lo que los separa sin agregar un campo a la struct (que viaja
    // por red en cada cambio): las conexiones reales de FishNet son siempre >= 0, así
    // que el rango negativo está libre y no hay forma de que choquen.
    //
    // Ready en true porque un bot no confirma nada: si contara como "falta este", el
    // host no podría arrancar nunca sin sacarlos.

    public const int FirstBotId = -1000;

    public static bool IsBot(int clientId) => clientId <= FirstBotId;

    // Cuántos bots hay en la sala. Lo usa el panel para numerarlos y para saber si el
    // "+" todavía tiene lugar.
    public int BotCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < _entries.Count; i++) if (IsBot(_entries[i].ClientId)) n++;
            return n;
        }
    }

    // Solo el HOST agrega y saca bots, igual que solo él aprieta Start. Un cliente que
    // mande esto se ignora en silencio.
    [ServerRpc(RequireOwnership = false)]
    public void ServerAddBot(int team, int classIndex, NetworkConnection sender = null)
    {
        if (sender == null || !sender.IsHost) return;
        if (!MercenariesGameMode.IsValidTeam(team)) return;
        if (IsTeamFull(team)) return;

        _entries.Add(new LobbyEntry
        {
            ClientId   = NextFreeBotId(),
            PlayerName = NextFreeBotName(),
            Team       = team,
            ClassIndex = classIndex,
            Ready      = true,
            Spectator  = false,
        });
    }

    [ServerRpc(RequireOwnership = false)]
    public void ServerSetBotClass(int botId, int classIndex, NetworkConnection sender = null)
    {
        if (sender == null || !sender.IsHost || !IsBot(botId)) return;

        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].ClientId != botId) continue;

            LobbyEntry e = _entries[i];
            e.ClassIndex = classIndex;
            _entries[i]  = e;
            return;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ServerRemoveBot(int botId, NetworkConnection sender = null)
    {
        if (sender == null || !sender.IsHost || !IsBot(botId)) return;
        ServerRemove(botId);
    }

    // El primer id libre hacia abajo. Se busca en vez de llevar un contador porque los
    // bots se pueden sacar del medio: con un contador, agregar-sacar-agregar dejaría
    // huecos y los nombres se irían despegando de los ids.
    [Server]
    private int NextFreeBotId()
    {
        int id = FirstBotId;
        while (TryGetEntry(id, out _)) id--;
        return id;
    }

    [Server]
    private string NextFreeBotName()
    {
        for (int n = 1; n < 100; n++)
        {
            string candidate = $"Bot {n}";
            if (!IsNameTaken(candidate, int.MinValue)) return candidate;
        }
        return "Bot";
    }

    // Plan B para la clase de un bot: la lista del prefab del jugador. Es la MISMA que
    // usa el panel (por eso el índice vale igual), pero no depende de que haya UI.
    [Server]
    private CharacterClassDefinition FallbackClassAt(NetworkGameManager gm, int index)
    {
        if (gm == null || gm.PlayerPrefab == null || index < 0) return null;

        PlayerController pc = gm.PlayerPrefab.GetComponent<PlayerController>();
        if (pc == null || pc.MainBaseClasses == null || index >= pc.MainBaseClasses.Length) return null;

        return pc.MainBaseClasses[index];
    }

    // Le pide al administrador de partida que cree el personaje de cada bot. Se llama
    // una sola vez, cuando el host aprieta Start: antes de eso no hay partida en la que
    // meterlos, y después de eso la sala ya no cambia.
    [Server]
    private void ServerSpawnBots()
    {
        NetworkGameManager gm = FindFirstObjectByType<NetworkGameManager>();
        if (gm == null) return;

        UI_LobbyPanel panel = FindFirstObjectByType<UI_LobbyPanel>();

        foreach (LobbyEntry e in _entries)
        {
            if (!IsBot(e.ClientId)) continue;

            // La lista de clases elegibles vive en el panel: es la misma que ve el host
            // al elegirle la clase, así que el índice significa lo mismo de los dos lados.
            // Si no hay panel (un servidor sin UI), se cae a MainBaseClasses del prefab del
            // jugador, que es de donde sale esa lista.
            CharacterClassDefinition cls = panel != null ? panel.ClassAt(e.ClassIndex) : null;
            if (cls == null) cls = FallbackClassAt(gm, e.ClassIndex);
            gm.ServerSpawnBot(e.PlayerName, e.Team, cls);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ServerSubmit(string playerName, int team, int classIndex, bool spectator,
                             NetworkConnection sender = null)
    {
        if (sender == null) return;

        playerName = string.IsNullOrWhiteSpace(playerName) ? "Jugador" : playerName.Trim();

        if (IsNameTaken(playerName, sender.ClientId))
        {
            TargetReject(sender, ELobbyRejection.NameTaken);
            return;
        }

        // El cupo solo aplica a jugadores, y solo si de verdad está CAMBIANDO de equipo:
        // si ya estaba en ese equipo, seguir editando su nombre o su clase no puede
        // rebotar por "equipo lleno".
        bool alreadyInTeam = TryGetEntry(sender.ClientId, out LobbyEntry previous)
                             && !previous.Spectator && previous.Team == team;

        if (!spectator && team >= 1 && !alreadyInTeam && IsTeamFull(team))
        {
            TargetReject(sender, ELobbyRejection.TeamFull);
            return;
        }

        LobbyEntry entry = new LobbyEntry
        {
            ClientId   = sender.ClientId,
            PlayerName = playerName,
            Team       = spectator ? 0 : Mathf.Clamp(team, 0, TeamCount),
            ClassIndex = spectator ? -1 : classIndex,
            Spectator  = spectator,
            // Cambiar de equipo o de clase te saca de "listo": si no, alguien podría
            // marcar listo y después reacomodarse sin que nadie lo vea.
            Ready      = false,
        };

        ServerUpsert(entry);
    }

    [ServerRpc(RequireOwnership = false)]
    public void ServerSetReady(bool ready, NetworkConnection sender = null)
    {
        if (sender == null) return;
        if (!TryGetEntry(sender.ClientId, out LobbyEntry entry)) return;

        // Sin equipo o sin clase no se puede estar listo: es exactamente el caso que el
        // gate tiene que atajar (entrar sin haber elegido).
        if (ready && !entry.Spectator && (entry.Team < 1 || entry.ClassIndex < 0)) return;

        entry.Ready = ready;
        ServerUpsert(entry);
    }

    // =========================================================
    // ESCRITURA (servidor)
    // =========================================================

    [Server]
    private void ServerUpsert(LobbyEntry entry)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].ClientId != entry.ClientId) continue;
            _entries[i] = entry;   // SyncList detecta el reemplazo y lo replica
            return;
        }
        _entries.Add(entry);
    }

    // El HOST pide arrancar la partida. Solo el host: cualquier otro cliente que mande
    // esto se ignora en silencio, porque la decision de empezar es suya.
    //
    // No exige AllReady a proposito: el boton se pinta de otro color cuando falta gente,
    // pero si el host quiere arrancar igual (alguien se colgo, o esta probando solo),
    // puede. La UI comunica el estado; la decision es del host.
    [ServerRpc(RequireOwnership = false)]
    public void ServerRequestStart(NetworkConnection sender = null)
    {
        if (sender == null || !sender.IsHost) return;
        if (_netStarted.Value) return;   // ya arrancó: no spawnear los bots dos veces

        _netStarted.Value = true;
        ServerSpawnBots();
    }

    // Termina la partida y devuelve a todos a la sala. La llama MercenariesGameMode
    // cuando se agota la pantalla de fin de partida.
    //
    // Deja la sala como estaba antes del Start: sin personajes en el mapa y con el
    // panel de vuelta en pantalla (el panel se muestra con !MatchStarted). Lo que NO
    // se pierde es lo elegido — equipo, clase y nombre siguen puestos, así que volver
    // a jugar es apretar Confirmar y que el host apriete Start.
    [Server]
    public void ServerReturnToLobby()
    {
        if (!_netStarted.Value) return;

        NetworkGameManager gm = FindFirstObjectByType<NetworkGameManager>();
        if (gm != null) gm.ServerDespawnAllCharacters();

        _netStarted.Value = false;

        // Los humanos vuelven a "sin confirmar". Entre partida y partida la gente se
        // cambia de clase, se va o se cuelga: arrancar la siguiente con todos en verde
        // le sacaría al host la posibilidad de esperar a alguien.
        //
        // Los BOTS quedan listos como estaban — no tienen a nadie que los reconfirme, y
        // si se los quiere sacar es con el botón de su fila.
        for (int i = 0; i < _entries.Count; i++)
        {
            LobbyEntry e = _entries[i];
            if (IsBot(e.ClientId) || !e.Ready) continue;

            e.Ready     = false;
            _entries[i] = e;
        }

        Debug.Log("[Mercenarios] Todos de vuelta a la sala.");
    }

    [Server]
    public void ServerRemove(int clientId)
    {
        for (int i = _entries.Count - 1; i >= 0; i--)
            if (_entries[i].ClientId == clientId) _entries.RemoveAt(i);
    }

    [TargetRpc]
    private void TargetReject(NetworkConnection conn, ELobbyRejection reason)
        => OnRejected?.Invoke(reason);
}
