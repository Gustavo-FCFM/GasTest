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
    Status_Invisible,        // Invisible para los ENEMIGOS; vos te ves fantasma (ver PlayerVisibility)
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

    // --- CANALIZADOS ---
    Status_Channeling,         // Está en medio de una habilidad canalizada (el molinete del bárbaro). Mientras dure, CanActivate bloquea TODA otra habilidad salvo las marcadas con UsableWhileChanneling. Lo pone y lo saca GA_ContinuousAoE con BlockOtherAbilities

    State_Disarmed,            // Desarmado: no puede usar las acciones de ARMA (golpear con ella o lanzarla). Lo bloquea el ActivationBlockedTags de cada habilidad de arma (desde el 1 de octubre; antes era solo el ataque básico). Lo da la Zona de verdad del Clérigo del Orden; lo va a reusar el Guerrero

    State_AlwaysVisible,       // Revelado: aunque tenga Status_Invisible, los ENEMIGOS lo ven igual (modelo, barra de vida, números). Ver ASC.IsHiddenFromEnemies. Lo da el Faro de esperanza del Clérigo de la Luz a los enemigos dentro de su aura

    // --- POSTURAS DEL GUERRERO ---
    Stance_Defensive,          // Postura defensiva del Guerrero (+armadura). La da GE_StanceDefensive; con ella el Shift es la Carga defensiva
    Stance_Offensive,          // Postura ofensiva del Guerrero (+ataque). La da GE_StanceOffensive; con ella el Shift es la Carga ofensiva
    Status_Stance,             // No lo otorga nadie: es el EffectGroup de las dos posturas (con ReplacesGroup, entrar a una saca la otra)

    // --- MAESTRO DE BATALLA ---
    Status_BreakLimits,        // Romper límites activo: Punto débil aplica su marca potenciada (ver WeakPointPassive). Lo da GE_BreakLimits junto con Status_Unstoppable
    Status_WeakPoint,          // Marcado por Punto débil (recibe más daño). También es el EffectGroup de las dos marcas, con ReplacesGroup

    // --- COMANDANTE ---
    Status_CommandingVoice,    // Voz de mando activa: prende el anillo de velocidad del aura del Comandante (PaladinAuraPassive con RequiredOwnerTag). Lo da GE_CommandingVoice

    // --- GUARDIÁN ---
    Status_Avatar,             // Avatar activo: el modelo crece (CharacterClassDefinition.GrowTag) y sus golpes cuerpo a cuerpo llegan más lejos (MeleeRangeBonus). Lo da GE_Avatar junto con Status_Unstoppable

    // --- SISTEMAS COMPARTIDOS ---
    Status_Flying,             // Vuelo libre: sin gravedad, se mueve en 3D hacia donde mira la cámara, Espacio sube y Ctrl baja; no aterriza mientras dure; el daño o un enraizado lo terminan. Lo da el GE del GA_Flight (ver PlayerController → VUELO LIBRE)
}
