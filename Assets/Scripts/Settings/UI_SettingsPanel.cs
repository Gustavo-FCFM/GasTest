using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ============================================================
// UI_SettingsPanel
//
// El panel de Ajustes. Se dibuja solo por código (con MercUIFactory, igual que la sala
// y el HUD de partida), así que alcanza con que el componente exista: no hay prefab
// que cablear ni referencias que se rompan. Sirve igual en el menú principal y dentro
// de la partida (ESC ▸ Ajustes en el recuadro de red).
//
// CÓMO SE USA DESDE OTRO SCRIPT:
//     UI_SettingsPanel.GetOrCreate().Open();
// Si hay uno en la escena lo usa (por ejemplo un prefab con los colores cambiados en el
// Inspector); si no, lo crea en el momento.
//
// Los valores viven en GameSettings (estático, PlayerPrefs): el panel solo los muestra
// y los escribe. Cada cambio se aplica al instante y se guarda al cerrar.
//
// Mientras está abierto pide el cursor a UICursor y bloquea el input de juego. ESC lo
// cierra; ConnectionHUD sabe no reaccionar a ese mismo ESC (ver LastCloseFrame).
// ============================================================
public class UI_SettingsPanel : MonoBehaviour
{
    [Header("Medidas")]
    [Tooltip("Ancho del panel. El alto se calcula solo según las filas.")]
    public float   PanelWidth  = 760f;
    [Tooltip("Espacio bajo la última fila para los botones y la ayuda.")]
    public float   FooterHeight = 110f;
    public float   RowHeight   = 44f;
    public float   LabelWidth  = 260f;
    public float   SectionGap  = 18f;

    [Header("Colores")]
    public Color BackdropColor = new Color(0f, 0f, 0f, 0.8f);
    public Color PanelColor    = new Color(0.10f, 0.11f, 0.13f, 0.97f);
    public Color RowColor      = new Color(1f, 1f, 1f, 0.04f);
    public Color AccentColor   = new Color(0.95f, 0.65f, 0.20f);
    public Color ControlColor  = new Color(0.20f, 0.22f, 0.26f);
    public Color TextColor     = new Color(0.92f, 0.92f, 0.92f);
    public Color DimTextColor  = new Color(0.65f, 0.65f, 0.65f);

    // True mientras algún panel de ajustes esté abierto (hay uno por escena como mucho).
    public static bool IsAnyOpen { get; private set; }

    // Frame en que se cerró el último panel. El recuadro de red también escucha ESC y
    // sin esto se abría en el mismo frame en que este se cerraba.
    public static int LastCloseFrame { get; private set; } = -1;

    public bool IsOpen => _open;

    private bool _open;
    private Canvas _canvas;
    private GameObject _root;
    private GameObject _firstControl;

    // Controles que hay que refrescar cuando cambia un valor desde afuera (Restablecer).
    private readonly List<Action> _refreshers = new List<Action>();

    // =========================================================
    // ACCESO
    // =========================================================

    public static UI_SettingsPanel GetOrCreate()
    {
        UI_SettingsPanel existing = FindFirstObjectByType<UI_SettingsPanel>(FindObjectsInactive.Include);
        if (existing != null) return existing;

        GameObject go = new GameObject("UI_SettingsPanel");
        return go.AddComponent<UI_SettingsPanel>();
    }

    public void Open()
    {
        if (_open) return;

        if (_canvas == null) Build();
        EnsureEventSystem();

        RefreshAll();
        _canvas.gameObject.SetActive(true);
        _open = true;
        IsAnyOpen = true;

        UICursor.Request(this);
        GameSettings.OnChanged += RefreshAll;

        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_firstControl);
    }

    public void Close()
    {
        if (!_open) return;

        _open = false;
        IsAnyOpen = false;
        LastCloseFrame = Time.frameCount;

        GameSettings.OnChanged -= RefreshAll;
        GameSettings.Save();

        if (_canvas != null) _canvas.gameObject.SetActive(false);
        UICursor.Release(this);
    }

    public void Toggle()
    {
        if (_open) Close(); else Open();
    }

    private void OnDisable()
    {
        if (_open) Close();
    }

    private void Update()
    {
        if (_open && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    // =========================================================
    // CONSTRUCCIÓN
    // =========================================================

    private void Build()
    {
        _canvas = MercUIFactory.CreateCanvas("SettingsCanvas", 300);
        _canvas.transform.SetParent(transform, false);

        // Fondo oscuro que tapa todo y se traga los clics (no se cierra al clickear afuera:
        // un clic perdido en la partida no tiene que soltar el panel).
        Image backdrop = MercUIFactory.CreateImage(_canvas.transform, "Backdrop", BackdropColor,
                                                   Vector2.zero, Vector2.zero,
                                                   Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        backdrop.raycastTarget = true;

        Image panel = MercUIFactory.CreateImage(_canvas.transform, "Panel", PanelColor,
                                                Vector2.zero, new Vector2(PanelWidth, 100f),
                                                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.raycastTarget = true;
        _root = panel.gameObject;

        Vector2 top = new Vector2(0.5f, 1f);
        TextMeshProUGUI title = MercUIFactory.CreateText(_root.transform, "Title", "AJUSTES", 34f, TextColor,
                                                         TextAlignmentOptions.Center,
                                                         new Vector2(0f, -36f), new Vector2(PanelWidth, 44f),
                                                         top, top, top);
        title.fontStyle = FontStyles.Bold;

        float y = -90f;

        // ---- Cámara ----
        Section(ref y, "Cámara");
        SliderRow(ref y, "Sensibilidad del mouse",
                  GameSettings.MinSensitivity, GameSettings.MaxSensitivity,
                  () => GameSettings.MouseSensitivity, v => GameSettings.MouseSensitivity = v,
                  v => $"{v:0.0}×", first: true);
        ToggleRow(ref y, "Invertir eje Y", () => GameSettings.InvertY, v => GameSettings.InvertY = v);

        // ---- Sonido ----
        Section(ref y, "Sonido");
        SliderRow(ref y, "Volumen general", 0f, 1f,
                  () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v, Percent);
        SliderRow(ref y, "Música", 0f, 1f,
                  () => GameSettings.MusicVolume, v => GameSettings.MusicVolume = v, Percent);
        SliderRow(ref y, "Efectos", 0f, 1f,
                  () => GameSettings.SfxVolume, v => GameSettings.SfxVolume = v, Percent);

        // ---- Pantalla ----
        Section(ref y, "Pantalla");
        CyclerRow(ref y, "Modo", ScreenModeNames.Length,
                  () => Array.IndexOf(ScreenModes, GameSettings.ScreenMode),
                  i => GameSettings.ScreenMode = ScreenModes[i],
                  i => ScreenModeNames[i]);
        CyclerRow(ref y, "Resolución", GameSettings.Resolutions.Length,
                  () => GameSettings.ResolutionIndex < 0 ? GameSettings.CurrentResolutionIndex() : GameSettings.ResolutionIndex,
                  i => GameSettings.ResolutionIndex = i,
                  i => $"{GameSettings.Resolutions[i].width} × {GameSettings.Resolutions[i].height}");
        CyclerRow(ref y, "Calidad", QualitySettings.names.Length,
                  () => GameSettings.QualityLevel,
                  i => GameSettings.QualityLevel = i,
                  i => QualitySettings.names[i]);
        ToggleRow(ref y, "Sincronía vertical (VSync)", () => GameSettings.VSync, v => GameSettings.VSync = v);

        // El alto lo dicta el contenido: las filas de arriba más el pie con los botones.
        // Con un alto fijo, agregar una fila corría los botones encima de las últimas.
        panel.rectTransform.sizeDelta = new Vector2(PanelWidth, -y + FooterHeight);

        // ---- Botones ----
        Vector2 bottom = new Vector2(0.5f, 0f);
        Button reset = MakeButton(_root.transform, "Reset", "Restablecer", ControlColor,
                                  bottom, new Vector2(-110f, 36f), new Vector2(190f, 44f));
        reset.onClick.AddListener(() => { GameSettings.ResetToDefaults(); RefreshAll(); });

        Button close = MakeButton(_root.transform, "Close", "Cerrar", AccentColor,
                                  bottom, new Vector2(110f, 36f), new Vector2(190f, 44f));
        close.onClick.AddListener(Close);

        TextMeshProUGUI hint = MercUIFactory.CreateText(_root.transform, "Hint",
                                                        "ESC también cierra · los cambios se aplican al momento",
                                                        15f, DimTextColor, TextAlignmentOptions.Center,
                                                        new Vector2(0f, 8f), new Vector2(PanelWidth, 20f),
                                                        bottom, bottom, bottom);

        _canvas.gameObject.SetActive(false);
    }

    private static readonly FullScreenMode[] ScreenModes =
        { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };
    private static readonly string[] ScreenModeNames =
        { "Pantalla completa", "Ventana sin bordes", "Ventana" };

    private static string Percent(float v) => $"{Mathf.RoundToInt(v * 100f)} %";

    // ---------- filas ----------

    private void Section(ref float y, string name)
    {
        y -= SectionGap;
        Vector2 top = new Vector2(0f, 1f);
        TextMeshProUGUI t = MercUIFactory.CreateText(_root.transform, "Section_" + name, name.ToUpperInvariant(),
                                                     16f, AccentColor, TextAlignmentOptions.Left,
                                                     new Vector2(40f, y), new Vector2(PanelWidth - 80f, 24f),
                                                     top, top, top);
        t.fontStyle = FontStyles.Bold;
        y -= 28f;
    }

    // Una fila: fondo tenue, etiqueta a la izquierda, y devuelve el rect donde va el control.
    private RectTransform Row(ref float y, string label)
    {
        Vector2 top = new Vector2(0f, 1f);
        float width = PanelWidth - 80f;

        Image bg = MercUIFactory.CreateImage(_root.transform, "Row_" + label, RowColor,
                                             new Vector2(40f, y), new Vector2(width, RowHeight),
                                             top, top, top);

        MercUIFactory.CreateText(bg.transform, "Label", label, 19f, TextColor, TextAlignmentOptions.Left,
                                 new Vector2(14f, 0f), new Vector2(LabelWidth, RowHeight));

        RectTransform control = MercUIFactory.CreateRect(bg.transform, "Control",
                                                         new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                                                         new Vector2(-14f, 0f), new Vector2(width - LabelWidth - 40f, RowHeight - 10f));
        y -= RowHeight + 4f;
        return control;
    }

    private void SliderRow(ref float y, string label, float min, float max,
                           Func<float> get, Action<float> set, Func<float, string> format, bool first = false)
    {
        RectTransform control = Row(ref y, label);
        float w = control.sizeDelta.x;

        TextMeshProUGUI value = MercUIFactory.CreateText(control, "Value", "", 18f, TextColor, TextAlignmentOptions.Right,
                                                         Vector2.zero, new Vector2(80f, RowHeight),
                                                         new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));

        Slider slider = MakeSlider(control, new Vector2(0f, 0f), new Vector2(w - 96f, 24f), min, max);

        bool refreshing = false;
        slider.onValueChanged.AddListener(v =>
        {
            if (refreshing) return;
            set(v);
            value.text = format(get());
        });

        _refreshers.Add(() =>
        {
            refreshing = true;
            slider.SetValueWithoutNotify(get());
            value.text = format(get());
            refreshing = false;
        });

        if (first) _firstControl = slider.gameObject;
    }

    // Casilla para marcar y desmarcar (invertir Y, VSync). Es un Toggle de uGUI armado a
    // mano: el cuadrado es el fondo y el cuadradito de color adentro es la "palomita" —
    // un cuadrado relleno y no el glifo ✓, que la fuente por defecto no tiene.
    private void ToggleRow(ref float y, string label, Func<bool> get, Action<bool> set)
    {
        RectTransform control = Row(ref y, label);
        float h = RowHeight - 14f;

        Vector2 right = new Vector2(1f, 0.5f);
        Image box = MercUIFactory.CreateImage(control, "Box", ControlColor,
                                              Vector2.zero, new Vector2(h, h), right, right, right);
        box.raycastTarget = true;

        Image mark = MercUIFactory.CreateImage(box.transform, "Mark", AccentColor,
                                               Vector2.zero, new Vector2(h - 10f, h - 10f),
                                               new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        Toggle toggle = box.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = box;
        toggle.graphic       = mark;

        ColorBlock colors = toggle.colors;
        colors.highlightedColor = Color.Lerp(ControlColor, Color.white, 0.25f);
        colors.selectedColor    = colors.highlightedColor;
        toggle.colors = colors;

        toggle.onValueChanged.AddListener(v => set(v));

        _refreshers.Add(() => toggle.SetIsOnWithoutNotify(get()));
    }

    // < valor > — para todo lo que es una lista corta (modo de pantalla, resolución,
    // calidad, sí/no). Anda con mouse y con control sin necesitar un dropdown.
    private void CyclerRow(ref float y, string label, int count,
                           Func<int> get, Action<int> set, Func<int, string> name)
    {
        RectTransform control = Row(ref y, label);
        float w = control.sizeDelta.x;
        float h = RowHeight - 10f;

        Button prev = MakeButton(control, "Prev", "<", ControlColor,
                                 new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(h, h));
        Button next = MakeButton(control, "Next", ">", ControlColor,
                                 new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(h, h));

        // Sin rich text: TextMeshPro intentaría leer el "<" como el inicio de una etiqueta.
        prev.GetComponentInChildren<TextMeshProUGUI>().richText = false;
        next.GetComponentInChildren<TextMeshProUGUI>().richText = false;

        TextMeshProUGUI value = MercUIFactory.CreateText(control, "Value", "", 18f, TextColor, TextAlignmentOptions.Center,
                                                         Vector2.zero, new Vector2(w - h * 2f - 8f, h),
                                                         new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        void Step(int dir)
        {
            if (count <= 0) return;
            int i = ((Mathf.Max(get(), 0) + dir) % count + count) % count;
            set(i);
            value.text = name(i);
        }

        prev.onClick.AddListener(() => Step(-1));
        next.onClick.AddListener(() => Step(+1));

        _refreshers.Add(() =>
        {
            int i = Mathf.Clamp(get(), 0, Mathf.Max(count - 1, 0));
            value.text = count > 0 ? name(i) : "—";
        });
    }

    private void RefreshAll()
    {
        foreach (Action r in _refreshers) r();
    }

    // ---------- widgets ----------

    private Button MakeButton(Transform parent, string name, string label, Color color,
                              Vector2 anchor, Vector2 position, Vector2 size)
    {
        RectTransform rect = MercUIFactory.CreateRect(parent, name, anchor, anchor, anchor, position, size);

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

        MercUIFactory.CreateText(rect, "Text", label, 18f, Color.white, TextAlignmentOptions.Center,
                                 Vector2.zero, new Vector2(size.x - 8f, size.y - 6f),
                                 new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        return button;
    }

    // Un Slider de uGUI armado a mano: fondo, relleno y manija. Anclado a la izquierda
    // del rect del control.
    private Slider MakeSlider(Transform parent, Vector2 position, Vector2 size, float min, float max)
    {
        Vector2 left = new Vector2(0f, 0.5f);
        RectTransform rect = MercUIFactory.CreateRect(parent, "Slider", left, left, left, position, size);

        Image track = MercUIFactory.CreateImage(rect, "Track", ControlColor,
                                                Vector2.zero, new Vector2(0f, 8f),
                                                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f));

        RectTransform fillArea = MercUIFactory.CreateRect(rect, "FillArea",
                                                          new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                                                          Vector2.zero, new Vector2(-12f, 8f));
        Image fill = MercUIFactory.CreateImage(fillArea, "Fill", AccentColor,
                                               Vector2.zero, new Vector2(12f, 0f),
                                               Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));

        RectTransform handleArea = MercUIFactory.CreateRect(rect, "HandleArea",
                                                            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                                                            Vector2.zero, new Vector2(-12f, 0f));
        Image handle = MercUIFactory.CreateImage(handleArea, "Handle", Color.white,
                                                 Vector2.zero, new Vector2(12f, 0f),
                                                 new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f));
        handle.raycastTarget = true;

        Slider slider = rect.gameObject.AddComponent<Slider>();
        slider.fillRect      = fill.rectTransform;
        slider.handleRect    = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction     = Slider.Direction.LeftToRight;
        slider.minValue      = min;
        slider.maxValue      = max;

        ColorBlock colors = slider.colors;
        colors.highlightedColor = AccentColor;
        colors.selectedColor    = AccentColor;
        slider.colors = colors;

        // Toda la fila del slider recibe el clic, no solo la manija.
        Image hit = rect.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        hit.raycastTarget = true;

        return slider;
    }

    // Sin EventSystem no funciona ningún clic. El menú principal lo trae; en la partida
    // ya hay uno, pero por las dudas.
    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }
}
