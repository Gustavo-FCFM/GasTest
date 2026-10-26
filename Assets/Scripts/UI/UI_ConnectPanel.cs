using System;
using System.Collections.Generic;
using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_ConnectPanel
//
// La pantalla de CONEXIÓN: la que aparece al apretar Jugar en el menú principal. Crear
// partida, o escribir la dirección de otra y Conectar. Volver regresa al menú principal.
//
// Reemplaza al recuadro de red de la esquina (ConnectionHUD), que se dibujaba aparte, con
// otro estilo y sin control (26 de octubre de 2026, para el showcase). Ese queda solo para
// la escena de pruebas, que no tiene el prefab de menús.
//
// CUALQUIERA PUEDE CREAR LA PARTIDA (decidido el 26 de octubre): se fue el candado de "solo
// el editor o una build abierta con -host".
//
// LA DIRECCIÓN: una IP ("192.168.0.5") o dirección:puerto ("algo.ply.gg:60625", la de
// playit). Sin puerto, el 7770. La primera vez aparece la "Dirección por defecto"; después,
// la última a la que se conectó (se guarda en PlayerPrefs).
//
// CREAR LA PARTIDA arranca el servidor y RECIÉN CUANDO ARRANCÓ conecta al cliente local. Si
// el puerto está tomado (ya hay una partida abierta en esta PC), el servidor no arranca y se
// avisa: conectarse igual habría metido a este cliente en la partida de la otra ventana.
//
// SI SE CORTA LA CONEXIÓN (el host cerró, se cayó la red), esta pantalla vuelve sola con el
// aviso. Si el corte fue por "Salir" (que muestra el menú principal), no.
//
// LA LISTA DE PARTIDAS (etapa 2 del showcase, 26 de octubre): mientras esta pantalla está a la
// vista, busca sola las partidas abiertas en esta PC y en la red (LanDiscovery) y las muestra
// como botones: apretar una conecta, sin escribir nada. La dirección escrita queda para
// playit. Y la PC que tiene una partida abierta la anuncia, la haya creado como la haya creado.
// La dirección elegida de la lista NO se guarda como "la última" (no pisa la de playit).
//
// CON CONTROL, A sobre el campo de la dirección abre el teclado en pantalla
// (UI_OnScreenKeyboard).
//
// VIVE EN EL PREFAB DE MENÚS (Menus_UI ▸ Connect): el velo, la franja, el título y los
// botones están armados ahí y se acomodan a mano. Este script los conecta.
// ============================================================
public class UI_ConnectPanel : MonoBehaviour, IMenuScreen
{
    [Header("Piezas")]
    [Tooltip("El canvas de la conexión: se prende al abrir y se apaga al cerrar.")]
    [Label("Canvas de la conexión")]
    public GameObject ConnectCanvas;

    [Label("Botón Crear partida")]
    public Button HostButton;

    [Tooltip("Donde se escribe a quién conectarse: una IP (192.168.0.5) o dirección:puerto (la de playit).")]
    [Label("Campo de la dirección")]
    public TMP_InputField AddressInput;

    [Label("Botón Conectar")]
    public Button JoinButton;

    [Label("Botón Volver")]
    public Button BackButton;

    [Tooltip("\"Conectando…\", \"No se pudo conectar\", \"Se cortó la conexión\".")]
    [Label("Línea de estado")]
    public TMP_Text StatusText;

    [Header("Red")]
    [Tooltip("La dirección que aparece escrita la primera vez. Después aparece la última a la que se conectó.")]
    [Label("Dirección por defecto")]
    public string DefaultAddress = "127.0.0.1";

    [Tooltip("El puerto en el que se abre la partida al crearla (al que apunta el túnel de playit).")]
    [Label("Puerto al crear la partida")]
    public ushort HostPort = NetAddress.DefaultPort;

    [Header("Partidas en la red")]
    [Tooltip("Donde van las partidas encontradas, una debajo de la otra (tiene su VerticalLayoutGroup).")]
    [Label("Lista de partidas")]
    public RectTransform GamesList;

    [Tooltip("El molde de cada fila. Queda apagado; se copia una vez por partida.")]
    [Label("Molde de fila")]
    public UI_LanGameRow GameRowTemplate;

    [Tooltip("\"Buscando partidas…\": se ve mientras no se encontró ninguna.")]
    [Label("Texto de búsqueda")]
    public TMP_Text SearchingText;

    [Tooltip("Cuántas partidas entran en la lista. Las que sobran no se muestran.")]
    [Label("Máximo de filas")]
    public int MaxRows = 4;

    [Tooltip("El puerto UDP por el que se buscan y se anuncian las partidas. Tiene que ser el mismo en todas las PCs, y distinto del de la partida.")]
    [Label("Puerto para buscar partidas")]
    public ushort DiscoveryPort = 7771;

    public static UI_ConnectPanel Instance { get; private set; }

    // True mientras está a la vista. Lo mira la cámara del menú (para el blur).
    public static bool IsShowing { get; private set; }

    private const string LastAddressKey = "Merc.LastAddress";

    // En qué está: nada, creando la partida, conectándose a otra o ya conectado.
    private enum EState { Idle, Hosting, Joining, Connected }
    private EState _state;

    // La línea de estado se guarda como función: si se cambia de idioma con la pantalla
    // abierta, se vuelve a escribir en el nuevo.
    private Func<string> _status;

    // Creando o uniéndose: true desde que ESTA pantalla le pidió al cliente que se conecte. Un
    // aviso de "cliente cerrado" que llega antes es de un intento viejo que se termina de cerrar.
    private bool _clientRequested;

    private bool _subscribed;
    private bool _checkLost;   // se cortó: al frame siguiente se mira si alguien mostró el menú

    // La lista de partidas: lo que se encontró, lo que se está mostrando (en orden) y las filas.
    private readonly LanDiscovery _lan = new LanDiscovery();
    private readonly List<LanDiscovery.Game> _shown = new List<LanDiscovery.Game>();
    private readonly List<UI_LanGameRow> _rows = new List<UI_LanGameRow>();
    private int _lanVersion = -1;

    private static NetworkManager Nm => InstanceFinder.NetworkManager;
    private static bool Connected => Nm != null && (Nm.IsClientStarted || Nm.IsServerStarted);

    // =========================================================
    // CICLO
    // =========================================================

    private void Awake()
    {
        Instance = this;
        if (ConnectCanvas != null) ConnectCanvas.SetActive(false);

        if (HostButton != null) { HostButton.onClick.AddListener(AudioManager.Click); HostButton.onClick.AddListener(Host); }
        if (JoinButton != null) { JoinButton.onClick.AddListener(AudioManager.Click); JoinButton.onClick.AddListener(Join); }
        if (BackButton != null) { BackButton.onClick.AddListener(AudioManager.Click); BackButton.onClick.AddListener(Back); }

        // Enter en el campo = Conectar.
        if (AddressInput != null) AddressInput.onSubmit.AddListener(_ => Join());
        UI_OnScreenKeyboard.SetTitle(AddressInput, "connect.address");

        if (GameRowTemplate != null) GameRowTemplate.gameObject.SetActive(false);
        if (SearchingText != null) SearchingText.gameObject.SetActive(true);
    }

    private void OnEnable()  => Loc.OnLanguageChanged += Redraw;

    private void OnDisable()
    {
        Loc.OnLanguageChanged -= Redraw;
        if (IsShowing) Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Unsubscribe();
        _lan.Dispose();
    }

    private void Update()
    {
        if (!_subscribed) Subscribe();

        // Esta PC anuncia su partida mientras la tenga abierta, y busca las demás mientras esta
        // pantalla esté a la vista.
        _lan.Tick(DiscoveryPort, advertise: Nm != null && Nm.IsServerStarted, search: IsShowing);
        if (_lan.Version != _lanVersion) RedrawGames();

        // El corte se mira un frame después: si fue por "Salir", en ese frame ya se mostró el
        // menú principal y no hay que tapar nada.
        if (_checkLost)
        {
            _checkLost = false;
            if (!UI_MainMenu.IsShowing && !IsShowing && !Connected) Open(() => Loc.T("connect.lost"));
        }
    }

    private void Subscribe()
    {
        if (Nm == null) return;
        Nm.ClientManager.OnClientConnectionState += OnClientState;
        Nm.ServerManager.OnServerConnectionState += OnServerState;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || Nm == null) return;
        Nm.ClientManager.OnClientConnectionState -= OnClientState;
        Nm.ServerManager.OnServerConnectionState -= OnServerState;
        _subscribed = false;
    }

    // =========================================================
    // ABRIR / CERRAR
    // =========================================================

    public void Open() => Open(null);

    public void Open(Func<string> status)
    {
        if (ConnectCanvas != null) ConnectCanvas.SetActive(true);

        if (AddressInput != null && string.IsNullOrWhiteSpace(AddressInput.text))
            AddressInput.text = PlayerPrefs.GetString(LastAddressKey, DefaultAddress);

        SetStatus(status);
        RefreshButtons();

        if (!IsShowing)
        {
            IsShowing = true;
            UICursor.Request(this);
            MenuInput.Push(this);
        }
    }

    public void Hide()
    {
        if (ConnectCanvas != null) ConnectCanvas.SetActive(false);
        if (!IsShowing) return;

        IsShowing = false;
        MenuInput.Pop(this);
        UICursor.Release(this);
    }

    // =========================================================
    // ACCIONES
    // =========================================================

    private void Host()
    {
        if (_state != EState.Idle) return;
        if (Nm == null) { SetStatus(() => Loc.T("net.no_manager")); return; }

        // Un intento anterior que quedó a medias se corta ANTES (con el estado en Idle, así su
        // aviso de "se cerró" no cuenta como que la partida nueva falló).
        UI_MainMenu.StopNetworking();

        _state = EState.Hosting;
        _clientRequested = false;
        Nm.TransportManager.Transport.SetPort(HostPort);
        NetAddress.Joined = null;

        // El cliente local se conecta en OnServerState, cuando el servidor ya arrancó.
        if (!Nm.ServerManager.StartConnection())
        {
            _state = EState.Idle;
            SetStatus(() => Loc.T("connect.host_failed"));
        }
        else SetStatus(() => Loc.T("connect.creating"));

        RefreshButtons();
    }

    private void Join()
    {
        if (_state != EState.Idle) return;

        string text = AddressInput != null ? AddressInput.text : "";
        if (!NetAddress.TryParse(text, out string host, out ushort port))
        {
            SetStatus(() => Loc.T("connect.need_address"));
            return;
        }

        StartJoin(host, port, remember: true);
    }

    // Una partida de la lista: conecta directo, sin tocar la dirección escrita.
    private void JoinListed(int index)
    {
        if (index < 0 || index >= _shown.Count || !SameVersion(_shown[index])) return;
        LanDiscovery.Game g = _shown[index];
        StartJoin(g.Address, g.Port, remember: false);
    }

    // remember: guardarla como la última dirección (la escrita a mano sí; la de la lista no,
    // para no pisar la de playit).
    private void StartJoin(string host, ushort port, bool remember)
    {
        if (_state != EState.Idle) return;
        if (Nm == null) { SetStatus(() => Loc.T("net.no_manager")); return; }

        UI_MainMenu.StopNetworking();   // un intento anterior a medias (ver Host)

        string target = NetAddress.Format(host, port);
        NetAddress.Joined = target;
        if (remember)
        {
            PlayerPrefs.SetString(LastAddressKey, target);
            PlayerPrefs.Save();
        }

        _state = EState.Joining;
        _clientRequested = false;
        bool started = Nm.ClientManager.StartConnection(host, port);
        _clientRequested = true;
        if (!started)
        {
            _state = EState.Idle;
            SetStatus(() => Loc.T("connect.failed", ("address", target)));
        }
        else SetStatus(() => Loc.T("net.connecting", ("address", target)));

        RefreshButtons();
    }

    // Volver: si se está conectando, lo cancela; si no, regresa al menú principal.
    private void Back()
    {
        if (_state == EState.Hosting || _state == EState.Joining)
        {
            _state = EState.Idle;
            UI_MainMenu.StopNetworking();
            SetStatus(null);
            RefreshButtons();
            return;
        }

        Hide();
        if (UI_MainMenu.Instance != null) UI_MainMenu.Instance.Show();
    }

    // =========================================================
    // LO QUE AVISA LA RED
    // =========================================================

    private void OnServerState(ServerConnectionStateArgs args)
    {
        if (_state != EState.Hosting) return;

        if (args.ConnectionState == LocalConnectionState.Started)
        {
            if (!Nm.ClientManager.Started) Nm.ClientManager.StartConnection("127.0.0.1", HostPort);
            _clientRequested = true;
        }
        else if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            _state = EState.Idle;
            SetStatus(() => Loc.T("connect.host_failed"));
            RefreshButtons();
        }
    }

    private void OnClientState(ClientConnectionStateArgs args)
    {
        switch (args.ConnectionState)
        {
            case LocalConnectionState.Started:
                // Conectado (como cliente o como host): la sala aparece sola.
                _state = EState.Connected;
                SetStatus(null);
                Hide();
                break;

            case LocalConnectionState.Stopped:
                // Creando o uniéndose, solo cuenta el cierre del cliente que ESTA pantalla conectó. Otro
                // (uno viejo que se termina de cerrar) no tira abajo la partida nueva.
                if ((_state == EState.Hosting || _state == EState.Joining) && !_clientRequested) break;

                EState was = _state;
                _state = EState.Idle;

                if (was == EState.Joining)
                {
                    string target = NetAddress.Joined;
                    SetStatus(() => Loc.T("connect.failed", ("address", target)));
                }
                else if (was == EState.Hosting)
                {
                    // El cliente local no llegó a su propio servidor: se cierra todo.
                    UI_MainMenu.StopNetworking();
                    SetStatus(() => Loc.T("connect.host_failed"));
                }
                else if (was == EState.Connected) _checkLost = true;

                RefreshButtons();
                break;
        }
    }

    // =========================================================
    // DIBUJO
    // =========================================================

    private void SetStatus(Func<string> status)
    {
        _status = status;
        RedrawStatus();
    }

    private void RedrawStatus()
    {
        if (StatusText != null) StatusText.text = _status != null ? _status() : "";
    }

    private void Redraw()
    {
        RedrawStatus();
        RedrawGames();
    }

    // Mientras se conecta, Crear, Conectar y la lista no se pueden apretar (Volver lo cancela).
    private void RefreshButtons()
    {
        bool idle = _state == EState.Idle;
        if (HostButton != null)   HostButton.interactable   = idle;
        if (JoinButton != null)   JoinButton.interactable   = idle;
        if (AddressInput != null) AddressInput.interactable = idle;

        for (int i = 0; i < _rows.Count; i++)
            if (_rows[i].Button != null)
                _rows[i].Button.interactable = idle && i < _shown.Count && SameVersion(_shown[i]);
    }

    // =========================================================
    // LA LISTA DE PARTIDAS
    // =========================================================

    // Las filas se reusan (no se destruyen y se vuelven a crear): así la que tiene elegida el
    // control no se pierde cada vez que una partida contesta.
    private void RedrawGames()
    {
        _lanVersion = _lan.Version;

        _shown.Clear();
        _shown.AddRange(_lan.Games);
        // Las de esta PC primero; después por nombre. Un orden fijo: las filas no saltan de lugar.
        _shown.Sort((a, b) => a.SamePc != b.SamePc ? (a.SamePc ? -1 : 1)
                            : a.Name != b.Name ? string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase)
                            : string.CompareOrdinal(a.Id, b.Id));

        int count = 0;
        if (GamesList != null && GameRowTemplate != null)
        {
            count = Mathf.Min(_shown.Count, Mathf.Max(0, MaxRows));
            for (int i = 0; i < count; i++)
            {
                if (i >= _rows.Count) _rows.Add(NewRow(i));
                DrawRow(_rows[i], _shown[i]);
            }
            for (int i = 0; i < _rows.Count; i++)
                if (_rows[i].gameObject.activeSelf != i < count) _rows[i].gameObject.SetActive(i < count);
        }

        if (SearchingText != null)
        {
            SearchingText.gameObject.SetActive(count == 0);
            SearchingText.text = Loc.T("connect.lan_searching");
        }
        RefreshButtons();
    }

    private UI_LanGameRow NewRow(int index)
    {
        UI_LanGameRow row = Instantiate(GameRowTemplate, GamesList);
        row.name = "Game " + (index + 1);
        if (row.Button != null)
        {
            row.Button.onClick.AddListener(AudioManager.Click);
            row.Button.onClick.AddListener(() => JoinListed(index));
        }
        return row;
    }

    private static void DrawRow(UI_LanGameRow row, LanDiscovery.Game g)
    {
        if (row.NameText != null) row.NameText.text = Loc.T("connect.lan_room", ("name", g.Name));
        if (row.InfoText == null) return;

        string state = !SameVersion(g) ? Loc.T("connect.lan_version", ("version", g.Version))
                     : Loc.T(g.Playing ? "connect.lan_playing" : "connect.lan_lobby");
        string where = g.SamePc ? Loc.T("connect.lan_this_pc") : NetAddress.Format(g.Address, g.Port);
        row.InfoText.text = Loc.T("connect.lan_info", ("players", g.Players), ("state", state), ("where", where));
    }

    // Una versión distinta del juego no se puede conectar (la red no es la misma): se ve, pero
    // no se puede apretar.
    private static bool SameVersion(LanDiscovery.Game g) => g.Version == Application.version;

    // =========================================================
    // IMenuScreen
    // =========================================================

    public int MenuLayer => 1;

    public GameObject DefaultSelection
    {
        get
        {
            Button b = _state == EState.Idle ? HostButton : BackButton;
            return b != null ? b.gameObject : null;
        }
    }

    public Transform SelectionRoot => ConnectCanvas != null ? ConnectCanvas.transform : transform;
    public void OnBack()  => Back();
    public void OnPause() => Back();
}
