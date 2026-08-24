using System.Collections.Generic;
using UnityEngine;

// ============================================================
// MercArenaBounds
//
// El "techo" y las paredes INVISIBLES de la arena: la caja que impide que un jugador
// con dash, salto o vuelo se escape del escenario. Nada de esto se ve; son solo
// colliders.
//
// Cómo usarlo: un GameObject vacío en el CENTRO de la arena con este componente. No
// hay que cablear nada más — al arrancar la partida se arma solo.
//
// POR QUÉ SE CONSTRUYE EN RUNTIME Y NO EN LA ESCENA: si estas paredes existieran al
// hornear el NavMesh, los NPCs las verían como obstáculos y el mallado se comería el
// borde de la arena. Creándolas en Awake, el NavMesh se hornea sobre el escenario
// limpio y las paredes aparecen recién al jugar. En el editor las ves como gizmo
// (seleccioná el objeto) para poder acomodarlas sin adivinar.
//
// LAS PUERTAS: el anillo deja un hueco en la dirección de cada base, así los equipos
// pueden entrar y salir aunque su sala esté FUERA del muro de la arena. Los huecos se
// calculan solos a partir de dónde estén las MercTeamBase — si movés una base, el
// hueco se mueve con ella.
// ============================================================
public class MercArenaBounds : MonoBehaviour
{
    [Header("Forma")]
    [Tooltip("Radio del anillo invisible. Ponelo un poco MÁS que el muro visible para que " +
             "nadie quede trabado entre las dos paredes.")]
    public float Radius = 43f;

    [Tooltip("Altura del techo invisible. Alto a propósito: tiene que frenar al que salta o " +
             "vuela, no molestar a nadie que juegue normal.")]
    public float CeilingHeight = 20f;

    [Tooltip("En cuántos bloques se divide el anillo. Más bloques = curva más fina.")]
    [Range(8, 64)] public int Segments = 28;

    [Header("Puertas")]
    [Tooltip("Dejar un hueco en el anillo hacia cada base, para poder entrar y salir de las " +
             "salas aunque estén fuera del muro.")]
    public bool OpenTowardBases = true;

    [Tooltip("Qué tan ancho es cada hueco, en grados a cada lado de la dirección de la base.")]
    [Range(2f, 45f)] public float GateHalfAngle = 15f;

    [Header("Techo")]
    public bool BuildCeiling = true;

    [Tooltip("Cuánto se pasa el techo del radio del anillo. Sirve para que también tape las " +
             "salas de los equipos si las pusiste afuera de la arena.")]
    public float CeilingMargin = 18f;

    [Header("Cuándo")]
    [Tooltip("Armar los límites al arrancar. Apagalo solo si querés armarlos a mano desde otro script.")]
    public bool BuildOnAwake = true;

    private const string ChildName = "InvisibleBounds";

    private void Awake()
    {
        if (BuildOnAwake) Build();
    }

    // Arma (o rehace) los colliders. Es público para poder llamarlo desde el menú
    // contextual del componente y ver el resultado sin darle Play.
    [ContextMenu("Armar límites ahora")]
    public void Build()
    {
        Clear();

        Transform root = new GameObject(ChildName).transform;
        root.SetParent(transform, false);

        if (BuildCeiling) BuildCeilingCollider(root);
        BuildRing(root);
    }

    [ContextMenu("Borrar límites")]
    public void Clear()
    {
        Transform existing = transform.Find(ChildName);
        if (existing == null) return;

        if (Application.isPlaying) Destroy(existing.gameObject);
        else                       DestroyImmediate(existing.gameObject);
    }

    // Un solo bloque plano bien arriba. Es cuadrado y no redondo a propósito: es
    // invisible, así que la forma no importa — importa que no queden esquinas por
    // donde colarse.
    private void BuildCeilingCollider(Transform parent)
    {
        float half = Mathf.Max(Radius, FarthestBaseDistance() + 8f) + CeilingMargin;

        GameObject go = new GameObject("Ceiling");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, CeilingHeight + 1f, 0f);

        BoxCollider box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(half * 2f, 2f, half * 2f);
    }

    private void BuildRing(Transform parent)
    {
        List<Vector3> gateDirections = OpenTowardBases ? CollectBaseDirections() : new List<Vector3>();

        float step        = 360f / Segments;
        float chord       = 2f * Mathf.PI * Radius / Segments * 1.15f; // 15% de solape
        float wallHeight  = CeilingHeight + 2f;

        for (int i = 0; i < Segments; i++)
        {
            float angle = i * step;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

            if (IsInsideGate(dir, gateDirections)) continue;

            GameObject go = new GameObject($"Wall_{i:00}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = dir * Radius + Vector3.up * (wallHeight * 0.5f - 1f);
            go.transform.localRotation = Quaternion.Euler(0f, angle, 0f);

            BoxCollider box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(chord, wallHeight, 1.5f);
        }
    }

    private bool IsInsideGate(Vector3 dir, List<Vector3> gates)
    {
        foreach (Vector3 gate in gates)
            if (Vector3.Angle(dir, gate) <= GateHalfAngle) return true;
        return false;
    }

    // Direcciones (desde este objeto) hacia cada base. Se leen de la escena en vez de
    // fijarlas en 0/120/240: así los huecos siguen a las bases si las movés a mano.
    private List<Vector3> CollectBaseDirections()
    {
        var dirs = new List<Vector3>();
        foreach (MercTeamBase b in FindObjectsByType<MercTeamBase>(FindObjectsSortMode.None))
        {
            if (b == null) continue;
            Vector3 d = b.transform.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f) dirs.Add(d.normalized);
        }
        return dirs;
    }

    private float FarthestBaseDistance()
    {
        float max = 0f;
        foreach (MercTeamBase b in FindObjectsByType<MercTeamBase>(FindObjectsSortMode.None))
        {
            if (b == null) continue;
            Vector3 d = b.transform.position - transform.position;
            d.y = 0f;
            max = Mathf.Max(max, d.magnitude);
        }
        return max;
    }

    // =========================================================
    // EDITOR
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;

        // Anillo, marcando los huecos de las puertas en otro color.
        List<Vector3> gates = OpenTowardBases ? CollectBaseDirections() : new List<Vector3>();
        int steps = Mathf.Max(24, Segments * 2);

        for (int i = 0; i < steps; i++)
        {
            float a0 = i * (360f / steps);
            float a1 = (i + 1) * (360f / steps);
            Vector3 d0 = Quaternion.Euler(0f, a0, 0f) * Vector3.forward;
            Vector3 d1 = Quaternion.Euler(0f, a1, 0f) * Vector3.forward;

            Gizmos.color = IsInsideGate(d0, gates)
                ? new Color(0.3f, 1f, 0.4f, 0.9f)     // hueco = por acá se pasa
                : new Color(1f, 0.55f, 0.1f, 0.8f);   // pared

            Gizmos.DrawLine(center + d0 * Radius, center + d1 * Radius);
            Gizmos.DrawLine(center + d0 * Radius + Vector3.up * CeilingHeight,
                            center + d1 * Radius + Vector3.up * CeilingHeight);
            if (i % 4 == 0)
                Gizmos.DrawLine(center + d0 * Radius, center + d0 * Radius + Vector3.up * CeilingHeight);
        }

        // Techo.
        if (!BuildCeiling) return;
        float half = Mathf.Max(Radius, FarthestBaseDistance() + 8f) + CeilingMargin;
        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.35f);
        Gizmos.DrawWireCube(center + Vector3.up * CeilingHeight, new Vector3(half * 2f, 0.2f, half * 2f));
    }
}
