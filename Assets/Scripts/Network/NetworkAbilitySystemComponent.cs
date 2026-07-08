using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;

// ============================================================
// NetworkAbilitySystemComponent
//
// ARQUITECTURA CORREGIDA:
//   Este componente hereda de NetworkBehaviour (FishNet) y
//   vive en el mismo GameObject que AbilitySystemComponent.
//   YA NO hereda de AbilitySystemComponent — eso causaba el
//   conflicto porque MonoBehaviour no tiene OnStartServer,
//   OnStartClient ni IsServerInitialized.
//
// CÓMO USARLO EN EL PREFAB DEL JUGADOR:
//   El prefab debe tener AMBOS componentes:
//     · AbilitySystemComponent   (lógica de juego, sin cambios)
//     · NetworkAbilitySystemComponent  (este script, sincronización)
//     · NetworkObject            (FishNet, obligatorio)
//     · NetworkTransform         (FishNet, para posición)
//
// QUÉ HACE:
//   - Al iniciar en servidor, lee los valores del ASC local y
//     los publica en SyncVars para que todos los clientes los vean.
//   - Cada vez que el ASC cambia un atributo (via los hooks
//     OnAttributeChanged / OnTagAdded / OnTagRemoved que
//     agregamos al ASC), este componente actualiza los SyncVars.
//   - Recibe ServerRpc del PlayerController para activar
//     habilidades en el servidor.
//   - Notifica a todos los clientes con ObserversRpc cuando
//     ocurren eventos visuales (animaciones, muerte, revivir).
// ============================================================

[RequireComponent(typeof(AbilitySystemComponent))]
public class NetworkAbilitySystemComponent : NetworkBehaviour
{
    // Referencia al ASC local (mismo GameObject)
    private AbilitySystemComponent _asc;

    // =========================================================
    // SYNCVARS — Servidor escribe, todos los clientes leen
    // =========================================================

    private readonly SyncVar<float> _netHealth    = new SyncVar<float>();
    private readonly SyncVar<float> _netMaxHealth = new SyncVar<float>();
    private readonly SyncVar<float> _netMana      = new SyncVar<float>();
    private readonly SyncVar<float> _netMaxMana   = new SyncVar<float>();
    private readonly SyncVar<float> _netEnergy    = new SyncVar<float>();
    private readonly SyncVar<float> _netShield    = new SyncVar<float>();
    private readonly SyncVar<int>   _netTeamID    = new SyncVar<int>();
    private readonly SyncVar<float> _netLevel     = new SyncVar<float>();
    private readonly SyncVar<float> _netExp       = new SyncVar<float>();

    // Tags activos para UI remota y efectos visuales
    public readonly SyncHashSet<EGameplayTag> NetTags = new SyncHashSet<EGameplayTag>();

    // =========================================================
    // COOLDOWNS EN RED
    //
    // ASC.ActiveEffects (de donde sale GetCooldownStatus) SOLO existe en la
    // copia del servidor — cada GameplayEffect es un ScriptableObject clonado
    // por proceso, así que ni siquiera tiene sentido "sincronizar la lista".
    //
    // En vez de eso mandamos, por slot de habilidad: el TICK en que empezó
    // el cooldown "vigente" y cuánto dura desde ese tick. El cliente calcula
    // el tiempo restante localmente con TimeManager.Tick (que aproxima el
    // tick del servidor), sin necesitar un paquete de red por frame.
    //
    // Se re-sincroniza cada CooldownSyncInterval segundos, no solo al activar
    // la habilidad — así, si algo reduce el cooldown a mitad de camino (carga
    // de ultimate por ChargeUltimate/ReduceCooldownByTag), el próximo barrido
    // lo corrige sin tener que enganchar cada lugar que podría modificarlo.
    // =========================================================

    public readonly SyncDictionary<EAbilityInput, uint>  NetCooldownStartTick = new SyncDictionary<EAbilityInput, uint>();
    public readonly SyncDictionary<EAbilityInput, float> NetCooldownDuration  = new SyncDictionary<EAbilityInput, float>();

    private static readonly EAbilityInput[] _cooldownSlots =
    {
        EAbilityInput.PrimaryAttack, EAbilityInput.SecondaryAttack,
        EAbilityInput.Action1, EAbilityInput.Action2, EAbilityInput.Action3,
        EAbilityInput.Movement
    };

    private const float CooldownSyncInterval = 0.25f;
    private float _cooldownSyncTimer;

    // =========================================================
    // PROPIEDADES PÚBLICAS DE LECTURA (para la UI y otros scripts)
    // =========================================================

    public float NetHealth    => _netHealth.Value;
    public float NetMaxHealth => _netMaxHealth.Value;
    public float NetMana      => _netMana.Value;
    public float NetMaxMana   => _netMaxMana.Value;
    public float NetEnergy    => _netEnergy.Value;
    public float NetShield    => _netShield.Value;
    public int   NetTeamID    => _netTeamID.Value;
    public float NetLevel     => _netLevel.Value;
    public float NetExp       => _netExp.Value;

    // =========================================================
    // AWAKE — Obtener referencia al ASC y suscribir hooks
    // =========================================================

    private void Awake()
    {
        _asc = GetComponent<AbilitySystemComponent>();

        if (_asc == null)
        {
            Debug.LogError("[NetworkASC] No se encontró AbilitySystemComponent en el mismo GameObject.");
            return;
        }

        // Suscribir los hooks que agregamos al ASC base
        _asc.OnAttributeChangedCallback += HandleAttributeChanged;
        _asc.OnTagAddedCallback         += HandleTagAdded;
        _asc.OnTagRemovedCallback       += HandleTagRemoved;

        // Suscribir eventos de muerte y revivir
        _asc.OnDeath  += HandleDeath;
        _asc.OnRevive += HandleRevive;
    }

    private void Update()
    {
        // Solo el servidor calcula y publica los cooldowns; los clientes los
        // leen vía TryGetNetCooldown().
        if (!IsServerInitialized || _asc == null) return;

        _cooldownSyncTimer += Time.deltaTime;
        if (_cooldownSyncTimer < CooldownSyncInterval) return;
        _cooldownSyncTimer = 0f;

        foreach (EAbilityInput slot in _cooldownSlots)
            SyncCooldownForSlot(slot);
    }

    [Server]
    private void SyncCooldownForSlot(EAbilityInput slot)
    {
        GameplayAbility ability = FindAbilityBySlot(slot);
        if (ability == null) return;

        if (_asc.GetCooldownStatus(ability, out float remaining, out _))
        {
            NetCooldownStartTick[slot] = TimeManager.Tick;
            NetCooldownDuration[slot]  = remaining;
        }
        else if (NetCooldownDuration.ContainsKey(slot))
        {
            NetCooldownStartTick.Remove(slot);
            NetCooldownDuration.Remove(slot);
        }
    }

    // Usado por la UI (UI_AbilitySlot / UI_UltimateSlot) para leer el
    // cooldown real sin depender del ASC local (que en el cliente remoto
    // nunca tiene ActiveEffects poblado).
    public bool TryGetNetCooldown(EAbilityInput slot, out float remaining, out float total)
    {
        remaining = 0f;

        if (!NetCooldownDuration.TryGetValue(slot, out total)) { total = 0f; return false; }
        if (!NetCooldownStartTick.TryGetValue(slot, out uint startTick)) return false;

        uint   elapsedTicks   = TimeManager.Tick > startTick ? (TimeManager.Tick - startTick) : 0;
        double elapsedSeconds = TimeManager.TicksToTime(elapsedTicks);

        remaining = Mathf.Max(0f, total - (float)elapsedSeconds);
        return remaining > 0f;
    }

    private void OnDestroy()
    {
        if (_asc == null) return;

        _asc.OnAttributeChangedCallback -= HandleAttributeChanged;
        _asc.OnTagAddedCallback         -= HandleTagAdded;
        _asc.OnTagRemovedCallback       -= HandleTagRemoved;
        _asc.OnDeath                    -= HandleDeath;
        _asc.OnRevive                   -= HandleRevive;

        // Desuscribir SyncVar callbacks
        _netHealth.OnChange    -= OnNetHealthChanged;
        _netMaxHealth.OnChange -= OnNetMaxHealthChanged;
        _netMana.OnChange      -= OnNetManaChanged;
        _netMaxMana.OnChange   -= OnNetMaxManaChanged;
        _netEnergy.OnChange    -= OnNetEnergyChanged;
        _netShield.OnChange    -= OnNetShieldChanged;
        _netTeamID.OnChange    -= OnNetTeamIDChanged;
        _netLevel.OnChange     -= OnNetLevelChanged;
        _netExp.OnChange       -= OnNetExpChanged;
        NetTags.OnChange       -= OnNetTagsChanged;
    }

    // =========================================================
    // CALLBACKS DE FISHNET
    // =========================================================

    public override void OnStartServer()
    {
        base.OnStartServer();

        // ============================================================
        // CORRECCIÓN BUG TeamID = 0:
        //
        // Antes aquí se hacía "_asc.TeamID = base.OwnerId + 1", pero
        // OwnerId todavía no está poblado de forma confiable en este
        // punto del ciclo de vida de FishNet (justo al spawnear),
        // por lo que devuelve -1 y el resultado siempre era TeamID = 0.
        //
        // El NetworkGameManager YA llama AssignTeam(uniqueTeamID)
        // inmediatamente después de hacer Spawn() del jugador, con
        // el número de jugador correcto (1, 2, 3, ...). Esa es la
        // única fuente de verdad para el TeamID — no la dupliques aquí.
        //
        // Dejamos esto vacío (más allá del base.OnStartServer()) para
        // no pisar el valor que el GameManager va a asignar a continuación.
        // ============================================================
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // Suscribir callbacks para recibir cambios del servidor
        _netHealth.OnChange    += OnNetHealthChanged;
        _netMaxHealth.OnChange += OnNetMaxHealthChanged;
        _netMana.OnChange      += OnNetManaChanged;
        _netMaxMana.OnChange   += OnNetMaxManaChanged;
        _netEnergy.OnChange    += OnNetEnergyChanged;
        _netShield.OnChange    += OnNetShieldChanged;
        _netTeamID.OnChange    += OnNetTeamIDChanged;
        _netLevel.OnChange     += OnNetLevelChanged;
        _netExp.OnChange       += OnNetExpChanged;
        NetTags.OnChange       += OnNetTagsChanged;
    }

    // =========================================================
    // HOOKS DEL ASC — Cuando el ASC cambia algo, actualizamos SyncVars
    // =========================================================

    private void HandleAttributeChanged(EAttributeType type, float value)
    {
        // Solo el servidor actualiza los SyncVars
        if (!IsServerInitialized) return;

        switch (type)
        {
            case EAttributeType.Health:    _netHealth.Value    = value; break;
            case EAttributeType.MaxHealth: _netMaxHealth.Value = value; break;
            case EAttributeType.Mana:      _netMana.Value      = value; break;
            case EAttributeType.MaxMana:   _netMaxMana.Value   = value; break;
            case EAttributeType.Energy:    _netEnergy.Value    = value; break;
            case EAttributeType.Shield:    _netShield.Value    = value; break;
            case EAttributeType.Level:     _netLevel.Value     = value; break;
            case EAttributeType.Exp:       _netExp.Value       = value; break;
        }
    }

    private void HandleTagAdded(EGameplayTag tag)
    {
        if (!IsServerInitialized) return;
        NetTags.Add(tag);
    }

    private void HandleTagRemoved(EGameplayTag tag)
    {
        if (!IsServerInitialized) return;
        NetTags.Remove(tag);
    }

    // =========================================================
    // CALLBACKS DE SYNCVAR — Cliente recibe cambio, actualiza ASC local
    // =========================================================

    private void OnNetHealthChanged(float prev, float next, bool asServer)
    {
        if (!asServer && _asc != null) _asc.SetCurrentAttributeValue(EAttributeType.Health, next);
    }
    private void OnNetMaxHealthChanged(float prev, float next, bool asServer)
    {
        if (!asServer && _asc != null) _asc.SetCurrentAttributeValue(EAttributeType.MaxHealth, next);
    }
    private void OnNetManaChanged(float prev, float next, bool asServer)
    {
        if (!asServer && _asc != null) _asc.SetCurrentAttributeValue(EAttributeType.Mana, next);
    }
    private void OnNetMaxManaChanged(float prev, float next, bool asServer)
    {
        if (!asServer && _asc != null) _asc.SetCurrentAttributeValue(EAttributeType.MaxMana, next);
    }
    private void OnNetEnergyChanged(float prev, float next, bool asServer)
    {
        if (!asServer && _asc != null) _asc.SetCurrentAttributeValue(EAttributeType.Energy, next);
    }
    private void OnNetShieldChanged(float prev, float next, bool asServer)
    {
        if (!asServer && _asc != null) _asc.SetCurrentAttributeValue(EAttributeType.Shield, next);
    }
    private void OnNetTeamIDChanged(int prev, int next, bool asServer)
    {
        if (!asServer && _asc != null) _asc.TeamID = next;
    }
    private void OnNetLevelChanged(float prev, float next, bool asServer)
    {
        if (!asServer && _asc != null) _asc.SetCurrentAttributeValue(EAttributeType.Level, next);
    }
    private void OnNetExpChanged(float prev, float next, bool asServer)
    {
        if (!asServer && _asc != null) _asc.SetCurrentAttributeValue(EAttributeType.Exp, next);
    }

    // NetTags se llenaba del lado servidor (HandleTagAdded/HandleTagRemoved)
    // pero nada aplicaba esos cambios de vuelta al ASC local del cliente.
    // Sin esto, HasTag() en la copia del dueño remoto nunca se entera de
    // Stunned/Rooted/Silenced/Dead/cooldowns — por eso el UI de cooldown
    // nunca se llenaba para el jugador 2.
    private void OnNetTagsChanged(SyncHashSetOperation op, EGameplayTag item, bool asServer)
    {
        if (asServer || _asc == null) return;

        switch (op)
        {
            case SyncHashSetOperation.Add:
                _asc.AddTag(item);
                break;
            case SyncHashSetOperation.Remove:
                _asc.RemoveTag(item);
                break;
        }
    }

    // =========================================================
    // ASIGNACIÓN DE EQUIPO (el GameManager llama esto en el servidor)
    // =========================================================

    [Server]
    public void AssignTeam(int teamID)
    {
        if (_asc != null) _asc.TeamID = teamID;
        _netTeamID.Value = teamID;
        Debug.Log($"{gameObject.name} → Equipo {teamID}");
    }

    // =========================================================
    // ACTIVACIÓN DE HABILIDADES POR RED
    // El PlayerController llama ServerRequestActivateAbility()
    // como ServerRpc. El servidor valida y ejecuta.
    // =========================================================

    [ServerRpc]
    public void ServerRequestActivateAbility(EAbilityInput inputSlot, Vector3 aimPoint)
    {
        if (_asc == null) return;

        GameplayAbility ability = FindAbilityBySlot(inputSlot);

        if (ability == null)
        {
            Debug.LogWarning($"[Server] No se encontró habilidad en slot {inputSlot}");
            return;
        }

        if (!ability.CanActivate())
        {
            Debug.Log($"[Server] Habilidad {ability.AbilityName} bloqueada.");
            return;
        }

        // El dueño calculó este punto con SU cámara y nos lo mandó; lo dejamos
        // disponible en el PlayerController para que RotateToAim()/GetAimPoint()
        // lo usen en vez de intentar leer Camera.main en el servidor.
        PlayerController pc = GetComponent<PlayerController>();
        if (pc != null) pc.NetworkAimPoint = aimPoint;

        ability.Activate();
        ObserversPlayAbilityAnimation(inputSlot);
    }

    // =========================================================
    // FIN DE HABILIDAD — avisar al dueño real (isAttacking es local,
    // no un SyncVar, así que si no le avisamos por red al dueño remoto,
    // se queda trabado en "atacando" para siempre).
    // =========================================================

    [Server]
    public void ServerNotifyAbilityEnded()
    {
        ObserversFinishAttack();
    }

    [ObserversRpc]
    private void ObserversFinishAttack()
    {
        // El host ya se resetea directo en GameplayAbility.EndAbility()
        // (server == dueño ahí); esto es sobre todo para el dueño remoto.
        if (IsServerInitialized) return;

        PlayerController pc = GetComponent<PlayerController>();
        if (pc != null) pc.FinishAttack();
    }

    [ObserversRpc]
    private void ObserversPlayAbilityAnimation(EAbilityInput inputSlot)
    {
        // El dueño ya disparó la animación localmente al presionar el botón
        if (IsOwner) return;

        GameplayAbility ability = FindAbilityBySlot(inputSlot);
        if (ability == null) return;

        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null && !string.IsNullOrEmpty(ability.AnimationTriggerName))
        {
            anim.SetInteger("ActionID", ability.AnimationID);
            anim.SetTrigger(ability.AnimationTriggerName);
        }
    }

    // =========================================================
    // MUERTE Y REVIVIR — Notificar a todos los clientes
    // =========================================================

    private void HandleDeath()
    {
        if (!IsServerInitialized) return;
        ObserversHandleDeath();
    }

    private void HandleRevive()
    {
        if (!IsServerInitialized) return;
        ObserversHandleRevive();
    }

    [ObserversRpc]
    private void ObserversHandleDeath()
    {
        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null) anim.SetTrigger("Death");
    }

    [ObserversRpc]
    private void ObserversHandleRevive()
    {
        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null) anim.SetTrigger("Revive");
    }

    // =========================================================
    // EXPERIENCIA (el servidor la otorga, ej. al matar enemigo)
    // =========================================================

    [Server]
    public void ServerGainExperience(float amount)
    {
        if (_asc != null) _asc.GainExperience(amount);
    }

    // =========================================================
    // REVIVIR DESDE RED (llamado por NetworkGameManager)
    // =========================================================

    [Server]
    public void Revive()
    {
        if (_asc != null) _asc.Revive();
    }

    // =========================================================
    // SINCRONIZAR TODOS LOS ATRIBUTOS AL SERVIDOR
    // =========================================================

    [Server]
    public void SyncAllAttributesToNet()
    {
        if (_asc == null) return;

        _netHealth.Value    = _asc.GetAttributeValue(EAttributeType.Health);
        _netMaxHealth.Value = _asc.GetAttributeValue(EAttributeType.MaxHealth);
        _netMana.Value      = _asc.GetAttributeValue(EAttributeType.Mana);
        _netMaxMana.Value   = _asc.GetAttributeValue(EAttributeType.MaxMana);
        _netEnergy.Value    = _asc.GetAttributeValue(EAttributeType.Energy);
        _netShield.Value    = _asc.GetAttributeValue(EAttributeType.Shield);
        _netLevel.Value     = _asc.GetAttributeValue(EAttributeType.Level);
        _netExp.Value       = _asc.GetAttributeValue(EAttributeType.Exp);
        _netTeamID.Value    = _asc.TeamID;
    }

    // =========================================================
    // UTILIDADES INTERNAS
    // =========================================================

    private GameplayAbility FindAbilityBySlot(EAbilityInput slot)
    {
        if (_asc == null || _asc.CurrentClass == null) return null;

        foreach (var assignment in _asc.CurrentClass.Abilities)
        {
            if (assignment.InputSlot == slot)
            {
                foreach (var granted in _asc.GrantedAbilities)
                {
                    if (granted.AbilityName == assignment.Ability.AbilityName)
                        return granted;
                }
            }
        }
        return null;
    }

    
}