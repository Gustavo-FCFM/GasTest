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
//   · INTERRUMPIDA    → se pone roja y dice "Interrumpido" un momento antes de irse.
//
// VIVE EN EL PREFAB DE LA CÁMARA (Player Camera ▸ UI ▸ CastBar): sus piezas (marco, fondo,
// relleno, destello y nombre) se acomodan y se les cambia el tamaño ahí. Este script solo
// las anima.
//
// QUIÉN LA PIDE: la habilidad, en el servidor, con GameplayAbility.ShowCastBar /
// HideCastBar, que llegan al dueño por TargetRpc (NetworkASC.ServerShowCastBar). La cuenta
// del tiempo corre en el cliente: arranca cuando llega el aviso.
// ============================================================
public class UI_CastBar : MonoBehaviour
{
    [Header("Piezas")]
    [Tooltip("El CanvasGroup de la barra: la muestra y la esconde.")]
    [Label("Grupo")]
    public CanvasGroup Group;

    [Tooltip("El relleno: se estira de izquierda a derecha moviendo su anchorMax.x (anclado a la izquierda).")]
    [Label("Relleno")]
    public Image Fill;

    [Tooltip("Una capa blanca encima que destella al terminar bien.")]
    [Label("Destello")]
    public Image Flash;

    [Tooltip("El nombre de la habilidad, debajo de la barra.")]
    [Label("Nombre")]
    public TMP_Text NameText;

    [Header("Colores")]
    [Label("Color al cargar")]
    public Color CastColor        = new Color(1.00f, 0.78f, 0.20f, 1f);
    [Label("Color al canalizar")]
    public Color ChannelColor     = new Color(0.35f, 0.85f, 0.35f, 1f);
    [Label("Color al interrumpirse")]
    public Color InterruptedColor = new Color(0.90f, 0.15f, 0.10f, 1f);

    [Header("Tiempos")]
    [Tooltip("Segundos que queda la barra roja de \"Interrumpido\" antes de irse.")]
    [Label("Interrumpida se ve (s)")]
    public float InterruptedSeconds = 0.6f;

    [Tooltip("Segundos que tarda en desvanecerse al terminar bien.")]
    [Label("Desvanecerse al terminar (s)")]
    public float FadeOutSeconds = 0.25f;

    // La del jugador local (hay una sola cámara por pantalla). Null si todavía no hay jugador.
    private static UI_CastBar _instance;

    // Lo llama NetworkASC.TargetShowCastBar en el dueño.
    public static void Show(string label, float duration, bool channel)
    {
        if (_instance != null) _instance.Begin(label, duration, channel);
    }

    // Lo llama NetworkASC.TargetHideCastBar en el dueño.
    public static void Hide(bool interrupted)
    {
        if (_instance != null) _instance.End(interrupted);
    }

    private enum EState { Hidden, Running, Done, Interrupted }
    private EState _state = EState.Hidden;
    private bool   _channel;
    private float  _startedAt;
    private float  _duration;
    private float  _endedAt;

    private void Awake()
    {
        _instance = this;
        if (Group != null) Group.alpha = 0f;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Begin(string label, float duration, bool channel)
    {
        _duration  = Mathf.Max(0.05f, duration);
        _channel   = channel;
        _startedAt = Time.time;
        _state     = EState.Running;

        if (Fill != null)     Fill.color = channel ? ChannelColor : CastColor;
        if (Flash != null)    Flash.color = new Color(1f, 1f, 1f, 0f);
        if (NameText != null) NameText.text = label ?? "";
        if (Group != null)    Group.alpha = 1f;
    }

    private void End(bool interrupted)
    {
        if (_state != EState.Running && _state != EState.Done) return;

        _endedAt = Time.time;
        if (interrupted)
        {
            _state = EState.Interrupted;
            if (Fill != null)
            {
                Fill.color = InterruptedColor;
                Fill.rectTransform.anchorMax = new Vector2(1f, 1f);
            }
            if (NameText != null) NameText.text = Loc.T("castbar.interrupted");
        }
        else
        {
            _state = EState.Done;
            if (Flash != null) Flash.color = new Color(1f, 1f, 1f, 0.8f);
        }
    }

    private void LateUpdate()
    {
        if (Group == null || Fill == null) return;

        switch (_state)
        {
            case EState.Hidden:
                return;

            case EState.Running:
            {
                float t = Mathf.Clamp01((Time.time - _startedAt) / _duration);
                Fill.rectTransform.anchorMax = new Vector2(_channel ? 1f - t : t, 1f);

                // Si el aviso de fin no llega (se perdió, o la habilidad no lo manda), se
                // cierra sola un rato después de la duración.
                if (Time.time - _startedAt > _duration + 1f) End(false);
                break;
            }

            case EState.Done:
            {
                if (!_channel) Fill.rectTransform.anchorMax = new Vector2(1f, 1f);
                if (Flash != null)
                {
                    Color f = Flash.color;
                    f.a = Mathf.MoveTowards(f.a, 0f, Time.unscaledDeltaTime * 4f);
                    Flash.color = f;
                }

                Group.alpha = 1f - Mathf.Clamp01((Time.time - _endedAt) / Mathf.Max(0.01f, FadeOutSeconds));
                if (Group.alpha <= 0f) _state = EState.Hidden;
                break;
            }

            case EState.Interrupted:
            {
                float k = (Time.time - _endedAt) / Mathf.Max(0.01f, InterruptedSeconds);
                Group.alpha = k < 0.6f ? 1f : 1f - Mathf.Clamp01((k - 0.6f) / 0.4f);
                if (k >= 1f) { Group.alpha = 0f; _state = EState.Hidden; }
                break;
            }
        }
    }
}
