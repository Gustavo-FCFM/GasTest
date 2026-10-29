using UnityEngine;
using UnityEngine.EventSystems;

// ============================================================
// UI_SelectionFrame
//
// El marco que muestra QUÉ está elegido con el control en los menús (28 de octubre de 2026,
// Gustavo: "la selección no es muy clara, hay que resaltar más el seleccionado"). El elegido de
// cada botón se ve de su mismo color más oscuro (ButtonTint), pero en un botón oscuro —las
// teclas del teclado en pantalla— eso casi no se nota.
//
// Es UNO para todos los menús: sigue a lo elegido en la pantalla de arriba de todo (MenuInput)
// mientras se usa el control, se desliza de un control al siguiente y late suave. Con el mouse
// no se ve (el mouse ya resalta lo que tiene abajo).
//
// VIVE EN EL PREFAB DE MENÚS (Menus_UI ▸ SelectionFrame): un canvas encima de todos los menús
// (y debajo del cartel "Aprieta A") con el marco adentro. El color y el grosor se cambian en el
// marco (UI_RectFrame); el margen y los tiempos, acá.
// ============================================================
public class UI_SelectionFrame : MonoBehaviour
{
    [Header("Piezas")]
    [Tooltip("El marco (UI_RectFrame). En juego toma la forma de lo elegido, más el margen.")]
    [Label("Marco")]
    public RectTransform Frame;

    [Tooltip("El CanvasGroup del marco: lo hace aparecer, irse y latir.")]
    [Label("Grupo")]
    public CanvasGroup Group;

    [Header("Medidas")]
    [Tooltip("Cuánto se separa el marco del borde de lo elegido, en píxeles del menú (1920×1080).")]
    [Label("Margen alrededor (px)")]
    public float Margin = 4f;

    [Header("Animación")]
    [Tooltip("Lo que tarda en pasar de un control al siguiente. 0 = salta de una.")]
    [Label("Pasa al siguiente en (s)")]
    public float MoveSeconds = 0.08f;

    [Label("Aparece y se va en (s)")]
    public float FadeSeconds = 0.1f;

    [Tooltip("Cuánto baja la opacidad al latir. 0 = no late.")]
    [Label("Latido (0 a 1)")]
    [Range(0f, 1f)]
    public float PulseAmount = 0.3f;

    [Label("Latidos por segundo")]
    public float PulsesPerSecond = 1.2f;

    private static readonly Vector3[] _corners = new Vector3[4];

    private float   _visible;      // 0..1
    private Vector2 _center, _size;

    private void Awake()
    {
        if (Group != null)
        {
            Group.alpha          = 0f;
            Group.interactable   = false;
            Group.blocksRaycasts = false;
        }
    }

    private void LateUpdate()
    {
        if (Frame == null || Group == null || !(Frame.parent is RectTransform space)) return;

        float dt = Time.unscaledDeltaTime;
        RectTransform target = CurrentTarget();

        if (target != null)
        {
            // Lo elegido, en el espacio del marco (todos los menús son del mismo tamaño en pantalla).
            target.GetWorldCorners(_corners);
            Vector2 a = space.InverseTransformPoint(_corners[0]);
            Vector2 b = space.InverseTransformPoint(_corners[2]);
            Vector2 center = (a + b) * 0.5f;
            Vector2 size   = new Vector2(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y)) + Vector2.one * (Margin * 2f);

            // Recién aparecido, ya en su lugar; si no, se desliza.
            float k = _visible <= 0.01f || MoveSeconds <= 0f ? 1f : 1f - Mathf.Exp(-dt * 3f / MoveSeconds);
            _center = Vector2.Lerp(_center, center, k);
            _size   = Vector2.Lerp(_size, size, k);

            Frame.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _size.x);
            Frame.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _size.y);
            Frame.localPosition = _center + Vector2.Scale(Frame.pivot - new Vector2(0.5f, 0.5f), _size);
        }

        _visible = Mathf.MoveTowards(_visible, target != null ? 1f : 0f, dt / Mathf.Max(0.01f, FadeSeconds));
        float pulse = 1f - PulseAmount * 0.5f * (1f + Mathf.Sin(Time.unscaledTime * PulsesPerSecond * 2f * Mathf.PI));
        Group.alpha = _visible * pulse;
    }

    // Lo elegido en la pantalla de arriba, si se está usando el control (en una ventana de control
    // siempre). Null si no hay nada que marcar.
    private static RectTransform CurrentTarget()
    {
        if (!MenuInput.UsingPad && LocalSeat.AllowsKeyboardMouse) return null;

        IMenuScreen top = MenuInput.Top;
        EventSystem es = EventSystem.current;
        GameObject sel = es != null ? es.currentSelectedGameObject : null;
        if (top == null || sel == null || !sel.activeInHierarchy) return null;

        Transform root = top.SelectionRoot;
        if (root != null && !sel.transform.IsChildOf(root)) return null;
        return sel.transform as RectTransform;
    }
}
