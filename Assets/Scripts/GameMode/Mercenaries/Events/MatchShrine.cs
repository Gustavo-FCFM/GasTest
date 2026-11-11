using System.Collections.Generic;
using UnityEngine;

// ============================================================
// MatchShrine
//
// Un SANTUARIO del mapa: la zona que hay que capturar (un cilindro) y su altar. Es el prefab
// Prefabs/GameMode/Shrine; en el Cementerio hay uno abajo de cada torre, dentro del prefab del sector,
// así que son tres. Cuál está activo, de qué tipo y cuánto lleva cada equipo lo decide y lo sincroniza
// MatchEvent_Shrines: este componente solo dice dónde está la zona y la muestra.
//
// NO ES UN OBJETO DE RED: cada pantalla mira qué santuario está activo y prende su zona negra y su
// cristal del color del tipo. Para identificarlos en la red se ordenan todos por su posición (ver
// All): todas las máquinas tienen el mismo mapa, así que el orden es el mismo en todas.
//
// PARA PONER UNO: arrastrar el prefab donde se quiera y ajustar el radio. La zona negra y el borde
// se acomodan solos al radio y al alto (también en el editor).
//
// ENEMIGOS EXTRA (11 de noviembre de 2026): el hijo "ExtraEnemies" es un campamento normal
// (MercEnemySpawner) con "Solo con su santuario activo": pone enemigos mientras este santuario está
// activo y se los lleva al apagarse. Cuántos y de qué tipo se cambia ahí, como en cualquier campamento.
// ============================================================
public class MatchShrine : MonoBehaviour
{
    // Qué beneficio da. "Al azar" elige uno distinto cada vez que se activa.
    public enum EChoice
    {
        [InspectorName("Al azar")]   Random,
        [InspectorName("Daño")]      Damage,
        [InspectorName("Curación")]  Healing,
        [InspectorName("Defensa")]   Defense,
        [InspectorName("Velocidad")] Speed,
    }

    [Header("Zona")]
    [Tooltip("Hasta dónde llega la zona desde el centro.")]
    [Label("Radio (m)")]
    public float Radius = 10f;

    [Tooltip("Cuánto sube la zona desde el piso. Quien esté más alto (en un techo) no cuenta.")]
    [Label("Alto (m)")]
    public float Height = 6f;

    [Tooltip("Cuánto baja por debajo del centro, por si el piso no es parejo.")]
    [Label("Por debajo (m)")]
    public float Below = 1f;

    [Header("Beneficio")]
    [Label("Tipo")]
    public EChoice Type = EChoice.Random;

    [Header("Piezas")]
    [Tooltip("La zona negra y su borde: solo se ven mientras este santuario está activo.")]
    [Label("Zona")]
    public Transform Zone;

    [Tooltip("El cristal del altar: se pinta del color del tipo mientras está activo.")]
    [Label("Cristal")]
    public Renderer Crystal;

    [Tooltip("El borde de la zona, en el piso: también se pinta del color del tipo.")]
    [Label("Borde")]
    public Renderer Edge;

    [Tooltip("La luz del altar: se prende del color del tipo mientras está activo.")]
    [Label("Luz")]
    public Light Glow;

    [Tooltip("Alto de la nube negra de la zona.")]
    [Label("Alto de la nube (m)")]
    public float CloudHeight = 3.5f;

    [Tooltip("El color del cristal cuando el santuario está apagado.")]
    [Label("Color apagado")]
    public Color IdleColor = new Color(0.30f, 0.30f, 0.36f);

    [Tooltip("Cuánto brilla el cristal con luz propia (la emisión), sobre su color. Sin bloom en el " +
             "juego: mucho lo vuelve blanco.")]
    [Label("Brillo del cristal (×)")]
    public float CrystalGlow = 0.8f;

    [Tooltip("Lo que gira el cristal mientras el santuario está activo; apagado gira a un cuarto.")]
    [Label("Giro del cristal (°/s)")]
    public float CrystalSpin = 90f;

    [Tooltip("Dónde apunta el marcador del santuario en pantalla, sobre el centro.")]
    [Label("Altura del marcador (m)")]
    public float MarkerHeight = 3f;

    public Vector3 MarkerPoint => transform.position + Vector3.up * MarkerHeight;

    // ¿Es el santuario activo ahora? Vale en cualquier máquina (lo sincroniza MatchEvent_Shrines).
    public bool IsActive => MatchEvent_Shrines.Instance != null && MatchEvent_Shrines.Instance.ActiveShrine == this;

    // ¿Lo puso el campamento de enemigos extra de este santuario (un MercEnemySpawner hijo)?
    public bool OwnsEnemy(MercEnemyAI enemy) =>
        enemy != null && enemy.Spawner != null && enemy.Spawner.transform.IsChildOf(transform);

    // ¿Ese punto está dentro de la zona?
    public bool Contains(Vector3 point)
    {
        Vector3 d = point - transform.position;
        if (d.y < -Below || d.y > Height) return false;
        d.y = 0f;
        return d.sqrMagnitude <= Radius * Radius;
    }

    // =========================================================
    // TODOS LOS SANTUARIOS, EN EL MISMO ORDEN EN TODAS LAS MÁQUINAS
    // =========================================================

    private static readonly List<MatchShrine> s_all = new List<MatchShrine>();
    private static bool s_sorted;

    public static IReadOnlyList<MatchShrine> All
    {
        get
        {
            if (!s_sorted)
            {
                s_all.RemoveAll(s => s == null);
                s_all.Sort(CompareByPosition);
                s_sorted = true;
            }
            return s_all;
        }
    }

    public static MatchShrine ByIndex(int index) => index >= 0 && index < All.Count ? All[index] : null;

    public int Index
    {
        get
        {
            IReadOnlyList<MatchShrine> all = All;
            for (int i = 0; i < all.Count; i++) if (all[i] == this) return i;
            return -1;
        }
    }

    // Por posición redondeada al centímetro: x, después z, después y.
    private static int CompareByPosition(MatchShrine a, MatchShrine b)
    {
        Vector3Int pa = Vector3Int.RoundToInt(a.transform.position * 100f);
        Vector3Int pb = Vector3Int.RoundToInt(b.transform.position * 100f);
        if (pa.x != pb.x) return pa.x.CompareTo(pb.x);
        if (pa.z != pb.z) return pa.z.CompareTo(pb.z);
        return pa.y.CompareTo(pb.y);
    }

    private void OnEnable()
    {
        if (!s_all.Contains(this)) s_all.Add(this);
        s_sorted = false;
    }

    private void OnDisable()
    {
        s_all.Remove(this);
        s_sorted = false;
    }

    // =========================================================
    // LO QUE SE VE
    // =========================================================

    private MaterialPropertyBlock _block;
    private bool _shownActive = true;   // fuerza el primer refresco
    private Color _shownColor;

    private void Awake()
    {
        FitZone();
        Show(false, IdleColor);
    }

    private void Update()
    {
        bool active = IsActive;
        Color color = active ? MatchEvent_Shrines.Instance.GetColor(MatchEvent_Shrines.Instance.ActiveType) : IdleColor;

        if (active != _shownActive || color != _shownColor) Show(active, color);

        if (Crystal != null)
            Crystal.transform.Rotate(0f, CrystalSpin * (active ? 1f : 0.25f) * Time.deltaTime, 0f, Space.Self);
    }

    private void Show(bool active, Color color)
    {
        _shownActive = active;
        _shownColor  = color;

        if (Zone != null && Zone.gameObject.activeSelf != active) Zone.gameObject.SetActive(active);
        if (Glow != null)
        {
            Glow.enabled = active;
            Glow.color   = color;
        }

        _block ??= new MaterialPropertyBlock();
        Paint(Crystal, color, CrystalGlow);
        Paint(Edge, color, 0f);
    }

    // _EmissionColor solo hace algo si el material tiene la emisión prendida (el del cristal).
    private void Paint(Renderer target, Color color, float glow)
    {
        if (target == null) return;
        target.GetPropertyBlock(_block);
        _block.SetColor("_BaseColor", color);
        _block.SetColor("_EmissionColor", color * glow);
        target.SetPropertyBlock(_block);
    }

    // La zona se arma con mallas de radio 1: se escala al radio y la nube al alto.
    private void FitZone()
    {
        if (Zone == null) return;
        Zone.localScale = new Vector3(Radius, 1f, Radius);

        Transform cloud = Zone.Find("Cloud");
        if (cloud != null) cloud.localScale = new Vector3(1f, CloudHeight, 1f);
    }

    private void OnValidate()
    {
        Radius = Mathf.Max(0.5f, Radius);
        FitZone();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.6f, 0.2f, 1f, 0.8f);
        DrawCircle(transform.position + Vector3.up * 0.05f);
        Gizmos.color = new Color(0.6f, 0.2f, 1f, 0.3f);
        DrawCircle(transform.position + Vector3.up * Height);
    }

    private void DrawCircle(Vector3 center)
    {
        const int steps = 48;
        Vector3 last = center + new Vector3(Radius, 0f, 0f);
        for (int i = 1; i <= steps; i++)
        {
            float a = i / (float)steps * Mathf.PI * 2f;
            Vector3 next = center + new Vector3(Mathf.Cos(a) * Radius, 0f, Mathf.Sin(a) * Radius);
            Gizmos.DrawLine(last, next);
            last = next;
        }
    }
}
