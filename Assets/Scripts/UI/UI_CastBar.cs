using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_CastBar
//
// La barra de CANALIZAR del jugador local, como la de lanzamiento del WoW, bajo la mira:
//
//   · CARGA (Cast)    → amarilla, se LLENA hasta que la habilidad sale (el Golpe final
//                       del Inmortal, y lo que en el futuro tenga un tiempo de lanzamiento).
//   · CANALIZADO      → verde, se VACÍA mientras dura (el molinete del Berserker).
//   · INTERRUMPIDA    → se pone roja y dice "Interrupted" un momento antes de irse.
//
// NO HAY NADA QUE CABLEAR: se arma sola al empezar el juego (ver Bootstrap) y se dibuja
// con MercUIFactory, igual que UI_ChargeBar.
//
// QUIÉN LA PIDE: la habilidad, en el servidor, con GameplayAbility.ShowCastBar /
// HideCastBar, que llegan al dueño por TargetRpc (NetworkASC.ServerShowCastBar). La
// cuenta del tiempo corre en el cliente: arranca cuando llega el aviso.
// ============================================================
public class UI_CastBar : MonoBehaviour
{
    // =========================================================
    // PERILLAS
    // =========================================================

    [Tooltip("Dónde va la barra, en píxeles desde el centro de la pantalla (1920x1080). Un poco " +
             "más abajo que la barra de carga por etapas.")]
    public Vector2 Position = new Vector2(0f, -215f);

    public Vector2 Size = new Vector2(320f, 16f);

    public Color CastColor        = new Color(1.00f, 0.78f, 0.20f, 1f);
    public Color ChannelColor     = new Color(0.35f, 0.85f, 0.35f, 1f);
    public Color InterruptedColor = new Color(0.90f, 0.15f, 0.10f, 1f);

    [Tooltip("Segundos que queda la barra roja de \"Interrupted\" antes de irse.")]
    public float InterruptedSeconds = 0.6f;

    [Tooltip("Segundos que tarda en desvanecerse al terminar bien.")]
    public float FadeOutSeconds = 0.25f;

    // =========================================================
    // ACCESO
    // =========================================================

    private static UI_CastBar _instance;

    public static UI_CastBar Get()
    {
        if (_instance != null) return _instance;
        _instance = FindFirstObjectByType<UI_CastBar>(FindObjectsInactive.Include);
        if (_instance == null) _instance = new GameObject("UI_CastBar").AddComponent<UI_CastBar>();
        return _instance;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() => DontDestroyOnLoad(Get().gameObject);

    // Lo llama NetworkASC.TargetShowCastBar en el dueño.
    public static void Show(string label, float duration, bool channel) => Get().Begin(label, duration, channel);

    // Lo llama NetworkASC.TargetHideCastBar en el dueño.
    public static void Hide(bool interrupted) => Get().End(interrupted);

    // --- construido en runtime ---
    private RectTransform   _root;
    private CanvasGroup     _group;
    private Image           _fill;
    private Image           _flash;
    private TextMeshProUGUI _label;

    private enum EState { Hidden, Running, Done, Interrupted }
    private EState _state = EState.Hidden;
    private bool   _channel;
    private float  _startedAt;
    private float  _duration;
    private float  _endedAt;
    private string _name = "";

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

    private void Build()
    {
        Canvas canvas = MercUIFactory.CreateCanvas("CastBarCanvas", 45);
        canvas.transform.SetParent(transform, false);

        Vector2 center = new Vector2(0.5f, 0.5f);
        _root = MercUIFactory.CreateRect(canvas.transform, "CastBar", center, center, center, Position, Size);
        _group = _root.gameObject.AddComponent<CanvasGroup>();
        _group.alpha          = 0f;
        _group.interactable   = false;
        _group.blocksRaycasts = false;

        MercUIFactory.CreateImage(_root, "Frame", new Color(0f, 0f, 0f, 0.7f), Vector2.zero, new Vector2(6f, 6f),
                                  Vector2.zero, Vector2.one, center);
        MercUIFactory.CreateImage(_root, "Back", new Color(0.12f, 0.12f, 0.12f, 0.9f), Vector2.zero, Vector2.zero,
                                  Vector2.zero, Vector2.one, center);

        // El relleno se estira de izquierda a derecha moviendo su anchorMax.x.
        _fill = MercUIFactory.CreateImage(_root, "Fill", CastColor, Vector2.zero, Vector2.zero,
                                          Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f));

        _flash = MercUIFactory.CreateImage(_root, "Flash", new Color(1f, 1f, 1f, 0f), Vector2.zero, Vector2.zero,
                                           Vector2.zero, Vector2.one, center);

        _label = MercUIFactory.CreateText(_root, "Label", "", 16f, Color.white, TextAlignmentOptions.Center,
                                          new Vector2(0f, -5f), new Vector2(Size.x, 22f),
                                          new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f));
        MercUIFactory.AddShadow(_label);
    }

    private void Begin(string label, float duration, bool channel)
    {
        _name      = label ?? "";
        _duration  = Mathf.Max(0.05f, duration);
        _channel   = channel;
        _startedAt = Time.time;
        _state     = EState.Running;

        _fill.color   = channel ? ChannelColor : CastColor;
        _flash.color  = new Color(1f, 1f, 1f, 0f);
        _label.text   = _name;
        _group.alpha  = 1f;
    }

    private void End(bool interrupted)
    {
        if (_state != EState.Running && _state != EState.Done) return;

        _endedAt = Time.time;
        if (interrupted)
        {
            _state       = EState.Interrupted;
            _fill.color  = InterruptedColor;
            _fill.rectTransform.anchorMax = new Vector2(1f, 1f);
            _label.text  = Loc.T("castbar.interrupted");
        }
        else
        {
            _state       = EState.Done;
            _flash.color = new Color(1f, 1f, 1f, 0.8f);
        }
    }

    // =========================================================
    // CADA FRAME
    // =========================================================

    private void LateUpdate()
    {
        switch (_state)
        {
            case EState.Hidden:
                return;

            case EState.Running:
            {
                float t = Mathf.Clamp01((Time.time - _startedAt) / _duration);
                _fill.rectTransform.anchorMax = new Vector2(_channel ? 1f - t : t, 1f);

                // Si el aviso de fin no llega (se perdió, o la habilidad no lo manda), se
                // cierra sola un rato después de la duración.
                if (Time.time - _startedAt > _duration + 1f) End(false);
                break;
            }

            case EState.Done:
            {
                if (!_channel) _fill.rectTransform.anchorMax = new Vector2(1f, 1f);
                Color f = _flash.color;
                f.a = Mathf.MoveTowards(f.a, 0f, Time.unscaledDeltaTime * 4f);
                _flash.color = f;

                _group.alpha = 1f - Mathf.Clamp01((Time.time - _endedAt) / Mathf.Max(0.01f, FadeOutSeconds));
                if (_group.alpha <= 0f) _state = EState.Hidden;
                break;
            }

            case EState.Interrupted:
            {
                float k = (Time.time - _endedAt) / Mathf.Max(0.01f, InterruptedSeconds);
                _group.alpha = k < 0.6f ? 1f : 1f - Mathf.Clamp01((k - 0.6f) / 0.4f);
                if (k >= 1f) { _group.alpha = 0f; _state = EState.Hidden; }
                break;
            }
        }
    }
}
