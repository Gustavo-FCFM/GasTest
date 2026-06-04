using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

[RequireComponent(typeof(AbilitySystemComponent))]
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    // =========================================================
    // 1. REFERENCIAS PRINCIPALES
    // =========================================================
    [Header("Referencias Base")]
    private AbilitySystemComponent ASC;
    private CharacterController characterController;
    private PlayerInput playerInput;

    [Header("Configuración de Clase")]
    public CharacterClassDefinition CurrentClassDef;
    [Header("UI Local Multijugador")]
    public UI_ClassSelectionMenu LocalClassMenu; // Arrastre su menú local aquí en el Prefab
    [Header("HUD Local Multijugador")]
    public UI_PlayerHUD LocalHUD;
    
    public CharacterClassDefinition[] MainBaseClasses; // Bárbaro, Mago, Ranger...
    [Header("Cámara Local")]
    public Camera localCamera;
    // =========================================================
    // 2. VISUALES Y ANIMACIÓN
    // =========================================================
    [Header("UI & Visuals")] 
    public Sprite CharacterIcon; 
    public Animator characterAnimator;
    
    [Header("Referencias de Huesos (Sockets)")]
    public Transform MainHandSocket; 
    public Transform OffHandSocket;

    private GameObject currentMainWeapon;
    private GameObject currentOffWeapon;
    private GameObject currentWeaponTrail;

    // =========================================================
    // 3. COMBATE Y HABILIDADES
    // =========================================================
    [Header("Estado de Combate")]
    private bool isAttacking = false; 
    [HideInInspector] public bool isRadialMenuOpen = false;
    private GameplayAbility currentRadialAbility = null;

    [Header("Habilidades Activas (Asignadas dinámicamente)")]
    [HideInInspector] public GameplayAbility PrimaryAttackAbility; 
    [HideInInspector] public GameplayAbility AimAbility;         
    [HideInInspector] public GameplayAbility AbilityQ;        
    [HideInInspector] public GameplayAbility AbilityE;   
    [HideInInspector] public GameplayAbility AbilityR;     
    [HideInInspector] public GameplayAbility MovementAbility; 

    // =========================================================
    // 4. FÍSICAS Y MOVIMIENTO
    // =========================================================
    [Header("Físicas")]
    public float jumpForce = 8f;
    public float gravity = -9.8f;
    
    [Tooltip("Si el jugador baja de esta altura Y, muere o reaparece.")]
    public float VoidYLevel = -5.0f;
    
    private float verticalVelocity; 
    private Vector3 abilityMoveVector; 
    private bool isAbilityLeaping = false;
    [HideInInspector] public GA_LeapAttack activeLeapAbility; 
    private Vector3 spawnPosition; 

    // =========================================================
    // MÉTODOS NATIVOS DE UNITY (CICLO DE VIDA)
    // =========================================================
    void Awake()
    {
        ASC = GetComponent<AbilitySystemComponent>();
        characterController = GetComponent<CharacterController>();
        spawnPosition = transform.position;

        // Inicializamos nuestro mapa de controles generado por Unity
        playerInput = GetComponent<PlayerInput>();
    }

    void Start()
    {
        if (CurrentClassDef != null) EquipCharacterClass(CurrentClassDef);
        if (ASC != null) ASC.OnDeath += HandlePlayerDeath;
        if (LocalClassMenu != null)
        {
            LocalClassMenu.InitializeMenu(this);
        }
    }

    void Update()
    {
        // 1. Validar si el jugador está muerto
        if (ASC.HasTag(EGameplayTag.State_Dead))
        {
            // Solo permitimos usar la Ultimate (Ej. Inmortalidad) estando muertos
            if (playerInput.actions["Ultimate"].WasPressedThisFrame())
            {
                if (AbilityR != null && AbilityR.CanActivate()) AbilityR.Activate();
            }
            return; 
        }
        
        // 2. Validar pérdida de control (Stun)
        if (ASC != null && ASC.HasTag(EGameplayTag.State_Stunned)) return;
        
        // 3. Validar caída al vacío
        if (transform.position.y < VoidYLevel)
        {   
            TeleportToSpawn();
            return;
        }
        
        // 4. Ejecutar lógicas principales
        HandleMovementInput(); 
        HandleAbilityInput();  
        UpdateAnimations();
    }

    // =========================================================
    // SISTEMA DE MOVIMIENTO Y ROTACIÓN
    // =========================================================
    private void HandleMovementInput()
    {
        // Si estamos inmovilizados, solo aplicamos gravedad
        if (ASC != null && ASC.HasTag(EGameplayTag.State_Rooted))
        {
            verticalVelocity += gravity * Time.deltaTime;
            characterController.Move(Vector3.up * verticalVelocity * Time.deltaTime);
            return; 
        }

        // --- 1. Calcular Velocidad con Modificadores ---
        float baseSpeed = 5f; 
        float speedMultiplier = 1.0f;

        if (ASC != null)
        {
            baseSpeed = ASC.GetAttributeValue(EAttributeType.MovSpeed);
            foreach (var activeEffect in ASC.GetActiveEffects())
            {
                foreach (var mod in activeEffect.Definition.Modifiers)
                {
                    if (mod.Attribute == EAttributeType.MovSpeed && mod.Type == Modifier.EModificationType.Multiply)
                    {
                        if (mod.Magnitude < speedMultiplier) speedMultiplier = mod.Magnitude;
                    }
                }
            }
        }
        float finalSpeed = baseSpeed * speedMultiplier;

        // --- 2. Leer Input del Joystick o Teclado (New Input System) ---
        Vector2 moveInput = playerInput.actions["Move"].ReadValue<Vector2>();
        float horizontal = moveInput.x;
        float vertical = moveInput.y;
        
        Vector3 inputVector = GetMovementInputVector(horizontal, vertical);
        
        // --- 3. Rotación del Personaje ---
        if (inputVector != Vector3.zero && !isAttacking)
        {
            Quaternion targetRotation = Quaternion.LookRotation(inputVector);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
        }

        // --- 4. Movimiento Físico ---
        Vector3 currentHorizontalMovement = Vector3.zero;

        if (isAbilityLeaping)
        {
            // Movimiento forzado por habilidades (Ej. Salto del Bárbaro)
            abilityMoveVector = Vector3.Lerp(abilityMoveVector, Vector3.zero, Time.deltaTime * 1f); 
            Vector3 airNudge = inputVector * finalSpeed * 1f; 
            currentHorizontalMovement = abilityMoveVector + airNudge;
        }
        else 
        {
            // Movimiento estándar
            currentHorizontalMovement = inputVector * finalSpeed;
            
            // Salto (New Input System)
            if (characterController.isGrounded && playerInput.actions["Jump"].WasPressedThisFrame()) 
            {
                verticalVelocity = jumpForce;
            }
        }

        // Aplicar Gravedad y mover el CharacterController
        verticalVelocity += gravity * Time.deltaTime; 
        Vector3 finalMovement = new Vector3(currentHorizontalMovement.x, 0, currentHorizontalMovement.z) + (Vector3.up * verticalVelocity);
        characterController.Move(finalMovement * Time.deltaTime); 
        
        CheckLanding();
    }

    // Convierte el Input 2D a una dirección 3D basada en la cámara
    private Vector3 GetMovementInputVector(float h, float v)
    {
        if (localCamera == null) return Vector3.zero;

        Vector3 f = localCamera.transform.forward; 
        Vector3 r = localCamera.transform.right;   
        f.y = 0; r.y = 0; // Aplanamos los vectores
        f.Normalize(); r.Normalize();
        return (f * v + r * h).normalized;
    }

    // =========================================================
    // SISTEMA DE ENTRADA DE HABILIDADES
    // =========================================================
    private void HandleAbilityInput()
    {
        // 1. MENÚ DE CLASES (Usamos "ToggleClassMenu" que configuraste en tu archivo de controles)
        if (playerInput.actions["ToggleClassMenu"].WasPressedThisFrame()) 
        {
            if (LocalClassMenu != null)
            {
                if (LocalClassMenu.MenuContainer.activeSelf)
                    LocalClassMenu.ConfirmCurrentSelectionFromGamepad();
                else
                    LocalClassMenu.ToggleMenu();
            }
        }

        // 2. VALIDACIONES DE COMBATE
        if (ASC != null && ASC.HasTag(EGameplayTag.State_Silenced)) return; 
        if (isAttacking && !isRadialMenuOpen) return; 

        // 3. LECTURA DE HABILIDADES
        CheckAbilityAction(playerInput.actions["PrimaryAttack"], PrimaryAttackAbility); 
        CheckAbilityAction(playerInput.actions["SecondaryAttack"], AimAbility);
        CheckAbilityAction(playerInput.actions["Ability1"], AbilityQ);
        CheckAbilityAction(playerInput.actions["Ability2"], AbilityE);
        CheckAbilityAction(playerInput.actions["Ultimate"], AbilityR);
        CheckAbilityAction(playerInput.actions["MovementAbility"], MovementAbility);
    }

    // Interpreta si el botón fue presionado o soltado en este frame
    private void CheckAbilityAction(InputAction action, GameplayAbility ability)
    {
        if (ability == null) return;

        if (action.WasPressedThisFrame())
        {
            ProcessAbilityPress(ability);
        }
        else if (action.WasReleasedThisFrame() && currentRadialAbility == ability)
        {
            ProcessAbilityRelease();
        }
    }

    private void ProcessAbilityPress(GameplayAbility ability)
    {
        // Si es una habilidad de menú radial, pausamos para apuntar
        if (ability is IRadialMenuAbility radialAbility)
        {
            if (!ability.CanActivate()) return; 
            
            isAttacking = true;
            isRadialMenuOpen = true;
            currentRadialAbility = ability;
            
            if (UI_RadialMenu.Instance != null)
                UI_RadialMenu.Instance.Show(radialAbility);
        }
        else
        {
            // Si es instacast, la activamos de inmediato
            TryActivateAbility(ability);
        }
    }

    private void ProcessAbilityRelease()
    {
        // Cuando suelta el botón del menú radial, ejecutamos la habilidad con la decisión
        if (currentRadialAbility is IRadialMenuAbility radialAbility)
        {
            int seleccionReal = 0;
            if (UI_RadialMenu.Instance != null)
                seleccionReal = UI_RadialMenu.Instance.HideAndGetSelection();
            
            Vector3 targetPos = GetAimPoint(radialAbility.MaxRadialRange);
            radialAbility.ActivateWithSelection(seleccionReal, targetPos);
        }

        isRadialMenuOpen = false;
        currentRadialAbility = null;
    }

    private void TryActivateAbility(GameplayAbility ability)
    {
        if (ASC != null && ability != null && ability.CanActivate())
        {
            isAttacking = true; 
            ability.Activate();
        }
    }

    // =========================================================
    // GESTIÓN DE CLASE Y ESTADÍSTICAS
    // =========================================================
    public void EquipCharacterClass(CharacterClassDefinition newClass)
    {
        if (newClass == null || ASC == null) return;
        
        // 1. Purga de Estado (Evita bugs de desincronización de stats)
        ASC.RemoveAllActiveEffects();
        
        CurrentClassDef = newClass;
        ASC.CurrentClass = newClass;
        CharacterIcon = newClass.ClassIcon;
        
        // 2. Actualizar armas y animador
        UpdateVisuals(newClass);
        
        // 3. Reasignar Habilidades
        ASC.ClearGrantedAbilities();
        ResetAbilitySlots();
        
        foreach (var assignment in newClass.Abilities)
        {
            GameplayAbility instance = ASC.GrantAbility(assignment.Ability);
            switch (assignment.InputSlot)
            {
                case EAbilityInput.PrimaryAttack:   PrimaryAttackAbility = instance; break;
                case EAbilityInput.SecondaryAttack: AimAbility = instance; break;
                case EAbilityInput.Action1:         AbilityQ = instance; break;
                case EAbilityInput.Action2:         AbilityE = instance; break;
                case EAbilityInput.Action3:         AbilityR = instance; break;
                case EAbilityInput.Movement:        MovementAbility = instance; break;
            }
        }
        
        // 4. Inicializar Atributos Base
        if (newClass.BaseAttributes != null)
        {
            ASC.CharacterRoleDefinition = newClass.BaseAttributes;
            ASC.InitializeAttributes(); 
        }
        
        UpdateHUD();
        if (LocalHUD != null) LocalHUD.HideLevelUpNotification();
        Debug.Log($"[PlayerController] Clase equipada: {newClass.ClassName}");
    }

    private void ResetAbilitySlots()
    {
        PrimaryAttackAbility = null; AimAbility = null; 
        AbilityQ = null; AbilityE = null; AbilityR = null; MovementAbility = null; 
    }

    // =========================================================
    // UTILIDADES VISUALES Y HUD
    // =========================================================
    private void UpdateHUD()
    {
        // Le pasamos 'this' (este jugador) directamente a su propio HUD
        if (LocalHUD != null) LocalHUD.InitializeHUD(this);
    }

    private void UpdateVisuals(CharacterClassDefinition newClass)
    {
        if (newClass.ClassAnimatorOverride != null && characterAnimator != null)
        {
            characterAnimator.runtimeAnimatorController = newClass.ClassAnimatorOverride;
        }
        
        if (currentMainWeapon != null) Destroy(currentMainWeapon);
        if (currentOffWeapon != null) Destroy(currentOffWeapon);
        
        if (newClass.MainHandWeaponPrefab != null && MainHandSocket != null)
        {
            currentMainWeapon = Instantiate(newClass.MainHandWeaponPrefab, MainHandSocket);
            currentMainWeapon.transform.localPosition = Vector3.zero;
            currentMainWeapon.transform.localRotation = Quaternion.identity;
            
            Transform trailTransform = currentMainWeapon.transform.Find("WeaponTrail");
            if (trailTransform != null)
            {
                currentWeaponTrail = trailTransform.gameObject;
                currentWeaponTrail.SetActive(false); 
            }
        }
        
        if (newClass.OffHandWeaponPrefab != null && OffHandSocket != null)
        {
            currentOffWeapon = Instantiate(newClass.OffHandWeaponPrefab, OffHandSocket);
            currentOffWeapon.transform.localPosition = Vector3.zero;
            currentOffWeapon.transform.localRotation = Quaternion.identity;
        }
    }

    void UpdateAnimations()
    {
        if (characterAnimator == null) return;
        
        Vector3 horizontalVelocity = new Vector3(characterController.velocity.x, 0, characterController.velocity.z);
        float speed = horizontalVelocity.magnitude;
        
        characterAnimator.SetFloat("Speed", speed, 0.1f, Time.deltaTime);
        characterAnimator.SetBool("IsJumping", !characterController.isGrounded);
        
        if (ASC != null)
        {
            float interval = ASC.GetAttributeValue(EAttributeType.AtkSpeed);
            if (interval > 0)
            {
                float animSpeed = 1f / interval;
                characterAnimator.SetFloat("AttackSpeedMult", animSpeed);
            }
        }
    }

    // =========================================================
    // EVENTOS DE ANIMACIÓN (ANIMATION EVENTS)
    // =========================================================
    public void FinishAttack() => isAttacking = false; 

    public void AnimationEvent_HitFrame()
    {
        // Interceptado por las habilidades de Daño Físico (Melee)
    }
    
    public void AnimationEvent_EnableTrail()
    {
        if (currentWeaponTrail != null) currentWeaponTrail.SetActive(true);
    }

    public void AnimationEvent_DisableTrail()
    {
        if (currentWeaponTrail != null) currentWeaponTrail.SetActive(false);
    }
    
    public void PlayAnimation(string triggerName, int actionID)
    {
        if (characterAnimator != null)
        {
            characterAnimator.SetInteger("ActionID", actionID);
            characterAnimator.SetTrigger(triggerName);
        }
    }

    // =========================================================
    // UTILIDADES DE COMBATE (APUNTADO, SALTOS Y MUERTE)
    // =========================================================
    public void ExecuteLeap(GA_LeapAttack ability, float upForce, float fwdForce)
    {
        if (!characterController.isGrounded) return;
        
        Vector3 camFwd = localCamera != null ? localCamera.transform.forward : transform.forward;
        Vector3 impulse = new Vector3(camFwd.x, 0, camFwd.z).normalized;
        abilityMoveVector = impulse * fwdForce;
        verticalVelocity = upForce;
        isAbilityLeaping = true;
        activeLeapAbility = ability;
        transform.forward = impulse; 
    }

    private void CheckLanding()
    {
        if (isAbilityLeaping && characterController.isGrounded)
        {
            if(activeLeapAbility != null) activeLeapAbility.ExecuteImpactCheck();
            isAbilityLeaping = false;
            activeLeapAbility = null;
            abilityMoveVector = Vector3.zero;
            FinishAttack(); 
        }
    }

    public Vector3 GetAimPoint(float maxRange = 100f)
    {
        if (localCamera == null) return transform.position + transform.forward * 10f;
        Ray ray = localCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit[] hits = Physics.RaycastAll(ray, maxRange);
        
        float closestDistance = float.MaxValue;
        Vector3 bestPoint = ray.GetPoint(maxRange); 

        foreach (RaycastHit hit in hits)
        {
            // Ignorar Triggers y al propio jugador
            if (!hit.collider.isTrigger && hit.collider.transform.root != this.transform.root)
            {
                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    bestPoint = hit.point;
                }
            }
        }
        return bestPoint;
    }

    public void RotateToAim()
    {
        Vector3 targetPoint = GetAimPoint();
        Vector3 direction = (targetPoint - transform.position).normalized;
        direction.y = 0; 
        
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    private void HandlePlayerDeath()
    {
        if (AbilityR != null && AbilityR is GA_InmortalWrath && AbilityR.CanActivate())
        {
            AbilityR.Activate();
            return;
        }
        StartCoroutine(RespawnRoutine(3f));
    }

    private System.Collections.IEnumerator RespawnRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        string currentSceneName = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(currentSceneName);
    }
    
    public void TeleportToSpawn()
    {
        characterController.enabled = false;
        transform.position = spawnPosition;
        verticalVelocity = 0f;
        characterController.enabled = true;
    }

    public GameObject GetCurrentMainWeapon()
    {
        return currentMainWeapon; 
    }
    public void OpenBaseClassMenuOnSpawn()
    {
        if (LocalClassMenu != null)
        {
            // Inyectamos las clases base al menú y lo abrimos
            LocalClassMenu.AvailableClasses = new System.Collections.Generic.List<CharacterClassDefinition>(MainBaseClasses);
            LocalClassMenu.InitializeMenu(this);
            LocalClassMenu.ToggleMenu();
        }
    }

    private void ToggleBaseClassMenu()
    {
        if (LocalClassMenu != null)
        {
            LocalClassMenu.ToggleMenu();
        }
    }
}