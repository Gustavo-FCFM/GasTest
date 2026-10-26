using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using FishNet;
using FishNet.Managing;
using UnityEngine;

// ============================================================
// LanDiscovery
//
// Las partidas abiertas en ESTA PC o en la red, para elegirlas de una lista en vez de escribir
// la IP (26 de octubre de 2026, etapa 2 del showcase). Funciona sola: la PC que tiene la
// partida la anuncia, y la pantalla de conexión pregunta quién hay.
//
// CÓMO: por UDP, en un puerto propio (el "Puerto para buscar partidas" de la pantalla de
// conexión, 7771), aparte del de la partida.
//   · Mientras la pantalla de conexión está abierta, cada segundo manda una pregunta ("¿hay
//     alguien?") a toda la red (broadcast) y a esta misma PC (127.0.0.1).
//   · La PC que tiene la partida abierta escucha ese puerto y le contesta a cada uno: el nombre
//     de la sala, el puerto de la partida, cuántos jugadores hay, si ya están jugando y la
//     versión del juego.
//   · Una partida que deja de contestar sale de la lista a los pocos segundos.
//
// SOLO EN LA MISMA RED (o en la misma PC): el broadcast no cruza internet. Para jugar por
// internet sigue la dirección de playit, escrita a mano.
//
// El firewall de Windows tiene que dejar entrar al juego en la PC del host: es el mismo
// "Permitir" que ya hace falta para la partida. Si el Wi-Fi aísla los equipos entre sí, no
// aparece nada (y tampoco se podría jugar).
//
// La usa UI_ConnectPanel: la crea, la llama cada frame (Tick) y dibuja Games.
// ============================================================
public sealed class LanDiscovery : IDisposable
{
    public struct Game
    {
        public string Id;        // uno nuevo cada vez que se abre una partida
        public string Address;   // a dónde conectarse: la IP del host, o 127.0.0.1 si es esta PC
        public ushort Port;      // el de la partida (Tugboat), no el de la búsqueda
        public string Name;      // el nombre del host en la sala (o el de su PC)
        public int    Players;   // los conectados, sin contar bots
        public bool   Playing;   // ya arrancó la partida (si no, están en la sala)
        public string Version;   // la versión del juego del host (Application.version)
        public bool   SamePc;
        public float  SeenAt;
    }

    private const string Ask    = "MERC?1";
    private const string Answer = "MERC!1";
    private const float  AskEvery    = 1f;     // segundos entre preguntas
    private const float  ForgetAfter = 3.5f;   // segundos sin contestar para salir de la lista

    // Sin esto, en Windows un "puerto cerrado" (preguntarle a 127.0.0.1 sin nadie escuchando)
    // rompe la próxima lectura del socket.
    private const int SioUdpConnReset = -1744830452;

    public IReadOnlyList<Game> Games => _games;

    // Sube cada vez que la lista cambia: la pantalla redibuja solo entonces.
    public int Version { get; private set; }

    private readonly List<Game> _games = new List<Game>();
    private readonly byte[] _buffer = new byte[1024];

    private Socket _server;          // la PC con la partida: escucha las preguntas
    private ushort _serverPort;
    private string _serverId;
    private float  _serverRetryAt;
    private bool   _warnedServer;

    private Socket _client;          // la pantalla de conexión: pregunta y junta respuestas
    private float  _nextAsk;
    private bool   _warnedClient;

    // A dónde preguntar (broadcast de cada placa de red, más 127.0.0.1) y las IPs de esta PC.
    private readonly List<IPAddress> _targets = new List<IPAddress>();
    private readonly HashSet<IPAddress> _mine = new HashSet<IPAddress>();
    private float _interfacesReadAt = -999f;

    // =========================================================
    // CADA FRAME
    // =========================================================

    // advertise: esta PC tiene la partida abierta (contesta). search: la pantalla de conexión
    // está a la vista (pregunta).
    public void Tick(ushort port, bool advertise, bool search)
    {
        if (advertise) Advertise(port); else CloseServer();
        if (search)    Search(port);    else StopSearch();
    }

    public void Dispose()
    {
        CloseServer();
        StopSearch();
    }

    // =========================================================
    // LA PC CON LA PARTIDA: CONTESTA
    // =========================================================

    private void Advertise(ushort port)
    {
        if (_server != null && _serverPort != port) CloseServer();
        if (_server == null)
        {
            if (Time.unscaledTime < _serverRetryAt) return;
            try
            {
                _server = NewSocket();
                _server.Bind(new IPEndPoint(IPAddress.Any, port));
                _serverPort = port;
                _serverId = Guid.NewGuid().ToString("N").Substring(0, 8);
                _warnedServer = false;
            }
            catch (Exception e)
            {
                CloseServer();
                _serverRetryAt = Time.unscaledTime + 5f;
                if (!_warnedServer)
                {
                    _warnedServer = true;
                    Debug.LogWarning($"[LanDiscovery] No se pudo escuchar el puerto {port} para anunciar la partida " +
                                     $"(¿otra ventana lo está usando?). La partida no aparece en las listas: {e.Message}");
                }
                return;
            }
        }

        EndPoint from = new IPEndPoint(IPAddress.Any, 0);
        for (int i = 0; i < 32 && Receive(_server, ref from, out string msg); i++)
            if (msg == Ask) SendTo(_server, Encoding.UTF8.GetBytes(Describe()), (IPEndPoint)from);
    }

    // "MERC!1|id|versión|puerto|jugadores|jugando|nombre". El nombre va último: es lo único
    // que escribe un jugador.
    private string Describe()
    {
        NetworkManager nm = InstanceFinder.NetworkManager;
        ushort gamePort = nm != null ? nm.TransportManager.Transport.GetPort() : NetAddress.DefaultPort;

        int players = 0;
        bool playing = false;
        string name = null;
        LobbyManager lobby = LobbyManager.Instance;
        if (lobby != null)
        {
            foreach (LobbyEntry e in lobby.Entries)
                if (e.ClientId >= 0) players++;   // los bots tienen id negativo
            playing = lobby.MatchStarted;
            if (lobby.TryGetLocalEntry(out LobbyEntry me)) name = me.PlayerName;
        }
        else if (nm != null) players = nm.ServerManager.Clients.Count;

        // Sin nombre propio todavía: el de la PC, que al menos distingue una sala de otra.
        if (string.IsNullOrWhiteSpace(name) || name == Loc.T("lobby.default_name")) name = Environment.MachineName;

        return string.Join("|", Answer, _serverId, Application.version.Replace("|", "/"), gamePort, players,
                           playing ? 1 : 0, name.Replace("|", "/"));
    }

    private void CloseServer()
    {
        if (_server == null) return;
        _server.Close();
        _server = null;
    }

    // =========================================================
    // LA PANTALLA DE CONEXIÓN: PREGUNTA
    // =========================================================

    private void Search(ushort port)
    {
        float now = Time.unscaledTime;

        if (_client == null)
        {
            try
            {
                _client = NewSocket();
                _client.EnableBroadcast = true;
                _client.Bind(new IPEndPoint(IPAddress.Any, 0));
                _warnedClient = false;
            }
            catch (Exception e)
            {
                if (_client != null) _client.Close();
                _client = null;
                if (!_warnedClient)
                {
                    _warnedClient = true;
                    Debug.LogWarning("[LanDiscovery] No se pudo abrir la búsqueda de partidas: " + e.Message);
                }
                return;
            }
            _nextAsk = 0f;
        }

        if (now >= _nextAsk)
        {
            _nextAsk = now + AskEvery;
            ReadInterfaces(now);
            byte[] ask = Encoding.UTF8.GetBytes(Ask);
            foreach (IPAddress target in _targets) SendTo(_client, ask, new IPEndPoint(target, port));
        }

        EndPoint from = new IPEndPoint(IPAddress.Any, 0);
        for (int i = 0; i < 32 && Receive(_client, ref from, out string msg); i++)
            if (msg != null) Heard(msg, ((IPEndPoint)from).Address, now);

        if (_games.RemoveAll(g => now - g.SeenAt > ForgetAfter) > 0) Version++;
    }

    private void Heard(string msg, IPAddress from, float now)
    {
        string[] p = msg.Split(new[] { '|' }, 7);
        if (p.Length < 7 || p[0] != Answer) return;
        if (!ushort.TryParse(p[3], out ushort gamePort) || !int.TryParse(p[4], out int players)) return;

        bool samePc = IPAddress.IsLoopback(from) || _mine.Contains(from);
        Game g = new Game
        {
            Id = p[1], Version = p[2], Port = gamePort, Players = players, Playing = p[5] == "1", Name = p[6],
            SamePc = samePc, Address = samePc ? "127.0.0.1" : from.ToString(), SeenAt = now,
        };

        int i = _games.FindIndex(x => x.Id == g.Id);
        if (i < 0) { _games.Add(g); Version++; return; }

        // La misma partida contesta por más de un camino (127.0.0.1 y la red, o dos placas de
        // red): se queda la dirección que ya tenía, salvo que la nueva sea de esta PC.
        Game old = _games[i];
        if (!g.SamePc || old.SamePc) { g.Address = old.Address; g.SamePc = old.SamePc; }

        bool changed = old.Players != g.Players || old.Playing != g.Playing || old.Name != g.Name ||
                       old.Address != g.Address || old.Version != g.Version || old.Port != g.Port;
        _games[i] = g;
        if (changed) Version++;
    }

    private void StopSearch()
    {
        if (_client != null) { _client.Close(); _client = null; }
        if (_games.Count > 0) { _games.Clear(); Version++; }
    }

    // El broadcast de cada placa de red (192.168.0.255) y no solo 255.255.255.255: con varias
    // placas (Wi-Fi, cable, VirtualBox), Windows manda ese por una sola. Se relee cada 5 s.
    private void ReadInterfaces(float now)
    {
        if (_targets.Count > 0 && now - _interfacesReadAt < 5f) return;
        _interfacesReadAt = now;

        _targets.Clear();
        _mine.Clear();
        _targets.Add(IPAddress.Loopback);
        _targets.Add(IPAddress.Broadcast);

        try
        {
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (UnicastIPAddressInformation u in nic.GetIPProperties().UnicastAddresses)
                {
                    if (u.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    _mine.Add(u.Address);

                    IPAddress mask = u.IPv4Mask;
                    if (mask == null || mask.Equals(IPAddress.Any)) continue;
                    byte[] ip = u.Address.GetAddressBytes(), m = mask.GetAddressBytes();
                    for (int k = 0; k < ip.Length; k++) ip[k] = (byte)(ip[k] | ~m[k]);
                    IPAddress broadcast = new IPAddress(ip);
                    if (!_targets.Contains(broadcast)) _targets.Add(broadcast);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[LanDiscovery] No se pudieron leer las placas de red (se busca igual en 255.255.255.255): " + e.Message);
        }
    }

    // =========================================================
    // SOCKETS
    // =========================================================

    private static Socket NewSocket()
    {
        Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) { Blocking = false };
        try { s.IOControl(SioUdpConnReset, new byte[4], null); }
        catch (Exception) { /* fuera de Windows no existe ni hace falta */ }
        return s;
    }

    // True mientras haya algo que leer. msg queda null si lo que llegó fue un error de un envío
    // anterior (se sigue leyendo).
    private bool Receive(Socket s, ref EndPoint from, out string msg)
    {
        msg = null;
        try
        {
            if (s.Available <= 0) return false;
            int n = s.ReceiveFrom(_buffer, ref from);
            msg = Encoding.UTF8.GetString(_buffer, 0, n);
            return true;
        }
        catch (SocketException) { return true; }
        catch (ObjectDisposedException) { return false; }
    }

    // Sin red, o una placa que no deja mandar broadcast: esa pregunta se pierde y listo.
    private static void SendTo(Socket s, byte[] data, IPEndPoint to)
    {
        try { s.SendTo(data, to); }
        catch (SocketException) { }
    }
}
