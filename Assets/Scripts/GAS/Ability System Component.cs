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

    // Tags de estado actualmente activos (ej: Stunned, State_Dead).
    protected HashSet<EGameplayTag> GameplayTags = new HashSet<EGameplayTag>();

    // Instancias de habilidades otorgadas a este personaje (una por cada
    // GameplayAbility de su clase). Modificar esta lista a mano rompe la
    // relación con los slots de PlayerController — usar GrantAbility().
    public List<GameplayAbility> GrantedAbilities = new List<GameplayAbility>();

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

    // Consulta si el personaje tiene un tag activo ahora mismo.
    public bool HasTag(EGameplayTag tag) => GameplayTags.Contains(tag);

    // Agrega un tag. Si no lo tenía ya, notifica OnTagAddedCallback.
    public void AddTag(EGameplayTag tag)
    {
        if (GameplayTags.Add(tag))
            OnTagAddedCallback?.Invoke(tag);
    }

    // Quita un tag. Si lo tenía, notifica OnTagRemovedCallback.
    public void RemoveTag(EGameplayTag tag)
    {
        if (GameplayTags.Remove(tag))
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
        if (type == EAttributeType.Health && val < 1f && HasTag(EGameplayTag.Status_Inmortal))
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

            ActiveGameplayEffect newEffect = new ActiveGameplayEffect(effect, finalDuration);
            ActiveEffects.Add(newEffect);
            ApplyEffectModifiers(effect, true);

            if (effect.GrantedTags != null)
                foreach (EGameplayTag tag in effect.GrantedTags) AddTag(tag);

            OnActiveEffectAddedCallback?.Invoke(effect, finalDuration);
        }
    }

    // Aplica de una vez los Modifiers de un efecto SIN duración (daño,
    // curación, etc.). Calcula escalado con stats del atacante, resuelve
    // el escudo antes que la vida, y dispara robo de vida si corresponde.
    private void ExecuteInstantEffect(GameplayEffect effect, object source = null)
    {
        AbilitySystemComponent sourceASC = source as AbilitySystemComponent;

        foreach (var mod in effect.Modifiers)
        {
            if (!Attributes.ContainsKey(mod.Attribute)) continue;

            float calculatedMagnitude = mod.Magnitude;

            if (mod.UseAttributeScaling && sourceASC != null)
                calculatedMagnitude += sourceASC.GetAttributeValue(mod.SourceAttribute) * mod.AttributeCoefficient;

            if (mod.Attribute == EAttributeType.Health && calculatedMagnitude < 0)
            {
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
    }

    // Suma/resta los modificadores Add/Multiply de un efecto CON duración
    // a los atributos afectados (apply=true al agregarlo, false al
    // quitarlo) y recalcula todo. No toca daño instantáneo.
    private void ApplyEffectModifiers(GameplayEffect effect, bool apply)
    {
        float sign = apply ? 1f : -1f;
        foreach (var mod in effect.Modifiers)
        {
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
                    ExecuteInstantEffect(active.Definition, null);
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
}
