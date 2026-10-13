using UnityEngine;

// ============================================================
// TrajectoryPreview
//
// La curva que va a hacer la flecha, mientras el jugador tensa el arco (pedido de Gustavo,
// 13 de octubre de 2026): una línea desde la mano hasta donde caería, y un anillo en el
// punto donde choca. Sale de la misma cuenta que el disparo de verdad (la mano, la
// dirección de la mira y la velocidad de la fase que sale si suelta ahora), así que con
// cada fase de carga la curva se estira.
//
// Solo en la pantalla del DUEÑO: la crea PlayerController la primera vez que hace falta y
// le pasa los datos cada frame (ver TickTrajectoryPreview). No toca la red.
// ============================================================
public class TrajectoryPreview : MonoBehaviour
{
    private const float Step       = 0.03f;   // segundos por tramo de la simulación
    private const int   MaxPoints  = 200;     // ~6 s de vuelo
    private const float RingRadius = 0.35f;
    private const int   RingPoints = 24;

    private static readonly Color LineColor = new Color(1f, 1f, 1f, 0.55f);
    private static readonly Color RingColor = new Color(1f, 0.85f, 0.3f, 0.9f);

    // Las líneas viven en un objeto APARTE, no colgadas del jugador: la cámara esconde los
    // renderers del jugador cuando se le pega (contra una pared) o al apuntar, y se llevaba
    // la curva con ellos.
    private GameObject _root;
    private LineRenderer _line;
    private LineRenderer _ring;
    private readonly Vector3[] _points = new Vector3[MaxPoints];

    // Choca contra todo menos las paredes invisibles (Ignore Raycast) y los triggers.
    private static readonly int Mask = ~(1 << 2);

    // straightTime: los segundos que vuela recto antes de que la gravedad la baje (el Vuelo
    // recto de la flecha).
    public void Show(Vector3 start, Vector3 velocity, bool gravity, float straightTime = 0f)
    {
        EnsureRenderers();

        int count = 1;
        _points[0] = start;
        Vector3 point = start;
        bool hit = false;
        Vector3 hitPoint = Vector3.zero, hitNormal = Vector3.up;

        float t = 0f;
        while (count < MaxPoints)
        {
            Vector3 next = point + velocity * Step;
            t += Step;
            if (gravity && t > straightTime) velocity += Physics.gravity * Step;

            Vector3 delta = next - point;
            if (Physics.Raycast(point, delta, out RaycastHit info, delta.magnitude, Mask, QueryTriggerInteraction.Ignore) &&
                info.transform.root != transform.root)
            {
                _points[count++] = info.point;
                hit = true;
                hitPoint  = info.point;
                hitNormal = info.normal;
                break;
            }

            _points[count++] = next;
            point = next;
        }

        _line.positionCount = count;
        _line.SetPositions(_points);
        _line.enabled = true;

        _ring.enabled = hit;
        if (hit) DrawRing(hitPoint, hitNormal);
    }

    public void Hide()
    {
        if (_line != null) _line.enabled = false;
        if (_ring != null) _ring.enabled = false;
    }

    private void OnDisable() => Hide();

    private void OnDestroy()
    {
        if (_root != null) Destroy(_root);
    }

    private void DrawRing(Vector3 center, Vector3 normal)
    {
        Quaternion rot = Quaternion.FromToRotation(Vector3.up, normal);
        Vector3 lift = normal * 0.03f;   // un pelito afuera de la superficie, para que no parpadee
        _ring.positionCount = RingPoints;
        for (int i = 0; i < RingPoints; i++)
        {
            float a = i * Mathf.PI * 2f / RingPoints;
            _ring.SetPosition(i, center + lift + rot * new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * RingRadius);
        }
    }

    private void EnsureRenderers()
    {
        if (_line != null) return;
        _root = new GameObject("TrajectoryPreview (" + name + ")");
        _line = CreateLine("TrajectoryLine", LineColor, 0.04f, false);
        _ring = CreateLine("TrajectoryRing", RingColor, 0.05f, true);
    }

    private LineRenderer CreateLine(string name, Color color, float width, bool loop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_root.transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace     = true;
        line.loop              = loop;
        line.widthMultiplier   = width;
        line.numCapVertices    = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows    = false;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        var material = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
        if (shader != null)
        {
            // Unlit transparente: la línea se ve sin tapar el mundo.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetColor("_BaseColor", color);
        }
        line.sharedMaterial = material;
        line.startColor = line.endColor = color;
        line.enabled = false;
        return line;
    }
}
