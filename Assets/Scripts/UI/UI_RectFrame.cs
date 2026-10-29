using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_RectFrame
//
// Un marco hueco (el borde de un rectángulo) dibujado como pieza de UI, sin sprite, como
// UI_RingWedge. El rectángulo es el de su RectTransform, el color el del Graphic y el grosor se
// pone acá, en píxeles del menú.
//
// Lo usa el marco de lo elegido con el control en los menús (UI_SelectionFrame).
// ============================================================
[AddComponentMenu("UI/Rect Frame (marco)")]
// Sin CanvasRenderer un Graphic no se dibuja (las Image lo traen solas; esta pieza lo pide).
[RequireComponent(typeof(CanvasRenderer))]
public class UI_RectFrame : Graphic
{
    [Tooltip("El ancho de cada lado del marco, en píxeles del menú (1920×1080).")]
    [Label("Grosor (px)")]
    public float Thickness = 4f;

    protected override void OnEnable()
    {
        base.OnEnable();
        raycastTarget = false;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect r = rectTransform.rect;
        float t = Mathf.Clamp(Thickness, 0f, Mathf.Min(r.width, r.height) * 0.5f);
        if (t <= 0f) return;

        // Cuatro tiras que no se pisan en las esquinas (si no, ahí se vería más opaco).
        Color32 c = color;
        AddQuad(vh, c, r.xMin,     r.yMax - t, r.xMax,     r.yMax);       // arriba
        AddQuad(vh, c, r.xMin,     r.yMin,     r.xMax,     r.yMin + t);   // abajo
        AddQuad(vh, c, r.xMin,     r.yMin + t, r.xMin + t, r.yMax - t);   // izquierda
        AddQuad(vh, c, r.xMax - t, r.yMin + t, r.xMax,     r.yMax - t);   // derecha
    }

    private static void AddQuad(VertexHelper vh, Color32 c, float x0, float y0, float x1, float y1)
    {
        int i = vh.currentVertCount;
        vh.AddVert(new Vector3(x0, y0), c, Vector4.zero);
        vh.AddVert(new Vector3(x0, y1), c, Vector4.zero);
        vh.AddVert(new Vector3(x1, y1), c, Vector4.zero);
        vh.AddVert(new Vector3(x1, y0), c, Vector4.zero);
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i, i + 2, i + 3);
    }
}
