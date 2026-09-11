using UnityEngine;
using FishNet;
using FishNet.Managing;

// ============================================================
// ConnectionHUD
//
// HUD mínimo de conexión para pruebas con amigos. Dibuja botones en
// pantalla (IMGUI / OnGUI) para no depender de ningún Canvas — así
// funciona en la build sin configurar nada.
//
// FLUJO DE PRUEBA:
//   - TÚ (host): abrís la escena en el editor y presionás "Iniciar Host".
//   - TUS AMIGOS (build): presionan "Conectarse (Cliente)" y entran a tu editor.
//
// El botón de Host solo aparece en el editor de Unity (ver HostOnlyInEditor),
// así que las builds que les pasás a tus amigos SOLO pueden ser cliente:
// nadie más puede hostear.
//
// IMPORTANTE (para que te lleguen desde internet):
//   - Misma red/LAN: poné tu IP local (ej: 192.168.x.x) en HostAddress.
//   - Por internet: tus amigos necesitan tu IP PÚBLICA y vos abrir/redirigir
//     el puerto (por defecto 7770 UDP) en tu router, o usar una VPN tipo
//     Radmin VPN / ZeroTier / Hamachi (ahí usás la IP que te da la VPN).
// ============================================================
public class ConnectionHUD : MonoBehaviour
{
    [Header("Conexión")]
    [Tooltip("IP del host (tu PC). LAN: tu IP local. Internet: tu IP pública o la de la VPN. " +
             "Tus amigos igual pueden editarla en pantalla antes de conectarse.")]
    public string HostAddress = "127.0.0.1";

    [Tooltip("Puerto del transporte (Tugboat por defecto usa 7770).")]
    public ushort Port = 7770;

    [Header("Permisos")]
    [Tooltip("Si está activo, el botón 'Iniciar Host' SOLO aparece dentro del editor de Unity. " +
             "Las builds de tus amigos solo verán el botón de Cliente → solo vos podés ser host.")]
    public bool HostOnlyInEditor = true;

    // Texto editable en pantalla (arranca con los valores del inspector).
    private string _addressField;
    private string _portField;
    private string _status = "";

    // El recuadro estorba durante la partida: tapa una esquina y encima te obliga a
    // tener el mouse suelto. Con ESC se abre y se cierra, y mientras esta abierto pide
    // el cursor — asi se puede apretar Desconectar en cualquier momento.
    //
    // SIN conexion queda abierto siempre: es lo unico que hay en pantalla y sin el no
    // se puede ni hostear ni conectarse.
    private bool _panelOpen = true;
    private bool _wasConnected;

    private void OnDisable() => UICursor.Release(this);

    private void Update()
    {
        // Con el menú principal a la vista este recuadro no existe: ni se dibuja ni pide
        // el cursor (lo tiene el menú). Vuelve al apretar Jugar.
        if (UI_MainMenu.IsShowing)
        {
            UICursor.Release(this);
            return;
        }

        bool connected = Nm != null && (Nm.IsServerStarted || Nm.IsClientStarted);

        if (!connected)
        {
            // Sin conexion queda abierto siempre: es lo unico que hay en pantalla, y sin
            // el no se puede ni hostear ni conectarse.
            _panelOpen = true;
        }
        else
        {
            // Al conectarse se guarda solo: si no, se queda encima del panel de la sala,
            // que es justo la esquina donde va la IP.
            if (!_wasConnected) _panelOpen = false;

            // El panel de ajustes también se cierra con ESC: ese ESC no es para nosotros
            // (ni el frame en que se cerró, si no se abrirían y cerrarían a la vez).
            bool settingsAteEscape = UI_SettingsPanel.IsAnyOpen ||
                                     UI_SettingsPanel.LastCloseFrame == Time.frameCount;
            if (Input.GetKeyDown(KeyCode.Escape) && !settingsAteEscape) _panelOpen = !_panelOpen;
        }

        _wasConnected = connected;

        // Con el recuadro abierto el cursor va suelto, conectado o no. Antes se pedia
        // SOLO estando conectado, y al desconectarse pasaba esto: el recuadro volvia a
        // aparecer pero nadie tenia el cursor pedido, asi que se trababa al centro y no
        // habia forma de apretar Iniciar Host de nuevo.
        if (_panelOpen) UICursor.Request(this);
        else            UICursor.Release(this);
    }

    private NetworkManager Nm => InstanceFinder.NetworkManager;

    // =========================================================
    // ACCIONES
    // =========================================================

    private void StartHost()
    {
        if (Nm == null) { _status = "No hay NetworkManager en la escena."; return; }

        ushort port = ParsePort();
        Nm.TransportManager.Transport.SetPort(port);

        // Servidor + cliente local (host): el cliente se conecta a sí mismo.
        Nm.ServerManager.StartConnection();
        Nm.ClientManager.StartConnection("127.0.0.1", port);
        _status = "Host iniciado. Esperando a tus amigos...";
    }

    private void StartClient()
    {
        if (Nm == null) { _status = "No hay NetworkManager en la escena."; return; }

        ushort port    = ParsePort();
        string address = string.IsNullOrWhiteSpace(_addressField) ? "127.0.0.1" : _addressField.Trim();

        Nm.ClientManager.StartConnection(address, port);
        _status = $"Conectando a {address}:{port}...";
    }

    private void Disconnect()
    {
        if (Nm == null) return;
        if (Nm.IsServerStarted) Nm.ServerManager.StopConnection(true);
        if (Nm.IsClientStarted) Nm.ClientManager.StopConnection();
        _status = "Desconectado.";
    }

    private ushort ParsePort()
    {
        if (ushort.TryParse(_portField, out ushort p)) { Port = p; return p; }
        _portField = Port.ToString();
        return Port;
    }

    // =========================================================
    // UI
    // =========================================================

    private void OnGUI()
    {
        if (UI_MainMenu.IsShowing) return;

        // Escala la UI para que se vea bien en resoluciones altas.
        float scale = Mathf.Max(1f, Screen.height / 720f);
        Matrix4x4 prev = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

        const float pad = 10f;

        bool serverStarted = Nm != null && Nm.IsServerStarted;
        bool clientStarted = Nm != null && Nm.IsClientStarted;

        // Minimizado: solo un cartelito en la esquina que recuerda como volver.
        if (!_panelOpen)
        {
            GUILayout.BeginArea(new Rect(pad, pad, 190f, 30f), GUI.skin.box);
            GUILayout.Label("<b>ESC</b> — menú de red", RichLabel());
            GUILayout.EndArea();
            GUI.matrix = prev;
            return;
        }

        // El área es alta de sobra y el recuadro (BeginVertical con estilo box) se ajusta
        // solo a lo que contiene. Con un alto fijo, cada botón nuevo quedaba recortado
        // por debajo del borde sin ningún aviso.
        GUILayout.BeginArea(new Rect(pad, pad, 280f, 600f));
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("<b>Red — Prueba de conexión</b>", RichLabel());


        if (!serverStarted && !clientStarted)
        {
            GUILayout.Space(4);
            GUILayout.Label("IP del host:");
            _addressField = GUILayout.TextField(_addressField);
            GUILayout.Label("Puerto:");
            _portField = GUILayout.TextField(_portField);
            GUILayout.Space(6);

            // Botón de Host: solo en el editor (salvo que desactives HostOnlyInEditor).
            bool canHost = !HostOnlyInEditor || Application.isEditor;
            if (canHost && GUILayout.Button("Iniciar Host (solo yo)", GUILayout.Height(32)))
                StartHost();

            if (GUILayout.Button("Conectarse (Cliente)", GUILayout.Height(32)))
                StartClient();
        }
        else
        {
            string rol = serverStarted && clientStarted ? "HOST"
                       : serverStarted ? "SERVIDOR" : "CLIENTE";
            GUILayout.Label($"Estado: <b>{rol}</b>", RichLabel());
            GUILayout.Space(6);
            if (GUILayout.Button("Desconectar", GUILayout.Height(32)))
                Disconnect();

            GUILayout.Space(2);
            GUILayout.Label("<i>ESC oculta este recuadro</i>", RichLabel());
        }

        if (!string.IsNullOrEmpty(_status))
        {
            GUILayout.Space(4);
            GUILayout.Label(_status);
        }

        // Ajustes y volver al menú: siempre, conectado o no. Volver corta la conexión
        // primero y muestra el menú (ver UI_MainMenu.ReturnToMenu). Si el menú no está
        // instalado en la escena, el botón no aparece.
        GUILayout.Space(6);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Ajustes", GUILayout.Height(28)))
            UI_SettingsPanel.GetOrCreate().Open();
        if (UI_MainMenu.Instance != null &&
            GUILayout.Button("Menú principal", GUILayout.Height(28)))
            UI_MainMenu.ReturnToMenu();
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
        GUILayout.EndArea();
        GUI.matrix = prev;
    }

    private GUIStyle RichLabel()
    {
        return new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
    }
}
