using UnityEngine;

// ============================================================
// TrajectoryPreview
//
// La curva que va a hacer el proyectil mientras el jugador apunta (pedido de Gustavo: el
// arco del Explorador el 13 de octubre de 2026, y todos los lanzamientos apuntados el 14 —
// el hacha, las dagas, la lanza del Comandante, el Guiding Bolt): una línea desde la mano
// hasta donde choca, y un anillo en ese punto. Sale de la misma cuenta que el disparo de
// verdad (la mano, la dirección de la mira, la velocidad, si cae y cuánto vuela), así que
// con cada fase de carga del arco la curva se estira.
//
// VIVE EN EL PREFAB DE LA CÁMARA (Player Camera ▸ TrajectoryPreview), con sus dos líneas
// armadas ahí: el COLOR, el GROSOR y el MATERIAL se cambian en cada LineRenderer (Line y
// Ring). En el prefab se ven con una curva de muestra; al jugar arrancan apagadas.
//
// Solo en la pantalla del DUEÑO (la cámara solo existe para él): PlayerController le pasa
// los datos cada frame mientras mantiene (ver TickTrajectoryPreview). No toca la red.
// ============================================================
public class TrajectoryPreview : MonoBehaviour
{
    [Header("Piezas")]
    [Tooltip("La línea de la curva, de la mano hasta donde choca. Su color, grosor y material " +
             "son los de este LineRenderer.")]
    [Label("Línea de la curva")]
    public LineRenderer Line;

    [Tooltip("El anillo en el punto donde choca. Su color, grosor y material son los de este " +
             "LineRenderer.")]
    [Label("Anillo del impacto")]
    public LineRenderer Ring;

    [Header("Medidas")]
    [Tooltip("Radio del anillo donde choca, en metros.")]
    [Label("Radio del anillo (m)")]
    public float RingRadius = 0.35f;

    [Tooltip("Cuántos puntos tiene el anillo. Más = más redondo.")]
    [Label("Puntos del anillo")]
    public int RingPoints = 24;

    [Tooltip("Cada cuántos segundos de vuelo se calcula un punto de la curva. Menos = más suave " +
             "(y más cuentas por frame).")]
    [Label("Precisión de la curva (s)")]
    public float StepSeconds = 0.03f;

    private const int   MaxPoints = 400;    // tope de puntos de la curva, por las dudas
    private const float Lift      = 0.03f;  // el anillo, un pelito afuera de la superficie

    // Choca contra todo menos las paredes invisibles (Ignore Raycast) y los triggers.
    private static readonly int Mask = ~(1 << 2);

    private readonly Vector3[] _points = new Vector3[MaxPoints];

    private void Awake()
    {
        if (Line != null) Line.useWorldSpace = true;
        if (Ring != null) { Ring.useWorldSpace = true; Ring.loop = true; }
        Hide();
    }

    // velocity: la de salida. straightTime: los segundos que vuela recto antes de que la
    // gravedad la baje (el Vuelo recto de la flecha). maxSeconds: cuánto vuela antes de
    // desaparecer (la curva no pasa de ahí). ignoreRoot: el que lanza (no choca consigo).
    public void Show(Vector3 start, Vector3 velocity, bool gravity, float straightTime, float maxSeconds,
                     Transform ignoreRoot)
    {
        if (Line == null) return;

        float step = Mathf.Max(0.005f, StepSeconds);
        int   limit = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(0.1f, maxSeconds) / step) + 1, 2, MaxPoints);

        int count = 1;
        _points[0] = start;
        Vector3 point = start;
        bool hit = false;
        Vector3 hitPoint = Vector3.zero, hitNormal = Vector3.up;

        float t = 0f;
        while (count < limit)
        {
            Vector3 next = point + velocity * step;
            t += step;
            if (gravity && t > straightTime) velocity += Physics.gravity * step;

            Vector3 delta = next - point;
            if (Physics.Raycast(point, delta, out RaycastHit info, delta.magnitude, Mask, QueryTriggerInteraction.Ignore) &&
                (ignoreRoot == null || info.transform.root != ignoreRoot))
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

        Line.positionCount = count;
        Line.SetPositions(_points);
        Line.enabled = true;

        if (Ring == null) return;
        Ring.enabled = hit;
        if (hit) DrawRing(hitPoint, hitNormal);
    }

    public void Hide()
    {
        if (Line != null) Line.enabled = false;
        if (Ring != null) Ring.enabled = false;
    }

    private void OnDisable() => Hide();

    private void DrawRing(Vector3 center, Vector3 normal)
    {
        int points = Mathf.Max(3, RingPoints);
        Quaternion rot = Quaternion.FromToRotation(Vector3.up, normal);
        Vector3 lift = normal * Lift;   // para que no parpadee contra la superficie
        Ring.positionCount = points;
        for (int i = 0; i < points; i++)
        {
            float a = i * Mathf.PI * 2f / points;
            Ring.SetPosition(i, center + lift + rot * new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * RingRadius);
        }
    }
}
