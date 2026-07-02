using UnityEngine;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;

// ============================================================
// NetworkGameManager — MODO TODOS CONTRA TODOS
//
// CAMBIOS respecto a la versión anterior:
//
//   1. TODOS CONTRA TODOS: Cada jugador recibe un TeamID único
//      (1, 2, 3, 4...). Como todos tienen TeamID diferente,
//      IsEnemyOf() en el ASC los trata a todos como enemigos.
//
//   2. SIN LÍMITE DE JUGADORES: Se eliminó MaxPlayersPerTeam
//      y la lógica de Kick. Entra quien quiera.
//
//   3. SPAWN POINTS: Un solo array para todos, repartido en
//      round-robin. Configura varios en el Inspector.
//
// CUANDO QUIERAS HACER EQUIPOS:
//   Cambia: int uniqueTeamID = _totalPlayersEverConnected;
//   Por:    int uniqueTeamID = (_totalPlayersEverConnected % 2) + 1;
//   Eso da equipos 1 y 2 alternados.
// ============================================================

public class NetworkGameManager : NetworkBehaviour
{
    [Header("Prefab del Jugador")]
    [Tooltip("DEBE tener NetworkObject, AbilitySystemComponent y NetworkAbilitySystemComponent")]
    public GameObject PlayerPrefab;

    [Header("Puntos de Spawn")]
    [Tooltip("Pon varios puntos separados para que los jugadores no aparezcan encimados")]
    public Transform[] SpawnPoints;

    // Contadores del servidor
    private int _totalPlayersEverConnected = 0;
    private int _currentPlayerCount       = 0;

    private Dictionary<NetworkConnection, GameObject> _playerObjects
        = new Dictionary<NetworkConnection, GameObject>();

    private readonly SyncVar<int> _netPlayerCount = new SyncVar<int>();
    public int PublicPlayerCount => _netPlayerCount.Value;

    public System.Action OnPlayerCountChanged;

    // =========================================================
    // FISHNET CALLBACKS
    // =========================================================

    public override void OnStartServer()
    {
        base.OnStartServer();
        // Evita doble-suscripción si OnStartServer llega a correr más de una vez
        // (p. ej. al reiniciar Play sin Domain Reload) — sin esto, cada conexión
        // dispara HandlePlayerConnected varias veces y se spawnean jugadores duplicados.
        ServerManager.OnRemoteConnectionState -= OnRemoteConnectionStateChanged;
        ServerManager.OnRemoteConnectionState += OnRemoteConnectionStateChanged;
        Debug.Log("[GameManager] Servidor FFA iniciado. Sin límite de jugadores.");
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        ServerManager.OnRemoteConnectionState -= OnRemoteConnectionStateChanged;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        _netPlayerCount.OnChange += (p, n, s) => OnPlayerCountChanged?.Invoke();
    }

    // =========================================================
    // CONEXIONES
    // =========================================================

    private void OnRemoteConnectionStateChanged(
        NetworkConnection conn,
        FishNet.Transporting.RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == FishNet.Transporting.RemoteConnectionState.Started)
            HandlePlayerConnected(conn);
        else if (args.ConnectionState == FishNet.Transporting.RemoteConnectionState.Stopped)
            HandlePlayerDisconnected(conn);
    }

    [Server]
    private void HandlePlayerConnected(NetworkConnection conn)
    {
        _totalPlayersEverConnected++;
        _currentPlayerCount++;

        // TODOS CONTRA TODOS: TeamID único por jugador
        int uniqueTeamID  = _totalPlayersEverConnected;

        Transform  spawnPoint = GetSpawnPoint(_totalPlayersEverConnected);
        GameObject playerObj  = Instantiate(PlayerPrefab, spawnPoint.position, spawnPoint.rotation);
        ServerManager.Spawn(playerObj, conn);

        NetworkAbilitySystemComponent netASC =
            playerObj.GetComponent<NetworkAbilitySystemComponent>();

        if (netASC != null)
            netASC.AssignTeam(uniqueTeamID);
        else
            Debug.LogError("[GameManager] El Prefab no tiene NetworkAbilitySystemComponent.");

        _playerObjects[conn]  = playerObj;
        _netPlayerCount.Value = _currentPlayerCount;

        Debug.Log($"[GameManager] Jugador #{_totalPlayersEverConnected} conectado. " +
                  $"TeamID={uniqueTeamID}. En partida: {_currentPlayerCount}");
    }

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

    [Server]
    public void RespawnPlayer(NetworkConnection conn, float delay = 3f)
    {
        StartCoroutine(RespawnCoroutine(conn, delay));
    }

    private System.Collections.IEnumerator RespawnCoroutine(
        NetworkConnection conn, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (!_playerObjects.ContainsKey(conn)) yield break;

        GameObject playerObj = _playerObjects[conn];
        NetworkAbilitySystemComponent netASC =
            playerObj.GetComponent<NetworkAbilitySystemComponent>();
        if (netASC == null) yield break;

        Transform spawnPoint = GetRandomSpawnPoint();

        CharacterController cc = playerObj.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        playerObj.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
        if (cc != null) cc.enabled = true;

        netASC.Revive();

        Debug.Log($"[GameManager] Jugador TeamID={netASC.NetTeamID} revivido.");
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private Transform GetSpawnPoint(int playerNumber)
    {
        if (SpawnPoints == null || SpawnPoints.Length == 0)
        {
            Debug.LogWarning("[GameManager] Sin SpawnPoints. Usando origen.");
            return transform;
        }
        return SpawnPoints[(playerNumber - 1) % SpawnPoints.Length];
    }

    private Transform GetRandomSpawnPoint()
    {
        if (SpawnPoints == null || SpawnPoints.Length == 0) return transform;
        return SpawnPoints[Random.Range(0, SpawnPoints.Length)];
    }
}