using System.Collections.Generic;
using TMPro;
using UnityEngine;

// ============================================================
// UI_ChargeBar
//
// La barra de carga por ETAPAS del jugador local, como la de "potenciar" del Dracthyr
// del WoW: mientras se mantiene un ataque cargado (GA_ChargedAttack, también dentro de
// un GA_HoldTagSwitch), aparece bajo la mira partida en un tramo por etapa. Cada tramo se
// llena mientras se está en esa etapa; al pasar a la siguiente, el tramo destella y la
// barra da un saltito. El último tramo es la cuenta hasta que se suelta sola.
//
// VIVE EN EL PREFAB DE LA CÁMARA (Player Camera ▸ UI ▸ ChargeBar): el marco, la zona de los
// tramos, el MOLDE de un tramo (UI_ChargeSegment) y el nombre se acomodan ahí. Este script
// copia el molde una vez por etapa, le da a cada copia su parte del ancho (según lo que dura
// esa etapa) y los anima.
//
// DE DÓNDE SALE: PlayerController.HeldAbility / HoldStartedAt (lo que sostiene el dueño
// y desde cuándo) y GameplayAbility.GetChargeStages (dónde empieza cada etapa). Se
// cuenta en el dueño, sin red: el servidor arranca la carga al mismo tiempo, con el
// retraso del viaje nada más.
// ============================================================
public class UI_ChargeBar : MonoBehaviour
{
    [Header("Piezas")]
    [Tooltip("El CanvasGroup de la barra: la muestra y la esconde.")]
    [Label("Grupo")]
    public CanvasGroup Group;

    [Tooltip("Lo que da el saltito al pasar de etapa (normalmente la barra entera).")]
    [Label("Lo que salta")]
    public RectTransform PunchTarget;

    [Tooltip("El rectángulo donde van los tramos: cada uno ocupa su parte del ancho, según " +
             "cuánto dura su etapa.")]
    [Label("Zona de los tramos")]
    public RectTransform SegmentArea;

    [Tooltip("El molde de un tramo (un hijo de la zona de los tramos). Se copia una vez por etapa.")]
    [Label("Molde de un tramo")]
    public UI_ChargeSegment SegmentTemplate;

    [Tooltip("El nombre de la habilidad, debajo de la barra.")]
    [Label("Nombre")]
    public TMP_Text NameText;

    [Header("Medidas")]
    [Tooltip("Espacio libre entre un tramo y el siguiente, en píxeles del HUD.")]
    [Label("Separación entre tramos (px)")]
    public float SegmentGap = 2f;

    [Tooltip("Cuánto crece la barra en el saltito al llegar a una etapa. 12 = un 12 % más grande.")]
    [Label("Saltito al subir de etapa (%)")]
    public float PunchPercent = 12f;

    [Header("Colores")]
    [Tooltip("El color de cada etapa, en orden. Si hay más etapas que colores, repite el último.")]
    [Label("Color de cada etapa")]
    public Color[] StageColors =
    {
        new Color(0.95f, 0.85f, 0.55f, 1f),   // 1: dorado pálido
        new Color(1.00f, 0.60f, 0.15f, 1f),   // 2: naranja
        new Color(1.00f, 0.25f, 0.10f, 1f),   // 3: rojo
    };

    [Tooltip("El fondo de un tramo al que todavía no se llegó.")]
    [Label("Fondo de un tramo sin llegar")]
    public Color PendingBackColor = new Color(0.12f, 0.12f, 0.12f, 0.9f);

    [Tooltip("El fondo de un tramo alcanzado es el color de su etapa, oscurecido: 35 = con el " +
             "35 % de su brillo.")]
    [Label("Brillo del fondo de un tramo alcanzado (%)")]
    [Range(0f, 100f)]
    public float ReachedBackBrightness = 35f;

    [Label("Número de un tramo alcanzado")]
    public Color ReachedNumberColor = Color.white;

    [Label("Número de un tramo sin llegar")]
    public Color PendingNumberColor = new Color(1f, 1f, 1f, 0.35f);

    [Header("Tiempos")]
    [Tooltip("Segundos que tarda en desvanecerse al soltar.")]
    [Label("Desvanecerse al soltar (s)")]
    public float FadeOutSeconds = 0.25f;

    [Tooltip("Segundos que dura el destello de un tramo al llegar a su etapa.")]
    [Label("Destello al llegar (s)")]
    public float FlashSeconds = 0.33f;

    [Tooltip("En la última etapa el tramo late, avisando que se va a soltar sola: cuántas " +
             "veces por segundo.")]
    [Label("Latidos de la última etapa por segundo")]
    public float LastStagePulsesPerSecond = 1.9f;

    // Una copia del molde y los segundos de carga que cubre.
    private class Segment
    {
        public UI_ChargeSegment View;
        public float From, To;
    }

    private readonly List<Segment> _segments = new List<Segment>();

    private readonly List<float> _stageTimes  = new List<float>();
    private readonly List<float> _builtTimes  = new List<float>();
    private float _builtMax = -1f;

    private int   _lastStage = -1;
    private float _shownStart = -1f;  // HoldStartedAt de la carga que se está mostrando
    private float _punch;            // 1 al llegar a una etapa, baja a 0

    private void Awake()
    {
        if (SegmentTemplate != null) SegmentTemplate.gameObject.SetActive(false);
        if (Group != null) Group.alpha = 0f;
    }

    // =========================================================
    // CADA FRAME
    // =========================================================

    private void LateUpdate()
    {
        if (Group == null) return;

        PlayerController pc   = PlayerController.LocalPlayer;
        GameplayAbility  held = pc != null ? pc.HeldAbility : null;

        float maxTime = 0f;
        bool  show    = held != null && held.GetChargeStages(_stageTimes, out maxTime) && maxTime > 0f;

        if (!show)
        {
            // Al soltar se desvanece con lo último que mostró.
            Group.alpha = Mathf.MoveTowards(Group.alpha, 0f,
                                            Time.unscaledDeltaTime / Mathf.Max(0.01f, FadeOutSeconds));
            if (Group.alpha <= 0f) _lastStage = -1;
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
            if (stage < _segments.Count && _segments[stage].View.Flash != null)
                _segments[stage].View.Flash.color = Color.white;
        }

        Group.alpha = 1f;
        if (NameText != null) NameText.text = held.DisplayName;

        float flashStep = Time.unscaledDeltaTime / Mathf.Max(0.01f, FlashSeconds);
        float brightness = ReachedBackBrightness / 100f;

        for (int i = 0; i < _segments.Count; i++)
        {
            Segment          seg  = _segments[i];
            UI_ChargeSegment view = seg.View;
            float span = Mathf.Max(0.0001f, seg.To - seg.From);
            float t    = Mathf.Clamp01((charged - seg.From) / span);

            Color stageColor = ColorFor(i);
            bool  reached    = i <= stage;

            if (view.Fill != null)
            {
                view.Fill.rectTransform.anchorMax = new Vector2(t, 1f);
                view.Fill.color = stageColor;
            }
            if (view.Back != null)
                view.Back.color = reached
                    ? new Color(stageColor.r * brightness, stageColor.g * brightness, stageColor.b * brightness, PendingBackColor.a)
                    : PendingBackColor;
            if (view.Number != null)
                view.Number.color = reached ? ReachedNumberColor : PendingNumberColor;

            if (view.Flash != null)
            {
                Color flash = view.Flash.color;
                flash.a = Mathf.MoveTowards(flash.a, 0f, flashStep);
                view.Flash.color = flash;
            }
        }

        // El último tramo (ya en la etapa máxima) late hacia el blanco: avisa que se va a
        // soltar sola.
        if (_segments.Count > 0 && stage == _segments.Count - 1 && _segments[stage].View.Fill != null)
        {
            float wave  = Mathf.Sin(Time.unscaledTime * LastStagePulsesPerSecond * Mathf.PI * 2f);
            float white = 0.25f - 0.25f * wave;   // de 0 (su color) a 0.5 (mitad blanco)
            _segments[stage].View.Fill.color = Color.Lerp(ColorFor(stage), Color.white, white);
        }

        _punch = Mathf.MoveTowards(_punch, 0f, Time.unscaledDeltaTime * 5f);
        if (PunchTarget != null)
            PunchTarget.localScale = Vector3.one * (1f + PunchPercent / 100f * _punch);
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
        foreach (Segment s in _segments) if (s.View != null) Destroy(s.View.gameObject);
        _segments.Clear();

        _builtTimes.Clear();
        _builtTimes.AddRange(_stageTimes);
        _builtTimes.Sort();
        _builtMax = maxTime;

        if (SegmentTemplate == null || SegmentArea == null) return;

        for (int i = 0; i < _builtTimes.Count; i++)
        {
            float from = _builtTimes[i];
            float to   = i + 1 < _builtTimes.Count ? _builtTimes[i + 1] : maxTime;
            if (to <= from) to = from + 0.0001f;

            UI_ChargeSegment view = Instantiate(SegmentTemplate, SegmentArea);
            view.name = $"Stage{i + 1}";
            view.gameObject.SetActive(true);

            // Cada tramo ocupa su parte del ancho de la zona (proporcional al tiempo), menos
            // media separación de cada lado que tenga vecino.
            var rect = (RectTransform)view.transform;
            rect.anchorMin = new Vector2(from / maxTime, 0f);
            rect.anchorMax = new Vector2(to   / maxTime, 1f);
            rect.offsetMin = new Vector2(i > 0 ? SegmentGap * 0.5f : 0f, 0f);
            rect.offsetMax = new Vector2(i + 1 < _builtTimes.Count ? -SegmentGap * 0.5f : 0f, 0f);

            if (view.Number != null) view.Number.text = (i + 1).ToString();
            if (view.Flash  != null) view.Flash.color = new Color(1f, 1f, 1f, 0f);

            _segments.Add(new Segment { View = view, From = from, To = to });
        }
    }
}
