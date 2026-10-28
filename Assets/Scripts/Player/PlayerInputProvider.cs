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
// propias acciones y solo los dispositivos de SU ventana (ver LocalSeat)— y
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
    [Label("Acciones de input")]
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
    public InputAction ChangeClass     { get; private set; }

    // Bajar mientras se vuela (Ctrl / L3). Puede faltar si el asset todavía no tiene la
    // acción "Descend": entonces DescendHeld lee el Ctrl izquierdo directo.
    public InputAction Descend         { get; private set; }
    public bool DescendHeld => Descend != null
        ? Descend.IsPressed()
        : Keyboard.current != null && LocalSeat.AllowsKeyboardMouse && Keyboard.current.leftCtrlKey.isPressed;

    // Girar lo que se pone (el Muro de fuego del Mago) mientras se apunta: la rueda del mouse y,
    // con control, la cruceta izquierda/derecha. Pueden faltar si el asset todavía no tiene las
    // acciones: entonces PlacementRotateStep lee la rueda y la cruceta directo.
    public InputAction RotatePlacementLeft  { get; private set; }
    public InputAction RotatePlacementRight { get; private set; }

    // -1, 0 o +1: si en este frame se pidió girar a un lado o al otro (rueda arriba / cruceta
    // derecha = +1). Leerlo una vez por frame.
    public int PlacementRotateStep
    {
        get
        {
            if (!_initialized) return 0;
            if (RotatePlacementLeft != null && RotatePlacementRight != null)
                return (RotatePlacementRight.WasPressedThisFrame() ? 1 : 0) - (RotatePlacementLeft.WasPressedThisFrame() ? 1 : 0);

            int step = 0;
            if (Mouse.current != null && LocalSeat.AllowsKeyboardMouse)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (scroll > 0.01f) step++;
                else if (scroll < -0.01f) step--;
            }
            Gamepad pad = LocalSeat.Pad;   // el de esta ventana (ver LocalSeat)
            if (pad != null)
            {
                if (pad.dpad.right.wasPressedThisFrame) step++;
                if (pad.dpad.left.wasPressedThisFrame)  step--;
            }
            return Mathf.Clamp(step, -1, 1);
        }
    }

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

    // True si esta ventana juega con teclado/mouse (o sin restricción). Los
    // menús lo usan para no procesar las teclas numéricas 1/2/3 en una ventana
    // que en realidad es de control (evita que el teclado compartido cruce menús
    // entre ventanas en multijugador local). Lo decide el puesto (LocalSeat).
    public bool UsesKeyboardMouse => LocalSeat.AllowsKeyboardMouse;

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
        ChangeClass     = _playerMap.FindAction("ChangeClass", true);
        Descend         = _playerMap.FindAction("Descend", false);
        RotatePlacementLeft  = _playerMap.FindAction("RotatePlacementLeft", false);
        RotatePlacementRight = _playerMap.FindAction("RotatePlacementRight", false);

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

        // Varias ventanas en una PC: este jugador escucha solo los dispositivos de SU
        // ventana (teclado y mouse, o su control), para que un control no mueva a todas.
        // Lo decide el puesto de la ventana (LocalSeat: -pad / -kbm, o los tags de MPPM).
        LocalSeat.Register(_assetInstance);

        _initialized = true;
        Local = this;

        // ADOPTAR EL ESTADO ACTUAL DEL ÁRBITRO, no asumir "modo juego".
        //
        // Arriba encendimos el mapa Player a ciegas, pero este jugador puede estar
        // naciendo con un menú YA abierto (la sala, el recuadro de red con ESC). Y al
        // revés: UICursor.Apply() no le pudo avisar a nadie mientras este proveedor no
        // existía, porque solo empuja el modo si Local != null — o sea que todos los
        // pedidos y sueltas que pasaron ANTES de este instante se perdieron.
        //
        // Volver a aplicar acá cierra esa ventana: el jugador recién nacido queda en el
        // modo que corresponda al conjunto de menús abiertos en este momento.
        UICursor.Apply();
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
            LocalSeat.Unregister(_assetInstance);
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
