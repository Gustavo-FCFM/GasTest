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

    [Tooltip("Nombre del clip PLACEHOLDER que ocupa el estado genérico de acción en el controller " +
             "BASE (AC_Player). Es la 'ranura' donde se mete el clip de cada habilidad que tenga " +
             "AnimationClip asignado, así no hace falta un estado por ataque.")]
    public string ActionClipSlotName = "PLACEHOLDER_Action";

    [Tooltip("Valor de ActionID que lleva al estado genérico de acción (la transición desde AnyState " +
             "de ese estado se filtra por este número).")]
    public int ActionClipStateID = 99;

    [Tooltip("Trigger que dispara el estado genérico de acción.")]
    public string ActionClipTrigger = "AttackTrigger";

    [Tooltip("Parámetro float que controla la VELOCIDAD del estado genérico de acción. Es propio y " +
             "NO se toca cada frame (a diferencia de AttackSpeedMult, que UpdateAnimations reescribe " +
             "siempre): se fija una vez al activar la habilidad, con la velocidad que hace que el clip " +
             "entre justo en el ritmo de ataque (ver GameplayAbility.ResolveAnimationSpeed).")]
    public string ActionSpeedParam = "ActionSpeedMult";

    // =========================================================
    // RANURAS DE "MANTENER" (habilidades IHoldAbility)
    //
    // Mismo principio que la ranura de acción de arriba, pero en TRES tiempos:
    // levantar (one-shot) → mantener (bucle) → bajar (one-shot). Los clips viven en
    // el asset de la habilidad y se meten en runtime en estos placeholders, así no
    // hace falta un estado por habilidad ni tocar el override de cada clase.
    //
    // SETUP MÍNIMO EN EL ANIMATOR BASE (AC_Player) — UN SOLO ESTADO:
    //   · Parámetros: bool "IsHolding", trigger "HoldTrigger", int "ActionID" (ya está).
    //   · 1 estado con el clip placeholder HoldLoopSlotName.
    //   · AnyState  → HoldLoop:   ActionID == HoldStateID  +  trigger HoldTrigger.
    //   · HoldLoop  → Locomotion: IsHolding == false  (SIN exit time: tiene que salir ya).
    //
    // Con eso alcanza y sobra: el escudo se levanta y se baja sin más. Las ranuras de
    // LEVANTAR y BAJAR son OPCIONALES — solo hacen falta si querés esas dos poses de
    // transición, y se agregan después sin tocar código:
    //   · HoldStart entre AnyState y HoldLoop (HoldStart → HoldLoop por Exit Time).
    //   · HoldEnd después del bucle (HoldLoop → HoldEnd con IsHolding == false, y
    //     HoldEnd → Locomotion por Exit Time).
    //
    // OPCIONAL TAMBIÉN — reacción al recibir un golpe EN el escudo:
    //   · 1 estado con el clip placeholder HoldImpactSlotName.
    //   · AnyState    → HoldImpact: ActionID == HoldImpactStateID + trigger HoldImpactTrigger.
    //   · HoldImpact  → HoldLoop:   por Exit Time, con IsHolding == true.
    //   · HoldImpact  → Locomotion: IsHolding == false (por si te bajan el escudo en pleno golpe).
    //
    // Lo único IMPRESCINDIBLE es la ranura del bucle. Si falta, se avisa una vez por
    // consola y la habilidad funciona igual, solo que sin animación.
    // =========================================================

    [Header("Animación — Mantener (escudo, canalizados)")]
    [Tooltip("Clip placeholder del estado 'mantener' (en bucle) en el controller BASE. Es el " +
             "ÚNICO obligatorio de los tres.")]
    public string HoldLoopSlotName  = "PLACEHOLDER_HoldLoop";

    [Tooltip("OPCIONAL: clip placeholder del estado 'levantar'. Vacío (o sin el estado en el " +
             "Animator) = se entra directo al bucle.")]
    public string HoldStartSlotName = "PLACEHOLDER_HoldStart";

    [Tooltip("OPCIONAL: clip placeholder del estado 'bajar'. Vacío (o sin el estado en el " +
             "Animator) = se sale del bucle directo a la locomoción.")]
    public string HoldEndSlotName   = "PLACEHOLDER_HoldEnd";

    [Tooltip("OPCIONAL: clip placeholder de la reacción al recibir un golpe mientras bloqueás " +
             "(el escudo 'acusa' el impacto). Vacío = sin reacción.")]
    public string HoldImpactSlotName = "PLACEHOLDER_HoldImpact";

    [Tooltip("Valor de ActionID que lleva al estado de mantener (la transición desde AnyState " +
             "se filtra por este número). Tiene que ser distinto de ActionClipStateID.")]
    public int HoldStateID = 98;

    [Tooltip("Valor de ActionID que lleva al estado de reacción al golpe en el escudo. Tiene que " +
             "ser distinto de ActionClipStateID y de HoldStateID.")]
    public int HoldImpactStateID = 97;

    [Tooltip("Trigger que dispara la entrada al mantenido.")]
    public string HoldTrigger = "HoldTrigger";

    [Tooltip("Trigger que dispara la reacción al golpe en el escudo.")]
    public string HoldImpactTrigger = "HoldImpactTrigger";

    [Tooltip("Parámetro BOOL que sostiene el bucle: mientras esté en true el personaje se queda " +
             "en el estado de mantener; al ponerse en false sale (al estado de bajar si existe, " +
             "o directo a la locomoción).")]
    public string HoldingParam = "IsHolding";

    // Copia en RUNTIME del controller (base + overrides de la clase). Es la que
    // permite intercambiar el clip de la ranura de acción sin tocar los assets.
    private AnimatorOverrideController _runtimeAnimator;
    // Clip que está puesto ahora en la ranura, para no re-asignarlo si no cambió
    // (asignar un override provoca un rebind del Animator).
    private AnimationClip _currentActionClip;
    // Nombre EXACTO del clip placeholder encontrado en el controller (la "llave" de la
    // ranura). null = el estado genérico todavía no existe, así que las habilidades
    // con AnimationClip igual usan el esquema viejo.
    private string _actionSlotKey;
    private bool HasActionSlot => !string.IsNullOrEmpty(_actionSlotKey);
    private bool _warnedNoActionSlot;

    // Llaves reales de las ranuras de mantener (mismo criterio que _actionSlotKey).
    private string _holdStartKey, _holdLoopKey, _holdEndKey, _holdImpactKey;
    // Qué clip está puesto ahora en cada una, para no provocar rebinds al pedo.
    private AnimationClip _currentHoldStart, _currentHoldLoop, _currentHoldEnd, _currentHoldImpact;

    // El BUCLE es lo único imprescindible: levantar, bajar y la reacción al golpe son
    // estados opcionales del Animator, y sin ellos el mantenido funciona igual.
    private bool HasHoldSlots   => !string.IsNullOrEmpty(_holdLoopKey);
    private bool HasHoldImpact  => !string.IsNullOrEmpty(_holdImpactKey);
    private bool _warnedNoHoldSlots;
    private bool _warnedNoHoldImpact;

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

    [Header("Caída de Pluma / Aleteo")]
    [Tooltip("Multiplicador de gravedad mientras el personaje tenga el tag Status_FeatherFall. " +
             "0.35 = cae a poco más de un tercio de la velocidad normal. 1 = gravedad normal.")]
    [Range(0f, 1f)]
    public float featherFallGravityScale = 0.35f;

    [Tooltip("Impulso de cada ALETEO. Mientras dure la caída de pluma se puede volver a saltar en " +
             "el aire las veces que se quiera, y cada aleteo vale esto. Conviene por debajo de " +
             "jumpForce, porque se encadenan uno atrás del otro.")]
    public float flapForce = 3f;

    [Tooltip("Tope de velocidad de caída mientras dura (valor positivo). Sin esto, la gravedad se " +
             "sigue acumulando entre aleteo y aleteo y caer desde muy alto termina en picada. " +
             "0 = sin tope.")]
    public float featherFallMaxSpeed = 6f;

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

    // Resto de velocidad que deja un dash al TERMINAR (ver ClearDashVelocity). No es
    // lo mismo que _abilityVelocity: acá el jugador conserva el control normal —gira,
    // frena y SALTA— y esto solo se suma a su movimiento mientras se disipa. Es lo que
    // evita que un dash termine en una frenada seca.
    private Vector3 _inertiaVelocity;
    private float   _inertiaDamping = 3f;

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

    // Menú ÚNICO de clases del dueño local (UI_ClassMenu). Se resuelve al spawnear la
    // cámara (OnStartClient) y atiende las dos teclas: C abre las clases base y V las
    // subclases de la clase actual. Solo el dueño lo tiene (null en las copias
    // remotas/servidor).
    private UI_ClassMenu _classMenu;

    // Habilidad de zona (IGroundTargetAbility) que se está apuntando ahora mismo,
    // mientras se mantiene su botón. Ver UI_GroundTargetIndicator.
    private GameplayAbility _groundTargetAbility;
    private EAbilityInput   _groundTargetSlot;

    // Habilidad de MANTENER (IHoldAbility) que el dueño está sosteniendo ahora
    // mismo, y en qué slot está. null = no hay ninguna. Ver ProcessHoldRelease.
    private GameplayAbility _holdAbility;
    private EAbilityInput   _holdSlot;

    // True mientras el dueño sostiene una habilidad de mantener.
    public bool IsHoldingAbility => _holdAbility != null;

    // True mientras se mantiene apretada una habilidad que se apunta antes de
    // lanzarse (menú radial o zona en el suelo), o una de MANTENER. En ese estado
    // isAttacking está en true a propósito, pero HAY que seguir leyendo el input
    // para detectar cuándo se suelta el botón — y el watchdog no debe contar (un
    // escudo puede sostenerse mucho más que MaxAttackSeconds).
    private bool IsAimingAbility => isRadialMenuOpen || _groundTargetAbility != null || IsHoldingAbility;

    // Bloquea el input del dueño (movimiento y habilidades) mientras un menú
    // modal está abierto — ej. la selección de clase (UI_ClassMenu).
    private bool _inputLocked;
    // La usan los menús modales para tomar/soltar el control del jugador.
    public void SetInputLocked(bool locked)
    {
        // Al ENTRAR a un menú se baja cualquier mantenido en curso: con el input
        // bloqueado nunca se detectaría que soltaste el botón, y el jugador saldría
        // del menú trabado con el escudo arriba. Ver CancelHeldAbility.
        if (locked) CancelHeldAbility();

        _inputLocked = locked;
    }

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

                // Menú ÚNICO de clases (UI_ClassMenu): sirve tanto para elegir la
                // clase base (tecla C) como para evolucionar a una subclase (tecla V),
                // sobre el mismo panel. Vive en ESTE prefab de cámara, que se
                // instancia acá — DESPUÉS de EquipCharacterClass/UpdateHUD. Por eso se
                // engancha acá y no en UpdateHUD: allá la cámara todavía no existía.
                // GetComponentInChildren(true) lo encuentra aunque su panel arranque
                // inactivo.
                _classMenu = camObj.GetComponentInChildren<UI_ClassMenu>(true);
                if (_classMenu != null) _classMenu.InitializeMenu(this);

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

        // SUBCLASES (acción "Cheat", rebindeada a la tecla V): abre el menú con las
        // subclases de la clase que tenés puesta ahora. Al elegir, conserva el
        // progreso — es una evolución, no un cambio de clase.
        //
        // Esta acción antes era el cheat de subir al nivel máximo, que existía solo
        // para forzar la aparición de este mismo menú; ahora se abre directo.
        if (_input.Cheat.WasPressedThisFrame() && _classMenu != null)
        {
            _classMenu.OpenSubclasses();
        }

        // CLASES BASE (acción ChangeClass: tecla C / D-pad arriba). Al elegir, la
        // clase nueva arranca en nivel 1. Ambos menús son modales (bloquean el input
        // vía SetInputLocked), así que _inputLocked de arriba ya evita reabrirlos
        // encima de sí mismos.
        if (_input.ChangeClass.WasPressedThisFrame() && _classMenu != null)
        {
            _classMenu.OpenBaseClasses();
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
            _inertiaVelocity  = Vector3.zero;
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
        //
        // EXCEPCIÓN: las habilidades de MANTENER (escudo). Ahí isAttacking está en
        // true todo el rato que sostenés el botón, y sin esta excepción el cuerpo
        // quedaba clavado mirando a donde apuntabas al levantar el escudo — o sea
        // que no podías APUNTARLO, que es justamente de lo que se trata.
        if (!isAttacking || IsHoldingAbility) FaceCameraForward();

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

        // Inercia que dejó el último dash: se disipa sola y se SUMA al movimiento
        // normal, así que el jugador nunca pierde el control mientras se le va.
        if (_inertiaVelocity.sqrMagnitude > 0.01f)
            _inertiaVelocity *= Mathf.Exp(-_inertiaDamping * Time.deltaTime);
        else
            _inertiaVelocity = Vector3.zero;

        // Caída de pluma (ult del Ángel vengador): cae más lento y puede volver a
        // impulsarse en el aire tantas veces como quiera, batiendo las alas. No es
        // volar libre: no hay ascenso sostenido, solo aleteos contra una gravedad floja.
        bool feathered = ASC.HasTag(EGameplayTag.Status_Feather_Fall);

        Vector3 horizontal;

        if (_abilityVelocityActive)
        {
            _abilityVelocity = Vector3.Lerp(_abilityVelocity, Vector3.zero, Time.deltaTime);
            horizontal        = _abilityVelocity + inputVec * baseSpeed;
        }
        else
        {
            horizontal = inputVec * baseSpeed + _inertiaVelocity;

            // En el piso es un salto normal; en el aire, solo con la caída de pluma
            // activa, y con el impulso más chico del aleteo.
            if (_input.Jump.WasPressedThisFrame() && (characterController.isGrounded || feathered))
                verticalVelocity = characterController.isGrounded ? jumpForce : flapForce;
        }

        verticalVelocity += gravity * (feathered ? featherFallGravityScale : 1f) * Time.deltaTime;

        if (feathered && featherFallMaxSpeed > 0f && verticalVelocity < -featherFallMaxSpeed)
            verticalVelocity = -featherFallMaxSpeed;

        Vector3 finalMove = new Vector3(horizontal.x, 0, horizontal.z)
                            + Vector3.up * verticalVelocity;
        characterController.Move(finalMove * Time.deltaTime);
    }

    // Cámara del dueño, cacheada. Camera.main busca por TAG cada vez que se llama, y
    // acá se usa varias veces por frame (movimiento, giro del cuerpo, punto de mira).
    // Se re-resuelve sola si la cámara actual se destruyó o desactivó (respawn,
    // cambio de escena).
    private Camera _mainCamera;
    private Camera MainCamera
    {
        get
        {
            if (_mainCamera == null || !_mainCamera.isActiveAndEnabled) _mainCamera = Camera.main;
            return _mainCamera;
        }
    }

    // Convierte el input horizontal/vertical crudo en un vector de mundo
    // relativo a hacia dónde mira la cámara del dueño.
    private Vector3 GetWASDInputVector(float h, float v)
    {
        Camera cam = MainCamera;
        if (cam == null) return Vector3.zero;

        Transform camT = cam.transform;
        Vector3 f = camT.forward; f.y = 0; f.Normalize();
        Vector3 r = camT.right;   r.y = 0; r.Normalize();
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
        Camera cam = MainCamera;
        if (cam == null) return;
        Vector3 fwd = cam.transform.forward;
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
        _inertiaVelocity = Vector3.zero;   // el impulso nuevo pisa el resto del anterior
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
    //
    // retainPercent conserva una fracción de la velocidad que traía el dash en vez de
    // frenar en seco: la parte HORIZONTAL pasa a _inertiaVelocity (se disipa sola,
    // sumándose al input del jugador) y la VERTICAL vuelve a verticalVelocity, así un
    // dash hacia arriba sigue subiendo un poco antes de caer en vez de desplomarse
    // apenas termina el impulso.
    //
    // En 0 (el default) el comportamiento es exactamente el de antes.
    public void ClearDashVelocity(float retainPercent = 0f, float damping = 3f)
    {
        Vector3 leftover = _dashVelocity * Mathf.Clamp01(retainPercent);

        _dashActive   = false;
        _dashVelocity = Vector3.zero;

        _inertiaDamping  = Mathf.Max(0.01f, damping);
        _inertiaVelocity = new Vector3(leftover.x, 0f, leftover.z);
        verticalVelocity = leftover.y;
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
        {
            ProcessAbilityPress(ability, slot);
        }
        else if (action.WasReleasedThisFrame())
        {
            // Habilidad de MANTENER: soltar la TERMINA (a diferencia del radial y
            // la zona en el suelo, donde soltar es lo que recién la lanza).
            if (_holdAbility == ability)
                ProcessHoldRelease();
            else if (currentRadialAbility == ability || _groundTargetAbility == ability)
                ProcessAbilityRelease();
        }
    }

    // Al presionar el input de una habilidad: si es de menú radial, abre
    // el menú; si no, predice la animación localmente y pide su
    // activación al servidor.
    private void ProcessAbilityPress(GameplayAbility ability, EAbilityInput slot)
    {
        // Con una habilidad de MANTENER activa no se puede empezar ninguna otra: el
        // escudo ocupa las manos. El input se sigue leyendo igual (HandleAbilityInput
        // no corta por IsAimingAbility) porque hace falta detectar cuándo se SUELTA
        // la que está en curso — pero las demás presiones se descartan acá.
        if (IsHoldingAbility) return;

        if (ability is IRadialMenuAbility radial)
        {
            if (!ability.CanActivate()) return;
            isAttacking          = true;
            _attackStartTime     = Time.time;
            isRadialMenuOpen     = true;
            currentRadialAbility = ability;
            if (UI_RadialMenu.Instance != null) UI_RadialMenu.Instance.Show(radial);
        }
        else if (ability is IHoldAbility)
        {
            // Habilidad de MANTENER: se activa por el camino NORMAL (el servidor
            // valida y arranca el estado), y queda registrada acá para que soltar el
            // botón la termine. isAttacking se sostiene todo el mantenido — bloquea
            // las demás habilidades pero no el movimiento, y IsAimingAbility saca al
            // watchdog del medio (un escudo puede durar más que MaxAttackSeconds).
            if (!ability.CanActivate()) return;

            isAttacking      = true;
            _attackStartTime = Time.time;
            _holdAbility     = ability;
            _holdSlot        = slot;

            // Predicción local del "levantar", mismo criterio que el camino normal:
            // solo en cliente remoto (en el host la dispara el Activate del servidor).
            if (!IsServerInitialized) PlayHoldAnimation(ability);

            RequestAbility(slot);
        }
        else if (ability is IGroundTargetAbility ground && ground.UsesGroundTarget)
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
                    PlayAnimation(ability);
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
                PlayAnimation(currentRadialAbility);

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
                PlayAnimation(_groundTargetAbility);

            RequestAbility(_groundTargetSlot);
            _groundTargetAbility = null;
        }

        isRadialMenuOpen     = false;
        currentRadialAbility = null;
        // Reiniciar el reloj del watchdog: recién ahora isAttacking pasa a
        // contar para el timeout (mientras se apuntaba no contaba).
        _attackStartTime     = Time.time;
    }

    // Al SOLTAR el botón de una habilidad de mantener: baja la animación en el acto
    // (feedback inmediato, sin esperar el round-trip) y le pide al servidor que
    // termine el estado real.
    //
    // isAttacking NO se libera acá: eso lo hace el servidor cuando la habilidad
    // llama EndAbility() al terminar de verdad (llega por ObserversFinishAttack).
    // Soltar antes de tiempo el bloqueo local dejaría al jugador atacar mientras el
    // servidor todavía lo tiene con el escudo arriba.
    private void ProcessHoldRelease()
    {
        if (!ReleaseHold(forceFinish: false)) return;

        // Recién ahora isAttacking pasa a contar para el watchdog (mientras se
        // mantenía, IsAimingAbility lo excluía).
        _attackStartTime = Time.time;
    }

    // Corta el mantenido en curso desde AFUERA del input, o sea sin que el jugador
    // haya soltado el botón. Lo llaman los menús modales al abrirse.
    //
    // Hace falta porque en esos casos el "soltar" NUNCA se llega a detectar: Update()
    // corta antes por _inputLocked. Sin esto, el jugador volvía del menú con
    // _holdAbility todavía apuntando al escudo, y como ProcessAbilityPress descarta
    // toda presión mientras haya un mantenido activo —y el watchdog excluye los
    // mantenidos a propósito, para que un escudo pueda sostenerse más de 5 s— quedaba
    // sin poder usar NINGUNA habilidad, para siempre.
    public void CancelHeldAbility() => ReleaseHold(forceFinish: true);

    // Baja el mantenido: avisa al servidor y limpia el estado local. Devuelve false si
    // no había ninguno.
    //
    // forceFinish libera además isAttacking en el acto, en vez de esperar el aviso de
    // fin del servidor. Se usa cuando ese aviso podría no llegar nunca (ver
    // EndActiveHolds: si la instancia de la habilidad ya se descartó, su EndHold no
    // hace nada y nadie manda ObserversFinishAttack).
    private bool ReleaseHold(bool forceFinish)
    {
        if (_holdAbility == null) return false;

        GameplayAbility ability = _holdAbility;
        _holdAbility = null;

        StopHoldAnimation();

        if (NetASC != null) NetASC.ServerRequestEndHoldAbility(_holdSlot);
        else if (ability is IHoldAbility hold) hold.EndHold(); // fallback singleplayer

        if (forceFinish) FinishAttack();
        return true;
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
                    NetASC.ServerBroadcastAbilityAnimation(ability);
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
    //
    // También cierra el estado de MANTENER si quedó abierto. Cubre los dos casos en
    // que el mantenido termina sin que el jugador suelte el botón: que el servidor
    // rechace la activación (NotifyOwnerAbilityRejected) y que la corte por su
    // cuenta (quedarse sin energía, morir). Sin esto el escudo se quedaba "pegado"
    // en el cliente: la animación de mantener seguía y soltar el botón mandaba un
    // fin de algo que ya no existía.
    public void FinishAttack()
    {
        isAttacking = false;

        if (_holdAbility == null) return;
        _holdAbility = null;
        StopHoldAnimation();
    }

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
        _inertiaVelocity   = Vector3.zero;
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

        // ANTES de tocar nada: cerrar los mantenidos en curso. Sus instancias están a
        // punto de descartarse, y una habilidad de mantener que se va sin cerrarse
        // deja su corutina viva y al jugador trabado. Ver EndActiveHolds.
        EndActiveHolds();

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

    // Cierra cualquier habilidad de MANTENER que siga activa, justo antes de descartar
    // las instancias otorgadas (cambio de clase / evolución a subclase).
    //
    // Corre en TODOS los peers, y hace falta en los dos lados por motivos distintos:
    //
    //  · En el SERVIDOR: un GA_ShieldBlock a medio mantener tiene su corutina de
    //    vigilancia corriendo sobre el ASC. Si simplemente descartamos la instancia,
    //    esa corutina queda viva para siempre girando en el vacío, y el efecto de
    //    bloqueo nunca se retira por la vía normal.
    //
    //  · En el DUEÑO: libera el estado de input. Se hace acá y no con el pedido normal
    //    al servidor porque para este punto CurrentClass ya apunta a la clase NUEVA,
    //    así que resolver la habilidad por slot daría cualquier cosa. Recorremos las
    //    instancias directamente, que es lo único fiable.
    private void EndActiveHolds()
    {
        if (ASC != null)
        {
            foreach (var granted in ASC.GrantedAbilities)
                if (granted is IHoldAbility hold && hold.IsHolding) hold.EndHold();
        }

        // Suelta _holdAbility, baja la animación de mantenido y libera isAttacking.
        FinishAttack();
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

    // Índice de clase SINCRONIZADO de este jugador. Vale en todos los peers (es un
    // SyncVar). Lo usan las Copias exactas para saber a qué jugador copiar el arma/anim.
    public int VisualClassIndex => _netClassIndex.Value;

    // Resuelve un índice de clase → definición, en cualquier peer. Como AllClasses se
    // arma igual en todos (desde MainBaseClasses del prefab), un peer puede resolver la
    // clase de OTRO jugador usando su propia lista. Lo usa Entity_PlayerCopy para
    // copiar el arma/animator del jugador origen sin depender de su CurrentClassDef
    // (que puede no estar seteado en un observador puro).
    public CharacterClassDefinition ResolveClassByIndex(int idx) => GetClassByIndex(idx);

    // Reemplaza el animator override y las armas equipadas según la
    // clase dada. La llaman tanto el dueño (al equipar) como los
    // observadores remotos (al recibir el cambio de clase por red).
    private void UpdateVisuals(CharacterClassDefinition newClass)
    {
        RebuildRuntimeAnimator(newClass);

        if (currentMainWeapon != null) Destroy(currentMainWeapon);
        if (currentOffWeapon  != null) Destroy(currentOffWeapon);

        if (newClass.MainHandWeaponPrefab != null && MainHandSocket != null)
        {
            currentMainWeapon = Instantiate(newClass.MainHandWeaponPrefab, MainHandSocket);
            // La pose sale de la CLASE, no del prefab del arma: los dos sockets tienen
            // orientaciones distintas, así que un mismo modelo necesita ajustes según
            // en qué mano vaya. En cero queda pegado al hueso, como antes.
            currentMainWeapon.transform.SetLocalPositionAndRotation(
                newClass.MainHandPositionOffset,
                Quaternion.Euler(newClass.MainHandRotationOffset));

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
                newClass.OffHandPositionOffset,
                Quaternion.Euler(newClass.OffHandRotationOffset));
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

    // (Re)arma la copia de runtime del Animator: parte del controller BASE y le
    // aplica encima los overrides de la clase. Se hace así —y no asignando el
    // override de clase directo— para poder intercambiar el clip de la ranura de
    // acción sin modificar ningún asset.
    //
    // Importante: la copia se construye sobre el controller BASE a propósito. Si la
    // construyéramos sobre el override de la clase, las claves de los overrides
    // serían los clips DE ESA CLASE (distintos en cada una) y el nombre de la ranura
    // dejaría de servir. Sobre el base, la clave es siempre el mismo placeholder.
    private void RebuildRuntimeAnimator(CharacterClassDefinition cls)
    {
        if (characterAnimator == null) return;

        RuntimeAnimatorController source = cls != null && cls.ClassAnimatorOverride != null
            ? cls.ClassAnimatorOverride
            : characterAnimator.runtimeAnimatorController;
        if (source == null) return;

        // Separamos el controller base de los overrides que traiga la clase.
        RuntimeAnimatorController baseController = source;
        var classOverrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();

        if (source is AnimatorOverrideController sourceOverride)
        {
            baseController = sourceOverride.runtimeAnimatorController;
            sourceOverride.GetOverrides(classOverrides);
        }
        if (baseController == null) return;

        _runtimeAnimator = new AnimatorOverrideController(baseController);
        if (classOverrides.Count > 0) _runtimeAnimator.ApplyOverrides(classOverrides);

        _currentActionClip = null; // la ranura vuelve al placeholder
        characterAnimator.runtimeAnimatorController = _runtimeAnimator;

        // ¿El controller base tiene realmente la ranura de acción? Si el estado
        // genérico todavía no se creó en el Animator, el esquema nuevo no puede
        // funcionar y hay que seguir con el viejo (ver ApplyAbilityAnimation).
        //
        // La búsqueda ignora mayúsculas/minúsculas por comodidad, pero guardamos el
        // nombre EXACTO del clip: el indexador del AnimatorOverrideController sí
        // distingue mayúsculas y, si no coincide al carácter, la asignación no hace
        // nada EN SILENCIO (el ataque se quedaría con el clip placeholder).
        var slots = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        _runtimeAnimator.GetOverrides(slots);

        _actionSlotKey = ResolveSlotKey(slots, ActionClipSlotName);

        // Ranuras de mantener (escudo y compañía). Se resuelven una por una y por
        // separado a propósito: todas menos el bucle son opcionales, así que la que
        // no exista simplemente queda en null y se saltea al animar.
        _holdLoopKey   = ResolveSlotKey(slots, HoldLoopSlotName);
        _holdStartKey  = ResolveSlotKey(slots, HoldStartSlotName);
        _holdEndKey    = ResolveSlotKey(slots, HoldEndSlotName);
        _holdImpactKey = ResolveSlotKey(slots, HoldImpactSlotName);
        _currentHoldStart = _currentHoldLoop = _currentHoldEnd = _currentHoldImpact = null;
    }

    // Busca en los overrides del controller el clip placeholder que se llame
    // 'slotName' y devuelve su nombre EXACTO (la llave de esa ranura), o null si no
    // existe.
    //
    // La búsqueda ignora mayúsculas/minúsculas por comodidad, pero se guarda el
    // nombre exacto: el indexador del AnimatorOverrideController sí las distingue y,
    // si no coincide al carácter, la asignación no hace nada EN SILENCIO (la
    // habilidad se quedaría con el clip placeholder).
    private static string ResolveSlotKey(List<KeyValuePair<AnimationClip, AnimationClip>> slots, string slotName)
    {
        if (string.IsNullOrEmpty(slotName)) return null;

        foreach (var pair in slots)
        {
            if (pair.Key == null) continue;
            if (string.Equals(pair.Key.name, slotName, System.StringComparison.OrdinalIgnoreCase))
                return pair.Key.name;
        }
        return null;
    }

    // Pone un clip en la ranura genérica de acción. Solo reasigna si cambió: cada
    // override provoca un rebind del Animator, así que repetir el mismo clip
    // (ej. atacar varias veces seguidas) no debe tocarlo.
    private void SetActionClip(AnimationClip clip)
    {
        if (_runtimeAnimator == null || clip == null || _currentActionClip == clip) return;
        if (!HasActionSlot) return;

        _runtimeAnimator[_actionSlotKey] = clip;
        _currentActionClip = clip;
    }

    // Reproduce la animación de una habilidad en la copia DUEÑA. Es la entrada que
    // usan las habilidades (pc.PlayAnimation(this)): elige sola entre el clip del
    // asset y el esquema viejo de ActionID.
    public void PlayAnimation(GameplayAbility ability)
    {
        if (ability == null) return;
        // Mismo guard que abajo: Activate() corre en el servidor y no debe animar
        // copias ajenas (los observadores reciben la animación por su propio RPC).
        if (IsSpawned && !IsOwner) return;

        ApplyAbilityAnimation(ability);
    }

    // Aplica la animación de una habilidad SIN guard de dueño. La usa la réplica a
    // observadores (NetworkAbilitySystemComponent), que corre justamente sobre
    // copias que no son el dueño.
    // animationSpeed: velocidad a la que reproducir el clip. -1 = calcularla acá. La
    // réplica a observadores la manda ya calculada desde el SERVIDOR, porque allá la
    // habilidad es el template del registro (sin dueño) y no podría resolverla igual.
    public void ApplyAbilityAnimation(GameplayAbility ability, float animationSpeed = -1f)
    {
        if (characterAnimator == null || ability == null) return;

        // Las habilidades "envoltorio" (el ataque principal que cambia según un tag)
        // delegan su animación en la variante que realmente se ejecuta. Para todas
        // las demás esto devuelve la misma habilidad. Ver ResolveAnimationSource.
        ability = ability.ResolveAnimationSource() ?? ability;

        // Camino nuevo: el clip viene en el propio asset de la habilidad. Requiere que
        // la ranura exista en el Animator; si no, seguimos con el esquema viejo (si
        // no, dispararíamos un ActionID que ninguna transición atiende y el ataque se
        // quedaría sin animación, dejando el trigger colgado).
        if (ability.AnimationClip != null && !HasActionSlot && !_warnedNoActionSlot)
        {
            _warnedNoActionSlot = true;

            // Listamos los clips que SÍ tiene el controller: casi siempre el problema
            // es que ActionClipSlotName no coincide con el nombre real del placeholder.
            string available = "(ninguno)";
            if (_runtimeAnimator != null)
            {
                var slots = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                _runtimeAnimator.GetOverrides(slots);
                var names = new List<string>();
                foreach (var pair in slots) if (pair.Key != null) names.Add(pair.Key.name);
                if (names.Count > 0) available = string.Join(", ", names);
            }

            Debug.LogWarning($"[PlayerController] '{ability.AbilityName}' tiene AnimationClip, pero el " +
                             $"controller no tiene la ranura '{ActionClipSlotName}'. Se usa el esquema viejo " +
                             $"(AnimationID). Creá el estado genérico de acción con ese clip placeholder, o " +
                             $"corregí ActionClipSlotName.\nClips disponibles en el controller: {available}");
        }

        if (ability.AnimationClip != null && HasActionSlot)
        {
            SetActionClip(ability.AnimationClip);

            // Velocidad del swing: la que hace que el clip dure lo que el ritmo de
            // ataque. Va en su propio parámetro para que UpdateAnimations no la pise.
            float speed = animationSpeed > 0f ? animationSpeed : ability.ResolveAnimationSpeed();
            if (!string.IsNullOrEmpty(ActionSpeedParam) && speed > 0f)
                characterAnimator.SetFloat(ActionSpeedParam, speed);

            // El ActionID igual hay que setearlo: las transiciones salen de AnyState
            // filtradas por ese número, así que sin esto iríamos al estado del ataque
            // anterior en vez de a la ranura genérica.
            characterAnimator.SetInteger("ActionID", ActionClipStateID);
            if (!string.IsNullOrEmpty(ActionClipTrigger)) characterAnimator.SetTrigger(ActionClipTrigger);
            return;
        }

        // Camino viejo: un estado propio por ataque, elegido por ActionID. Va por
        // TriggerAnimator (sin guard): este método también corre en observadores.
        TriggerAnimator(ability.AnimationTriggerName, ability.AnimationID);
    }

    // Reproduce un CLIP suelto en la ranura genérica de acción, sin necesitar la
    // habilidad que lo trae. La usa la réplica de los pasos de un combo: ahí el clip
    // puede ser un AnimationClipOverride del paso, que no coincide con el AnimationClip
    // del asset de esa habilidad, así que el observador no lo puede resolver por la
    // habilidad — lo resuelve por las coordenadas del paso y llega acá con el clip ya
    // en mano (ver GameplayAbility.GetStepAnimationClip).
    //
    // Sin guard de dueño a propósito: corre en observadores Y en el dueño remoto.
    // Si el clip es null o falta la ranura, cae al esquema viejo con trigger/ActionID.
    public void PlayActionClip(AnimationClip clip, float animationSpeed,
                               string fallbackTrigger, int fallbackActionID)
    {
        if (characterAnimator == null) return;

        if (clip == null || !HasActionSlot)
        {
            TriggerAnimator(fallbackTrigger, fallbackActionID);
            return;
        }

        SetActionClip(clip);

        if (!string.IsNullOrEmpty(ActionSpeedParam) && animationSpeed > 0f)
            characterAnimator.SetFloat(ActionSpeedParam, animationSpeed);

        characterAnimator.SetInteger("ActionID", ActionClipStateID);
        if (!string.IsNullOrEmpty(ActionClipTrigger)) characterAnimator.SetTrigger(ActionClipTrigger);
    }

    // =========================================================
    // ANIMACIÓN DE MANTENER (IHoldAbility)
    // =========================================================

    // Arranca la animación de mantener de una habilidad en la copia DUEÑA
    // (levantar → bucle). Entrada que usan las habilidades y la predicción del input.
    public void PlayHoldAnimation(GameplayAbility ability)
    {
        // Mismo guard que PlayAnimation: Activate() corre en el servidor y no debe
        // animar copias ajenas (los observadores lo reciben por su propio RPC).
        if (IsSpawned && !IsOwner) return;
        ApplyHoldAnimation(ability);
    }

    // Termina la animación de mantener en la copia DUEÑA (bucle → bajar).
    public void StopHoldAnimation()
    {
        if (IsSpawned && !IsOwner) return;
        ApplyStopHoldAnimation();
    }

    // Igual que PlayHoldAnimation pero SIN guard de dueño: la usa la réplica a
    // observadores (NetworkAbilitySystemComponent), que corre justamente sobre
    // copias que no son el dueño.
    //
    // Los tres clips salen del asset de la habilidad, así que cada peer resuelve los
    // suyos desde el registro y no hay que mandar nada más que el índice.
    public void ApplyHoldAnimation(GameplayAbility ability)
    {
        if (characterAnimator == null || ability is not IHoldAbility hold) return;

        if (!HasHoldSlots)
        {
            if (!_warnedNoHoldSlots)
            {
                _warnedNoHoldSlots = true;
                Debug.LogWarning($"[PlayerController] '{ability.AbilityName}' es una habilidad de mantener, " +
                                 $"pero el controller no tiene la ranura del bucle ('{HoldLoopSlotName}'). " +
                                 $"La habilidad funciona igual, pero sin animación: alcanza con crear UN " +
                                 $"estado con ese clip placeholder en AC_Player (ver el bloque de " +
                                 $"comentarios de HoldLoopSlotName).");
            }
            return;
        }

        SetHoldClips(hold.HoldStartClip, hold.HoldLoopClip, hold.HoldEndClip);

        // El bool va ANTES del trigger: si se seteara después, el Animator podría
        // evaluar la transición bucle → bajar con IsHolding todavía en false (del
        // mantenido anterior) y salir del bucle en el primer frame.
        if (!string.IsNullOrEmpty(HoldingParam)) characterAnimator.SetBool(HoldingParam, true);

        // El ActionID igual hay que setearlo: la transición sale de AnyState filtrada
        // por ese número (mismo esquema que la ranura de acción).
        characterAnimator.SetInteger("ActionID", HoldStateID);
        if (!string.IsNullOrEmpty(HoldTrigger)) characterAnimator.SetTrigger(HoldTrigger);
    }

    // Igual que StopHoldAnimation pero SIN guard de dueño (para observadores).
    // Basta con bajar el bool: la transición bucle → bajar la resuelve el Animator.
    public void ApplyStopHoldAnimation()
    {
        if (characterAnimator == null || string.IsNullOrEmpty(HoldingParam)) return;
        characterAnimator.SetBool(HoldingParam, false);
    }

    // Reacción al recibir un golpe MIENTRAS se bloquea (el escudo acusa el impacto).
    // Es un one-shot que sale del bucle y vuelve solo. Todo opcional: sin clip o sin
    // el estado en el Animator, no hace nada.
    //
    // Sin guard de dueño: la usan por igual el dueño y la réplica a observadores.
    public void ApplyHoldImpactAnimation(AnimationClip clip)
    {
        if (characterAnimator == null || clip == null) return;

        // La habilidad TRAE un clip de impacto pero el Animator no tiene dónde
        // ponerlo. Sin este aviso, asignar el clip en el asset no hacía absolutamente
        // nada y no había forma de darse cuenta: la reacción simplemente no se veía.
        if (!HasHoldImpact)
        {
            if (!_warnedNoHoldImpact)
            {
                _warnedNoHoldImpact = true;
                Debug.LogWarning($"[PlayerController] Hay un clip de reacción al golpe en el escudo " +
                                 $"('{clip.name}'), pero el controller no tiene la ranura " +
                                 $"'{HoldImpactSlotName}'. La reacción no se va a ver.\n" +
                                 $"Creá un estado con ese clip placeholder en AC_Player: " +
                                 $"AnyState → HoldImpact con ActionID == {HoldImpactStateID} + trigger " +
                                 $"'{HoldImpactTrigger}', y HoldImpact → HoldLoop por Exit Time con " +
                                 $"{HoldingParam} == true.");
            }
            return;
        }

        if (clip != _currentHoldImpact)
        {
            _runtimeAnimator[_holdImpactKey] = clip;
            _currentHoldImpact = clip;
        }

        characterAnimator.SetInteger("ActionID", HoldImpactStateID);
        if (!string.IsNullOrEmpty(HoldImpactTrigger)) characterAnimator.SetTrigger(HoldImpactTrigger);
    }

    // Mete los clips en sus ranuras, salteando las que no existen (levantar, bajar e
    // impacto son opcionales) y las que no cambiaron — cada override provoca un rebind
    // del Animator, así que levantar el escudo dos veces seguidas no debe tocarlo.
    private void SetHoldClips(AnimationClip start, AnimationClip loop, AnimationClip end)
    {
        if (_runtimeAnimator == null) return;

        if (loop != null && loop != _currentHoldLoop && !string.IsNullOrEmpty(_holdLoopKey))
        { _runtimeAnimator[_holdLoopKey] = loop; _currentHoldLoop = loop; }

        if (start != null && start != _currentHoldStart && !string.IsNullOrEmpty(_holdStartKey))
        { _runtimeAnimator[_holdStartKey] = start; _currentHoldStart = start; }

        if (end != null && end != _currentHoldEnd && !string.IsNullOrEmpty(_holdEndKey))
        { _runtimeAnimator[_holdEndKey] = end; _currentHoldEnd = end; }
    }

    // Setea ActionID + trigger en el Animator, sin ningún guard. Lo comparten la
    // ruta del dueño y la de observadores.
    private void TriggerAnimator(string trigger, int actionID)
    {
        if (characterAnimator == null || string.IsNullOrEmpty(trigger)) return;
        characterAnimator.SetInteger("ActionID", actionID);
        characterAnimator.SetTrigger(trigger);
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

        TriggerAnimator(trigger, actionID);
    }

    // Gancho del Animation Event que marca el frame de impacto. A propósito NO hace
    // nada: el evento sirve como MARCA DE TIEMPO dentro del clip, y quien lo usa es el
    // servidor, que lee su timestamp del asset (GameplayAbility.GetHitFrameTimes) y
    // programa el daño ahí. Así el golpe cae en el frame exacto sin depender de que
    // cada cliente reporte nada (los eventos corren local en cada peer y no serían
    // confiables para daño).
    //
    // NO borrar este método: Unity dispara igual el evento en cada peer y, sin un
    // receptor, la consola se llena de "AnimationEvent has no receiver!".
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

        Camera cam = MainCamera;
        if (cam == null)
            return transform.position + transform.forward * 10f;

        Ray          ray  = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
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
