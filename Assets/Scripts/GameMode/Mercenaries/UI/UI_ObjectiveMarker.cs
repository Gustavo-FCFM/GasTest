
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_ObjectiveMarker
//
// El rombo con la distancia que señala el Objetivo en pantalla, y que se pega al
// borde con una flecha cuando queda fuera de cámara. Es lo que cumple el pedido del
// diseño de que TODOS sepan siempre dónde está el Objetivo.
//
// Marca dos cosas:
//   · EL OBJETIVO: dónde está (o dónde va a aparecer, mientras no exista). Se pinta
//     del color del equipo que lo lleva, así se ve de un vistazo si lo tiene un rival.
//   · TU ENTREGA: el punto de tu base, y solo mientras alguien de tu equipo lo carga —
//     que es el único momento en que importa saber para dónde correr.
//
// VIVE EN EL PREFAB DE PARTIDA (Match_UI ▸ ObjectiveMarkers), desde el 14 de octubre de
// 2026: los dos marcadores (el del Objetivo y el de tu entrega) están armados ahí, cada uno
// con su rombo, su texto de distancia y la barra de entrega. Se acomodan y se les cambia el
// tamaño a mano; el tamaño del rombo y el ancho de la barra salen de cómo están en el prefab.
// Este script solo los pone en la pantalla, los pinta y escribe la distancia. No toca gameplay.
// ============================================================
public class UI_ObjectiveMarker : MonoBehaviour
{
    // Las piezas de un marcador.
    [System.Serializable]
    public class MarkerPieces
    {
        [Tooltip("La raíz del marcador, anclada abajo a la izquierda del canvas (se mueve a la posición en pantalla).")]
        [Label("Raíz")]
        public RectTransform Root;

        [Tooltip("El rombo (una Image girada 45°): se pinta del color de quien lleva el Objetivo.")]
        [Label("Rombo")]
        public Image Diamond;

        [Label("Distancia")]
        public TMP_Text Label;

        [Tooltip("El fondo de la barra de entrega (solo la usa el marcador del Objetivo; se prende sola).")]
        [Label("Barra de entrega")]
        public Image BarBack;

        [Tooltip("El relleno de la barra (anclado a la izquierda: se le cambia el ancho).")]
        [Label("Relleno de la barra")]
        public Image BarFill;
    }

    [Header("Piezas")]
    [Label("Marcador del Objetivo")]
    public MarkerPieces Objective = new MarkerPieces();

    [Tooltip("El punto de entrega de tu base: sale solo mientras alguien de tu equipo lleva el Objetivo.")]
    [Label("Marcador de tu entrega")]
    public MarkerPieces Delivery = new MarkerPieces();

    [Header("Estilo")]
    [Tooltip("El color del Objetivo cuando nadie lo lleva.")]
    [Label("Color del Objetivo")]
    public Color ObjectiveColor = new Color(1f, 0.82f, 0.25f);

    [Tooltip("Margen contra el borde de la pantalla cuando el marcador queda fuera de cámara, en px de pantalla.")]
    [Label("Margen del borde (px)")]
    public float EdgePadding = 60f;

    [Tooltip("Fuera de cámara el rombo se achica a esta fracción de su tamaño, para distinguir " +
             "\"está allá\" de \"lo estás mirando\".")]
    [Label("Rombo fuera de cámara (×)")]
    public float OffScreenScale = 0.75f;

    private Canvas _canvas;
    private Vector2 _objectiveDiamondSize, _deliveryDiamondSize;
    private float _barWidth;

    // Progreso de entrega que se dibuja. El servidor lo manda a saltos (su tick es de
    // 0.15 s), así que acá avanza suave a la velocidad real y se corrige con cada dato.
    private float _shownDelivery;

    private void Start()
    {
        _canvas = GetComponentInParent<Canvas>();
        if (Objective.Diamond != null) _objectiveDiamondSize = Objective.Diamond.rectTransform.sizeDelta;
        if (Delivery.Diamond != null)  _deliveryDiamondSize  = Delivery.Diamond.rectTransform.sizeDelta;
        if (Objective.BarBack != null) _barWidth = Objective.BarBack.rectTransform.sizeDelta.x;
        if (Objective.BarBack != null) Objective.BarBack.gameObject.SetActive(false);
        if (Delivery.BarBack != null)  Delivery.BarBack.gameObject.SetActive(false);
        Hide(Objective); Hide(Delivery);
    }

    // =========================================================
    // REFRESCO
    // =========================================================

    private void LateUpdate()
    {
        MercenariesGameMode gm = MercenariesGameMode.Instance;
        Camera cam = Camera.main;

        if (gm == null || cam == null)
        {
            Hide(Objective); Hide(Delivery);
            return;
        }

        UpdateObjectiveMarker(gm, cam);
        UpdateDeliveryMarker(gm, cam);
    }

    private void UpdateObjectiveMarker(MercenariesGameMode gm, Camera cam)
    {
        MercObjective objective = gm.ActiveObjective;

        Vector3 worldPos;
        string  prefix;
        Color   color;

        UpdateShownDelivery(objective);

        if (objective != null)
        {
            worldPos = objective.WorldPosition;

            if (objective.IsCarried)
            {
                color  = gm.GetTeamColor(objective.CarrierTeam);
                prefix = Loc.T(objective.CarrierTeam == MercUIFactory.LocalTeam() ? "marker.objective_ally" : "marker.objective");

                // Entregando: lo ven TODOS, que es la gracia — los rivales tienen hasta
                // que se llene la barra para cortarla con un golpe.
                if (_shownDelivery > 0f)
                {
                    float remaining = (1f - _shownDelivery) * objective.DeliverSeconds;
                    prefix = Loc.T("marker.delivering_time", ("seconds", remaining.ToString("0.0")));
                }
            }
            else
            {
                color  = ObjectiveColor;
                prefix = Loc.T("marker.objective");
            }
        }
        else if (gm.ObjectiveSpawnPoint != null && gm.State != EMatchState.Ended)
        {
            // Todavía no existe: igual se señala dónde va a caer, con la cuenta atrás.
            worldPos = gm.ObjectiveSpawnPoint.position;
            color    = new Color(ObjectiveColor.r, ObjectiveColor.g, ObjectiveColor.b, 0.55f);
            float eta = gm.ObjectiveEta;
            prefix   = eta > 0f ? Loc.T("marker.objective_in", ("time", MercUIFactory.FormatTime(eta))) : Loc.T("marker.objective");
        }
        else
        {
            Hide(Objective);
            return;
        }

        Place(Objective, _objectiveDiamondSize, worldPos, cam, color, prefix);
        UpdateDeliveryBar(Objective, color);
    }

    private void UpdateShownDelivery(MercObjective objective)
    {
        float target = objective != null && objective.IsCarried ? objective.DeliveryProgress : 0f;
        float rate   = 1f / Mathf.Max(0.1f, objective != null ? objective.DeliverSeconds : 1f);

        // Bajó de golpe: la entrega se cortó (salió de la zona o le pegaron).
        if (target <= 0f || target < _shownDelivery - rate * 0.3f)
        {
            _shownDelivery = target;
            return;
        }

        // Nunca por detrás del último dato, ni más de un tick por delante.
        _shownDelivery = Mathf.Clamp(_shownDelivery + rate * Time.deltaTime,
                                     target, Mathf.Min(1f, target + rate * 0.15f));
    }

    private void UpdateDeliveryBar(MarkerPieces marker, Color color)
    {
        if (marker == null || marker.BarBack == null || marker.BarFill == null) return;

        bool show = _shownDelivery > 0f && marker.Root.gameObject.activeSelf;
        if (marker.BarBack.gameObject.activeSelf != show) marker.BarBack.gameObject.SetActive(show);
        if (!show) return;

        marker.BarFill.color = color;
        marker.BarFill.rectTransform.sizeDelta = new Vector2(_barWidth * _shownDelivery, marker.BarFill.rectTransform.sizeDelta.y);
    }

    // La entrega propia solo se marca cuando tu equipo lleva el Objetivo: el resto del
    // tiempo sería ruido (ya sabés dónde está tu base).
    private void UpdateDeliveryMarker(MercenariesGameMode gm, Camera cam)
    {
        int localTeam = MercUIFactory.LocalTeam();
        MercObjective objective = gm.ActiveObjective;

        if (localTeam <= 0 || objective == null || !objective.IsCarried || objective.CarrierTeam != localTeam)
        {
            Hide(Delivery);
            return;
        }

        MercTeamBase teamBase = gm.GetBase(localTeam);
        if (teamBase == null) { Hide(Delivery); return; }

        Place(Delivery, _deliveryDiamondSize, teamBase.DeliveryWorldPoint, cam, gm.GetTeamColor(localTeam),
              Loc.T(_shownDelivery > 0f ? "marker.delivering" : "marker.deliver"));
    }

    // Coloca un marcador en la pantalla a partir de un punto del mundo. Si el punto
    // está fuera de cámara (o detrás), el marcador se pega al borde en la dirección
    // correcta en vez de desaparecer — así el jugador siempre sabe para dónde girar.
    private void Place(MarkerPieces marker, Vector2 diamondSize, Vector3 worldPos, Camera cam, Color color, string prefix)
    {
        if (marker == null || marker.Root == null) return;

        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
        bool behind = screenPos.z < 0f;

        if (behind)
        {
            // Detrás de la cámara: WorldToScreenPoint devuelve la posición espejada.
            screenPos.x = Screen.width  - screenPos.x;
            screenPos.y = Screen.height - screenPos.y;
        }

        Vector2 center  = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 flat    = new Vector2(screenPos.x, screenPos.y);
        bool offScreen  = behind ||
                          flat.x < EdgePadding || flat.x > Screen.width  - EdgePadding ||
                          flat.y < EdgePadding || flat.y > Screen.height - EdgePadding;

        if (offScreen)
        {
            Vector2 dir = (flat - center);
            if (dir.sqrMagnitude < 0.001f) dir = Vector2.up;
            dir.Normalize();

            // Empujar hasta el borde de un rectángulo con margen.
            float halfW = Screen.width  * 0.5f - EdgePadding;
            float halfH = Screen.height * 0.5f - EdgePadding;
            float scale = Mathf.Min(
                Mathf.Abs(dir.x) > 0.0001f ? halfW / Mathf.Abs(dir.x) : float.MaxValue,
                Mathf.Abs(dir.y) > 0.0001f ? halfH / Mathf.Abs(dir.y) : float.MaxValue);

            flat = center + dir * scale;
        }

        float scaleFactor = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
        marker.Root.anchoredPosition = flat / scaleFactor;

        float distance = Vector3.Distance(cam.transform.position, worldPos);
        if (marker.Label != null)
        {
            marker.Label.text  = $"{prefix}  {Mathf.RoundToInt(distance)}m";
            marker.Label.color = Color.white;
        }

        // Fuera de cámara el rombo se achica un poco, para distinguir "está allá" de
        // "lo estás mirando".
        if (marker.Diamond != null)
        {
            marker.Diamond.color = color;
            marker.Diamond.rectTransform.sizeDelta = offScreen ? diamondSize * OffScreenScale : diamondSize;
        }

        if (!marker.Root.gameObject.activeSelf) marker.Root.gameObject.SetActive(true);
    }

    private static void Hide(MarkerPieces marker)
    {
        if (marker != null && marker.Root != null && marker.Root.gameObject.activeSelf)
            marker.Root.gameObject.SetActive(false);
    }
}
