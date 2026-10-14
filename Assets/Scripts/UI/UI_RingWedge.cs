using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_RingWedge
//
// Una porción de anillo dibujada como pieza de UI (como la Reticle: un Graphic, sin sprite):
// un anillo entero, un trozo de anillo o un disco, según sus medidas. Se ve en el prefab tal
// cual va a verse y se edita desde el Inspector, en píxeles del HUD.
//
//   · Radio interior 0              → un disco (el centro de la rueda).
//   · Ancho 360°                    → el anillo entero.
//   · "Se desvanece hacia afuera"   → opaco en el centro y transparente en el borde (el
//                                     fondo oscuro detrás de la rueda).
//
// Lo usa la rueda (UI_RadialMenu): cada porción, su brillo, el centro y el fondo. La rueda
// cambia los radios y el ancho de las copias según cuántas opciones hay (SetShape).
// ============================================================
[AddComponentMenu("UI/Ring Wedge (porción de anillo)")]
// Sin CanvasRenderer un Graphic no se dibuja (las Image lo traen solas; esta pieza lo pide).
[RequireComponent(typeof(CanvasRenderer))]
public class UI_RingWedge : Graphic
{
    [Header("Forma")]
    [Tooltip("Desde dónde empieza el anillo, en píxeles del HUD. 0 = un disco lleno.")]
    [Label("Radio interior (px)")]
    public float InnerRadius = 0f;

    [Tooltip("Hasta dónde llega el anillo, en píxeles del HUD.")]
    [Label("Radio exterior (px)")]
    public float OuterRadius = 50f;

    [Tooltip("Cuántos grados ocupa la porción. 360 = el anillo entero.")]
    [Label("Ancho (°)")]
    [Range(0f, 360f)]
    public float SpanDegrees = 360f;

    [Tooltip("Hacia dónde apunta el medio de la porción, en grados: 90 = arriba, 0 = a la " +
             "derecha, 180 = a la izquierda.")]
    [Label("Apunta hacia (°)")]
    public float CenterAngle = 90f;

    [Header("Borde")]
    [Tooltip("Opaco en el radio interior y transparente en el exterior, como una sombra suave.")]
    [Label("Se desvanece hacia afuera")]
    public bool FadeOutward;

    [Tooltip("Borde suavizado de los círculos (adentro y afuera), en píxeles del HUD. Sin esto " +
             "el borde se ve dentado.")]
    [Label("Borde suavizado (px)")]
    public float Feather = 0.5f;

    [Tooltip("Cuántos lados tendría el anillo entero. Más = más redondo.")]
    [Label("Lados por vuelta")]
    public int SidesPerTurn = 120;

    // Cambia la forma desde código (la rueda, al armar sus porciones).
    public void SetShape(float inner, float outer, float spanDegrees, float centerAngle)
    {
        InnerRadius = inner;
        OuterRadius = outer;
        SpanDegrees = spanDegrees;
        CenterAngle = centerAngle;
        SetVerticesDirty();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        raycastTarget = false;
    }

    // Las "columnas" del anillo: a qué radio va cada vuelta de vértices y con qué opacidad.
    private readonly List<Vector2> _columns = new List<Vector2>();

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        float span  = Mathf.Clamp(SpanDegrees, 0f, 360f);
        float inner = Mathf.Max(0f, InnerRadius);
        float outer = Mathf.Max(inner, OuterRadius);
        if (span <= 0f || outer <= 0f) return;

        _columns.Clear();
        if (FadeOutward)
        {
            // Cae rápido al principio y suave al final: se lee como una sombra, no como un aro.
            for (int k = 0; k <= 4; k++)
            {
                float t = k / 4f;
                float a = Mathf.Clamp01((1f - t) * (1f - t) * 1.6f);
                _columns.Add(new Vector2(Mathf.Lerp(inner, outer, t), a));
            }
        }
        else
        {
            float f = Mathf.Max(0f, Feather);
            if (inner > 0f && f > 0f) _columns.Add(new Vector2(Mathf.Max(0f, inner - f), 0f));
            _columns.Add(new Vector2(inner, 1f));
            _columns.Add(new Vector2(outer, 1f));
            if (f > 0f) _columns.Add(new Vector2(outer + f, 0f));
        }

        int   steps  = Mathf.Max(2, Mathf.CeilToInt(Mathf.Max(8, SidesPerTurn) * span / 360f));
        float start  = (CenterAngle - span * 0.5f) * Mathf.Deg2Rad;
        float step   = span * Mathf.Deg2Rad / steps;
        Vector2 origin = rectTransform.rect.center;
        Color32 full = color;
        int cols = _columns.Count;

        for (int i = 0; i <= steps; i++)
        {
            float   a   = start + step * i;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            for (int j = 0; j < cols; j++)
            {
                Color32 c = full;
                c.a = (byte)(full.a * _columns[j].y);
                vh.AddVert(origin + dir * _columns[j].x, c, Vector4.zero);
            }
        }

        for (int i = 0; i < steps; i++)
        {
            int row  = i * cols;
            int next = row + cols;
            for (int j = 0; j < cols - 1; j++)
            {
                vh.AddTriangle(row + j, next + j, next + j + 1);
                vh.AddTriangle(row + j, next + j + 1, row + j + 1);
            }
        }
    }
}
