using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ============================================================
// UI_RadialMenu
//
// La rueda para elegir una opción de una habilidad IRadialMenuAbility (el tótem del
// Chamán, y más adelante los hechizos del Mago). Mantener el botón la abre alrededor de la
// mira; se apunta con el mouse (o el stick) y al SOLTAR sale la opción resaltada. El centro
// es cancelar.
//
// CUÁNTAS OPCIONES: las que dé la habilidad (RadialIcons.Length). Cada una es una porción
// del anillo; con más de 6 el anillo crece y los íconos se achican para que entren.
//
// LO QUE SE VE:
//   · Abre con un "pop" (escala y fundido).
//   · La porción bajo la mira se ilumina, sale un poco hacia afuera con un halo y su ícono
//     crece; un rombo en el borde interior apunta hacia donde va la mira.
//   · El centro muestra el nombre de la opción y una línea de qué hace
//     (RadialLabels / RadialDescriptions). Sobre el centro dice "Cancel" en rojo.
//   · Una opción que no se puede usar ahora (su cooldown propio,
//     IsRadialOptionAvailable) se ve apagada; soltarla ahí es cancelar.
//   · Al confirmar, la elegida destella mientras la rueda se desvanece.
//
// NO HAY NADA QUE CABLEAR: se dibuja sola por código (como UI_ChargeBar). La versión vieja
// del prefab Player Camera (MenuContainer con 4 porciones fijas) se apaga al arrancar y se
// puede borrar del prefab.
// ============================================================
public class UI_RadialMenu : MonoBehaviour
{
    // =========================================================
    // PERILLAS
    // =========================================================

    [Header("Tamaño (píxeles a 1920x1080)")]
    [Tooltip("Radio exterior del anillo con hasta 6 opciones.")]
    public float OuterRadius = 200f;

    [Tooltip("Cuánto crece el radio por cada opción después de la sexta, para que entren.")]
    public float ExtraRadiusPerOption = 14f;

    [Tooltip("Radio interior del anillo, como fracción del exterior.")]
    [Range(0.2f, 0.8f)] public float InnerRatio = 0.46f;

    [Tooltip("Radio del centro (cancelar). Con la mira ahí adentro, soltar no elige nada.")]
    public float CenterRadius = 72f;

    [Tooltip("Tamaño máximo de los íconos. Con muchas opciones se achican solos.")]
    public float IconSize = 64f;

    [Tooltip("Grados de separación entre porciones.")]
    public float GapDegrees = 2.5f;

    [Tooltip("Cuánto sale hacia afuera la porción resaltada.")]
    public float PopDistance = 12f;

    [Header("Colores")]
    public Color WedgeColor     = new Color(0.10f, 0.10f, 0.12f, 0.82f);
    public Color HoverColor     = new Color(1.00f, 0.78f, 0.25f, 0.95f);
    public Color GlowColor      = new Color(1.00f, 0.70f, 0.20f, 0.35f);
    public Color DisabledColor  = new Color(0.25f, 0.08f, 0.08f, 0.80f);
    public Color CancelColor    = new Color(0.90f, 0.25f, 0.20f, 1f);
    public Color CenterColor    = new Color(0.05f, 0.05f, 0.06f, 0.88f);

    [Header("Animación")]
    public float OpenSeconds  = 0.14f;
    public float CloseSeconds = 0.18f;
    [Tooltip("Qué tan rápido sigue el resaltado a la mira.")]
    public float HoverSpeed   = 16f;

    [Header("Versión vieja")]
    [Tooltip("El menú viejo del prefab (4 porciones fijas). Se apaga al arrancar; se puede borrar.")]
    public GameObject MenuContainer;

    // =========================================================
    // ACCESO
    // =========================================================

    private static UI_RadialMenu _instance;

    // La única rueda. Si la escena no trae una (el prefab Player Camera sí), se crea sola.
    public static UI_RadialMenu Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = FindFirstObjectByType<UI_RadialMenu>();
            if (_instance == null) _instance = new GameObject("UI_RadialMenu").AddComponent<UI_RadialMenu>();
            return _instance;
        }
    }

    // --- construido en runtime ---
    private class Slice
    {
        public RectTransform Root;
        public Image Glow;
        public Image Wedge;
        public Image Icon;
        public float Angle;       // grados, antihorario desde la derecha
        public float Hover;       // 0..1, sigue suave al resaltado
        public bool  Available;
    }

    private Canvas          _canvas;
    private RectTransform   _root;
    private CanvasGroup     _group;
    private Image           _backdrop;
    private RectTransform   _ringRoot;
    private Image           _center;
    private Image           _pointer;
    private TextMeshProUGUI _label;
    private TextMeshProUGUI _description;
    private Sprite          _ringSprite;
    private Sprite          _discSprite;
    private Sprite          _softSprite;
    private float           _ringSpriteRatio = -1f;

    private Slice[]  _slices = new Slice[0];
    private string[] _labels;
    private string[] _descriptions;
    private IRadialMenuAbility _ability;

    private bool  _open;
    private bool  _closing;
    private float _stateTime;
    private int   _selected = -1;
    private int   _confirmed = -1;
    private float _outer, _inner;

    private void Awake()
    {
        if (MenuContainer != null) MenuContainer.SetActive(false);

        if (_instance != null && _instance != this) { enabled = false; return; }
        _instance = this;
        Build();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
        if (_canvas != null) Destroy(_canvas.gameObject);
        if (_open) UICursor.Release(this);
    }

    // =========================================================
    // ABRIR / CERRAR (los llama PlayerController)
    // =========================================================

    // Abre la rueda con las opciones de la habilidad y libera el cursor para apuntar.
    public void Show(IRadialMenuAbility ability)
    {
        if (_root == null) Build();

        _ability      = ability;
        _labels       = ability.RadialLabels;
        _descriptions = ability.RadialDescriptions;
        BuildSlices(ability.RadialIcons);

        _open      = true;
        _closing   = false;
        _stateTime = 0f;
        _selected  = -1;
        _confirmed = -1;
        _root.gameObject.SetActive(true);

        UICursor.Request(this, blockGameplayInput: false);   // la rueda usa el mapa de juego
    }

    // Cierra la rueda y devuelve la opción elegida (-1 = cancelar, o una que no se puede usar).
    public int HideAndGetSelection()
    {
        if (!_open) return -1;

        int pick = _selected >= 0 && _selected < _slices.Length && _slices[_selected].Available ? _selected : -1;

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
        if (_root == null || (!_open && !_closing)) return;

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
        _group.alpha = k;
        _ringRoot.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, k);

        // Dirección de la mira: el stick con control, el mouse si no.
        float scale = _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
        Vector2 dir;
        PlayerInputProvider input = PlayerInputProvider.Local;
        if (input != null && input.MoveIsGamepad)
            dir = input.MoveValue * (_outer * scale);
        else
        {
            Vector2 mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition;
            dir = mouse - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        bool inCenter = dir.magnitude <= CenterRadius * scale;
        _selected = inCenter || _slices.Length == 0 ? -1 : IndexForAngle(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        // Puntero: un rombo en el borde interior, hacia donde va la mira.
        _pointer.enabled = !inCenter;
        if (!inCenter)
        {
            float a = Mathf.Atan2(dir.y, dir.x);
            _pointer.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (_inner - 12f);
            _pointer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 45f);
        }

        // Centro: cancelar, o el nombre de la opción.
        if (inCenter)
        {
            _center.color = Color.Lerp(CenterColor, CancelColor, 0.35f);
            SetText("Cancel", "", CancelColor);
        }
        else
        {
            _center.color = CenterColor;
            Slice s = _slices[_selected];
            string name = Label(_selected);
            string desc = s.Available ? Description(_selected) : "On cooldown";
            SetText(name, desc, s.Available ? Color.white : new Color(1f, 0.55f, 0.5f));
        }

        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < _slices.Length; i++)
        {
            Slice s = _slices[i];
            s.Hover = Mathf.MoveTowards(s.Hover, i == _selected ? 1f : 0f, dt * HoverSpeed * 0.5f);
            ApplySlice(s, s.Hover, 0f);
        }
    }

    private void TickClosing()
    {
        float t = Mathf.Clamp01(_stateTime / Mathf.Max(0.01f, CloseSeconds));
        _group.alpha = 1f - t;
        _ringRoot.localScale = Vector3.one * Mathf.Lerp(1f, _confirmed >= 0 ? 1.08f : 0.9f, Ease(t));
        _pointer.enabled = false;

        // La elegida destella mientras la rueda se va.
        for (int i = 0; i < _slices.Length; i++)
            ApplySlice(_slices[i], i == _confirmed ? 1f : _slices[i].Hover * (1f - t), i == _confirmed ? 1f - t : 0f);

        if (t >= 1f)
        {
            _closing = false;
            _root.gameObject.SetActive(false);
        }
    }

    // Color, salida y escala de una porción según qué tan resaltada está (h) y el destello.
    private void ApplySlice(Slice s, float h, float flash)
    {
        Color baseColor = s.Available ? WedgeColor : DisabledColor;
        Color hover     = s.Available ? HoverColor : Color.Lerp(DisabledColor, CancelColor, 0.5f);
        s.Wedge.color = Color.Lerp(Color.Lerp(baseColor, hover, h), Color.white, flash * 0.6f);

        Color glow = GlowColor;
        glow.a *= Mathf.Max(h, flash);
        s.Glow.color = s.Available ? glow : new Color(0f, 0f, 0f, 0f);

        Vector2 dir = new Vector2(Mathf.Cos(s.Angle * Mathf.Deg2Rad), Mathf.Sin(s.Angle * Mathf.Deg2Rad));
        s.Root.anchoredPosition = dir * (PopDistance * h);

        float iconScale = 1f + 0.25f * h + 0.15f * flash;
        s.Icon.rectTransform.localScale = Vector3.one * iconScale;
        Color iconColor = s.Available ? Color.white : new Color(0.55f, 0.55f, 0.55f, 0.45f);
        s.Icon.color = Color.Lerp(iconColor * new Color(0.85f, 0.85f, 0.85f, 1f), iconColor, h);
    }

    // Opción bajo un ángulo (grados, antihorario desde la derecha). La 0 está arriba y
    // siguen en sentido horario, como un reloj.
    private int IndexForAngle(float angle)
    {
        int n = _slices.Length;
        float step = 360f / n;
        float fromTop = Mathf.Repeat(90f - angle + step * 0.5f, 360f);
        return Mathf.Clamp(Mathf.FloorToInt(fromTop / step), 0, n - 1);
    }

    private void SetText(string label, string desc, Color color)
    {
        _label.text  = label;
        _label.color = color;
        _description.text = desc;
    }

    private string Label(int i)
    {
        if (_labels != null && i < _labels.Length && !string.IsNullOrEmpty(_labels[i])) return _labels[i];
        return $"Option {i + 1}";
    }

    private string Description(int i)
        => _descriptions != null && i < _descriptions.Length ? _descriptions[i] ?? "" : "";

    private static float Ease(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

    // =========================================================
    // CONSTRUCCIÓN
    // =========================================================

    private void Build()
    {
        _canvas = MercUIFactory.CreateCanvas("RadialMenuCanvas", 60);

        Vector2 c = new Vector2(0.5f, 0.5f);
        _root = MercUIFactory.CreateRect(_canvas.transform, "RadialMenu", c, c, c, Vector2.zero, Vector2.zero);
        _group = _root.gameObject.AddComponent<CanvasGroup>();
        _group.interactable = false;
        _group.blocksRaycasts = false;

        _softSprite = MakeDisc(128, 0f, soft: true);
        _discSprite = MakeDisc(256, 0f, soft: false);

        _backdrop = MakeImage(_root, "Backdrop", _softSprite, new Color(0f, 0f, 0f, 0.55f), Vector2.zero);
        _ringRoot = MercUIFactory.CreateRect(_root, "Ring", c, c, c, Vector2.zero, Vector2.zero);

        _center = MakeImage(_root, "Center", _discSprite, CenterColor, new Vector2(CenterRadius * 2f, CenterRadius * 2f));

        _pointer = MakeImage(_root, "Pointer", MercUIFactory.WhiteSprite, HoverColor, new Vector2(12f, 12f));

        _label = MercUIFactory.CreateText(_root, "Label", "", 20f, Color.white, TextAlignmentOptions.Center,
                                          new Vector2(0f, 10f), new Vector2(CenterRadius * 2.4f, 28f), c, c, c);
        _label.fontStyle = FontStyles.Bold;
        _label.textWrappingMode = TextWrappingModes.NoWrap;
        MercUIFactory.AddShadow(_label, 1.5f);

        _description = MercUIFactory.CreateText(_root, "Description", "", 14f, new Color(0.85f, 0.85f, 0.85f),
                                                TextAlignmentOptions.Center, new Vector2(0f, -16f),
                                                new Vector2(CenterRadius * 1.9f, 40f), c, c, c);
        MercUIFactory.AddShadow(_description, 1f);

        _root.gameObject.SetActive(false);
    }

    // Una porción por opción, con el radio según cuántas son.
    private void BuildSlices(Sprite[] icons)
    {
        foreach (Slice s in _slices) if (s != null && s.Root != null) Destroy(s.Root.gameObject);

        int n = icons != null ? icons.Length : 0;
        _slices = new Slice[n];
        if (n == 0) return;

        _outer = OuterRadius + Mathf.Max(0, n - 6) * ExtraRadiusPerOption;
        _inner = _outer * InnerRatio;
        if (!Mathf.Approximately(_ringSpriteRatio, InnerRatio))
        {
            _ringSprite = MakeDisc(512, InnerRatio, soft: false);
            _ringSpriteRatio = InnerRatio;
        }

        Vector2 size = new Vector2(_outer * 2f, _outer * 2f);
        _backdrop.rectTransform.sizeDelta = size + new Vector2(110f, 110f);

        float step = 360f / n;
        float span = Mathf.Max(1f, step - GapDegrees);
        float mid  = (_inner + _outer) * 0.5f;
        float icon = Mathf.Min(IconSize, 2f * Mathf.PI * mid / n * 0.55f, (_outer - _inner) * 0.8f);

        for (int i = 0; i < n; i++)
        {
            float angle = 90f - i * step;   // la primera arriba, después en sentido horario

            var s = new Slice { Angle = angle, Available = _ability == null || _ability.IsRadialOptionAvailable(i) };
            Vector2 c = new Vector2(0.5f, 0.5f);
            s.Root = MercUIFactory.CreateRect(_ringRoot, $"Option{i}", c, c, c, Vector2.zero, Vector2.zero);

            // El relleno radial arranca arriba y va en sentido horario: se gira para que la
            // porción quede centrada en su ángulo.
            float rot = angle - (90f - span * 0.5f);
            s.Glow  = MakeWedge(s.Root, "Glow",  size * 1.07f, span + 1.5f, rot - 0.75f);
            s.Wedge = MakeWedge(s.Root, "Wedge", size, span, rot);

            Vector2 at = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * mid;
            s.Icon = MakeImage(s.Root, "Icon", icons[i], Color.white, new Vector2(icon, icon));
            s.Icon.rectTransform.anchoredPosition = at;
            s.Icon.preserveAspect = true;

            _slices[i] = s;
            ApplySlice(s, 0f, 0f);
        }
    }

    private Image MakeWedge(RectTransform parent, string name, Vector2 size, float spanDegrees, float rotation)
    {
        Image img = MakeImage(parent, name, _ringSprite, WedgeColor, size);
        img.type          = Image.Type.Filled;
        img.fillMethod    = Image.FillMethod.Radial360;
        img.fillOrigin    = (int)Image.Origin360.Top;
        img.fillClockwise = true;
        img.fillAmount    = spanDegrees / 360f;
        img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        return img;
    }

    private static Image MakeImage(RectTransform parent, string name, Sprite sprite, Color color, Vector2 size)
    {
        Vector2 c = new Vector2(0.5f, 0.5f);
        RectTransform rt = MercUIFactory.CreateRect(parent, name, c, c, c, Vector2.zero, size);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // Un disco (innerRatio 0) o un anillo, con el borde suavizado. soft = degradé hacia
    // afuera (el fondo oscuro detrás de la rueda).
    private static Sprite MakeDisc(int size, float innerRatio, bool soft)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float r = size * 0.5f;
        float inner = r * innerRatio;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                float a;
                if (soft) a = Mathf.Clamp01(1f - d / r) * Mathf.Clamp01(1f - d / r) * 1.6f;
                else      a = Mathf.Clamp01(r - d) * (innerRatio > 0f ? Mathf.Clamp01(d - inner) : 1f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
