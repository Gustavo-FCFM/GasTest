using UnityEngine;
using System.Collections.Generic;
using System;

// ============================================================
// AbilitySystemComponent
//
// Núcleo del sistema de habilidades (GAS) de un personaje: guarda
// sus atributos (vida, ataque, etc.), sus tags de estado, los
// efectos activos y las habilidades otorgadas, y expone toda la
// lógica para aplicarles daño/curación/buffs. Vive en el mismo
// GameObject que NetworkAbilitySystemComponent, que se suscribe a
// sus eventos para replicar los cambios a la red — este script en
// sí no sabe nada de FishNet.
// ============================================================
public class AbilitySystemComponent : MonoBehaviour
{
    // =========================================================
    // CONFIGURACIÓN
    // =========================================================

    [Header("Configuración de Rol")]
    // Stats iniciales (vida, maná, etc.) que se cargan al arrancar o al
    // llamar InitializeAttributes(). Normalmente lo asigna la clase actual.
    public AttributeSetDefinition CharacterRoleDefinition;

    [Header("Clase y Progresión")]
    // Clase equipada actualmente; se usa para saber cuánto suben los stats
    // al subir de nivel (StatGrowthPerLevel).
    public CharacterClassDefinition CurrentClass;

    [Header("Multijugador y Afiliación")]
    // Equipo al que pertenece. 0 = neutral/hostil para todos (ver
    // IsEnemyOf). NetworkGameManager le asigna un valor único por jugador.
    public int TeamID = 0;

    // Nivel máximo alcanzable; al llegarlo, GainExperience deja de subir
    // de nivel y dispara OnMaxLevelReached.
    public int MaxLevel = 3;

    // =========================================================
    // EVENTOS — NetworkAbilitySystemComponent se suscribe a estos en
    // Awake() para saber cuándo replicar algo a la red
    // =========================================================

    // Se dispara cada vez que un atributo cambia de valor (ej: al recibir
    // daño). Parámetros: qué atributo y su nuevo valor.
    public event Action<EAttributeType, float> OnAttributeChangedCallback;

    // Se disparan al agregar/quitar un tag de estado (ej: Stunned).
    public event Action<EGameplayTag> OnTagAddedCallback;
    public event Action<EGameplayTag> OnTagRemovedCallback;

    // Se disparan al agregar/quitar un efecto CON DURACIÓN (buff/debuff) —
    // alimentan la barra de efectos activos en red. El primero manda
    // también la duración total.
    public event Action<GameplayEffect, float> OnActiveEffectAddedCallback;
    public event Action<GameplayEffect> OnActiveEffectRemovedCallback;

    // Eventos de juego: progresión, muerte y revivir.
    public event Action OnLevelUp;
    public event Action OnDeath;
    public event Action OnRevive;
    public event Action OnMaxLevelReached;

    // Se dispara cuando ESTE personaje le REPARTE un golpe de daño a alguien.
    // Parámetros: la víctima y CUÁNTO daño entró de verdad (ya pasado por el
    // bloqueo, las defensas y el escudo — o sea, lo que realmente le bajó de la
    // vida, siempre positivo). Solo en golpes directos (no ticks de DoT), y con
    // autoridad de servidor (ver ExecuteInstantEffect).
    //
    // La cantidad viaja en el evento porque hay pasivas que curan/escalan con el
    // daño hecho (Aura de protección del Paladín), y recalcularla afuera sería
    // imposible: el atacante no conoce las defensas ni el escudo de la víctima.
    // Las pasivas que no la necesitan (Cuchillas ilusorias) simplemente la ignoran.
    public event Action<AbilitySystemComponent, float> OnDealtDamage;

    // Permite que ExecuteInstantEffect (que corre sobre la VÍCTIMA) dispare el
    // evento en el ATACANTE. Los eventos solo se pueden invocar desde su clase.
    public void NotifyDealtDamage(AbilitySystemComponent victim, float damageDealt)
        => OnDealtDamage?.Invoke(victim, damageDealt);

    // Se dispara cuando ESTE personaje RECIBE un golpe de daño directo (el
    // parámetro es el atacante). Mismo criterio que OnDealtDamage: solo golpes
    // directos (no ticks de DoT), server-side. Lo usa la Copia exacta del
    // Ilusionista para reaccionar a quien la golpea (cegar + herir + explotar).
    public event Action<AbilitySystemComponent> OnTookDamage;
    public void NotifyTookDamage(AbilitySystemComponent attacker) => OnTookDamage?.Invoke(attacker);

    // =========================================================
    // ALMACENAMIENTO INTERNO
    // =========================================================

    // Valor actual de cada atributo del personaje (vida, ataque, etc.).
    protected Dictionary<EAttributeType, AttributeValue> Attributes = new Dictionary<EAttributeType, AttributeValue>();

    // Efectos con duración actualmente aplicados (buffs, debuffs,
    // cooldowns). Solo existe poblado de verdad en la copia servidor —
    // ver notas en NetworkAbilitySystemComponent.
    protected List<ActiveGameplayEffect> ActiveEffects = new List<ActiveGameplayEffect>();

    // Copia de trabajo que reutiliza ProcessActiveEffects para recorrer los efectos
    // sin iterar la lista viva (un tick puede modificarla). Es un campo y no una
    // variable local para no alocar una lista por frame y por personaje.
    private readonly List<ActiveGameplayEffect> _tickBuffer = new List<ActiveGameplayEffect>();

    // Tags de estado activos, con el CONTEO de cuántas fuentes los otorgan.
    // El tag existe mientras su conteo sea > 0.
    //
    // Es un conteo y no un simple set porque varios efectos pueden otorgar el
    // MISMO tag a la vez (ej: 3 Heridas apiladas otorgan Status_Wound 3 veces, o
    // un buff y un tótem que comparten Status_Buff_Damage). Con un set, al expirar
    // el PRIMERO se borraba el tag y los demás quedaban sin él aunque siguieran
    // activos. Ver AddTag/RemoveTag.
    protected Dictionary<EGameplayTag, int> GameplayTags = new Dictionary<EGameplayTag, int>();

    // Instancias de habilidades otorgadas a este personaje (una por cada
    // GameplayAbility de su clase). Modificar esta lista a mano rompe la
    // relación con los slots de PlayerController — usar GrantAbility().
    public List<GameplayAbility> GrantedAbilities = new List<GameplayAbility>();

    // Último ASC que le hizo daño a este personaje. Lo setea ExecuteInstantEffect
    // en cada golpe que baja la vida, y se limpia al revivir. Sirve para atribuir
    // la baja: al morir, NetworkAbilitySystemComponent.HandleDeath (server) le da
    // EXP a este atacante. Se guarda en la copia del SERVIDOR (ahí se aplica el
    // daño); en los clientes queda en null, que es lo correcto.
    [HideInInspector] public AbilitySystemComponent LastAttacker;

    // =========================================================
    // UNITY
    // =========================================================

    // Carga los atributos iniciales al crear el objeto.
    void Awake()
    {
        InitializeAttributes();
    }

    // Hace avanzar el cronómetro de todos los efectos activos cada frame.
    void Update()
    {
        ProcessActiveEffects(Time.deltaTime);
    }

    // =========================================================
    // TAGS DE ESTADO
    // =========================================================

    // Consulta si el personaje tiene un tag activo ahora mismo (conteo > 0).
    public bool HasTag(EGameplayTag tag) => GameplayTags.ContainsKey(tag);

    // Cuántas fuentes están otorgando este tag (0 si no lo tiene). Sirve, por
    // ejemplo, para saber cuántas Heridas apiladas hay.
    public int GetTagCount(EGameplayTag tag) => GameplayTags.TryGetValue(tag, out int count) ? count : 0;

    // Suma una fuente del tag. Solo notifica OnTagAddedCallback cuando pasa de
    // 0 a 1 (recién ahí el personaje "gana" el tag de verdad).
    public void AddTag(EGameplayTag tag)
    {
        if (GameplayTags.TryGetValue(tag, out int count))
        {
            GameplayTags[tag] = count + 1;
            return;
        }

        GameplayTags[tag] = 1;
        OnTagAddedCallback?.Invoke(tag);
    }

    // Resta una fuente del tag. Solo lo quita (y notifica) cuando el conteo llega
    // a 0: mientras otro efecto lo siga otorgando, el tag se mantiene.
    public void RemoveTag(EGameplayTag tag)
    {
        if (!GameplayTags.TryGetValue(tag, out int count)) return;

        if (count > 1)
        {
            GameplayTags[tag] = count - 1;
            return;
        }

        GameplayTags.Remove(tag);
        OnTagRemovedCallback?.Invoke(tag);
    }

    // =========================================================
    // ATRIBUTOS
    // =========================================================

    // (Re)carga todos los atributos desde CharacterRoleDefinition. Si el
    // personaje ya tenía nivel/experiencia, los conserva (se llama de
    // nuevo al cambiar de clase). Llamarla resetea buffs/modificadores
    // temporales sobre los atributos recalculados.
    // keepProgress=true (por defecto) conserva nivel/exp si ya los tenía —lo que
    // se quiere al evolucionar a subclase—. En false reinicia a nivel 1 / 0 exp:
    // lo usa el cambio manual de clase desde el menú, que arranca la clase nueva
    // desde cero.
    public void InitializeAttributes(bool keepProgress = true)
    {
        if (CharacterRoleDefinition == null) return;

        float savedLevel = 1, savedExp = 0;
        bool hadProgress = keepProgress && Attributes.ContainsKey(EAttributeType.Level);
        if (hadProgress) { savedLevel = GetAttributeValue(EAttributeType.Level); savedExp = GetAttributeValue(EAttributeType.Exp); }

        Attributes.Clear();

        // Primero TODO lo que el set define explícitamente.
        foreach (var attrData in CharacterRoleDefinition.InitialAttributes)
            if (!Attributes.ContainsKey(attrData.Attribute))
                Attributes.Add(attrData.Attribute, new AttributeValue(attrData.BaseValue));

        // Recién después derivamos los "Max" de su pool, y SOLO si el set no los trae.
        // Antes se pisaban siempre con el valor de Health/Mana, así que un
        // AttributeSetDefinition que definiera MaxHealth distinto a Health lo veía
        // ignorado en silencio (imposible arrancar con la vida no llena, por ejemplo).
        if (!Attributes.ContainsKey(EAttributeType.MaxHealth) && Attributes.ContainsKey(EAttributeType.Health))
            Attributes[EAttributeType.MaxHealth] = new AttributeValue(Attributes[EAttributeType.Health].BaseValue);

        if (!Attributes.ContainsKey(EAttributeType.MaxMana) && Attributes.ContainsKey(EAttributeType.Mana))
            Attributes[EAttributeType.MaxMana] = new AttributeValue(Attributes[EAttributeType.Mana].BaseValue);

        if (!Attributes.ContainsKey(EAttributeType.Level))  Attributes[EAttributeType.Level]  = new AttributeValue(1);
        if (!Attributes.ContainsKey(EAttributeType.Exp))    Attributes[EAttributeType.Exp]    = new AttributeValue(0);
        if (!Attributes.ContainsKey(EAttributeType.MaxExp)) Attributes[EAttributeType.MaxExp] = new AttributeValue(100);

        if (hadProgress && savedLevel > 1)
        {
            Attributes[EAttributeType.Level].CurrentValue = savedLevel;
            Attributes[EAttributeType.Exp].CurrentValue   = savedExp;
        }

        // Avisar de los valores recién cargados para que la capa de red los sincronice.
        //
        // Acá los atributos se escriben DIRECTO en el diccionario (no por
        // SetCurrentAttributeValue), así que no dispararon OnAttributeChangedCallback.
        // Para los stats derivados no importa —RecalculateAllAttributes los notifica—,
        // pero los POOLS (Vida, Maná, Energía) están excluidos de ese recálculo: su
        // SyncVar quedaba en 0 hasta el primer golpe o curación, y por eso los demás
        // jugadores veían la barra de vida VACÍA de alguien que estaba a full.
        NotifyAttribute(EAttributeType.Health);
        NotifyAttribute(EAttributeType.MaxHealth);
        NotifyAttribute(EAttributeType.Mana);
        NotifyAttribute(EAttributeType.MaxMana);
        NotifyAttribute(EAttributeType.Energy);
    }

    // Dispara el callback de cambio de un atributo con su valor actual (si existe).
    private void NotifyAttribute(EAttributeType type)
    {
        if (Attributes.ContainsKey(type))
            OnAttributeChangedCallback?.Invoke(type, Attributes[type].CurrentValue);
    }

    // Lee el valor actual de un atributo (0 si el personaje no lo tiene).
    public float GetAttributeValue(EAttributeType type)
        => Attributes.ContainsKey(type) ? Attributes[type].CurrentValue : 0f;

    // Escribe directamente el valor actual de un atributo (sin pasar por
    // un GameplayEffect). La usan ExecuteInstantEffect, level-ups, etc.
    // Dispara OnAttributeChangedCallback y, si deja la Vida en 0, Die().
    public void SetCurrentAttributeValue(EAttributeType type, float val)
    {
        if (type == EAttributeType.Health && val < 1f && HasTag(EGameplayTag.Status_Immortal))
            val = 1f;

        // Clampeo de "pools": la vida (y el maná) nunca quedan en negativo ni
        // superan su máximo. Sin esto, un golpe fuerte dejaba la Vida en negativo
        // (ej. -30/100), que se colaba a la barra de vida y a los cálculos que
        // escalan con la vida faltante (MaxHealth - Health). El tope superior solo
        // se aplica si el Max existe y es > 0 (los NPC/objetos sin MaxHealth
        // definido no se ven afectados). El chequeo de muerte de más abajo sigue
        // disparándose porque val queda exactamente en 0.
        if (type == EAttributeType.Health)
        {
            float max = GetAttributeValue(EAttributeType.MaxHealth);
            val = max > 0f ? Mathf.Clamp(val, 0f, max) : Mathf.Max(0f, val);
        }
        else if (type == EAttributeType.Mana)
        {
            float max = GetAttributeValue(EAttributeType.MaxMana);
            val = max > 0f ? Mathf.Clamp(val, 0f, max) : Mathf.Max(0f, val);
        }
        else if (type == EAttributeType.Energy)
        {
            // La energía es un pool igual que Vida/Maná: la regeneración la empuja
            // hacia arriba (un GE pasivo con Period) y el escudo la consume. Sin este
            // clamp, la regeneración se pasaba de MaxEnergy sin techo y la barra del
            // HUD quedaba por encima del 100% (y el escudo duraba de más).
            float max = GetAttributeValue(EAttributeType.MaxEnergy);
            val = max > 0f ? Mathf.Clamp(val, 0f, max) : Mathf.Max(0f, val);
        }
        else if (type == EAttributeType.Shield)
        {
            val = Mathf.Max(0f, val);
        }

        if (Attributes.ContainsKey(type)) Attributes[type].CurrentValue = val;
        else Attributes[type] = new AttributeValue(val);

        OnAttributeChangedCallback?.Invoke(type, val);

        if (type == EAttributeType.Health && val <= 0 && !HasTag(EGameplayTag.State_Dead))
            Die();

        // Si lo que acaba de cambiar es un TECHO y quedó por debajo de su pool, hay que
        // bajar el pool: el clamp de arriba solo actúa cuando se escribe el pool, así
        // que un máximo que BAJA dejaba la vida por encima de su tope (ej. 120/100 al
        // pasar del Bárbaro —120 de vida— al Paladín —100—, o al expirar un buff de
        // vida máxima). Se veía "120/100" hasta el primer golpe o curación, que recién
        // ahí escribía la Vida y la clampeaba de golpe.
        ClampPoolToMax(type, EAttributeType.MaxHealth,  EAttributeType.Health);
        ClampPoolToMax(type, EAttributeType.MaxMana,    EAttributeType.Mana);
        ClampPoolToMax(type, EAttributeType.MaxEnergy,  EAttributeType.Energy);
    }

    // Si 'changed' es el atributo techo indicado y el pool asociado lo supera, baja el
    // pool hasta el techo. La reentrada es segura: vuelve a entrar a
    // SetCurrentAttributeValue con el POOL, y ese camino no toca ningún techo.
    private void ClampPoolToMax(EAttributeType changed, EAttributeType maxType, EAttributeType poolType)
    {
        if (changed != maxType) return;

        float max = GetAttributeValue(maxType);
        if (max <= 0f) return; // sin techo definido no clampeamos (NPCs/objetos)

        if (GetAttributeValue(poolType) > max)
            SetCurrentAttributeValue(poolType, max);
    }

    // Sube permanentemente el valor BASE de un atributo (ej: al elegir una
    // mejora roguelike). Si sube MaxHealth/MaxMana, también cura/rellena
    // esa misma cantidad en el valor actual.
    public void UpgradeAttribute(EAttributeType type, float amount)
    {
        if (!Attributes.ContainsKey(type)) return;
        Attributes[type].BaseValue += amount;
        RecalculateAllAttributes();
        if (type == EAttributeType.MaxHealth) SetCurrentAttributeValue(EAttributeType.Health, GetAttributeValue(EAttributeType.Health) + amount);
        if (type == EAttributeType.MaxMana)   SetCurrentAttributeValue(EAttributeType.Mana,   GetAttributeValue(EAttributeType.Mana)   + amount);
    }

    // Recalcula CurrentValue = (Base + Aditivos) * Multiplicativos para
    // todos los atributos "derivados" (no toca Vida/Maná/Energía/Exp/
    // Nivel/Escudo, que se manejan aparte porque son "pools" con su
    // propio valor actual independiente del recálculo).
    // True si el atributo es un "POOL": tiene su propio valor actual que sube y baja
    // por consumo (daño, gasto, regeneración) en vez de calcularse como
    // (Base + Aditivos) × Multiplicativos.
    //
    // Los pools quedan afuera del sistema de modificadores por completo: no se
    // recalculan (RecalculateAllAttributes los saltea, o el recálculo les pisaría el
    // valor actual) y un modificador CON DURACIÓN sobre uno de ellos es inerte —
    // para tocarlos hay que usar Period (por ticks) o Shield. Ver WarnInertPoolModifiers.
    private static bool IsPoolAttribute(EAttributeType type)
    {
        return type == EAttributeType.Health || type == EAttributeType.Mana   ||
               type == EAttributeType.Energy || type == EAttributeType.Exp    ||
               type == EAttributeType.MaxExp || type == EAttributeType.Level  ||
               type == EAttributeType.Shield;
    }

    private void RecalculateAllAttributes()
    {
        foreach (var pair in Attributes)
        {
            EAttributeType type = pair.Key;
            AttributeValue attr = pair.Value;

            if (IsPoolAttribute(type)) continue;

            float newValue = (attr.BaseValue + attr.AdditiveModifier) * attr.MultiplicativeModifier;

            // Piso de velocidad de ataque: AtkSpeed son "segundos entre ataques"
            // (menor = más rápido), así que un mínimo limita la velocidad MÁXIMA
            // por más buffs que se apilen (ej. rage + tótem del tigre). Evita
            // que el personaje quede atacando absurdamente rápido.
            if (type == EAttributeType.AtkSpeed) newValue = Mathf.Max(newValue, 0.2f);

            if (newValue == attr.CurrentValue) continue;

            attr.CurrentValue = newValue;

            // Notificar el cambio para que NetworkAbilitySystemComponent lo
            // sincronice a los clientes. Antes se escribía CurrentValue directo
            // sin disparar el callback — por eso los buffs que cambian stats
            // vía modificadores (MaxHealth, AtkSpeed, MovSpeed...) se aplicaban
            // en el servidor pero NUNCA llegaban a los clientes remotos: la UI
            // del jugador 2 mostraba MaxHealth viejo (ej. 142/120) y no recibía
            // el buff de velocidad de ataque/movimiento.
            OnAttributeChangedCallback?.Invoke(type, newValue);
        }
    }

    // Combina el valor actual de un atributo con un Modifier según su tipo
    // (Add/Multiply/Override). La usa ExecuteInstantEffect.
    private float CalculateModifiedValue(float current, Modifier mod, float magnitude)
    {
        switch (mod.Type)
        {
            case Modifier.EModificationType.Add:      return current + magnitude;
            case Modifier.EModificationType.Multiply: return current * magnitude;
            case Modifier.EModificationType.Override: return magnitude;
            default: return current;
        }
    }

    // =========================================================
    // GAMEPLAY EFFECTS
    // =========================================================

    // Punto de entrada para aplicarle cualquier GameplayEffect a este
    // personaje (daño, curación, buffs, cooldowns...). Si el efecto no
    // tiene duración lo ejecuta una sola vez; si tiene duración lo agrega
    // a ActiveEffects respetando su StackingPolicy (Refresh/Stack/Override).
    // durationOverride pisa la duración del asset (usado por cooldowns
    // dinámicos, ej. basados en velocidad de ataque).
    public void ApplyGameplayEffect(GameplayEffect effect, object source = null, float durationOverride = -1f)
    {
        if (effect == null) return;

        // Inmunidad a debuffs (Imparable del Pirata): mientras Status_Unstoppable esté
        // activo, los efectos marcados como Debuff CON DURACIÓN (CC, DoT) no entran. El
        // daño instantáneo (Duration 0) sí — no queremos hacerlo invulnerable, solo a CC.
        if (effect.EffectType == GameplayEffect.EEffectType.Debuff && effect.Duration > 0 &&
            HasTag(EGameplayTag.Status_Unstoppable))
            return;

        float finalDuration = (durationOverride > 0) ? durationOverride : effect.Duration;

        // Resistencia al control: recorta (o alarga) lo que dura el CC que entra.
        // Si el objetivo lo resiste del todo, el efecto NO se aplica en absoluto.
        if (finalDuration > 0f && effect.CountsAsCrowdControl)
        {
            finalDuration = ResolveCrowdControlDuration(finalDuration);
            if (finalDuration <= 0f) return;
        }

        if (finalDuration <= 0)
        {
            ExecuteInstantEffect(effect, source);
        }
        else
        {
            // Exclusión mutua por grupo (jerarquía): dentro de un mismo
            // EffectGroup solo vive el de mayor Priority. Si ya hay uno igual o
            // superior activo, este (inferior) no se aplica; si este es
            // superior, remueve a los inferiores del grupo. El MISMO efecto
            // (misma Definition) se saltea — su re-aplicación la maneja la
            // StackingPolicy de abajo.
            if (effect.EffectGroup != EGameplayTag.None)
            {
                for (int i = ActiveEffects.Count - 1; i >= 0; i--)
                {
                    GameplayEffect otro = ActiveEffects[i].Definition;
                    if (otro == effect || otro.EffectGroup != effect.EffectGroup) continue;

                    if (otro.Priority >= effect.Priority) return;   // ya hay uno igual/superior
                    RemoveActiveEffect(ActiveEffects[i]);           // este es superior → quitar el inferior
                }
            }

            if (effect.StackingPolicy == GameplayEffect.EStackingType.Refresh)
            {
                foreach (var existing in ActiveEffects)
                {
                    if (existing.Definition == effect)
                    {
                        existing.DurationRemaining = finalDuration;
                        existing.TotalDuration     = finalDuration;
                        OnActiveEffectAddedCallback?.Invoke(effect, finalDuration);
                        return;
                    }
                }
            }
            else if (effect.StackingPolicy == GameplayEffect.EStackingType.Override)
            {
                for (int i = ActiveEffects.Count - 1; i >= 0; i--)
                    if (ActiveEffects[i].Definition == effect)
                        RemoveActiveEffect(ActiveEffects[i]);
            }
            else if (effect.StackingPolicy == GameplayEffect.EStackingType.Stack && effect.MaxStacks > 0)
            {
                int stacks = 0;
                foreach (var existing in ActiveEffects)
                    if (existing.Definition == effect) stacks++;

                // Explosión al tope (Heridas del Ilusionista): si al sumar ESTA
                // acumulación se llega al máximo y hay un OnMaxStacksEffect, se
                // consumen TODAS las acumulaciones y se aplica ese efecto (el daño
                // masivo). La fuente se conserva para el crédito de la muerte / robo
                // de vida.
                if (effect.OnMaxStacksEffect != null && stacks + 1 >= effect.MaxStacks)
                {
                    RemoveEffectsByDefinition(effect);
                    ApplyGameplayEffect(effect.OnMaxStacksEffect, source);
                    return;
                }

                // Reloj COMPARTIDO: cada nueva acumulación refresca la duración de
                // TODAS las existentes. Antes cada instancia tenía su propio reloj, así
                // que la acumulación más vieja expiraba primero (5→4 "de la nada")
                // aunque el icono mostrara el reloj de la última. Con esto, seguir
                // golpeando mantiene vivo el stack entero y todas expiran juntas
                // 'finalDuration' después del último golpe.
                foreach (var existing in ActiveEffects)
                {
                    if (existing.Definition != effect) continue;
                    existing.DurationRemaining = finalDuration;
                    existing.TotalDuration     = finalDuration;
                }

                // Tope normal (sin explosión): al llegar al máximo ya refrescamos
                // arriba, no agregamos otra instancia.
                if (stacks >= effect.MaxStacks)
                {
                    OnActiveEffectAddedCallback?.Invoke(effect, finalDuration);
                    return;
                }
            }

            WarnInertPoolModifiers(effect);

            ActiveGameplayEffect newEffect = new ActiveGameplayEffect(effect, finalDuration, source);
            ActiveEffects.Add(newEffect);

            // El escudo se otorga ANTES que los demás modificadores: si el mismo
            // efecto también sube MaxHealth (ej. Enfurecer), no queremos que eso
            // infle la "vida faltante" con la que escala el escudo.
            newEffect.GrantedShield = GrantTemporaryShield(effect, source);

            ApplyEffectModifiers(effect, true);

            if (effect.GrantedTags != null)
            {
                foreach (EGameplayTag tag in effect.GrantedTags) AddTag(tag);

                // Imparable: al otorgarse el tag de inmunidad, limpia los debuffs ya
                // activos (la inmunidad de arriba se encarga de bloquear los futuros).
                if (effect.GrantedTags.Contains(EGameplayTag.Status_Unstoppable))
                    RemoveAllDebuffs();
            }

            OnActiveEffectAddedCallback?.Invoke(effect, finalDuration);
        }
    }

    // =========================================================
    // RESISTENCIA AL CONTROL (CC)
    // =========================================================

    // Tope de la resistencia: por más fuentes que se apilen, un CC nunca se recorta más
    // allá de esto. Sin tope, dos auras juntas darían inmunidad total al control por la
    // puerta de atrás — y para eso ya está Status_Unstoppable, que es explícito y se ve
    // venir. Mismo criterio que el tope de Resistance en ApplyDefenses.
    private const float MaxCCResistance = 0.9f;

    // Por debajo de esto, un CC deja de tener sentido y se descarta entero.
    //
    // El umbral NO es cosmético: si dejáramos que la duración cayera a 0,
    // ApplyGameplayEffect lo tomaría como efecto INSTANTÁNEO y le aplicaría los
    // modificadores una vez. O sea que "resistir un aturdimiento por completo" te
    // aplicaría igual lo que ese aturdimiento trajera. Devolviendo 0 acá, quien llama
    // corta antes de llegar a esa rama.
    private const float MinCCDuration = 0.05f;

    // Duración final de un efecto de control sobre ESTE personaje, según su
    // CCResistance. Devuelve 0 si lo resiste del todo.
    //
    // La resistencia negativa (un aura enemiga que te la baja) alarga el CC en vez de
    // acortarlo, y eso es a propósito: es lo que deja expresar "reduce la resistencia
    // al aturdimiento de los enemigos" sin un segundo atributo.
    private float ResolveCrowdControlDuration(float duration)
    {
        float resistance = Mathf.Min(GetAttributeValue(EAttributeType.CCResistance), MaxCCResistance);
        if (Mathf.Approximately(resistance, 0f)) return duration;

        float scaled = duration * (1f - resistance);
        return scaled < MinCCDuration ? 0f : scaled;
    }

    // Calcula la magnitud final de un modificador: su valor fijo + el escalado
    // con un atributo del ATACANTE + el escalado con la vida del OBJETIVO (this).
    // No incluye el crítico por la espalda (eso es exclusivo del daño a la vida).
    //
    // El escalado por vida del objetivo sirve tanto para daño (Golpe mortal del
    // Pícaro: % de la vida faltante del enemigo) como para otorgar (escudo del
    // Berserker: % de su PROPIA vida faltante, porque el efecto se lo aplica a sí
    // mismo). El SIGNO del coeficiente marca la dirección, igual que Magnitude:
    // negativo = quita (daño), positivo = otorga (curación/escudo).
    private float CalculateBaseMagnitude(Modifier mod, AbilitySystemComponent sourceASC)
    {
        float magnitude = mod.Magnitude;

        if (mod.UseAttributeScaling && sourceASC != null)
            magnitude += sourceASC.GetAttributeValue(mod.SourceAttribute) * mod.AttributeCoefficient;

        if (mod.UseTargetHealthScaling)
        {
            float portion;
            switch (mod.TargetHealthMode)
            {
                case Modifier.ETargetHealthMode.CurrentHealth: portion = GetAttributeValue(EAttributeType.Health); break;
                case Modifier.ETargetHealthMode.MaxHealth:     portion = GetAttributeValue(EAttributeType.MaxHealth); break;
                default:                                        portion = GetAttributeValue(EAttributeType.MaxHealth) - GetAttributeValue(EAttributeType.Health); break;
            }
            magnitude += portion * mod.TargetHealthCoefficient;
        }

        // Piso del resultado. El signo de MinMagnitude marca la direccion, asi que no hay
        // que inferir nada del valor calculado: positivo empuja hacia arriba (otorgar),
        // negativo hacia abajo (quitar). En 0 no toca nada.
        //
        // Sin esto, un escalado por vida faltante da CERO a vida llena — el escudo del
        // Berserker se otorgaba y se agotaba en el mismo instante, con su burbuja
        // prendiendose y apagandose de golpe.
        if      (mod.MinMagnitude > 0f) magnitude = Mathf.Max(magnitude, mod.MinMagnitude);
        else if (mod.MinMagnitude < 0f) magnitude = Mathf.Min(magnitude, mod.MinMagnitude);

        return magnitude;
    }

    // Cuánto daño a la VIDA le haría a ESTE personaje un efecto instantáneo lanzado
    // por 'sourceASC', si se aplicara ahora. Devuelve un número positivo (0 si el
    // efecto no hace daño).
    //
    // Es una ESTIMACIÓN a propósito: incluye la magnitud base, el escalado por stats
    // del atacante, el escalado por la vida de este objetivo y el daño mágico plano —
    // o sea, todo lo que se puede saber sin llegar a aplicar el golpe. Lo que NO
    // incluye es lo que solo se resuelve al impactar: críticos, bloqueo, defensas y
    // escudo.
    //
    // Existe para los casos en los que un golpe se FRENA antes de resolverse y aun así
    // hace falta saber cuánto valía. Hoy: un proyectil que muere contra una barrera
    // (ver Entity_ShieldBarrier.NotifyProjectileBlocked), donde el escudo tiene que
    // reportar cuánto daño evitó para que las pasivas que escalan con eso —la curación
    // del aura del Paladín— funcionen igual que con un golpe normal.
    public float EstimateInstantDamage(GameplayEffect effect, AbilitySystemComponent sourceASC)
    {
        // Solo los instantáneos: un efecto con duración no "pega" al aplicarse.
        if (effect == null || effect.Duration > 0f) return 0f;

        float total = 0f;

        foreach (var mod in effect.Modifiers)
        {
            if (mod.Attribute != EAttributeType.Health) continue;

            float magnitude = CalculateBaseMagnitude(mod, sourceASC);
            if (magnitude < 0f) total += -magnitude;   // negativo = daño
        }

        // El daño mágico plano del atacante se suma a cualquier golpe que ya hiciera
        // daño, igual que en ExecuteInstantEffect.
        if (total > 0f && sourceASC != null)
            total += sourceASC.GetAttributeValue(EAttributeType.MagicDamage);

        return total;
    }

    // Efectos ya avisados, para no repetir el mismo warning de config cada vez
    // que se aplican.
    private static readonly HashSet<GameplayEffect> _warnedInertEffects = new HashSet<GameplayEffect>();

    // Avisa si un efecto CON duración tiene modificadores sobre "pools" que el
    // sistema de modificadores no puede aplicar y quedarían INERTES en silencio.
    // Los pools (Health/Mana/Energy) tienen su propio valor actual, así que
    // RecalculateAllAttributes los saltea: para tocarlos con un efecto de duración
    // hay que usar Period > 0 (por tick). Shield es la excepción soportada, vía
    // GrantTemporaryShield.
    private void WarnInertPoolModifiers(GameplayEffect effect)
    {
        if (effect.Period > 0 || _warnedInertEffects.Contains(effect)) return;

        foreach (var mod in effect.Modifiers)
        {
            if (mod.Attribute != EAttributeType.Health &&
                mod.Attribute != EAttributeType.Mana &&
                mod.Attribute != EAttributeType.Energy) continue;

            Debug.LogWarning($"[GAS] '{effect.name}' tiene duración y un modificador sobre {mod.Attribute}, " +
                             $"que es un 'pool': NO se aplica por el sistema de modificadores y queda inerte. " +
                             $"Usá Period > 0 para que actúe por ticks (DoT/regeneración), o Shield si querés " +
                             $"un escudo temporal.");
            _warnedInertEffects.Add(effect);
            return;
        }
    }

    // Quita todas las instancias activas de un efecto concreto, revirtiendo sus
    // modificadores, tags y escudo. Sirve para cancelar un buff antes de tiempo
    // (ej: el escudo de carga de Golpe Final si lo interrumpen).
    public void RemoveEffectsByDefinition(GameplayEffect definition)
    {
        if (definition == null) return;

        for (int i = ActiveEffects.Count - 1; i >= 0; i--)
            if (ActiveEffects[i].Definition == definition)
                RemoveActiveEffect(ActiveEffects[i]);
    }

    // Quita todos los efectos activos que otorguen un tag dado (revirtiendo sus
    // modificadores/tags/escudo). Genérico: lo usa BreakInvisibility, y sirve para
    // cualquier "romper el estado X" (ej: una purga que quita todos los debuffs).
    public void RemoveEffectsWithTag(EGameplayTag tag)
    {
        for (int i = ActiveEffects.Count - 1; i >= 0; i--)
            if (ActiveEffects[i].Definition.GrantedTags.Contains(tag))
                RemoveActiveEffect(ActiveEffects[i]);
    }

    // Quita UNA SOLA acumulación de las que otorgan un tag: la que está por vencer.
    // Devuelve true si encontró alguna.
    //
    // Es la versión "de a una" de RemoveEffectsWithTag, para los estados que se
    // CONSUMEN por uso y pueden acumularse (ej. las cargas del Castigo divino del
    // Paladín). Quitando una sola, el CONTEO del tag baja de 2 a 1 y el personaje
    // sigue teniendo el tag — o sea, sigue cargado para el próximo golpe. Con la
    // versión que las borra todas, un solo ataque se llevaba puestas las dos cargas.
    //
    // Se elige la más vieja (menor DurationRemaining) porque es la que se perdería
    // sola por tiempo: gastar esa primero es lo que más aprovecha las acumulaciones.
    public bool RemoveOneEffectWithTag(EGameplayTag tag)
    {
        ActiveGameplayEffect oldest = null;

        foreach (var active in ActiveEffects)
        {
            if (!active.Definition.GrantedTags.Contains(tag)) continue;
            if (oldest == null || active.DurationRemaining < oldest.DurationRemaining)
                oldest = active;
        }

        if (oldest == null) return false;

        RemoveActiveEffect(oldest);
        return true;
    }

    // Quita todos los efectos activos marcados como Debuff (EffectType, el mismo campo
    // que usa la UI para pintarlos de rojo). Lo usa el buff Imparable del Pirata para
    // limpiar CC/debuffs al activarse. Solo recorre ActiveEffects (efectos con
    // duración), así que el daño instantáneo no cuenta.
    public void RemoveAllDebuffs()
    {
        for (int i = ActiveEffects.Count - 1; i >= 0; i--)
            if (ActiveEffects[i].Definition.EffectType == GameplayEffect.EEffectType.Debuff)
                RemoveActiveEffect(ActiveEffects[i]);
    }

    // Termina la invisibilidad de este personaje quitando los efectos que la
    // otorgan. Lo dispara ExecuteInstantEffect al atacar o al recibir daño, y
    // también lo puede llamar una habilidad a mano.
    public void BreakInvisibility()
    {
        if (HasTag(EGameplayTag.Status_Invisible))
            RemoveEffectsWithTag(EGameplayTag.Status_Invisible);
    }

    // Otorga el ESCUDO de un efecto CON duración y devuelve cuánto otorgó.
    // Shield es un "pool" (tiene su propio valor actual que el daño consume), así
    // que no puede pasar por el sistema de modificadores/RecalculateAllAttributes
    // —que lo saltea a propósito— sino que se suma acá al aplicar el efecto y se
    // resta al expirar (ver RemoveActiveEffect). Así el escudo se puede ir de las
    // dos formas: consumido por el daño, o retirado al terminar la duración.
    private float GrantTemporaryShield(GameplayEffect effect, object source)
    {
        AbilitySystemComponent sourceASC = source as AbilitySystemComponent;
        float total = 0f;

        foreach (var mod in effect.Modifiers)
        {
            if (mod.Attribute != EAttributeType.Shield) continue;
            // Para un pool solo tiene sentido sumar una cantidad.
            if (mod.Type != Modifier.EModificationType.Add) continue;

            float amount = CalculateBaseMagnitude(mod, sourceASC);
            if (amount > 0f) total += amount;
        }

        if (total > 0f)
            SetCurrentAttributeValue(EAttributeType.Shield, GetAttributeValue(EAttributeType.Shield) + total);

        return total;
    }

    // Aplica de una vez los Modifiers de un efecto SIN duración (daño,
    // curación, etc.). Calcula escalado con stats del atacante, resuelve
    // el escudo antes que la vida, y dispara robo de vida si corresponde.
    private void ExecuteInstantEffect(GameplayEffect effect, object source = null, bool isPeriodicTick = false)
    {
        AbilitySystemComponent sourceASC = source as AbilitySystemComponent;

        bool wasDamagingHit = false;

        // Daño que de verdad llegó a la VIDA en este golpe (después de bloqueo,
        // defensas y escudo). Se acumula sobre todos los modificadores del efecto y
        // viaja en OnDealtDamage, para las pasivas que curan/escalan con lo hecho
        // (Aura de protección del Paladín).
        float damageToHealth = 0f;

        foreach (var mod in effect.Modifiers)
        {
            if (!Attributes.ContainsKey(mod.Attribute)) continue;

            float calculatedMagnitude = CalculateBaseMagnitude(mod, sourceASC);

            // Pipeline de modificadores de daño SALIENTE del atacante (backstab del
            // Pícaro, crítico mejorado del Asesino, futuros bonos...). Cada pasiva
            // registra su IDamageModifier en el ASC de su dueño; acá recorremos esa
            // lista y resolvemos el crítico. Solo aplica a daño directo a la vida.
            if (sourceASC != null && mod.Attribute == EAttributeType.Health && calculatedMagnitude < 0)
                calculatedMagnitude = sourceASC.ResolveOutgoingDamage(this, calculatedMagnitude, isPeriodicTick);

            if (mod.Attribute == EAttributeType.Health && calculatedMagnitude < 0)
            {
                // INMUNIDAD TOTAL (Status_Immunity, ej. Protección divina del Paladín):
                // el golpe no entra y punto. Se saltea el modificador entero, así que
                // tampoco consume escudo, ni anota al atacante, ni rompe invisibilidad,
                // ni dispara las pasivas de "al golpear".
                //
                // Es distinto de Status_Immortal, que NO es inmunidad: ahí el daño entra
                // normal y lo único que pasa es que la vida no puede bajar de 1.
                //
                // Solo se saltea este modificador: un efecto que además ralentice sigue
                // ralentizando. Lo que la inmunidad niega es el DAÑO.
                if (HasTag(EGameplayTag.Status_Immunity)) continue;

                wasDamagingHit = true;

                // Registrar al atacante para atribuir la baja (EXP al matador). Se
                // guarda antes de resolver escudo/vida: aunque este golpe no sea el
                // letal, deja anotado quién fue el último en pegar.
                if (sourceASC != null && !ReferenceEquals(sourceASC, this))
                    LastAttacker = sourceASC;

                float physicalDamage = Mathf.Abs(calculatedMagnitude);
                float magicDamage    = sourceASC != null ? sourceASC.GetAttributeValue(EAttributeType.MagicDamage) : 0f;

                // Bloqueo DIRECCIONAL antes que nada (escudo del Paladín y compañía):
                // lo que la barrera frena no llega siquiera a las defensas. Ver
                // ResolveIncomingDamage / IIncomingDamageModifier.
                ResolveIncomingDamage(sourceASC, ref physicalDamage, ref magicDamage, isPeriodicTick);

                // Defensas del que RECIBE, ya con TODO el daño entrante sumado
                // (físico + mágico) y antes de tocar el escudo. Ver ApplyDefenses.
                ApplyDefenses(ref physicalDamage, ref magicDamage);

                float currentShield      = GetAttributeValue(EAttributeType.Shield);
                float damageBeforeShield = physicalDamage + magicDamage;

                if (currentShield > 0)
                {
                    if (magicDamage > 0)
                    {
                        float effectiveMagic = magicDamage * 2f;
                        if (currentShield >= effectiveMagic) { currentShield -= effectiveMagic; magicDamage = 0f; }
                        else { magicDamage -= currentShield / 2f; currentShield = 0f; }
                    }
                    if (currentShield > 0 && physicalDamage > 0)
                    {
                        if (currentShield >= physicalDamage) { currentShield -= physicalDamage; physicalDamage = 0f; }
                        else { physicalDamage -= currentShield; currentShield = 0f; }
                    }
                    SetCurrentAttributeValue(EAttributeType.Shield, currentShield);
                }

                // ESCUDO QUE CURA (Status_HealShield — "Cubrir con escudo" del Juramento
                // de la conquista, y más adelante la defensa mejorada del Monje): lo que
                // el escudo alcanzó a frenar no solo no duele, además vuelve como VIDA.
                //
                // El escudo se gasta igual, así que es un INTERCAMBIO: un escudo de 50
                // contra un golpe de 60 cura 50, se rompe, y los 10 que sobran entran
                // normal (ya pasados por bloqueo y defensas, que corren más arriba).
                //
                // Se cura el DAÑO NEUTRALIZADO, no los puntos de escudo gastados. Solo
                // se diferencian con daño MÁGICO, que cuesta el doble de escudo: 50 de
                // escudo frenan 25 de magia, y son 25 los que se curan.
                //
                // Esto es A PROPÓSITO, no un descuido de la conversión: el escudo es
                // mágico, así que las clases que pegan con daño mágico son su contra
                // natural. Le rompen el doble de escudo por punto de daño y, al frenarse
                // menos daño, el dueño se cura menos. Es el mismo hecho visto dos veces,
                // y hace que un mago sea la respuesta a un Paladín de la Conquista.
                //
                // El tag lo trae el mismo GE que otorga el escudo, así que cuando el
                // escudo se agota la curación deja de sumar sola (absorbed queda en 0)
                // aunque al tag le quede duración.
                float absorbedByShield = damageBeforeShield - (physicalDamage + magicDamage);
                if (absorbedByShield > 0f && HasTag(EGameplayTag.Status_HealShield))
                    SetCurrentAttributeValue(EAttributeType.Health,
                                             GetAttributeValue(EAttributeType.Health) + absorbedByShield);

                calculatedMagnitude = -(physicalDamage + magicDamage);
                damageToHealth     += physicalDamage + magicDamage;
            }

            float newValue = CalculateModifiedValue(Attributes[mod.Attribute].CurrentValue, mod, calculatedMagnitude);
            SetCurrentAttributeValue(mod.Attribute, newValue);
            HandleLifeSteal(mod, calculatedMagnitude, sourceASC);
        }

        // Rotura de invisibilidad: un golpe de daño delata tanto a quien lo RECIBE
        // (this) como a quien lo REPARTE (sourceASC).
        //
        // Solo en golpes DIRECTOS (no ticks de DoT): un tick periódico corre desde
        // ProcessActiveEffects, que está iterando ActiveEffects — y BreakInvisibility
        // remueve efectos de esa misma lista, lo que reventaría la iteración. Además,
        // el caso "estoy invisible y me tickea un veneno viejo" es marginal.
        if (wasDamagingHit && !isPeriodicTick)
        {
            BreakInvisibility(); // el objetivo se vuelve visible al ser golpeado

            if (sourceASC != null && !ReferenceEquals(sourceASC, this))
            {
                sourceASC.BreakInvisibility();       // el atacante se delata al golpear
                sourceASC.NotifyDealtDamage(this, damageToHealth); // pasivas "al golpear" (Cuchillas ilusorias, Aura del Paladín)
                NotifyTookDamage(sourceASC);         // reacciones "al ser golpeado" (ej. Copia exacta)
            }
        }
    }

    // =========================================================
    // DEFENSAS (del que RECIBE el golpe)
    // =========================================================

    // Aplica las tres defensas al daño que está por entrar, en este orden:
    //
    //   1) VULNERABILIDAD (%): sube el daño. 0.1 = recibe 10% más.
    //   2) RESISTENCIA (%): lo baja. 0.2 = evita el 20%.
    //   3) Redondeo hacia ABAJO.
    //   4) DEFENSA (fija): resta un valor plano.
    //
    // Ejemplo del diseño: 10 de daño, +10% vulnerabilidad → 11; −20% resistencia →
    // 8.8; redondeo → 8; defensa 1 → 7. Recién después entran escudo y vida.
    //
    // Las dos porcentuales se combinan en UN multiplicador y se aplican por igual al
    // daño físico y al mágico (son "cuánto duele todo lo que entra"). La DEFENSA en
    // cambio solo recorta el FÍSICO: el daño mágico la ignora, igual que ya penetra
    // el escudo — es lo que hace que subir defensa no vuelva a nadie inmune a magia.
    //
    // Ojo de balance: la defensa se descuenta POR GOLPE, así que castiga mucho más a
    // los ataques rápidos y a los ticks de veneno que a un golpe único y grande.
    // Daño mínimo que deja pasar la DEFENSA: si la resta fija dejaría el golpe en 0
    // (o menos), igual entra 1. Evita que acumular defensa vuelva a alguien
    // literalmente inmune a un ataque, sin tocar el valor fijo en sí.
    private const float MinDamageAfterDefense = 1f;

    private void ApplyDefenses(ref float physicalDamage, ref float magicDamage)
    {
        float vulnerability = GetAttributeValue(EAttributeType.Vulnerability);
        float resistance    = GetAttributeValue(EAttributeType.Resistance);

        // Tope de resistencia: al 100% el daño sería 0 (inmunidad total) y por encima
        // el golpe pasaría a CURAR. Se corta en 90% por más que se apilen buffs.
        resistance = Mathf.Min(resistance, 0.9f);

        float multiplier = (1f + vulnerability) * (1f - resistance);
        if (multiplier < 0f) multiplier = 0f; // una vulnerabilidad negativa enorme tampoco cura

        physicalDamage *= multiplier;
        magicDamage    *= multiplier;

        // Redondeo hacia abajo (8.8 → 8): a favor de quien recibe, y deja números
        // enteros en los indicadores de daño.
        physicalDamage = Mathf.Floor(physicalDamage);
        magicDamage    = Mathf.Floor(magicDamage);

        // Defensa: reducción FIJA al físico. Si la resta dejaría el golpe en 0 o menos,
        // igual entra 1 de daño: un ataque que conecta nunca debería no hacer NADA, y
        // así acumular defensa no vuelve a nadie inmune a los ataques chicos.
        //
        // El piso solo aplica a golpes que traían daño físico: si el ataque era puro
        // daño mágico (physicalDamage 0), no se inventa daño de la nada.
        float defense = GetAttributeValue(EAttributeType.Def);
        if (defense > 0f && physicalDamage > 0f)
            physicalDamage = Mathf.Max(MinDamageAfterDefense, physicalDamage - defense);
    }

    // Suma/resta los modificadores Add/Multiply de un efecto CON duración
    // a los atributos afectados (apply=true al agregarlo, false al
    // quitarlo) y recalcula todo. No toca daño instantáneo.
    private void ApplyEffectModifiers(GameplayEffect effect, bool apply)
    {
        float sign = apply ? 1f : -1f;
        foreach (var mod in effect.Modifiers)
        {
            // Shield es un "pool": no pasa por el sistema de modificadores (que
            // RecalculateAllAttributes saltea igual). Su alta/baja la manejan
            // GrantTemporaryShield y RemoveActiveEffect.
            if (mod.Attribute == EAttributeType.Shield) continue;

            // Si el personaje todavía no tiene ese atributo, se CREA en 0 y el
            // modificador entra igual.
            //
            // Antes acá había un `continue` y era un agujero grande y SILENCIOSO: si el
            // atributo no estaba listado en el AttributeSetDefinition de la clase, el
            // modificador se descartaba sin aviso. Como ninguna clase declaraba
            // Resistance, el Aura de protección del Paladín y el buff de la Intercepción
            // heroica no hacían absolutamente nada, y no había forma de notarlo salvo
            // midiendo el daño a mano.
            //
            // Crear en 0 es lo coherente con el resto del sistema: GetAttributeValue ya
            // devuelve 0 para un atributo ausente, o sea que "no declarado" siempre
            // significó "vale 0". Los POOLS quedan afuera porque un modificador con
            // duración sobre ellos es inerte por diseño (ver WarnInertPoolModifiers).
            if (!Attributes.TryGetValue(mod.Attribute, out AttributeValue attr))
            {
                if (IsPoolAttribute(mod.Attribute)) continue;

                attr = new AttributeValue(0f);
                Attributes[mod.Attribute] = attr;
            }
            if (mod.Type == Modifier.EModificationType.Add)
                attr.AdditiveModifier += mod.Magnitude * sign;
            else if (mod.Type == Modifier.EModificationType.Multiply)
                attr.MultiplicativeModifier += (mod.Magnitude - 1f) * sign;
        }
        RecalculateAllAttributes();
    }

    // Quita un efecto activo: revierte sus modificadores, le saca los tags
    // que otorgaba, y notifica OnActiveEffectRemovedCallback (usado tanto
    // para remoción explícita como para expiración natural).
    private void RemoveActiveEffect(ActiveGameplayEffect effect)
    {
        ApplyEffectModifiers(effect.Definition, false);

        // Escudo temporal: retiramos exactamente lo que este efecto otorgó, sin
        // bajar de 0 (si el daño ya se lo comió, no le sacamos escudo de otros).
        if (effect.GrantedShield > 0f)
        {
            float current = GetAttributeValue(EAttributeType.Shield);
            SetCurrentAttributeValue(EAttributeType.Shield, Mathf.Max(0f, current - effect.GrantedShield));
        }

        foreach (EGameplayTag tag in effect.Definition.GrantedTags) RemoveTag(tag);
        ActiveEffects.Remove(effect);
        OnActiveEffectRemovedCallback?.Invoke(effect.Definition);
    }

    // Le hace bajar un tick a la duración (y al período, si es
    // periódico) de todos los efectos activos, ejecuta los ticks que
    // corresponda, y limpia los que ya expiraron. Se llama una vez por
    // frame desde Update().
    private void ProcessActiveEffects(float deltaTime)
    {
        if (ActiveEffects.Count == 0) return;

        // Recorremos una COPIA, no la lista viva: un tick puede bajar la vida a 0 y
        // disparar Die() → OnDeath, y cualquier handler que aplique o quite un efecto
        // sobre ESTE mismo personaje modificaría ActiveEffects en plena iteración
        // (InvalidOperationException). Con la copia, esos cambios son seguros.
        _tickBuffer.Clear();
        _tickBuffer.AddRange(ActiveEffects);

        // Un cadáver no recibe ticks: pegarle a un muerto no hace nada y evita
        // re-entrar en la muerte. Las duraciones SÍ siguen corriendo, así que los
        // efectos vencen normalmente aunque el personaje esté esperando revivir.
        bool isDead = HasTag(EGameplayTag.State_Dead);

        List<ActiveGameplayEffect> expired = null;

        foreach (var active in _tickBuffer)
        {
            // Pudo haberlo quitado un handler durante este mismo barrido.
            if (!ActiveEffects.Contains(active)) continue;

            active.DurationRemaining -= deltaTime;

            if (!isDead && active.Definition.Period > 0)
            {
                active.PeriodRemaining -= deltaTime;
                if (active.PeriodRemaining <= 0)
                {
                    // Le pasamos la fuente original para que el tick tenga robo de
                    // vida / escalado por el atacante. isPeriodicTick evita que un
                    // tick de DoT dispare el crítico por la espalda (no queremos que
                    // una herida pegue más fuerte por dónde está parado el atacante).
                    ExecuteInstantEffect(active.Definition, active.Source, true);
                    active.PeriodRemaining = active.Definition.Period;
                }
            }

            if (active.IsExpired) (expired ??= new List<ActiveGameplayEffect>()).Add(active);
        }

        // Solo se aloca la lista si de verdad venció algo (antes se creaba una
        // lista nueva CADA frame, para todos los personajes).
        if (expired == null) return;

        foreach (var e in expired)
            RemoveActiveEffect(e);
    }

    // Lista en vivo de los efectos activos (buffs/debuffs/cooldowns). Solo
    // está poblada de verdad en la copia servidor — ver notas en
    // NetworkAbilitySystemComponent y UI_EffectContainer.
    public List<ActiveGameplayEffect> GetActiveEffects() => ActiveEffects;

    // Quita TODOS los efectos activos de una (ej: al cambiar de clase).
    public void RemoveAllActiveEffects()
    {
        for (int i = ActiveEffects.Count - 1; i >= 0; i--)
            RemoveActiveEffect(ActiveEffects[i]);
    }

    // Cura al atacante (sourceASC) un porcentaje del daño infligido, según
    // su stat LifeSteal. Solo aplica si el modificador bajó la Vida.
    private void HandleLifeSteal(Modifier mod, float magnitude, AbilitySystemComponent sourceASC)
    {
        if (mod.Attribute != EAttributeType.Health || magnitude >= 0 || sourceASC == null) return;
        float ls = sourceASC.GetAttributeValue(EAttributeType.LifeSteal);
        if (ls <= 0) return;
        float heal = Mathf.Abs(magnitude) * ls;
        float cur  = sourceASC.GetAttributeValue(EAttributeType.Health);
        float max  = sourceASC.GetAttributeValue(EAttributeType.MaxHealth);
        sourceASC.SetCurrentAttributeValue(EAttributeType.Health, Mathf.Clamp(cur + heal, 0, max));
    }

    // =========================================================
    // COOLDOWNS
    // =========================================================

    // Fracción (0 a 1) de cooldown restante para el efecto que tenga este
    // tag entre sus GrantedTags. La usa AbilityCooldownUI.
    public float GetCooldownRemainingNormalized(EGameplayTag tag)
    {
        foreach (var e in ActiveEffects)
            if (e.Definition.GrantedTags.Contains(tag) && e.Definition.Duration > 0)
                return e.DurationRemaining / e.TotalDuration;
        return 0f;
    }

    // Busca si el CooldownEffect de una habilidad está actualmente activo
    // y, si es así, cuánto falta y cuánto duraba en total.
    public bool GetCooldownStatus(GameplayAbility ability, out float timeRemaining, out float totalDuration)
    {
        timeRemaining = 0f; totalDuration = 0f;
        if (ability?.CooldownEffect == null) return false;
        foreach (var e in ActiveEffects)
        {
            if (e.Definition == ability.CooldownEffect)
            {
                timeRemaining = e.DurationRemaining;
                totalDuration = e.TotalDuration;
                return true;
            }
        }
        return false;
    }

    // Adelanta (reduce) el cooldown de cualquier efecto activo que tenga
    // este tag — lo usa la carga de ultimate al golpear con otras
    // habilidades (ver GameplayAbility.ChargeUltimate).
    public void ReduceCooldownByTag(EGameplayTag tag, float amount)
    {
        foreach (var e in ActiveEffects)
            if (e.Definition.GrantedTags.Contains(tag) && !e.IsExpired)
                e.DurationRemaining = Mathf.Max(0, e.DurationRemaining - amount);
    }

    // Chequea si el personaje tiene suficiente de cada atributo que un
    // CostEffect vaya a restarle (ej: maná suficiente). No descuenta nada,
    // solo valida — el descuento real lo hace ApplyGameplayEffect.
    public bool CanAffordGameplayEffect(GameplayEffect costEffect)
    {
        if (costEffect == null) return true;
        foreach (var mod in costEffect.Modifiers)
            if (mod.Type == Modifier.EModificationType.Add && mod.Magnitude < 0)
                if (Attributes.ContainsKey(mod.Attribute) && Attributes[mod.Attribute].CurrentValue < Mathf.Abs(mod.Magnitude))
                    return false;
        return true;
    }

    // =========================================================
    // HABILIDADES
    // =========================================================

    // Crea una instancia propia (clon) de una GameplayAbility y la agrega
    // a GrantedAbilities. Cada personaje necesita su propia instancia
    // porque una habilidad guarda estado (OwnerASC, cooldowns, etc.).
    public GameplayAbility GrantAbility(GameplayAbility template)
    {
        if (template == null) return null;
        GameplayAbility instance = Instantiate(template);
        instance.Initialize(this);
        instance.SourceTemplate = template; // para resolver su índice en GameplayAbilityRegistry
        GrantedAbilities.Add(instance);
        return instance;
    }

    // Vacía la lista de habilidades otorgadas (ej: antes de equipar una
    // clase nueva, para no arrastrar habilidades de la anterior).
    public void ClearGrantedAbilities() => GrantedAbilities.Clear();

    // Punto único para que una GameplayAbility (ScriptableObject, sin
    // MonoBehaviour propio) arranque una corutina usando este componente
    // como dueño.
    public void StartAbilityCoroutine(System.Collections.IEnumerator routine)
        => StartCoroutine(routine);

    // =========================================================
    // VIDA Y MUERTE
    // =========================================================

    // Marca al personaje como muerto (tag State_Dead) y notifica OnDeath.
    // La dispara SetCurrentAttributeValue cuando la Vida llega a 0.
    private void Die()
    {
        AddTag(EGameplayTag.State_Dead);
        OnDeath?.Invoke();
    }

    // Saca el tag de muerte y restaura la vida al máximo. La llama
    // NetworkGameManager al respawnear a un jugador.
    public void Revive()
    {
        // Olvidar quién nos mató la vida anterior: si después caemos a una
        // DeathZone o morimos por otra vía, no queremos darle la baja a alguien
        // que nos pegó hace rato.
        LastAttacker = null;

        // Empezar la vida nueva limpio: sin esto reaparecías con TODO lo que
        // tenías al morir, y un veneno/Heridas todavía activo te podía volver a
        // matar apenas spawneabas. Solo se van los DEBUFFS (EffectType.Debuff):
        // los buffs propios y los cooldowns de tus habilidades se conservan.
        RemoveAllDebuffs();

        RemoveTag(EGameplayTag.State_Dead);
        if (Attributes.ContainsKey(EAttributeType.MaxHealth))
            SetCurrentAttributeValue(EAttributeType.Health, Attributes[EAttributeType.MaxHealth].CurrentValue);
        OnRevive?.Invoke();
    }

    // =========================================================
    // EXPERIENCIA Y NIVEL
    // =========================================================

    // Suma experiencia y sube de nivel las veces que corresponda (soporta
    // ganar de golpe experiencia suficiente para varios niveles). No hace
    // nada si ya está en el nivel máximo.
    public void GainExperience(float amount)
    {
        if (!Attributes.ContainsKey(EAttributeType.Exp)) return;
        if (GetAttributeValue(EAttributeType.Level) >= MaxLevel) return;

        float newExp = GetAttributeValue(EAttributeType.Exp) + amount;
        float maxExp = GetAttributeValue(EAttributeType.MaxExp);

        while (newExp >= maxExp)
        {
            newExp -= maxExp;
            HandleLevelUp();
            if (GetAttributeValue(EAttributeType.Level) >= MaxLevel) { newExp = 0; OnMaxLevelReached?.Invoke(); break; }
            maxExp = Mathf.Round(maxExp * 1.5f);
            SetCurrentAttributeValue(EAttributeType.MaxExp, maxExp);
        }
        SetCurrentAttributeValue(EAttributeType.Exp, newExp);
    }

    // Aplica el crecimiento de stats de la clase actual (StatGrowthPerLevel),
    // sube el nivel, rellena Vida/Maná al máximo, y dispara OnLevelUp
    // (y OnMaxLevelReached si corresponde).
    private void HandleLevelUp()
    {
        float newLevel = GetAttributeValue(EAttributeType.Level) + 1;
        SetCurrentAttributeValue(EAttributeType.Level, newLevel);

        if (CurrentClass != null)
        {
            foreach (var growth in CurrentClass.StatGrowthPerLevel)
                if (Attributes.ContainsKey(growth.Attribute))
                    Attributes[growth.Attribute].BaseValue += growth.AmountPerLevel;
            RecalculateAllAttributes();
            SetCurrentAttributeValue(EAttributeType.Health, GetAttributeValue(EAttributeType.MaxHealth));
            SetCurrentAttributeValue(EAttributeType.Mana,   GetAttributeValue(EAttributeType.MaxMana));
        }

        OnLevelUp?.Invoke();
        if (newLevel >= MaxLevel) OnMaxLevelReached?.Invoke();
    }

    // Lleva al personaje HASTA 'targetLevel' aplicando, nivel por nivel, el mismo
    // crecimiento de stats que la experiencia (HandleLevelUp). Nunca baja de nivel.
    //
    // Existe para la progresión COMPARTIDA del modo Mercenarios: ahí el nivel es una
    // propiedad del EQUIPO (sale de una bolsa de experiencia común), así que el
    // servidor tiene que poder decir "este jugador va en el nivel 2" sin fabricar
    // experiencia individual. También es lo que hace que cambiar de clase no cueste
    // progreso: equipar una clase nueva reinicia al personaje a nivel 1, y el modo de
    // juego le vuelve a poner el nivel del equipo en el tick siguiente.
    //
    // Es idempotente: llamarla con un nivel que ya se tiene no hace nada.
    public void SetLevelTo(int targetLevel)
    {
        if (!Attributes.ContainsKey(EAttributeType.Level)) return;

        targetLevel = Mathf.Min(targetLevel, MaxLevel);

        // Tope de seguridad: si algo dejara el nivel sin subir, un while sobre un
        // atributo que no avanza congelaría el juego entero.
        int guard = 0;
        while (GetAttributeValue(EAttributeType.Level) < targetLevel && guard++ < 100)
            HandleLevelUp();
    }

    // Dispara OnMaxLevelReached a mano (sin pasar por GainExperience). Lo usa la
    // capa de red para hacer aparecer la selección de subclase en el cliente DUEÑO:
    // el nivel sube en el SERVIDOR, así que el ASC local del dueño remoto nunca se
    // enteraría — ver NetworkAbilitySystemComponent.TargetShowSubclassSelection.
    public void TriggerMaxLevelReached() => OnMaxLevelReached?.Invoke();

    // =========================================================
    // AFILIACIÓN / EQUIPOS
    // =========================================================

    // True si target es un objetivo válido para atacar: TeamID distinto
    // (o cualquiera de los dos en 0 = neutral, siempre hostil).
    public bool IsEnemyOf(AbilitySystemComponent target)
    {
        if (target == null || target == this) return false;
        if (TeamID == 0 || target.TeamID == 0) return true;
        return TeamID != target.TeamID;
    }

    // True si target es aliado (mismo TeamID, ninguno neutral).
    // includeSelf controla si uno mismo cuenta como aliado.
    public bool IsAllyOf(AbilitySystemComponent target, bool includeSelf = true)
    {
        if (target == null) return false;
        if (target == this) return includeSelf;
        if (TeamID == 0 || target.TeamID == 0) return false;
        return TeamID == target.TeamID;
    }

    // =========================================================
    // DAÑO SALIENTE (modificadores + críticos)
    // =========================================================

    // Modificadores de daño saliente que las pasivas de la clase registran acá
    // (ver IDamageModifier). Solo existen mientras la clase que los trae está
    // equipada: se registran/desregistran desde los PassiveBehaviorsPrefab. El core
    // no sabe qué es un backstab; solo recorre la lista y resuelve las banderas.
    private readonly List<IDamageModifier> _damageModifiers = new List<IDamageModifier>();

    public void RegisterDamageModifier(IDamageModifier modifier)
    {
        if (modifier != null && !_damageModifiers.Contains(modifier))
            _damageModifiers.Add(modifier);
    }

    public void UnregisterDamageModifier(IDamageModifier modifier)
    {
        _damageModifiers.Remove(modifier);
    }

    // Corre el pipeline de daño SALIENTE de ESTE personaje (el atacante) contra
    // 'target' y devuelve la magnitud final. Recorre los modificadores registrados
    // (que pueden mutar la magnitud y marcar críticos) y después resuelve las
    // banderas de crítico. La llama ExecuteInstantEffect sobre el ASC del atacante:
    // sourceASC.ResolveOutgoingDamage(this, magnitud, isPeriodicTick).
    //
    // Sobre las capas de crítico (con CritDamage = 2 y un golpe base de 6):
    //   1) "Es crítico" (x2 → 12): el backstab (por la espalda) y el crítico
    //      asegurado son dos formas de lo MISMO. NO se apilan entre sí.
    //   2) "Crítico mejorado" (otro x2): capa aparte que SÍ se acumula con la
    //      anterior (espalda + mejorado = x4 → 24).
    public float ResolveOutgoingDamage(AbilitySystemComponent target, float magnitude, bool isPeriodicTick)
    {
        DamageContext ctx = new DamageContext
        {
            Source         = this,
            Target         = target,
            IsPeriodicTick = isPeriodicTick,
            Magnitude      = magnitude,
        };

        for (int i = 0; i < _damageModifiers.Count; i++)
            _damageModifiers[i]?.ModifyOutgoingDamage(ref ctx);

        magnitude = ctx.Magnitude;

        // Crítico asegurado: mecánica GENERAL (cualquier habilidad puede otorgar el
        // tag, ej. Emboscada sombría), no de una clase puntual, así que se resuelve
        // acá y no en un modificador. Es otra forma de "es crítico" —no se apila con
        // el backstab— y se consume al usarlo, haya criteado o no. Los ticks de DoT
        // no lo consumen (no critean).
        bool isCrit = ctx.IsCrit;
        if (!isPeriodicTick && HasTag(EGameplayTag.Status_GuaranteedCrit))
        {
            isCrit = true;
            RemoveTag(EGameplayTag.Status_GuaranteedCrit);
        }

        // Resolución del crítico (regla core): cada capa marcada multiplica por
        // CritDamage. Por defecto x2 si la clase no configuró el atributo.
        if (isCrit || ctx.IsImprovedCrit)
        {
            float critDamage = GetAttributeValue(EAttributeType.CritDamage);
            if (critDamage < 1f) critDamage = 2f;
            if (isCrit)             magnitude *= critDamage;
            if (ctx.IsImprovedCrit) magnitude *= critDamage;
        }

        return magnitude;
    }

    // =========================================================
    // DAÑO ENTRANTE (bloqueo direccional)
    // =========================================================

    // Modificadores de daño ENTRANTE que registran las mecánicas de bloqueo de
    // ESTE personaje (ver IIncomingDamageModifier). Gemelo de _damageModifiers,
    // pero del lado del que recibe: existen mientras la barrera/postura esté
    // levantada, y se dan de baja al bajarla.
    private readonly List<IIncomingDamageModifier> _incomingDamageModifiers = new List<IIncomingDamageModifier>();

    public void RegisterIncomingDamageModifier(IIncomingDamageModifier modifier)
    {
        if (modifier != null && !_incomingDamageModifiers.Contains(modifier))
            _incomingDamageModifiers.Add(modifier);
    }

    public void UnregisterIncomingDamageModifier(IIncomingDamageModifier modifier)
    {
        _incomingDamageModifiers.Remove(modifier);
    }

    // Corre el pipeline de daño ENTRANTE sobre un golpe que va a recibir este
    // personaje, ANTES de las defensas (ver ExecuteInstantEffect).
    //
    // Recibe el daño ya separado en físico y mágico porque las etapas siguientes
    // los tratan distinto (la Defensa solo recorta el físico; el escudo cuesta el
    // doble contra el mágico). Pero un bloqueo frena "el golpe", no una de sus
    // componentes — así que el pipeline trabaja sobre el TOTAL y el resultado se
    // reparte de vuelta en la MISMA proporción, sin inventar ni perder la mezcla.
    private void ResolveIncomingDamage(AbilitySystemComponent sourceASC, ref float physicalDamage,
                                       ref float magicDamage, bool isPeriodicTick)
    {
        if (_incomingDamageModifiers.Count == 0) return;

        float total = physicalDamage + magicDamage;
        if (total <= 0f) return;

        IncomingDamageContext ctx = new IncomingDamageContext
        {
            Source         = sourceASC,
            Target         = this,
            IsPeriodicTick = isPeriodicTick,
            Magnitude      = -total,   // negativa = daño, igual que en el pipeline saliente
        };

        for (int i = 0; i < _incomingDamageModifiers.Count; i++)
            _incomingDamageModifiers[i]?.ModifyIncomingDamage(ref ctx);

        float remaining = Mathf.Max(0f, -ctx.Magnitude);
        if (Mathf.Approximately(remaining, total)) return;

        float ratio = remaining / total;
        physicalDamage *= ratio;
        magicDamage    *= ratio;
    }

    // Readout del Crítico mejorado para el HUD/nameplate. El estado y la lógica
    // viven en el modificador FirstStrikeCritModifier (en el prefab del Asesino);
    // acá solo lo buscamos y delegamos, para que el core no dependa de la clase más
    // que por este acceso opcional. Se cachea y se re-busca si el modificador
    // desaparece (cambio de clase → GetComponentInChildren vuelve a dar null).
    private FirstStrikeCritModifier _firstStrikeCache;
    private FirstStrikeCritModifier FirstStrike
    {
        get
        {
            if (_firstStrikeCache == null) _firstStrikeCache = GetComponentInChildren<FirstStrikeCritModifier>();
            return _firstStrikeCache;
        }
    }

    // ¿El Crítico mejorado está disponible (pasó su reutilización global)? En clases
    // sin la pasiva no hay modificador, así que es false.
    public bool IsFirstStrikeReady => FirstStrike != null && FirstStrike.IsReady;

    // ¿Le puedo clavar un Crítico mejorado a ESTE enemigo ahora? (reutilización
    // global + "frescura" del objetivo). Ver la nota de red en FirstStrikeCritModifier.
    public bool IsFirstStrikeReadyAgainst(AbilitySystemComponent target)
        => FirstStrike != null && FirstStrike.IsReadyAgainst(target);

    // =========================================================
    // BACKSTAB (helper posicional, lo usa BackstabDamageModifier)
    // =========================================================

    // Ángulo trasero a partir del cual un atacante cuenta como "por la espalda".
    // Dot(forward, dirHaciaAtacante) < este umbral ⇒ el atacante está detrás.
    // -0.25 ≈ arco trasero de ~150° (más permisivo que exactamente 180°).
    private const float BackstabDotThreshold = -0.25f;

    // True si 'attacker' está por detrás de ESTE personaje (usando hacia dónde
    // mira este objetivo, no el atacante). Lo usa BackstabDamageModifier (el Ataque
    // Furtivo del Pícaro) para decidir el crítico por la espalda.
    public bool IsBackstab(AbilitySystemComponent attacker)
    {
        if (attacker == null) return false;
        Vector3 toAttacker = attacker.transform.position - transform.position;
        toAttacker.y = 0;
        if (toAttacker.sqrMagnitude < 0.0001f) return false;
        return Vector3.Dot(transform.forward, toAttacker.normalized) < BackstabDotThreshold;
    }
}
