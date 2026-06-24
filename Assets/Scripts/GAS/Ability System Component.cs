using UnityEngine;
using System.Collections.Generic;
using System;

// ============================================================
// ABILITY SYSTEM COMPONENT — VERSIÓN MULTIJUGADOR FINAL
//
// CAMBIOS respecto al original:
//
//   1. Se agregan tres eventos públicos que NetworkAbilitySystemComponent
//      usa para saber cuándo sincronizar algo a la red:
//
//        OnAttributeChangedCallback  → cuando cambia un atributo
//        OnTagAddedCallback          → cuando se agrega un tag
//        OnTagRemovedCallback        → cuando se quita un tag
//
//      Estos reemplazan los "hooks virtuales" del diseño anterior
//      que fallaba porque MonoBehaviour no puede heredar
//      métodos de NetworkBehaviour.
//
//   2. private → protected en Attributes, ActiveEffects, GameplayTags
//      (para compatibilidad futura, aunque ya no se heredan en red).
//
//   3. IsEnemyOf / IsAllyOf agregados como métodos públicos.
//
//   4. GainExperience es ahora public (para que el NetworkASC lo llame).
//
//   TODO LO DEMÁS: idéntico al original de singleplayer.
// ============================================================

public class AbilitySystemComponent : MonoBehaviour
{
    [Header("Configuración de Rol")]
    public AttributeSetDefinition CharacterRoleDefinition;

    [Header("Clase y Progresión")]
    public CharacterClassDefinition CurrentClass;

    [Header("Multijugador y Afiliación")]
    public int TeamID = 0;

    // =========================================================
    // EVENTOS PARA RED
    // NetworkAbilitySystemComponent se suscribe a estos en Awake()
    // =========================================================
    public event Action<EAttributeType, float> OnAttributeChangedCallback;
    public event Action<EGameplayTag>          OnTagAddedCallback;
    public event Action<EGameplayTag>          OnTagRemovedCallback;

    // =========================================================
    // EVENTOS DE JUEGO
    // =========================================================
    public event Action OnLevelUp;
    public event Action OnDeath;
    public event Action OnRevive;
    public event Action OnMaxLevelReached;

    public int MaxLevel = 3;
    private bool hasReachedMaxLevel = false;

    // =========================================================
    // ALMACENAMIENTO INTERNO
    // =========================================================
    protected Dictionary<EAttributeType, AttributeValue> Attributes    = new Dictionary<EAttributeType, AttributeValue>();
    protected List<ActiveGameplayEffect>                  ActiveEffects = new List<ActiveGameplayEffect>();
    protected HashSet<EGameplayTag>                       GameplayTags  = new HashSet<EGameplayTag>();

    public List<GameplayAbility> GrantedAbilities = new List<GameplayAbility>();

    // =========================================================
    // TAGS
    // =========================================================

    public bool HasTag(EGameplayTag tag) => GameplayTags.Contains(tag);

    public void AddTag(EGameplayTag tag)
    {
        if (GameplayTags.Add(tag))
            OnTagAddedCallback?.Invoke(tag);
    }

    public void RemoveTag(EGameplayTag tag)
    {
        if (GameplayTags.Remove(tag))
            OnTagRemovedCallback?.Invoke(tag);
    }

    // =========================================================
    // UNITY
    // =========================================================

    void Awake()
    {
        InitializeAttributes();
    }

    void Update()
    {
        ProcessActiveEffects(Time.deltaTime);
    }

    // =========================================================
    // EFECTOS
    // =========================================================

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
        }
    }

    private void RemoveActiveEffect(ActiveGameplayEffect effect)
    {
        ApplyEffectModifiers(effect.Definition, false);
        foreach (EGameplayTag tag in effect.Definition.GrantedTags) RemoveTag(tag);
        ActiveEffects.Remove(effect);
    }

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

            attr.CurrentValue = (attr.BaseValue + attr.AdditiveModifier) * attr.MultiplicativeModifier;
        }
    }

    private void Die()
    {
        AddTag(EGameplayTag.State_Dead);
        OnDeath?.Invoke();
    }

    public void Revive()
    {
        RemoveTag(EGameplayTag.State_Dead);
        if (Attributes.ContainsKey(EAttributeType.MaxHealth))
            SetCurrentAttributeValue(EAttributeType.Health, Attributes[EAttributeType.MaxHealth].CurrentValue);
        OnRevive?.Invoke();
    }

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
        {
            ApplyEffectModifiers(e.Definition, false);
            foreach (EGameplayTag tag in e.Definition.GrantedTags) RemoveTag(tag);
            ActiveEffects.Remove(e);
        }
    }

    // =========================================================
    // HABILIDADES
    // =========================================================

    public GameplayAbility GrantAbility(GameplayAbility template)
    {
        if (template == null) return null;
        GameplayAbility instance = Instantiate(template);
        instance.Initialize(this);
        GrantedAbilities.Add(instance);
        return instance;
    }

    public void ClearGrantedAbilities() => GrantedAbilities.Clear();

    // =========================================================
    // ATRIBUTOS
    // =========================================================

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

        hasReachedMaxLevel = false;
    }

    public float GetAttributeValue(EAttributeType type)
        => Attributes.ContainsKey(type) ? Attributes[type].CurrentValue : 0f;

    public void SetCurrentAttributeValue(EAttributeType type, float val)
    {
        if (type == EAttributeType.Health && val < 1f && HasTag(EGameplayTag.Status_Inmortal))
            val = 1f;

        if (Attributes.ContainsKey(type)) Attributes[type].CurrentValue = val;
        else Attributes[type] = new AttributeValue(val);

        // Notificar a NetworkAbilitySystemComponent para sincronizar
        OnAttributeChangedCallback?.Invoke(type, val);

        if (type == EAttributeType.Health && val <= 0 && !HasTag(EGameplayTag.State_Dead))
            Die();
    }

    public List<ActiveGameplayEffect> GetActiveEffects() => ActiveEffects;

    public float GetCooldownRemainingNormalized(EGameplayTag tag)
    {
        foreach (var e in ActiveEffects)
            if (e.Definition.GrantedTags.Contains(tag) && e.Definition.Duration > 0)
                return e.DurationRemaining / e.TotalDuration;
        return 0f;
    }

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

    public bool CanAffordGameplayEffect(GameplayEffect costEffect)
    {
        if (costEffect == null) return true;
        foreach (var mod in costEffect.Modifiers)
            if (mod.Type == Modifier.EModificationType.Add && mod.Magnitude < 0)
                if (Attributes.ContainsKey(mod.Attribute) && Attributes[mod.Attribute].CurrentValue < Mathf.Abs(mod.Magnitude))
                    return false;
        return true;
    }

    public void StartAbilityCoroutine(System.Collections.IEnumerator routine)
        => StartCoroutine(routine);

    public void UpgradeAttribute(EAttributeType type, float amount)
    {
        if (!Attributes.ContainsKey(type)) return;
        Attributes[type].BaseValue += amount;
        RecalculateAllAttributes();
        if (type == EAttributeType.MaxHealth) SetCurrentAttributeValue(EAttributeType.Health, GetAttributeValue(EAttributeType.Health) + amount);
        if (type == EAttributeType.MaxMana)   SetCurrentAttributeValue(EAttributeType.Mana,   GetAttributeValue(EAttributeType.Mana)   + amount);
    }

    public void ReduceCooldownByTag(EGameplayTag tag, float amount)
    {
        foreach (var e in ActiveEffects)
            if (e.Definition.GrantedTags.Contains(tag) && !e.IsExpired)
                e.DurationRemaining = Mathf.Max(0, e.DurationRemaining - amount);
    }

    public void RemoveAllActiveEffects()
    {
        for (int i = ActiveEffects.Count - 1; i >= 0; i--)
            RemoveActiveEffect(ActiveEffects[i]);
    }

    // =========================================================
    // EXPERIENCIA Y NIVEL
    // =========================================================

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

    // =========================================================
    // AFILIACIÓN
    // =========================================================

    public bool IsEnemyOf(AbilitySystemComponent target)
    {
        if (target == null || target == this) return false;
        if (TeamID == 0 || target.TeamID == 0) return true;
        return TeamID != target.TeamID;
    }

    public bool IsAllyOf(AbilitySystemComponent target, bool includeSelf = true)
    {
        if (target == null) return false;
        if (target == this) return includeSelf;
        if (TeamID == 0 || target.TeamID == 0) return false;
        return TeamID == target.TeamID;
    }

    // =========================================================
    // HELPERS
    // =========================================================

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
}