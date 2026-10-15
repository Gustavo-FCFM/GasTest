using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_LobbySlot
//
// Un lugar de la sala (Menus_UI ▸ Lobby): una fila de un equipo o de los espectadores. Dice
// de qué columna es y dónde están sus partes; UI_LobbyPanel las busca al empezar y las pinta
// según quién ocupa cada lugar. La fila no tiene lógica propia.
//
// Los lugares de un equipo van en el orden de la jerarquía (el de arriba primero). Los de
// los espectadores pueden ser más de los que hay armados: el panel copia el último y lo pone
// debajo, a la misma distancia que hay entre los dos primeros.
// ============================================================
public class UI_LobbySlot : MonoBehaviour
{
    [Tooltip("De qué columna es: 1, 2 o 3 = ese equipo; 0 = los espectadores.")]
    [Label("Equipo (0 = espectadores)")]
    public int Team;

    [Tooltip("El recuadro de la fila: se pinta solo cuando hay alguien (el tuyo, de otro color).")]
    [Label("Fondo")]
    public Image Background;

    [Tooltip("El ícono de la clase. Es un botón: el tuyo (o el de un bot, para el host) abre el selector de clase.")]
    [Label("Ícono de clase")]
    public Image Icon;

    [Label("Botón del ícono")]
    public Button IconButton;

    [Tooltip("El nombre de quien ocupa el lugar.")]
    [Label("Nombre")]
    public TMP_Text Label;

    [Tooltip("En un lugar libre: \"Unirte\" en un equipo, \"Espectador\" en la columna de espectadores.")]
    [Label("Botón Unirte")]
    public Button JoinButton;

    [Label("Texto de Unirte")]
    public TMP_Text JoinLabel;

    [Tooltip("\"+\": agregar un bot en este lugar libre. Solo lo ve el host.")]
    [Label("Botón agregar bot")]
    public Button AddBotButton;

    [Tooltip("\"x\": sacar el bot que ocupa este lugar. Solo lo ve el host.")]
    [Label("Botón sacar bot")]
    public Button RemoveButton;

    // Quién ocupa esta fila AHORA (lo escribe el panel en cada redibujado).
    [System.NonSerialized] public int EntryId;
}
