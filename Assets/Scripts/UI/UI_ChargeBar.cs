using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_ChargeBar
//
// La barra de carga por ETAPAS del jugador local, como la de "potenciar" del Dracthyr
// del WoW: mientras se mantiene un ataque cargado (GA_ChargedAttack, también dentro de
// un GA_HoldTagSwitch), aparece bajo la mira partida en un tramo por etapa. Cada tramo se
// llena mientras se está en esa etapa; al pasar a la siguiente, el tramo destella y la
// barra da un saltito. El último tramo es la cuenta hasta que se suelta sola.
//
// NO HAY NADA QUE CABLEAR: se arma sola al empezar el juego (ver Bootstrap) y se dibuja
// con MercUIFactory, igual que UI_ScreenFeedback.
//
// DE DÓNDE SALE: PlayerController.HeldAbility / HoldStartedAt (lo que sostiene el dueño
// y desde cuándo) y GameplayAbility.GetChargeStages (dónde empieza cada etapa). Se
// cuenta en el dueño, sin red: el servidor arranca la carga al mismo tiempo, con el
// retraso del viaje nada más.
// ============================================================
public class UI_ChargeBar : MonoBehaviour
{
    // =========================================================
    // PERILLAS
    // =========================================================

    [Tooltip("Dónde va la barra, en píxeles desde el centro de la pantalla (1920x1080).")]
    public Vector2 Position = new Vector2(0f, -170f);

    public Vector2 Size = new Vector2(380f, 20f);

    [Tooltip("Espacio entre tramos.")]
    public float SegmentGap = 4f;

    [Tooltip("Color de cada etapa, en orden. Si hay más etapas que colores, repite el último.")]
    public Color[] StageColors =
    {
        new Color(0.95f, 0.85f, 0.55f, 1f),   // 1: dorado pálido
        new Color(1.00f, 0.60f, 0.15f, 1f),   // 2: naranja
        new Color(1.00f, 0.25f, 0.10f, 1f),   // 3: rojo
    };

    [Tooltip("Segundos que tarda en desvanecerse al soltar.")]
    public float FadeOutSeconds = 0.25f;

    // =========================================================
    // ACCESO
    // =========================================================

    private static UI_ChargeBar _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        _instance = FindFirstObjectByType<UI_ChargeBar>(FindObjectsInactive.Include);
        if (_instance == null) _instance = new GameObject("UI_ChargeBar").AddComponent<UI_ChargeBar>();
        DontDestroyOnLoad(_instance.gameObject);
    }

    // --- construido en runtime ---
    private class Segment
    {
        public RectTransform Rect;
        public Image Back;
        public Image Fill;
        public Image Flash;
        public TextMeshProUGUI Number;
        public float From, To;      // segundos de carga que cubre
    }

    private Canvas          _canvas;
    private RectTransform   _root;
    private CanvasGroup     _group;
    private RectTransform   _segmentsRoot;
    private TextMeshProUGUI _label;
    private readonly List<Segment> _segments = new List<Segment>();

    private readonly List<float> _stageTimes  = new List<float>();
    private readonly List<float> _builtTimes  = new List<float>();
    private float _builtMax = -1f;

    private int   _lastStage = -1;
    private float _shownStart = -1f;  // HoldStartedAt de la carga que se está mostrando
    private float _punch;            // 1 al llegar a una etapa, baja a 0

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
        _canvas = MercUIFactory.CreateCanvas("ChargeBarCanvas", 45);
        _canvas.transform.SetParent(transform, false);

        Vector2 center = new Vector2(0.5f, 0.5f);
        _root = MercUIFactory.CreateRect(_canvas.transform, "ChargeBar", center, center, center,
                                         Position, Size);
        _group = _root.gameObject.AddComponent<CanvasGroup>();
        _group.alpha          = 0f;
        _group.interactable   = false;
        _group.blocksRaycasts = false;

        // Marco oscuro, un poco más grande que la barra.
        MercUIFactory.CreateImage(_root, "Frame", new Color(0f, 0f, 0f, 0.65f),
                                  Vector2.zero, new Vector2(6f, 6f),
                                  Vector2.zero, Vector2.one, center);

        _segmentsRoot = MercUIFactory.CreateRect(_root, "Segments", Vector2.zero, Vector2.one, center,
                                                 Vector2.zero, Vector2.zero);

        _label = MercUIFactory.CreateText(_root, "Label", "", 18f, Color.white,
                                          TextAlignmentOptions.Center,
                                          new Vector2(0f, -6f), new Vector2(Size.x, 24f),
                                          new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f));
        MercUIFactory.AddShadow(_label);
    }

    // =========================================================
    // CADA FRAME
    // =========================================================

    private void LateUpdate()
    {
        PlayerController pc   = PlayerController.LocalPlayer;
        GameplayAbility  held = pc != null ? pc.HeldAbility : null;

        float maxTime = 0f;
        bool  show    = held != null && held.GetChargeStages(_stageTimes, out maxTime);

        if (!show)
        {
            // Al soltar se desvanece con lo último que mostró.
            _group.alpha = Mathf.MoveTowards(_group.alpha, 0f,
                                             Time.unscaledDeltaTime / Mathf.Max(0.01f, FadeOutSeconds));
            if (_group.alpha <= 0f) _lastStage = -1;
            return;
        }

        if (NeedsRebuild(maxTime)) Rebuild(maxTime);

        float charged = Mathf.Clamp(Time.time - pc.HoldStartedAt, 0f, maxTime);
        int   stage   = CurrentStage(charged);

        // Una etapa nueva: destello en su tramo y saltito de la barra. La primera (al
        // apretar, o una carga nueva) no cuenta.
        if (_lastStage < 0 || !Mathf.Approximately(_shownStart, pc.HoldStartedAt))
        {
            _shownStart = pc.HoldStartedAt;
            _lastStage  = stage;
        }
        else if (stage > _lastStage)
        {
            _lastStage = stage;
            _punch     = 1f;
            if (stage < _segments.Count) _segments[stage].Flash.color = Color.white;
        }

        _group.alpha = 1f;
        _label.text  = held.AbilityName;

        for (int i = 0; i < _segments.Count; i++)
        {
            Segment seg  = _segments[i];
            float   span = Mathf.Max(0.0001f, seg.To - seg.From);
            float   t    = Mathf.Clamp01((charged - seg.From) / span);

            Color stageColor = ColorFor(i);
            seg.Fill.rectTransform.anchorMax = new Vector2(t, 1f);
            seg.Fill.color = stageColor;

            bool reached = i <= stage;
            seg.Back.color   = Color.Lerp(new Color(0.12f, 0.12f, 0.12f, 0.9f), stageColor * 0.35f, reached ? 1f : 0.4f);
            seg.Number.color = reached ? Color.white : new Color(1f, 1f, 1f, 0.35f);

            Color flash = seg.Flash.color;
            flash.a = Mathf.MoveTowards(flash.a, 0f, Time.unscaledDeltaTime * 3f);
            seg.Flash.color = flash;
        }

        // El último tramo (ya en la etapa máxima) late: avisa que se va a soltar sola.
        if (stage == _segments.Count - 1 && _segments.Count > 0)
        {
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 12f);
            Color c = ColorFor(stage);
            _segments[stage].Fill.color = new Color(c.r * pulse + (1f - pulse), c.g * pulse + (1f - pulse),
                                                    c.b * pulse + (1f - pulse), 1f);
        }

        _punch = Mathf.MoveTowards(_punch, 0f, Time.unscaledDeltaTime * 5f);
        _root.localScale = Vector3.one * (1f + 0.12f * _punch);
    }

    private int CurrentStage(float charged)
    {
        int stage = 0;
        for (int i = 0; i < _segments.Count; i++)
            if (charged >= _segments[i].From) stage = i;
        return stage;
    }

    private Color ColorFor(int stage)
    {
        if (StageColors == null || StageColors.Length == 0) return Color.white;
        return StageColors[Mathf.Clamp(stage, 0, StageColors.Length - 1)];
    }

    // =========================================================
    // TRAMOS
    // =========================================================

    private bool NeedsRebuild(float maxTime)
    {
        if (!Mathf.Approximately(maxTime, _builtMax) || _stageTimes.Count != _builtTimes.Count) return true;
        for (int i = 0; i < _stageTimes.Count; i++)
            if (!Mathf.Approximately(_stageTimes[i], _builtTimes[i])) return true;
        return false;
    }

    private void Rebuild(float maxTime)
    {
        foreach (Segment s in _segments) Destroy(s.Rect.gameObject);
        _segments.Clear();

        _builtTimes.Clear();
        _builtTimes.AddRange(_stageTimes);
        _builtTimes.Sort();
        _builtMax = maxTime;

        float width = Size.x;
        for (int i = 0; i < _builtTimes.Count; i++)
        {
            float from = _builtTimes[i];
            float to   = i + 1 < _builtTimes.Count ? _builtTimes[i + 1] : maxTime;
            if (to <= from) to = from + 0.0001f;

            // Cada tramo ocupa su parte del ancho (proporcional al tiempo), menos la separación.
            float x0 = from / maxTime * width + (i > 0 ? SegmentGap * 0.5f : 0f);
            float x1 = to   / maxTime * width - (i + 1 < _builtTimes.Count ? SegmentGap * 0.5f : 0f);

            Segment seg = new Segment { From = from, To = to };
            seg.Rect = MercUIFactory.CreateRect(_segmentsRoot, $"Stage{i + 1}",
                                                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                                                new Vector2(x0, 0f), new Vector2(Mathf.Max(1f, x1 - x0), 0f));

            seg.Back = MercUIFactory.CreateImage(seg.Rect, "Back", Color.black, Vector2.zero, Vector2.zero,
                                                 Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));

            // El relleno se estira de izquierda a derecha moviendo su anchorMax.x.
            seg.Fill = MercUIFactory.CreateImage(seg.Rect, "Fill", ColorFor(i), Vector2.zero, Vector2.zero,
                                                 Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f));

            seg.Flash = MercUIFactory.CreateImage(seg.Rect, "Flash", new Color(1f, 1f, 1f, 0f),
                                                  Vector2.zero, Vector2.zero,
                                                  Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));

            seg.Number = MercUIFactory.CreateText(seg.Rect, "Number", (i + 1).ToString(), 14f, Color.white,
                                                  TextAlignmentOptions.Center, Vector2.zero, Vector2.zero,
                                                  Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            seg.Number.fontStyle = FontStyles.Bold;
            MercUIFactory.AddShadow(seg.Number, 1f);

            _segments.Add(seg);
        }
    }
}
