using UnityEngine;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;

// ============================================================
// PlayerController
//
// Controla al personaje jugador: movimiento (CharacterController
// client-authoritative), input de habilidades, cambio de clase,
// animación, cámara y HUD propios. Solo el DUEÑO (IsOwner) procesa
// input y mueve el CharacterController — las copias remotas/servidor
// se mantienen sincronizadas por NetworkTransform y por las RPCs de
// NetworkAbilitySystemComponent.
//
// IMPORTANTE EN EL PREFAB: necesita el componente NetworkTransform
// (FishNet) configurado como Component Type: Transform, Smoothing:
// Interpolation, Send Rate: 10 o más — sin él la posición del
// jugador remoto no se sincroniza y aparece estático o errático.
// ============================================================
[RequireComponent(typeof(AbilitySystemComponent))]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInputProvider))]
public class PlayerController : NetworkBehaviour
{
    // =========================================================
    // COMPONENTES Y CONFIGURACIÓN
    // =========================================================

    private AbilitySystemComponent        ASC;
    private NetworkAbilitySystemComponent NetASC;
    private CharacterController           characterController;

    // Lee el input del jugador vía el Input System nuevo (acciones Move, Look,
    // PrimaryAttack, ...). Solo el dueño local lo inicializa (ver OnStartClient).
    private PlayerInputProvider           _input;

    // El jugador DUEÑO en este proceso (null en un servidor sin cliente). Lo usan
    // los sistemas que necesitan saber "de qué lado está quien mira esta pantalla",
    // como la invisibilidad (que oculta al invisible solo para sus enemigos).
    // Ver PlayerVisibility.
    public static PlayerController LocalPlayer { get; private set; }

    [Header("Cámara de Red")]
    // Prefab de cámara que se instancia solo para el dueño local (ver
    // OnStartClient).
    public GameObject CameraPrefab;

    [Header("Clase")]
    public CharacterClassDefinition   CurrentClassDef;
    // Todas las clases jugables posibles, usadas para sincronizar el
    // ÍNDICE de clase por red (ver ServerSetClass) en vez de la
    // referencia al asset.
    public CharacterClassDefinition[] MainBaseClasses;

    [Header("UI")]
    public Sprite CharacterIcon;

    [Header("Animación")]
    public Animator characterAnimator;

    [Header("Huesos")]
    public Transform MainHandSocket;
    public Transform OffHandSocket;

    private GameObject currentMainWeapon;
    private GameObject currentOffWeapon;
    private GameObject currentWeaponTrail;

    // Instancia del prefab de comportamientos de pasiva de la clase actual (ver
    // CharacterClassDefinition.PassiveBehaviorsPrefab). Se destruye al cambiar de clase.
    private GameObject currentPassiveBehaviors;

    // True mientras se está ejecutando una habilidad — bloquea input de
    // otras habilidades y el giro por movimiento normal.
    private bool isAttacking = false;

    // Cuándo se puso isAttacking=true, para el watchdog de abajo. Si una
    // habilidad no llama a FinishAttack (su corutina se interrumpió, etc.),
    // el jugador quedaría sin poder actuar; el watchdog lo resetea tras un
    // máximo razonable. No aplica al menú radial (que mantiene isAttacking a
    // propósito hasta soltar el botón).
    private float _attackStartTime;
    private const float MaxAttackSeconds = 5f;

    // Instancias de habilidad otorgadas, una por slot (ver
    // EquipCharacterClass). [HideInInspector] porque se llenan en
    // runtime, no se configuran a mano.
    [HideInInspector] public GameplayAbility MovementAbility;
    [HideInInspector] public GameplayAbility AbilityQ;
    [HideInInspector] public GameplayAbility AbilityE;
    [HideInInspector] public GameplayAbility AbilityR;
    [HideInInspector] public GameplayAbility PrimaryAttackAbility;
    [HideInInspector] public GameplayAbility AimAbility;

    [Header("Físicas")]
    public float jumpForce  = 8f;
    public float gravity    = -9.8f;

    [Header("Apuntado (Third Person Shooter)")]
    // Qué tan rápido el cuerpo gira para alinearse con el frente de la cámara.
    // Más alto = casi instantáneo; más bajo = giro más suave/perezoso.
    // Si algún día querés que el cuerpo SOLO gire mientras te movés o apuntás
    // (y no mientras estás quieto girando la cámara), ver FaceCameraForward.
    public float aimTurnSpeed = 15f;

    private float   verticalVelocity;
    private Vector3 spawnPosition;

    // Impulso temporal de movimiento genérico: cualquier habilidad
    // (salto, dash, empujón...) puede tomar control del desplazamiento
    // horizontal + un impulso vertical sin que este script tenga que
    // conocerla por nombre — ver ApplyAbilityVelocity()/ClearAbilityVelocity()
    // más abajo. El único que sabe qué habilidad la usó y qué hacer al
    // aterrizar es quien la disparó (NetworkAbilitySystemComponent).
    private Vector3 _abilityVelocity;
    private bool    _abilityVelocityActive;

    // Impulso de DASH en 3D: a diferencia de _abilityVelocity (horizontal +
    // gravedad, para el salto), este es un vector completo en cualquier
    // dirección —incluye arriba/abajo— y durante su vigencia NO se aplica
    // gravedad, para que el dash sea una línea recta hacia donde apunta el
    // jugador. Se atenúa solo (inercia). Ver ApplyDashVelocity/HandleMovementInput.
    private Vector3 _dashVelocity;
    private bool    _dashActive;

    // Último punto de mira que el dueño calculó con SU cámara y envió al
    // servidor junto con el input de habilidad. El servidor (y por lo tanto
    // las Abilities, que corren ahí) NO tiene una cámara de juego válida
    // propia, así que GetAimPoint() cae a este valor cuando IsOwner es falso.
    [HideInInspector] public Vector3 NetworkAimPoint;

    // Dirección de movimiento (WASD/stick en espacio de mundo, plano horizontal)
    // que el dueño tenía al activar la última habilidad. La usa la esquiva
    // direccional del dash. Igual que NetworkAimPoint: el servidor no tiene input
    // del dueño, así que este se la envía con el pedido (ver GetMoveDirection y
    // NetworkAbilitySystemComponent.ServerActivateAbility). Cero = quieto.
    [HideInInspector] public Vector3 NetworkMoveDirection;

    // Índice de clase sincronizado a los observadores remotos (ver
    // EquipCharacterClass/ServerSetClass) — mandamos el índice y no el
    // asset porque FishNet no serializa referencias a ScriptableObjects.
    private readonly SyncVar<int> _netClassIndex = new SyncVar<int>(-1);

    [HideInInspector] public bool isRadialMenuOpen = false;
    private GameplayAbility currentRadialAbility;

    // Menú de selección de clase (UI_InitialClassMenu) del dueño local. Se resuelve
    // al spawnear la cámara (OnStartClient); la acción ChangeClass lo reabre para
    // cambiar de clase en pleno juego. Solo el dueño lo tiene (null en las copias
    // remotas/servidor).
    private UI_InitialClassMenu _initialClassMenu;

    // Habilidad de zona (IGroundTargetAbility) que se está apuntando ahora mismo,
    // mientras se mantiene su botón. Ver UI_GroundTargetIndicator.
    private GameplayAbility _groundTargetAbility;
    private EAbilityInput   _groundTargetSlot;

    // True mientras se mantiene apretada una habilidad que se apunta antes de
    // lanzarse (menú radial o zona en el suelo). En ese estado isAttacking está en
    // true a propósito, pero HAY que seguir leyendo el input para detectar cuándo
    // se suelta el botón — y el watchdog no debe contar.
    private bool IsAimingAbility => isRadialMenuOpen || _groundTargetAbility != null;

    // Bloquea el input del dueño (movimiento y habilidades) mientras un menú
    // modal está abierto — ej. la selección de clase inicial (UI_InitialClassMenu).
    private bool _inputLocked;
    // La usan los menús modales para tomar/soltar el control del jugador.
    public void SetInputLocked(bool locked) => _inputLocked = locked;

    // =========================================================
    // CICLO DE VIDA DE RED
    // =========================================================

    // Cachea las referencias a los demás componentes del mismo GameObject.
    void Awake()
    {
        ASC                = GetComponent<AbilitySystemComponent>();
        NetASC             = GetComponent<NetworkAbilitySystemComponent>();
        characterController = GetComponent<CharacterController>();
        _input             = GetComponent<PlayerInputProvider>();

        // Los clips de ataque disparan Animation Events (AnimationEvent_EnableTrail,
        // AnimationEvent_DisableTrail, AnimationEvent_HitFrame) sobre el GameObject
        // que tiene el Animator — el modelo del personaje, NO esta raíz. Unity
        // busca el método receptor solo en los componentes de ESE GameObject, así
        // que necesita un PlayerAnimationEvents ahí (que reenvía a este script).
        // Lo agregamos por código para que funcione aunque no esté puesto a mano
        // en el prefab del modelo: si faltaba, la consola tiraba
        // "AnimationEvent 'AnimationEvent_EnableTrail' has no receiver!" y el
        // trail del arma nunca se activaba.
        if (characterAnimator != null &&
            characterAnimator.GetComponent<PlayerAnimationEvents>() == null)
        {
            characterAnimator.gameObject.AddComponent<PlayerAnimationEvents>();
        }
    }

    // Equipa la clase inicial (a todos los peers, para que la visual esté
    // bien en todos lados), y configura las cosas exclusivas del dueño
    // local: cámara propia y bloqueo de cursor.
    public override void OnStartClient()
    {
        base.OnStartClient();
        spawnPosition = transform.position;

        // Recibir los cambios de clase en runtime (evolución a subclase) para
        // actualizar los visuales/arma en los observadores. Faltaba este
        // suscribir (solo estaba el -= en OnStopClient), por eso los demás
        // jugadores nunca veían el cambio de arma del otro al evolucionar.
        _netClassIndex.OnChange += OnNetClassIndexChanged;

        // Seguro anti-errores: si el clon nace sin clase, forzamos la
        // primera clase disponible.
        if (CurrentClassDef == null && MainBaseClasses != null && MainBaseClasses.Length > 0)
        {
            CurrentClassDef = MainBaseClasses[0];
        }

        // Equipamos la clase a TODOS (dueños y clones) para que la visual
        // esté bien en todos lados.
        if (CurrentClassDef != null) EquipCharacterClass(CurrentClassDef);
        if (ASC != null) ASC.OnDeath += HandlePlayerDeath;

        // Cosas exclusivas del dueño local
        if (base.IsOwner)
        {
            LocalPlayer = this;

            // Habilitar el input del Input System nuevo solo para el dueño local.
            if (_input != null) _input.InitializeForOwner();

            if (Camera.main != null) Camera.main.gameObject.SetActive(false);

            if (CameraPrefab != null)
            {
                GameObject camObj = Instantiate(CameraPrefab);
                ThirdPersonOrbitCam cam = camObj.GetComponent<ThirdPersonOrbitCam>();
                if (cam != null)
                {
                    cam.Target = this.transform;
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }

                // El UI_ClassSelectionMenu (menú de subclase con tarjetas) vive
                // en ESTE prefab de cámara, que se instancia acá — DESPUÉS de
                // EquipCharacterClass/UpdateHUD. Por eso hay que engancharlo a
                // este jugador dueño acá y no en UpdateHUD: allá la cámara
                // todavía no existía y FindFirstObjectByType no lo encontraba
                // (por eso el menú nunca aparecía). GetComponentInChildren(true)
                // lo encuentra aunque su panel arranque inactivo.
                UI_ClassSelectionMenu classMenu = camObj.GetComponentInChildren<UI_ClassSelectionMenu>(true);
                if (classMenu != null) classMenu.InitializeMenu(this);

                // Menú de selección de clase INICIAL: aparece al conectarse y se
                // elige clickeando una tarjeta de las clases base (MainBaseClasses).
                // Bloquea el input hasta que el jugador elige. Vive en el mismo
                // prefab de cámara; GetComponentInChildren(true) lo encuentra aunque
                // su panel arranque inactivo.
                _initialClassMenu = camObj.GetComponentInChildren<UI_InitialClassMenu>(true);
                if (_initialClassMenu != null) _initialClassMenu.InitializeMenu(this);

                // La retícula (crosshair) vive en el Canvas del prefab "Player
                // Camera" (componente Reticle sobre un objeto de UI). Como esta
                // cámara solo se instancia para el dueño local, la retícula solo
                // la ve su dueño — no hace falta crearla por código acá.
            }
        }
        else
        {
            // Destroy() falla siempre acá: PlayerController tiene
            // [RequireComponent(typeof(CharacterController))], así que Unity
            // bloquea la destrucción ("Can't remove CharacterController
            // because PlayerController depends on it") y el componente se
            // queda vivo en todos los fantasmas remotos. Nada llama a
            // .Move() en una copia que no es dueña (guard de IsOwner en
            // Update()), así que NO hace falta desactivarlo.
            //
            // IMPORTANTE: no desactivar el CharacterController acá.
            // Es el único collider del jugador, y las habilidades de daño
            // lo usan como hitbox vía Physics.OverlapSphere/OverlapBox en
            // el servidor. En Host, el cliente del host trata a los jugadores
            // remotos como "no dueños" — si acá lo desactivábamos, esa misma
            // instancia (la que usa el servidor, porque host = server+client
            // en un solo proceso) quedaba sin collider, y el host nunca
            // detectaba ni dañaba a los demás jugadores.
        }
    }

    // Se desuscribe de eventos al despawnear (evita callbacks sobre un
    // objeto ya destruido).
    public override void OnStopClient()
    {
        base.OnStopClient();
        _netClassIndex.OnChange -= OnNetClassIndexChanged;
        if (ASC != null) ASC.OnDeath -= HandlePlayerDeath;
        if (LocalPlayer == this) LocalPlayer = null;
        if (base.IsOwner && _input != null) _input.ShutdownOwner();
    }

    // =========================================================
    // UNITY UPDATE — solo el dueño procesa input y movimiento
    // =========================================================

    void Update()
    {
        if (!IsOwner) return;

        // El input del dueño todavía no está listo (se habilita en OnStartClient).
        if (_input == null || !_input.IsReady) return;

        // Menú modal abierto (ej. selección de clase inicial): el jugador no se
        // mueve ni activa habilidades hasta cerrarlo (elegir una tarjeta).
        if (_inputLocked) return;

        // CHEAT (debug): subir al nivel máximo para disparar la selección de
        // subclase. Acción "Cheat" (Alt en teclado; Start/Menu en el mando).
        // Ver el mapa Player en InputSystem_Actions. OJO: el mando usa Start
        // (button 7), NO Back (button 6), porque button 6 es la Ultimate.
        if (_input.Cheat.WasPressedThisFrame())
        {
            if (NetASC != null) NetASC.ServerCheatMaxLevel();
        }

        // Cambiar de clase (acción ChangeClass: tecla C / D-pad arriba): reabre el
        // menú de clases base. El menú es modal (bloquea el input mientras está
        // abierto vía SetInputLocked), así que _inputLocked ya evita reabrirlo
        // encima. Al elegir, la clase nueva arranca en nivel 1 (ver
        // UI_InitialClassMenu.ConfirmSelection).
        if (_input.ChangeClass.WasPressedThisFrame() && _initialClassMenu != null)
        {
            _initialClassMenu.ReopenMenu();
        }

        // Watchdog: si isAttacking quedó trabado (una habilidad no reseteó el
        // estado — su corutina se interrumpió, no llegó el aviso de fin, etc.),
        // lo liberamos tras un máximo razonable para no dejar al jugador sin
        // poder actuar. El menú radial queda excluido (mantiene isAttacking a
        // propósito hasta que soltás el botón).
        if (isAttacking && !IsAimingAbility && Time.time - _attackStartTime > MaxAttackSeconds)
        {
            Debug.LogWarning("[PlayerController] isAttacking quedó trabado — reseteando (watchdog).");
            FinishAttack();
        }

        if (ASC.HasTag(EGameplayTag.State_Dead))
        {
            if (_input.Ability3.WasPressedThisFrame() && AbilityR != null && AbilityR.CanActivate())
                RequestAbility(EAbilityInput.Action3);
            return;
        }

        if (ASC.HasTag(EGameplayTag.State_Stunned)) return;

        HandleMovementInput();
        HandleAbilityInput();
        UpdateGroundTargetIndicator();

        // UpdateAnimations solo en el dueño — los observadores remotos ven
        // las animaciones sincronizadas por las ObserversRpc del NetworkASC.
        UpdateAnimations();
    }

    // =========================================================
    // MOVIMIENTO
    // =========================================================

    // Mueve el CharacterController según el input WASD (o el impulso de
    // una habilidad, si hay uno activo) más gravedad. Es el único lugar
    // que llama Move() — solo corre para el dueño (ver Update).
    private void HandleMovementInput()
    {
        if (ASC.HasTag(EGameplayTag.State_Rooted))
        {
            verticalVelocity += gravity * Time.deltaTime;
            characterController.Move(Vector3.up * verticalVelocity * Time.deltaTime);
            return;
        }

        if (characterController.isGrounded && verticalVelocity < 0)
            verticalVelocity = -2f;

        float baseSpeed = ASC.GetAttributeValue(EAttributeType.MovSpeed);
        if (baseSpeed <= 0) baseSpeed = 5f;

        Vector2 moveInput = _input.MoveValue;

        // Con el menú radial abierto y apuntando con el stick, el stick sirve para
        // ELEGIR en la rueda (ver UI_RadialMenu), no para mover al personaje.
        if (isRadialMenuOpen && _input.MoveIsGamepad) moveInput = Vector2.zero;

        Vector3 inputVec  = GetWASDInputVector(moveInput.x, moveInput.y);

        // Third-person shooter: el cuerpo SIEMPRE mira hacia donde apunta la
        // cámara, sin importar en qué dirección apunten las WASD. El
        // desplazamiento en 8 direcciones lo resuelve el blend tree 2D de la
        // animación (parámetros MoveX/MoveY, ver UpdateAnimations), NO la
        // rotación del personaje. Mientras se ejecuta una habilidad no tocamos
        // la rotación: varias (salto, dash, ataques) orientan al personaje a
        // propósito hacia el punto de mira.
        if (!isAttacking) FaceCameraForward();

        // Dash 3D (Dash siniestro): movimiento recto en la dirección apuntada,
        // incluyendo arriba/abajo, SIN gravedad durante el impulso. Se atenúa
        // solo (inercia) hasta que NetworkASC.DashRoutine llama ClearDashVelocity.
        // Sigue chocando con paredes/entorno (el CharacterController colisiona;
        // solo se excluye la capa de jugadores en GA_Dash).
        if (_dashActive)
        {
            _dashVelocity   = Vector3.Lerp(_dashVelocity, Vector3.zero, Time.deltaTime);
            Vector3 steer   = inputVec * baseSpeed;
            Vector3 dashMove = _dashVelocity + new Vector3(steer.x, 0, steer.z);
            characterController.Move(dashMove * Time.deltaTime);
            return;
        }

        Vector3 horizontal;

        if (_abilityVelocityActive)
        {
            _abilityVelocity = Vector3.Lerp(_abilityVelocity, Vector3.zero, Time.deltaTime);
            horizontal        = _abilityVelocity + inputVec * baseSpeed;
        }
        else
        {
            horizontal = inputVec * baseSpeed;
            if (characterController.isGrounded && _input.Jump.WasPressedThisFrame())
                verticalVelocity = jumpForce;
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 finalMove = new Vector3(horizontal.x, 0, horizontal.z)
                            + Vector3.up * verticalVelocity;
        characterController.Move(finalMove * Time.deltaTime);
    }

    // Convierte el input horizontal/vertical crudo en un vector de mundo
    // relativo a hacia dónde mira la cámara del dueño.
    private Vector3 GetWASDInputVector(float h, float v)
    {
        if (Camera.main == null) return Vector3.zero;
        Vector3 f = Camera.main.transform.forward; f.y = 0; f.Normalize();
        Vector3 r = Camera.main.transform.right;   r.y = 0; r.Normalize();
        return (f * v + r * h).normalized;
    }

    // Orienta el cuerpo hacia el frente horizontal (yaw) de la cámara. Es el
    // núcleo del giro estilo third-person shooter: el personaje siempre "ve"
    // hacia donde apunta la cámara y las WASD solo deciden en qué dirección se
    // desliza (lo anima el blend tree 2D de strafe). Solo yaw — nunca inclina
    // el cuerpo hacia arriba/abajo.
    //
    // Si en el futuro querés que el cuerpo gire SOLO cuando te movés o apuntás
    // (y no mientras estás quieto orbitando la cámara), envolvé la llamada en
    // HandleMovementInput con algo como: if (inputVec != Vector3.zero) ...
    private void FaceCameraForward()
    {
        if (Camera.main == null) return;
        Vector3 fwd = Camera.main.transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) return;

        Quaternion target = Quaternion.LookRotation(fwd.normalized);
        transform.rotation = Quaternion.Slerp(
            transform.rotation, target, aimTurnSpeed * Time.deltaTime);
    }

    // =========================================================
    // IMPULSO DE HABILIDAD (genérico — salto, dash, empujón, etc.)
    //
    // Este script NO sabe qué habilidad lo usa. GA_LeapAttack (vía
    // NetworkAbilitySystemComponent.TargetExecuteLeap, que corre en la
    // conexión DUEÑA) le calcula la dirección con SU cámara y llama acá.
    // Quien llama es también responsable de detectar el aterrizaje
    // (IsGrounded) y de avisarle al servidor cuándo resolver cualquier
    // efecto asociado — este script solo mueve el CharacterController.
    // =========================================================

    // True si el CharacterController está tocando el piso ahora mismo.
    public bool IsGrounded => characterController.isGrounded;

    // Toma control temporal del movimiento: aplica un impulso horizontal
    // (que se va atenuando solo) y uno vertical instantáneo. Funciona esté o no
    // en el piso (se puede usar en el aire — ej. el Salto Furioso o el dash).
    public bool ApplyAbilityVelocity(Vector3 horizontalVelocity, float verticalImpulse)
    {
        _abilityVelocity       = horizontalVelocity;
        verticalVelocity       = verticalImpulse;
        _abilityVelocityActive = true;

        Vector3 flatDir = new Vector3(horizontalVelocity.x, 0, horizontalVelocity.z);
        if (flatDir != Vector3.zero) transform.forward = flatDir.normalized;

        return true;
    }

    // Devuelve el movimiento al control normal del input del jugador.
    public void ClearAbilityVelocity()
    {
        _abilityVelocityActive = false;
        _abilityVelocity       = Vector3.zero;
    }

    // Toma control del movimiento para un DASH en 3D: impulso recto en cualquier
    // dirección (incluida la vertical) que se atenúa solo, SIN gravedad mientras
    // dura. Funciona en el aire. El fin lo maneja quien lo disparó llamando
    // ClearDashVelocity (ver NetworkAbilitySystemComponent.DashRoutine).
    public void ApplyDashVelocity(Vector3 velocity, bool faceVelocity = true)
    {
        _dashVelocity    = velocity;
        _dashActive      = true;
        // Durante el dash no hay gravedad; al terminar, la caída arranca de 0.
        verticalVelocity = 0f;

        // En la esquiva direccional (faceVelocity=false) NO giramos el cuerpo:
        // mantiene la orientación hacia la mira que RequestAbility ya fijó, así la
        // esquiva lateral/hacia atrás no te da vuelta.
        if (!faceVelocity) return;

        Vector3 flatDir = new Vector3(velocity.x, 0, velocity.z);
        if (flatDir.sqrMagnitude > 0.0001f) transform.forward = flatDir.normalized;
    }

    // Termina el dash 3D y devuelve el movimiento al control normal (con gravedad).
    public void ClearDashVelocity()
    {
        _dashActive   = false;
        _dashVelocity = Vector3.zero;
    }

    // Teletransporta instantáneamente a una posición (blink de Golpe mortal).
    // Desactiva el CharacterController un instante para mover el transform sin
    // que el propio CC lo bloquee, y orienta al personaje hacia faceDir. Debe
    // correr en el proceso DUEÑO (el CC es client-authoritative) — lo dispara
    // NetworkAbilitySystemComponent.TargetExecuteTeleport.
    public void TeleportTo(Vector3 position, Vector3 faceDir)
    {
        characterController.enabled = false;
        transform.position          = position;
        characterController.enabled = true;
        verticalVelocity            = 0f;

        faceDir.y = 0;
        if (faceDir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(faceDir.normalized);
    }

    // Excluye (o restaura) capas de colisión del CharacterController. Lo usa el
    // dash para atravesar a otros jugadores sin dejar de chocar con las paredes:
    // se excluye la capa de jugadores durante el impulso y se restaura al terminar
    // (ver GA_Dash / NetworkAbilitySystemComponent.TargetExecuteDash).
    public void SetCollisionExclusion(LayerMask mask, bool exclude)
    {
        if (exclude) characterController.excludeLayers |=  mask;
        else         characterController.excludeLayers &= ~mask;
    }

    // =========================================================
    // INPUT Y ACTIVACIÓN DE HABILIDADES
    // =========================================================

    // Revisa cada botón/tecla de habilidad y dispara su activación o
    // liberación (para las de tipo menú radial).
    private void HandleAbilityInput()
    {
        if (ASC.HasTag(EGameplayTag.State_Silenced)) return;
        if (isAttacking && !IsAimingAbility) return;

        CheckAbilityButton(_input.MovementAbility, MovementAbility,      EAbilityInput.Movement);
        CheckAbilityButton(_input.Ability1,        AbilityQ,             EAbilityInput.Action1);
        CheckAbilityButton(_input.Ability2,        AbilityE,             EAbilityInput.Action2);
        CheckAbilityButton(_input.Ability3,        AbilityR,             EAbilityInput.Action3);
        CheckAbilityButton(_input.PrimaryAttack,   PrimaryAttackAbility, EAbilityInput.PrimaryAttack);
        CheckAbilityButton(_input.Secondary,       AimAbility,           EAbilityInput.SecondaryAttack);
    }

    // Detecta presionar/soltar la ACCIÓN (Input System) asignada a una
    // habilidad. Al presionar la activa; al soltar cierra el menú radial (o la
    // habilidad de zona) si esta la tenía abierta. WasPressedThisFrame /
    // WasReleasedThisFrame son el equivalente del viejo GetButtonDown/GetButtonUp.
    private void CheckAbilityButton(UnityEngine.InputSystem.InputAction action, GameplayAbility ability, EAbilityInput slot)
    {
        if (ability == null || action == null) return;
        if (action.WasPressedThisFrame())
            ProcessAbilityPress(ability, slot);
        else if (action.WasReleasedThisFrame() && (currentRadialAbility == ability || _groundTargetAbility == ability))
            ProcessAbilityRelease();
    }

    // Al presionar el input de una habilidad: si es de menú radial, abre
    // el menú; si no, predice la animación localmente y pide su
    // activación al servidor.
    private void ProcessAbilityPress(GameplayAbility ability, EAbilityInput slot)
    {
        if (ability is IRadialMenuAbility radial)
        {
            if (!ability.CanActivate()) return;
            isAttacking          = true;
            _attackStartTime     = Time.time;
            isRadialMenuOpen     = true;
            currentRadialAbility = ability;
            if (UI_RadialMenu.Instance != null) UI_RadialMenu.Instance.Show(radial);
        }
        else if (ability is IGroundTargetAbility ground)
        {
            // Habilidad de zona: al presionar solo entramos en modo apuntado y
            // mostramos el marcador. Se lanza al SOLTAR (ver ProcessAbilityRelease).
            if (!ability.CanActivate()) return;
            isAttacking          = true;
            _attackStartTime     = Time.time;
            _groundTargetAbility = ability;
            _groundTargetSlot    = slot;

            if (UI_GroundTargetIndicator.Instance != null)
                UI_GroundTargetIndicator.Instance.Show(ground.TargetRadius);
        }
        else
        {
            if (ability.CanActivate())
            {
                isAttacking = true;
                _attackStartTime = Time.time;
                // Predicción local SOLO en un cliente remoto (no host). El
                // ObserversRpc del servidor se salta al dueño (asume que ya la
                // disparó acá), así que sin esta predicción el dueño remoto
                // nunca vería/animaría su propio ataque — de ahí que la
                // necesitemos.
                //
                // PERO en el host, servidor y dueño son el MISMO objeto:
                // ability.Activate() (que corre server-side este mismo frame)
                // ya llama PlayAnimation. Si además predijéramos acá, el
                // SetTrigger se dispararía DOS veces sobre el mismo Animator; el
                // segundo trigger queda buffeado y reproduce el ataque una
                // SEGUNDA vez solo, sin haber presionado nada.
                if (!IsServerInitialized)
                    PlayAnimation(ability.AnimationTriggerName, ability.AnimationID);
                RequestAbility(slot);
            }
        }
    }

    // Al soltar el input de una habilidad de menú radial: confirma la
    // selección hecha con el mouse y la manda al servidor.
    private void ProcessAbilityRelease()
    {
        if (currentRadialAbility is IRadialMenuAbility radial)
        {
            int     sel = UI_RadialMenu.Instance != null
                ? UI_RadialMenu.Instance.HideAndGetSelection() : -1;
            Vector3 pos = GetAimPoint(radial.MaxRadialRange);

            // Predicción local de la animación (mismo criterio que en
            // ProcessAbilityPress): solo en cliente remoto, porque en el host
            // la ActivateWithSelection del servidor ya la dispara. Sin esto,
            // las habilidades de menú radial no dispararían su animación en
            // ningún lado para el dueño remoto (no hay RPC de animación y el
            // Activate del servidor se saltea por el guard de PlayAnimation).
            if (sel != -1 && !IsServerInitialized)
                PlayAnimation(currentRadialAbility.AnimationTriggerName,
                              currentRadialAbility.AnimationID);

            ServerRequestRadialAbility(sel, pos);
        }
        else if (_groundTargetAbility != null)
        {
            // Zona confirmada: escondemos el marcador y la lanzamos por el camino
            // NORMAL — RequestAbility ya le manda el punto de mira al servidor
            // (NetworkAimPoint), que es justo la zona que el jugador venía viendo.
            if (UI_GroundTargetIndicator.Instance != null)
                UI_GroundTargetIndicator.Instance.Hide();

            // Predicción local de la animación, mismo criterio que arriba: solo en
            // cliente remoto (en el host la dispara el propio Activate del servidor).
            if (!IsServerInitialized)
                PlayAnimation(_groundTargetAbility.AnimationTriggerName, _groundTargetAbility.AnimationID);

            RequestAbility(_groundTargetSlot);
            _groundTargetAbility = null;
        }

        isRadialMenuOpen     = false;
        currentRadialAbility = null;
        // Reiniciar el reloj del watchdog: recién ahora isAttacking pasa a
        // contar para el timeout (mientras se apuntaba no contaba).
        _attackStartTime     = Time.time;
    }

    // Mientras se mantiene una habilidad de zona, el marcador sigue la mira
    // (recortado a su alcance, para que la vista previa coincida con lo que el
    // servidor va a hacer de verdad). Solo corre en el dueño (ver Update).
    private void UpdateGroundTargetIndicator()
    {
        if (_groundTargetAbility is not IGroundTargetAbility ground) return;
        if (UI_GroundTargetIndicator.Instance == null) return;

        UI_GroundTargetIndicator.Instance.UpdatePosition(GetClampedAimPoint(ground.MaxTargetRange));
    }

    // Punto de mira recortado a maxRange desde el jugador.
    private Vector3 GetClampedAimPoint(float maxRange)
    {
        Vector3 point  = GetAimPoint(maxRange);
        Vector3 offset = point - transform.position;
        if (offset.magnitude > maxRange) point = transform.position + offset.normalized * maxRange;
        return point;
    }

    // Calcula el punto de mira y rota al dueño hacia él ANTES de pedirle
    // al servidor que active la habilidad (para que la rotación correcta
    // llegue a tiempo — ver comentario abajo), y manda la petición.
    private void RequestAbility(EAbilityInput slot)
    {
        if (NetASC != null)
        {
            // Calculamos el punto de mira AQUÍ, en el dueño, donde Camera.main
            // sí es la cámara correcta, y lo mandamos junto con el input.
            Vector3 aimPoint = GetAimPoint();

            // Dirección de movimiento actual (WASD/stick en mundo) para la esquiva
            // direccional del dash. El servidor no la puede leer, así que viaja con
            // el pedido (ver GetMoveDirection).
            Vector3 moveDir = GetMoveDirection();

            // Rotamos también en el cliente dueño AHORA. El NetworkTransform es
            // client-authoritative: si solo rotamos en el servidor (dentro de la
            // Ability), el próximo paquete de transform que mande este mismo
            // cliente (con su rotación vieja) lo pisa antes de que se calcule
            // el daño. Rotando acá, la rotación correcta es la que se sincroniza.
            RotateToAim(aimPoint);

            NetASC.ServerRequestActivateAbility(slot, aimPoint, moveDir);
        }
        else
            ActivateAbilityBySlot(slot); // Fallback singleplayer
    }

    // Ejecuta en el servidor la habilidad de menú radial elegida y replica su
    // animación a los observadores (el dueño ya la predijo en ProcessAbilityRelease).
    [ServerRpc]
    private void ServerRequestRadialAbility(int selectedIndex, Vector3 targetPosition)
    {
        foreach (var ability in ASC.GrantedAbilities)
        {
            if (ability is IRadialMenuAbility radial)
            {
                if (!ability.CanActivate()) return;
                radial.ActivateWithSelection(selectedIndex, targetPosition);

                if (selectedIndex != -1 && NetASC != null)
                    NetASC.ServerBroadcastAbilityAnimation(ability.AnimationTriggerName, ability.AnimationID);
                return;
            }
        }
    }

    // Activa directamente la habilidad de un slot, sin pasar por red
    // (fallback cuando no hay NetworkAbilitySystemComponent).
    private void ActivateAbilityBySlot(EAbilityInput slot)
    {
        GameplayAbility ability = slot switch
        {
            EAbilityInput.PrimaryAttack   => PrimaryAttackAbility,
            EAbilityInput.SecondaryAttack => AimAbility,
            EAbilityInput.Action1         => AbilityQ,
            EAbilityInput.Action2         => AbilityE,
            EAbilityInput.Action3         => AbilityR,
            EAbilityInput.Movement        => MovementAbility,
            _                             => null
        };
        if (ability != null && ability.CanActivate()) ability.Activate();
    }

    // Libera el estado "atacando" — lo llama GameplayAbility.EndAbility()
    // (directo o vía RPC) al terminar una habilidad.
    public void FinishAttack() => isAttacking = false;

    // =========================================================
    // MUERTE Y RESPAWN
    // =========================================================

    // Reacciona a la muerte del personaje: si tiene la resurrección de
    // Inmortal disponible la usa, si no pide el respawn normal.
    private void HandlePlayerDeath()
    {
        // ASC.OnDeath se suscribe en OnStartClient() para TODOS los peers
        // (dueño, servidor y observadores remotos), porque cada uno tiene
        // su propia copia local del ASC que dispara Die() cuando su salud
        // llega a 0 (el servidor lo dispara directo al aplicar el daño; el
        // dueño remoto lo vuelve a disparar un instante después, al recibir
        // el SyncVar de vida en 0). Sin este guard, ServerRequestRespawn()
        // se llama dos veces por muerte: una directa en el servidor y otra
        // vía RPC desde el cliente dueño, duplicando el respawn.
        if (!IsServerInitialized) return;

        if (AbilityR is GA_ImmortalWrath && AbilityR.CanActivate())
        {
            // Activación DIRECTA server-side: HandlePlayerDeath ya corre en el
            // servidor. NO usamos RequestAbility porque ese va por el [ServerRpc]
            // de dueño, que el servidor no puede invocar sobre un cliente remoto
            // → el Inmortal remoto no revivía ni respawneaba. El aimPoint no
            // importa para un auto-revive.
            if (NetASC != null) NetASC.ServerActivateAbility(EAbilityInput.Action3, transform.position);
            else                ActivateAbilityBySlot(EAbilityInput.Action3); // fallback singleplayer
            return;
        }
        ServerRequestRespawn();
    }

    // Pide el respawn al NetworkGameManager de la escena (o hace un
    // respawn simple si no hay uno). Es [Server], NO [ServerRpc]: lo llama
    // HandlePlayerDeath, que ya corre solo en el servidor. Con [ServerRpc]
    // fallaba para los jugadores NO-host: un ServerRpc requiere que lo invoque
    // el DUEÑO, y el servidor no es dueño de un cliente remoto → FishNet lo
    // bloqueaba y ese jugador nunca respawneaba (parecía "no poder morir").
    [Server]
    private void ServerRequestRespawn()
    {
        NetworkGameManager gm = FindFirstObjectByType<NetworkGameManager>();
        if (gm != null)
            gm.RespawnPlayer(Owner, 3f);
        else
            StartCoroutine(SimpleServerRespawn(3f));
    }

    // Respawn de emergencia (sin NetworkGameManager en la escena): espera,
    // reposiciona en el punto de spawn original, y revive.
    private System.Collections.IEnumerator SimpleServerRespawn(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Igual que en NetworkGameManager: el transform es client-authoritative, así
        // que el teletransporte lo ejecuta el DUEÑO (si lo escribiéramos acá, en el
        // servidor, el próximo paquete del dueño lo pisaría).
        Vector3 respawnPos = new Vector3(spawnPosition.x, 3f, spawnPosition.z);
        if (NetASC != null) NetASC.ServerTeleportOwnerTo(respawnPos, transform.forward);
        else                TeleportTo(respawnPos, transform.forward); // sin red

        ASC.Revive();
    }

    // Teletransporta al personaje de vuelta a su punto de spawn (ej: al
    // caer a un DeathZone), sin pasar por el flujo de muerte/revivir.
    public void TeleportToSpawn()
    {
        characterController.enabled = false;
        transform.position = new Vector3(spawnPosition.x, 3f, spawnPosition.z);
        verticalVelocity   = 0f;
        characterController.enabled = true;
    }

    // =========================================================
    // CLASE Y EQUIPAMIENTO
    // =========================================================

    // Cambia la clase del personaje: limpia estado anterior, actualiza
    // visuales/armas, otorga las nuevas habilidades por slot, recarga
    // atributos base, y (si sos el dueño) sincroniza el cambio por red y
    // refresca el HUD.
    // resetProgress=true reinicia nivel/exp a 1/0 (empezás la clase nueva desde
    // cero) — lo usa el cambio manual de clase desde el menú. En false (por
    // defecto) conserva el progreso, que es lo correcto al evolucionar a subclase
    // o al equipar la clase inicial (que ya está en nivel 1).
    public void EquipCharacterClass(CharacterClassDefinition newClass, bool resetProgress = false)
    {
        if (newClass == null || ASC == null) return;

        ASC.RemoveAllActiveEffects();
        CurrentClassDef  = newClass;
        ASC.CurrentClass = newClass;
        CharacterIcon    = newClass.ClassIcon;

        // Comportamientos de código propios de la clase (PassiveBehaviorsPrefab):
        // se instancian como hijo del jugador y se destruyen al cambiar de clase.
        SetupPassiveBehaviors(newClass);

        // Armas equipadas + animator override de la clase. Esta llamada se había
        // perdido: por eso el dueño/host no veía las armas ni le cambiaban las
        // animaciones (ej. el bárbaro). Corre para el dueño y para la copia del
        // servidor de un remoto; los observadores puros lo reciben por
        // OnNetClassIndexChanged.
        UpdateVisuals(newClass);

        ASC.ClearGrantedAbilities();
        MovementAbility = AbilityQ = AbilityE = AbilityR =
            PrimaryAttackAbility = AimAbility = null;

        foreach (var assignment in newClass.Abilities)
        {
            if (assignment.Ability == null) continue;
            GameplayAbility inst = ASC.GrantAbility(assignment.Ability);
            switch (assignment.InputSlot)
            {
                case EAbilityInput.PrimaryAttack:   PrimaryAttackAbility = inst; break;
                case EAbilityInput.SecondaryAttack: AimAbility           = inst; break;
                case EAbilityInput.Action1:         AbilityQ             = inst; break;
                case EAbilityInput.Action2:         AbilityE             = inst; break;
                case EAbilityInput.Action3:         AbilityR             = inst; break;
                case EAbilityInput.Movement:        MovementAbility      = inst; break;
            }
        }

        if (newClass.BaseAttributes != null)
        {
            ASC.CharacterRoleDefinition = newClass.BaseAttributes;
            ASC.InitializeAttributes(keepProgress: !resetProgress);
        }

        // Pasivas de la clase (GEs siempre activos, ej: Ataque Furtivo del Pícaro).
        // Se aplican DESPUÉS de InitializeAttributes (que resetea los modificadores)
        // y solo en el lado autoritativo: el servidor los aplica y sus tags/stats
        // se sincronizan a los clientes por los canales normales del NetworkASC. En
        // una escena sin red (IsSpawned=false) se aplican localmente.
        if (!IsSpawned || IsServerInitialized)
            ApplyClassPassives(newClass);

        if (IsOwner)
        {
            int idx = GetClassIndex(newClass);
            if (idx >= 0) ServerSetClass(idx, resetProgress);
            else
                // La clase no está en la lista plana (no es alcanzable desde
                // MainBaseClasses vía AvailableSubclasses). Sin sincronizar,
                // el servidor se queda con la clase vieja y después tira
                // "No se encontró habilidad en slot ..." al activar una
                // habilidad nueva de esta clase. Revisar que newClass sea
                // subclase (directa o indirecta) de alguna de MainBaseClasses.
                Debug.LogError($"[PlayerController] '{newClass.ClassName}' no está en " +
                               $"MainBaseClasses ni en sus subclases — no se puede sincronizar " +
                               $"la clase al servidor. Las habilidades de esta clase fallarán en red.");
            UpdateHUD();
        }
    }

    // Aplica los GEs pasivos de la clase (CharacterClassDefinition.PassiveEffects).
    // RemoveAllActiveEffects() al inicio de EquipCharacterClass ya limpió las
    // pasivas de la clase anterior, así que no se acumulan al evolucionar.
    private void ApplyClassPassives(CharacterClassDefinition cls)
    {
        if (cls.PassiveEffects == null) return;
        foreach (var passive in cls.PassiveEffects)
            if (passive != null) ASC.ApplyGameplayEffect(passive, ASC);
    }

    // Destruye los comportamientos de código de la clase anterior e instancia los
    // de la nueva (ver CharacterClassDefinition.PassiveBehaviorsPrefab). Corre en
    // TODOS los peers (igual que otorgar habilidades): así el servidor tiene la
    // lógica que reacciona a sus eventos (ej. IllusoryBladesPassive escuchando
    // OnDealtDamage) y cada cliente la suya si la necesita. Los componentes del
    // prefab llegan al ASC del jugador con GetComponentInParent, ya que quedan como
    // hijos de este GameObject.
    private void SetupPassiveBehaviors(CharacterClassDefinition cls)
    {
        if (currentPassiveBehaviors != null)
        {
            Destroy(currentPassiveBehaviors);
            currentPassiveBehaviors = null;
        }

        if (cls.PassiveBehaviorsPrefab != null)
        {
            currentPassiveBehaviors = Instantiate(cls.PassiveBehaviorsPrefab, transform);
            currentPassiveBehaviors.transform.localPosition = Vector3.zero;
            currentPassiveBehaviors.transform.localRotation = Quaternion.identity;
        }
    }

    // Sincroniza el índice de clase elegido al SyncVar y, en la copia del
    // SERVIDOR de un jugador remoto, equipa la clase completa (habilidades
    // incluidas). Sin ese re-equip en el servidor, este seguía con las
    // habilidades de la clase vieja y tiraba "No se encontró habilidad en
    // slot ..." al intentar activar una habilidad de la subclase nueva.
    [ServerRpc]
    private void ServerSetClass(int classIndex, bool resetProgress)
    {
        _netClassIndex.Value = classIndex;

        // Si somos el dueño (host), ya lo equipamos localmente en
        // EquipCharacterClass. Para un jugador remoto, esta copia server-side
        // necesita el equip completo (otorga las habilidades que después
        // FindAbilityBySlot busca). resetProgress viaja igual que en el dueño para
        // que el reinicio a nivel 1 sea autoritativo (los atributos son
        // server-authoritative y se sincronizan de vuelta).
        if (!IsOwner)
        {
            CharacterClassDefinition def = GetClassByIndex(classIndex);
            if (def != null) EquipCharacterClass(def, resetProgress);
        }
    }

    // En los CLIENTES-observadores puros, aplica los visuales de la clase que
    // el dueño equipó. El dueño ya equipó localmente (se saltea con IsOwner) y
    // el servidor lo hizo en ServerSetClass (se saltea con IsServerInitialized,
    // así no duplicamos el UpdateVisuals ahí).
    private void OnNetClassIndexChanged(int prev, int next, bool asServer)
    {
        if (IsOwner || IsServerInitialized) return;
        CharacterClassDefinition def = GetClassByIndex(next);
        if (def != null) UpdateVisuals(def);
    }

    // Lista plana de TODAS las clases (base + subclases, recursivo) usada para
    // sincronizar el cambio de clase por índice. Se construye igual en todos
    // los peers (mismos assets, mismo MainBaseClasses), así que el índice
    // significa lo mismo para todos. Antes se usaba MainBaseClasses directo,
    // que NO incluye las subclases → al evolucionar, GetClassIndex daba -1 y el
    // cambio (arma incluida) nunca se sincronizaba.
    private List<CharacterClassDefinition> _allClasses;
    private List<CharacterClassDefinition> AllClasses
    {
        get
        {
            if (_allClasses == null)
            {
                _allClasses = new List<CharacterClassDefinition>();
                if (MainBaseClasses != null)
                    foreach (var c in MainBaseClasses) AddClassRecursive(c);
            }
            return _allClasses;
        }
    }

    // Agrega una clase y sus subclases (en profundidad) a _allClasses, sin
    // duplicar. El orden es determinístico, así que el índice es estable
    // entre peers.
    private void AddClassRecursive(CharacterClassDefinition c)
    {
        if (c == null || _allClasses.Contains(c)) return;
        _allClasses.Add(c);
        if (c.AvailableSubclasses != null)
            foreach (var sub in c.AvailableSubclasses) AddClassRecursive(sub);
    }

    // Índice de una clase en la lista plana (para mandarlo por red). -1 si no
    // está (no debería pasar si la clase deriva de alguna base).
    private int GetClassIndex(CharacterClassDefinition def) => AllClasses.IndexOf(def);

    // Inverso: resuelve un índice recibido por red de vuelta a la clase real.
    private CharacterClassDefinition GetClassByIndex(int idx)
    {
        if (idx < 0 || idx >= AllClasses.Count) return null;
        return AllClasses[idx];
    }

    // Reemplaza el animator override y las armas equipadas según la
    // clase dada. La llaman tanto el dueño (al equipar) como los
    // observadores remotos (al recibir el cambio de clase por red).
    private void UpdateVisuals(CharacterClassDefinition newClass)
    {
        if (newClass.ClassAnimatorOverride != null && characterAnimator != null)
            characterAnimator.runtimeAnimatorController = newClass.ClassAnimatorOverride;

        if (currentMainWeapon != null) Destroy(currentMainWeapon);
        if (currentOffWeapon  != null) Destroy(currentOffWeapon);

        if (newClass.MainHandWeaponPrefab != null && MainHandSocket != null)
        {
            currentMainWeapon = Instantiate(newClass.MainHandWeaponPrefab, MainHandSocket);
            currentMainWeapon.transform.SetLocalPositionAndRotation(
                Vector3.zero, Quaternion.identity);
            Transform trail = currentMainWeapon.transform.Find("WeaponTrail");
            if (trail != null)
            {
                currentWeaponTrail = trail.gameObject;
                currentWeaponTrail.SetActive(false);
            }
        }

        if (newClass.OffHandWeaponPrefab != null && OffHandSocket != null)
        {
            currentOffWeapon = Instantiate(newClass.OffHandWeaponPrefab, OffHandSocket);
            currentOffWeapon.transform.SetLocalPositionAndRotation(
                Vector3.zero, Quaternion.identity);
        }
    }

    // =========================================================
    // ANIMACIÓN
    // =========================================================

    // Actualiza los parámetros del Animator (velocidad, salto,
    // multiplicador de velocidad de ataque) según el movimiento actual.
    // Solo corre en el dueño (ver Update).
    void UpdateAnimations()
    {
        if (characterAnimator == null) return;

        Vector3 flatVel = new Vector3(
            characterController.velocity.x, 0,
            characterController.velocity.z);
        float speed = flatVel.magnitude;

        characterAnimator.SetFloat("Speed", speed, 0.1f, Time.deltaTime);

        // Strafe en 8 direcciones: tomamos la velocidad REAL del personaje y la
        // pasamos a su espacio LOCAL (relativo a hacia dónde mira: la cámara).
        // - MoveX  > 0 = se desliza a la derecha,  < 0 = a la izquierda
        // - MoveY  > 0 = hacia adelante,           < 0 = hacia atrás
        // La normalizamos por la velocidad máxima para que quede en ~[-1, 1] y
        // así encaje con las posiciones del blend tree 2D (Freeform Directional).
        // El body no rota con las WASD (siempre mira a la cámara), por eso el
        // blend tree es lo que muestra caminar de lado / de espaldas / diagonal.
        float maxSpeed = ASC.GetAttributeValue(EAttributeType.MovSpeed);
        if (maxSpeed <= 0) maxSpeed = 5f;
        Vector3 localVel = transform.InverseTransformDirection(flatVel) / maxSpeed;
        characterAnimator.SetFloat("MoveX", localVel.x, 0.1f, Time.deltaTime);
        characterAnimator.SetFloat("MoveY", localVel.z, 0.1f, Time.deltaTime);

        characterAnimator.SetBool("IsJumping", !characterController.isGrounded);

        float spd = ASC.GetAttributeValue(EAttributeType.AtkSpeed);
        if (spd > 0)
            characterAnimator.SetFloat("AttackSpeedMult", 1f / spd);
    }

    // Dispara un trigger de animación con el ID de acción dado (usado por
    // las habilidades para animar el ataque correspondiente).
    public void PlayAnimation(string trigger, int actionID)
    {
        if (characterAnimator == null || string.IsNullOrEmpty(trigger)) return;

        // Este método solo dispara la animación en la copia DUEÑA. Para los
        // observadores, NetworkAbilitySystemComponent.ObserversPlayAbilityAnimation
        // la replica aparte (el NetworkAnimator no sincroniza triggers).
        //
        // El guard es clave: ability.Activate() corre en el SERVIDOR y llama a
        // PlayAnimation sobre la copia server-side del jugador. En el host, que
        // renderiza esa copia, eso se sumaba al ObserversRpc y mostraba la
        // animación del otro jugador dos veces. Con el guard, el Activate()
        // server-side no toca copias ajenas. IsSpawned lo deja pasar en escenas
        // sin red (pruebas locales sin NetworkObject spawneado).
        if (IsSpawned && !IsOwner) return;

        characterAnimator.SetInteger("ActionID", actionID);
        characterAnimator.SetTrigger(trigger);
    }

    // Gancho de Animation Event, sin uso actualmente (reservado para
    // lógica en el frame exacto de impacto de la animación).
    public void AnimationEvent_HitFrame()    { }

    // Gancho de Animation Event: prende/apaga el trail del arma principal
    // en el frame exacto que marque el clip de animación.
    public void AnimationEvent_EnableTrail()
    {
        if (currentWeaponTrail == null) return;
        currentWeaponTrail.SetActive(true);

        // Limpiar cada TrailRenderer al activarlo, para que no dibuje una
        // "raya" recta desde la última posición que tenía (antes de
        // reactivarse) hasta la posición actual del arma.
        foreach (var tr in currentWeaponTrail.GetComponentsInChildren<TrailRenderer>(true))
            tr.Clear();
    }
    public void AnimationEvent_DisableTrail()
    {
        if (currentWeaponTrail != null) currentWeaponTrail.SetActive(false);
    }

    // =========================================================
    // HUD
    // =========================================================

    // Conecta el UI_PlayerHUD de la escena a este personaje (solo si sos
    // el dueño). Si el HUD todavía no existe (carga de escena en curso),
    // reintenta unos frames en vez de fallar directamente.
    private void UpdateHUD()
    {
        if (!base.IsOwner) return;

        UI_PlayerHUD hud = FindFirstObjectByType<UI_PlayerHUD>();
        if (hud != null)
        {
            hud.InitializeHUD(this);
            return;
        }

        // Cuando el jugador se spawnea, OnStartClient/EquipCharacterClass
        // corren en el MISMO frame en que FishNet instancia el objeto. Si
        // el UI_PlayerHUD de la escena todavía no terminó su Awake/Start
        // (o está en otra escena que aún está cargando), FindFirstObjectByType
        // devuelve null y el HUD se quedaría vacío para siempre porque
        // EquipCharacterClass no se vuelve a llamar — por eso reintentamos.
        StartCoroutine(RetryUpdateHUD());
    }

    // Reintenta encontrar el UI_PlayerHUD durante unos frames (ver
    // UpdateHUD) antes de rendirse.
    private System.Collections.IEnumerator RetryUpdateHUD()
    {
        const int   maxAttempts = 30;   // ~0.5s a 60fps
        int         attempts    = 0;

        while (attempts < maxAttempts)
        {
            yield return null;

            UI_PlayerHUD hud = FindFirstObjectByType<UI_PlayerHUD>();
            if (hud != null)
            {
                hud.InitializeHUD(this);
                yield break;
            }

            attempts++;
        }

        Debug.LogWarning("[PlayerController] No se encontró UI_PlayerHUD tras varios intentos.");
    }

    // =========================================================
    // APUNTADO
    // =========================================================

    // Calcula hacia dónde apunta el dueño (centro de su cámara) hasta
    // maxRange, ignorando triggers y su propio cuerpo. Si no sos el
    // dueño (ej: esta copia corre en el servidor o es un observador
    // remoto), no hay Camera.main válida para este proceso — se usa el
    // último punto que el dueño calculó y envió (NetworkAimPoint).
    public Vector3 GetAimPoint(float maxRange = 100f)
    {
        if (!IsOwner) return NetworkAimPoint;

        if (Camera.main == null)
            return transform.position + transform.forward * 10f;

        Ray          ray  = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit[] hits = Physics.RaycastAll(ray, maxRange);
        float   bestDist  = float.MaxValue;
        Vector3 bestPoint = ray.GetPoint(maxRange);

        foreach (var h in hits)
        {
            if (!h.collider.isTrigger
                && h.collider.transform.root != transform.root
                && h.distance < bestDist)
            {
                bestDist  = h.distance;
                bestPoint = h.point;
            }
        }
        return bestPoint;
    }

    // Dirección de movimiento del dueño (WASD/stick) en espacio de mundo, plano
    // horizontal y normalizada; Vector3.zero si está quieto. En el dueño se lee
    // en vivo del input; en el servidor/observadores (IsOwner falso) no hay input
    // que leer, así que se usa el último valor que el dueño envió
    // (NetworkMoveDirection) — mismo patrón que GetAimPoint. La usa la esquiva
    // direccional del dash (GA_Dash).
    public Vector3 GetMoveDirection()
    {
        if (!IsOwner) return NetworkMoveDirection;
        if (_input == null || !_input.IsReady) return Vector3.zero;
        Vector2 mv = _input.MoveValue;
        return GetWASDInputVector(mv.x, mv.y);
    }

    // Rota al personaje para mirar hacia GetAimPoint().
    public void RotateToAim() => RotateToAim(GetAimPoint());

    // Rota al personaje para mirar hacia un punto específico (sin
    // inclinar en el eje vertical).
    public void RotateToAim(Vector3 aimPoint)
    {
        Vector3 dir = (aimPoint - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero) transform.rotation = Quaternion.LookRotation(dir);
    }

    // =========================================================
    // UTILIDADES PÚBLICAS
    // =========================================================

    // Devuelve el GameObject del arma principal actualmente equipada
    // (lo usan los proyectiles para clonar su visual).
    public GameObject GetCurrentMainWeapon() => currentMainWeapon;

    // =========================================================
    // GIZMOS — vista previa de las áreas de habilidad en el Editor
    //
    // Lee CurrentClassDef directo (no las instancias otorgadas en runtime),
    // así funciona con el jugador seleccionado en la Scene view SIN
    // necesidad de darle Play — ajustás Range/AbilityRadius/ConeAngle/etc.
    // en el asset de la habilidad y ves el área real actualizarse ahí mismo.
    // =========================================================
    private void OnDrawGizmosSelected()
    {
        if (CurrentClassDef == null || CurrentClassDef.Abilities == null) return;

        foreach (var assignment in CurrentClassDef.Abilities)
            assignment.Ability?.DrawGizmos(transform);
    }
}
