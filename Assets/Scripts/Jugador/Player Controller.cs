using UnityEngine;

[RequireComponent(typeof(AbilitySystemComponent))]
public class PlayerController : MonoBehaviour
{
    // --- Referencias ---
    private AbilitySystemComponent ASC;
    private CharacterController characterController;

    [Header("Configuración de Clase")]
    [Tooltip("Arrastra aquí la clase inicial (ej: Class_Barbarian).")]
    public CharacterClassDefinition CurrentClassDef;
    
    [Header("UI & Visuals")] 
    public Sprite CharacterIcon; 
    [Header("Animación")]
    public Animator characterAnimator;
    [Header("Referencias de Huesos")]
    public Transform MainHandSocket; 
    public Transform OffHandSocket;

    private GameObject currentMainWeapon;
    private GameObject currentOffWeapon;

    // --- SEMÁFORO DE COMBATE (NUEVO) ---
    // Si es true, no podemos iniciar otra habilidad
    private bool isAttacking = false; 

    // --- Habilidades Activas ---
    [HideInInspector] public GameplayAbility MovementAbility; 
    [HideInInspector] public GameplayAbility AbilityQ;        
    [HideInInspector] public GameplayAbility AbilityE;   
    [HideInInspector] public GameplayAbility AbilityR;     
    [HideInInspector] public GameplayAbility PrimaryAttackAbility; 
    [HideInInspector] public GameplayAbility AimAbility;         

    [Header("Físicas")]
    public float jumpForce = 8f;
    public float gravity = -9.8f;
    
    private float verticalVelocity; 
    private Vector3 abilityMoveVector; 
    private bool isAbilityLeaping = false;
    [HideInInspector] public GA_LeapAttack activeLeapAbility; 
    private Vector3 spawnPosition; 

    void Awake()
    {
        ASC = GetComponent<AbilitySystemComponent>();
        characterController = GetComponent<CharacterController>();
        spawnPosition = transform.position;
    }

    void Start()
    {
        if (CurrentClassDef != null) EquipCharacterClass(CurrentClassDef);
        if (ASC != null) ASC.OnDeath += HandlePlayerDeath;
    }

    void Update()
    {
        if (ASC != null && ASC.HasTag(EGameplayTag.State_Dead)) return;
        if (ASC != null && ASC.HasTag(EGameplayTag.State_Stunned)) return;
        
        HandleMovementInput(); 
        HandleAbilityInput();  
        UpdateAnimations();
    }

    private void HandlePlayerDeath()
    {
        StartCoroutine(RespawnRoutine(3f));
    }

    // ---------------------------------------------------------
    // 1. INPUT DE HABILIDADES CON SEMÁFORO
    // ---------------------------------------------------------
    private void HandleAbilityInput()
    {
        if (ASC != null && ASC.HasTag(EGameplayTag.State_Silenced)) return; 
        
        // Si ya estamos atacando, ignoramos nuevos clicks (Anti-Spam)
        if (isAttacking) return; 

        if (Input.GetButtonDown("Fire3")) TryActivateAbility(MovementAbility); 
        if (Input.GetKeyDown(KeyCode.Q))  TryActivateAbility(AbilityQ);
        if (Input.GetKeyDown(KeyCode.E))  TryActivateAbility(AbilityE);
        if (Input.GetKeyDown(KeyCode.R))  TryActivateAbility(AbilityR);
        if (Input.GetButtonDown("Fire1")) TryActivateAbility(PrimaryAttackAbility); 
        if (Input.GetButtonDown("Fire2")) TryActivateAbility(AimAbility); 
    }

    private void TryActivateAbility(GameplayAbility ability)
    {
        if (ASC != null && ability != null && ability.CanActivate())
        {
            isAttacking = true; // Ponemos el semáforo en ROJO
            ability.Activate();
        }
    }

    // --- MÉTODO PÚBLICO PARA LIBERAR EL SEMÁFORO ---
    // Las habilidades (GAs) llamarán a esto cuando terminen su animación
    public void FinishAttack()
    {
        isAttacking = false; // Semáforo en VERDE
    }

    // ---------------------------------------------------------
    // ... (El resto de métodos: EquipCharacterClass, UpdateVisuals, etc. siguen IGUAL) ...
    // Copia y pega el resto de tu script original aquí abajo
    // ---------------------------------------------------------

    public void EquipCharacterClass(CharacterClassDefinition newClass)
    {
        if (newClass == null || ASC == null) return;
        CurrentClassDef = newClass;
        ASC.CurrentClass = newClass;
        CharacterIcon = newClass.ClassIcon;
        UpdateVisuals(newClass);
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
        if (newClass.BaseAttributes != null)
        {
            ASC.CharacterRoleDefinition = newClass.BaseAttributes;
            ASC.InitializeAttributes(); 
        }
        UpdateHUD();
        Debug.Log($"Clase equipada: {newClass.ClassName}");
    }

    private void ResetAbilitySlots()
    {
        MovementAbility = null; AbilityQ = null; AbilityE = null;
        PrimaryAttackAbility = null; AimAbility = null; AbilityR = null;
    }

    private void UpdateHUD()
    {
        UI_PlayerHUD hud = FindFirstObjectByType<UI_PlayerHUD>();
        if (hud != null) hud.InitializeHUD();
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
        
        // Actualizar velocidad de ataque según stats
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

    public void AnimationEvent_HitFrame()
    {
        Debug.Log("¡HIT FRAME! Aplicando daño ahora.");
    }

    public void PlayAnimation(string triggerName, int actionID)
    {
        if (characterAnimator != null)
        {
            // 1. Le decimos QUÉ animación queremos
            characterAnimator.SetInteger("ActionID", actionID);
            
            // 2. Le decimos ¡HAZLA AHORA!
            characterAnimator.SetTrigger(triggerName);
        }
    }

    private void HandleMovementInput()
    {
        if (ASC != null && ASC.HasTag(EGameplayTag.State_Rooted))
        {
            verticalVelocity += gravity * Time.deltaTime;
            characterController.Move(Vector3.up * verticalVelocity * Time.deltaTime);
            return; 
        }

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
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        Vector3 inputVector = GetWASDInputVector(horizontal, vertical);
        Vector3 currentHorizontalMovement = Vector3.zero;

        if (isAbilityLeaping)
        {
            abilityMoveVector = Vector3.Lerp(abilityMoveVector, Vector3.zero, Time.deltaTime * 1f); 
            Vector3 airNudge = inputVector * finalSpeed * 1f; 
            currentHorizontalMovement = abilityMoveVector + airNudge;
        }
        else 
        {
            currentHorizontalMovement = inputVector * finalSpeed;
            if (characterController.isGrounded && Input.GetButtonDown("Jump")) 
            {
                verticalVelocity = jumpForce;
            }
        }

        verticalVelocity += gravity * Time.deltaTime; 
        Vector3 finalMovement = new Vector3(currentHorizontalMovement.x, 0, currentHorizontalMovement.z) + (Vector3.up * verticalVelocity);
        characterController.Move(finalMovement * Time.deltaTime); 
        CheckLanding();
    }

    private Vector3 GetWASDInputVector(float h, float v)
    {
        Vector3 f = Camera.main.transform.forward; f.y = 0; f.Normalize();
        Vector3 r = Camera.main.transform.right;   r.y = 0; r.Normalize();
        return (f * v + r * h).normalized;
    }

    public void ExecuteLeap(GA_LeapAttack ability, float upForce, float fwdForce)
    {
        if (!characterController.isGrounded) return;
        Vector3 camFwd = Camera.main.transform.forward;
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
            FinishAttack(); // Asegurar que liberamos si era un ataque de salto
        }
    }

    private System.Collections.IEnumerator RespawnRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        characterController.enabled = false;
        transform.position = spawnPosition;
        characterController.enabled = true;
        ASC.Revive();
    }
}