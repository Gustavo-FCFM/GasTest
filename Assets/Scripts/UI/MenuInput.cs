using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// ============================================================
// IMenuScreen
//
// Una pantalla de menú: el menú principal, la conexión, la sala, los ajustes, la pausa.
// Al abrirse se anota en MenuInput (Push) y al cerrarse se borra (Pop). Así MenuInput sabe
// cuál está arriba de todo: esa es la que recibe "volver" y Start, y la que tiene la
// selección del control.
// ============================================================
public interface IMenuScreen
{
    // 0 = una pantalla de fondo (el menú principal, la sala); 1 = una que se abre encima (la
    // conexión, los ajustes, la pausa). A igual capa, arriba queda la última que se abrió.
    int MenuLayer { get; }

    // A qué se le pone la selección al abrirse, o cuando se pierde (con control). Null = nada.
    GameObject DefaultSelection { get; }

    // La selección tiene que quedar dentro de esto. Si el stick la saca afuera (a otra
    // pantalla que está detrás), vuelve a donde estaba.
    Transform SelectionRoot { get; }

    // B o ESC con esta pantalla arriba de todo.
    void OnBack();

    // Start o ESC con esta pantalla arriba de todo. En una que se abre encima es lo mismo que
    // volver; en la sala abre la pausa, en el menú principal los ajustes.
    void OnPause();
}

// ============================================================
// MenuInput
//
// El input de los MENÚS, en una sola pieza. Vive en el prefab de menús (Menus_UI ▸ MenuInput).
//
// LOS MENÚS VAN POR EL INPUT SYSTEM NUEVO (desde el 26 de octubre de 2026, para el showcase).
// El EventSystem de la escena lleva un InputSystemUIInputModule, y acá se le da una COPIA
// PROPIA del asset de input con su mapa UI siempre prendido. Es propia porque la del jugador
// apaga ese mapa mientras se juega (ver PlayerInputProvider.SetUIMode), y porque los menús
// tienen que andar antes de que exista el jugador. Antes era el StandaloneInputModule del
// input viejo: no leía la cruceta y no había forma de abrir nada con Start.
//
// QUIÉN RESPONDE A "VOLVER" Y A START. Cada pantalla abierta se anota acá (IMenuScreen), y
// solo se entera la de arriba de todo:
//   · B (o ESC) → OnBack: cierra lo de arriba (los ajustes, la pausa) o vuelve atrás.
//   · Start (o ESC) → OnPause: en la sala abre la pausa; en el menú principal, los ajustes;
//     en las demás es lo mismo que volver.
//   · Sin ninguna pantalla (jugando), Start o ESC abren la pausa. B es el dash: no hace nada acá.
// ESC es las dos cosas a la vez (Cancel y Pause en el asset): cuenta una sola vez, como Pause.
//
// LA SELECCIÓN (lo que se mueve con el stick):
//   · Al abrirse una pantalla se elige su DefaultSelection; al cerrarse, la de abajo
//     recupera lo que tenía elegido.
//   · Si la selección se pierde (el botón elegido se apagó: en la sala aparecen y
//     desaparecen) y se está usando el control, vuelve a la DefaultSelection de la de arriba.
//     Con el mouse no se toca nada.
//   · El stick no saca la selección de la pantalla de arriba: si la navegación automática
//     salta a un botón de otra pantalla (la que está detrás), vuelve a donde estaba.
// ============================================================
public class MenuInput : MonoBehaviour
{
    [Tooltip("El asset de input del juego (InputSystem_Actions), el mismo que tiene el jugador. " +
             "Los menús usan su mapa UI.")]
    [Label("Acciones de input")]
    public InputActionAsset InputActions;

    public static MenuInput Instance { get; private set; }

    // El asset original (el del jugador), para quien necesite su propia copia: el espectador.
    public static InputActionAsset ActionsTemplate => Instance != null ? Instance.InputActions : null;

    // Las acciones de los menús (de la copia propia). Null si no hay MenuInput en la escena.
    public static InputAction Navigate { get; private set; }
    public static InputAction Submit   { get; private set; }
    public static InputAction Cancel   { get; private set; }
    public static InputAction Pause    { get; private set; }

    // X y Y del control: borrar y espacio en el teclado en pantalla (UI_OnScreenKeyboard).
    public static InputAction Backspace { get; private set; }
    public static InputAction Space     { get; private set; }

    // True mientras se maneja el menú con el control (o con las flechas): ahí la selección
    // tiene que estar siempre en algo. Se apaga al mover o tocar el mouse.
    public static bool UsingPad { get; private set; }

    private InputActionAsset _actions;
    private InputAction _point, _click;
    private InputSystemUIInputModule _module;
    private bool _warnedOldModule;

    // Mientras se escribe en un campo de texto, ESC es del campo (lo suelta), no de la pantalla.
    private bool _typingLastFrame;

    // Lo último elegido DENTRO de la pantalla de arriba (para devolver la selección si el
    // stick la saca afuera) y de qué pantalla era.
    private GameObject  _lastValid;
    private IMenuScreen _lastValidOwner;
    private Vector2     _lastPointer;

    // Las pantallas abiertas, de abajo hacia arriba.
    private static readonly List<IMenuScreen> _screens = new List<IMenuScreen>();
    // Lo que cada pantalla tenía elegido cuando otra se le abrió encima.
    private static readonly Dictionary<IMenuScreen, GameObject> _remembered = new Dictionary<IMenuScreen, GameObject>();

    public static IMenuScreen Top => _screens.Count > 0 ? _screens[_screens.Count - 1] : null;

    // True si hay abierta una pantalla de las que van ENCIMA (la pausa, los ajustes). Los menús
    // del jugador (el de clases, el de subclase) no leen el control mientras tanto.
    public static bool HasOverlay
    {
        get
        {
            foreach (IMenuScreen s in _screens)
                if (s.MenuLayer > 0) return true;
            return false;
        }
    }

    // Sin recarga de dominio al darle Play, las listas estáticas sobrevivirían de la vez anterior.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _screens.Clear();
        _remembered.Clear();
        UsingPad = false;
    }

    // =========================================================
    // PANTALLAS
    // =========================================================

    public static void Push(IMenuScreen screen)
    {
        if (screen == null) return;
        Prune();

        IMenuScreen oldTop = Top;
        _screens.Remove(screen);
        int i = _screens.Count;
        while (i > 0 && _screens[i - 1].MenuLayer > screen.MenuLayer) i--;
        _screens.Insert(i, screen);

        if (Top != screen) return;   // se abrió detrás de otra (la sala detrás de la pausa)

        // Lo que tenía elegido la de abajo, para devolvérselo cuando esta se cierre.
        EventSystem es = EventSystem.current;
        if (oldTop != null && es != null) _remembered[oldTop] = es.currentSelectedGameObject;

        Select(screen.DefaultSelection);
    }

    public static void Pop(IMenuScreen screen)
    {
        if (screen == null) return;
        bool wasTop = Top == screen;
        if (!_screens.Remove(screen)) return;
        _remembered.Remove(screen);
        Prune();
        if (!wasTop) return;

        IMenuScreen top = Top;
        if (top == null)
        {
            // Jugando no queda nada elegido: A es saltar, no apretar el último botón.
            Select(null);
            return;
        }

        GameObject back = _remembered.TryGetValue(top, out GameObject g) ? g : null;
        _remembered.Remove(top);
        Select(IsValidSelection(back, top) ? back : top.DefaultSelection);
    }

    // Elige un objeto para el control (null = nada).
    public static void Select(GameObject go)
    {
        EventSystem es = EventSystem.current;
        if (es == null) return;
        if (go != null && !go.activeInHierarchy) go = null;
        if (es.currentSelectedGameObject == go) return;
        es.SetSelectedGameObject(go);
    }

    // Saca las pantallas destruidas (el == de Unity da true con un objeto destruido).
    private static void Prune()
    {
        _screens.RemoveAll(s => s == null || (s is Object o && o == null));
    }

    private static bool IsValidSelection(GameObject sel, IMenuScreen screen)
    {
        if (sel == null || !sel.activeInHierarchy || screen == null) return false;
        Transform root = screen.SelectionRoot;
        if (root != null && !sel.transform.IsChildOf(root)) return false;
        Selectable s = sel.GetComponent<Selectable>();
        return s == null || s.IsInteractable();
    }

    // =========================================================
    // CICLO
    // =========================================================

    private void Awake()
    {
        Instance = this;

        if (InputActions == null)
        {
            Debug.LogError("[MenuInput] Falta el asset de input (InputSystem_Actions): los menús no andan con control.");
            return;
        }

        _actions = Instantiate(InputActions);
        InputActionMap ui = _actions.FindActionMap("UI", true);
        Navigate = ui.FindAction("Navigate", true);
        Submit   = ui.FindAction("Submit", true);
        Cancel   = ui.FindAction("Cancel", true);
        Pause    = ui.FindAction("Pause", false);
        Backspace = ui.FindAction("Backspace", false);
        Space     = ui.FindAction("Space", false);
        _point   = ui.FindAction("Point", false);
        _click   = ui.FindAction("Click", false);
        if (Pause == null)
            Debug.LogWarning("[MenuInput] El asset de input no tiene la acción UI/Pause: Start no abre la pausa.");

        // Solo los dispositivos de esta ventana (LocalSeat): con varias ventanas en una PC,
        // los menús de una no los mueve el control de otra.
        LocalSeat.Register(_actions);
        ui.Enable();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        Navigate = Submit = Cancel = Pause = Backspace = Space = null;
        if (_actions != null) { LocalSeat.Unregister(_actions); _actions.Disable(); Destroy(_actions); }
    }

    private void Update()
    {
        if (_actions == null) return;

        EnsureModule();
        Prune();
        TrackDevice();
        HandleBackAndPause();
        KeepSelection();
    }

    // El EventSystem tiene que leer de NUESTRA copia. Se revisa cada frame porque el de la
    // escena puede cambiar (los Ajustes crean uno si no hay).
    private void EnsureModule()
    {
        EventSystem es = EventSystem.current;
        if (es == null) return;
        if (_module != null && _module.gameObject == es.gameObject) return;

        _module = es.GetComponent<InputSystemUIInputModule>();
        if (_module == null)
        {
            if (!_warnedOldModule)
            {
                _warnedOldModule = true;
                Debug.LogWarning("[MenuInput] El EventSystem de la escena no usa el Input System nuevo: los menús " +
                                 "no andan con la cruceta. Correr Mercenarios ▸ Una vez ▸ Menús con control (showcase 1).");
            }
            return;
        }

        _module.actionsAsset = _actions;
        _module.point       = Ref("UI/Point");
        _module.leftClick   = Ref("UI/Click");
        _module.rightClick  = Ref("UI/RightClick");
        _module.middleClick = Ref("UI/MiddleClick");
        _module.scrollWheel = Ref("UI/ScrollWheel");
        _module.move        = Ref("UI/Navigate");
        _module.submit      = Ref("UI/Submit");
        _module.cancel      = Ref("UI/Cancel");
    }

    private InputActionReference Ref(string path)
    {
        InputAction a = _actions.FindAction(path);
        return a != null ? InputActionReference.Create(a) : null;
    }

    // ¿Control o mouse? El stick, la cruceta, las flechas y A/B/Start prenden el modo control;
    // mover o tocar el mouse lo apaga.
    private void TrackDevice()
    {
        bool pad = Navigate.ReadValue<Vector2>().sqrMagnitude > 0.25f ||
                   Submit.WasPressedThisFrame() || Cancel.WasPressedThisFrame() ||
                   (Pause != null && Pause.WasPressedThisFrame());

        bool mouse = _click != null && _click.WasPressedThisFrame();
        if (_point != null)
        {
            Vector2 p = _point.ReadValue<Vector2>();
            if ((p - _lastPointer).sqrMagnitude > 4f) mouse = true;
            _lastPointer = p;
        }

        if (pad) UsingPad = true;
        else if (mouse) UsingPad = false;
    }

    private void HandleBackAndPause()
    {
        bool typing = IsTyping();
        bool typingNow = typing || _typingLastFrame;
        _typingLastFrame = typing;

        bool pause = Pause != null && Pause.WasPressedThisFrame();
        bool back  = Cancel.WasPressedThisFrame();
        if (!pause && !back) return;
        if (typingNow) return;   // ESC (o B) suelta el campo de texto; no cierra la pantalla

        IMenuScreen top = Top;
        if (top != null)
        {
            if (pause) top.OnPause();
            else       top.OnBack();
            return;
        }

        if (pause) UI_PauseMenu.Get()?.Open();
    }

    private static bool IsTyping()
    {
        EventSystem es = EventSystem.current;
        GameObject sel = es != null ? es.currentSelectedGameObject : null;
        if (sel == null) return false;
        TMP_InputField field = sel.GetComponent<TMP_InputField>();
        return field != null && field.isFocused;
    }

    private void KeepSelection()
    {
        EventSystem es = EventSystem.current;
        IMenuScreen top = Top;
        if (es == null || top == null) { _lastValid = null; _lastValidOwner = null; return; }

        if (_lastValidOwner != top) { _lastValid = null; _lastValidOwner = top; }

        GameObject sel = es.currentSelectedGameObject;
        if (IsValidSelection(sel, top)) { _lastValid = sel; return; }

        // El stick la sacó a otra pantalla: vuelve a donde estaba (el movimiento no hace nada).
        bool leftTheScreen = sel != null && sel.activeInHierarchy && top.SelectionRoot != null &&
                             !sel.transform.IsChildOf(top.SelectionRoot);
        if (leftTheScreen && IsValidSelection(_lastValid, top))
        {
            Select(_lastValid);
            return;
        }

        // Se perdió (se apagó, o no hay nada): solo con el control. Con el mouse no hace falta
        // resaltar nada.
        if (!UsingPad) return;
        GameObject fallback = IsValidSelection(_lastValid, top) ? _lastValid : top.DefaultSelection;
        if (IsValidSelection(fallback, top)) Select(fallback);
    }
}
