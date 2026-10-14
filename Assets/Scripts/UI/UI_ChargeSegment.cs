using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_ChargeSegment
//
// Un tramo de la barra de carga (UI_ChargeBar): las piezas que lo forman, armadas en el
// prefab de la cámara del jugador. La barra lo usa como MOLDE: hace una copia por cada etapa
// de la carga, le da su parte del ancho y lo va llenando.
// ============================================================
public class UI_ChargeSegment : MonoBehaviour
{
    [Tooltip("El fondo del tramo: oscuro hasta que se llega a su etapa, después teñido de su color.")]
    [Label("Fondo")]
    public Image Back;

    [Tooltip("El relleno: se estira de izquierda a derecha moviendo su anchorMax.x (anclado a la izquierda).")]
    [Label("Relleno")]
    public Image Fill;

    [Tooltip("Una capa blanca encima que destella al llegar a su etapa.")]
    [Label("Destello")]
    public Image Flash;

    [Tooltip("El número de la etapa (1, 2, 3…).")]
    [Label("Número")]
    public TMP_Text Number;
}
