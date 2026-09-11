using System;
using UnityEngine;

// ============================================================
// GameSettings
//
// Los ajustes del jugador, en un solo lugar: sensibilidad, volumen, pantalla. Es
// estático a propósito —no hay nada que instanciar ni cablear— y vive en PlayerPrefs,
// así que sobreviven a cerrar el juego y valen igual en el menú principal y en la
// partida.
//
// QUIÉN LOS LEE:
//   · ThirdPersonOrbitCam y SpectatorCamera multiplican su sensibilidad por
//     MouseSensitivity y respetan InvertY.
//   · El volumen general va directo a AudioListener.volume. Los de MÚSICA y EFECTOS
//     quedan guardados para el sistema de sonido (3º del plan): cuando exista, lee
//     MusicVolume / SfxVolume y se suscribe a OnChanged.
//   · Pantalla completa, resolución, calidad y VSync se aplican solos en Apply().
//
// QUIÉN LOS ESCRIBE: UI_SettingsPanel, y nadie más. Cada cambio se aplica al momento
// (para verlo mientras movés el slider) y se guarda al cerrar el panel.
// ============================================================
public static class GameSettings
{
    // =========================================================
    // VALORES
    // =========================================================

    // Multiplicador sobre la sensibilidad base de cada cámara. 1 = como está en el
    // prefab; 0.5 = la mitad; 2 = el doble.
    public static float MouseSensitivity { get { Load(); return _mouseSensitivity; } set => Set(ref _mouseSensitivity, Mathf.Clamp(value, MinSensitivity, MaxSensitivity)); }
    public static bool  InvertY          { get { Load(); return _invertY; }          set => Set(ref _invertY, value); }

    public static float MasterVolume { get { Load(); return _masterVolume; } set => Set(ref _masterVolume, Mathf.Clamp01(value)); }
    public static float MusicVolume  { get { Load(); return _musicVolume; }  set => Set(ref _musicVolume,  Mathf.Clamp01(value)); }
    public static float SfxVolume    { get { Load(); return _sfxVolume; }    set => Set(ref _sfxVolume,    Mathf.Clamp01(value)); }

    public static FullScreenMode ScreenMode { get { Load(); return _screenMode; } set => Set(ref _screenMode, value); }

    // Índice dentro de Resolutions (ver abajo). -1 = la que tenga el monitor.
    public static int  ResolutionIndex { get { Load(); return _resolutionIndex; } set => Set(ref _resolutionIndex, value); }
    public static int  QualityLevel    { get { Load(); return _qualityLevel; }    set => Set(ref _qualityLevel, Mathf.Clamp(value, 0, QualitySettings.names.Length - 1)); }
    public static bool VSync           { get { Load(); return _vsync; }           set => Set(ref _vsync, value); }

    public const float MinSensitivity = 0.2f;
    public const float MaxSensitivity = 3f;

    // Se dispara con cada cambio (después de aplicarlo). El sistema de sonido se cuelga
    // de acá para enterarse del volumen sin preguntar cada frame.
    public static event Action OnChanged;

    // =========================================================
    // CARGA / GUARDADO
    // =========================================================

    private const string KeyPrefix = "Merc.";

    private static bool _loaded;

    private static float _mouseSensitivity = 1f;
    private static bool  _invertY;
    private static float _masterVolume = 1f;
    private static float _musicVolume  = 0.8f;
    private static float _sfxVolume    = 1f;
    private static FullScreenMode _screenMode = FullScreenMode.FullScreenWindow;
    private static int  _resolutionIndex = -1;
    private static int  _qualityLevel = -1;
    private static bool _vsync = true;

    // Se llama solo la primera vez que alguien pregunta algo. Idempotente.
    public static void Load()
    {
        if (_loaded) return;
        _loaded = true;

        _mouseSensitivity = PlayerPrefs.GetFloat(KeyPrefix + "MouseSensitivity", 1f);
        _invertY          = PlayerPrefs.GetInt  (KeyPrefix + "InvertY", 0) == 1;
        _masterVolume     = PlayerPrefs.GetFloat(KeyPrefix + "MasterVolume", 1f);
        _musicVolume      = PlayerPrefs.GetFloat(KeyPrefix + "MusicVolume", 0.8f);
        _sfxVolume        = PlayerPrefs.GetFloat(KeyPrefix + "SfxVolume", 1f);
        _screenMode       = (FullScreenMode)PlayerPrefs.GetInt(KeyPrefix + "ScreenMode", (int)Screen.fullScreenMode);
        _resolutionIndex  = PlayerPrefs.GetInt  (KeyPrefix + "Resolution", -1);
        _qualityLevel     = PlayerPrefs.GetInt  (KeyPrefix + "Quality", QualitySettings.GetQualityLevel());
        _vsync            = PlayerPrefs.GetInt  (KeyPrefix + "VSync", QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;

        Apply();
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat(KeyPrefix + "MouseSensitivity", _mouseSensitivity);
        PlayerPrefs.SetInt  (KeyPrefix + "InvertY", _invertY ? 1 : 0);
        PlayerPrefs.SetFloat(KeyPrefix + "MasterVolume", _masterVolume);
        PlayerPrefs.SetFloat(KeyPrefix + "MusicVolume", _musicVolume);
        PlayerPrefs.SetFloat(KeyPrefix + "SfxVolume", _sfxVolume);
        PlayerPrefs.SetInt  (KeyPrefix + "ScreenMode", (int)_screenMode);
        PlayerPrefs.SetInt  (KeyPrefix + "Resolution", _resolutionIndex);
        PlayerPrefs.SetInt  (KeyPrefix + "Quality", _qualityLevel);
        PlayerPrefs.SetInt  (KeyPrefix + "VSync", _vsync ? 1 : 0);
        PlayerPrefs.Save();
    }

    // Vuelve a los valores de fábrica (sin guardar: eso lo decide el panel al cerrar).
    public static void ResetToDefaults()
    {
        _mouseSensitivity = 1f;
        _invertY          = false;
        _masterVolume     = 1f;
        _musicVolume      = 0.8f;
        _sfxVolume        = 1f;
        _screenMode       = FullScreenMode.FullScreenWindow;
        _resolutionIndex  = -1;
        _qualityLevel     = QualitySettings.names.Length - 1;
        _vsync            = true;

        Apply();
        OnChanged?.Invoke();
    }

    // =========================================================
    // APLICAR
    // =========================================================

    // Lleva los valores al motor. Se puede llamar cuantas veces haga falta: solo toca
    // la pantalla si de verdad cambió algo (Screen.SetResolution reinicia el swapchain
    // y parpadea, no es gratis).
    public static void Apply()
    {
        AudioListener.volume = _masterVolume;

        if (_qualityLevel >= 0 && _qualityLevel < QualitySettings.names.Length &&
            _qualityLevel != QualitySettings.GetQualityLevel())
            QualitySettings.SetQualityLevel(_qualityLevel, true);

        QualitySettings.vSyncCount = _vsync ? 1 : 0;

        ApplyScreen();
    }

    private static void ApplyScreen()
    {
        Resolution[] list = Resolutions;
        if (list.Length == 0) return;

        int index = _resolutionIndex;
        if (index < 0 || index >= list.Length) index = NativeResolutionIndex();

        Resolution target = list[index];
        bool sameSize = Screen.width == target.width && Screen.height == target.height;
        bool sameMode = Screen.fullScreenMode == _screenMode;
        if (sameSize && sameMode) return;

        Screen.SetResolution(target.width, target.height, _screenMode);
    }

    // =========================================================
    // RESOLUCIONES
    // =========================================================

    // Las del monitor sin repetir (Screen.resolutions trae una entrada por cada tasa de
    // refresco). Ordenadas de menor a mayor.
    private static Resolution[] _resolutions;

    public static Resolution[] Resolutions
    {
        get
        {
            if (_resolutions != null) return _resolutions;

            var unique = new System.Collections.Generic.List<Resolution>();
            foreach (Resolution r in Screen.resolutions)
            {
                bool dup = false;
                foreach (Resolution u in unique)
                    if (u.width == r.width && u.height == r.height) { dup = true; break; }
                if (!dup) unique.Add(r);
            }

            // Sin lista (pasa en algunos entornos sin monitor): al menos la actual.
            if (unique.Count == 0) unique.Add(Screen.currentResolution);

            unique.Sort((a, b) => a.width != b.width ? a.width.CompareTo(b.width) : a.height.CompareTo(b.height));
            _resolutions = unique.ToArray();
            return _resolutions;
        }
    }

    // La del monitor: la más grande de la lista.
    public static int NativeResolutionIndex() => Resolutions.Length - 1;

    // Índice de la resolución que está puesta AHORA, para arrancar el panel en el lugar
    // correcto aunque el índice guardado sea -1.
    public static int CurrentResolutionIndex()
    {
        Resolution[] list = Resolutions;
        for (int i = 0; i < list.Length; i++)
            if (list[i].width == Screen.width && list[i].height == Screen.height) return i;
        return NativeResolutionIndex();
    }

    // =========================================================
    // INTERNO
    // =========================================================

    private static void Set<T>(ref T field, T value)
    {
        Load();
        if (Equals(field, value)) return;
        field = value;
        Apply();
        OnChanged?.Invoke();
    }

}
