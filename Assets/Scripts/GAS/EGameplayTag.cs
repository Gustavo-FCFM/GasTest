// ============================================================
// EGameplayTag
//
// Etiquetas de estado que un AbilitySystemComponent puede tener
// (aturdido, en cooldown, envenenado, etc.). Los GameplayEffect las
// otorgan/quitan mientras están activos, y CanActivate()/HasTag()
// las usan para bloquear acciones o consultar estado. También se
// usan como "identidad" de un efecto (ver GrantedTags[0] en
// GameplayAbility.CanActivate y NetworkAbilitySystemComponent) para
// poder referenciarlo sin mandar el ScriptableObject por red.
// ============================================================
public enum EGameplayTag
{
    None,

    // --- ESTADOS DE CONTROL (CC) ---
    State_Stunned,   // Bloquea todo (movimiento y habilidades)
    State_Rooted,    // Bloquea solo el movimiento
    State_Silenced,  // Bloquea habilidades (movimiento permitido)
    State_Dead,      // Estado de muerte

    // --- COOLDOWNS (uno por slot de habilidad) ---
    Ability_Cooldown_Global,
    Ability_Cooldown_Melee,
    Ability_Cooldown_Ultimate,
    Ability_Cooldown_0,
    Ability_Cooldown_Ranged,
    Ability_Cooldown_Special,
    Ability_Cooldown_Extra,
    Ability_Cooldown_Movement,

    // --- EFECTOS DE ESTADO (BUFFS/DEBUFFS) ---
    Status_Poison,
    Status_Burning,
    Status_Slow,
    Status_Buff_Damage,  // Ej: Grito de guerra
    Status_Buff_Speed,   // Ej: Sprint
    Status_Immunity,     // Ej: Invencible
    Status_Rage,
    Status_Frenzy,
    Status_Inmortal,
    Status_Buff_Bear,
    Status_Buff_Wolf,
    Status_Buff_Eagle,
    Status_Buff_Tiger,

    // --- COOLDOWNS DE TÓTEMS ---
    Totem_Cooldown_Bear,
    Totem_Cooldown_Wolf,
    Totem_Cooldown_Eagle,
    Totem_Cooldown_Tiger,

    // IMPORTANTE: agregar tags nuevos SIEMPRE al final. Los .asset serializan los
    // tags por su NÚMERO de enum; insertarlos en el medio corre los índices y
    // rompe las referencias ya guardadas (ej. Status_Inmortal, Rage, tótems).
    Status_Wound,      // Heridas del Pícaro — daño con el tiempo, apilable (ver GE_Heridas)
    Passive_Backstab,  // Pícaro: golpear por la espalda hace daño crítico (ver ExecuteInstantEffect)
}
