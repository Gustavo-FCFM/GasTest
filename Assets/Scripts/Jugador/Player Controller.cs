using UnityEngine;
using UnityEngine.SceneManagement;
using FishNet.Object;

[RequireComponent(typeof(AbilitySystemComponent))]
[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    // --- Referencias ---
    private AbilitySystemComponent ASC;
    private CharacterController characterController;

    [Header("Configuración de Cámara de Red")]
    [Tooltip("Arrastre aquí el Prefab de su cámara (ThirdPersonOrbitCam)")]
    public GameObject CameraPrefab;

    [Header("Configuración de Clase")]
    public CharacterClassDefinition CurrentClassDef;
    public CharacterClassDefinition[] MainBaseClasses;

    [Header("UI & Visuals")] 
    public Sprite CharacterIcon; 
    [Header("Animación")]
    public Animator characterAnimator;
    [Header("Referencias de Huesos")]
    public Transform MainHandSocket; 
    public Transform OffHandSocket;

    private GameObject currentMainWeapon;
    private GameObject currentOffWeapon;
    private GameObject currentWeaponTrail;

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
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // LOG 1: Saber exactamente dónde cree el juego que nacimos
        spawnPosition = transform.position;
        Debug.LogWarning($"[PlayerController] OnStartClient: Mi spawnPosition guardado es {spawnPosition}");

        if (base.IsOwner)
        {
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
            }

            if (CurrentClassDef != null) EquipCharacterClass(CurrentClassDef);
            if (ASC != null) ASC.OnDeath += HandlePlayerDeath;
        }
    }

    void Update()
    {
        if (!base.IsOwner) return;

        if (ASC.HasTag(EGameplayTag.State_Dead))
        {
            if (Input.GetKeyDown(KeyCode.R) || Input.GetButtonDown("Ultimate"))
            {
                if (AbilityR != null && AbilityR.CanActivate()) AbilityR.Activate();
            }
            return; 
        }
        
        if (ASC != null && ASC.HasTag(EGameplayTag.State_Stunned)) return;
        
        // --- LOG 2: EL SEMÁFORO DE LA MUERTE ---
        
        HandleMovementInput(); 
        HandleAbilityInput();  
        UpdateAnimations();
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

    private void HandleMovementInput()
    {
        if (ASC != null && ASC.HasTag(EGameplayTag.State_Rooted))
        {
            verticalVelocity += gravity * Time.deltaTime;
            characterController.Move(Vector3.up * verticalVelocity * Time.deltaTime);
            return; 
        }

        // --- SOLUCIÓN DE GRAVEDAD INFINITA ---
        // Si tocamos el piso y la velocidad va hacia abajo, la mantenemos en un número pequeño (-2f)
        // Esto evita que se acumule fuerza masiva y permite que characterController.isGrounded siga funcionando.
        if (characterController.isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f;
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
        
        if (inputVector != Vector3.zero && !isAttacking)
        {
            Quaternion targetRotation = Quaternion.LookRotation(inputVector);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
        }

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
        if (Camera.main == null) return Vector3.zero;
        Vector3 f = Camera.main.transform.forward; 
        Vector3 r = Camera.main.transform.right;   
        f.y = 0; r.y = 0; 
        f.Normalize(); r.Normalize();
        return (f * v + r * h).normalized;
    }

    [HideInInspector] public bool isRadialMenuOpen = false;
    private GameplayAbility currentRadialAbility = null;
    
    private void HandleAbilityInput()
    {
        if (ASC != null && ASC.HasTag(EGameplayTag.State_Silenced)) return; 
        if (isAttacking && !isRadialMenuOpen) return; 

        CheckAbilityButton("Fire3", MovementAbility); 
        CheckAbilityKey(KeyCode.Q, AbilityQ);
        CheckAbilityKey(KeyCode.E, AbilityE);
        CheckAbilityKey(KeyCode.R, AbilityR);
        CheckAbilityButton("Fire1", PrimaryAttackAbility); 
        CheckAbilityButton("Fire2", AimAbility);
    }

    private void CheckAbilityKey(KeyCode key, GameplayAbility ability)
    {
        if (ability == null) return;
        if (Input.GetKeyDown(key)) ProcessAbilityPress(ability);
        else if (Input.GetKeyUp(key) && currentRadialAbility == ability) ProcessAbilityRelease();
    }

    private void CheckAbilityButton(string buttonName, GameplayAbility ability)
    {
        if (ability == null) return;
        if (Input.GetButtonDown(buttonName)) ProcessAbilityPress(ability);
        else if (Input.GetButtonUp(buttonName))
        {
            if (currentRadialAbility == ability) ProcessAbilityRelease();
        }
    }

    private void ProcessAbilityPress(GameplayAbility ability)
    {
        if (ability is IRadialMenuAbility radialAbility)
        {
            if (!ability.CanActivate()) return; 
            isAttacking = true;
            isRadialMenuOpen = true;
            currentRadialAbility = ability;
            if (UI_RadialMenu.Instance != null) UI_RadialMenu.Instance.Show(radialAbility);
        }
        else
        {
            TryActivateAbility(ability);
        }
    }

    private void ProcessAbilityRelease()
    {
        if (currentRadialAbility is IRadialMenuAbility radialAbility)
        {
            int seleccionReal = 0;
            if (UI_RadialMenu.Instance != null) seleccionReal = UI_RadialMenu.Instance.HideAndGetSelection();
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

    public void FinishAttack()
    {
        isAttacking = false; 
    }

    public void EquipCharacterClass(CharacterClassDefinition newClass)
    {
        if (newClass == null || ASC == null) return;
        ASC.RemoveAllActiveEffects();
        CurrentClassDef = newClass;
        ASC.CurrentClass = newClass;
        CharacterIcon = newClass.ClassIcon;
        UpdateVisuals(newClass);
        ASC.ClearGrantedAbilities();
        ResetAbilitySlots();
        foreach (var assignment in newClass.Abilities)
        {
            if (assignment.Ability == null) continue;
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
        
        UpdateHUD(); // <-- Manda a llamar al HUD local de esta ventana
        Debug.Log($"[PlayerController] Clase equipada: {newClass.ClassName}");
    }

    private void ResetAbilitySlots()
    {
        MovementAbility = null; AbilityQ = null; AbilityE = null;
        PrimaryAttackAbility = null; AimAbility = null; AbilityR = null;
    }

    private void UpdateHUD()
    {
        // En FishNet, buscamos el Canvas que está en la escena y le pasamos 'this'
        UI_PlayerHUD hud = FindFirstObjectByType<UI_PlayerHUD>();
        if (hud != null) 
        {
            hud.InitializeHUD(this); 
        }
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
            Transform trailTransform = currentOffWeapon.transform.Find("WeaponTrail");
            if (trailTransform != null)
            {
                currentWeaponTrail = trailTransform.gameObject;
                currentWeaponTrail.SetActive(false); 
            }
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

    public void AnimationEvent_HitFrame() { }
    public void AnimationEvent_EnableTrail() { if (currentWeaponTrail != null) currentWeaponTrail.SetActive(true); }
    public void AnimationEvent_DisableTrail() { if (currentWeaponTrail != null) currentWeaponTrail.SetActive(false); }

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
        if (Camera.main == null) return;
        
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

    private System.Collections.IEnumerator RespawnRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        UnityEngine.SceneManagement.SceneManager.LoadScene(currentSceneName);
    }

    public GameObject GetCurrentMainWeapon()
    {
        return currentMainWeapon; 
    }

    public Vector3 GetAimPoint(float maxRange = 100f)
    {
        if (Camera.main == null) return transform.position + transform.forward * 10f;
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit[] hits = Physics.RaycastAll(ray, maxRange);
        
        float closestDistance = float.MaxValue;
        Vector3 bestPoint = ray.GetPoint(maxRange); 

        foreach (RaycastHit hit in hits)
        {
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

    public void TeleportToSpawn()
    {
        // LOG 3: Confirmar que el código ejecuta el teletransporte y hacia dónde
        Debug.LogWarning($"[PlayerController] EJECUTANDO TELETRANSPORTE. Moviendo al jugador desde {transform.position} hacia Y=3.");
        
        characterController.enabled = false;
        transform.position = new Vector3(spawnPosition.x, 3f, spawnPosition.z);
        verticalVelocity = 0f; 
        characterController.enabled = true;
    }
}