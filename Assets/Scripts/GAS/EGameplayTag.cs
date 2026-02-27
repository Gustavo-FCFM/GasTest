public enum EGameplayTag
{
    None,
    
    
    // --- ESTADOS DE CONTROL (CC) ---
    State_Stunned,   // Bloquea todo
    State_Rooted,    // Bloquea movimiento
    State_Silenced,  // Bloquea habilidades (PROXIMAMENTE)
    State_Dead,      // Estado de muerte
    
    // --- COOLDOWNS ---
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
    Status_Immunity,      // Ej: Invencible
    Status_Rage,
    Status_Frenzy,
    Status_Inmortal
}