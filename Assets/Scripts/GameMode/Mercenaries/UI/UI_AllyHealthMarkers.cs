using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_AllyHealthMarkers
//
// Para los SOPORTES (13 de noviembre de 2026, pedido de Gustavo; como en Overwatch): una cruz sobre la cabeza de cada
// aliado herido, que se ve A TRAVÉS de las paredes, del color de cuánta vida le queda, como un semáforo: verde casi
// lleno, amarillo a la mitad, rojo casi muerto (y en rojo late). Así el que cura sabe a quién ir a buscar sin verlo.
//
// Solo se muestra si tu clase es de rol Soporte (CharacterClassDefinition.DisplayRole: también la clase base). Lee la vida sincronizada de cada
// aliado (NetworkAbilitySystemComponent), así que anda igual en el host y en los clientes. Un aliado fuera de la
// pantalla no se marca (no se empuja al borde como el Objetivo: serían muchas flechas).
//
// VIVE EN EL PREFAB DE PARTIDA (Match_UI ▸ AllyHealthMarkers): el MOLDE de la marca está armado ahí, apagado; se
// copia una por aliado. Es solo una vista: no toca nada del juego.
// ============================================================
public class UI_AllyHealthMarkers : MonoBehaviour
{
    [Header("Piezas")]
    [Tooltip("El molde de una marca (fondo y cruz). Se apaga solo; cada aliado herido usa una copia.")]
    [Label("Molde de la marca")]
    public RectTransform MarkerTemplate;

    [Tooltip("La cruz del molde: es la que se pinta del color de la vida.")]
    [Label("Cruz del molde")]
    public Image TemplateIcon;

    [Header("Cuándo")]
    [Tooltip("Se marca al aliado con menos vida que esta fracción de su máxima.")]
    [Label("Se marca por debajo de (0 a 1)")]
    [Range(0f, 1f)] public float ShowBelow = 0.95f;

    [Tooltip("Por debajo de esto, amarillo.")]
    [Label("Amarillo por debajo de (0 a 1)")]
    [Range(0f, 1f)] public float YellowBelow = 0.6f;

    [Tooltip("Por debajo de esto, rojo (y late).")]
    [Label("Rojo por debajo de (0 a 1)")]
    [Range(0f, 1f)] public float RedBelow = 0.3f;

    [Header("Colores")]
    [Label("Verde")]
    public Color HighColor = new Color(0.35f, 0.95f, 0.4f);

    [Label("Amarillo")]
    public Color MidColor = new Color(1f, 0.85f, 0.25f);

    [Label("Rojo")]
    public Color LowColor = new Color(1f, 0.3f, 0.3f);

    [Header("Dónde")]
    [Tooltip("Altura sobre los pies del aliado.")]
    [Label("Altura (m)")]
    public float Height = 2.6f;

    [Tooltip("Lejos, la marca se achica hasta este tamaño (fracción del molde).")]
    [Label("Tamaño de lejos (0 a 1)")]
    [Range(0.2f, 1f)] public float FarScale = 0.6f;

    [Tooltip("A partir de esta distancia, la marca ya está en su tamaño de lejos.")]
    [Label("Lejos desde (m)")]
    public float FarDistance = 40f;

    [Tooltip("A menos de esto, no se marca: ya lo tienes al lado.")]
    [Label("Sin marca a menos de (m)")]
    public float HideCloserThan = 4f;

    private readonly List<PlayerController> _players = new List<PlayerController>();
    private readonly List<(RectTransform root, Image icon)> _markers = new List<(RectTransform, Image)>();
    private float _nextScan;
    private Canvas _canvas;
    private Camera _cam;

    private void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        if (MarkerTemplate != null) MarkerTemplate.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        int used = 0;
        PlayerController local = PlayerController.LocalPlayer;
        bool support = local != null && local.CurrentClassDef != null && local.CurrentClassDef.DisplayRole == EClassRole.Support;   // el Clérigo base también (su rol de menú)

        if (support && MarkerTemplate != null)
        {
            if (_cam == null || !_cam.isActiveAndEnabled) _cam = Camera.main;
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 0.5f;
                _players.Clear();
                _players.AddRange(FindObjectsByType<PlayerController>(FindObjectsSortMode.None));
            }

            NetworkAbilitySystemComponent localNet = local.GetComponent<NetworkAbilitySystemComponent>();
            int localTeam = localNet != null ? localNet.NetTeamID : -1;

            if (_cam != null && localTeam > 0)
                foreach (PlayerController pc in _players)
                    if (TryPlace(pc, local, localTeam, used)) used++;
        }

        for (int i = used; i < _markers.Count; i++)
            if (_markers[i].root.gameObject.activeSelf) _markers[i].root.gameObject.SetActive(false);
    }

    private bool TryPlace(PlayerController pc, PlayerController local, int localTeam, int index)
    {
        if (pc == null || pc == local) return false;
        NetworkAbilitySystemComponent net = pc.GetComponent<NetworkAbilitySystemComponent>();
        if (net == null || net.NetTeamID != localTeam) return false;

        float max = net.NetMaxHealth;
        float health = net.NetHealth;
        if (max <= 0f || health <= 0f) return false;   // muerto: no hay a quién curar
        float fraction = health / max;
        if (fraction >= ShowBelow) return false;

        if ((pc.transform.position - local.transform.position).sqrMagnitude < HideCloserThan * HideCloserThan) return false;

        Vector3 world = pc.transform.position + Vector3.up * Height;
        Vector3 screen = _cam.WorldToScreenPoint(world);
        if (screen.z <= 0f) return false;   // detrás de la cámara
        if (screen.x < 0f || screen.x > Screen.width || screen.y < 0f || screen.y > Screen.height) return false;

        var (root, icon) = Marker(index);
        if (!root.gameObject.activeSelf) root.gameObject.SetActive(true);

        float scaleFactor = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
        root.anchoredPosition = new Vector2(screen.x, screen.y) / scaleFactor;

        float far = Mathf.InverseLerp(HideCloserThan, FarDistance, screen.z);
        float size = Mathf.Lerp(1f, FarScale, far);
        bool low = fraction < RedBelow;
        if (low) size *= 1f + 0.15f * (0.5f + 0.5f * Mathf.Sin(Time.time * 8f));
        root.localScale = Vector3.one * size;

        if (icon != null) icon.color = low ? LowColor : fraction < YellowBelow ? MidColor : HighColor;
        return true;
    }

    private (RectTransform, Image) Marker(int index)
    {
        while (_markers.Count <= index)
        {
            RectTransform copy = Instantiate(MarkerTemplate, MarkerTemplate.parent);
            Image icon = null;
            if (TemplateIcon != null)
            {
                string path = TemplateIcon.transform == MarkerTemplate ? "" : TemplateIcon.name;
                Transform t = string.IsNullOrEmpty(path) ? copy : copy.Find(path);
                icon = t != null ? t.GetComponent<Image>() : null;
            }
            _markers.Add((copy, icon));
        }
        return _markers[index];
    }
}
