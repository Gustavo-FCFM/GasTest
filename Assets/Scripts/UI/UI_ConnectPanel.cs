using System;
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
    }

    private void OnEnable()  => Loc.OnLanguageChanged += RedrawStatus;

    private void OnDisable()
    {
        Loc.OnLanguageChanged -= RedrawStatus;
        if (IsShowing) Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Unsubscribe();
    }

    private void Update()
    {
        if (!_subscribed) Subscribe();

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
        if (Nm == null) { SetStatus(() => Loc.T("net.no_manager")); return; }

        string text = AddressInput != null ? AddressInput.text : "";
        if (!NetAddress.TryParse(text, out string host, out ushort port))
        {
            SetStatus(() => Loc.T("connect.need_address"));
            return;
        }

        UI_MainMenu.StopNetworking();   // un intento anterior a medias (ver Host)

        string target = NetAddress.Format(host, port);
        NetAddress.Joined = target;
        PlayerPrefs.SetString(LastAddressKey, target);
        PlayerPrefs.Save();

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

    // Mientras se conecta, Crear y Conectar no se pueden apretar (Volver lo cancela).
    private void RefreshButtons()
    {
        bool idle = _state == EState.Idle;
        if (HostButton != null)   HostButton.interactable   = idle;
        if (JoinButton != null)   JoinButton.interactable   = idle;
        if (AddressInput != null) AddressInput.interactable = idle;
    }

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
