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
        _netStarted.Value = true;
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
