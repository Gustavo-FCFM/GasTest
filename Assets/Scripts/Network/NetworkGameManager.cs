using UnityEngine;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using FishNet.Broadcast;
using FishNet.Transporting;

// Pedido de entrada a la partida que manda el menú (UI_LobbyMenu) al servidor.
//
// Es un BROADCAST y no un ServerRpc a propósito: un ServerRpc viaja "a través de" un
// NetworkObject ya inicializado en el cliente, y acá el jugador todavía NO tiene
// personaje — de ahí el error "Cannot complete action because client is not active".
// Los broadcasts no dependen de ningún objeto, que es justo lo que hace falta antes
// de spawnear.
public struct SpawnRequestBroadcast : IBroadcast
{
    public string PlayerName;
    public int    TeamID;
}

// ============================================================
// NetworkGameManager
//
// Administra la partida en el servidor: crea el personaje de cada jugador cuando
// este confirma el menú de entrada, lo despawnea al desconectarse, y maneja el
// respawn tras morir. Vive en la escena de la arena, no en un prefab.
//
// EQUIPOS: el jugador elige el suyo (1, 2 o 3) en el menú de entrada; ya no se
// asigna automático. Antes cada conexión recibía un TeamID único —todos contra
// todos—, y por eso NADIE era aliado de nadie: IsAllyOf comparaba TeamIDs que nunca
// coincidían, así que las habilidades con Targets = Allies solo afectaban a quien
// las lanzaba.
// ============================================================
public class NetworkGameManager : NetworkBehaviour
{
    [Header("Prefab del Jugador")]
    [Tooltip("DEBE tener NetworkObject, AbilitySystemComponent y NetworkAbilitySystemComponent")]
    public GameObject PlayerPrefab;

    [Header("Puntos de Spawn")]
    [Tooltip("Pon varios puntos separados para que los jugadores no aparezcan encimados")]
    public Transform[] SpawnPoints;

    // Cuenta total histórica de conexiones (nunca baja) — se usa para
    // repartir spawn points y TeamID únicos de forma determinística.
    private int _totalPlayersEverConnected = 0;
    // Cuántos jugadores hay conectados AHORA (baja al desconectarse).
    private int _currentPlayerCount       = 0;

    // Qué GameObject de jugador le pertenece a cada conexión.
    private Dictionary<NetworkConnection, GameObject> _playerObjects
        = new Dictionary<NetworkConnection, GameObject>();

    private readonly SyncVar<int> _netPlayerCount = new SyncVar<int>();
    public int PublicPlayerCount => _netPlayerCount.Value;

    public System.Action OnPlayerCountChanged;

    // =========================================================
    // CICLO DE VIDA DE FISHNET
    // =========================================================

    // Se suscribe a los eventos de conexión/carga de escena del servidor.
    public override void OnStartServer()
    {
        base.OnStartServer();
        // Evita doble-suscripción si OnStartServer llega a correr más de una vez
        // (p. ej. al reiniciar Play sin Domain Reload) — sin esto, cada conexión
        // dispara HandleClientLoadedStartScenes varias veces y se spawnean jugadores duplicados.
        ServerManager.OnRemoteConnectionState -= OnRemoteConnectionStateChanged;
        ServerManager.OnRemoteConnectionState += OnRemoteConnectionStateChanged;

        // Escuchar los pedidos de entrada del menú (ver SpawnRequestBroadcast).
        ServerManager.UnregisterBroadcast<SpawnRequestBroadcast>(OnSpawnRequest);
        ServerManager.RegisterBroadcast<SpawnRequestBroadcast>(OnSpawnRequest);

        // El spawn del jugador va atado a OnClientLoadedStartScenes, NO a
        // OnRemoteConnectionState. FishNet avisa explícitamente: "not recommended
        // to spawn objects for connections until they have loaded start scenes".
        // Si spawneamos antes de que la conexión termine de cargar la escena,
        // esa conexión nunca queda registrada como observadora de los objetos que
        // ya existían (y viceversa) → los jugadores no se ven entre sí, y el que
        // se conecta tarde recibe RPCs/estado a medias (animaciones y habilidades
        // rotas).
        SceneManager.OnClientLoadedStartScenes -= HandleClientLoadedStartScenes;
        SceneManager.OnClientLoadedStartScenes += HandleClientLoadedStartScenes;

        Debug.Log("[GameManager] Servidor iniciado. Esperando que los jugadores confirmen el menú de entrada.");
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        ServerManager.OnRemoteConnectionState -= OnRemoteConnectionStateChanged;
        ServerManager.UnregisterBroadcast<SpawnRequestBroadcast>(OnSpawnRequest);
        SceneManager.OnClientLoadedStartScenes -= HandleClientLoadedStartScenes;
    }

    // Se suscribe a los cambios del contador de jugadores para reenviar
    // el evento local OnPlayerCountChanged (ej: lo usa un menú de lobby).
    public override void OnStartClient()
    {
        base.OnStartClient();
        _netPlayerCount.OnChange += (p, n, s) => OnPlayerCountChanged?.Invoke();
    }

    // =========================================================
    // CONEXIONES
    // =========================================================

    // Solo nos importa la desconexión acá — el spawn se maneja en
    // HandleClientLoadedStartScenes.
    private void OnRemoteConnectionStateChanged(
        NetworkConnection conn,
        FishNet.Transporting.RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == FishNet.Transporting.RemoteConnectionState.Stopped)
            HandlePlayerDisconnected(conn);
    }

    // Spawnea el jugador de una conexión ni bien termina de cargar la
    // escena inicial: le asigna spawn point y TeamID únicos, lo registra
    // como observador de la escena por defecto, y lo suma al conteo.
    [Server]
    private void HandleClientLoadedStartScenes(NetworkConnection conn, bool asServer)
    {
        if (!asServer) return;

        // YA NO se spawnea acá. El jugador aparece recién cuando confirma el menú de
        // entrada (nombre + equipo), que llega por SpawnRequestBroadcast. Así el equipo
        // lo elige el jugador en vez de asignarse automático, y el personaje entra a
        // la partida ya con su nombre puesto.
    }

    // Llega el pedido del menú de entrada de un cliente. La conexión la da FishNet
    // ('conn'), así que un cliente no puede pedir el spawn de otro.
    private void OnSpawnRequest(NetworkConnection conn, SpawnRequestBroadcast msg, Channel channel)
    {
        if (conn == null) return;
        // Ya tiene personaje (doble clic en confirmar, o un cliente insistente).
        if (_playerObjects.ContainsKey(conn)) return;

        SpawnPlayerFor(conn, msg.PlayerName, msg.TeamID);
    }

    // Crea el personaje de una conexión con el nombre y equipo elegidos.
    [Server]
    private void SpawnPlayerFor(NetworkConnection conn, string playerName, int teamID)
    {
        _totalPlayersEverConnected++;
        _currentPlayerCount++;

        // El equipo lo elige el jugador en el menú de entrada. Se acota al rango
        // válido (1..3): fuera de eso, TeamID 0 significa "neutral, hostil a todos"
        // y rompería la lógica de aliados sin que se note por qué.
        int uniqueTeamID = Mathf.Clamp(teamID, 1, 3);

        // Con el modo Mercenarios en la escena, cada equipo entra por SU base (es
        // media regla del modo: los tres equipos arrancan encerrados en su sala
        // segura). Sin modo de juego, se cae al reparto redondo de siempre.
        Transform  spawnPoint = ResolveSpawnPointForTeam(uniqueTeamID)
                                ?? GetSpawnPoint(_totalPlayersEverConnected);
        GameObject playerObj  = Instantiate(PlayerPrefab, spawnPoint.position, spawnPoint.rotation);
        ServerManager.Spawn(playerObj, conn);

        // Registra al dueño en la escena por defecto para el sistema de
        // observers de FishNet — sin esto, esta conexión y las que ya estaban
        // en la escena nunca se "ven" entre sí (Scene Condition del Observer
        // Manager nunca se cumple).
        NetworkObject nob = playerObj.GetComponent<NetworkObject>();
        if (nob != null) SceneManager.AddOwnerToDefaultScene(nob);

        NetworkAbilitySystemComponent netASC =
            playerObj.GetComponent<NetworkAbilitySystemComponent>();

        if (netASC != null)
        {
            netASC.AssignTeam(uniqueTeamID);
            // Nombre elegido en el menú de entrada: se sincroniza a todos para
            // mostrarlo sobre su barra de vida (ver UI_WorldHealthbar).
            netASC.AssignPlayerName(playerName);
        }
        else
        {
            Debug.LogError("[GameManager] El Prefab no tiene NetworkAbilitySystemComponent.");
        }

        _playerObjects[conn]  = playerObj;
        _netPlayerCount.Value = _currentPlayerCount;

        Debug.Log($"[GameManager] '{playerName}' entró como jugador #{_totalPlayersEverConnected}. " +
                  $"TeamID={uniqueTeamID}. En partida: {_currentPlayerCount}");
    }

    // Despawnea el jugador de una conexión que se cayó y actualiza el conteo.
    [Server]
    private void HandlePlayerDisconnected(NetworkConnection conn)
    {
        if (!_playerObjects.ContainsKey(conn)) return;

        ServerManager.Despawn(_playerObjects[conn]);
        _playerObjects.Remove(conn);

        _currentPlayerCount   = Mathf.Max(0, _currentPlayerCount - 1);
        _netPlayerCount.Value = _currentPlayerCount;

        Debug.Log($"[GameManager] Jugador desconectado. En partida: {_currentPlayerCount}");
    }

    // =========================================================
    // RESPAWN
    // =========================================================

    // Programa el respawn de un jugador tras "delay" segundos (lo llama
    // PlayerController al morir).
    [Server]
    public void RespawnPlayer(NetworkConnection conn, float delay = 3f)
    {
        // El modo de juego manda sobre el tiempo de reaparición: en Mercenarios son 5
        // segundos (y se toca desde ahí, no desde cada llamador).
        if (MercenariesGameMode.Instance != null)
            delay = MercenariesGameMode.Instance.RespawnSeconds;

        StartCoroutine(RespawnCoroutine(conn, delay));
    }

    // Espera el delay, manda al jugador a un spawn point al azar, y lo revive.
    private System.Collections.IEnumerator RespawnCoroutine(
        NetworkConnection conn, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (!_playerObjects.ContainsKey(conn)) yield break;

        GameObject playerObj = _playerObjects[conn];
        NetworkAbilitySystemComponent netASC =
            playerObj.GetComponent<NetworkAbilitySystemComponent>();
        if (netASC == null) yield break;

        // Reaparecer en la BASE PROPIA (regla del modo): dentro de la sala segura la
        // vida vuelve al máximo y no te pueden tocar mientras te reorganizás.
        Transform spawnPoint = ResolveSpawnPointForTeam(netASC.NetTeamID) ?? GetRandomSpawnPoint();

        // BUG QUE ARREGLAMOS: acá se movía el transform DESDE EL SERVIDOR. Pero el
        // NetworkTransform del jugador es client-authoritative, así que el próximo
        // paquete del dueño (que seguía en su posición de muerte) pisaba el cambio:
        // el host veía al jugador aparecer en el spawn y "reacomodarse" de vuelta,
        // y en la pantalla del propio jugador nunca se movía. El teletransporte lo
        // tiene que ejecutar el DUEÑO — ver ServerTeleportOwnerTo.
        netASC.ServerTeleportOwnerTo(spawnPoint.position, spawnPoint.forward);

        netASC.Revive();
    }

    // =========================================================
    // HELPERS
    // =========================================================

    // Punto de aparición de la base de un equipo, si hay un modo de juego que las
    // administre. Devuelve null cuando no lo hay (escena de pruebas suelta), y ahí
    // quien llama usa el reparto de SpawnPoints de siempre.
    private Transform ResolveSpawnPointForTeam(int teamID)
    {
        MercenariesGameMode gm = MercenariesGameMode.Instance;
        return gm != null ? gm.GetTeamSpawnPoint(teamID) : null;
    }

    // Punto de spawn determinístico por número de jugador (round-robin).
    private Transform GetSpawnPoint(int playerNumber)
    {
        if (SpawnPoints == null || SpawnPoints.Length == 0)
        {
            Debug.LogWarning("[GameManager] Sin SpawnPoints. Usando origen.");
            return transform;
        }
        return SpawnPoints[(playerNumber - 1) % SpawnPoints.Length];
    }

    // Punto de spawn al azar (usado para respawns).
    private Transform GetRandomSpawnPoint()
    {
        if (SpawnPoints == null || SpawnPoints.Length == 0) return transform;
        return SpawnPoints[Random.Range(0, SpawnPoints.Length)];
    }
}
