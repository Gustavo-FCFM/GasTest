using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_MainMenu
//
// La pantalla de inicio: título, Jugar, Ajustes, Salir. NO es otra escena: es un panel
// sobre la arena, que se ve al fondo desde la cámara de la sala girando despacio y con
// blur (ver MenuOrbitCamera). Al apretar Jugar el panel se esconde y aparece la pantalla de
// conexión (UI_ConnectPanel), para crear la partida o unirse a una.
//
// POR QUÉ NO ES UNA ESCENA APARTE: el fondo que querías es la arena de verdad, y
// cargarla dos veces (una para el menú y otra para jugar) obligaba a pasar el
// NetworkManager de una escena a otra — un camino de FishNet sin probar. Así, no hay
// ninguna carga de escena: "volver al menú" es cortar la conexión y mostrar este panel,
// exactamente el mismo camino que Desconectar → Iniciar Host, que ya está probado.
//
// VIVE EN EL PREFAB DE MENÚS (Menus_UI ▸ MainMenu), desde el 14 de octubre de 2026: el velo,
// la franja, el título, el subtítulo, los tres botones y la versión están armados ahí y se
// acomodan a mano (los textos de los botones y el subtítulo llevan su LocalizedText). Este
// script solo los conecta y prende o apaga el canvas.
//
// Los textos salen de Loc (claves menu.*), salvo el título: es el nombre del juego.
//
// CON CONTROL (26 de octubre de 2026): es una pantalla de MenuInput. Al mostrarse elige Jugar;
// Start o ESC abren los Ajustes; B no hace nada (no hay a dónde volver).
// ============================================================
public class UI_MainMenu : MonoBehaviour, IMenuScreen
{
    [Header("Piezas")]
    [Tooltip("El canvas del menú: se prende y se apaga entero.")]
    [Label("Canvas del menú")]
    public GameObject MenuCanvas;

    [Label("Botón Jugar")]
    public Button PlayButton;

    [Label("Botón Ajustes")]
    public Button SettingsButton;

    [Label("Botón Salir")]
    public Button QuitButton;

    [Tooltip("La versión, abajo a la derecha (se escribe sola con la del proyecto).")]
    [Label("Versión")]
    public TMP_Text VersionText;

    [Header("Comportamiento")]
    [Tooltip("Mostrar el menú apenas arranca la escena. Apagalo para probar la arena directo.")]
    [Label("Mostrar al arrancar")]
    public bool ShowOnStart = true;

    public static UI_MainMenu Instance { get; private set; }

    // True mientras el menú está a la vista. Lo miran la pantalla de conexión (para no volver
    // a mostrarse encima), el recuadro de red de la escena de pruebas y la cámara de la sala
    // (para el blur).
    public static bool IsShowing { get; private set; }

    private void Awake()
    {
        Instance = this;

        if (PlayButton != null)     { PlayButton.onClick.AddListener(AudioManager.Click);     PlayButton.onClick.AddListener(Play); }
        if (SettingsButton != null) { SettingsButton.onClick.AddListener(AudioManager.Click); SettingsButton.onClick.AddListener(OpenSettings); }
        if (QuitButton != null)     { QuitButton.onClick.AddListener(AudioManager.Click);     QuitButton.onClick.AddListener(Quit); }
        if (VersionText != null) VersionText.text = "v" + Application.version;
    }

    private void Start()
    {
        if (ShowOnStart) Show(); else Hide();
    }

    // En el menú, ESC abre los Ajustes (y el panel de ajustes se cierra con ESC también). Eso
    // lo hace MenuInput (OnPause); esto queda solo para una escena sin el prefab de menús.
    private void Update()
    {
        if (MenuInput.Instance != null) return;
        if (!IsShowing || !Input.GetKeyDown(KeyCode.Escape)) return;
        if (UI_SettingsPanel.IsAnyOpen || UI_SettingsPanel.LastCloseFrame == Time.frameCount) return;

        OpenSettings();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (IsShowing) { IsShowing = false; MenuInput.Pop(this); UICursor.Release(this); }
    }

    private static void OpenSettings() => UI_SettingsPanel.Get()?.Open();

    // Jugar: la pantalla de conexión. Sin ella (una escena vieja) queda el recuadro de red.
    private void Play()
    {
        Hide();
        if (UI_ConnectPanel.Instance != null) UI_ConnectPanel.Instance.Open();
    }

    // =========================================================
    // MOSTRAR / ESCONDER
    // =========================================================

    public void Show()
    {
        if (MenuCanvas != null) MenuCanvas.SetActive(true);
        IsShowing = true;
        UICursor.Request(this);
        MenuInput.Push(this);   // elige Jugar
    }

    public void Hide()
    {
        if (MenuCanvas != null) MenuCanvas.SetActive(false);
        IsShowing = false;
        MenuInput.Pop(this);
        UICursor.Release(this);
    }

    // Corta la conexión (si la hay) y vuelve a mostrar el menú. Lo llama la pausa desde su
    // botón Salir (y el recuadro de red de la escena de pruebas).
    public static void ReturnToMenu()
    {
        StopNetworking();
        if (UI_ConnectPanel.Instance != null) UI_ConnectPanel.Instance.Hide();
        if (Instance != null) Instance.Show();
    }

    // Corta el servidor y el cliente, también si TODAVÍA SE ESTÁN CONECTANDO. Antes miraba
    // IsServerStarted / IsClientStarted, que recién dan true al terminar de conectar: un
    // intento a medias (a una dirección que no responde) seguía vivo en segundo plano, y al
    // crear la partida después se cerraba solo y la tiraba abajo.
    public static void StopNetworking()
    {
        NetworkManager nm = InstanceFinder.NetworkManager;
        if (nm == null) return;

        Transport t = nm.TransportManager.Transport;
        if (t.GetConnectionState(true)  != LocalConnectionState.Stopped) nm.ServerManager.StopConnection(true);
        if (t.GetConnectionState(false) != LocalConnectionState.Stopped) nm.ClientManager.StopConnection();
    }

    // Salir del juego. En el editor solo para el Play.
    public static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // =========================================================
    // IMenuScreen
    // =========================================================

    public int MenuLayer => 0;
    public GameObject DefaultSelection => PlayButton != null ? PlayButton.gameObject : null;
    public Transform SelectionRoot => MenuCanvas != null ? MenuCanvas.transform : transform;
    public void OnBack()  { }
    public void OnPause() => OpenSettings();
}
