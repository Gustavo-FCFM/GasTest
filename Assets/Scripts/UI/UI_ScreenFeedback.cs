using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_ScreenFeedback
//
// Lo que la pantalla le dice al jugador LOCAL sobre su propio estado:
//
//   · POCA VIDA      → el borde de la pantalla late en rojo por debajo de un umbral.
//                      Más rojo y más rápido cuanto menos vida queda. Avisa "retírate"
//                      sin tener que mirar la barra del HUD.
//   · MORISTE        → quién te eliminó (con su clase y el color de su equipo) y la
//                      cuenta regresiva para reaparecer. Sin esto uno moría sin saber
//                      por qué ni cuánto faltaba.
//   · AVISOS CORTOS  → un texto que aparece un momento bajo la mira. Lo usa la
//                      definitiva al quedar cargada ("¡DEFINITIVA LISTA!").
//
// NO HAY NADA QUE CABLEAR: se arma solo al empezar el juego (ver Bootstrap) y se
// dibuja con MercUIFactory, igual que UI_CombatFeedback.
//
// QUIÉN LO LLAMA: la viñeta se mira sola cada frame (la vida del jugador local ya está
// sincronizada). La pantalla de muerte la pide el servidor por TargetRpc
// (PlayerController.TargetShowDeath), porque solo él sabe quién dio el golpe final.
// ============================================================
public class UI_ScreenFeedback : MonoBehaviour
{
    // =========================================================
    // PERILLAS
    // =========================================================

    [Header("Viñeta de poca vida")]
    [Tooltip("Por debajo de esta fracción de vida empieza a verse la viñeta. 0.3 = 30 %.")]
    [Range(0f, 1f)] public float LowHealthThreshold = 0.3f;

    [Tooltip("Opacidad máxima del borde, con la vida casi en cero.")]
    [Range(0f, 1f)] public float VignetteMaxAlpha = 0.6f;

    [Tooltip("Latidos por segundo: el primero es justo en el umbral, el segundo casi muerto.")]
    public Vector2 PulseSpeed = new Vector2(1.2f, 3f);

    [Tooltip("Qué tan ancho es el borde: 0 = todo rojo, 1 = casi nada. El centro de la " +
             "pantalla siempre queda limpio.")]
    [Range(0f, 0.95f)] public float VignetteInnerRadius = 0.55f;

    public Color VignetteColor = new Color(0.85f, 0.05f, 0.05f, 1f);

    [Header("Pantalla de muerte")]
    [Tooltip("Si no hay reaparición (o no se sabe cuándo), cuánto se muestra el aviso.")]
    public float DeathFallbackSeconds = 3f;

    [Header("Avisos cortos")]
    public float ToastSeconds = 1.6f;

    // =========================================================
    // ACCESO
    // =========================================================

    private static UI_ScreenFeedback _instance;

    public static UI_ScreenFeedback Get()
    {
        if (_instance != null) return _instance;

        _instance = FindFirstObjectByType<UI_ScreenFeedback>(FindObjectsInactive.Include);
        if (_instance != null) return _instance;

        GameObject go = new GameObject("UI_ScreenFeedback");
        _instance = go.AddComponent<UI_ScreenFeedback>();
        return _instance;
    }

    // La viñeta tiene que existir antes de que pase nada (se mira sola cada frame), así
    // que se crea al arrancar el juego en vez de esperar a que alguien la pida.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        UI_ScreenFeedback feedback = Get();
        DontDestroyOnLoad(feedback.gameObject);
    }

    // --- construido en runtime ---
    private Canvas _canvas;
    private Image  _vignette;

    private CanvasGroup     _deathGroup;
    private TextMeshProUGUI _deathTitle;
    private TextMeshProUGUI _deathCountdown;
    private float _deathShownAt = -1f;
    private float _deathEndsAt  = -1f;
    private bool  _deathHasCountdown;

    private CanvasGroup     _toastGroup;
    private TextMeshProUGUI _toastText;
    private float _toastAlpha;

    private float _pulsePhase;

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;

        Build();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // =========================================================
    // LO QUE SE LE PIDE DESDE AFUERA
    // =========================================================

    // Moriste. killerName vacío = no hubo atacante (el entorno, o te mataste solo).
    // respawnSeconds <= 0 = no hay cuenta regresiva que mostrar.
    public void ShowDeath(string killerName, string killerClass, int killerTeam, float respawnSeconds)
    {
        string title;
        if (string.IsNullOrEmpty(killerName))
        {
            title = "YOU DIED";
        }
        else
        {
            string hex   = ColorUtility.ToHtmlStringRGB(MercUIFactory.TeamColor(killerTeam));
            string klass = string.IsNullOrEmpty(killerClass) ? "" : $" <size=70%>({killerClass})</size>";
            title = $"KILLED BY <color=#{hex}>{killerName}</color>{klass}";
        }

        _deathTitle.text   = title;
        _deathHasCountdown = respawnSeconds > 0f;
        _deathShownAt      = Time.time;
        _deathEndsAt       = Time.time + (_deathHasCountdown ? respawnSeconds : DeathFallbackSeconds);
        _deathCountdown.text = "";
        _deathGroup.alpha  = 1f;
    }

    // Un aviso corto bajo la mira.
    public void ShowToast(string text, Color color)
    {
        _toastText.text  = text;
        _toastText.color = color;
        _toastAlpha      = 1f;
    }

    // =========================================================
    // CADA FRAME
    // =========================================================

    private void Update()
    {
        UpdateVignette();
        UpdateDeath();
        UpdateToast();
    }

    private void UpdateVignette()
    {
        float target = 0f;
        float speed  = PulseSpeed.x;

        if (TryGetLocalHealth(out float health, out float max) && max > 0f && health > 0f)
        {
            float fraction = health / max;
            if (fraction < LowHealthThreshold && LowHealthThreshold > 0f)
            {
                // 0 justo en el umbral, 1 con la vida casi en cero.
                float danger = 1f - fraction / LowHealthThreshold;
                target = Mathf.Lerp(0.35f, 1f, danger);
                speed  = Mathf.Lerp(PulseSpeed.x, PulseSpeed.y, danger);
            }
        }

        // Latido: nunca baja del 65 % de su intensidad, así se lee como un pulso y no
        // como algo que parpadea.
        _pulsePhase += Time.deltaTime * speed * Mathf.PI * 2f;
        float pulse = 0.825f + 0.175f * Mathf.Sin(_pulsePhase);

        Color c = VignetteColor;
        c.a = target * pulse * VignetteMaxAlpha;
        _vignette.color = c;
        _vignette.enabled = c.a > 0.001f;
    }

    // Los componentes del jugador local, cacheados: esto corre cada frame y el jugador
    // local solo cambia al reaparecer o cambiar de clase.
    private PlayerController              _cachedLocal;
    private AbilitySystemComponent        _localAsc;
    private NetworkAbilitySystemComponent _localNet;

    private bool ResolveLocal()
    {
        PlayerController local = PlayerController.LocalPlayer;
        if (local != _cachedLocal)
        {
            _cachedLocal = local;
            _localAsc    = local != null ? local.GetComponent<AbilitySystemComponent>()        : null;
            _localNet    = local != null ? local.GetComponent<NetworkAbilitySystemComponent>() : null;
        }
        return _localAsc != null;
    }

    // La vida del jugador local: la sincronizada por red si existe (sirve igual en el
    // host y en un cliente), si no la del ASC.
    private bool TryGetLocalHealth(out float health, out float max)
    {
        health = max = 0f;
        if (!ResolveLocal()) return false;

        if (_localNet != null)
        {
            health = _localNet.NetHealth;
            max    = _localNet.NetMaxHealth;
            return true;
        }

        health = _localAsc.GetAttributeValue(EAttributeType.Health);
        max    = _localAsc.GetAttributeValue(EAttributeType.MaxHealth);
        return true;
    }

    private void UpdateDeath()
    {
        if (_deathShownAt < 0f) return;

        float remaining = _deathEndsAt - Time.time;

        // Se va al terminar la cuenta, o antes si ya volviste (una resurrección, la
        // definitiva del Inmortal). El medio segundo de gracia es porque la muerte llega
        // por RPC y el estado "muerto" por otro camino: sin él, un aviso recién llegado
        // podía ver todavía la vida vieja y cerrarse solo.
        bool alive = Time.time - _deathShownAt > 0.5f && IsLocalAlive();
        if (remaining <= 0f || alive)
        {
            _deathShownAt     = -1f;
            _deathGroup.alpha = 0f;
            return;
        }

        if (_deathHasCountdown)
        {
            string text = $"Respawning in {Mathf.CeilToInt(remaining)}";
            if (_deathCountdown.text != text) _deathCountdown.text = text;
        }
    }

    private bool IsLocalAlive()
    {
        if (!TryGetLocalHealth(out float health, out _)) return false;
        return health > 0f && !_localAsc.HasTag(EGameplayTag.State_Dead);
    }

    private void UpdateToast()
    {
        if (_toastAlpha <= 0f) { _toastGroup.alpha = 0f; return; }

        _toastAlpha = Mathf.Max(0f, _toastAlpha - Time.deltaTime / Mathf.Max(0.1f, ToastSeconds));

        // Entero durante la primera mitad, y después se apaga: se alcanza a leer.
        _toastGroup.alpha = Mathf.Clamp01(_toastAlpha * 2f);
    }

    // =========================================================
    // CONSTRUCCIÓN
    // =========================================================

    private void Build()
    {
        // Debajo del feedback de combate (120) y de los menús, encima del HUD.
        _canvas = MercUIFactory.CreateCanvas("ScreenFeedbackCanvas", 110);
        _canvas.transform.SetParent(transform, false);

        BuildVignette();
        BuildDeathPanel();
        BuildToast();
    }

    // La viñeta es UNA imagen que cubre la pantalla con una textura hecha en código:
    // transparente en el centro y opaca hacia los bordes. Estirada a la pantalla queda
    // ovalada, que es justo la forma de una viñeta. El proyecto no trae ningún sprite así.
    private void BuildVignette()
    {
        RectTransform rt = MercUIFactory.CreateRect(_canvas.transform, "LowHealthVignette",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        _vignette = rt.gameObject.AddComponent<Image>();
        _vignette.sprite        = BuildVignetteSprite(VignetteInnerRadius);
        _vignette.raycastTarget = false;
        _vignette.enabled       = false;
    }

    private static Sprite BuildVignetteSprite(float inner)
    {
        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode   = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Distancia al centro, 0 en el medio y 1 en el centro de cada borde (las
                // esquinas pasan de 1: ahí queda lo más rojo).
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float d  = Mathf.Sqrt(dx * dx + dy * dy);

                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner, 1.25f, d));
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // Una franja oscura un poco debajo del centro: quién te eliminó y cuánto falta.
    private void BuildDeathPanel()
    {
        Vector2 mid = new Vector2(0.5f, 0.5f);
        RectTransform panel = MercUIFactory.CreateRect(_canvas.transform, "DeathPanel",
            new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.3f), mid, Vector2.zero, new Vector2(620f, 96f));

        _deathGroup = panel.gameObject.AddComponent<CanvasGroup>();
        _deathGroup.alpha = 0f;
        _deathGroup.blocksRaycasts = false;
        _deathGroup.interactable   = false;

        MercUIFactory.CreateImage(panel, "Background", new Color(0f, 0f, 0f, 0.6f),
            Vector2.zero, new Vector2(620f, 96f), mid, mid, mid);

        _deathTitle = MercUIFactory.CreateText(panel, "Killer", "", 30f, Color.white,
            TextAlignmentOptions.Center, new Vector2(0f, 16f), new Vector2(600f, 44f), mid, mid, mid);
        _deathTitle.fontStyle = FontStyles.Bold;
        _deathTitle.richText  = true;
        MercUIFactory.AddShadow(_deathTitle);

        _deathCountdown = MercUIFactory.CreateText(panel, "Countdown", "", 22f,
            new Color(0.85f, 0.85f, 0.85f, 1f), TextAlignmentOptions.Center,
            new Vector2(0f, -24f), new Vector2(600f, 30f), mid, mid, mid);
        MercUIFactory.AddShadow(_deathCountdown);
    }

    private void BuildToast()
    {
        Vector2 mid = new Vector2(0.5f, 0.5f);
        RectTransform holder = MercUIFactory.CreateRect(_canvas.transform, "Toast",
            mid, mid, mid, new Vector2(0f, -110f), new Vector2(700f, 50f));

        _toastGroup = holder.gameObject.AddComponent<CanvasGroup>();
        _toastGroup.alpha = 0f;
        _toastGroup.blocksRaycasts = false;
        _toastGroup.interactable   = false;

        _toastText = MercUIFactory.CreateText(holder, "Text", "", 30f, Color.white,
            TextAlignmentOptions.Center, Vector2.zero, new Vector2(700f, 50f), mid, mid, mid);
        _toastText.fontStyle = FontStyles.Bold;
        MercUIFactory.AddShadow(_toastText);
    }
}
