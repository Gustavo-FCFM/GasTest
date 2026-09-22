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

        if (_fill != null) _fill.fillAmount = ready ? 1f : recharge;
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
