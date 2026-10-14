using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_CompanionSlot
//
// Un cuadrito del panel de compañeros (UI_CompanionPanel): las piezas que lo forman, armadas
// en el prefab de la cámara del jugador. El panel lo usa como MOLDE: hace una copia por cada
// compañero de la rueda y le cambia el ícono, el tamaño y los colores.
// ============================================================
public class UI_CompanionSlot : MonoBehaviour
{
    [Tooltip("El marco: verde en el compañero activo.")]
    [Label("Marco")]
    public Image Frame;

    [Label("Ícono")]
    public Image Icon;

    [Tooltip("La capa oscura de encima: tenue en los no elegidos, fuerte en el que espera para volver.")]
    [Label("Oscurecido")]
    public Image Dim;

    [Tooltip("Los segundos que le faltan para volver, si murió.")]
    [Label("Tiempo para volver")]
    public TMP_Text RespawnTimer;

    [Tooltip("La barra de vida (solo en el activo, vivo). Se prende y apaga entera.")]
    [Label("Barra de vida")]
    public GameObject HealthBar;

    [Tooltip("El relleno de la barra: se estira de izquierda a derecha según la vida (su anchorMax.x).")]
    [Label("Relleno de la barra")]
    public RectTransform HealthFill;
}
