using UnityEngine;

// ============================================================
// IRadialMenuAbility
//
// La implementan las habilidades que, en vez de activarse con un
// solo click, abren un menú circular para elegir una opción antes
// de ejecutarse (ej: elegir qué tótem invocar). PlayerController
// detecta esta interfaz para mostrar UI_RadialMenu en vez de llamar
// a Activate() directamente.
// ============================================================
public interface IRadialMenuAbility
{
    // Distancia máxima a la que se puede apuntar/seleccionar con este menú.
    float MaxRadialRange { get; }

    // Un ícono por cada opción del menú (mismo orden que ActivateWithSelection).
    Sprite[] RadialIcons { get; }

    // Se llama al confirmar una opción del menú. selectedIndex es -1 si el
    // jugador canceló.
    void ActivateWithSelection(int selectedIndex, Vector3 targetPosition);
}
