using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AbilitySystemComponent))]
public class PlayerController : MonoBehaviour
{
    // --- Referencias ---
    private AbilitySystemComponent ASC;
    private CharacterController characterController;

    [Header("Configuración de Clase")]
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

    // --- SEMÁFORO DE COMBATE ---
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
    [Tooltip("Si el jugador baja de esta altura Y, muere instantáneamente.")]
    public float VoidYLevel = -5.0f;
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
        if (transform.position.y < VoidYLevel)
        {   
            TeleportToSpawn();
            // ASC.ApplyGameplayEffect(algunEfectoDeDaño);
            return;
        }
        HandleMovementInput(); 
        HandleAbilityInput();  
        UpdateAnimations();
    }

    private void HandlePlayerDeath()
    {
        StartCoroutine(RespawnRoutine(3f));
    }

    // ---------------------------------------------------------
    // INPUT Y MOVIMIENTO
    // ---------------------------------------------------------
    private void HandleMovementInput()
    {
        if (ASC != null && ASC.HasTag(EGameplayTag.State_Rooted))
        {
            verticalVelocity += gravity * Time.deltaTime;
            characterController.Move(Vector3.up * verticalVelocity * Time.deltaTime);
            return; 
        }

        // 1. Calcular Velocidad
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

        // 2. Leer Input
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        // Esta función usa la cámara principal, así que si la cámara rota, esto rota.
        Vector3 inputVector = GetWASDInputVector(horizontal, vertical);
        
        // 3. ROTACIÓN DEL PERSONAJE (Estilo Aventura/GTA sin apuntar)
        // Si nos movemos y no estamos atacando (para no girar bruscamente en medio de un golpe)
        if (inputVector != Vector3.zero && !isAttacking)
        {
            // Girar suavemente hacia la dirección de movimiento
            Quaternion targetRotation = Quaternion.LookRotation(inputVector);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
        }

        // 4. Movimiento Físico
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

    // Helper clave: Convierte WASD a dirección relativa a la cámara
    private Vector3 GetWASDInputVector(float h, float v)
    {
        Vector3 f = Camera.main.transform.forward; 
        Vector3 r = Camera.main.transform.right;   
        
        // Aplanamos para no caminar hacia el cielo/suelo
        f.y = 0; 
        r.y = 0; 
        f.Normalize();
        r.Normalize();
        
        return (f * v + r * h).normalized;
    }

    private void HandleAbilityInput()
    {
        if (ASC != null && ASC.HasTag(EGameplayTag.State_Silenced)) return; 
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
            isAttacking = true; 
            ability.Activate();
        }
    }

    public void FinishAttack()
    {
        isAttacking = false; 
    }

    // ... (Métodos de Equipar Clase, Visuales, Animaciones IGUAL QUE ANTES) ...
    // Pégalos aquí para mantener el archivo completo.
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
            characterAnimator.SetInteger("ActionID", actionID);
            characterAnimator.SetTrigger(triggerName);
        }
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
            FinishAttack(); 
        }
    }

    /*private System.Collections.IEnumerator RespawnRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        characterController.enabled = false;
        transform.position = spawnPosition;
        characterController.enabled = true;
        ASC.Revive();
    }*/
    private System.Collections.IEnumerator RespawnRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        // --- LÓGICA DE REINICIO DE NIVEL ---
        // Obtenemos el nombre de la escena actual y la volvemos a cargar.
        // Esto resetea enemigos, rondas, vida, posición, TODO.
        string currentSceneName = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(currentSceneName);
    }
    public GameObject GetCurrentMainWeapon()
    {
        return currentMainWeapon; 
    }
    public Vector3 GetAimPoint(float maxRange = 100f)
    {
        // Rayo desde el centro de la cámara (0.5, 0.5)
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        
        // Ignoramos la capa del Jugador (para no apuntarnos a nosotros mismos)
        // Asumimos que el Player está en la capa "Default" o "Player". 
        // Lo ideal es usar una LayerMask que incluya Suelo y Enemigos.
        int layerMask = ~LayerMask.GetMask("Player"); // El símbolo ~ invierte (Todo MENOS Player)

        if (Physics.Raycast(ray, out RaycastHit hit, maxRange, layerMask))
        {
            return hit.point; // Golpeamos algo (suelo, pared, enemigo)
        }
        else
        {
            return ray.GetPoint(maxRange); // No golpeamos nada, apuntamos al horizonte
        }
    }

    // Fuerza al personaje a mirar hacia donde apunta la cámara (Usar antes de atacar)
    public void RotateToAim()
    {
        Vector3 targetPoint = GetAimPoint();
        Vector3 direction = (targetPoint - transform.position).normalized;
        direction.y = 0; // Aplanamos para que no mire al cielo/suelo y se caiga
        
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }
    public void TeleportToSpawn()
    {
        characterController.enabled = false;
        transform.position = spawnPosition;
        characterController.enabled = true;
    }
}