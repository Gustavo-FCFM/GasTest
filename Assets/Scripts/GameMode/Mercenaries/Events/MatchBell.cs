using System.Collections.Generic;
using UnityEngine;

// ============================================================
// MatchBell
//
// Una CAMPANA del mapa (11 de noviembre de 2026, pedido de Gustavo): la que hay arriba de cada torre
// del Cementerio. Se toca GOLPEÁNDOLA con cualquier habilidad (desde el 11 de noviembre de 2026, a pedido de
// Gustavo; antes era acercándose): el equipo que la toca ve a través de las
// paredes a todos los enemigos que haya alrededor por unos segundos, y todo el mapa se entera (el aviso
// y el sonido). Después descansa un rato antes de poder volver a tocarse.
//
// Qué hace al sonar, cuánto dura y cuánto descansa lo decide MatchEvent_Bells (en el prefab de los
// eventos): este componente solo dice dónde está, la muestra lista o descansando y la hace balancearse.
//
// NO ES UN OBJETO DE RED, igual que los santuarios: cada pantalla las ordena por su posición (ver All)
// y el evento las nombra por ese número.
//
// PARA PONER UNA: arrastrar el prefab Prefabs/GameMode/Bell donde se quiera. En el Cementerio está
// dentro del prefab del sector, sobre el techo de la torre: tres en el mapa.
// ============================================================
public class MatchBell : MonoBehaviour
{
    [Header("Piezas")]
    [Tooltip("Lo que se balancea al sonar (la campana y su badajo), colgado de su eje.")]
    [Label("Se balancea")]
    public Transform Swing;

    [Tooltip("La campana: se pinta lista o descansando.")]
    [Label("Campana")]
    public Renderer BellRenderer;

    [Tooltip("La luz de la campana: prendida mientras se puede tocar.")]
    [Label("Luz")]
    public Light Glow;

    [Header("Cómo se ve")]
    [Tooltip("Cuánto brilla la campana lista (la emisión, sobre su color). Sin bloom en el juego: mucho la vuelve blanca.")]
    [Label("Brillo lista (×)")]
    public float ReadyGlow = 0.35f;

    [Tooltip("El color de la emisión con la campana lista.")]
    [Label("Color lista")]
    public Color ReadyColor = new Color(1f, 0.72f, 0.35f);

    [Tooltip("Cuánto se inclina al sonar, al principio.")]
    [Label("Balanceo (°)")]
    public float SwingAngle = 28f;

    [Tooltip("Cuántas idas y vueltas por segundo.")]
    [Label("Ritmo del balanceo (veces/s)")]
    public float SwingFrequency = 1.1f;

    [Tooltip("Cuánto tarda en quedarse quieta.")]
    [Label("Dura el balanceo (s)")]
    public float SwingSeconds = 3.5f;

    [Header("Al sonar")]
    [Tooltip("El sonido de la campana. Es 3D: para que lo oiga todo el mapa, \"Se escucha hasta\" grande (150 m).")]
    [Label("Sonido")]
    public SfxCue RingSound = new SfxCue { MaxDistance = 150f, PitchVariance = 0.03f };

    [Tooltip("Opcional: un efecto que aparece en la campana al sonar (una onda, polvo). Se borra solo a los 4 s.")]
    [Label("VFX al sonar")]
    public GameObject RingVfx;

    // De donde sale el sonido: el centro de la campana.
    public Vector3 RingPoint => BellRenderer != null ? BellRenderer.bounds.center : transform.position;

    // ¿Se puede tocar ahora? Vale en cualquier máquina (lo sincroniza MatchEvent_Bells).
    public bool IsReady => MatchEvent_Bells.Instance != null && MatchEvent_Bells.Instance.IsReady(Index);

    // =========================================================
    // TODAS LAS CAMPANAS, EN EL MISMO ORDEN EN TODAS LAS MÁQUINAS
    // =========================================================

    private static readonly List<MatchBell> s_all = new List<MatchBell>();
    private static bool s_sorted;

    public static IReadOnlyList<MatchBell> All
    {
        get
        {
            if (!s_sorted)
            {
                s_all.RemoveAll(b => b == null);
                s_all.Sort(CompareByPosition);
                s_sorted = true;
            }
            return s_all;
        }
    }

    public static MatchBell ByIndex(int index) => index >= 0 && index < All.Count ? All[index] : null;

    public int Index
    {
        get
        {
            IReadOnlyList<MatchBell> all = All;
            for (int i = 0; i < all.Count; i++) if (all[i] == this) return i;
            return -1;
        }
    }

    // Por posición redondeada al centímetro: x, después z, después y.
    private static int CompareByPosition(MatchBell a, MatchBell b)
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
    private bool _shownReady = true;    // fuerza el primer refresco
    private float _swingStarted = -100f;
    private Quaternion _swingRest = Quaternion.identity;

    private void Awake()
    {
        if (Swing != null) _swingRest = Swing.localRotation;
        Show(false);

        // Los golpes le llegan por su StrikeTarget (con un ASC y un collider en la capa Character, en el
        // cuerpo de la campana). Solo el servidor los recibe.
        StrikeTarget strike = GetComponent<StrikeTarget>();
        if (strike != null) strike.OnStruck += (source, ability) =>
        {
            if (MatchEvent_Bells.Instance != null) MatchEvent_Bells.Instance.ServerStrike(this, source);
        };
        else Debug.LogWarning($"[Campanas] {name} no tiene StrikeTarget: no se la puede golpear. Correr Mercenarios ▸ Campanas ▸ Armar las campanas de las torres.");
    }

    private void Update()
    {
        bool ready = IsReady;
        if (ready != _shownReady) Show(ready);

        if (Swing != null)
        {
            float t = Time.time - _swingStarted;
            float angle = 0f;
            if (t >= 0f && t < SwingSeconds)
            {
                // Una oscilación que se apaga: arranca fuerte y se queda quieta al terminar.
                float fade = 1f - t / Mathf.Max(0.01f, SwingSeconds);
                angle = SwingAngle * fade * fade * Mathf.Sin(t * SwingFrequency * Mathf.PI * 2f);
            }
            Swing.localRotation = _swingRest * Quaternion.Euler(angle, 0f, 0f);
        }
    }

    private void Show(bool ready)
    {
        _shownReady = ready;
        if (Glow != null) Glow.enabled = ready;

        if (BellRenderer == null) return;
        _block ??= new MaterialPropertyBlock();
        BellRenderer.GetPropertyBlock(_block);
        _block.SetColor("_EmissionColor", ready ? ReadyColor * ReadyGlow : Color.black);
        BellRenderer.SetPropertyBlock(_block);
    }

    // Sonó (lo llama MatchEvent_Bells en cada pantalla): el balanceo, el sonido y el VFX.
    public void PlayRing()
    {
        _swingStarted = Time.time;
        AudioManager.Play(RingSound, RingPoint);

        if (RingVfx != null)
        {
            GameObject vfx = Instantiate(RingVfx, RingPoint, Quaternion.identity);
            Destroy(vfx, 4f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Hasta dónde revela, en el piso de la campana (el radio está en el evento).
        float radius = 25f;
        MatchEvent_Bells ev = MatchEvent_Bells.Instance != null ? MatchEvent_Bells.Instance : FindAnyObjectByType<MatchEvent_Bells>();
        if (ev != null) radius = ev.RevealRadius;

        Gizmos.color = new Color(1f, 0.7f, 0.3f, 0.35f);
        Vector3 center = transform.position;
        const int steps = 64;
        Vector3 last = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= steps; i++)
        {
            float a = i / (float)steps * Mathf.PI * 2f;
            Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(last, next);
            last = next;
        }
    }
}
