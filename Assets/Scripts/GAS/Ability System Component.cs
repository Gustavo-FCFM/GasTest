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

    // =========================================================
    // ALMACENAMIENTO INTERNO
    // =========================================================

    // Valor actual de cada atributo del personaje (vida, ataque, etc.).
    protected Dictionary<EAttributeType, AttributeValue> Attributes = new Dictionary<EAttributeType, AttributeValue>();

    // Efectos con duración actualmente aplicados (buffs, debuffs,
    // cooldowns). Solo existe poblado de verdad en la copia servidor —
    // ver notas en NetworkAbilitySystemComponent.
    protected List<ActiveGameplayEffect> ActiveEffects = new List<ActiveGameplayEffect>();

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
    public void InitializeAttributes()
    {
        if (CharacterRoleDefinition == null) return;

        float savedLevel = 1, savedExp = 0;
        bool keepProgress = Attributes.ContainsKey(EAttributeType.Level);
        if (keepProgress) { savedLevel = GetAttributeValue(EAttributeType.Level); savedExp = GetAttributeValue(EAttributeType.Exp); }

        Attributes.Clear();

        foreach (var attrData in CharacterRoleDefinition.InitialAttributes)
        {
            if (!Attributes.ContainsKey(attrData.Attribute))
                Attributes.Add(attrData.Attribute, new AttributeValue(attrData.BaseValue));
            if (attrData.Attribute == EAttributeType.Health)
                Attributes[EAttributeType.MaxHealth] = new AttributeValue(attrData.BaseValue);
            if (attrData.Attribute == EAttributeType.Mana)
                Attributes[EAttributeType.MaxMana] = new AttributeValue(attrData.BaseValue);
        }

        if (!Attributes.ContainsKey(EAttributeType.Level))  Attributes[EAttributeType.Level]  = new AttributeValue(1);
        if (!Attributes.ContainsKey(EAttributeType.Exp))    Attributes[EAttributeType.Exp]    = new AttributeValue(0);
        if (!Attributes.ContainsKey(EAttributeType.MaxExp)) Attributes[EAttributeType.MaxExp] = new AttributeValue(100);

        if (keepProgress && savedLevel > 1)
        {
            Attributes[EAttributeType.Level].CurrentValue = savedLevel;
            Attributes[EAttributeType.Exp].CurrentValue   = savedExp;
        }
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

        if (Attributes.ContainsKey(type)) Attributes[type].CurrentValue = val;
        else Attributes[type] = new AttributeValue(val);

        OnAttributeChangedCallback?.Invoke(type, val);

        if (type == EAttributeType.Health && val <= 0 && !HasTag(EGameplayTag.State_Dead))
            Die();
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
    private void RecalculateAllAttributes()
    {
        foreach (var pair in Attributes)
        {
            EAttributeType type = pair.Key;
            AttributeValue attr = pair.Value;

            if (type == EAttributeType.Health  || type == EAttributeType.Mana   ||
                type == EAttributeType.Energy  || type == EAttributeType.Exp    ||
                type == EAttributeType.MaxExp  || type == EAttributeType.Level  ||
                type == EAttributeType.Shield) continue;

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

        float finalDuration = (durationOverride > 0) ? durationOverride : effect.Duration;

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
                // Tope de acumulaciones: al llegar al máximo no agregamos otra, sino
                // que refrescamos la que está por expirar — así seguir golpeando
                // mantiene la acumulación viva sin pasarse del límite.
                int stacks = 0;
                ActiveGameplayEffect soonest = null;
                foreach (var existing in ActiveEffects)
                {
                    if (existing.Definition != effect) continue;
                    stacks++;
                    if (soonest == null || existing.DurationRemaining < soonest.DurationRemaining)
                        soonest = existing;
                }

                if (stacks >= effect.MaxStacks)
                {
                    if (soonest != null)
                    {
                        soonest.DurationRemaining = finalDuration;
                        soonest.TotalDuration     = finalDuration;
                        OnActiveEffectAddedCallback?.Invoke(effect, finalDuration);
                    }
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
                foreach (EGameplayTag tag in effect.GrantedTags) AddTag(tag);

            OnActiveEffectAddedCallback?.Invoke(effect, finalDuration);
        }
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

        return magnitude;
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

        foreach (var mod in effect.Modifiers)
        {
            if (!Attributes.ContainsKey(mod.Attribute)) continue;

            float calculatedMagnitude = CalculateBaseMagnitude(mod, sourceASC);

            // Críticos (backstab, asegurado, primer golpe). Ver ResolveCritMultiplier.
            calculatedMagnitude *= ResolveCritMultiplier(mod, calculatedMagnitude, sourceASC, isPeriodicTick);

            if (mod.Attribute == EAttributeType.Health && calculatedMagnitude < 0)
            {
                wasDamagingHit = true;

                // Registrar al atacante para atribuir la baja (EXP al matador). Se
                // guarda antes de resolver escudo/vida: aunque este golpe no sea el
                // letal, deja anotado quién fue el último en pegar.
                if (sourceASC != null && !ReferenceEquals(sourceASC, this))
                    LastAttacker = sourceASC;

                float physicalDamage = Mathf.Abs(calculatedMagnitude);
                float magicDamage    = sourceASC != null ? sourceASC.GetAttributeValue(EAttributeType.MagicDamage) : 0f;
                float currentShield  = GetAttributeValue(EAttributeType.Shield);

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

                calculatedMagnitude = -(physicalDamage + magicDamage);
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
                sourceASC.BreakInvisibility(); // el atacante se delata al golpear
        }
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

            if (!Attributes.TryGetValue(mod.Attribute, out AttributeValue attr)) continue;
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
        List<ActiveGameplayEffect> expired = new List<ActiveGameplayEffect>();

        foreach (var active in ActiveEffects)
        {
            active.DurationRemaining -= deltaTime;

            if (active.Definition.Period > 0)
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

            if (active.IsExpired) expired.Add(active);
        }

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

    // Dispara OnMaxLevelReached a mano (sin pasar por GainExperience). Lo usa
    // el cheat de subir de nivel para hacer aparecer la selección de subclase
    // en el cliente DUEÑO — ver NetworkAbilitySystemComponent.ServerCheatMaxLevel.
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
    // CRÍTICOS
    // =========================================================

    [Header("Crítico Mejorado (Asesino)")]
    [Tooltip("Ventana por objetivo: un enemigo vuelve a estar 'fresco' para un crítico mejorado " +
             "si no lo golpeás en estos segundos.")]
    public float FirstStrikeWindow = 6f;
    [Tooltip("Reutilización global: mínimo de segundos entre dos críticos mejorados (contra cualquiera).")]
    public float FirstStrikeCooldown = 2f;

    // Última vez que ESTE personaje golpeó a cada enemigo (para el Crítico mejorado).
    private readonly Dictionary<AbilitySystemComponent, float> _lastStrikeTime
        = new Dictionary<AbilitySystemComponent, float>();
    // Última vez que disparó un crítico de "Crítico mejorado" (su cooldown).
    private float _lastFirstStrikeCrit = -999f;

    // Multiplicador de daño crítico de un golpe. Hay DOS capas independientes que
    // sí se acumulan ENTRE SÍ (con CritDamage = 2 y un golpe base de 6):
    //
    //   1) "Es crítico" (x2 → 12). El Ataque Furtivo (por la espalda) y el crítico
    //      asegurado (ej. Emboscada sombría) son dos formas de lo MISMO: hacer que
    //      el golpe sea crítico. NO se duplican entre sí — pegar por la espalda
    //      estando en Emboscada sigue siendo 12, no 24.
    //
    //   2) "Crítico mejorado" (otro x2). Es una capa APARTE que se suma a lo
    //      anterior: por enfrente y sin nada más son 12; combinado con un crítico
    //      (espalda o asegurado) son 24.
    //
    // Devuelve 1 (sin crítico) para curaciones, ticks de DoT o sin atacante.
    private float ResolveCritMultiplier(Modifier mod, float magnitude, AbilitySystemComponent sourceASC, bool isPeriodicTick)
    {
        // Los ticks de una DoT no critean (sería raro que una herida pegue más
        // fuerte según dónde está parado el atacante).
        if (isPeriodicTick || sourceASC == null) return 1f;
        if (mod.Attribute != EAttributeType.Health || magnitude >= 0) return 1f;

        float critDamage = sourceASC.GetAttributeValue(EAttributeType.CritDamage);
        if (critDamage < 1f) critDamage = 2f; // por defecto x2 si la clase no configuró CritDamage

        // Capa 1: ¿es crítico? (espalda o asegurado — da igual cuál, no se suman)
        bool isCrit = sourceASC.HasTag(EGameplayTag.Passive_Backstab) && IsBackstab(sourceASC);

        // El crítico asegurado se consume igual, haya sido crítico o no: era "su
        // PRIMER ataque". Por eso, en un dash que atraviesa a varios, solo el
        // primero lo recibe (ver el orden del trayecto en GA_Dash). Quitar el tag
        // a mano es seguro aunque lo haya otorgado un GE: al expirar, su RemoveTag
        // es un no-op.
        if (sourceASC.HasTag(EGameplayTag.Status_GuaranteedCrit))
        {
            isCrit = true;
            sourceASC.RemoveTag(EGameplayTag.Status_GuaranteedCrit);
        }

        // Capa 2: Crítico mejorado (Asesino), independiente de la anterior.
        bool improvedCrit = sourceASC.ConsumeFirstStrikeCrit(this);

        float multiplier = 1f;
        if (isCrit)       multiplier *= critDamage;
        if (improvedCrit) multiplier *= critDamage;

        return multiplier;
    }

    // ¿Este personaje puede pegarle un "Crítico mejorado" a 'target' ahora? Marca
    // el golpe (para la ventana por objetivo) y consume el cooldown si aplica.
    // La llama ResolveCritMultiplier sobre el ATACANTE.
    private bool ConsumeFirstStrikeCrit(AbilitySystemComponent target)
    {
        if (target == null || !HasTag(EGameplayTag.Passive_FirstStrikeCrit)) return false;

        float now = Time.time;
        bool  isFirstStrike = !_lastStrikeTime.TryGetValue(target, out float last) ||
                              (now - last) >= FirstStrikeWindow;

        _lastStrikeTime[target] = now;

        if (!isFirstStrike) return false;
        if (now - _lastFirstStrikeCrit < FirstStrikeCooldown) return false;

        _lastFirstStrikeCrit = now;
        return true;
    }

    // ¿El Crítico mejorado está disponible (pasó su reutilización global)? Lo usa
    // el feedback visual del HUD. Refleja la reutilización, no la "frescura" de un
    // enemigo puntual (eso depende de a quién le pegues). Solo tiene sentido en un
    // personaje con la pasiva; para el resto siempre es false.
    public bool IsFirstStrikeReady =>
        HasTag(EGameplayTag.Passive_FirstStrikeCrit) &&
        (Time.time - _lastFirstStrikeCrit >= FirstStrikeCooldown);

    // ¿Le puedo clavar un Crítico mejorado a ESTE enemigo ahora? Suma la
    // reutilización global (arriba) más la "frescura" del objetivo puntual: que no
    // lo hayas golpeado en FirstStrikeWindow. Lo usa el nameplate del enemigo para
    // avisarte que ya podés atacarlo con el golpe potenciado.
    //
    // NOTA DE RED: _lastStrikeTime solo se llena en el SERVIDOR (donde se aplica el
    // daño). En el host es exacto; en un cliente remoto la "frescura" no la conoce,
    // así que ahí este chequeo cae en la disponibilidad global (mismo que
    // IsFirstStrikeReady). Suficiente para el aviso visual.
    public bool IsFirstStrikeReadyAgainst(AbilitySystemComponent target)
    {
        if (target == null || !IsFirstStrikeReady) return false;
        return !_lastStrikeTime.TryGetValue(target, out float last) ||
               (Time.time - last >= FirstStrikeWindow);
    }

    // Ángulo trasero a partir del cual un atacante cuenta como "por la espalda".
    // Dot(forward, dirHaciaAtacante) < este umbral ⇒ el atacante está detrás.
    // -0.25 ≈ arco trasero de ~150° (más permisivo que exactamente 180°).
    private const float BackstabDotThreshold = -0.25f;

    // True si 'attacker' está por detrás de ESTE personaje (usando hacia dónde
    // mira este objetivo, no el atacante). Lo usa el Ataque Furtivo del Pícaro
    // en ExecuteInstantEffect para decidir el crítico por la espalda.
    public bool IsBackstab(AbilitySystemComponent attacker)
    {
        if (attacker == null) return false;
        Vector3 toAttacker = attacker.transform.position - transform.position;
        toAttacker.y = 0;
        if (toAttacker.sqrMagnitude < 0.0001f) return false;
        return Vector3.Dot(transform.forward, toAttacker.normalized) < BackstabDotThreshold;
    }
}
