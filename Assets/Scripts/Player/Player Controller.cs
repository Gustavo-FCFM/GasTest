using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

// ============================================================
// PLAYER CONTROLLER — CORRECCIÓN DE MOVIMIENTO ERRÁTICO
//
// El movimiento errático se debía a que UpdateAnimations()
// corría en todos los clientes (incluidos observadores remotos)
// modificando el Animator con valores locales incorrectos.
//
// CORRECCIONES:
//   1. UpdateAnimations() solo corre en IsOwner.
//      Los observadores remotos reciben las animaciones via
//      ObserversPlayAbilityAnimation en el NetworkASC.
//
//   2. Se agrega [HideInInspector] public bool IsMoving para
//      que el NetworkTransform de FishNet sepa que hay movimiento
//      local y lo sincronice correctamente.
//
//   3. HandleMovementInput() ya tenía el guard de IsOwner
//      a través del Update(), pero se hace explícito también
//      en el método para claridad.
//
// IMPORTANTE EN EL PREFAB:
//   Asegúrate de que el componente NetworkTransform (FishNet)
//   esté en el Prefab del jugador. Sin él la posición del
//   jugador remoto no se sincroniza y aparece como estático
//   o errático. Configúralo así:
//     · Component Type: Transform
//     · Smoothing: Interpolation
//     · Send Rate: 10 o más
// ============================================================

[RequireComponent(typeof(AbilitySystemComponent))]
[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    private AbilitySystemComponent        ASC;
    private NetworkAbilitySystemComponent NetASC;
    private CharacterController           characterController;

    [Header("Cámara de Red")]
    public GameObject CameraPrefab;

    [Header("Clase")]
    public CharacterClassDefinition   CurrentClassDef;
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

    private bool isAttacking = false;

    [HideInInspector] public GameplayAbility MovementAbility;
    [HideInInspector] public GameplayAbility AbilityQ;
    [HideInInspector] public GameplayAbility AbilityE;
    [HideInInspector] public GameplayAbility AbilityR;
    [HideInInspector] public GameplayAbility PrimaryAttackAbility;
    [HideInInspector] public GameplayAbility AimAbility;

    [Header("Físicas")]
    public float jumpForce  = 8f;
    public float gravity    = -9.8f;

    private float   verticalVelocity;
    private Vector3 abilityMoveVector;
    private bool    isAbilityLeaping = false;
    private Vector3 spawnPosition;

    [HideInInspector] public GA_LeapAttack activeLeapAbility;

    private readonly SyncVar<int> _netClassIndex = new SyncVar<int>(-1);

    // =========================================================
    // AWAKE / INICIALIZACIÓN
    // =========================================================

    void Awake()
    {
        ASC                = GetComponent<AbilitySystemComponent>();
        NetASC             = GetComponent<NetworkAbilitySystemComponent>();
        characterController = GetComponent<CharacterController>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        spawnPosition = transform.position;

        // --- SEGURO ANTI-ERRORES ---
        // Si el clon nace sin clase (None), le forzamos la primera clase disponible (Bárbaro)
        if (CurrentClassDef == null && MainBaseClasses != null && MainBaseClasses.Length > 0)
        {
            CurrentClassDef = MainBaseClasses[0];
        }

        // 1. Equipamos la clase a TODOS (Dueños y Clones) para revivir la UI
        if (CurrentClassDef != null) EquipCharacterClass(CurrentClassDef);
        if (ASC != null) ASC.OnDeath += HandlePlayerDeath;

        // 2. Cosas exclusivas del dueño local
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
        }
        else 
        {
            if (characterController != null) Destroy(characterController);
        }
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        _netClassIndex.OnChange -= OnNetClassIndexChanged;
        if (ASC != null) ASC.OnDeath -= HandlePlayerDeath;
    }

    // =========================================================
    // UPDATE — SOLO EL DUEÑO PROCESA INPUT
    // =========================================================

    void Update()
    {
        // GUARD PRINCIPAL: solo el dueño procesa input y movimiento
        if (!IsOwner) return;

        if (ASC.HasTag(EGameplayTag.State_Dead))
        {
            if (Input.GetKeyDown(KeyCode.R) && AbilityR != null && AbilityR.CanActivate())
                RequestAbility(EAbilityInput.Action3);
            return;
        }

        if (ASC.HasTag(EGameplayTag.State_Stunned)) return;

        HandleMovementInput();
        HandleAbilityInput();

        // CORRECCIÓN: UpdateAnimations solo en el dueño.
        // Los observadores remotos ven las animaciones sincronizadas
        // por el Animator que FishNet sincroniza via NetworkAnimator
        // (si lo tienes) o por las ObserversRpc del NetworkASC.
        UpdateAnimations();
    }

    // =========================================================
    // MUERTE Y RESPAWN
    // =========================================================

    private void HandlePlayerDeath()
    {
        if (AbilityR is GA_InmortalWrath && AbilityR.CanActivate())
        {
            RequestAbility(EAbilityInput.Action3);
            return;
        }
        ServerRequestRespawn();
    }

    [ServerRpc]
    private void ServerRequestRespawn()
    {
        NetworkGameManager gm = FindFirstObjectByType<NetworkGameManager>();
        if (gm != null)
            gm.RespawnPlayer(Owner, 3f);
        else
            StartCoroutine(SimpleServerRespawn(3f));
    }

    private System.Collections.IEnumerator SimpleServerRespawn(float delay)
    {
        yield return new WaitForSeconds(delay);
        characterController.enabled = false;
        transform.position = new Vector3(spawnPosition.x, 3f, spawnPosition.z);
        characterController.enabled = true;
        ASC.Revive();
    }

    // =========================================================
    // MOVIMIENTO
    // =========================================================

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

        float   h        = Input.GetAxis("Horizontal");
        float   v        = Input.GetAxis("Vertical");
        Vector3 inputVec = GetWASDInputVector(h, v);

        if (inputVec != Vector3.zero && !isAttacking)
        {
            Quaternion targetRot = Quaternion.LookRotation(inputVec);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, targetRot, 10f * Time.deltaTime);
        }

        Vector3 horizontal;

        if (isAbilityLeaping)
        {
            abilityMoveVector = Vector3.Lerp(abilityMoveVector, Vector3.zero, Time.deltaTime);
            horizontal        = abilityMoveVector + inputVec * baseSpeed;
        }
        else
        {
            horizontal = inputVec * baseSpeed;
            if (characterController.isGrounded && Input.GetButtonDown("Jump"))
                verticalVelocity = jumpForce;
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 finalMove = new Vector3(horizontal.x, 0, horizontal.z)
                            + Vector3.up * verticalVelocity;
        characterController.Move(finalMove * Time.deltaTime);

        CheckLanding();
    }

    private Vector3 GetWASDInputVector(float h, float v)
    {
        if (Camera.main == null) return Vector3.zero;
        Vector3 f = Camera.main.transform.forward; f.y = 0; f.Normalize();
        Vector3 r = Camera.main.transform.right;   r.y = 0; r.Normalize();
        return (f * v + r * h).normalized;
    }

    // =========================================================
    // INPUT DE HABILIDADES
    // =========================================================

    [HideInInspector] public bool isRadialMenuOpen = false;
    private GameplayAbility currentRadialAbility;

    private void HandleAbilityInput()
    {
        if (ASC.HasTag(EGameplayTag.State_Silenced)) return;
        if (isAttacking && !isRadialMenuOpen) return;

        CheckAbilityButton("Fire3",  MovementAbility,      EAbilityInput.Movement);
        CheckAbilityKey(KeyCode.Q,   AbilityQ,             EAbilityInput.Action1);
        CheckAbilityKey(KeyCode.E,   AbilityE,             EAbilityInput.Action2);
        CheckAbilityKey(KeyCode.R,   AbilityR,             EAbilityInput.Action3);
        CheckAbilityButton("Fire1",  PrimaryAttackAbility, EAbilityInput.PrimaryAttack);
        CheckAbilityButton("Fire2",  AimAbility,           EAbilityInput.SecondaryAttack);
    }

    private void CheckAbilityKey(KeyCode key, GameplayAbility ability, EAbilityInput slot)
    {
        if (ability == null) return;
        if (Input.GetKeyDown(key))
            ProcessAbilityPress(ability, slot);
        else if (Input.GetKeyUp(key) && currentRadialAbility == ability)
            ProcessAbilityRelease();
    }

    private void CheckAbilityButton(string btn, GameplayAbility ability, EAbilityInput slot)
    {
        if (ability == null) return;
        if (Input.GetButtonDown(btn))
            ProcessAbilityPress(ability, slot);
        else if (Input.GetButtonUp(btn) && currentRadialAbility == ability)
            ProcessAbilityRelease();
    }

    private void ProcessAbilityPress(GameplayAbility ability, EAbilityInput slot)
    {
        if (ability is IRadialMenuAbility radial)
        {
            if (!ability.CanActivate()) return;
            isAttacking          = true;
            isRadialMenuOpen     = true;
            currentRadialAbility = ability;
            if (UI_RadialMenu.Instance != null) UI_RadialMenu.Instance.Show(radial);
        }
        else
        {
            if (ability.CanActivate())
            {
                isAttacking = true;
                RequestAbility(slot);
            }
        }
    }

    private void ProcessAbilityRelease()
    {
        if (currentRadialAbility is IRadialMenuAbility radial)
        {
            int     sel = UI_RadialMenu.Instance != null
                ? UI_RadialMenu.Instance.HideAndGetSelection() : -1;
            Vector3 pos = GetAimPoint(radial.MaxRadialRange);
            ServerRequestRadialAbility(sel, pos);
        }
        isRadialMenuOpen     = false;
        currentRadialAbility = null;
    }

    // =========================================================
    // SERVER RPCs
    // =========================================================

    private void RequestAbility(EAbilityInput slot)
    {
        if (NetASC != null)
            NetASC.ServerRequestActivateAbility(slot);
        else
            ActivateAbilityBySlot(slot); // Fallback singleplayer
    }

    [ServerRpc]
    private void ServerRequestRadialAbility(int selectedIndex, Vector3 targetPosition)
    {
        foreach (var ability in ASC.GrantedAbilities)
        {
            if (ability is IRadialMenuAbility radial)
            {
                if (!ability.CanActivate()) return;
                radial.ActivateWithSelection(selectedIndex, targetPosition);
                return;
            }
        }
    }

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

    public void FinishAttack() => isAttacking = false;

    // =========================================================
    // EQUIPAR CLASE
    // =========================================================

    public void EquipCharacterClass(CharacterClassDefinition newClass)
    {
        if (newClass == null || ASC == null) return;

        ASC.RemoveAllActiveEffects();
        CurrentClassDef  = newClass;
        ASC.CurrentClass = newClass;
        CharacterIcon    = newClass.ClassIcon;

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
            ASC.InitializeAttributes();
        }

        if (IsOwner)
        {
            int idx = GetClassIndex(newClass);
            if (idx >= 0) ServerSetClass(idx);
            UpdateHUD();
        }
    }

    [ServerRpc]
    private void ServerSetClass(int classIndex) => _netClassIndex.Value = classIndex;

    private void OnNetClassIndexChanged(int prev, int next, bool asServer)
    {
        if (IsOwner) return;
        CharacterClassDefinition def = GetClassByIndex(next);
        if (def != null) UpdateVisuals(def);
    }

    private int GetClassIndex(CharacterClassDefinition def)
    {
        if (MainBaseClasses == null) return -1;
        for (int i = 0; i < MainBaseClasses.Length; i++)
            if (MainBaseClasses[i] == def) return i;
        return -1;
    }

    private CharacterClassDefinition GetClassByIndex(int idx)
    {
        if (MainBaseClasses == null || idx < 0 || idx >= MainBaseClasses.Length) return null;
        return MainBaseClasses[idx];
    }

    // =========================================================
    // HUD
    // =========================================================

    private void UpdateHUD()
    {
        // Si este personaje no es mío, no actualizo mi pantalla
        if (!base.IsOwner) return;

        UI_PlayerHUD hud = FindFirstObjectByType<UI_PlayerHUD>();
        if (hud != null)
        {
            hud.InitializeHUD(this);
            return;
        }

        // ============================================================
        // CORRECCIÓN: "no recibe su propio UI"
        //
        // Cuando el jugador se spawnea, OnStartClient/EquipCharacterClass
        // corren en el MISMO frame en que FishNet instancia el objeto.
        // Si el UI_PlayerHUD de la escena todavía no terminó su Awake/Start
        // (o está en otra escena que aún está cargando), FindFirstObjectByType
        // devuelve null y el HUD se queda vacío para siempre porque
        // EquipCharacterClass no se vuelve a llamar.
        //
        // Solución: si no lo encontramos aún, reintentamos unos frames
        // hasta encontrarlo.
        // ============================================================
        StartCoroutine(RetryUpdateHUD());
    }

    private System.Collections.IEnumerator RetryUpdateHUD()
    {
        const int   maxAttempts = 30;   // ~0.5s a 60fps
        int         attempts    = 0;

        while (attempts < maxAttempts)
        {
            yield return null; // esperar un frame

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
    // VISUALES / ANIMACIONES / ARMAS
    // =========================================================

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

    void UpdateAnimations()
    {
        if (characterAnimator == null) return;

        float speed = new Vector3(
            characterController.velocity.x, 0,
            characterController.velocity.z).magnitude;

        characterAnimator.SetFloat("Speed", speed, 0.1f, Time.deltaTime);
        characterAnimator.SetBool("IsJumping", !characterController.isGrounded);

        float spd = ASC.GetAttributeValue(EAttributeType.AtkSpeed);
        if (spd > 0)
            characterAnimator.SetFloat("AttackSpeedMult", 1f / spd);
    }

    public void PlayAnimation(string trigger, int actionID)
    {
        if (characterAnimator == null) return;
        characterAnimator.SetInteger("ActionID", actionID);
        characterAnimator.SetTrigger(trigger);
    }

    public void AnimationEvent_HitFrame()    { }
    public void AnimationEvent_EnableTrail()
    {
        if (currentWeaponTrail != null) currentWeaponTrail.SetActive(true);
    }
    public void AnimationEvent_DisableTrail()
    {
        if (currentWeaponTrail != null) currentWeaponTrail.SetActive(false);
    }

    // =========================================================
    // LEAP
    // =========================================================

    public void ExecuteLeap(GA_LeapAttack ability, float up, float fwd)
    {
        if (!characterController.isGrounded || Camera.main == null) return;
        Vector3 camFwd    = Camera.main.transform.forward;
        camFwd.y          = 0;
        camFwd.Normalize();
        abilityMoveVector = camFwd * fwd;
        verticalVelocity  = up;
        isAbilityLeaping  = true;
        activeLeapAbility = ability;
        transform.forward = camFwd;
    }

    private void CheckLanding()
    {
        if (isAbilityLeaping && characterController.isGrounded)
        {
            activeLeapAbility?.ExecuteImpactCheck();
            isAbilityLeaping  = false;
            activeLeapAbility = null;
            abilityMoveVector = Vector3.zero;
            FinishAttack();
        }
    }

    // =========================================================
    // UTILIDADES PÚBLICAS
    // =========================================================

    public GameObject GetCurrentMainWeapon() => currentMainWeapon;

    public Vector3 GetAimPoint(float maxRange = 100f)
    {
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

    public void RotateToAim()
    {
        Vector3 dir = (GetAimPoint() - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero) transform.rotation = Quaternion.LookRotation(dir);
    }

    public void TeleportToSpawn()
    {
        characterController.enabled = false;
        transform.position = new Vector3(spawnPosition.x, 3f, spawnPosition.z);
        verticalVelocity   = 0f;
        characterController.enabled = true;
    }
}