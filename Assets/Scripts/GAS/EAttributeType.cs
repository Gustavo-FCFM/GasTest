// ============================================================
// EAttributeType
//
// Lista de todos los atributos numéricos que puede tener un
// personaje (vida, maná, daño, velocidad, etc.). AttributeValue
// guarda un valor actual por cada uno de estos, y Modifier/
// GameplayEffect los usan para saber a qué atributo afectar.
// Agregar un valor acá no rompe nada; solo queda sin uso hasta que
// algún AttributeSetDefinition/Modifier lo referencie.
// ============================================================
public enum EAttributeType
{
    None,
    Health,      // Vida actual
    MaxHealth,   // Vida máxima
    Mana,        // Maná actual
    MaxMana,     // Maná máximo
    Energy,      // Energía actual (sistema de bloqueo/stamina)
    MaxEnergy,   // Energía máxima
    Def,         // Defensa — reduce el daño físico recibido
    Atq,         // Ataque — daño físico base
    MovSpeed,    // Velocidad de movimiento
    AtkSpeed,    // Tiempo entre ataques (a menor valor, ataca más rápido)
    CritChance,  // Probabilidad de golpe crítico
    LifeSteal,   // Porcentaje de robo de vida por el daño infligido
    Exp,         // Puntos de experiencia acumulados
    MaxExp,      // Experiencia necesaria para subir de nivel
    Level,       // Nivel del personaje
    Shield,      // Escudo temporal — absorbe daño antes que la vida
    MagicDamage, // Daño mágico base
    // IMPORTANTE: agregar valores nuevos SIEMPRE al final. Los .asset serializan
    // los atributos por su número de enum; insertarlos en el medio correría los
    // índices y rompería las referencias ya guardadas en los assets existentes.
    CritDamage   // Multiplicador de daño crítico (ej: 2 = x2). Lo usa el ataque furtivo por la espalda del Pícaro
}
