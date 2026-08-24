using UnityEngine;

// ============================================================
// MercGate
//
// La REJA de la puerta de una base: está cerrada durante los 30 segundos de
// preparación y se levanta sola cuando arranca la partida. Es lo que evita que
// alguien salga a farmear antes de tiempo, y encima marca el comienzo de la partida
// con algo que se VE (una reja subiendo lee mucho mejor que un cartel).
//
// NO ES UN OBJETO DE RED, y es a propósito: la fase de la partida ya viaja
// sincronizada en MercenariesGameMode, así que cada máquina mira ese estado y mueve
// su propia reja. Cero tráfico, cero desincronización posible — si todos ven la misma
// fase, todos ven la misma reja.
//
// CÓMO USARLA
//   1. Poné un objeto en el hueco de la puerta (el modelo de reja que quieras del
//      pack medieval, o generá barrotes con el menú contextual de este componente).
//   2. Agregale este script.
//   3. Listo. Si no hay modo de juego en la escena, la reja se queda ABIERTA — así
//      una escena de pruebas nunca te deja encerrado.
//
// Si el modelo tiene que subir pero el pivote está en el piso, poné la parte que se
// mueve en "Moving Part" y dejá este objeto quieto.
// ============================================================
public class MercGate : MonoBehaviour
{
    [Header("Qué se mueve")]
    [Tooltip("La parte que sube al abrirse. Si lo dejás vacío se mueve este mismo objeto.")]
    public Transform MovingPart;

    [Tooltip("Cuánto sube la reja al abrirse. Tiene que ser suficiente para que el hueco " +
             "quede libre de verdad.")]
    public float OpenHeight = 5f;

    [Tooltip("Cuánto tarda en subir o bajar, en segundos.")]
    public float MoveSeconds = 1.5f;

    [Header("Cuándo se cierra")]
    [Tooltip("Cerrada durante los segundos de preparación (lo normal).")]
    public bool ClosedDuringWarmup = true;

    [Tooltip("Cerrarla de nuevo al terminar la partida, mientras se muestra el resultado.")]
    public bool ClosedWhenMatchEnds = false;

    // Posición local con la reja CERRADA. Se toma de cómo la dejaste en el editor: la
    // colocás cerrada y el script sube desde ahí.
    private Vector3 _closedLocalPosition;
    private float _openAmount;   // 0 = cerrada, 1 = abierta

    private Transform Part => MovingPart != null ? MovingPart : transform;

    private void Awake()
    {
        _closedLocalPosition = Part.localPosition;

        // Arrancamos ya en la posición que corresponda, sin animación: si no, en el
        // primer frame de la partida la reja se ve "cayendo" desde arriba.
        _openAmount = ShouldBeClosed() ? 0f : 1f;
        Apply();
    }

    private void Update()
    {
        float target = ShouldBeClosed() ? 0f : 1f;

        if (!Mathf.Approximately(_openAmount, target))
        {
            float speed = MoveSeconds > 0.01f ? Time.deltaTime / MoveSeconds : 1f;
            _openAmount = Mathf.MoveTowards(_openAmount, target, speed);
            Apply();
        }
    }

    private void Apply()
    {
        // SmoothStep en vez de lineal: una reja pesada arranca y frena, no se desliza
        // a velocidad constante.
        float eased = Mathf.SmoothStep(0f, 1f, _openAmount);
        Part.localPosition = _closedLocalPosition + Vector3.up * (OpenHeight * eased);
    }

    private bool ShouldBeClosed()
    {
        MercenariesGameMode gm = MercenariesGameMode.Instance;
        if (gm == null) return false;   // sin partida, la reja no estorba

        if (ClosedDuringWarmup  && gm.State == EMatchState.Warmup) return true;
        if (ClosedWhenMatchEnds && gm.State == EMatchState.Ended)  return true;
        return false;
    }

    // =========================================================
    // REJA DE BARROTES GENERADA
    //
    // Para no depender de tener el modelo justo: genera una reja con marco y barrotes,
    // con colliders. Después la podés reemplazar por el prefab que quieras del pack.
    // =========================================================

    [Header("Barrotes generados (opcional)")]
    public float BarsWidth  = 5.5f;
    public float BarsHeight = 4.5f;
    [Range(3, 15)] public int BarCount = 7;
    public float BarThickness = 0.18f;

    [ContextMenu("Generar reja de barrotes")]
    private void GenerateBars()
    {
        Transform host = Part;

        // Borramos una reja generada antes, para poder iterar el tamaño.
        for (int i = host.childCount - 1; i >= 0; i--)
        {
            Transform child = host.GetChild(i);
            if (child.name.StartsWith("Bar_")) DestroyImmediate(child.gameObject);
        }

        // Marco: dos travesaños.
        MakePiece(host, "Bar_Top", new Vector3(0f, BarsHeight - 0.15f, 0f),
                  new Vector3(BarsWidth, 0.3f, BarThickness * 1.6f));
        MakePiece(host, "Bar_Mid", new Vector3(0f, BarsHeight * 0.5f, 0f),
                  new Vector3(BarsWidth, 0.22f, BarThickness * 1.4f));

        // Barrotes verticales, repartidos parejo.
        for (int i = 0; i < BarCount; i++)
        {
            float t = BarCount == 1 ? 0.5f : i / (float)(BarCount - 1);
            float x = Mathf.Lerp(-BarsWidth * 0.5f + BarThickness, BarsWidth * 0.5f - BarThickness, t);
            MakePiece(host, $"Bar_{i:00}", new Vector3(x, BarsHeight * 0.5f, 0f),
                      new Vector3(BarThickness, BarsHeight, BarThickness));
        }

        Debug.Log($"[MercGate] Reja generada en '{host.name}'. Acomodala en el hueco de la puerta " +
                  "con la reja CERRADA: esa posición es la que el script usa como punto de partida.");
    }

    private static void MakePiece(Transform parent, string name, Vector3 localPos, Vector3 size)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale    = size;
    }

    private void OnDrawGizmosSelected()
    {
        Transform part = Part;
        Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.9f);

        // Dónde va a quedar la reja abierta.
        Vector3 openPos = part.position + Vector3.up * OpenHeight;
        Gizmos.DrawWireCube(openPos + Vector3.up * (BarsHeight * 0.5f),
                            new Vector3(BarsWidth, BarsHeight, 0.3f));
        Gizmos.DrawLine(part.position, openPos);
    }
}
