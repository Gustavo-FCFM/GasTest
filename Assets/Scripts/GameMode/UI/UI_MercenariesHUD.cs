using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_MercenariesHUD
//
// El marcador de la partida, arriba al centro: los tres equipos con su COLOR, su
// NIVEL y una barra de entregas partida en dos mitades (una por punto de victoria) —
// la misma idea que el marcador de The Finals. Debajo, el reloj y el estado del
// Objetivo.
//
// Se arma solo: poné este componente en un GameObject vacío de la escena y listo. Si
// no cuelga de un Canvas, se crea uno propio. No hay nada que cablear en el Inspector.
//
// Lee TODO de MercenariesGameMode (que es quien sincroniza el estado) y no toca nada:
// es una vista, sin lógica de juego.
// ============================================================
public class UI_MercenariesHUD : MonoBehaviour
{
    [Header("Colocación")]
    [Tooltip("Separación desde el borde superior de la pantalla, en píxeles de referencia (1080p).")]
    public float TopMargin = 14f;

    [Tooltip("Ancho del marcador.")]
    public float PanelWidth = 520f;

    [Header("Estilo")]
    public Color PanelBackground     = new Color(0f, 0f, 0f, 0.55f);
    public Color LocalTeamBackground = new Color(1f, 1f, 1f, 0.16f);
    public Color EmptySegmentColor   = new Color(1f, 1f, 1f, 0.14f);

    private const float RowHeight  = 34f;
    private const float RowSpacing = 38f;

    // Una fila por equipo. Guardamos las piezas que hay que refrescar cada frame.
    private class TeamRow
    {
        public int Team;
        public Image Background;
        public Image Chip;
        public TextMeshProUGUI Label;
        public TextMeshProUGUI Level;
        public Image XpFill;
        public Image[] Segments;
    }

    private readonly TeamRow[] _rows = new TeamRow[MercenariesGameMode.TeamCount];

    private RectTransform _root;
    private TextMeshProUGUI _clockText;
    private TextMeshProUGUI _objectiveText;

    private void Start()
    {
        Build();
    }

    // =========================================================
    // CONSTRUCCIÓN
    // =========================================================

    private void Build()
    {
        Transform parent = ResolveCanvas();

        float panelHeight = RowSpacing * MercenariesGameMode.TeamCount + 56f;

        _root = MercUIFactory.CreateRect(parent, "MarcadorMercenarios",
            anchorMin: new Vector2(0.5f, 1f), anchorMax: new Vector2(0.5f, 1f),
            pivot:     new Vector2(0.5f, 1f),
            anchoredPos: new Vector2(0f, -TopMargin),
            size:        new Vector2(PanelWidth, panelHeight));

        // --- reloj ---
        _clockText = MercUIFactory.CreateText(_root, "Reloj", "0:00", 30f, Color.white,
            TextAlignmentOptions.Center,
            anchoredPos: new Vector2(0f, -2f), size: new Vector2(PanelWidth, 34f),
            anchorMin: new Vector2(0.5f, 1f), anchorMax: new Vector2(0.5f, 1f),
            pivot: new Vector2(0.5f, 1f));
        _clockText.fontStyle = FontStyles.Bold;
        MercUIFactory.AddShadow(_clockText);

        // --- filas de equipo ---
        for (int i = 0; i < MercenariesGameMode.TeamCount; i++)
            _rows[i] = BuildRow(i + 1, -38f - i * RowSpacing);

        // --- estado del Objetivo ---
        _objectiveText = MercUIFactory.CreateText(_root, "EstadoObjetivo", "", 18f,
            new Color(1f, 0.92f, 0.55f), TextAlignmentOptions.Center,
            anchoredPos: new Vector2(0f, -(44f + RowSpacing * MercenariesGameMode.TeamCount)),
            size: new Vector2(PanelWidth, 24f),
            anchorMin: new Vector2(0.5f, 1f), anchorMax: new Vector2(0.5f, 1f),
            pivot: new Vector2(0.5f, 1f));
        MercUIFactory.AddShadow(_objectiveText);
    }

    private TeamRow BuildRow(int team, float y)
    {
        var row = new TeamRow { Team = team };

        RectTransform rowRect = MercUIFactory.CreateRect(_root, $"Equipo{team}",
            anchorMin: new Vector2(0.5f, 1f), anchorMax: new Vector2(0.5f, 1f),
            pivot:     new Vector2(0.5f, 1f),
            anchoredPos: new Vector2(0f, y),
            size:        new Vector2(PanelWidth, RowHeight));

        row.Background = MercUIFactory.CreateImage(rowRect, "Fondo", PanelBackground,
            anchoredPos: Vector2.zero, size: new Vector2(PanelWidth, RowHeight),
            anchorMin: new Vector2(0f, 0.5f), anchorMax: new Vector2(0f, 0.5f),
            pivot: new Vector2(0f, 0.5f));

        Color teamColor = MercUIFactory.TeamColor(team);

        // Franja de color: es lo que hace que se lea de un vistazo de qué equipo es
        // cada fila, igual que el marcador de The Finals.
        row.Chip = MercUIFactory.CreateImage(rowRect, "Color", teamColor,
            anchoredPos: new Vector2(0f, 0f), size: new Vector2(6f, RowHeight));

        row.Label = MercUIFactory.CreateText(rowRect, "Nombre",
            MercenariesGameMode.TeamName(team), 17f, Color.white, TextAlignmentOptions.Left,
            anchoredPos: new Vector2(16f, 0f), size: new Vector2(120f, RowHeight));
        row.Label.fontStyle = FontStyles.Bold;
        MercUIFactory.AddShadow(row.Label);

        // Nivel del equipo (sale de la bolsa compartida de experiencia).
        row.Level = MercUIFactory.CreateText(rowRect, "Nivel", "Nv. 1", 16f,
            new Color(1f, 1f, 1f, 0.9f), TextAlignmentOptions.Left,
            anchoredPos: new Vector2(142f, 4f), size: new Vector2(60f, 20f));
        MercUIFactory.AddShadow(row.Level);

        // Barrita fina de progreso hacia el próximo nivel, debajo del texto del nivel.
        MercUIFactory.CreateImage(rowRect, "XpFondo", EmptySegmentColor,
            anchoredPos: new Vector2(142f, -9f), size: new Vector2(56f, 4f));
        row.XpFill = MercUIFactory.CreateImage(rowRect, "XpRelleno", teamColor,
            anchoredPos: new Vector2(142f, -9f), size: new Vector2(56f, 4f));
        row.XpFill.type       = Image.Type.Filled;
        row.XpFill.fillMethod = Image.FillMethod.Horizontal;
        row.XpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        row.XpFill.fillAmount = 0f;

        // Entregas: una barra partida en tantos trozos como puntos hagan falta para
        // ganar. Cada entrega enciende un trozo.
        int points = ResolvePointsToWin();
        row.Segments = new Image[points];

        const float barLeft = 212f;
        float barWidth  = PanelWidth - barLeft - 10f;
        float gap       = 5f;
        float segWidth  = (barWidth - gap * (points - 1)) / points;

        for (int s = 0; s < points; s++)
        {
            float x = barLeft + s * (segWidth + gap);
            MercUIFactory.CreateImage(rowRect, $"Segmento{s}Fondo", EmptySegmentColor,
                anchoredPos: new Vector2(x, 0f), size: new Vector2(segWidth, 16f));

            Image fill = MercUIFactory.CreateImage(rowRect, $"Segmento{s}", teamColor,
                anchoredPos: new Vector2(x, 0f), size: new Vector2(segWidth, 16f));
            fill.type       = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            row.Segments[s] = fill;
        }

        return row;
    }

    private static int ResolvePointsToWin()
    {
        MercenariesGameMode gm = MercenariesGameMode.Instance;
        return gm != null ? Mathf.Clamp(gm.PointsToWin, 1, 6) : 2;
    }

    // Busca un Canvas hacia arriba; si no hay ninguno se crea uno propio, así el
    // componente funciona tanto colgado de un Canvas existente como suelto en la escena.
    private Transform ResolveCanvas()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null) return canvas.transform;

        canvas = MercUIFactory.CreateCanvas("Canvas_HUD_Partida");
        canvas.transform.SetParent(transform, false);
        return canvas.transform;
    }

    // =========================================================
    // REFRESCO
    // =========================================================

    private void Update()
    {
        if (_root == null) return;

        MercenariesGameMode gm = MercenariesGameMode.Instance;

        // Sin partida (o antes de que llegue el objeto de red) el marcador no se muestra.
        bool show = gm != null;
        if (_root.gameObject.activeSelf != show) _root.gameObject.SetActive(show);
        if (!show) return;

        int localTeam = MercUIFactory.LocalTeam();

        UpdateClock(gm);
        UpdateObjectiveLine(gm);

        foreach (TeamRow row in _rows)
        {
            if (row == null) continue;

            row.Level.text = $"Nv. {gm.GetLevel(row.Team)}";
            row.XpFill.fillAmount = gm.GetXpNormalized(row.Team);

            int score = gm.GetScore(row.Team);
            for (int s = 0; s < row.Segments.Length; s++)
                row.Segments[s].fillAmount = s < score ? 1f : 0f;

            // La fila del equipo propio se resalta para encontrarse rápido.
            row.Background.color = (row.Team == localTeam) ? LocalTeamBackground : PanelBackground;
            row.Label.text = row.Team == localTeam
                ? $"{MercenariesGameMode.TeamName(row.Team)} (VOS)"
                : MercenariesGameMode.TeamName(row.Team);
        }
    }

    private void UpdateClock(MercenariesGameMode gm)
    {
        switch (gm.State)
        {
            case EMatchState.Warmup:
                _clockText.text  = $"PREPARACIÓN  {MercUIFactory.FormatTime(gm.PhaseTimeRemaining)}";
                _clockText.color = new Color(1f, 0.9f, 0.4f);
                break;

            case EMatchState.Playing:
                _clockText.text  = MercUIFactory.FormatTime(gm.PhaseTimeRemaining);
                _clockText.color = Color.white;
                break;

            case EMatchState.Ended:
                int winner = gm.WinnerTeam;
                _clockText.text = winner > 0
                    ? $"GANA {MercenariesGameMode.TeamName(winner)}"
                    : "EMPATE";
                _clockText.color = winner > 0 ? gm.GetTeamColor(winner) : Color.white;
                break;
        }
    }

    // Dónde está el Objetivo, en una línea. El diseño pide que TODOS sepan siempre
    // dónde está — esto es la mitad de esa promesa; la otra mitad es UI_ObjectiveMarker,
    // que lo señala en el mundo.
    private void UpdateObjectiveLine(MercenariesGameMode gm)
    {
        if (gm.State != EMatchState.Playing)
        {
            _objectiveText.text = gm.State == EMatchState.Warmup
                ? "Elegí tu clase en la base. No se puede cambiar afuera."
                : "";
            _objectiveText.color = new Color(1f, 1f, 1f, 0.8f);
            return;
        }

        MercObjective objective = gm.ActiveObjective;

        if (objective != null && objective.IsCarried)
        {
            int team = objective.CarrierTeam;
            _objectiveText.text  = $"OBJETIVO · lo lleva {MercenariesGameMode.TeamName(team)}";
            _objectiveText.color = gm.GetTeamColor(team);
            return;
        }

        if (objective != null)
        {
            _objectiveText.text  = "OBJETIVO · en el mapa";
            _objectiveText.color = new Color(1f, 0.92f, 0.55f);
            return;
        }

        float eta = gm.ObjectiveEta;
        _objectiveText.text  = eta > 0f
            ? $"Próximo Objetivo en {MercUIFactory.FormatTime(eta)}"
            : "";
        _objectiveText.color = new Color(1f, 1f, 1f, 0.75f);
    }
}
