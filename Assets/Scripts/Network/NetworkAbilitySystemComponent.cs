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

    [Header("Recompensas")]
    [Tooltip("EXP que gana el jugador que consigue una baja. Provisional para pruebas; " +
             "es lo que reemplaza al cheat de subir de nivel a mano una vez que haya varios jugadores.")]
    public float ExperiencePerKill = 50f;

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

    // Feedback del Crítico mejorado del Asesino: el estado "listo" (pasó la
    // reutilización) vive en el servidor, así que lo sincronizamos para que el HUD
    // del dueño (host o cliente remoto) lo muestre. SyncVar solo manda al cambiar.
    private readonly SyncVar<bool> _netFirstStrikeReady = new SyncVar<bool>();
    public bool NetFirstStrikeReady => _netFirstStrikeReady.Value;

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

    // Cargas disponibles por slot, para las habilidades que usan sistema de
    // cargas (ej: el dash). El estado real vive en la habilidad (server-side);
    // esta la reporta con ServerReportCharges cada vez que cambia, y la UI la
    // lee con TryGetNetCharges. Un slot sin entrada = habilidad sin cargas.
    public readonly SyncDictionary<EAbilityInput, int> NetCharges = new SyncDictionary<EAbilityInput, int>();

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

    // Cuántas instancias (stacks) de ese efecto están activas. Un efecto con
    // StackingPolicy = Stack puede tener varias a la vez (ej: las Heridas del
    // Pícaro), y todas comparten esta única entrada porque el diccionario está
    // indexado por EFECTO, no por instancia — ver SyncActiveEffect.
    public readonly SyncDictionary<int, int> NetActiveEffectStacks = new SyncDictionary<int, int>();

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
        _asc.OnMaxLevelReached += HandleMaxLevelReached;

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
        _asc.OnMaxLevelReached          -= HandleMaxLevelReached;
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
        NetActiveEffectDuration.OnChange -= OnNetActiveEffectVfxChanged;

        DespawnAllEffectVfx();
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
        NetActiveEffectDuration.OnChange += OnNetActiveEffectVfxChanged;

        // OnChange solo dispara para cambios FUTUROS — aplicamos de una los
        // stats que ya venían sincronizados al momento de conectarnos (ej: un
        // jugador que ya tenía un buff activo cuando entramos a la partida).
        if (!IsServerInitialized && _asc != null)
            foreach (var kvp in NetStats)
                _asc.SetCurrentAttributeValue(kvp.Key, kvp.Value);

        // Lo mismo para los VFX: si entramos a la partida con un debuff ya puesto
        // encima de este personaje, su VFX tiene que aparecer igual.
        foreach (int index in NetActiveEffectDuration.Keys) SpawnEffectVfx(index);
    }

    // Solo en el servidor: re-sincroniza los cooldowns cada
    // CooldownSyncInterval segundos (ver sección COOLDOWNS).
    private void Update()
    {
        if (!IsServerInitialized || _asc == null) return;

        // Feedback del Crítico mejorado: barato de chequear cada frame, y el SyncVar
        // solo manda cuando el bool cambia (no hay tráfico si no se mueve).
        _netFirstStrikeReady.Value = _asc.IsFirstStrikeReady;

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
    //
    // IMPORTANTE: solo los clientes PUROS deben espejar NetTags. En el host,
    // el servidor ya maneja GameplayTags directo (AddTag/RemoveTag desde el
    // sistema de efectos), así que re-aplicarlos acá es redundante Y peligroso:
    // el SyncHashSet entrega su OnChange de forma asíncrona, y para un tag de
    // vida corta (ej. el cooldown melee = ~0.16s) el 'Add' puede llegar DESPUÉS
    // de que el servidor ya removió el tag → lo vuelve a agregar sin ningún
    // efecto detrás → queda huérfano/pegado para siempre. Por eso salteamos
    // también cuando este peer ES servidor (IsServerInitialized).
    private void OnNetTagsChanged(SyncHashSetOperation op, EGameplayTag item, bool asServer)
    {
        if (asServer || IsServerInitialized || _asc == null) return;

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
    // CARGAS EN RED (habilidades con sistema de cargas, ej: el dash)
    // =========================================================

    // El servidor publica cuántas cargas le quedan a una habilidad. Resuelve el
    // slot de la habilidad (por nombre, igual que FindAbilityBySlot) y lo guarda
    // en NetCharges para que la UI del dueño lo muestre. Lo llama GA_Dash cada
    // vez que sus cargas cambian (usar, recargar, reembolsar).
    [Server]
    public void ServerReportCharges(GameplayAbility ability, int charges)
    {
        if (_asc == null || _asc.CurrentClass == null || ability == null) return;

        EAbilityInput slot = EAbilityInput.None;
        foreach (var assignment in _asc.CurrentClass.Abilities)
        {
            if (assignment.Ability != null && assignment.Ability.AbilityName == ability.AbilityName)
            {
                slot = assignment.InputSlot;
                break;
            }
        }
        if (slot == EAbilityInput.None) return;

        NetCharges[slot] = Mathf.Max(0, charges);
    }

    // Lee las cargas sincronizadas de un slot. Devuelve false si ese slot no es
    // una habilidad con cargas (o todavía no reportó ninguna).
    public bool TryGetNetCharges(EAbilityInput slot, out int charges)
    {
        return NetCharges.TryGetValue(slot, out charges);
    }

    // =========================================================
    // EFECTOS ACTIVOS EN RED (buffs/debuffs)
    // =========================================================

    private void HandleActiveEffectAdded(GameplayEffect effect, float totalDuration) => SyncActiveEffect(effect);
    private void HandleActiveEffectRemoved(GameplayEffect effect)                    => SyncActiveEffect(effect);

    // Recalcula y publica el estado de UN efecto agregando TODAS sus instancias
    // (stacks): sincroniza el stack que expira ÚLTIMO (para la barra de progreso)
    // y cuántos hay en total.
    //
    // BUG QUE ARREGLAMOS: antes esto se escribía directo desde el add/remove. Como
    // el diccionario está indexado por EFECTO y no por instancia, los stacks se
    // pisaban entre sí: cada nueva aplicación reiniciaba el reloj de la UI, y al
    // expirar el PRIMER stack se borraba la entrada entera → el icono desaparecía
    // aunque quedaran stacks vivos (se veía como si el tiempo fuera solo el de la
    // primera aplicación). Recontar la lista real en cada cambio lo resuelve.
    private void SyncActiveEffect(GameplayEffect effect)
    {
        if (!IsServerInitialized || _asc == null || effect == null) return;

        // Los efectos "Hidden" son mecánicas internas (ej: el propio CooldownEffect,
        // que ya se sincroniza aparte) — no muestran icono en la barra.
        if (effect.EffectType == GameplayEffect.EEffectType.Hidden) return;

        int index = ResolveEffectIndex(effect);
        if (index < 0) return;

        int   stacks           = 0;
        float longestRemaining = 0f;
        float longestTotal     = 0f;

        foreach (var active in _asc.GetActiveEffects())
        {
            if (active.Definition != effect) continue;
            stacks++;
            if (active.DurationRemaining > longestRemaining)
            {
                longestRemaining = active.DurationRemaining;
                longestTotal     = active.TotalDuration;
            }
        }

        if (stacks == 0)
        {
            NetActiveEffectStartTick.Remove(index);
            NetActiveEffectDuration.Remove(index);
            NetActiveEffectStacks.Remove(index);
            return;
        }

        // Reconstruimos el tick de inicio desde lo ya transcurrido (igual que con
        // los cooldowns), para no reiniciar la barra en cada resincronización.
        uint elapsedTicks = TimeManager.TimeToTicks(Mathf.Max(0f, longestTotal - longestRemaining));
        NetActiveEffectStartTick[index] = TimeManager.Tick > elapsedTicks ? TimeManager.Tick - elapsedTicks : 0;
        NetActiveEffectDuration[index]  = longestTotal;
        NetActiveEffectStacks[index]    = stacks;
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

    // Cuántos stacks de un efecto están activos ahora mismo. Devuelve false si ese
    // efecto no está activo (la UI lo trata como 1). Ver SyncActiveEffect.
    public bool TryGetNetActiveEffectStacks(int effectIndex, out int stacks)
    {
        return NetActiveEffectStacks.TryGetValue(effectIndex, out stacks);
    }

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
    // VFX DE EFECTOS ACTIVOS (GameplayEffect.TargetVFX)
    //
    // Un debuff importante tiene que VERSE encima de quien lo sufre, y eso la
    // VisualsSequence de la habilidad no lo puede resolver: esa secuencia solo conoce
    // al que LANZA, y arranca en CommitAbility() —antes de que la habilidad sepa
    // siquiera a quién le pegó— (ver GameplayAbility.PlayVisualsSequence).
    //
    // Se cuelga de NetActiveEffectDuration, que YA replica a todos los peers qué
    // efectos están activos en este personaje, indexados por GameplayEffectRegistry.
    // O sea que el VFX aparece igual en el dueño, en los observadores y en el host sin
    // un solo RPC nuevo: ese diccionario ya viajaba para la barra de buffs.
    //
    // Corre solo del lado CLIENTE (asServer = false): en un servidor dedicado no hay
    // nada que dibujar, y en host es la copia cliente del callback la que instancia.
    // =========================================================

    // VFX vivos ahora mismo, uno por efecto (índice del registro → instancia).
    private readonly Dictionary<int, GameObject> _activeEffectVfx = new Dictionary<int, GameObject>();

    private void OnNetActiveEffectVfxChanged(SyncDictionaryOperation op, int effectIndex,
                                             float duration, bool asServer)
    {
        if (asServer) return;

        switch (op)
        {
            case SyncDictionaryOperation.Add:
            case SyncDictionaryOperation.Set:
                SpawnEffectVfx(effectIndex);
                break;
            case SyncDictionaryOperation.Remove:
                DespawnEffectVfx(effectIndex);
                break;
            case SyncDictionaryOperation.Clear:
                DespawnAllEffectVfx();
                break;
        }
    }

    // Instancia el TargetVFX del efecto como HIJO de este personaje, así lo sigue a
    // donde vaya. Idempotente a propósito: un efecto que se refresca o suma un stack
    // vuelve a disparar Set, y no queremos dos partículas encimadas.
    private void SpawnEffectVfx(int effectIndex)
    {
        if (_activeEffectVfx.ContainsKey(effectIndex)) return;

        GameplayEffectRegistry registry = GameplayEffectRegistry.Instance;
        GameplayEffect definition = registry != null ? registry.GetEffect(effectIndex) : null;
        if (definition == null || definition.TargetVFX == null) return;

        Vector3    pos = transform.position + transform.TransformDirection(definition.TargetVFXOffset);
        Quaternion rot = transform.rotation * Quaternion.Euler(definition.TargetVFXRotation);

        GameObject vfx = Instantiate(definition.TargetVFX, pos, rot, transform);
        if (definition.TargetVFXScale != Vector3.zero)
            vfx.transform.localScale = definition.TargetVFXScale;

        _activeEffectVfx[effectIndex] = vfx;
    }

    private void DespawnEffectVfx(int effectIndex)
    {
        if (!_activeEffectVfx.TryGetValue(effectIndex, out GameObject vfx)) return;
        if (vfx != null) Destroy(vfx);
        _activeEffectVfx.Remove(effectIndex);
    }

    private void DespawnAllEffectVfx()
    {
        foreach (var kvp in _activeEffectVfx)
            if (kvp.Value != null) Destroy(kvp.Value);

        _activeEffectVfx.Clear();
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

    // Nombre elegido por el jugador en el menú de entrada. Se sincroniza a todos
    // para mostrarlo sobre su barra de vida (ver UI_WorldHealthbar). Vive acá y no
    // en el ASC porque es puramente identidad de red, no un stat de juego.
    private readonly SyncVar<string> _netPlayerName = new SyncVar<string>(string.Empty);
    public string PlayerName => _netPlayerName.Value;

    // La llama NetworkGameManager al spawnear, con el nombre que mandó el cliente.
    [Server]
    public void AssignPlayerName(string playerName)
    {
        _netPlayerName.Value = string.IsNullOrWhiteSpace(playerName) ? "Jugador" : playerName.Trim();
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
    public void ServerRequestActivateAbility(EAbilityInput inputSlot, Vector3 aimPoint, Vector3 moveDir)
        => ServerActivateAbility(inputSlot, aimPoint, moveDir);

    // Valida y ejecuta la habilidad de un slot con autoridad de servidor, y
    // replica su animación a los observadores. Separado del ServerRpc de arriba
    // para que código que YA corre en el servidor (ej. la auto-revivida de la
    // clase Inmortal en HandlePlayerDeath) pueda activar directo, sin pasar por
    // el [ServerRpc] de dueño —que el servidor NO puede invocar sobre un cliente
    // remoto (fallaría por RequireOwnership) y dejaba al Inmortal remoto tirado—.
    [Server]
    public void ServerActivateAbility(EAbilityInput inputSlot, Vector3 aimPoint, Vector3 moveDir = default)
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

        // El dueño calculó este punto con SU cámara y nos lo mandó; lo dejamos
        // disponible en el PlayerController para que RotateToAim()/GetAimPoint()
        // lo usen en vez de intentar leer Camera.main en el servidor.
        //
        // VA ANTES DE CanActivate() a propósito: hay habilidades que validan si tienen
        // un objetivo A TIRO (Enemigo jurado, las de GA_Target), y para eso necesitan la
        // mira de ESTE pedido. Con la asignación después, el servidor las validaba
        // contra la mira de la activación ANTERIOR y podía rechazar un tiro bueno.
        PlayerController pc = GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.NetworkAimPoint     = aimPoint;
            // Dirección de movimiento del dueño al activar (para la esquiva
            // direccional del dash). Igual que NetworkAimPoint: el servidor no
            // puede leer el input del dueño, así que este la envía con el pedido.
            pc.NetworkMoveDirection = moveDir;
        }

        if (!ability.CanActivate())
        {
            Debug.Log($"[Server] Habilidad {ability.AbilityName} bloqueada.");
            NotifyOwnerAbilityRejected();
            return;
        }

        ability.Activate();

        // Una habilidad de MANTENER no anima con un clip one-shot sino con un estado
        // sostenido: su Activate() ya mandó ServerBroadcastHoldAnimation. Mandar acá
        // además el clip suelto pisaría ese estado apenas se levanta.
        if (ability is IHoldAbility) return;

        // De qué habilidad sale la animación: normalmente ella misma, pero las
        // envoltorio (ataque que cambia según un tag) delegan en su variante — y hay
        // que resolverlo DESPUÉS de Activate(), cuando el tag ya se consumió o no.
        GameplayAbility animSource = ability.ResolveAnimationSource() ?? ability;

        // Mandamos también el índice del registro: con él, cada observador resuelve el
        // asset y puede usar su AnimationClip (esquema nuevo). Sin índice válido, el
        // trigger + ActionID alcanzan para el esquema viejo.
        int animIndex = GameplayAbilityRegistry.Instance != null
            ? GameplayAbilityRegistry.Instance.GetIndex(animSource) : -1;
        // La velocidad se calcula ACÁ (el servidor tiene el dueño y sus stats); en el
        // observador la habilidad es el template del registro y no podría resolverla.
        ObserversPlayAbilityAnimation(animIndex, animSource.AnimationTriggerName, animSource.AnimationID,
                                      animSource.ResolveAnimationSpeed());
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
    private void ObserversPlayAbilityAnimation(int abilityIndex, string trigger, int actionID,
                                               float animationSpeed)
    {
        if (IsOwner) return;

        // Esquema nuevo: si la habilidad trae su propio AnimationClip, se lo pedimos a
        // PlayerController (que lo mete en la ranura genérica de acción). Va por
        // ApplyAbilityAnimation, la variante SIN guard de dueño — acá justamente
        // estamos en copias que no son el dueño.
        GameplayAbility ability = abilityIndex >= 0
            ? GameplayAbilityRegistry.Instance?.GetAbility(abilityIndex) : null;

        if (ability != null && ability.AnimationClip != null)
        {
            PlayerController pc = GetComponent<PlayerController>();
            if (pc != null) { pc.ApplyAbilityAnimation(ability, animationSpeed); return; }
        }

        // Esquema viejo: seteamos el Animator directo.
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
        ObserversPlayAbilityAnimation(-1, trigger, actionID, -1f);
    }

    // =========================================================
    // HABILIDADES DE MANTENER (IHoldAbility — escudo del Paladín, etc.)
    //
    // La ACTIVACIÓN va por el camino normal (ServerRequestActivateAbility). Lo que
    // hace falta acá es el otro extremo: avisarle al servidor que el dueño SOLTÓ el
    // botón, y replicar el levantar/bajar de la animación a los observadores (que
    // no reciben nada por ObserversPlayAbilityAnimation, porque un mantenido no es
    // un clip one-shot sino un estado con bool).
    // =========================================================

    // El dueño soltó el botón. Se busca la habilidad del slot y se le pide que
    // termine; EndHold es idempotente, así que un pedido duplicado (soltar justo
    // cuando el servidor ya la cortó por falta de energía) no hace nada.
    [ServerRpc]
    public void ServerRequestEndHoldAbility(EAbilityInput inputSlot)
    {
        GameplayAbility ability = FindAbilityBySlot(inputSlot);
        if (ability is IHoldAbility hold) hold.EndHold();
    }

    // Qué le pedimos a los observadores respecto de la animación de mantenido.
    public enum EHoldAnimationPhase
    {
        Start,   // levantar → entrar al bucle
        Stop,    // salir del bucle (bajar)
        Impact   // reacción one-shot al recibir un golpe en el escudo, sin salir del mantenido
    }

    // Replica una fase de la animación de mantenido a los observadores. La habilidad
    // viaja por índice de registro para que cada peer resuelva SUS clips desde su
    // propia copia del asset.
    [Server]
    public void ServerBroadcastHoldAnimation(GameplayAbility ability, EHoldAnimationPhase phase)
    {
        int index = GameplayAbilityRegistry.Instance != null
            ? GameplayAbilityRegistry.Instance.GetIndex(ability) : -1;

        if (index < 0 && phase != EHoldAnimationPhase.Stop)
            Debug.LogWarning($"[NetworkASC] '{ability?.AbilityName}' no está en GameplayAbilityRegistry — " +
                             $"su animación de mantenido no se replicará a los clientes remotos.");

        ObserversPlayHoldAnimation(index, phase);
    }

    [ObserversRpc]
    private void ObserversPlayHoldAnimation(int abilityIndex, EHoldAnimationPhase phase)
    {
        // Levantar y bajar los salteamos en el DUEÑO: ya los disparó por predicción
        // (cliente remoto) o vía Activate()/ProcessHoldRelease (host).
        //
        // El IMPACTO no: es un evento que nace en el SERVIDOR (lo dispara la barrera al
        // frenar daño), así que el dueño remoto no lo predijo y sí necesita recibirlo.
        // Solo se saltea el personaje del HOST, donde el propio evento ya lo animó —
        // mismo criterio que ObserversPlayComboStepAnimation.
        bool skip = phase == EHoldAnimationPhase.Impact
            ? (IsServerInitialized && IsOwner)
            : IsOwner;
        if (skip) return;

        PlayerController pc = GetComponent<PlayerController>();
        if (pc == null) return;

        // Bajar solo necesita el bool, así que no depende de resolver la habilidad:
        // si el índice no estaba registrado, al menos el escudo no queda pegado.
        if (phase == EHoldAnimationPhase.Stop) { pc.ApplyStopHoldAnimation(); return; }

        GameplayAbility ability = abilityIndex >= 0
            ? GameplayAbilityRegistry.Instance?.GetAbility(abilityIndex) : null;
        if (ability == null) return;

        if (phase == EHoldAnimationPhase.Start) pc.ApplyHoldAnimation(ability);
        else if (ability is IHoldAbility hold)  pc.ApplyHoldImpactAnimation(hold.HoldImpactClip);
    }

    // =========================================================
    // DESTELLO DE LA BARRERA AL BLOQUEAR
    //
    // El bloqueo se resuelve SOLO en el servidor (es donde corre el pipeline de daño),
    // así que el feedback visual del escudo necesita su propia réplica: sin esto, el
    // que dispara nunca vería que su tiro pegó contra un escudo, que es justamente la
    // información que tiene que leer para dejar de disparar y rodearlo.
    //
    // Solo viaja el punto de impacto: el resto (colores, duración, prefab del efecto)
    // lo resuelve cada peer con su propia copia del ShieldBlockFlash del prefab.
    // =========================================================

    [Server]
    public void ServerBroadcastShieldBlockFlash(Vector3 hitPoint)
    {
        ObserversPlayShieldBlockFlash(hitPoint);
    }

    [ObserversRpc]
    private void ObserversPlayShieldBlockFlash(Vector3 hitPoint)
    {
        // El servidor ya lo reprodujo local en Entity_ShieldBarrier.BroadcastFlash
        // (importa para el host, que renderiza esa misma copia).
        if (IsServerInitialized) return;

        Entity_ShieldBarrier barrier = GetComponentInChildren<Entity_ShieldBarrier>(true);
        if (barrier != null) barrier.PlayFlash(hitPoint);
    }

    // =========================================================
    // ANIMACIÓN DE UN PASO DE COMBO
    //
    // Un combo (GA_ComboSequence / GA_AlternatingCombo) transmite su PROPIA animación
    // al activarse, pero sus pasos corren después, dentro de una corutina server-side.
    // Cada paso llama a pc.PlayAnimation(this), que tiene guard de dueño — o sea que
    // en el host se ve, pero para un cliente remoto (dueño u observador) no se veía
    // NADA: se quedaban con la animación del combo padre, que normalmente ni siquiera
    // tiene clip propio. Esto replica cada paso a todos los demás peers.
    //
    // Va por COORDENADAS (índice del combo en el registro + qué secuencia + qué paso)
    // en vez de por la habilidad del paso: el paso puede traer un AnimationClipOverride
    // distinto al clip de su propio asset, y resolver la habilidad en el registro daría
    // el clip equivocado. Ver GameplayAbility.GetStepAnimationClip.
    // =========================================================

    [Server]
    public void ServerBroadcastComboStepAnimation(GameplayAbility parent, int sequenceIndex,
                                                  int stepIndex, GameplayAbility stepInstance)
    {
        if (parent == null || stepInstance == null) return;

        int parentIndex = GameplayAbilityRegistry.Instance != null
            ? GameplayAbilityRegistry.Instance.GetIndex(parent) : -1;

        if (parentIndex < 0)
            Debug.LogWarning($"[NetworkASC] '{parent.AbilityName}' no está en GameplayAbilityRegistry — " +
                             $"las animaciones de sus pasos no se replicarán a los clientes remotos.");

        // La velocidad se resuelve ACÁ: el paso ya trae el AnimationSpeedOverride que le
        // impuso el combo, y en el observador es solo un clip suelto sin dueño.
        ObserversPlayComboStepAnimation(parentIndex, sequenceIndex, stepIndex,
                                        stepInstance.AnimationTriggerName, stepInstance.AnimationID,
                                        stepInstance.ResolveAnimationSpeed());
    }

    [ObserversRpc]
    private void ObserversPlayComboStepAnimation(int parentIndex, int sequenceIndex, int stepIndex,
                                                 string trigger, int actionID, float animationSpeed)
    {
        // Salteamos SOLO donde el Activate() del paso ya animó por su cuenta: en el
        // servidor Y siendo el dueño, o sea el personaje propio del host. Repetirla ahí
        // dispararía el trigger dos veces sobre el mismo Animator y el segundo queda
        // buffeado, reproduciendo el ataque de nuevo solo.
        //
        // Las dos condiciones importan:
        //  · IsOwner solo (sin IsServerInitialized) saltearía al dueño REMOTO, que sí
        //    necesita esto: su predicción local cubrió el combo padre, no los pasos.
        //  · IsServerInitialized solo saltearía, en el host, a los jugadores AJENOS —
        //    y ahí Activate() no animó nada, porque pc.PlayAnimation tiene guard de
        //    dueño y esas copias no son del host. Sus combos se veían mudos.
        if (IsServerInitialized && IsOwner) return;

        PlayerController pc = GetComponent<PlayerController>();
        if (pc == null) return;

        GameplayAbility parent = parentIndex >= 0
            ? GameplayAbilityRegistry.Instance?.GetAbility(parentIndex) : null;

        // Si no se puede resolver el clip, PlayActionClip cae solo al esquema viejo.
        AnimationClip clip = parent != null ? parent.GetStepAnimationClip(sequenceIndex, stepIndex) : null;
        pc.PlayActionClip(clip, animationSpeed, trigger, actionID);
    }

    // Igual que la anterior pero a partir de la habilidad, para que los observadores
    // puedan usar su AnimationClip. Preferí esta cuando tengas el asset a mano.
    [Server]
    public void ServerBroadcastAbilityAnimation(GameplayAbility ability)
    {
        if (ability == null) return;
        int index = GameplayAbilityRegistry.Instance != null
            ? GameplayAbilityRegistry.Instance.GetIndex(ability) : -1;
        ObserversPlayAbilityAnimation(index, ability.AnimationTriggerName, ability.AnimationID,
                                      ability.ResolveAnimationSpeed());
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
    // TELETRANSPORTE — mover al jugador desde el servidor.
    //
    // IMPORTANTE: el transform del jugador es CLIENT-AUTHORITATIVE (su
    // NetworkTransform lo manda el dueño). Si el servidor le escribe la posición
    // directo, el próximo paquete del dueño —que sigue donde estaba— la PISA: se
    // ve como que el personaje aparece en el destino y "vuelve" al instante.
    // Por eso cualquier reposicionamiento tiene que ejecutarlo el proceso DUEÑO,
    // y el servidor solo le manda a dónde ir por TargetRpc.
    //
    // Lo usan el blink del Pícaro (GA_Blink) y el respawn (NetworkGameManager /
    // PlayerController). DeathZone hace lo mismo por su cuenta, filtrando por
    // IsOwner.
    // =========================================================

    // El servidor le pide al dueño que se teletransporte a 'position' mirando
    // hacia 'faceDir'.
    [Server]
    public void ServerTeleportOwnerTo(Vector3 position, Vector3 faceDir)
    {
        TargetExecuteTeleport(Owner, position, faceDir);
    }

    [TargetRpc]
    private void TargetExecuteTeleport(NetworkConnection conn, Vector3 position, Vector3 faceDir)
    {
        PlayerController pc = GetComponent<PlayerController>();
        if (pc != null) pc.TeleportTo(position, faceDir);
    }

    // =========================================================
    // DASH (GA_Dash / Dash siniestro) — impulso rápido con inercia.
    //
    // El daño a lo largo del trayecto lo resuelve el servidor al activar (ver
    // GA_Dash, que hace un barrido geométrico del recorrido). Acá solo movemos
    // al dueño: le aplicamos el impulso (que se atenúa solo en PlayerController,
    // dando el "frenado con inercia") y excluimos la capa de jugadores de su
    // colisión durante el dash para atravesarlos, restaurándola al terminar.
    // La colisión con paredes se mantiene (no se excluye esa capa).
    //
    // El LayerMask viaja como int: FishNet serializa int de forma nativa, no así
    // el struct LayerMask.
    // =========================================================

    [Server]
    public void ServerStartDash(Vector3 velocity, float duration, int excludeMask, bool faceVelocity = true,
                               float exitSpeedPercent = 0f, float exitDamping = 3f)
    {
        TargetExecuteDash(Owner, velocity, duration, excludeMask, faceVelocity, exitSpeedPercent, exitDamping);
    }

    [TargetRpc]
    private void TargetExecuteDash(NetworkConnection conn, Vector3 velocity, float duration, int excludeMask,
                                  bool faceVelocity, float exitSpeedPercent, float exitDamping)
    {
        PlayerController pc = GetComponent<PlayerController>();
        if (pc == null) return;

        // Dash 3D: se puede en el aire y en cualquier dirección (incluye vertical).
        // faceVelocity=false en la esquiva direccional del pícaro: el cuerpo NO gira
        // hacia el dash, mantiene la orientación a la mira.
        pc.ApplyDashVelocity(velocity, faceVelocity);
        StartCoroutine(DashRoutine(pc, duration, excludeMask, exitSpeedPercent, exitDamping));
    }

    // Corre en el proceso dueño: mantiene la exclusión de colisión durante el
    // dash y, al terminar, la restaura, devuelve el control del movimiento y
    // libera el estado "atacando".
    private IEnumerator DashRoutine(PlayerController pc, float duration, int excludeMask,
                                   float exitSpeedPercent, float exitDamping)
    {
        pc.SetCollisionExclusion(excludeMask, true);
        yield return new WaitForSeconds(duration);
        pc.SetCollisionExclusion(excludeMask, false);
        pc.ClearDashVelocity(exitSpeedPercent, exitDamping);
        pc.FinishAttack();
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
        AwardKillExperience();
        ObserversHandleDeath();
    }

    // Le da EXP al jugador que consiguió esta baja. El ASC dejó anotado quién pegó
    // el daño en LastAttacker (poblado solo en la copia del servidor, que es donde
    // corre HandleDeath). El _netExp del matador sincroniza el cambio a su cliente,
    // igual que hace el cheat de nivel — no hace falta nada extra acá.
    [Server]
    private void AwardKillExperience()
    {
        if (_asc == null) return;

        AbilitySystemComponent killer = _asc.LastAttacker;
        if (killer == null || ReferenceEquals(killer, _asc)) return;

        // En el modo Mercenarios la experiencia NO es individual: va a la bolsa
        // compartida del equipo del que remató, y de ahí sale el nivel de sus tres
        // jugadores. Este es el único punto del core que sabe quién dio el golpe final,
        // así que la baja se rutea acá y el modo decide cuánto vale (NPC o jugador).
        if (MercenariesGameMode.Instance != null)
        {
            MercenariesGameMode.Instance.ServerAwardKill(killer, _asc);
            return;
        }

        // CAMINO VIEJO (sin modo de juego, ej. la escena de pruebas): experiencia
        // INDIVIDUAL para el que remató, con el valor de este componente.
        //
        // El chequeo de ExperiencePerKill va acá abajo y no arriba de todo a propósito:
        // arriba cortaba el método entero, así que poner este campo en 0 —cosa razonable
        // cuando uno cree que no se usa— dejaba también al modo Mercenarios sin repartir
        // NADA de experiencia por bajas, sin ningún aviso.
        if (ExperiencePerKill <= 0f) return;

        NetworkAbilitySystemComponent killerNet = killer.GetComponent<NetworkAbilitySystemComponent>();
        if (killerNet != null) killerNet.ServerGainExperience(ExperiencePerKill);
    }

    // El ASC llegó a nivel máximo (por experiencia de bajas). OnMaxLevelReached se
    // disparó en la copia del SERVIDOR, pero la UI de selección de subclase escucha
    // ese evento en el ASC LOCAL del dueño — así que a un dueño remoto hay que
    // avisarle por su conexión. En el host el dueño ES este proceso y su UI ya
    // escuchó el evento local, por eso solo reenviamos cuando !IsOwner.
    private void HandleMaxLevelReached()
    {
        if (!IsServerInitialized) return;
        if (!IsOwner) TargetShowSubclassSelection(Owner);
    }

    [TargetRpc]
    private void TargetShowSubclassSelection(NetworkConnection conn)
    {
        // En el host el evento ya se disparó localmente; evitamos re-disparar.
        if (IsServerInitialized) return;
        if (_asc != null) _asc.TriggerMaxLevelReached();
    }

    private void HandleRevive()
    {
        if (!IsServerInitialized) return;
        ObserversHandleRevive();
    }

    [ObserversRpc]
    private void ObserversHandleDeath()
    {
        SafeSetTrigger(GetComponentInChildren<Animator>(), "Death");
    }

    [ObserversRpc]
    private void ObserversHandleRevive()
    {
        SafeSetTrigger(GetComponentInChildren<Animator>(), "Revive");
    }

    // Dispara un trigger del Animator solo si ese parámetro existe. Muerte y
    // revivir son animaciones OPCIONALES: si el AnimatorOverrideController de una
    // clase no define el parámetro, Unity llenaba la consola con "Parameter
    // 'Revive' does not exist". Si querés la animación, agregá el trigger al
    // Animator Controller; si no, esto lo ignora en silencio.
    private static void SafeSetTrigger(Animator anim, string trigger)
    {
        if (anim == null || string.IsNullOrEmpty(trigger)) return;

        foreach (var p in anim.parameters)
        {
            if (p.name == trigger)
            {
                anim.SetTrigger(trigger);
                return;
            }
        }
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
            // Ability puede ser null: un slot sin habilidad asignada (ej. el ult del
            // Ilusionista aún sin implementar). Sin este guard, acceder a
            // assignment.Ability.AbilityName tira NullReference desde el sync de
            // cooldowns, que recorre TODOS los slots cada 0.25s.
            if (assignment.InputSlot != slot || assignment.Ability == null) continue;

            foreach (var granted in _asc.GrantedAbilities)
            {
                if (granted != null && granted.AbilityName == assignment.Ability.AbilityName)
                    return granted;
            }
        }
        return null;
    }
}
