using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// HealthPackVisual
//
// Lo que se VE de un botiquín, armado por código: una cruz que flota y gira despacio, y
// un disco en el piso que hace de cuenta regresiva mientras está en recarga (se llena
// como una gráfica de pastel, igual que el cooldown de una habilidad en el HUD).
//
// Se arma solo: no hay prefab que cablear. Si se le asigna un CrossVisual propio en el
// HealthPack, se usa ese modelo en vez de la cruz de barras.
//
// El disco es un Canvas en World Space mirando hacia arriba, con una Image en modo
// Filled/Radial360 — la misma técnica del HUD, pero acostada en el piso.
// ============================================================
public class HealthPackVisual : MonoBehaviour
{
    private HealthPack _pack;

    private Transform _cross;      // la cruz flotante
    private Image     _fill;       // el disco de la cuenta regresiva
    private Image     _ring;       // el borde del disco, siempre visible
    private GameObject _crossRoot;
    private Transform  _label;     // el "+75 HP" que dice qué es esto

    private float _phase;

    public void Build(HealthPack pack)
    {
        _pack = pack;
        if (_cross != null) return;   // ya armado

        // --- la cruz ---
        _crossRoot = new GameObject("Cross");
        _crossRoot.transform.SetParent(transform, false);
        _crossRoot.transform.localPosition = Vector3.up * pack.CrossHeight;
        _cross = _crossRoot.transform;

        if (pack.CrossVisual != null)
        {
            Instantiate(pack.CrossVisual, _cross, false);
        }
        else
        {
            // Dos barras: la cruz más barata que existe, y se ve bien de lejos.
            MakeBar(_cross, "Vertical",   new Vector3(0.22f, 0.7f,  0.22f), pack.Tint);
            MakeBar(_cross, "Horizontal", new Vector3(0.7f,  0.22f, 0.22f), pack.Tint);
        }

        // --- la etiqueta ---
        //
        // Sin esto el botiquín es "un piso verde": en la prueba de 9 nadie supo qué
        // era, ni siquiera quienes lo levantaron. Un cartel con la cantidad lo explica
        // solo, y de paso se lee desde lejos, que es cuando decidís si vale el desvío.
        float heal = pack.HealAmount;
        GameObject label = MakeLabel(_cross, "", pack.Tint);
        LocalizedText.Bind(label.GetComponentInChildren<TextMeshProUGUI>(),
                           () => Loc.T("healthpack.label", ("amount", heal.ToString("F0"))));
        _label = label.transform;

        // --- el disco del piso ---
        GameObject canvasGo = new GameObject("Disc", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        canvasGo.transform.localPosition = Vector3.up * 0.06f;   // justo sobre el piso
        canvasGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta  = new Vector2(100f, 100f);
        canvasRect.localScale = Vector3.one * (pack.PickupRadius * 2f / 100f);

        _ring = MakeDisc(canvasGo.transform, "Ring", new Color(Tint().r, Tint().g, Tint().b, 0.22f));
        _fill = MakeDisc(canvasGo.transform, "Fill", new Color(Tint().r, Tint().g, Tint().b, 0.75f));
        _fill.type        = Image.Type.Filled;
        _fill.fillMethod  = Image.FillMethod.Radial360;
        _fill.fillOrigin  = (int)Image.Origin360.Top;
        _fill.fillClockwise = true;
    }

    private Color Tint() => _pack != null ? _pack.Tint : Color.green;

    // La cruz flota y gira mientras está lista; el disco se llena mientras no lo está.
    public void Refresh(bool ready, float recharge)
    {
        if (_crossRoot != null && _crossRoot.activeSelf != ready) _crossRoot.SetActive(ready);

        if (ready && _cross != null)
        {
            // El mismo bamboleo de los fantasmas: sube y baja, y gira despacio.
            _phase += Time.deltaTime;
            float bob = Mathf.Sin(_phase * 1.6f) * 0.12f;
            _cross.localPosition = Vector3.up * ((_pack != null ? _pack.CrossHeight : 1.1f) + bob);
            _cross.localRotation = Quaternion.Euler(0f, _phase * 45f, 0f);
        }

        // La etiqueta mira SIEMPRE a la cámara de quien juega, y se mantiene derecha
        // aunque la cruz gire: un cartel que rota con ella sería ilegible la mitad del
        // tiempo.
        if (_label != null && _label.gameObject.activeInHierarchy)
        {
            Camera cam = ResolveCamera();
            if (cam != null)
            {
                _label.rotation = Quaternion.LookRotation(_label.position - cam.transform.position);
                _label.localPosition = Vector3.up * 0.55f;
            }
        }

        if (_fill != null) _fill.fillAmount = ready ? 1f : recharge;
    }

    // La cámara de quien mira. Se re-resuelve si la que teníamos quedó APAGADA, no
    // solo si murió: al spawnear, el jugador apaga la cámara del lobby, y un componente
    // apagado NO es == null. Con el chequeo de null a secas, el cartel se quedaría
    // mirando para siempre a una cámara desactivada — el mismo error que tuvimos con
    // los nameplates.
    private Camera _cam;

    private Camera ResolveCamera()
    {
        if (_cam == null || !_cam.isActiveAndEnabled) _cam = Camera.main;
        return _cam;
    }

    // Cartel world-space con la cantidad. Cuelga de la cruz, así se prende y se apaga
    // con ella sin que nadie lo recuerde.
    private static GameObject MakeLabel(Transform parent, string text, Color color)
    {
        GameObject canvasGo = new GameObject("Label", typeof(Canvas));
        canvasGo.transform.SetParent(parent, false);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        RectTransform rect = canvasGo.GetComponent<RectTransform>();
        rect.sizeDelta  = new Vector2(220f, 70f);
        rect.localScale = Vector3.one * 0.006f;

        GameObject textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(canvasGo.transform, false);

        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = textGo.AddComponent<TextMeshProUGUI>();
        label.text                 = text;
        label.fontSize             = 48f;
        label.alignment            = TextAlignmentOptions.Center;
        label.color                = color;
        label.fontStyle            = FontStyles.Bold;
        label.raycastTarget        = false;
        label.enableWordWrapping   = false;

        return canvasGo;
    }

    // =========================================================
    // PIEZAS
    // =========================================================

    private static void MakeBar(Transform parent, string name, Vector3 size, Color color)
    {
        GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.name = name;
        bar.transform.SetParent(parent, false);
        bar.transform.localScale = size;

        // Sin collider: es decoración, y uno de más confundiría a los ataques.
        Collider col = bar.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer r = bar.GetComponent<Renderer>();
        if (r != null)
        {
            // Material propio para no teñir el del proyecto entero. URP usa _BaseColor;
            // el emissive es lo que lo hace visible de lejos y en sombra.
            Material mat = new Material(r.sharedMaterial);
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 1.5f);
            }
            r.material = mat;
        }
    }

    private static Image MakeDisc(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.AddComponent<Image>();
        img.sprite        = MercUIFactory.WhiteSprite;
        img.color         = color;
        img.raycastTarget = false;
        return img;
    }
}
