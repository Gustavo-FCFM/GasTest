using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ============================================================
// UI_SettingsPanel
//
// El panel de Ajustes. Sirve igual en el menú principal y dentro de la partida (ESC o Start ▸
// Ajustes en la pausa).
//
// VIVE EN EL PREFAB DE MENÚS (Menus_UI ▸ Settings), desde el 14 de octubre de 2026: el velo,
// el panel, el título, las secciones, las filas (cada una con su UI_SettingRow, que dice
// qué ajuste maneja), los botones y la ayuda están armados ahí y se acomodan a mano. Este
// script conecta cada fila con su valor de GameSettings y abre o cierra el canvas.
//
// CÓMO SE USA DESDE OTRO SCRIPT:
//     UI_SettingsPanel.Get()?.Open();
//
// Los valores viven en GameSettings (estático, PlayerPrefs): el panel solo los muestra
// y los escribe. Cada cambio se aplica al instante y se guarda al cerrar.
//
// Mientras está abierto pide el cursor a UICursor y bloquea el input de juego. B, ESC o Start
// lo cierran: es una pantalla de MenuInput (desde el 26 de octubre de 2026). El recuadro de
// red de la escena de pruebas sabe no reaccionar a ese mismo ESC (ver LastCloseFrame).
//
// IDIOMA: es la primera fila (cualquiera tiene que poder encontrarla sin entender el
// idioma de ahora: dice "Idioma · Language" en los dos). Los textos fijos del panel están
// atados con LocalizedText y se reescriben solos; los valores de las filas se recalculan
// con RefreshAll, que corre con cada cambio de GameSettings.
// ============================================================
public class UI_SettingsPanel : MonoBehaviour, IMenuScreen
{
    [Header("Piezas")]
    [Tooltip("El canvas del panel: se prende al abrir y se apaga al cerrar.")]
    [Label("Canvas de los ajustes")]
    public GameObject PanelCanvas;

    [Label("Botón Restablecer")]
    public Button ResetButton;

    [Label("Botón Cerrar")]
    public Button CloseButton;

    // True mientras algún panel de ajustes esté abierto (hay uno por escena como mucho).
    public static bool IsAnyOpen { get; private set; }

    // Frame en que se cerró el último panel. El recuadro de red también escucha ESC y
    // sin esto se abría en el mismo frame en que este se cerraba.
    public static int LastCloseFrame { get; private set; } = -1;

    public bool IsOpen => _open;

    private bool _open;
    private bool _bound;
    private GameObject _firstControl;

    // Controles que hay que refrescar cuando cambia un valor desde afuera (Restablecer).
    private readonly List<Action> _refreshers = new List<Action>();

    // =========================================================
    // ACCESO
    // =========================================================

    private static bool _warnedMissing;

    // El de la escena (el prefab de menús). Null si no hay: avisa una vez.
    public static UI_SettingsPanel Get()
    {
        UI_SettingsPanel existing = FindFirstObjectByType<UI_SettingsPanel>(FindObjectsInactive.Include);
        if (existing == null && !_warnedMissing)
        {
            _warnedMissing = true;
            Debug.LogWarning("[UI_SettingsPanel] La escena no tiene el prefab de menús (Menus_UI): no hay panel de Ajustes.");
        }
        return existing;
    }

    private void Awake()
    {
        if (PanelCanvas != null) PanelCanvas.SetActive(false);
    }

    public void Open()
    {
        if (_open) return;

        if (!_bound) BindAll();
        EnsureEventSystem();

        RefreshAll();
        if (PanelCanvas != null) PanelCanvas.SetActive(true);
        _open = true;
        IsAnyOpen = true;

        UICursor.Request(this);
        GameSettings.OnChanged += RefreshAll;
        MenuInput.Push(this);   // elige el primer control
    }

    public void Close()
    {
        if (!_open) return;

        _open = false;
        IsAnyOpen = false;
        LastCloseFrame = Time.frameCount;

        GameSettings.OnChanged -= RefreshAll;
        GameSettings.Save();

        if (PanelCanvas != null) PanelCanvas.SetActive(false);
        MenuInput.Pop(this);
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

    // Con el prefab de menús lo cierra MenuInput (OnBack). Esto queda para una escena sin él.
    private void Update()
    {
        if (_open && MenuInput.Instance == null && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    // =========================================================
    // CONECTAR LAS FILAS (una vez, al abrir por primera vez)
    // =========================================================

    private void BindAll()
    {
        _bound = true;

        if (ResetButton != null)
        {
            ResetButton.onClick.AddListener(AudioManager.Click);
            ResetButton.onClick.AddListener(() => { GameSettings.ResetToDefaults(); RefreshAll(); });
        }
        if (CloseButton != null)
        {
            CloseButton.onClick.AddListener(AudioManager.Click);
            CloseButton.onClick.AddListener(Close);
        }

        GameObject root = PanelCanvas != null ? PanelCanvas : gameObject;
        foreach (UI_SettingRow row in root.GetComponentsInChildren<UI_SettingRow>(true))
            Bind(row);
    }

    private static readonly EInputScheme[] InputSchemes =
        { EInputScheme.KeyboardMouse, EInputScheme.Xbox, EInputScheme.PlayStation };

    private static readonly FullScreenMode[] ScreenModes =
        { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };
    private static readonly string[] ScreenModeKeys =
        { "screen.fullscreen", "screen.borderless", "screen.windowed" };

    private static string Percent(float v) => $"{Mathf.RoundToInt(v * 100f)} %";

    private void Bind(UI_SettingRow row)
    {
        switch (row.Setting)
        {
            case ESettingKind.Language:
                BindCycler(row, Loc.Languages.Length,
                           () => Array.IndexOf(Loc.Languages, GameSettings.Language),
                           i => GameSettings.Language = Loc.Languages[i],
                           i => Loc.LanguageName(Loc.Languages[i]));
                break;

            case ESettingKind.InputScheme:
                BindCycler(row, InputSchemes.Length,
                           () => Array.IndexOf(InputSchemes, GameSettings.InputScheme),
                           i => GameSettings.InputScheme = InputSchemes[i],
                           i => InputGlyphs.SchemeName(InputSchemes[i]));
                break;

            case ESettingKind.Sensitivity:
                BindSlider(row, GameSettings.MinSensitivity, GameSettings.MaxSensitivity,
                           () => GameSettings.MouseSensitivity, v => GameSettings.MouseSensitivity = v,
                           v => $"{v:0.0}×");
                break;

            case ESettingKind.InvertY:
                BindToggle(row, () => GameSettings.InvertY, v => GameSettings.InvertY = v);
                break;

            case ESettingKind.MasterVolume:
                BindSlider(row, 0f, 1f, () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v, Percent);
                break;
            case ESettingKind.MusicVolume:
                BindSlider(row, 0f, 1f, () => GameSettings.MusicVolume, v => GameSettings.MusicVolume = v, Percent);
                break;
            case ESettingKind.EffectsVolume:
                BindSlider(row, 0f, 1f, () => GameSettings.SfxVolume, v => GameSettings.SfxVolume = v, Percent);
                break;

            case ESettingKind.ScreenMode:
                BindCycler(row, ScreenModeKeys.Length,
                           () => Array.IndexOf(ScreenModes, GameSettings.ScreenMode),
                           i => GameSettings.ScreenMode = ScreenModes[i],
                           i => Loc.T(ScreenModeKeys[i]));
                break;

            case ESettingKind.Resolution:
                BindCycler(row, GameSettings.Resolutions.Length,
                           () => GameSettings.ResolutionIndex < 0 ? GameSettings.CurrentResolutionIndex() : GameSettings.ResolutionIndex,
                           i => GameSettings.ResolutionIndex = i,
                           i => $"{GameSettings.Resolutions[i].width} × {GameSettings.Resolutions[i].height}");
                break;

            case ESettingKind.VSync:
                BindToggle(row, () => GameSettings.VSync, v => GameSettings.VSync = v);
                break;
        }
    }

    private void BindSlider(UI_SettingRow row, float min, float max,
                            Func<float> get, Action<float> set, Func<float, string> format)
    {
        Slider slider = row.Slider;
        if (slider == null) { Debug.LogWarning($"[Ajustes] La fila '{row.name}' ({row.Setting}) no tiene barra."); return; }

        slider.minValue = min;
        slider.maxValue = max;

        bool refreshing = false;
        slider.onValueChanged.AddListener(v =>
        {
            if (refreshing) return;
            set(v);
            if (row.Value != null) row.Value.text = format(get());
        });

        _refreshers.Add(() =>
        {
            refreshing = true;
            slider.SetValueWithoutNotify(get());
            if (row.Value != null) row.Value.text = format(get());
            refreshing = false;
        });

        // El primer control que se selecciona al abrir (con el mando): la sensibilidad.
        if (_firstControl == null) _firstControl = slider.gameObject;
    }

    private void BindToggle(UI_SettingRow row, Func<bool> get, Action<bool> set)
    {
        Toggle toggle = row.Toggle;
        if (toggle == null) { Debug.LogWarning($"[Ajustes] La fila '{row.name}' ({row.Setting}) no tiene casilla."); return; }

        toggle.onValueChanged.AddListener(v => set(v));
        _refreshers.Add(() => toggle.SetIsOnWithoutNotify(get()));
    }

    // < valor > — para todo lo que es una lista corta. Anda con mouse y con control sin
    // necesitar un dropdown.
    private void BindCycler(UI_SettingRow row, int count, Func<int> get, Action<int> set, Func<int, string> name)
    {
        void Step(int dir)
        {
            if (count <= 0) return;
            int i = ((Mathf.Max(get(), 0) + dir) % count + count) % count;
            set(i);
            if (row.Value != null) row.Value.text = name(i);
        }

        if (row.Previous != null) { row.Previous.onClick.AddListener(AudioManager.Click); row.Previous.onClick.AddListener(() => Step(-1)); }
        if (row.Next != null)     { row.Next.onClick.AddListener(AudioManager.Click);     row.Next.onClick.AddListener(() => Step(+1)); }

        _refreshers.Add(() =>
        {
            int i = Mathf.Clamp(get(), 0, Mathf.Max(count - 1, 0));
            if (row.Value != null) row.Value.text = count > 0 ? name(i) : "—";
        });
    }

    private void RefreshAll()
    {
        foreach (Action r in _refreshers) r();
    }

    // Sin EventSystem no funciona ningún clic. El menú principal lo trae; en la partida
    // ya hay uno, pero por las dudas.
    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    // =========================================================
    // IMenuScreen
    // =========================================================

    public int MenuLayer => 1;
    public GameObject DefaultSelection => _firstControl;
    public Transform SelectionRoot => PanelCanvas != null ? PanelCanvas.transform : transform;
    public void OnBack()  => Close();
    public void OnPause() => Close();
}
