using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;

// ============================================================
// NetworkAbilitySystemComponent
//
// Capa de red de un personaje: vive en el mismo GameObject que
// AbilitySystemComponent (que no sabe nada de FishNet) y traduce sus
// cambios (atributos, tags, efectos activos) a SyncVars/RPCs para que
// todos los clientes los vean. También recibe las peticiones de
// activar habilidades desde PlayerController y actúa de puente para
// todo lo que necesita autoridad de servidor + ejecución en el
// cliente dueño (salto, VFX, secuencias visuales).
//
// PREFAB DEL JUGADOR: necesita AMBOS componentes en el mismo
// GameObject — AbilitySystemComponent (lógica de juego) y este script
// (sincronización) — más NetworkObject y NetworkTransform de FishNet.
// ============================================================
[RequireComponent(typeof(AbilitySystemComponent))]
public class NetworkAbilitySystemComponent : NetworkBehaviour
{
    // =========================================================
    // CAMPOS Y SYNCVARS
    // =========================================================

    // Referencia al ASC local (mismo GameObject).
    private AbilitySystemComponent _asc;

    // Instancia de GA_LeapAttack cuyo impacto falta resolver — la guarda el
    // servidor entre ServerStartLeap() y ServerResolveLeapImpact() (que
    // llama el dueño al aterrizar). Ver esos métodos en la sección SALTO.
    private GA_LeapAttack _pendingLeapImpact;

    // Atributos numéricos sincronizados. El servidor escribe, todos los
    // clientes leen (ver HandleAttributeChanged / OnNet*Changed).
    private readonly SyncVar<float> _netHealth    = new SyncVar<float>();
    private readonly SyncVar<float> _netMaxHealth = new SyncVar<float>();
    private readonly SyncVar<float> _netMana      = new SyncVar<float>();
    private readonly SyncVar<float> _netMaxMana   = new SyncVar<float>();
    private readonly SyncVar<float> _netEnergy    = new SyncVar<float>();
    private readonly SyncVar<float> _netShield    = new SyncVar<float>();
    private readonly SyncVar<int>   _netTeamID    = new SyncVar<int>();
    private readonly SyncVar<float> _netLevel     = new SyncVar<float>();
    private readonly SyncVar<float> _netExp       = new SyncVar<float>();

    // Tags de estado activos, sincronizados para UI remota y efectos visuales.
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

    // Slots que se revisan en cada barrido de cooldowns.
    private static readonly EAbilityInput[] _cooldownSlots =
    {
        EAbilityInput.PrimaryAttack, EAbilityInput.SecondaryAttack,
        EAbilityInput.Action1, EAbilityInput.Action2, EAbilityInput.Action3,
        EAbilityInput.Movement
    };

    // Cada cuánto se re-sincronizan los cooldowns (ver Update).
    private const float CooldownSyncInterval = 0.25f;
    private float _cooldownSyncTimer;

    // =========================================================
    // EFECTOS ACTIVOS EN RED (buffs/debuffs con duración, para la UI)
    //
    // Mismo problema que con los cooldowns: ActiveEffects del ASC solo
    // existe de verdad en la copia del servidor. Acá no podemos mandar la
    // referencia al ScriptableObject GameplayEffect por SyncVar (FishNet no
    // sabe serializar assets arbitrarios), así que sincronizamos el ÍNDICE
    // del efecto dentro de GameplayEffectRegistry — el cliente lo resuelve
    // de vuelta al mismo asset, que ya tiene cargado localmente porque es
    // el mismo build. Junto al índice mandamos el TICK en que empezó y
    // cuánto dura, igual que con los cooldowns, para no necesitar un
    // paquete de red por frame.
    //
    // A diferencia de los cooldowns (que se barren cada CooldownSyncInterval
    // segundos), acá actualizamos on-demand desde los hooks del ASC
    // (OnActiveEffectAddedCallback/Removed) porque los buffs/debuffs pueden
    // ser mucho más cortos y un barrido periódico se los podría perder.
    // =========================================================

    public readonly SyncDictionary<int, uint>  NetActiveEffectStartTick = new SyncDictionary<int, uint>();
    public readonly SyncDictionary<int, float> NetActiveEffectDuration  = new SyncDictionary<int, float>();

    // =========================================================
    // STATS DERIVADOS EN RED (AtkSpeed, MovSpeed, Atq, Def, etc.)
    //
    // Los atributos "pool" (Health, Mana...) y los Max tienen su SyncVar
    // dedicado arriba. Pero los demás stats (los que RecalculateAllAttributes
    // del ASC computa como Base*Modificadores) los cambian los buffs vía el
    // sistema de modificadores, y no tenían dónde sincronizarse. Este
    // diccionario genérico replica CUALQUIER stat que no tenga SyncVar
    // propio, así un buff nuevo sobre cualquier atributo llega a los clientes
    // sin tener que agregar un SyncVar por cada uno. Sin esto, el jugador 2
    // nunca recibía el buff de velocidad de ataque/movimiento de su Rage.
    // =========================================================

    public readonly SyncDictionary<EAttributeType, float> NetStats = new SyncDictionary<EAttributeType, float>();

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
    // CICLO DE VIDA
    // =========================================================

    // Obtiene la referencia al ASC local y se suscribe a todos sus hooks
    // (atributos, tags, muerte/revivir, efectos activos).
    private void Awake()
    {
        _asc = GetComponent<AbilitySystemComponent>();

        if (_asc == null)
        {
            Debug.LogError("[NetworkASC] No se encontró AbilitySystemComponent en el mismo GameObject.");
            return;
        }

        _asc.OnAttributeChangedCallback += HandleAttributeChanged;
        _asc.OnTagAddedCallback         += HandleTagAdded;
        _asc.OnTagRemovedCallback       += HandleTagRemoved;

        _asc.OnDeath  += HandleDeath;
        _asc.OnRevive += HandleRevive;

        _asc.OnActiveEffectAddedCallback   += HandleActiveEffectAdded;
        _asc.OnActiveEffectRemovedCallback += HandleActiveEffectRemoved;
    }

    // Desuscribe todos los hooks/callbacks para no dejar referencias
    // colgando cuando este componente se destruye.
    private void OnDestroy()
    {
        if (_asc == null) return;

        _asc.OnAttributeChangedCallback -= HandleAttributeChanged;
        _asc.OnTagAddedCallback         -= HandleTagAdded;
        _asc.OnTagRemovedCallback       -= HandleTagRemoved;
        _asc.OnDeath                    -= HandleDeath;
        _asc.OnRevive                   -= HandleRevive;
        _asc.OnActiveEffectAddedCallback   -= HandleActiveEffectAdded;
        _asc.OnActiveEffectRemovedCallback -= HandleActiveEffectRemoved;

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
        NetStats.OnChange      -= OnNetStatChanged;
    }

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

    // Suscribe los callbacks de SyncVar para recibir los cambios que
    // manda el servidor.
    public override void OnStartClient()
    {
        base.OnStartClient();

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
        NetStats.OnChange      += OnNetStatChanged;

        // OnChange solo dispara para cambios FUTUROS — aplicamos de una los
        // stats que ya venían sincronizados al momento de conectarnos (ej: un
        // jugador que ya tenía un buff activo cuando entramos a la partida).
        if (!IsServerInitialized && _asc != null)
            foreach (var kvp in NetStats)
                _asc.SetCurrentAttributeValue(kvp.Key, kvp.Value);
    }

    // Solo en el servidor: re-sincroniza los cooldowns cada
    // CooldownSyncInterval segundos (ver sección COOLDOWNS).
    private void Update()
    {
        if (!IsServerInitialized || _asc == null) return;

        _cooldownSyncTimer += Time.deltaTime;
        if (_cooldownSyncTimer < CooldownSyncInterval) return;
        _cooldownSyncTimer = 0f;

        foreach (EAbilityInput slot in _cooldownSlots)
            SyncCooldownForSlot(slot);
    }

    // =========================================================
    // ATRIBUTOS EN RED
    // =========================================================

    // El servidor refleja cada cambio de atributo del ASC en el SyncVar
    // correspondiente.
    private void HandleAttributeChanged(EAttributeType type, float value)
    {
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

            // Cualquier otro stat (AtkSpeed, MovSpeed, Atq, Def, CritChance...)
            // va por el diccionario genérico — así los buffs sobre stats
            // derivados también llegan a los clientes sin un SyncVar por cada uno.
            default:                       NetStats[type]      = value; break;
        }
    }

    // En los clientes, cada callback de SyncVar aplica el nuevo valor al
    // ASC local (asServer=true significa que lo escribimos nosotros como
    // servidor, así que lo ignoramos para no reescribirnos a nosotros mismos).
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

    // Vuelca todos los atributos actuales del ASC a sus SyncVars de una
    // sola vez (ej: para forzar una resincronización completa).
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
    // TAGS EN RED
    // =========================================================

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

    // En los clientes, aplica al ASC local cada stat derivado que cambió en
    // el servidor (ver NetStats). Así la UI (MaxHealth), el movimiento
    // (MovSpeed) y las animaciones (AtkSpeed) del jugador reflejan sus buffs.
    private void OnNetStatChanged(SyncDictionaryOperation op, EAttributeType key, float value, bool asServer)
    {
        if (asServer || _asc == null) return;

        if (op == SyncDictionaryOperation.Add || op == SyncDictionaryOperation.Set)
            _asc.SetCurrentAttributeValue(key, value);
    }

    // =========================================================
    // COOLDOWNS EN RED
    // =========================================================

    // Publica el tick de inicio y la duración total del cooldown vigente
    // de una habilidad (o limpia la entrada si ya no está en cooldown).
    [Server]
    private void SyncCooldownForSlot(EAbilityInput slot)
    {
        GameplayAbility ability = FindAbilityBySlot(slot);
        if (ability == null) return;

        if (_asc.GetCooldownStatus(ability, out float remaining, out float total))
        {
            // BUG QUE ARREGLAMOS: acá antes se mandaba "remaining" como si
            // fuera la duración TOTAL y "ahora" como tick de inicio. Como
            // esto se re-sincroniza cada CooldownSyncInterval, el "total"
            // (denominador de la barra en la UI) se achicaba a lo que
            // quedaba en cada barrido, y el tick de inicio se reiniciaba a
            // "ahora" — la barra saltaba a casi llena y se vaciaba de nuevo
            // cada 0.25s en vez de vaciarse una sola vez a lo largo de todo
            // el cooldown real (se veía como si "recargara" varias veces).
            //
            // Mandamos la duración TOTAL real (constante durante todo el
            // cooldown) y reconstruimos el tick de inicio a partir de cuánto
            // ya transcurrió (total - remaining), no "ahora". Así, si algo
            // reduce el cooldown a mitad de camino (ChargeUltimate /
            // ReduceCooldownByTag, que solo tocan el remaining, no el total),
            // el próximo barrido corrige el tiempo restante sin resetear la
            // barra visualmente.
            uint elapsedTicks = TimeManager.TimeToTicks(Mathf.Max(0f, total - remaining));
            NetCooldownStartTick[slot] = TimeManager.Tick > elapsedTicks ? TimeManager.Tick - elapsedTicks : 0;
            NetCooldownDuration[slot]  = total;
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

    // =========================================================
    // EFECTOS ACTIVOS EN RED (buffs/debuffs)
    // =========================================================

    private void HandleActiveEffectAdded(GameplayEffect effect, float totalDuration)
    {
        if (!IsServerInitialized) return;
        // Los efectos "Hidden" son mecánicas internas (ej: el propio
        // CooldownEffect, que ya se sincroniza aparte) — no deben mostrar
        // icono en la barra de buffs/debuffs.
        if (effect.EffectType == GameplayEffect.EEffectType.Hidden) return;

        int index = ResolveEffectIndex(effect);
        if (index < 0) return;

        NetActiveEffectStartTick[index] = TimeManager.Tick;
        NetActiveEffectDuration[index]  = totalDuration;
    }

    private void HandleActiveEffectRemoved(GameplayEffect effect)
    {
        if (!IsServerInitialized) return;

        int index = ResolveEffectIndex(effect);
        if (index < 0) return;

        NetActiveEffectStartTick.Remove(index);
        NetActiveEffectDuration.Remove(index);
    }

    // Busca el índice de un GameplayEffect en GameplayEffectRegistry (ver
    // esa clase). Logea un warning si el registro no existe o el efecto
    // no está registrado — en ambos casos, el efecto sigue funcionando en
    // gameplay, solo no sincroniza su icono.
    private int ResolveEffectIndex(GameplayEffect effect)
    {
        GameplayEffectRegistry registry = GameplayEffectRegistry.Instance;
        if (registry == null)
        {
            Debug.LogWarning("[NetworkASC] No se encontró GameplayEffectRegistry en Resources — la barra de buffs/debuffs no se va a sincronizar a los clientes remotos.");
            return -1;
        }

        int index = registry.GetIndex(effect);
        if (index < 0)
            Debug.LogWarning($"[NetworkASC] '{effect.name}' no está en GameplayEffectRegistry — su icono no se sincronizará a los clientes remotos.");

        return index;
    }

    // Usado por UI_EffectContainer para saber qué efectos (índices de
    // GameplayEffectRegistry) están activos ahora mismo en este ASC.
    public IEnumerable<int> GetActiveEffectIndices() => NetActiveEffectDuration.Keys;

    // Igual que TryGetNetCooldown pero para un efecto activo (buff/debuff)
    // identificado por su índice en GameplayEffectRegistry.
    public bool TryGetNetActiveEffect(int effectIndex, out float remaining, out float total)
    {
        remaining = 0f;

        if (!NetActiveEffectDuration.TryGetValue(effectIndex, out total)) { total = 0f; return false; }
        if (!NetActiveEffectStartTick.TryGetValue(effectIndex, out uint startTick)) return false;

        uint   elapsedTicks   = TimeManager.Tick > startTick ? (TimeManager.Tick - startTick) : 0;
        double elapsedSeconds = TimeManager.TicksToTime(elapsedTicks);

        remaining = Mathf.Max(0f, total - (float)elapsedSeconds);
        return remaining > 0f;
    }

    // =========================================================
    // EQUIPOS
    // =========================================================

    // Asigna el equipo de este personaje (lo llama NetworkGameManager al
    // spawnearlo) y lo sincroniza a los clientes.
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

    // Valida y ejecuta la habilidad de un slot, y replica su animación a los
    // observadores. El dueño ya la disparó por predicción (o, en el host, vía
    // Activate()); ObserversPlayAbilityAnimation la reproduce en los demás.
    [ServerRpc]
    public void ServerRequestActivateAbility(EAbilityInput inputSlot, Vector3 aimPoint)
    {
        if (_asc == null) return;

        GameplayAbility ability = FindAbilityBySlot(inputSlot);

        if (ability == null)
        {
            // Incluimos la clase server-side: si no coincide con la que el
            // dueño cree tener, es un desync de clase/subclase (el servidor se
            // quedó en la clase vieja porque ServerSetClass no sincronizó — ej.
            // GetClassIndex devolvió -1). Ese dato hace obvio el diagnóstico.
            string serverClass = _asc.CurrentClass != null ? _asc.CurrentClass.ClassName : "null";
            Debug.LogWarning($"[Server] No se encontró habilidad en slot {inputSlot} " +
                             $"(clase server-side: {serverClass}). Posible desync de clase con el dueño.");
            // El dueño ya puso isAttacking=true por predicción; si no le
            // avisamos, queda trabado hasta que salte el watchdog y no puede
            // usar NINGUNA habilidad mientras tanto. Lo destrabamos ya.
            NotifyOwnerAbilityRejected();
            return;
        }

        if (!ability.CanActivate())
        {
            Debug.Log($"[Server] Habilidad {ability.AbilityName} bloqueada.");
            NotifyOwnerAbilityRejected();
            return;
        }

        // El dueño calculó este punto con SU cámara y nos lo mandó; lo dejamos
        // disponible en el PlayerController para que RotateToAim()/GetAimPoint()
        // lo usen en vez de intentar leer Camera.main en el servidor.
        PlayerController pc = GetComponent<PlayerController>();
        if (pc != null) pc.NetworkAimPoint = aimPoint;

        ability.Activate();
        ObserversPlayAbilityAnimation(ability.AnimationTriggerName, ability.AnimationID);
    }

    // El NetworkAnimator del prefab NO sincroniza los SetTrigger de forma
    // confiable (los triggers se consumen antes de que pueda muestrearlos —
    // solo replica floats/bools como Speed/IsJumping, o sea la locomoción).
    // Por eso la animación de ataque se propaga a los observadores con este
    // ObserversRpc explícito. Al dueño se la salteamos: él ya la disparó por
    // predicción (cliente remoto) o vía Activate() (host). Setea el Animator
    // directo, sin pasar por PlayerController.PlayAnimation, porque ese método
    // tiene un guard de IsOwner que justamente bloquea las copias ajenas.
    [ObserversRpc]
    private void ObserversPlayAbilityAnimation(string trigger, int actionID)
    {
        if (IsOwner) return;

        Animator anim = GetComponentInChildren<Animator>();
        if (anim == null || string.IsNullOrEmpty(trigger)) return;

        anim.SetInteger("ActionID", actionID);
        anim.SetTrigger(trigger);
    }

    // Permite que otras rutas server-side (ej. habilidades de menú radial en
    // PlayerController.ServerRequestRadialAbility) también repliquen su
    // animación a los observadores.
    [Server]
    public void ServerBroadcastAbilityAnimation(string trigger, int actionID)
    {
        ObserversPlayAbilityAnimation(trigger, actionID);
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

    // El servidor rechazó una activación (no encontró la habilidad, o estaba
    // en cooldown). El dueño ya había puesto isAttacking=true por predicción,
    // así que hay que destrabarlo YA — si no, queda bloqueado sin poder usar
    // ninguna habilidad hasta que salte el watchdog de PlayerController.
    [Server]
    private void NotifyOwnerAbilityRejected()
    {
        // Host: el dueño es este mismo proceso; reseteamos directo (el
        // TargetRpc de abajo se saltea solo con el guard de IsServerInitialized).
        if (IsOwner)
        {
            PlayerController pc = GetComponent<PlayerController>();
            if (pc != null) pc.FinishAttack();
        }
        // Dueño remoto: se lo avisamos solo a su conexión.
        TargetFinishAttack(Owner);
    }

    [TargetRpc]
    private void TargetFinishAttack(NetworkConnection conn)
    {
        // En el host, el dueño ya se reseteó en NotifyOwnerAbilityRejected.
        if (IsServerInitialized) return;

        PlayerController pc = GetComponent<PlayerController>();
        if (pc != null) pc.FinishAttack();
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

    // =========================================================
    // SALTO (GA_LeapAttack) — TODA la orquestación de esta habilidad vive
    // acá, no en PlayerController. Ese script solo expone primitivas
    // genéricas de movimiento (ApplyAbilityVelocity/ClearAbilityVelocity/
    // IsGrounded) — no sabe qué es "GA_LeapAttack" ni cuándo termina.
    //
    // PlayerController tampoco puede saberlo: su Update() está bloqueado
    // con IsOwner, así que la física del salto (y por lo tanto detectar
    // cuándo aterriza) solo puede correr en el proceso DUEÑO. Y
    // GA_LeapAttack.Activate() tampoco puede correr ahí — como toda
    // GameplayAbility en este proyecto, Activate() es server-only. Este
    // componente es el único lugar que existe en ambos lados (llega el
    // TargetRpc del servidor Y corre en el proceso dueño), así que acá es
    // donde tiene que vivir el puente entre las dos partes.
    //
    // Flujo: GA_LeapAttack.Activate() (servidor) -> ServerStartLeap() ->
    // TargetExecuteLeap() (solo a la conexión dueña, calcula la dirección
    // con SU cámara) -> WaitForLandingThenResolve() (corutina local del
    // dueño, espera a que IsGrounded vuelva a true) -> ServerResolveLeapImpact()
    // -> el servidor resuelve el daño con la MISMA instancia de
    // GA_LeapAttack que se guardó en ServerStartLeap().
    // =========================================================

    // Guarda la habilidad pendiente y manda el TargetRpc a la conexión dueña.
    [Server]
    public void ServerStartLeap(GA_LeapAttack ability, float upVelocity, float forwardForce)
    {
        _pendingLeapImpact = ability;
        TargetExecuteLeap(Owner, upVelocity, forwardForce);
    }

    // Solo corre en el cliente dueño: calcula la dirección del salto con
    // SU cámara y le pide a PlayerController que aplique el impulso.
    [TargetRpc]
    private void TargetExecuteLeap(NetworkConnection conn, float upVelocity, float forwardForce)
    {
        PlayerController pc = GetComponent<PlayerController>();
        if (pc == null) { Debug.LogWarning("[NetASC] No hay PlayerController — no se puede ejecutar el salto."); return; }
        if (Camera.main == null) { Debug.LogWarning("[NetASC] Camera.main es null — no se puede calcular la dirección del salto."); return; }

        Vector3 camFwd = Camera.main.transform.forward;
        camFwd.y = 0;
        camFwd.Normalize();

        if (pc.ApplyAbilityVelocity(camFwd * forwardForce, upVelocity))
            StartCoroutine(WaitForLandingThenResolve(pc));
    }

    // Espera a que el dueño despegue del piso y después a que vuelva a
    // aterrizar, y recién ahí le pide al servidor que resuelva el impacto.
    private IEnumerator WaitForLandingThenResolve(PlayerController pc)
    {
        // BUG QUE ARREGLAMOS: acá había un solo "frame de gracia" antes de
        // chequear IsGrounded una vez. El orden entre el Update() de
        // PlayerController (que recién ahí aplica el impulso hacia arriba
        // vía Move()) y la reanudación de esta corutina no está garantizado
        // — si esa primera lectura llegaba a pasar ANTES de que el
        // CharacterController realmente despegara, el while de abajo nunca
        // entraba y resolvíamos el impacto instantáneamente en el punto de
        // DESPEGUE en vez del de ATERRIZAJE, así que casi nunca había un
        // enemigo cerca.
        //
        // Solución: esperar explícitamente la transición "despegó" antes de
        // esperar la transición "aterrizó" — así no importa el orden de
        // ejecución entre scripts, solo importa el estado real de
        // IsGrounded. El timeout es un salvavidas por si algo (ej. un
        // obstáculo encima) impide despegar del todo.
        float elapsed = 0f;
        const float liftoffTimeout = 1f;
        while (pc.IsGrounded && elapsed < liftoffTimeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (elapsed >= liftoffTimeout)
            Debug.LogWarning("[NetASC] Salto: nunca detecté el despegue (timeout) — probablemente algo bloqueó el impulso hacia arriba.");

        while (!pc.IsGrounded)
            yield return null;

        pc.ClearAbilityVelocity();
        pc.FinishAttack();
        ServerResolveLeapImpact();
    }

    // Resuelve el daño en área del aterrizaje, con la instancia de
    // GA_LeapAttack guardada en ServerStartLeap.
    [ServerRpc]
    private void ServerResolveLeapImpact()
    {
        _pendingLeapImpact?.ExecuteImpactCheck();
        _pendingLeapImpact = null;
    }

    // =========================================================
    // VFX DE HABILIDAD — replicar impactos/efectos a todos los clientes
    //
    // GameplayAbility.PlayImpactVFX() solo hace un Instantiate() local —
    // si algo lo llama desde el servidor (donde corre toda la lógica de
    // daño), ese VFX solo existe en el proceso servidor y ningún cliente
    // remoto lo ve (mismo problema que tuvimos con el arma del proyectil).
    //
    // No podemos mandar la referencia al GameObject del VFX por RPC
    // (FishNet no serializa assets arbitrarios), así que en vez de eso
    // identificamos la habilidad por su SLOT (igual que FindAbilityBySlot
    // para cooldowns) y cada peer reproduce el VFX con SU PROPIA copia
    // local de esa misma habilidad.
    // =========================================================

    // Reproduce el VFX de impacto puntual de una habilidad en el servidor
    // y le avisa a los demás peers que hagan lo mismo con su propia copia.
    [Server]
    public void ServerPlayAbilityVFX(GameplayAbility ability, Vector3 position)
    {
        if (ability == null) return;

        // El servidor reproduce el suyo ya mismo (importa para el host).
        ability.PlayImpactVFX(position);

        // Identificamos la habilidad por su índice en GameplayAbilityRegistry
        // (no por slot). Así los observadores la resuelven aunque no tengan la
        // clase equipada, y funciona para pasos de combo que no viven en ningún
        // slot — que era justo lo que fallaba con el esquema por slot.
        int abilityIndex = GameplayAbilityRegistry.Instance != null
            ? GameplayAbilityRegistry.Instance.GetIndex(ability) : -1;

        if (abilityIndex >= 0)
            ObserversPlayAbilityVFX(abilityIndex, position);
        else
            Debug.LogWarning($"[NetworkASC] '{ability.AbilityName}' no está en GameplayAbilityRegistry — su VFX de impacto no se replicará a los clientes remotos.");
    }

    [ObserversRpc]
    private void ObserversPlayAbilityVFX(int abilityIndex, Vector3 position)
    {
        // El servidor ya lo reprodujo en ServerPlayAbilityVFX() de arriba.
        if (IsServerInitialized || _asc == null) return;

        GameplayAbility ability = GameplayAbilityRegistry.Instance?.GetAbility(abilityIndex);
        // PlayImpactVFXFor le presta este ASC como dueño puntual (el template
        // del registro no tiene uno propio; algunos VFX lo necesitan).
        if (ability != null) ability.PlayImpactVFXFor(_asc, position);
    }

    // Igual que ServerPlayAbilityVFX, pero para la secuencia automática de
    // GameplayAbility.VisualsSequence (CommitAbility) en vez de un único
    // impacto puntual. Como PlayVisualsSequence() ya calcula todo (delays,
    // offsets, destrucción) a partir de OwnerASC.transform/tags — que están
    // igual en todos los peers — alcanza con arrancar la MISMA corutina en
    // cada uno; no hace falta sincronizar nada más.
    [Server]
    public void ServerPlayAbilityVisualsSequence(GameplayAbility ability)
    {
        if (ability == null || _asc == null) return;

        _asc.StartAbilityCoroutine(ability.PlayVisualsSequence());

        int abilityIndex = GameplayAbilityRegistry.Instance != null
            ? GameplayAbilityRegistry.Instance.GetIndex(ability) : -1;

        if (abilityIndex >= 0)
            ObserversPlayAbilityVisualsSequence(abilityIndex);
        else
            Debug.LogWarning($"[NetworkASC] '{ability.AbilityName}' no está en GameplayAbilityRegistry — su secuencia de visuales no se replicará a los clientes remotos.");
    }

    [ObserversRpc]
    private void ObserversPlayAbilityVisualsSequence(int abilityIndex)
    {
        // El servidor ya la arrancó en ServerPlayAbilityVisualsSequence() de arriba.
        if (IsServerInitialized || _asc == null) return;

        GameplayAbility ability = GameplayAbilityRegistry.Instance?.GetAbility(abilityIndex);
        // Le pasamos este ASC como dueño puntual: la corutina usa su transform
        // y tags (el template del registro no tiene OwnerASC propio).
        if (ability != null) _asc.StartAbilityCoroutine(ability.PlayVisualsSequence(_asc));
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

    // Revive al personaje (lo llama NetworkGameManager al respawnearlo).
    [Server]
    public void Revive()
    {
        if (_asc != null) _asc.Revive();
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
    // CHEAT (debug) — subir al nivel máximo para probar subclases
    //
    // El dueño lo pide con una tecla/botón (ver PlayerController). El subir
    // de nivel corre en el servidor (autoridad de atributos); después
    // avisamos por TargetRpc al dueño para que la selección de subclase
    // aparezca en SU pantalla (la UI escucha OnMaxLevelReached del ASC local,
    // que de otro modo nunca se enteraría porque el level-up pasó en el server).
    // =========================================================

    [ServerRpc]
    public void ServerCheatMaxLevel()
    {
        if (_asc == null) return;

        // Si ya está al máximo, no hacemos nada — así apretar Alt de nuevo por
        // accidente no vuelve a mostrar el anuncio/menú de nivel máximo.
        if (_asc.GetAttributeValue(EAttributeType.Level) >= _asc.MaxLevel) return;

        _asc.GainExperience(999999f); // sube al máximo (aplica el crecimiento de stats)
        TargetCheatShowSubclass(Owner);
    }

    [TargetRpc]
    private void TargetCheatShowSubclass(NetworkConnection conn)
    {
        // Corre en el proceso DUEÑO (host o cliente remoto). Dispara el evento
        // en su ASC local para que aparezca la selección de subclase. Si es el
        // host, GainExperience de arriba pudo haberlo disparado también, pero
        // los suscriptores (UI) son idempotentes, así que no molesta.
        if (_asc != null) _asc.TriggerMaxLevelReached();
    }

    // =========================================================
    // UTILIDADES INTERNAS
    // =========================================================

    // Resuelve, a partir de un slot, la instancia de habilidad otorgada a
    // este personaje (buscando en CurrentClass.Abilities qué template
    // corresponde a ese slot, y matcheándolo por nombre contra
    // GrantedAbilities).
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
