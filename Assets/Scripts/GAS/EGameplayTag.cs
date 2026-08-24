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
    Status_Immortal,
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
    // rompe las referencias ya guardadas (ej. Status_Immortal, Rage, tótems).
    Status_Wound,      // Heridas del Pícaro — daño con el tiempo, apilable (ver GE_Heridas)
    Passive_Backstab,  // OBSOLETO: el backstab ahora es BackstabDamageModifier (pasiva por prefab). No borrar (corre índices).

    // --- ASESINO ---
    Status_Invisible,        // Invisible para los ENEMIGOS (ver PlayerVisibility)
    Status_GuaranteedCrit,   // El próximo golpe es crítico sí o sí; se CONSUME al usarlo (ver ResolveOutgoingDamage)
    Passive_FirstStrikeCrit, // OBSOLETO: el crítico mejorado ahora es FirstStrikeCritModifier (pasiva por prefab). No borrar (corre índices).

    // --- ILUSIONISTA ---
    Passive_Illusory_Blades,  // OBSOLETO: las cuchillas ahora son IllusoryBladesPassive (pasiva por prefab). No borrar (corre índices).
    Status_Blinded,          // Cegado ("flashbang"): lo aplica la Copia exacta al enemigo que la golpea (ver Entity_PlayerCopy)

    // --- PIRATA ---
    Status_Unstoppable,      // Imparable: limpia los debuffs al otorgarse y bloquea nuevos debuffs con duración (CC/DoT) mientras dura (ver ApplyGameplayEffect)
    Status_Gambled,          // Apostar: el enemigo sobre el que el Pirata apostó. Recibe más daño DEL PIRATA (ver GamblePassive)

    // --- PALADÍN ---
    Status_Blocking,         // Escudo levantado (GA_ShieldBlock). Dura lo que el jugador mantenga el botón; lo usan la animación de hold y el VFX de la barrera
    Status_Divine_Smite,      // Castigo divino cargado: el próximo ataque principal se cambia por el combo cono+estela (ver GA_TagSwitch)
    Status_Aura_Protection,  // El personaje está dentro del Aura de protección de un Paladín aliado (marca para VFX; el stat lo da el GE del aura)
    Status_Aura_Devotion,

    // --- PALADÍN · JURAMENTO DE LA VENGANZA ---
    Status_Aura_Vengeance,   // El enemigo está dentro del Aura de venganza (marca para VFX; la Vulnerabilidad la da el GE del aura)
    Status_Sworn_Enemy,       // Enemigo jurado: recibe más daño y CURA a los aliados que lo golpean (ver GA_SwornEnemy)
    Status_Avenging_Angel,    // Ángel vengador activo: cambia el ataque principal (vía GA_TagSwitch) y habilita los anillos de aura condicionales

    // --- MOVIMIENTO ---
    Status_Feather_Fall,      // Caída de pluma: cae más lento y puede volver a impulsarse en el aire sin límite mientras dure, como un aleteo (ver PlayerController.HandleMovementInput). NO es volar libremente — eso sería un Status_Flight aparte, con control vertical propio

    // --- DEFENSIVOS COMPARTIDOS ---
    Status_HealShield,       // Mientras dure, lo que el ESCUDO frena se devuelve como vida (Cubrir con escudo de la Conquista, defensa mejorada del Monje). Ver ExecuteInstantEffect
    Status_Invincible_Conqueror, // Invencible de la definitiva del Juramento de la conquista
    Status_Aura_Conqueror,       // El personaje está dentro del Aura de la Conquista de un Paladín aliado (marca para VFX; el stat lo da el GE del aura)

    // --- MODO MERCENARIOS ---
    Status_Carrying_Objective, // Lleva el Objetivo encima: se mueve más lento y su botón de definitiva pasa a SOLTARLO (ver MercObjective)
    Status_SafeZone,           // Está dentro de la sala segura de SU equipo: vida topeada, inmune al daño, y es el único lugar donde puede cambiar de clase (ver MercTeamBase)
}
