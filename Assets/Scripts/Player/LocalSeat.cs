using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.LowLevel;

// ============================================================
// LocalSeat
//
// EL PUESTO DE ESTA VENTANA: con qué juega y si suena (28 de octubre de 2026, etapa 3 del
// showcase). En la PC del showcase corren varias ventanas del juego a la vez, una por
// monitor y cada una con su jugador. Sin esto todas leen todos los dispositivos: un mismo
// control mueve las tres.
//
// SE ELIGE AL ABRIR LA BUILD, con argumentos (los pone el Showcase.bat que se arma junto a
// cada build):
//   · -pad  → esta ventana juega con UN control. Al arrancar pide "Aprieta A en tu control"
//             (UI_PadClaim) y se queda con el que apriete A mientras ELLA tiene el foco (clic
//             en la ventana y A). Desde ahí ignora todo lo demás, también el teclado y el
//             mouse, aunque pierda el foco. Si el control se desconecta, lo vuelve a tomar
//             al reconectarse, o se aprieta A en otro.
//   · -kbm  → teclado y mouse, ningún control.
//   · -mute → sin sonido, para que suene una sola ventana.
//   · nada  → como siempre: todos los dispositivos.
// En el EDITOR mandan los tags de Multiplayer Play Mode (ventana Multiplayer Play Mode ▸
// Players; se le puede poner tag también al editor principal): "kbm", "pad" (pide A),
// "pad0" / "pad1" / "pad2" (ese control por número) y "mute".
//
// CÓMO SE APLICA:
//   · Cada copia del asset de input (la del jugador, la de los menús, la del espectador) se
//     anota acá (Register) y se le restringen los dispositivos (asset.devices). Esperando
//     la A, la lista está vacía: esa ventana no responde a nada.
//   · Lo que lee un dispositivo por fuera del asset pregunta AllowsKeyboardMouse o Pad.
//   · El cursor: una ventana de control nunca lo atrapa (UICursor); si no, le roba el mouse
//     a la de teclado.
//   · Lo que se DIBUJA (InputGlyphs) sigue al puesto: la ventana de control muestra RB y la
//     de teclado Q, aunque las tres compartan los Ajustes (son los mismos PlayerPrefs).
//
// POR QUÉ ANDAN SIN FOCO: el Input System está en "Ignore Focus" (InputSystem.inputsettings,
// y acá también en el editor) y el juego corre en segundo plano (Run In Background): una
// ventana de atrás sigue leyendo su control, y su EventSystem sigue navegando los menús.
// ============================================================
public static class LocalSeat
{
    public enum EMode
    {
        All,             // todos los dispositivos (sin argumentos)
        KeyboardMouse,   // -kbm
        Gamepad,         // -pad: un control
    }

    public static EMode Mode { get; private set; } = EMode.All;

    // Sin sonido (-mute). Lo respeta GameSettings al poner el volumen.
    public static bool Muted { get; private set; }

    // -pad y todavía nadie apretó A (o se desconectó el que tenía).
    public static bool WaitingForPad => Mode == EMode.Gamepad && _pad == null;

    // Se desconectó el control que tenía (para avisarlo distinto que la primera vez).
    public static bool LostPad => WaitingForPad && _lastPad != null;

    // ¿Esta ventana escucha el teclado y el mouse? No en una ventana de control.
    public static bool AllowsKeyboardMouse => Mode != EMode.Gamepad;

    // EL control de esta ventana, para lo que lo lee directo. Sin puesto, el último que se
    // usó (Gamepad.current); con -kbm, ninguno.
    public static Gamepad Pad =>
        Mode == EMode.Gamepad ? _pad : Mode == EMode.KeyboardMouse ? null : Gamepad.current;

    // Qué botones dibujar (InputGlyphs). null = lo que se eligió en Ajustes.
    public static EInputScheme? Scheme
    {
        get
        {
            switch (Mode)
            {
                case EMode.KeyboardMouse: return EInputScheme.KeyboardMouse;
                case EMode.Gamepad:       return _pad is DualShockGamepad ? EInputScheme.PlayStation : EInputScheme.Xbox;
                default:                  return null;
            }
        }
    }

    // Cambió el control de la ventana (se tomó, se perdió).
    public static event Action OnChanged;

    private static readonly List<InputActionAsset> _assets = new List<InputActionAsset>();
    private static Gamepad _pad;
    private static Gamepad _lastPad;
    private static int     _fixedPadIndex = -1;   // tags pad0..pad2 del editor: sin pedir A

    // =========================================================
    // ARRANQUE
    // =========================================================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _assets.Clear();
        _pad = _lastPad = null;
        _fixedPadIndex = -1;
        Mode = EMode.All;
        Muted = false;
        OnChanged = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
#if UNITY_EDITOR
        ConfigureEditorInput();
        ReadEditorTags();
#else
        ReadArgs();
#endif
        InputSystem.onDeviceChange -= OnDeviceChange;
        InputSystem.onDeviceChange += OnDeviceChange;
        InputSystem.onAfterUpdate  -= WatchForA;
        InputSystem.onAfterUpdate  += WatchForA;

        if (_fixedPadIndex >= 0) TakeFixedPad();
        if (Muted) AudioListener.volume = 0f;

        if (Mode != EMode.All || Muted)
            Debug.Log($"[Puesto] Esta ventana juega con: {Describe()}" + (Muted ? " · sin sonido" : "") + ".");
    }

    // La build: -pad, -kbm, -mute.
    private static void ReadArgs()
    {
        bool pad = false, kbm = false;
        foreach (string arg in Environment.GetCommandLineArgs())
        {
            if (string.Equals(arg, "-pad",  StringComparison.OrdinalIgnoreCase)) pad = true;
            if (string.Equals(arg, "-kbm",  StringComparison.OrdinalIgnoreCase)) kbm = true;
            if (string.Equals(arg, "-mute", StringComparison.OrdinalIgnoreCase)) Muted = true;
        }

        if (pad && kbm) Debug.LogWarning("[Puesto] Vinieron -pad y -kbm juntos: queda -pad (un control).");
        Mode = pad ? EMode.Gamepad : kbm ? EMode.KeyboardMouse : EMode.All;
    }

#if UNITY_EDITOR
    // Las ventanas del editor: los tags de Multiplayer Play Mode.
    private static void ReadEditorTags()
    {
        string[] tags = Unity.Multiplayer.Playmode.CurrentPlayer.ReadOnlyTags();
        bool Has(string t) => tags != null && Array.Exists(tags, x => string.Equals(x, t, StringComparison.OrdinalIgnoreCase));

        Muted = Has("mute");
        if      (Has("kbm"))  Mode = EMode.KeyboardMouse;
        else if (Has("pad0")) { Mode = EMode.Gamepad; _fixedPadIndex = 0; }
        else if (Has("pad1")) { Mode = EMode.Gamepad; _fixedPadIndex = 1; }
        else if (Has("pad2")) { Mode = EMode.Gamepad; _fixedPadIndex = 2; }
        else if (Has("pad"))  Mode = EMode.Gamepad;
    }

    // En el editor, que el input llegue a la Game view aunque la ventana no tenga el foco.
    // Hacen falta los dos juntos: sin "Ignore Focus", el primero respeta el comportamiento por
    // defecto, que APAGA el control al perder el foco (justo lo que no queremos).
    private static void ConfigureEditorInput()
    {
        var playMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        if (InputSystem.settings.editorInputBehaviorInPlayMode != playMode)
            InputSystem.settings.editorInputBehaviorInPlayMode = playMode;

        var background = InputSettings.BackgroundBehavior.IgnoreFocus;
        if (InputSystem.settings.backgroundBehavior != background)
            InputSystem.settings.backgroundBehavior = background;
    }
#endif

    // =========================================================
    // LAS COPIAS DEL ASSET DE INPUT
    // =========================================================

    // Anota una copia del asset de input: desde ahora solo escucha los dispositivos de esta
    // ventana (y se actualiza sola si el control cambia).
    public static void Register(InputActionAsset asset)
    {
        if (asset == null) return;
        if (!_assets.Contains(asset)) _assets.Add(asset);
        ApplyTo(asset);
    }

    public static void Unregister(InputActionAsset asset) => _assets.Remove(asset);

    private static void ApplyAll()
    {
        _assets.RemoveAll(a => a == null);
        foreach (InputActionAsset a in _assets) ApplyTo(a);
    }

    private static void ApplyTo(InputActionAsset asset)
    {
        switch (Mode)
        {
            case EMode.KeyboardMouse:
                var km = new List<InputDevice>();
                foreach (InputDevice d in InputSystem.devices)
                    if (d is Keyboard || d is Mouse) km.Add(d);
                asset.devices = km.ToArray();
                break;

            case EMode.Gamepad:
                // Esperando la A: ninguno (la ventana no responde a nada).
                asset.devices = _pad != null ? new InputDevice[] { _pad } : new InputDevice[0];
                break;

            default:
                asset.devices = null;   // todos
                break;
        }
    }

    // =========================================================
    // TOMAR EL CONTROL
    // =========================================================

    // Esperando: el primer control que aprieta A mientras ESTA ventana tiene el foco es el
    // suyo. Las otras ventanas también ven esa A (están en "Ignore Focus"), pero no tienen el
    // foco: no lo toman. Corre después de cada actualización del input del juego.
    private static void WatchForA()
    {
        if (!WaitingForPad || _fixedPadIndex >= 0) return;
        if (!Application.isPlaying || !Application.isFocused) return;
        if (InputState.currentUpdateType != InputUpdateType.Dynamic) return;

        foreach (Gamepad g in Gamepad.all)
        {
            if (!g.buttonSouth.wasPressedThisFrame) continue;
            SetPad(g);
            return;
        }
    }

    private static void TakeFixedPad()
    {
        if (_fixedPadIndex < Gamepad.all.Count) SetPad(Gamepad.all[_fixedPadIndex]);
        else Debug.LogWarning($"[Puesto] No hay control #{_fixedPadIndex} conectado: esta ventana espera a que se conecte.");
    }

    private static void SetPad(Gamepad pad)
    {
        if (_pad == pad) return;
        _pad = pad;
        if (pad != null) _lastPad = pad;

        ApplyAll();
        OnChanged?.Invoke();
        Debug.Log(pad != null ? $"[Puesto] Esta ventana juega con: {pad.displayName}." : "[Puesto] Se desconectó el control de esta ventana.");
    }

    // Se desconectó el control: la ventana vuelve a esperar. Si vuelve el MISMO (el Input
    // System lo reconoce al reconectarlo), lo retoma sin pedir A.
    private static void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        switch (change)
        {
            case InputDeviceChange.Removed:
            case InputDeviceChange.Disconnected:
                if (device == _pad) SetPad(null);
                else if (Mode == EMode.KeyboardMouse) ApplyAll();
                break;

            case InputDeviceChange.Added:
            case InputDeviceChange.Reconnected:
                if (Mode == EMode.Gamepad && _pad == null)
                {
                    if (device == _lastPad) SetPad((Gamepad)device);
                    else if (_fixedPadIndex >= 0) TakeFixedPad();
                }
                else if (Mode == EMode.KeyboardMouse) ApplyAll();
                break;
        }
    }

    private static string Describe()
    {
        switch (Mode)
        {
            case EMode.KeyboardMouse: return "teclado y mouse (-kbm)";
            case EMode.Gamepad:       return _fixedPadIndex >= 0 ? $"el control #{_fixedPadIndex}" : "un control (espera la A, -pad)";
            default:                  return "todos los dispositivos";
        }
    }
}
