using UnityEngine;

// ============================================================
// IRadialMenuAbility
//
// La implementan las habilidades que, en vez de activarse con un
// solo click, abren un menú circular para elegir una opción antes
// de ejecutarse (ej: elegir qué tótem invocar). PlayerController
// detecta esta interfaz para mostrar UI_RadialMenu en vez de llamar
// a Activate() directamente. La rueda acepta cualquier cantidad de opciones.
// ============================================================
public interface IRadialMenuAbility
{
    // Distancia máxima a la que se puede apuntar/seleccionar con este menú.
    float MaxRadialRange { get; }

    // Un ícono por cada opción del menú (mismo orden que ActivateWithSelection).
    Sprite[] RadialIcons { get; }

    // OPCIONAL: el nombre de cada opción, que la rueda muestra en el centro al pasar por
    // encima, YA en el idioma del jugador (Loc.Pick). Se lee cada vez que se abre la rueda.
    // Sin esto dice "Option 1", "Option 2"...
    string[] RadialLabels => null;

    // OPCIONAL: una línea corta debajo del nombre (qué da esa opción).
    string[] RadialDescriptions => null;

    // OPCIONAL: si la opción se puede elegir AHORA (un cooldown propio por opción). Corre en
    // el dueño, con los tags sincronizados. La que no, se ve apagada y soltarla ahí cancela.
    bool IsRadialOptionAvailable(int index) => true;

    // Se llama al confirmar una opción del menú. selectedIndex es -1 si el
    // jugador canceló.
    void ActivateWithSelection(int selectedIndex, Vector3 targetPosition);
}
