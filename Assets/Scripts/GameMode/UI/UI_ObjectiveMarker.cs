
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
// Se arma solo (componente en la escena, sin cablear nada) y no toca gameplay.
// ============================================================
public class UI_ObjectiveMarker : MonoBehaviour
{
    [Header("Estilo")]
    public float MarkerSize = 26f;
    public Color ObjectiveColor = new Color(1f, 0.82f, 0.25f);

    [Tooltip("Margen contra el borde de la pantalla cuando el marcador queda fuera de cámara.")]
    public float EdgePadding = 60f;

    // Un marcador dibujado: rombo + flecha de borde + texto de distancia.
    private class Marker
    {
        public RectTransform Root;
        public Image Diamond;
        public TextMeshProUGUI Label;
    }

    private Canvas _canvas;
    private Marker _objectiveMarker;
    private Marker _deliveryMarker;

    private void Start()
    {
        Transform parent = ResolveCanvas();

        _objectiveMarker = BuildMarker(parent, "MarcadorObjetivo");
        _deliveryMarker  = BuildMarker(parent, "MarcadorEntrega");
    }

    private Transform ResolveCanvas()
    {
        _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null)
        {
            // Order bajo: los marcadores del mundo van por DEBAJO del marcador de
            // partida y de los avisos, no encima.
            _canvas = MercUIFactory.CreateCanvas("Canvas_MarcadoresMundo", 45);
            _canvas.transform.SetParent(transform, false);
        }
        return _canvas.transform;
    }

    private Marker BuildMarker(Transform parent, string name)
    {
        var marker = new Marker();

        // Anclado abajo-izquierda: así la posición en pantalla se puede escribir tal
        // cual (dividida por el factor de escala del Canvas), sin más matemática.
        marker.Root = MercUIFactory.CreateRect(parent, name,
            anchorMin: Vector2.zero, anchorMax: Vector2.zero, pivot: new Vector2(0.5f, 0.5f),
            anchoredPos: Vector2.zero, size: new Vector2(MarkerSize * 2f, MarkerSize * 2f));

        marker.Diamond = MercUIFactory.CreateImage(marker.Root, "Rombo", ObjectiveColor,
            anchoredPos: Vector2.zero, size: new Vector2(MarkerSize, MarkerSize),
            anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
            pivot: new Vector2(0.5f, 0.5f));
        marker.Diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        marker.Label = MercUIFactory.CreateText(marker.Root, "Distancia", "", 16f, Color.white,
            TextAlignmentOptions.Center,
            anchoredPos: new Vector2(0f, -MarkerSize), size: new Vector2(140f, 22f),
            anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
            pivot: new Vector2(0.5f, 0.5f));
        marker.Label.fontStyle = FontStyles.Bold;
        MercUIFactory.AddShadow(marker.Label);

        marker.Root.gameObject.SetActive(false);
        return marker;
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
            Hide(_objectiveMarker); Hide(_deliveryMarker);
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

        if (objective != null)
        {
            worldPos = objective.WorldPosition;

            if (objective.IsCarried)
            {
                color  = gm.GetTeamColor(objective.CarrierTeam);
                prefix = objective.CarrierTeam == MercUIFactory.LocalTeam() ? "OBJETIVO (aliado)" : "OBJETIVO";
            }
            else
            {
                color  = ObjectiveColor;
                prefix = "OBJETIVO";
            }
        }
        else if (gm.ObjectiveSpawnPoint != null && gm.State != EMatchState.Ended)
        {
            // Todavía no existe: igual se señala dónde va a caer, con la cuenta atrás.
            worldPos = gm.ObjectiveSpawnPoint.position;
            color    = new Color(ObjectiveColor.r, ObjectiveColor.g, ObjectiveColor.b, 0.55f);
            float eta = gm.ObjectiveEta;
            prefix   = eta > 0f ? $"OBJETIVO EN {MercUIFactory.FormatTime(eta)}" : "OBJETIVO";
        }
        else
        {
            Hide(_objectiveMarker);
            return;
        }

        Place(_objectiveMarker, worldPos, cam, color, prefix);
    }

    // La entrega propia solo se marca cuando tu equipo lleva el Objetivo: el resto del
    // tiempo sería ruido (ya sabés dónde está tu base).
    private void UpdateDeliveryMarker(MercenariesGameMode gm, Camera cam)
    {
        int localTeam = MercUIFactory.LocalTeam();
        MercObjective objective = gm.ActiveObjective;

        if (localTeam <= 0 || objective == null || !objective.IsCarried || objective.CarrierTeam != localTeam)
        {
            Hide(_deliveryMarker);
            return;
        }

        MercTeamBase teamBase = gm.GetBase(localTeam);
        if (teamBase == null) { Hide(_deliveryMarker); return; }

        Place(_deliveryMarker, teamBase.DeliveryWorldPoint, cam, gm.GetTeamColor(localTeam), "ENTREGAR");
    }

    // Coloca un marcador en la pantalla a partir de un punto del mundo. Si el punto
    // está fuera de cámara (o detrás), el marcador se pega al borde en la dirección
    // correcta en vez de desaparecer — así el jugador siempre sabe para dónde girar.
    private void Place(Marker marker, Vector3 worldPos, Camera cam, Color color, string prefix)
    {
        if (marker == null) return;

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
        marker.Label.text = $"{prefix}  {Mathf.RoundToInt(distance)}m";
        marker.Diamond.color = color;
        marker.Label.color   = Color.white;

        // Fuera de cámara el rombo se achica un poco, para distinguir "está allá" de
        // "lo estás mirando".
        float size = offScreen ? MarkerSize * 0.75f : MarkerSize;
        marker.Diamond.rectTransform.sizeDelta = new Vector2(size, size);

        if (!marker.Root.gameObject.activeSelf) marker.Root.gameObject.SetActive(true);
    }

    private static void Hide(Marker marker)
    {
        if (marker != null && marker.Root != null && marker.Root.gameObject.activeSelf)
            marker.Root.gameObject.SetActive(false);
    }
}
