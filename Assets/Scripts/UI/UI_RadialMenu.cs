using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ============================================================
// UI_RadialMenu
//
// La rueda para elegir una opción de una habilidad IRadialMenuAbility (el tótem del Chamán,
// el compañero del Maestro de bestias, los libros del Mago). Mantener el botón la abre
// alrededor de la mira; se apunta con el mouse o, con control, con el stick DERECHO (desde el
// 28 de octubre de 2026; antes era el izquierdo, que ahora sigue moviendo al personaje), y al
// SOLTAR sale la opción resaltada. El centro es cancelar (con el stick: soltarlo).
//
// CUÁNTAS OPCIONES: las que dé la habilidad (RadialIcons.Length). Cada una es una porción
// del anillo; con más de 6 el anillo crece y los íconos se achican para que entren.
//
// LO QUE SE VE:
//   · Abre con un "pop" (escala y fundido).
//   · La porción bajo la mira se ilumina, sale un poco hacia afuera con un halo y su ícono
//     crece; un rombo en el borde interior apunta hacia donde va la mira.
//   · El centro muestra el nombre de la opción y una línea de qué hace
//     (RadialLabels / RadialDescriptions). Sobre el centro dice "Cancelar" en rojo.
//   · Una opción que no se puede usar ahora (su cooldown propio,
//     IsRadialOptionAvailable) se ve apagada; soltarla ahí es cancelar.
//   · Al confirmar, la elegida destella mientras la rueda se desvanece.
//
// VIVE EN EL PREFAB DE LA CÁMARA (Player Camera ▸ UI ▸ RadialMenu), con sus piezas a la vista
// para acomodarlas: el fondo, el MOLDE de una porción (UI_RadialOption), el centro, el puntero
// y los dos textos. Al empezar el juego este script las apaga.
//   · LO QUIETO se cambia en las piezas: los radios y colores de la porción y su brillo (en el
//     molde), el tamaño máximo del ícono, el radio y color del centro (es también la zona de
//     cancelar), el fondo, el puntero y los textos.
//   · LO QUE CAMBIA AL MOVER LA MIRA se cambia acá: el color de la resaltada, el de las que no
//     se pueden usar, el de cancelar y los tiempos.
// ============================================================
public class UI_RadialMenu : MonoBehaviour
{
    [Header("Piezas")]
    [Tooltip("El CanvasGroup de la rueda: la muestra y la esconde.")]
    [Label("Grupo")]
    public CanvasGroup Group;

    [Tooltip("Lo que se agranda al abrir y al elegir: el padre de las porciones.")]
    [Label("Anillo")]
    public RectTransform Ring;

    [Tooltip("El molde de una porción (un hijo del anillo). Se copia una vez por opción. Sus " +
             "radios son los de la rueda con hasta 6 opciones.")]
    [Label("Molde de una porción")]
    public UI_RadialOption OptionTemplate;

    [Tooltip("La sombra oscura detrás de la rueda. Lo que sobresale de las porciones en el " +
             "prefab se mantiene cuando la rueda crece.")]
    [Label("Fondo")]
    public UI_RingWedge Backdrop;

    [Tooltip("El disco del medio. Su radio es también la zona de cancelar: con la mira ahí " +
             "adentro, soltar no elige nada.")]
    [Label("Centro")]
    public UI_RingWedge Center;

    [Tooltip("El rombo que apunta hacia la mira, en el borde interior del anillo. La distancia " +
             "que tiene en el prefab al borde interior se mantiene.")]
    [Label("Puntero")]
    public Graphic Pointer;

    [Tooltip("El nombre de la opción, arriba del medio.")]
    [Label("Nombre")]
    public TMP_Text NameText;

    [Tooltip("Qué hace la opción, debajo del nombre.")]
    [Label("Descripción")]
    public TMP_Text DescriptionText;

    [Header("Medidas")]
    [Tooltip("Grados libres entre una porción y la siguiente.")]
    [Label("Separación entre porciones (°)")]
    public float GapDegrees = 2.5f;

    [Tooltip("Con más de 6 opciones el anillo crece esto por cada una de más, para que entren.")]
    [Label("Radio extra por opción después de la sexta (px)")]
    public float ExtraRadiusPerOption = 8f;

    [Tooltip("Cuánto sale hacia afuera la porción resaltada, en píxeles del HUD.")]
    [Label("Salida de la resaltada (px)")]
    public float PopDistance = 7f;

    [Tooltip("Cuánto crece el ícono de la porción resaltada. 25 = un 25 % más grande.")]
    [Label("Ícono resaltado crece (%)")]
    public float HoverIconGrowthPercent = 25f;

    [Tooltip("Al elegir, el ícono de la elegida crece esto de más mientras la rueda se va. 15 = un 15 % más.")]
    [Label("Ícono elegido crece de más (%)")]
    public float ConfirmIconGrowthPercent = 15f;

    [Header("Colores que cambian al apuntar")]
    [Label("Porción resaltada")]
    public Color HoverColor = new Color(0.35f, 0.85f, 0.40f, 0.95f);

    [Tooltip("Una opción que no se puede usar ahora (en espera).")]
    [Label("Porción que no se puede usar")]
    public Color DisabledColor = new Color(0.25f, 0.08f, 0.08f, 0.80f);

    [Label("Porción que no se puede usar, resaltada")]
    public Color DisabledHoverColor = new Color(0.575f, 0.165f, 0.14f, 0.90f);

    [Tooltip("El centro con la mira adentro (cancelar).")]
    [Label("Centro al cancelar")]
    public Color CenterCancelColor = new Color(0.35f, 0.12f, 0.11f, 0.92f);

    [Tooltip("El color del texto \"Cancelar\".")]
    [Label("Texto de cancelar")]
    public Color CancelTextColor = new Color(0.90f, 0.25f, 0.20f, 1f);

    [Tooltip("El nombre de una opción que no se puede usar ahora.")]
    [Label("Nombre de una opción en espera")]
    public Color UnavailableNameColor = new Color(1f, 0.55f, 0.5f, 1f);

    [Label("Ícono de una opción en espera")]
    public Color UnavailableIconColor = new Color(0.55f, 0.55f, 0.55f, 0.45f);

    [Tooltip("Los íconos de las porciones sin resaltar se ven un poco más oscuros. 85 = al 85 % " +
             "de su brillo.")]
    [Label("Brillo de los íconos sin resaltar (%)")]
    [Range(0f, 100f)]
    public float IdleIconBrightness = 85f;

    [Header("Animación")]
    [Label("Abrir en (s)")]
    public float OpenSeconds = 0.14f;

    [Label("Cerrar en (s)")]
    public float CloseSeconds = 0.18f;

    [Tooltip("Lo que tarda el resaltado en pasar de una porción a otra.")]
    [Label("El resaltado tarda (s)")]
    public float HoverSeconds = 0.125f;

    [Tooltip("Al abrir, la rueda arranca a este tamaño y crece hasta el 100 %.")]
    [Label("Abre desde (%)")]
    public float OpenFromScalePercent = 80f;

    [Tooltip("Al elegir, la rueda crece hasta este tamaño mientras se desvanece.")]
    [Label("Al elegir crece hasta (%)")]
    public float ConfirmScalePercent = 108f;

    [Tooltip("Al cancelar, la rueda se achica hasta este tamaño mientras se desvanece.")]
    [Label("Al cancelar se achica hasta (%)")]
    public float CancelScalePercent = 90f;

    [Tooltip("Cuánto destella hacia el blanco la porción elegida. 60 = al 60 % del blanco.")]
    [Label("Destello de la elegida (%)")]
    [Range(0f, 100f)]
    public float ConfirmFlashPercent = 60f;

    // =========================================================
    // ACCESO
    // =========================================================

    private static UI_RadialMenu _instance;

    // La rueda del jugador local (en su cámara). Null si todavía no hay jugador.
    public static UI_RadialMenu Instance => _instance;

    private class Slice
    {
        public UI_RadialOption View;
        public RectTransform   Root;
        public float Angle;       // grados, antihorario desde la derecha
        public float Hover;       // 0..1, sigue suave al resaltado
        public bool  Available;
        public bool  Selectable;  // se puede elegir aunque se vea apagada (el libro del Filo danzante)
    }

    private readonly List<Slice> _slices = new List<Slice>();
    private string[] _labels;
    private string[] _descriptions;
    private IRadialMenuAbility _ability;

    // Medidas leídas del prefab (ver Awake).
    private float _baseInner, _baseOuter;           // la porción del molde
    private float _glowInnerExtra, _glowOuterExtra, _glowSpanExtra;
    private float _iconMax;
    private float _backdropExtra;
    private float _pointerInset;
    private Color _wedgeColor, _glowColor, _centerColor, _nameColor;
    private Canvas _canvas;

    private bool  _open;
    private bool  _closing;
    private bool  _aimWithStick;   // se apunta con el stick (si no, con el mouse): ver TrackAimDevice
    private float _stateTime;
    private int   _selected = -1;
    private int   _confirmed = -1;
    private float _inner, _outer;

    private void Awake()
    {
        if (Group == null || Ring == null || OptionTemplate == null || OptionTemplate.Wedge == null ||
            Center == null || NameText == null)
        {
            Debug.LogWarning($"[Rueda] {name} no tiene sus piezas asignadas: la rueda no va a abrir.", this);
            enabled = false;
            return;
        }

        _instance = this;
        _canvas   = GetComponentInParent<Canvas>();

        // Las medidas y colores quietos salen de las piezas del prefab.
        UI_RingWedge wedge = OptionTemplate.Wedge;
        _baseInner  = wedge.InnerRadius;
        _baseOuter  = wedge.OuterRadius;
        _wedgeColor = wedge.color;
        if (OptionTemplate.Glow != null)
        {
            _glowInnerExtra = OptionTemplate.Glow.InnerRadius - wedge.InnerRadius;
            _glowOuterExtra = OptionTemplate.Glow.OuterRadius - wedge.OuterRadius;
            _glowSpanExtra  = OptionTemplate.Glow.SpanDegrees - wedge.SpanDegrees;
            _glowColor      = OptionTemplate.Glow.color;
        }
        if (OptionTemplate.Icon != null)
        {
            Vector2 size = OptionTemplate.Icon.rectTransform.sizeDelta;
            _iconMax = Mathf.Max(size.x, size.y);
        }
        _backdropExtra = Backdrop != null ? Backdrop.OuterRadius - _baseOuter : 0f;
        _pointerInset  = Pointer != null ? _baseInner - Pointer.rectTransform.anchoredPosition.magnitude : 0f;
        _centerColor   = Center.color;
        _nameColor     = NameText.color;

        OptionTemplate.gameObject.SetActive(false);
        Group.alpha          = 0f;
        Group.interactable   = false;
        Group.blocksRaycasts = false;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
        if (_open) UICursor.Release(this);
    }

    // =========================================================
    // ABRIR / CERRAR (los llama PlayerController)
    // =========================================================

    // Abre la rueda con las opciones de la habilidad y libera el cursor para apuntar.
    public void Show(IRadialMenuAbility ability)
    {
        if (!enabled) return;

        _ability      = ability;
        _labels       = ability.RadialLabels;
        _descriptions = ability.RadialDescriptions;
        BuildSlices(ability.RadialIcons);

        _open      = true;
        _closing   = false;
        _stateTime = 0f;
        _selected  = -1;
        _confirmed = -1;
        _aimWithStick = !LocalSeat.AllowsKeyboardMouse;

        UICursor.Request(this, blockGameplayInput: false);   // la rueda usa el mapa de juego
    }

    // Cierra la rueda y devuelve la opción elegida (-1 = cancelar, o una que no se puede usar).
    public int HideAndGetSelection()
    {
        if (!_open) return -1;

        int pick = _selected >= 0 && _selected < _slices.Count && _slices[_selected].Selectable ? _selected : -1;

        _open      = false;
        _closing   = true;
        _stateTime = 0f;
        _confirmed = pick;
        UICursor.Release(this);
        return pick;
    }

    // =========================================================
    // CADA FRAME
    // =========================================================

    private void Update()
    {
        if (!_open && !_closing) return;

        _stateTime += Time.unscaledDeltaTime;

        // Red de seguridad: si el jugador ya no la tiene abierta (murió, lo cortaron), se cierra.
        PlayerController local = PlayerController.LocalPlayer;
        if (_open && local != null && !local.isRadialMenuOpen) HideAndGetSelection();

        if (_open) TickOpen();
        else       TickClosing();
    }

    private void TickOpen()
    {
        float k = Ease(Mathf.Clamp01(_stateTime / Mathf.Max(0.01f, OpenSeconds)));
        Group.alpha = k;
        Ring.localScale = Vector3.one * Mathf.Lerp(OpenFromScalePercent / 100f, 1f, k);

        // Dirección de la mira desde el centro de la rueda en pantalla: el stick derecho con
        // control, el mouse si no.
        float scale = _canvas != null && _canvas.rootCanvas.scaleFactor > 0f ? _canvas.rootCanvas.scaleFactor : 1f;
        PlayerInputProvider input = PlayerInputProvider.Local;
        TrackAimDevice(input);

        Vector2 dir;
        if (_aimWithStick)
            dir = (input != null ? input.LookValue : Vector2.zero) * (_outer * scale);
        else
        {
            Vector2 mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition;
            dir = mouse - ScreenCenter();
        }

        bool inCenter = dir.magnitude <= Center.OuterRadius * scale;
        _selected = inCenter || _slices.Count == 0 ? -1 : IndexForAngle(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        // Puntero: un rombo en el borde interior, hacia donde va la mira.
        if (Pointer != null)
        {
            Pointer.enabled = !inCenter;
            if (!inCenter)
            {
                float a = Mathf.Atan2(dir.y, dir.x);
                Pointer.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (_inner - _pointerInset);
                Pointer.rectTransform.localRotation    = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 45f);
            }
        }

        // Centro: cancelar, o el nombre de la opción.
        if (inCenter)
        {
            Center.color = CenterCancelColor;
            SetText(Loc.T("radial.cancel"), "", CancelTextColor);
        }
        else
        {
            Center.color = _centerColor;
            Slice s = _slices[_selected];
            string desc = s.Available || s.Selectable ? Description(_selected) : Loc.T("radial.on_cooldown");
            SetText(Label(_selected), desc, s.Available ? _nameColor : UnavailableNameColor);
        }

        float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, HoverSeconds);
        for (int i = 0; i < _slices.Count; i++)
        {
            Slice s = _slices[i];
            s.Hover = Mathf.MoveTowards(s.Hover, i == _selected ? 1f : 0f, step);
            ApplySlice(s, s.Hover, 0f);
        }
    }

    private void TickClosing()
    {
        float t = Mathf.Clamp01(_stateTime / Mathf.Max(0.01f, CloseSeconds));
        Group.alpha = 1f - t;
        float endScale = (_confirmed >= 0 ? ConfirmScalePercent : CancelScalePercent) / 100f;
        Ring.localScale = Vector3.one * Mathf.Lerp(1f, endScale, Ease(t));
        if (Pointer != null) Pointer.enabled = false;

        // La elegida destella mientras la rueda se va.
        for (int i = 0; i < _slices.Count; i++)
            ApplySlice(_slices[i], i == _confirmed ? 1f : _slices[i].Hover * (1f - t), i == _confirmed ? 1f - t : 0f);

        if (t >= 1f)
        {
            _closing = false;
            Group.alpha = 0f;
        }
    }

    // Con qué se apunta. Una ventana de control (LocalSeat) SIEMPRE con el stick: el mouse que
    // anda por ahí es el de la ventana de teclado. Si no, con el último que se movió, y se
    // recuerda: el stick que vuelve al centro es cancelar, no le pasa la mira al mouse (que quedó
    // quieto en cualquier lado). El stick es el DERECHO, la acción Look: mientras la rueda está
    // abierta la cámara no gira (UICursor.Free) y el izquierdo sigue moviendo al personaje.
    private void TrackAimDevice(PlayerInputProvider input)
    {
        if (!LocalSeat.AllowsKeyboardMouse) { _aimWithStick = true; return; }
        if (input == null || !input.IsReady) return;

        InputDevice device = input.Look.activeControl?.device;
        if (device is Gamepad)    _aimWithStick = true;
        else if (device is Mouse) _aimWithStick = false;
    }

    // El centro de la rueda en la pantalla (en píxeles), para medir hacia dónde va el mouse.
    private Vector2 ScreenCenter()
    {
        Camera cam = _canvas != null && _canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _canvas.rootCanvas.worldCamera : null;
        return RectTransformUtility.WorldToScreenPoint(cam, transform.position);
    }

    // Color, salida y escala de una porción según qué tan resaltada está (h) y el destello.
    private void ApplySlice(Slice s, float h, float flash)
    {
        UI_RadialOption v = s.View;

        Color idle  = s.Available ? _wedgeColor : DisabledColor;
        Color hover = s.Available ? HoverColor  : DisabledHoverColor;
        v.Wedge.color = Color.Lerp(Color.Lerp(idle, hover, h), Color.white, flash * ConfirmFlashPercent / 100f);

        if (v.Glow != null)
        {
            Color glow = _glowColor;
            glow.a *= s.Available ? Mathf.Max(h, flash) : 0f;
            v.Glow.color = glow;
        }

        Vector2 dir = new Vector2(Mathf.Cos(s.Angle * Mathf.Deg2Rad), Mathf.Sin(s.Angle * Mathf.Deg2Rad));
        s.Root.anchoredPosition = dir * (PopDistance * h);

        if (v.Icon != null)
        {
            v.Icon.rectTransform.localScale =
                Vector3.one * (1f + HoverIconGrowthPercent / 100f * h + ConfirmIconGrowthPercent / 100f * flash);

            Color icon = s.Available ? Color.white : UnavailableIconColor;
            float dim  = IdleIconBrightness / 100f;
            Color idleIcon = new Color(icon.r * dim, icon.g * dim, icon.b * dim, icon.a);
            v.Icon.color = Color.Lerp(idleIcon, icon, h);
        }
    }

    // Opción bajo un ángulo (grados, antihorario desde la derecha). La 0 está arriba y
    // siguen en sentido horario, como un reloj.
    private int IndexForAngle(float angle)
    {
        int n = _slices.Count;
        float step = 360f / n;
        float fromTop = Mathf.Repeat(90f - angle + step * 0.5f, 360f);
        return Mathf.Clamp(Mathf.FloorToInt(fromTop / step), 0, n - 1);
    }

    private void SetText(string label, string desc, Color color)
    {
        NameText.text  = label;
        NameText.color = color;
        if (DescriptionText != null) DescriptionText.text = desc;
    }

    private string Label(int i)
    {
        if (_labels != null && i < _labels.Length && !string.IsNullOrEmpty(_labels[i])) return _labels[i];
        return Loc.T("radial.option", ("number", i + 1));
    }

    private string Description(int i)
        => _descriptions != null && i < _descriptions.Length ? _descriptions[i] ?? "" : "";

    private static float Ease(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

    // =========================================================
    // PORCIONES
    // =========================================================

    // Una copia del molde por opción, con el radio según cuántas son.
    private void BuildSlices(Sprite[] icons)
    {
        foreach (Slice s in _slices) if (s.View != null) Destroy(s.View.gameObject);
        _slices.Clear();

        int n = icons != null ? icons.Length : 0;
        if (n == 0) return;

        float extra = Mathf.Max(0, n - 6) * ExtraRadiusPerOption;
        _inner = _baseInner + extra;
        _outer = _baseOuter + extra;
        if (Backdrop != null) Backdrop.SetShape(0f, _outer + _backdropExtra, 360f, 90f);

        float step = 360f / n;
        float span = Mathf.Max(1f, step - GapDegrees);
        float mid  = (_inner + _outer) * 0.5f;
        float icon = Mathf.Min(_iconMax, 2f * Mathf.PI * mid / n * 0.55f, (_outer - _inner) * 0.8f);

        for (int i = 0; i < n; i++)
        {
            float angle = 90f - i * step;   // la primera arriba, después en sentido horario

            UI_RadialOption view = Instantiate(OptionTemplate, OptionTemplate.transform.parent);
            view.name = $"Option{i}";
            view.gameObject.SetActive(true);

            view.Wedge.SetShape(_inner, _outer, span, angle);
            if (view.Glow != null)
                view.Glow.SetShape(_inner + _glowInnerExtra, _outer + _glowOuterExtra, span + _glowSpanExtra, angle);

            if (view.Icon != null)
            {
                view.Icon.sprite         = icons[i];
                view.Icon.preserveAspect = true;
                view.Icon.rectTransform.sizeDelta = new Vector2(icon, icon);
                view.Icon.rectTransform.anchoredPosition =
                    new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * mid;
            }

            var s = new Slice
            {
                View      = view,
                Root      = (RectTransform)view.transform,
                Angle     = angle,
                Available = _ability == null || _ability.IsRadialOptionAvailable(i),
                Selectable = _ability == null || _ability.IsRadialOptionSelectable(i),
            };
            _slices.Add(s);
            ApplySlice(s, 0f, 0f);
        }
    }
}
