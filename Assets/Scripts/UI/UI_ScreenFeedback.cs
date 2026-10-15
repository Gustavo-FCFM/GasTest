using TMPro;
using UnityEngine;

// ============================================================
// UI_ScreenFeedback
//
// Lo que la pantalla le dice al jugador LOCAL sobre la partida:
//
//   · MORISTE        → quién te eliminó (con su clase y el color de su equipo) y la
//                      cuenta regresiva para reaparecer. Sin esto uno moría sin saber
//                      por qué ni cuánto faltaba.
//   · AVISOS CORTOS  → un texto que aparece un momento bajo la mira. Lo usa la
//                      definitiva al quedar cargada ("¡DEFINITIVA LISTA!").
//
// (La viñeta de poca vida no está acá: vive en el prefab de la cámara, UI_LowHealthVignette.)
//
// VIVE EN EL PREFAB DE PARTIDA (Match_UI ▸ ScreenFeedback), desde el 14 de octubre de 2026:
// la franja de muerte (fondo, quién te mató, cuenta regresiva) y el aviso corto se
// acomodan y se les cambia el tamaño ahí. Este script solo escribe los textos y los
// muestra y esconde. Es de la PARTIDA y no del personaje: tiene que seguir en pantalla
// mientras estás muerto.
//
// QUIÉN LO LLAMA: la pantalla de muerte la pide el servidor por TargetRpc
// (PlayerController.TargetShowDeath), porque solo él sabe quién dio el golpe final.
// ============================================================
public class UI_ScreenFeedback : MonoBehaviour
{
    [Header("Piezas: pantalla de muerte")]
    [Tooltip("El CanvasGroup de la franja de muerte: la muestra y la esconde.")]
    [Label("Grupo de la muerte")]
    public CanvasGroup DeathGroup;

    [Tooltip("\"Te eliminó X\" (o \"Moriste\").")]
    [Label("Quién te mató")]
    public TMP_Text DeathTitle;

    [Tooltip("\"Reapareces en N\".")]
    [Label("Cuenta regresiva")]
    public TMP_Text DeathCountdown;

    [Header("Piezas: aviso corto")]
    [Tooltip("El CanvasGroup del aviso corto bajo la mira.")]
    [Label("Grupo del aviso")]
    public CanvasGroup ToastGroup;

    [Label("Texto del aviso")]
    public TMP_Text ToastText;

    [Header("Tiempos")]
    [Tooltip("Si no hay reaparición (o no se sabe cuándo), cuánto se muestra el aviso de muerte.")]
    [Label("Aviso de muerte sin cuenta (s)")]
    public float DeathFallbackSeconds = 3f;

    [Tooltip("Cuánto dura el aviso corto: entero la primera mitad, después se apaga.")]
    [Label("Aviso corto dura (s)")]
    public float ToastSeconds = 1.6f;

    // =========================================================
    // ACCESO
    // =========================================================

    private static UI_ScreenFeedback _instance;
    private static bool _warnedMissing;

    // El de la escena (el prefab de partida). Null si la escena no lo tiene: avisa una vez.
    public static UI_ScreenFeedback Get()
    {
        if (_instance != null) return _instance;
        _instance = FindFirstObjectByType<UI_ScreenFeedback>(FindObjectsInactive.Include);
        if (_instance == null && !_warnedMissing)
        {
            _warnedMissing = true;
            Debug.LogWarning("[UI_ScreenFeedback] La escena no tiene el prefab de partida (Match_UI): " +
                             "no hay pantalla de muerte ni avisos cortos.");
        }
        return _instance;
    }

    private float _deathShownAt = -1f;
    private float _deathEndsAt  = -1f;
    private bool  _deathHasCountdown;
    private float _toastAlpha;

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;

        if (DeathGroup != null) { DeathGroup.alpha = 0f; DeathGroup.blocksRaycasts = false; DeathGroup.interactable = false; }
        if (ToastGroup != null) { ToastGroup.alpha = 0f; ToastGroup.blocksRaycasts = false; ToastGroup.interactable = false; }
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
        if (DeathGroup == null || DeathTitle == null) return;

        string title;
        if (string.IsNullOrEmpty(killerName))
        {
            title = Loc.T("death.you_died");
        }
        else
        {
            string hex   = ColorUtility.ToHtmlStringRGB(MercUIFactory.TeamColor(killerTeam));
            string klass = string.IsNullOrEmpty(killerClass) ? "" : $" <size=70%>({killerClass})</size>";
            title = Loc.T("death.killed_by", ("killer", $"<color=#{hex}>{killerName}</color>{klass}"));
        }

        DeathTitle.text    = title;
        _deathHasCountdown = respawnSeconds > 0f;
        _deathShownAt      = Time.time;
        _deathEndsAt       = Time.time + (_deathHasCountdown ? respawnSeconds : DeathFallbackSeconds);
        if (DeathCountdown != null) DeathCountdown.text = "";
        DeathGroup.alpha   = 1f;
    }

    // Un aviso corto bajo la mira.
    public void ShowToast(string text, Color color)
    {
        if (ToastText == null) return;
        ToastText.text  = text;
        ToastText.color = color;
        _toastAlpha     = 1f;
    }

    // =========================================================
    // CADA FRAME
    // =========================================================

    private void Update()
    {
        UpdateDeath();
        UpdateToast();
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
        if (_deathShownAt < 0f || DeathGroup == null) return;

        float remaining = _deathEndsAt - Time.time;

        // Se va al terminar la cuenta, o antes si ya volviste (una resurrección, la
        // definitiva del Inmortal). El medio segundo de gracia es porque la muerte llega
        // por RPC y el estado "muerto" por otro camino: sin él, un aviso recién llegado
        // podía ver todavía la vida vieja y cerrarse solo.
        bool alive = Time.time - _deathShownAt > 0.5f && IsLocalAlive();
        if (remaining <= 0f || alive)
        {
            _deathShownAt    = -1f;
            DeathGroup.alpha = 0f;
            return;
        }

        if (_deathHasCountdown && DeathCountdown != null)
        {
            string text = Loc.T("death.respawning", ("seconds", Mathf.CeilToInt(remaining)));
            if (DeathCountdown.text != text) DeathCountdown.text = text;
        }
    }

    private bool IsLocalAlive()
    {
        if (!TryGetLocalHealth(out float health, out _)) return false;
        return health > 0f && !_localAsc.HasTag(EGameplayTag.State_Dead);
    }

    private void UpdateToast()
    {
        if (ToastGroup == null) return;
        if (_toastAlpha <= 0f) { ToastGroup.alpha = 0f; return; }

        _toastAlpha = Mathf.Max(0f, _toastAlpha - Time.deltaTime / Mathf.Max(0.1f, ToastSeconds));

        // Entero durante la primera mitad, y después se apaga: se alcanza a leer.
        ToastGroup.alpha = Mathf.Clamp01(_toastAlpha * 2f);
    }
}
