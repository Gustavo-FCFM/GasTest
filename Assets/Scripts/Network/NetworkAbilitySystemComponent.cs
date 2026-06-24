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
    public void ServerRequestActivateAbility(EAbilityInput inputSlot)
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

        ability.Activate();
        ObserversPlayAbilityAnimation(inputSlot);
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