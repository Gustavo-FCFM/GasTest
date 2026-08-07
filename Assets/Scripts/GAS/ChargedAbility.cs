// ============================================================
// IChargedAbility
//
// La implementa cualquier GameplayAbility que use sistema de cargas (varios
// usos disponibles antes de tener que esperar la recarga completa, ej: el
// dash). El HUD (UI_AbilitySlot) la detecta para mostrar el número de cargas
// disponibles junto al ícono. El conteo real en vivo se sincroniza aparte por
// NetworkAbilitySystemComponent.NetCharges.
// ============================================================
public interface IChargedAbility
{
    // Cantidad máxima de cargas de la habilidad. La UI lo usa como valor por
    // defecto (lleno) antes de recibir el primer conteo sincronizado.
    int MaxChargeCount { get; }
}
