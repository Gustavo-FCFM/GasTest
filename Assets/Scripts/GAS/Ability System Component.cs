using UnityEngine;
using System.Collections.Generic;
using System;

public class AbilitySystemComponent : MonoBehaviour
{
    [Header("Configuración de Rol")]
    public AttributeSetDefinition CharacterRoleDefinition; // Atributos actuales del personaje

    [Header("Clase y Progresión")]
    public CharacterClassDefinition CurrentClass; // Clase actual del personaje
    // Eventos
    public event System.Action OnLevelUp; // Evento para avisar "Subí de nivel"
    public event System.Action OnDeath;      // Evento para avisar "Me morí"
    public event System.Action OnRevive;     // Evento para avisar "Volví"
    public int MaxLevel = 3; // Cap de nivel
    public event System.Action OnMaxLevelReached; // Evento para la UI
    private bool hasReachedMaxLevel = false; // Llegue al nivel maximo?

    // --- ALMACENAMIENTO ---
    private Dictionary<EAttributeType, AttributeValue> Attributes = new Dictionary<EAttributeType, AttributeValue>();

    // --- LISTAS ---
    public List<GameplayAbility> GrantedAbilities = new List<GameplayAbility>();
    private List<ActiveGameplayEffect> ActiveEffects = new List<ActiveGameplayEffect>();
    // ---Tags---
    private HashSet<EGameplayTag> GameplayTags = new HashSet<EGameplayTag>();
    public bool HasTag(EGameplayTag tag) => GameplayTags.Contains(tag);
    public void AddTag(EGameplayTag tag) => GameplayTags.Add(tag);
    public void RemoveTag(EGameplayTag tag) => GameplayTags.Remove(tag);
    

    void Awake()
    {
        InitializeAttributes();
    }

    void Update()
    {
        ProcessActiveEffects(Time.deltaTime);
    }

    // =========================================================
    // 1. GESTIÓN DE EFECTOS (APPLY)
    // =========================================================
    
    public void ApplyGameplayEffect(GameplayEffect effect, object source = null, float durationOverride = -1f)
    {
        if (effect == null) return;

        // A) Calculamos duración real
        float finalDuration = (durationOverride > 0) ? durationOverride : effect.Duration;

        // B) Si es <= 0, es INSTANTÁNEO
        if (finalDuration <= 0) 
        {
            ExecuteInstantEffect(effect, source);
        }
        // C) Si es > 0, es DURADERO
        else
        {
            // --- LÓGICA DE STACKING ---
            if (effect.StackingPolicy == GameplayEffect.EStackingType.Refresh)
            {
                // Buscamos si ya tenemos este efecto activo
                foreach (var existingEffect in ActiveEffects)
                {
                    if (existingEffect.Definition == effect)
                    {
                        // ¡Lo encontramos! Reiniciamos su reloj y salimos.
                        existingEffect.DurationRemaining = finalDuration;
                        Debug.Log($"Efecto {effect.name} refrescado.");
                        return; // IMPORTANTE: No agregamos uno nuevo
                    }
                }
            }
            else if (effect.StackingPolicy == GameplayEffect.EStackingType.Override)
            {
                // Borramos el anterior y ponemos el nuevo (Simplificado: removemos y seguimos)
                for (int i = ActiveEffects.Count - 1; i >= 0; i--)
                {
                    if (ActiveEffects[i].Definition == effect)
                    {
                        RemoveActiveEffect(ActiveEffects[i]);
                    }
                }
            }
            // ---------------------------

            // Si no era Refresh (o no existía), creamos uno nuevo
            ActiveGameplayEffect newEffect = new ActiveGameplayEffect(effect, finalDuration);
            ActiveEffects.Add(newEffect);
            
            ApplyEffectModifiers(effect, true);
            
            if (effect.GrantedTags != null)
            {
                foreach (EGameplayTag tag in effect.GrantedTags) AddTag(tag);
            }
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
            if (Attributes.ContainsKey(mod.Attribute))
            {
                float calculatedMagnitude = mod.Magnitude;
                if (mod.UseAttributeScaling && sourceASC != null)
                {
                    float sourceAttrValue = sourceASC.GetAttributeValue(mod.SourceAttribute);
                    calculatedMagnitude += sourceAttrValue * mod.AttributeCoefficient;
                }

                float currentValue = Attributes[mod.Attribute].CurrentValue;
                float newValue = CalculateModifiedValue(currentValue, mod, calculatedMagnitude);
                SetCurrentAttributeValue(mod.Attribute, newValue);
                
                HandleLifeSteal(mod, calculatedMagnitude, sourceASC);
            }
        }
    }

    // --- ESTE MÉTODO FUE EL QUE SE BORRÓ Y CAUSABA EL ERROR DE IMAGEN 1 ---
    private void ApplyEffectModifiers(GameplayEffect effect, bool apply)
    {
        float sign = apply ? 1f : -1f;

        foreach (var mod in effect.Modifiers)
        {
            // Solo aplicamos modificadores persistentes (Add/Multiply), no Override
            if (Attributes.TryGetValue(mod.Attribute, out AttributeValue attr))
            {
                switch (mod.Type)
                {
                    case Modifier.EModificationType.Add:
                        attr.AdditiveModifier += mod.Magnitude * sign;
                        break;
                    case Modifier.EModificationType.Multiply:
                        attr.MultiplicativeModifier += (mod.Magnitude - 1f) * sign;
                        break;
                }
            }
        }
        // Recalcular valores finales
        RecalculateAllAttributes();
    }

    private void RecalculateAllAttributes()
    {
        foreach (var pair in Attributes)
        {
            EAttributeType type = pair.Key;
            AttributeValue attr = pair.Value;

            // LISTA NEGRA: Estos atributos NO deben recalcularse desde la base.
            // Son valores de estado que cambian con el tiempo (Vida actual, Mana actual, Nivel, Exp).
            if (type == EAttributeType.Health || 
                type == EAttributeType.Mana || 
                type == EAttributeType.Energy || 
                type == EAttributeType.Exp || 
                type == EAttributeType.MaxExp ||
                type == EAttributeType.Level)    
            {
                // No hacemos nada, conservan el valor que tengan actualmente.
                continue;
            }
            
            // Para todo lo demás (Fuerza, Defensa, Ataque), sí recalculamos.
            attr.CurrentValue = (attr.BaseValue + attr.AdditiveModifier) * attr.MultiplicativeModifier;

            // Clamping (Topes)
            if (type == EAttributeType.Health && Attributes.ContainsKey(EAttributeType.MaxHealth))
            {
                float max = Attributes[EAttributeType.MaxHealth].CurrentValue;
                attr.CurrentValue = Mathf.Clamp(attr.CurrentValue, 0, max);
            }
            if (type == EAttributeType.Mana && Attributes.ContainsKey(EAttributeType.MaxMana))
            {
                float max = Attributes[EAttributeType.MaxMana].CurrentValue;
                attr.CurrentValue = Mathf.Clamp(attr.CurrentValue, 0, max);
            }
            /*if (Attributes.ContainsKey(EAttributeType.Health))
            {
                float currentHp = Attributes[EAttributeType.Health].CurrentValue;
                
                // Si tengo 0 vida y NO estoy muerto aún...
                if (currentHp <= 0 && !HasTag(EGameplayTag.State_Dead))
                {
                    Die();
                }
            }*/
        }
    }
    private void Die()
    {
        AddTag(EGameplayTag.State_Dead); // Marcar como muerto
        OnDeath?.Invoke(); // Avisar a PlayerController/NPC
        Debug.Log($"{gameObject.name} ha muerto.");
    }
    public void Revive()
    {
        // 1. Quitar estado de muerte
        RemoveTag(EGameplayTag.State_Dead);
        
        // 2. Restaurar vida al máximo
        if (Attributes.ContainsKey(EAttributeType.MaxHealth))
        {
            float maxHp = Attributes[EAttributeType.MaxHealth].CurrentValue;
            SetCurrentAttributeValue(EAttributeType.Health, maxHp);
        }
        
        OnRevive?.Invoke();
        Debug.Log($"{gameObject.name} ha revivido.");
    }
    // =========================================================
    // 2. GESTIÓN DE HABILIDADES (GRANT)
    // =========================================================
    public GameplayAbility GrantAbility(GameplayAbility abilityTemplate)
    {
        if (abilityTemplate == null) return null;
        GameplayAbility newInstance = Instantiate(abilityTemplate);
        newInstance.Initialize(this);
        GrantedAbilities.Add(newInstance);
        return newInstance;
    }

    public void ClearGrantedAbilities()
    {
        GrantedAbilities.Clear();
    }

    // =========================================================
    // 3. UTILIDADES Y PROCESOS
    // =========================================================
    private void ProcessActiveEffects(float deltaTime)
    {
        List<ActiveGameplayEffect> expiredEffects = new List<ActiveGameplayEffect>();

        foreach (var activeEffect in ActiveEffects)
        {
            // 1. Reducir Duración Global
            activeEffect.DurationRemaining -= deltaTime;

            // 2. LÓGICA PERIÓDICA (Veneno, Regeneración, etc.)
            if (activeEffect.Definition.Period > 0)
            {
                activeEffect.PeriodRemaining -= deltaTime;
                if (activeEffect.PeriodRemaining <= 0)
                {
                    // ¡TICK! Aplicamos el efecto de nuevo (Daño o Cura)
                    ExecuteInstantEffect(activeEffect.Definition, null); 
                    
                    // Reiniciamos el contador del intervalo
                    activeEffect.PeriodRemaining = activeEffect.Definition.Period;
                }
            }
            
            // 3. Chequear si expiró
            if (activeEffect.IsExpired) expiredEffects.Add(activeEffect);
        }

        // Limpieza...
        foreach (var expired in expiredEffects)
        {
            ApplyEffectModifiers(expired.Definition, false); 
            foreach (EGameplayTag tag in expired.Definition.GrantedTags) RemoveTag(tag);
            ActiveEffects.Remove(expired);
        }
    }

    public void InitializeAttributes()
    {
        if (CharacterRoleDefinition == null) return;

        // NO borramos el diccionario si ya tiene datos importantes (como el Nivel actual al cambiar de clase)
        // Pero para la inicialización inicial (Awake), sí limpiamos.
        // Por seguridad para tu nivel actual de desarrollo, usaremos Clear() y luego restauraremos valores si es necesario.
        
        // Guardamos valores temporales por si estamos cambiando de clase y queremos conservar el nivel
        float savedLevel = 1;
        float savedExp = 0;
        bool keepingProgress = Attributes.ContainsKey(EAttributeType.Level);

        if (keepingProgress)
        {
            savedLevel = GetAttributeValue(EAttributeType.Level);
            savedExp = GetAttributeValue(EAttributeType.Exp);
        }

        Attributes.Clear(); 

        // Cargamos desde el Asset
        foreach (var attrData in CharacterRoleDefinition.InitialAttributes)
        {
            if (!Attributes.ContainsKey(attrData.Attribute))
            {
                Attributes.Add(attrData.Attribute, new AttributeValue(attrData.BaseValue));
            }
            
            // Auto-crear Maximos
            if (attrData.Attribute == EAttributeType.Health) Attributes[EAttributeType.MaxHealth] = new AttributeValue(attrData.BaseValue);
            if (attrData.Attribute == EAttributeType.Mana) Attributes[EAttributeType.MaxMana] = new AttributeValue(attrData.BaseValue);
        }

        // Garantizar existencia de atributos de sistema (si no venían en el asset)
        if (!Attributes.ContainsKey(EAttributeType.Level)) Attributes.Add(EAttributeType.Level, new AttributeValue(1));
        if (!Attributes.ContainsKey(EAttributeType.Exp)) Attributes.Add(EAttributeType.Exp, new AttributeValue(0));
        if (!Attributes.ContainsKey(EAttributeType.MaxExp)) Attributes.Add(EAttributeType.MaxExp, new AttributeValue(100));

        // RESTAURAR PROGRESO (Para cuando cambies a Berserker no vuelvas a nivel 1)
        // Si el savedLevel es mayor al base (1), lo respetamos.
        if (keepingProgress && savedLevel > Attributes[EAttributeType.Level].CurrentValue)
        {
            Attributes[EAttributeType.Level].CurrentValue = savedLevel;
            Attributes[EAttributeType.Exp].CurrentValue = savedExp;
            // Recalcular MaxExp para ese nivel sería ideal, pero por ahora con esto basta.
        }
        
        // Resetear flag de evento
        hasReachedMaxLevel = false;
    }

    // --- GETTERS Y SETTERS ---
    public float GetAttributeValue(EAttributeType type) => Attributes.ContainsKey(type) ? Attributes[type].CurrentValue : 0f;
   public void SetCurrentAttributeValue(EAttributeType type, float val) 
    { 
        // 1. Guardar el valor
        if(Attributes.ContainsKey(type)) 
        {
            Attributes[type].CurrentValue = val; 
        }
        else
        {
            Attributes.Add(type, new AttributeValue(val));
        }

        // 2. CHEQUEO DE MUERTE INMEDIATO
        // Si acabamos de modificar la Vida y llegó a 0...
        if (type == EAttributeType.Health)
        {
            if (val <= 0 && !HasTag(EGameplayTag.State_Dead))
            {
                Die();
            }
        }
    }
    public List<ActiveGameplayEffect> GetActiveEffects() => ActiveEffects;
    

    
    public float GetCooldownRemainingNormalized(EGameplayTag tag)
    {
        foreach (var activeEffect in ActiveEffects)
        {
            if (activeEffect.Definition.GrantedTags.Contains(tag) && activeEffect.Definition.Duration > 0)
            {
                return activeEffect.DurationRemaining / activeEffect.TotalDuration;
            }
        }
        return 0f;
    }
    
    // Método nuevo para la UI más avanzada (UI_AbilitySlot)
    public bool GetCooldownStatus(GameplayAbility ability, out float timeRemaining, out float totalDuration)
    {
        timeRemaining = 0f; totalDuration = 0f;
        if (ability == null || ability.CooldownEffect == null) return false;

        foreach (var activeEffect in ActiveEffects)
        {
            if (activeEffect.Definition == ability.CooldownEffect)
            {
                timeRemaining = activeEffect.DurationRemaining;
                totalDuration = activeEffect.TotalDuration; 
                return true;
            }
        }
        return false;
    }
    public void ReduceCooldownByTag(EGameplayTag cooldownTag, float amount)
    {
        foreach (var activeEffect in ActiveEffects)
        {
            // Si el efecto tiene el tag (ej: "Ability.Cooldown.Ultimate") y NO ha expirado
            if (activeEffect.Definition.GrantedTags.Contains(cooldownTag) && !activeEffect.IsExpired)
            {
                activeEffect.DurationRemaining -= amount;
                
                // Opcional: Feedback visual si baja de golpe
                if (activeEffect.DurationRemaining < 0) activeEffect.DurationRemaining = 0;
                
                Debug.Log($"Cooldown {cooldownTag} reducido en {amount}s. Restan: {activeEffect.DurationRemaining}");
            }
        }
    }

    // Experiencia y Nivel
    public void GainExperience(float amount)
    {
        // 1. Si no tengo stat de experiencia o ya llegué al máximo, no hago nada.
        if (!Attributes.ContainsKey(EAttributeType.Exp)) return;
        if (hasReachedMaxLevel) return; // <--- Freno total si ya somos nivel máximo

        float currentLevel = GetAttributeValue(EAttributeType.Level);
        
        // Seguridad extra: Si ya somos nivel 3, nos aseguramos de marcar el flag y salir
        if (currentLevel >= MaxLevel)
        {
            hasReachedMaxLevel = true;
            return;
        }

        float currentExp = GetAttributeValue(EAttributeType.Exp);
        float maxExp = GetAttributeValue(EAttributeType.MaxExp);
        
        float newExp = currentExp + amount;

        // Bucle de subida de nivel
        while (newExp >= maxExp)
        {
            newExp -= maxExp;
            
            // Subimos de nivel
            HandleLevelUp();

            // Verificamos si ALCANZAMOS el tope en esta iteración
            if (GetAttributeValue(EAttributeType.Level) >= MaxLevel)
            {
                newExp = 0; // Opcional: Limpiar exp sobrante
                hasReachedMaxLevel = true;
                Debug.Log("Nivel Máximo Alcanzado. Evolución disponible.");
                OnMaxLevelReached?.Invoke(); // <--- Disparamos evento para activar el selector
                break; // Rompemos el bucle inmediatamente
            }

            // Si no es el tope, aumentamos la dificultad del siguiente nivel
            maxExp = Mathf.Round(maxExp * 1.5f); 
            SetCurrentAttributeValue(EAttributeType.MaxExp, maxExp);
        }

        SetCurrentAttributeValue(EAttributeType.Exp, newExp);
    }

    private void HandleLevelUp()
    {
        // Subir nivel
        float currentLevel = GetAttributeValue(EAttributeType.Level);
        float newLevel = currentLevel + 1;
        SetCurrentAttributeValue(EAttributeType.Level, newLevel);
        Debug.Log($"¡SUBIDA DE NIVEL! Ahora eres nivel {newLevel}");
        // Aplicar mejoras de stats
        if (CurrentClass != null && newLevel <= MaxLevel)
        {
            foreach (var growth in CurrentClass.StatGrowthPerLevel)
            {
                if (Attributes.ContainsKey(growth.Attribute))
                {
                    Attributes[growth.Attribute].BaseValue += growth.AmountPerLevel;
                }
            }
            RecalculateAllAttributes();
            // Restaurar vida/mana al subir
            SetCurrentAttributeValue(EAttributeType.Health, GetAttributeValue(EAttributeType.MaxHealth));
            SetCurrentAttributeValue(EAttributeType.Mana, GetAttributeValue(EAttributeType.MaxMana));
        }
        OnLevelUp?.Invoke();
        if (newLevel >= MaxLevel)
        {
            Debug.Log("¡NIVEL MÁXIMO ALCANZADO! Desbloqueando Subclase...");
            OnMaxLevelReached?.Invoke();
        }
    }

    // --- MATH HELPERS ---
    private float CalculateModifiedValue(float current, Modifier mod, float magnitude)
    {
        switch (mod.Type)
        {
            case Modifier.EModificationType.Add: return current + magnitude;
            case Modifier.EModificationType.Multiply: return current * magnitude;
            case Modifier.EModificationType.Override: return magnitude;
            default: return current;
        }
    }

    private void HandleLifeSteal(Modifier mod, float magnitude, AbilitySystemComponent sourceASC)
    {
        if (mod.Attribute == EAttributeType.Health && magnitude < 0 && sourceASC != null)
        {
            float lifesteal = sourceASC.GetAttributeValue(EAttributeType.LifeSteal);
            if (lifesteal > 0)
            {
                float heal = Mathf.Abs(magnitude) * lifesteal;
                float cur = sourceASC.GetAttributeValue(EAttributeType.Health);
                float max = sourceASC.GetAttributeValue(EAttributeType.MaxHealth);
                sourceASC.SetCurrentAttributeValue(EAttributeType.Health, Mathf.Clamp(cur + heal, 0, max));
            }
        }
    }
    
    // Método para verificar costes de Maná/Energía
    public bool CanAffordGameplayEffect(GameplayEffect costEffect)
    {
        if (costEffect == null) return true; 

        foreach (var mod in costEffect.Modifiers)
        {
            if (mod.Type == Modifier.EModificationType.Add && mod.Magnitude < 0)
            {
                float costAmount = Mathf.Abs(mod.Magnitude); 
                if (Attributes.ContainsKey(mod.Attribute))
                {
                    if (Attributes[mod.Attribute].CurrentValue < costAmount) return false;
                }
                // Si no tiene el atributo, asumimos que es gratis (regla del Bárbaro)
            }
        }
        return true;
    }
    // Helper para que las habilidades puedan correr corutinas (secuencias)
    public void StartAbilityCoroutine(System.Collections.IEnumerator routine)
    {
        StartCoroutine(routine);
    }
}