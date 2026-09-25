using TMPro;
using UnityEngine;

// Qué clase de número es. Viaja por red como byte (ver NetworkAbilitySystemComponent →
// NÚMEROS FLOTANTES), así que si se agrega uno va AL FINAL.
public enum ECombatNumber : byte
{
    Damage         = 0,   // rojo
    CriticalDamage = 1,   // amarillo, más grande
    Heal           = 2,   // verde, con "+"
    DamageOverTime = 3,   // rojo más claro y más chico (venenos, heridas, quemaduras)
}

// ============================================================
// UI_DamageNumbers
//
// Los números que saltan sobre un personaje cuando le pegan o lo curan: rojo el daño,
// amarillo el crítico, verde la curación, y más chicos los del daño con el tiempo.
// Salen sobre el que RECIBE, suben un poco y se apagan.
//
// CÓMO SE DIBUJAN: en un canvas de pantalla, no en el mundo. Cada número guarda el
// punto del mundo donde nació y cada frame se proyecta a la pantalla, igual que los
// marcadores del Objetivo. Así se leen siempre nítidos, del mismo tamaño y de frente,
// y no se esconden detrás de una pared (el que te pega detrás de una columna igual te
// muestra cuánto te hizo).
//
// Son un conjunto FIJO de textos que se reciclan: una pelea de nueve no crea y destruye
// objetos a cada golpe. Si se acaban, se reusa el más viejo.
//
// NO HAY NADA QUE CABLEAR: se arma solo la primera vez que llega un número.
// ============================================================
public class UI_DamageNumbers : MonoBehaviour
{
    [Header("Colores")]
    public Color DamageColor         = new Color(1f, 0.28f, 0.22f, 1f);
    public Color CriticalColor       = new Color(1f, 0.85f, 0.1f, 1f);
    public Color HealColor           = new Color(0.35f, 1f, 0.4f, 1f);
    public Color DamageOverTimeColor = new Color(1f, 0.55f, 0.5f, 1f);

    [Header("Tamaños")]
    public float DamageFontSize         = 30f;
    public float CriticalFontSize       = 42f;
    public float HealFontSize           = 28f;
    public float DamageOverTimeFontSize = 21f;

    [Header("Movimiento")]
    [Tooltip("A qué altura nacen, como fracción entre los pies (0) y la barra de vida (1). " +
             "0.6 = a la altura del pecho, que es donde uno apunta casi siempre.")]
    [Range(0f, 1.2f)] public float HeightFraction = 0.6f;

    [Tooltip("Cuánto dura cada número en pantalla, en segundos.")]
    public float Lifetime = 0.9f;

    [Tooltip("Cuánto sube en el mundo mientras dura, en metros. Poco: que no se vaya de " +
             "la vista si estás apuntando al pecho.")]
    public float RiseMeters = 0.6f;

    [Tooltip("Desvío al azar a los costados al nacer, en metros: varios golpes seguidos " +
             "no salen uno encima del otro.")]
    public float Jitter = 0.45f;

    [Header("Distancia")]
    [Tooltip("Más lejos que esto (desde la cámara) no se dibujan: en una pelea grande, " +
             "los números del otro lado del mapa son ruido.")]
    public float MaxDistance = 40f;

    [Tooltip("A esta distancia el número se ve de su tamaño normal; más lejos se achica " +
             "(hasta la mitad), más cerca no crece.")]
    public float ReferenceDistance = 10f;

    [Tooltip("Cuántos números puede haber a la vez.")]
    public int PoolSize = 48;

    private class Popup
    {
        public RectTransform   Rect;
        public TextMeshProUGUI Text;
        public Vector3 World;
        public float   BornAt;
        public float   Pop;       // cuánto "salta" al aparecer (los críticos más)
        public bool    Active;
    }

    private static UI_DamageNumbers _instance;

    public static UI_DamageNumbers Get()
    {
        if (_instance != null) return _instance;

        _instance = FindFirstObjectByType<UI_DamageNumbers>(FindObjectsInactive.Include);
        if (_instance != null) return _instance;

        GameObject go = new GameObject("UI_DamageNumbers");
        _instance = go.AddComponent<UI_DamageNumbers>();
        return _instance;
    }

    private Canvas  _canvas;
    private Popup[] _pool;
    private Camera  _cam;

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;

        // Encima de los marcadores del mundo (45) y debajo del HUD de la partida.
        _canvas = MercUIFactory.CreateCanvas("DamageNumbersCanvas", 48);
        _canvas.transform.SetParent(transform, false);

        _pool = new Popup[Mathf.Max(8, PoolSize)];
        for (int i = 0; i < _pool.Length; i++) _pool[i] = BuildPopup(i);
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // =========================================================
    // LO QUE SE LE PIDE DESDE AFUERA
    // =========================================================

    // Un número nuevo sobre un personaje: 'feet' son sus pies y 'top' la altura de su
    // barra de vida; nace a HeightFraction entre los dos.
    public void Spawn(Vector3 feet, Vector3 top, int amount, ECombatNumber kind)
        => Spawn(Vector3.Lerp(feet, top, HeightFraction), amount, kind);

    // Un número nuevo en 'worldPosition'.
    public void Spawn(Vector3 worldPosition, int amount, ECombatNumber kind)
    {
        Camera cam = ResolveCamera();
        if (cam == null || amount <= 0) return;
        if (Vector3.Distance(cam.transform.position, worldPosition) > MaxDistance) return;

        Popup p = TakePopup();

        Vector2 jitter = Random.insideUnitCircle * Jitter;
        p.World  = worldPosition + new Vector3(jitter.x, Random.Range(-0.1f, 0.15f), jitter.y);
        p.BornAt = Time.time;
        p.Active = true;

        switch (kind)
        {
            case ECombatNumber.CriticalDamage:
                Configure(p, $"{amount}!", CriticalColor, CriticalFontSize, 0.6f);
                break;
            case ECombatNumber.Heal:
                Configure(p, $"+{amount}", HealColor, HealFontSize, 0.25f);
                break;
            case ECombatNumber.DamageOverTime:
                Configure(p, amount.ToString(), DamageOverTimeColor, DamageOverTimeFontSize, 0.1f);
                break;
            default:
                Configure(p, amount.ToString(), DamageColor, DamageFontSize, 0.3f);
                break;
        }

        p.Rect.gameObject.SetActive(true);
    }

    private static void Configure(Popup p, string text, Color color, float size, float pop)
    {
        p.Text.text     = text;
        p.Text.color    = color;
        p.Text.fontSize = size;
        p.Pop           = pop;
    }

    // =========================================================
    // CADA FRAME
    // =========================================================

    private void LateUpdate()
    {
        Camera cam = ResolveCamera();
        float scaleFactor = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;

        foreach (Popup p in _pool)
        {
            if (!p.Active) continue;

            float t = (Time.time - p.BornAt) / Mathf.Max(0.05f, Lifetime);
            if (t >= 1f || cam == null) { Hide(p); continue; }

            // Sube rápido al principio y frena: se lee como un salto, no como un ascensor.
            float rise = 1f - (1f - t) * (1f - t);
            Vector3 world = p.World + Vector3.up * (RiseMeters * rise);

            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z <= 0f)
            {
                // Detrás de la cámara: no se dibuja, pero sigue su vida por si giras.
                if (p.Rect.gameObject.activeSelf) p.Rect.gameObject.SetActive(false);
                continue;
            }
            if (!p.Rect.gameObject.activeSelf) p.Rect.gameObject.SetActive(true);

            p.Rect.anchoredPosition = new Vector2(screen.x, screen.y) / scaleFactor;

            // Tamaño: un salto al nacer (más grande en los críticos) y más chico de lejos.
            float pop      = 1f + p.Pop * Mathf.Clamp01(1f - t / 0.15f);
            float distance = Mathf.Max(0.01f, screen.z);
            float far      = Mathf.Clamp(ReferenceDistance / distance, 0.5f, 1f);
            p.Rect.localScale = Vector3.one * (pop * far);

            // Entero la primera mitad; después se apaga.
            Color c = p.Text.color;
            c.a = t < 0.55f ? 1f : 1f - (t - 0.55f) / 0.45f;
            p.Text.color = c;
        }
    }

    private Camera ResolveCamera()
    {
        // Mismo criterio que los nameplates: una cámara apagada (la del lobby, al
        // spawnear) no es null, así que también se re-resuelve si está desactivada.
        if (_cam == null || !_cam.isActiveAndEnabled) _cam = Camera.main;
        return _cam;
    }

    // =========================================================
    // CONJUNTO DE TEXTOS
    // =========================================================

    private Popup BuildPopup(int index)
    {
        // Anclado abajo-izquierda: la posición en pantalla se escribe tal cual (dividida
        // por el factor de escala del canvas), como en UI_ObjectiveMarker.
        RectTransform rt = MercUIFactory.CreateRect(_canvas.transform, $"Number_{index}",
            Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 60f));

        TextMeshProUGUI text = MercUIFactory.CreateText(rt, "Text", "", DamageFontSize, Color.white,
            TextAlignmentOptions.Center, Vector2.zero, new Vector2(200f, 60f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        text.fontStyle     = FontStyles.Bold;
        text.raycastTarget = false;
        MercUIFactory.AddShadow(text, 2f);

        rt.gameObject.SetActive(false);
        return new Popup { Rect = rt, Text = text };
    }

    // Uno libre, o si no hay, el más viejo (un golpe nuevo importa más que uno que ya
    // se está apagando).
    private Popup TakePopup()
    {
        Popup oldest = _pool[0];
        foreach (Popup p in _pool)
        {
            if (!p.Active) return p;
            if (p.BornAt < oldest.BornAt) oldest = p;
        }
        return oldest;
    }

    private static void Hide(Popup p)
    {
        p.Active = false;
        if (p.Rect.gameObject.activeSelf) p.Rect.gameObject.SetActive(false);
    }
}
