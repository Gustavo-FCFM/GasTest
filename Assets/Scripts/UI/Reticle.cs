using UnityEngine;
using UnityEngine.UI;

// ============================================================
// Reticle
//
// Retícula (crosshair) como componente de UI de verdad: es un Graphic de
// UGUI, así que vive dentro de un Canvas (el del prefab "Player Camera"),
// se edita desde el Inspector y se previsualiza en la Scene/Game view sin
// necesidad de entrar en Play. Dibuja una cruz con hueco central a partir de
// sus parámetros (no usa un sprite, pero podés cambiar Color/tamaño/grosor).
//
// USO: en el prefab "Player Camera", bajo el Canvas "UI", creá un objeto de
// UI (GameObject → UI → Image y quitale el Image, o un objeto vacío con
// RectTransform), ancralo al centro (pos 0,0) y agregale este componente
// (Add Component → "UI/Reticle (Crosshair)"). Como esa cámara solo se
// instancia para el jugador dueño local, la retícula solo la ve su dueño.
//
// Marca a dónde apunta PlayerController.GetAimPoint() (raycast desde el
// centro exacto de la pantalla), que es el punto que usan Golpe mortal, el
// dash y los proyectiles.
// ============================================================
[AddComponentMenu("UI/Reticle (Crosshair)")]
public class Reticle : Graphic
{
    [Header("Forma")]
    [Tooltip("Largo total de cada línea de la cruz, en píxeles.")]
    public float Size = 18f;
    [Tooltip("Grosor de cada línea, en píxeles.")]
    public float Thickness = 2f;
    [Tooltip("Hueco central (espacio en el medio para no tapar el objetivo).")]
    public float Gap = 4f;
    [Tooltip("Dibuja también un punto central.")]
    public bool  CenterDot = false;
    [Tooltip("Tamaño del punto central, en píxeles.")]
    public float CenterDotSize = 2f;

    // El color lo hereda de Graphic (campo "Color" en el Inspector). No hace
    // falta bloquear los clics del mouse: una retícula nunca debe capturarlos.
    protected override void OnEnable()
    {
        base.OnEnable();
        raycastTarget = false;
    }

    // Construye la malla de la cruz (4 brazos + punto opcional) centrada en el
    // origen local del RectTransform (con pivote 0.5, el centro).
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        float arm = Mathf.Max(0f, (Size - Gap) * 0.5f);
        float t   = Thickness * 0.5f;
        float g   = Gap;

        AddQuad(vh, -g - arm, -t,       -g,       t);        // brazo izquierdo
        AddQuad(vh,  g,       -t,        g + arm,  t);        // brazo derecho
        AddQuad(vh, -t,        g,        t,        g + arm);  // brazo superior
        AddQuad(vh, -t,       -g - arm,  t,       -g);        // brazo inferior

        if (CenterDot)
        {
            float d = CenterDotSize * 0.5f;
            AddQuad(vh, -d, -d, d, d);
        }
    }

    // Agrega un rectángulo (2 triángulos) a la malla, con el color del Graphic.
    private void AddQuad(VertexHelper vh, float xMin, float yMin, float xMax, float yMax)
    {
        int i = vh.currentVertCount;

        UIVertex v = UIVertex.simpleVert;
        v.color = color;

        v.position = new Vector3(xMin, yMin); vh.AddVert(v);
        v.position = new Vector3(xMin, yMax); vh.AddVert(v);
        v.position = new Vector3(xMax, yMax); vh.AddVert(v);
        v.position = new Vector3(xMax, yMin); vh.AddVert(v);

        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i, i + 2, i + 3);
    }

#if UNITY_EDITOR
    // Redibuja al tocar los parámetros en el Inspector (vista previa en vivo).
    protected override void OnValidate()
    {
        base.OnValidate();
        raycastTarget = false;
        SetVerticesDirty();
    }
#endif
}
