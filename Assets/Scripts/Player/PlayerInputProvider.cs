using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// ============================================================
// PlayerInputProvider
//
// Puente entre el Input System nuevo (el asset InputSystem_Actions) y el
// resto de scripts del jugador (PlayerController, la cámara y los menús).
// En vez de que cada uno llame a Input.GetButtonDown("Fire1") del sistema
// legacy, leen las ACCIONES tipadas de acá (Move, Look, PrimaryAttack, ...).
//
// Vive en el mismo prefab del jugador. Solo el DUEÑO local lo inicializa
// (InitializeForOwner), que clona el asset —para que cada jugador tenga sus
// propias acciones y, más adelante, su propio dispositivo emparejado— y
// habilita el mapa "Player". Las copias remotas/servidor no leen input, así
// que no lo tocan.
//
// ACCESO GLOBAL: como en cada proceso (editor o build) hay UN solo jugador
// local, exponemos el proveedor del dueño como Local, igual que
// PlayerController.LocalPlayer. Así la cámara y los menús —que son objetos de
// escena -pueden leer el input del jugador sin una referencia asignada a mano.
//
// PREFAB: hay que asignar el asset InputSystem_Actions al campo InputActions
// en el inspector de este componente.
// ============================================================
public class PlayerInputProvider : MonoBehaviour
{
    [Header("Input System")]
    [Tooltip("Asignar el asset InputSystem_Actions (Assets/InputSystem_Actions.inputactions).")]
    public InputActionAsset InputActions;

    // Proveedor del jugador DUEÑO local (null en un servidor sin cliente, o
    // antes de que el dueño se inicialice). Lo leen la cámara y los menús.
    public static PlayerInputProvider Local { get; private set; }

    // Clon propio del asset (ver InitializeForOwner). Cada dueño trabaja sobre
    // su propia copia, no sobre el template compartido del prefab.
    private InputActionAsset _assetInstance;
    private InputActionMap   _playerMap;

    // Acciones del mapa "Player". Los otros scripts las leen directamente con
    // ReadValue<Vector2>() / WasPressedThisFrame() / WasReleasedThisFrame().
    public InputAction Move            { get; private set; }
    public InputAction Look            { get; private set; }
    public InputAction Jump            { get; private set; }
    public InputAction PrimaryAttack   { get; private set; }
    public InputAction Secondary       { get; private set; }
    public InputAction MovementAbility { get; private set; }
    public InputAction Ability1        { get; private set; }
    public InputAction Ability2        { get; private set; }
    public InputAction Ability3        { get; private set; }
    public InputAction Cheat           { get; private set; }

    // Acciones del mapa "UI" (para navegar menús con control sin chocar con las
    // de juego). Se leen solo cuando el jugador está en modo UI (ver SetUIMode).
    public InputAction Navigate { get; private set; }
    public InputAction Submit   { get; private set; }
    public InputAction Cancel   { get; private set; }

    private InputActionMap _uiMap;

    private bool _initialized;

    // True una vez que el dueño local habilitó su input. La cámara/menús lo
    // consultan para no leer acciones a medio armar.
    public bool IsReady => _initialized;

    // True si esta instancia juega con teclado/mouse (o sin restricción). Los
    // menús lo usan para no procesar las teclas numéricas 1/2/3 en una instancia
    // que en realidad es de mando (evita que el teclado compartido cruce menús
    // entre instancias en multijugador local).
    public bool UsesKeyboardMouse { get; private set; } = true;

    // Lecturas cómodas (con default seguro si todavía no se inicializó).
    public Vector2 MoveValue => _initialized ? Move.ReadValue<Vector2>() : Vector2.zero;
    public Vector2 LookValue => _initialized ? Look.ReadValue<Vector2>() : Vector2.zero;

    // True si el movimiento lo está manejando un mando (stick izquierdo) y no el
    // teclado. Lo usa el menú radial para decidir si apunta con el stick o con el
    // mouse, y PlayerController para no mover al personaje mientras se apunta el
    // radial con el stick.
    public bool MoveIsGamepad =>
        _initialized && Move.activeControl != null && Move.activeControl.device is Gamepad;

    // Inicializa el input para el jugador DUEÑO local. La llama
    // PlayerController.OnStartClient cuando IsOwner. Clona el asset, resuelve
    // las acciones del mapa "Player", lo habilita y se registra como Local.
    public void InitializeForOwner()
    {
        if (_initialized) return;

        if (InputActions == null)
        {
            Debug.LogError("[PlayerInputProvider] Falta asignar el asset InputActions " +
                           "en el prefab del jugador — el input no va a funcionar.");
            return;
        }

        // Clon propio: así cada jugador local tiene sus acciones independientes.
        _assetInstance = Instantiate(InputActions);

        _playerMap      = _assetInstance.FindActionMap("Player", true);
        Move            = _playerMap.FindAction("Move", true);
        Look            = _playerMap.FindAction("Look", true);
        Jump            = _playerMap.FindAction("Jump", true);
        PrimaryAttack   = _playerMap.FindAction("PrimaryAttack", true);
        Secondary       = _playerMap.FindAction("Secondary", true);
        MovementAbility = _playerMap.FindAction("MovementAbility", true);
        Ability1        = _playerMap.FindAction("Ability1", true);
        Ability2        = _playerMap.FindAction("Ability2", true);
        Ability3        = _playerMap.FindAction("Ability3", true);
        Cheat           = _playerMap.FindAction("Cheat", true);

        _uiMap   = _assetInstance.FindActionMap("UI", true);
        Navigate = _uiMap.FindAction("Navigate", true);
        Submit   = _uiMap.FindAction("Submit", true);
        Cancel   = _uiMap.FindAction("Cancel", true);

        // Arranca en modo juego: mapa Player activo, UI apagado.
        _playerMap.Enable();
        _uiMap.Disable();

        // Que el juego siga leyendo input aunque su ventana no tenga foco —
        // necesario para jugar en varias instancias a la vez (multijugador local).
        Application.runInBackground = true;

        // Multijugador local: restringir este jugador a un solo dispositivo
        // (teclado+mouse o UN mando concreto), para que el mando no controle a
        // todas las instancias a la vez. Solo aplica en el editor (MPPM).
        ApplyLocalDeviceProfile();

        _initialized = true;
        Local = this;
    }

    // Cambia el contexto de input entre JUEGO y MENÚ. En modo UI se apaga el mapa
    // Player y se enciende el UI, así los mismos botones (ej. Cross) significan
    // "confirmar" en el menú sin además disparar el salto/habilidad del juego. Lo
    // llaman los menús al abrirse (true) y cerrarse (false). No toca el cursor —
    // de eso se siguen encargando los menús.
    public void SetUIMode(bool uiMode)
    {
        if (!_initialized) return;

        if (uiMode) { _playerMap.Disable(); _uiMap.Enable(); }
        else        { _uiMap.Disable();     _playerMap.Enable(); }
    }

    // Devuelve un "paso" de navegación discreto (-1 anterior / +1 siguiente / 0)
    // a partir de la acción Navigate (stick/d-pad/flechas), con anti-repetición:
    // hay que soltar el stick por debajo del umbral para que cuente otro paso. Lo
    // usan los menús de tarjetas para mover la selección. Requiere estar en modo
    // UI (ver SetUIMode); si no, Navigate está apagado y devuelve 0.
    private bool _navStepLatched;
    public int ReadNavigateStep()
    {
        if (Navigate == null) return 0;

        Vector2 nav = Navigate.ReadValue<Vector2>();
        // Eje dominante: horizontal derecha o vertical abajo = siguiente (+1).
        float v = Mathf.Abs(nav.x) >= Mathf.Abs(nav.y) ? nav.x : -nav.y;

        if (Mathf.Abs(v) < 0.5f) { _navStepLatched = false; return 0; }
        if (_navStepLatched) return 0;

        _navStepLatched = true;
        return v > 0 ? 1 : -1;
    }

    // ============================================================
    // SEPARACIÓN DE DISPOSITIVOS (multijugador local con MPPM)
    //
    // Cada instancia del editor (editor principal + "virtual players" de
    // Multiplayer Play Mode) corre en su propio proceso, pero todas ven los
    // mismos dispositivos físicos. Sin restringir, un mando manda su input a
    // TODAS las instancias a la vez. Acá le decimos a ESTA instancia con qué
    // dispositivo jugar, restringiendo el asset de acciones a esos dispositivos.
    //
    // Reglas (por tags de MPPM, que se configuran en la ventana Multiplayer Play
    // Mode → Players; se le puede poner tag TAMBIÉN al editor principal):
    //   - Tag "kbm"                    → teclado + mouse.
    //   - Tag "pad0" / "pad1" / "pad2" → ese mando por índice (para varios mandos).
    //   - Tag "pad"                    → primer mando.
    //   - SIN tag                      → sin restricción (usa todos los dispositivos).
    //
    // Para jugar local sin cruce: al editor principal ponele el tag "kbm" y a
    // cada virtual player "pad0", "pad1", etc. Si no ponés tags (ej. probando con
    // una sola instancia), no se restringe nada y podés usar teclado o mando.
    //
    // En una BUILD no se compila nada de esto (#if UNITY_EDITOR): cada amigo usa
    // sus propios dispositivos sin restricción.
    // ============================================================
    private void ApplyLocalDeviceProfile()
    {
#if UNITY_EDITOR
        InputDevice[] profile = ResolveEditorDeviceProfile();
        if (profile != null && profile.Length > 0)
        {
            _assetInstance.devices = profile;
            UsesKeyboardMouse = System.Array.Exists(profile, d => d is Keyboard || d is Mouse);
            Debug.Log($"[PlayerInputProvider] Instancia restringida a: " +
                      $"{string.Join(", ", System.Array.ConvertAll(profile, d => d.displayName))}");
        }
#endif
    }

#if UNITY_EDITOR
    // Decide con qué dispositivo(s) juega esta instancia, según los tags de MPPM.
    // null = sin restricción (usa todos los dispositivos).
    private InputDevice[] ResolveEditorDeviceProfile()
    {
        string[] tags = Unity.Multiplayer.Playmode.CurrentPlayer.ReadOnlyTags();

        bool HasTag(string t)
        {
            if (tags == null) return false;
            foreach (var tag in tags)
                if (string.Equals(tag, t, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        if (HasTag("kbm"))  return KeyboardMouseDevices();
        if (HasTag("pad0")) return OneGamepad(0);
        if (HasTag("pad1")) return OneGamepad(1);
        if (HasTag("pad2")) return OneGamepad(2);
        if (HasTag("pad"))  return OneGamepad(0);

        // Sin tag: no restringir. Sirve para probar con una sola instancia
        // (teclado o mando indistinto). Para multijugador local, poné tags.
        return null;
    }

    private InputDevice[] KeyboardMouseDevices()
    {
        var list = new List<InputDevice>();
        if (Keyboard.current != null) list.Add(Keyboard.current);
        if (Mouse.current != null)    list.Add(Mouse.current);
        return list.ToArray();
    }

    private InputDevice[] OneGamepad(int index)
    {
        var pads = Gamepad.all;
        if (index >= 0 && index < pads.Count) return new InputDevice[] { pads[index] };
        Debug.LogWarning($"[PlayerInputProvider] No hay gamepad #{index} conectado — " +
                         $"esta instancia no se restringió a ningún dispositivo.");
        return null;
    }

    // Hace que, en el editor, el input llegue a la Game view aunque la ventana no
    // tenga el foco. Se necesitan DOS ajustes juntos para jugar en varias
    // instancias a la vez:
    //
    //   1) Play Mode Input Behavior = AllDeviceInputAlwaysGoesToGameView:
    //      el input no se desvía al editor cuando la ventana pierde el foco.
    //   2) Background Behavior = IgnoreFocus:
    //      SIN esto, el #1 respeta el "background behavior" por defecto, que
    //      DESACTIVA el mando al perder el foco (justo lo que NO queremos). Con
    //      IgnoreFocus, los dispositivos siguen vivos aunque la ventana no esté
    //      enfocada, así el 2º jugador puede usar el mando en su ventana de fondo.
    //
    // La separación por dispositivo (arriba) evita que un mismo mando controle
    // todas las instancias.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ConfigureEditorMultiInstanceInput()
    {
        var playMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        if (InputSystem.settings.editorInputBehaviorInPlayMode != playMode)
            InputSystem.settings.editorInputBehaviorInPlayMode = playMode;

        var background = InputSettings.BackgroundBehavior.IgnoreFocus;
        if (InputSystem.settings.backgroundBehavior != background)
            InputSystem.settings.backgroundBehavior = background;
    }
#endif

    // Libera el input al despawnear al dueño (lo llama PlayerController.OnStopClient).
    public void ShutdownOwner()
    {
        if (!_initialized) return;

        _playerMap?.Disable();
        _uiMap?.Disable();
        if (Local == this) Local = null;
        _initialized = false;

        if (_assetInstance != null)
        {
            Destroy(_assetInstance);
            _assetInstance = null;
        }
    }

    private void OnDestroy()
    {
        // Salvavidas por si el objeto se destruye sin pasar por ShutdownOwner.
        if (Local == this) Local = null;
        if (_assetInstance != null) Destroy(_assetInstance);
    }
}
