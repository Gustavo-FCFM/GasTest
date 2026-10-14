using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_RadialOption
//
// Una porción de la rueda (UI_RadialMenu): las piezas que la forman, armadas en el prefab de
// la cámara del jugador. La rueda la usa como MOLDE: hace una copia por cada opción, le da su
// ángulo y su ancho, le pone el ícono y la anima.
//
// Del molde la rueda toma las MEDIDAS: los radios de la porción y del brillo (lo que el brillo
// sobresale de la porción se mantiene) y el tamaño máximo del ícono.
// ============================================================
public class UI_RadialOption : MonoBehaviour
{
    [Tooltip("El halo detrás de la porción, que se prende al resaltarla. Un poco más grande que ella.")]
    [Label("Brillo")]
    public UI_RingWedge Glow;

    [Tooltip("La porción en sí.")]
    [Label("Porción")]
    public UI_RingWedge Wedge;

    [Tooltip("El ícono de la opción. Su tamaño en el molde es el máximo: con muchas opciones se " +
             "achica para que entre.")]
    [Label("Ícono")]
    public Image Icon;
}
