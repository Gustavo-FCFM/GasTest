using FishNet;
using FishNet.Managing;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ============================================================
// UI_MainMenu
//
// La pantalla de inicio: título, Jugar, Ajustes, Salir. NO es otra escena: es un panel
// sobre la arena, que se ve al fondo desde la cámara de la sala girando despacio y con
// blur (ver MenuOrbitCamera). Al apretar Jugar el panel se esconde y queda lo de
// siempre: el recuadro de red para hostear o conectarse.
//
// POR QUÉ NO ES UNA ESCENA APARTE: el fondo que querías es la arena de verdad, y
// cargarla dos veces (una para el menú y otra para jugar) obligaba a pasar el
// NetworkManager de una escena a otra — un camino de FishNet sin probar. Así, no hay
// ninguna carga de escena: "volver al menú" es cortar la conexión y mostrar este panel,
// exactamente el mismo camino que Desconectar → Iniciar Host, que ya está probado.
//
// Se dibuja solo por código, como el resto de la UI del modo. Lo instala
// `Mercenarios ▸ Instalar el menú principal en la arena`.
// ============================================================
public class UI_MainMenu : MonoBehaviour
{
    [Header("Textos")]
    public string Title    = "MERCENARIES";
    public string Subtitle = "Demo · 3 contra 3 contra 3";

    [Header("Comportamiento")]
    [Tooltip("Mostrar el menú apenas arranca la escena. Apagalo para probar la arena directo.")]
    public bool ShowOnStart = true;

    [Header("Fondo")]
    [Tooltip("Velo oscuro sobre la arena para que el texto se lea. Transparente del todo si querés ver el mapa limpio.")]
    public Color BackdropColor = new Color(0f, 0f, 0f, 0.35f);
    public Color BandColor     = new Color(0f, 0f, 0f, 0.35f);

    [Header("Botones")]
    public Vector2 ButtonSize   = new Vector2(320f, 58f);
    public float   ButtonGap    = 16f;
    public Color   ButtonColor  = new Color(0.20f, 0.22f, 0.26f);
    public Color   PlayColor    = new Color(0.95f, 0.65f, 0.20f);
    public Color   TextColor    = new Color(0.94f, 0.94f, 0.94f);
    public Color   DimTextColor = new Color(0.6f, 0.6f, 0.6f);

    public static UI_MainMenu Instance { get; private set; }

    // True mientras el menú está a la vista. Lo miran el recuadro de red (para no
    // dibujarse encima) y la cámara de la sala (para el blur).
    public static bool IsShowing { get; private set; }

    private Canvas _canvas;
    private Button _playButton;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        Build();
        if (ShowOnStart) Show(); else Hide();
    }

    // En el menú, ESC abre los Ajustes (y el panel de ajustes se cierra con ESC también).
    private void Update()
    {
        if (!IsShowing || !Input.GetKeyDown(KeyCode.Escape)) return;
        if (UI_SettingsPanel.IsAnyOpen || UI_SettingsPanel.LastCloseFrame == Time.frameCount) return;

        UI_SettingsPanel.GetOrCreate().Open();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (IsShowing) { IsShowing = false; UICursor.Release(this); }
    }

    // =========================================================
    // MOSTRAR / ESCONDER
    // =========================================================

    public void Show()
    {
        if (_canvas == null) Build();

        _canvas.gameObject.SetActive(true);
        IsShowing = true;
        UICursor.Request(this);

        if (EventSystem.current != null && _playButton != null)
            EventSystem.current.SetSelectedGameObject(_playButton.gameObject);
    }

    public void Hide()
    {
        if (_canvas != null) _canvas.gameObject.SetActive(false);
        IsShowing = false;
        UICursor.Release(this);
    }

    // Corta la conexión (si la hay) y vuelve a mostrar el menú. Lo llama el recuadro de
    // red desde su botón "Menú principal".
    public static void ReturnToMenu()
    {
        StopNetworking();
        if (Instance != null) Instance.Show();
    }

    public static void StopNetworking()
    {
        NetworkManager nm = InstanceFinder.NetworkManager;
        if (nm == null) return;

        if (nm.IsServerStarted) nm.ServerManager.StopConnection(true);
        if (nm.IsClientStarted) nm.ClientManager.StopConnection();
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
    // CONSTRUCCIÓN
    // =========================================================

    private void Build()
    {
        _canvas = MercUIFactory.CreateCanvas("MainMenuCanvas", 100);
        _canvas.transform.SetParent(transform, false);
        Transform root = _canvas.transform;

        Vector2 center = new Vector2(0.5f, 0.5f);

        // Velo sobre la arena. Se traga los clics para que nada de abajo los reciba.
        Image backdrop = MercUIFactory.CreateImage(root, "Backdrop", BackdropColor,
                                                   Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one, center);
        backdrop.raycastTarget = true;

        // Banda un poco más oscura al centro, donde va el texto.
        MercUIFactory.CreateImage(root, "Band", BandColor,
                                  Vector2.zero, Vector2.zero,
                                  new Vector2(0.3f, 0f), new Vector2(0.7f, 1f), center);

        TextMeshProUGUI title = MercUIFactory.CreateText(root, "Title", Title, 92f, TextColor,
                                                         TextAlignmentOptions.Center,
                                                         new Vector2(0f, 200f), new Vector2(1200f, 120f),
                                                         center, center, center);
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 12f;
        MercUIFactory.AddShadow(title, 3f);

        MercUIFactory.CreateText(root, "Subtitle", Subtitle, 24f, DimTextColor,
                                 TextAlignmentOptions.Center,
                                 new Vector2(0f, 128f), new Vector2(900f, 34f),
                                 center, center, center);

        float y = 10f;
        _playButton = MakeButton(root, "Play", "Jugar", PlayColor, new Vector2(0f, y));
        _playButton.onClick.AddListener(Hide);

        y -= ButtonSize.y + ButtonGap;
        Button settings = MakeButton(root, "Settings", "Ajustes", ButtonColor, new Vector2(0f, y));
        settings.onClick.AddListener(() => UI_SettingsPanel.GetOrCreate().Open());

        y -= ButtonSize.y + ButtonGap;
        Button quit = MakeButton(root, "Quit", "Salir", ButtonColor, new Vector2(0f, y));
        quit.onClick.AddListener(Quit);

        Vector2 corner = new Vector2(1f, 0f);
        MercUIFactory.CreateText(root, "Version", "v" + Application.version, 15f, DimTextColor,
                                 TextAlignmentOptions.Right,
                                 new Vector2(-16f, 10f), new Vector2(300f, 20f),
                                 corner, corner, corner);
    }

    private Button MakeButton(Transform parent, string name, string label, Color color, Vector2 position)
    {
        Vector2 center = new Vector2(0.5f, 0.5f);
        RectTransform rect = MercUIFactory.CreateRect(parent, name, center, center, center, position, ButtonSize);

        Image bg = rect.gameObject.AddComponent<Image>();
        bg.sprite = MercUIFactory.WhiteSprite;
        bg.color  = color;

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;

        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.25f);
        colors.selectedColor    = colors.highlightedColor;
        colors.pressedColor     = Color.Lerp(color, Color.black, 0.25f);
        button.colors = colors;

        TextMeshProUGUI text = MercUIFactory.CreateText(rect, "Text", label, 24f, Color.white,
                                                        TextAlignmentOptions.Center,
                                                        Vector2.zero, new Vector2(ButtonSize.x - 8f, ButtonSize.y - 6f),
                                                        center, center, center);
        text.fontStyle = FontStyles.Bold;
        return button;
    }
}
