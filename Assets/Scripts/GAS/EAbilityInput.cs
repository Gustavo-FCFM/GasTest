// ============================================================
// EAbilityInput
//
// Identifica cada "slot" de habilidad de un personaje (a qué botón
// está atada). Lo usan CharacterClassDefinition (para asignar qué
// habilidad va en cada slot), PlayerController (para saber qué
// input revisar) y NetworkAbilitySystemComponent (para sincronizar
// cooldowns/VFX por slot en vez de por referencia a la habilidad).
// ============================================================
public enum EAbilityInput
{
    None,
    PrimaryAttack,   // Click Izquierdo (LMB)
    SecondaryAttack, // Click Derecho (RMB)
    Action1,         // Q
    Action2,         // E
    Action3,         // R
    Movement,        // Shift (habilidades de desplazamiento: salto, dash, etc.)
    Passive          // Pasiva, no tiene botón propio
}
