using UnityEngine;

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
    [InspectorName("Ninguno")]              None,
    [InspectorName("Clic izquierdo")]       PrimaryAttack,   // Click Izquierdo (LMB)
    [InspectorName("Clic derecho")]         SecondaryAttack, // Click Derecho (RMB)
    [InspectorName("Q (acción 1)")]         Action1,         // Q
    [InspectorName("E (acción 2)")]         Action2,         // E
    [InspectorName("R (definitiva)")]       Action3,         // R
    [InspectorName("Shift (movimiento)")]   Movement,        // Shift (habilidades de desplazamiento: salto, dash, etc.)
    [InspectorName("Pasiva")]               Passive          // Pasiva, no tiene botón propio
}
