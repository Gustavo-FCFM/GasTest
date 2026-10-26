using FishNet;
using FishNet.Managing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_PauseMenu
//
// El menú de la partida: lo abren ESC o Start (ver MenuInput) en la sala o jugando.
// Continuar, Ajustes, Desatascarse y Salir al menú principal. La partida NO se detiene: es
// en red, los demás siguen jugando.
//
// Reemplaza al recuadro de red de la esquina (ConnectionHUD), que se dibujaba aparte y no
// se podía usar con control (26 de octubre de 2026, para el showcase).
//
// DESATASCARSE vive acá a propósito: este menú lee su propia copia del input (la de
// MenuInput), así que se abre aunque el mapa de input del jugador esté apagado — que es uno
// de los estados de los que hay que poder salir.
//
// VIVE EN EL PREFAB DE MENÚS (Menus_UI ▸ Pause): el velo, el panel, el título, la línea de
// estado y los botones están armados ahí y se acomodan a mano. Este script los conecta.
// ============================================================
public class UI_PauseMenu : MonoBehaviour, IMenuScreen
{
    [Header("Piezas")]
    [Tooltip("El canvas de la pausa: se prende al abrir y se apaga al cerrar.")]
    [Label("Canvas de la pausa")]
    public GameObject PauseCanvas;

    [Label("Botón Continuar")]
    public Button ResumeButton;

    [Label("Botón Ajustes")]
    public Button SettingsButton;

    [Tooltip("Solo se ve con un personaje en juego.")]
    [Label("Botón Desatascarse")]
    public Button UnstuckButton;

    [Label("Botón Salir")]
    public Button LeaveButton;

    [Tooltip("El texto del botón Salir: cambia si eres el host (salir cierra la partida para todos).")]
    [Label("Texto de Salir")]
    public TMP_Text LeaveLabel;

    [Tooltip("Si eres host o cliente, y a qué dirección se conectan los demás.")]
    [Label("Línea de estado")]
    public TMP_Text StatusText;

    [Header("Salir siendo host")]
    [Tooltip("Siendo host, Salir se aprieta dos veces: la primera pregunta \"¿Seguro?\". Lo que espera la segunda antes de volver a decir \"Cerrar la partida y salir\".")]
    [Label("Tiempo para confirmar (s)")]
    public float LeaveConfirmSeconds = 4f;

    public static bool IsOpen { get; private set; }

    private bool  _open;
    private float _refreshIn;
    private readonly LeaveConfirm _leaveConfirm = new LeaveConfirm();

    // =========================================================
    // ACCESO
    // =========================================================

    // La de la escena (el prefab de menús). Null si no hay.
    public static UI_PauseMenu Get() => FindFirstObjectByType<UI_PauseMenu>(FindObjectsInactive.Include);

    private static NetworkManager Nm => InstanceFinder.NetworkManager;
    private static bool Connected => Nm != null && (Nm.IsClientStarted || Nm.IsServerStarted);

    private void Awake()
    {
        if (PauseCanvas != null) PauseCanvas.SetActive(false);

        if (ResumeButton != null)   { ResumeButton.onClick.AddListener(AudioManager.Click);   ResumeButton.onClick.AddListener(Close); }
        if (SettingsButton != null) { SettingsButton.onClick.AddListener(AudioManager.Click); SettingsButton.onClick.AddListener(() => UI_SettingsPanel.Get()?.Open()); }
        if (UnstuckButton != null)  { UnstuckButton.onClick.AddListener(AudioManager.Click);  UnstuckButton.onClick.AddListener(Unstuck); }
        if (LeaveButton != null)    { LeaveButton.onClick.AddListener(AudioManager.Click);    LeaveButton.onClick.AddListener(Leave); }
    }

    private void OnEnable()  => Loc.OnLanguageChanged += Refresh;

    private void OnDisable()
    {
        Loc.OnLanguageChanged -= Refresh;
        if (_open) Close();
    }

    // =========================================================
    // ABRIR / CERRAR
    // =========================================================

    // Solo con conexión: en el menú principal o en la de conexión no hay partida que pausar.
    public void Open()
    {
        if (_open || !Connected) return;

        _open = true;
        IsOpen = true;
        if (PauseCanvas != null) PauseCanvas.SetActive(true);
        Refresh();

        UICursor.Request(this);
        MenuInput.Push(this);
    }

    public void Close()
    {
        if (!_open) return;

        _open = false;
        IsOpen = false;
        _leaveConfirm.Cancel();
        if (PauseCanvas != null) PauseCanvas.SetActive(false);

        MenuInput.Pop(this);
        UICursor.Release(this);
    }

    private void Update()
    {
        if (!_open) return;

        // Se cortó la conexión con el menú abierto: no hay nada que continuar.
        if (!Connected) { Close(); return; }

        if (_leaveConfirm.Changed(LeaveButton != null ? LeaveButton.gameObject : null)) Refresh();

        // Lo que cambia solo (aparecer el personaje, la IP) se repasa despacio.
        _refreshIn -= Time.unscaledDeltaTime;
        if (_refreshIn <= 0f) Refresh();
    }

    private void Refresh()
    {
        _refreshIn = 0.5f;
        if (!_open) return;

        bool host = Nm != null && Nm.IsServerStarted;

        if (UnstuckButton != null)
        {
            bool show = PlayerController.LocalPlayer != null;
            if (UnstuckButton.gameObject.activeSelf != show) UnstuckButton.gameObject.SetActive(show);
        }

        if (LeaveLabel != null)
            LeaveLabel.text = Loc.T(!host ? "pause.leave" : _leaveConfirm.Asking ? "pause.leave_confirm" : "pause.leave_host");

        if (StatusText != null)
            StatusText.text = host
                ? Loc.T("pause.status_host", ("ip", NetAddress.LanAddress()))
                : Loc.T("pause.status_client", ("address", string.IsNullOrEmpty(NetAddress.Joined) ? "—" : NetAddress.Joined));
    }

    // =========================================================
    // BOTONES
    // =========================================================

    private void Unstuck()
    {
        if (PlayerController.LocalPlayer != null) PlayerController.LocalPlayer.RequestUnstuck();
        Close();
    }

    // Volver al menú principal corta la conexión (ver UI_MainMenu.ReturnToMenu). Siendo host,
    // eso cierra la partida para todos: por eso el botón lo dice, y pregunta antes (LeaveConfirm).
    private void Leave()
    {
        bool host = Nm != null && Nm.IsServerStarted;
        if (!_leaveConfirm.Press(host, LeaveConfirmSeconds)) { Refresh(); return; }

        Close();
        UI_MainMenu.ReturnToMenu();
    }

    // =========================================================
    // IMenuScreen
    // =========================================================

    public int MenuLayer => 1;
    public GameObject DefaultSelection => ResumeButton != null ? ResumeButton.gameObject : null;
    public Transform SelectionRoot => PauseCanvas != null ? PauseCanvas.transform : transform;
    public void OnBack()  => Close();
    public void OnPause() => Close();
}
