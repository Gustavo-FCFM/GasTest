using UnityEngine;
using UnityEngine.UI;

// ============================================================
// ButtonTint
//
// El color de los botones de los menús, para que el ELEGIDO se distinga bien (26 de octubre de
// 2026, para el showcase: con el control hay que ver dónde estás parado).
//
// POR QUÉ: Unity pinta los estados de un botón (normal, elegido, apretado, apagado)
// MULTIPLICANDO un tinte por el color de su imagen. Con el color en la imagen, el resultado
// dependía del color: "Ajustes" elegido se veía casi negro. Ahora el color va en el botón (sus
// colores normal / elegido / apretado / apagado) y la imagen queda blanca:
//   · normal   = el color del botón
//   · elegido  = el MISMO color, más oscuro (es también el de pasar el mouse)
//   · apretado = todavía más oscuro
//   · apagado  = como antes: más oscuro y medio transparente
//
// MÁS OSCURO, NO MÁS PÁLIDO (Gustavo, 26 de octubre): primero el elegido iba hacia el blanco y
// quedaba deslavado ("no me gustan los colores pálidos"). Ahora solo baja el BRILLO: el tono y
// la intensidad del color quedan iguales (el naranja pasa a un ámbar más hondo, no a un café ni
// a un durazno).
//
// PARA CAMBIARLE EL COLOR A UN BOTÓN A MANO: en el inspector del Button, "Normal Color" (y los
// demás estados si se quiere); la imagen se deja blanca. Desde código: Paint, nunca
// image.color (la imagen se multiplicaría por el color del botón).
// ============================================================
public static class ButtonTint
{
    // Cuánto baja el brillo (de 0 a 1, el "V" del selector de color de Unity).
    public const float SelectedDarken = 0.12f;
    public const float PressedDarken  = 0.22f;

    // El "apagado" de Unity por defecto: más oscuro y medio transparente.
    private static readonly Color DisabledTint = new Color(0.784f, 0.784f, 0.784f, 0.5f);

    // Pinta un gráfico. Si es la imagen de un botón (su "Target Graphic", con tinte de color),
    // el color va a los estados del botón y la imagen queda blanca; si no, se pinta normal.
    public static void Paint(Graphic graphic, Color color)
    {
        if (graphic == null) return;

        Selectable s = graphic.GetComponentInParent<Selectable>(true);
        if (s == null || s.targetGraphic != graphic || s.transition != Selectable.Transition.ColorTint)
        {
            graphic.color = color;
            return;
        }

        s.colors = Colors(s.colors, color);
        if (graphic.color != Color.white) graphic.color = Color.white;
    }

    // Los estados de un botón de ese color (lo demás del bloque, como el tiempo del fundido,
    // queda como estaba). La transparencia es la del color en todos los estados.
    public static ColorBlock Colors(ColorBlock block, Color color)
    {
        block.normalColor      = color;
        block.highlightedColor = Darken(color, SelectedDarken);
        block.selectedColor    = block.highlightedColor;
        block.pressedColor     = Darken(color, PressedDarken);
        block.disabledColor    = color * DisabledTint;
        return block;
    }

    // Baja solo el brillo: el tono y la intensidad quedan.
    private static Color Darken(Color c, float amount)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        Color d = Color.HSVToRGB(h, s, Mathf.Max(0f, v - amount));
        d.a = c.a;
        return d;
    }
}
