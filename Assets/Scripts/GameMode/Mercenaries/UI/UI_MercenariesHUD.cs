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
// VIVE EN EL PREFAB DE PARTIDA (Match_UI ▸ Scoreboard), desde el 14 de octubre de 2026: el
// panel, el reloj, el estado del Objetivo y las tres filas (fondo, franja de color, nombre,
// nivel, barrita de experiencia y un tramo por punto de victoria) están armados ahí y se
// acomodan a mano. Este script solo escribe los textos, pinta del color de cada equipo la
// franja, la barrita y los tramos, y los llena. Para cambiar cuántos puntos hacen falta para
// ganar, la fila tiene que tener esa cantidad de tramos (en "Tramos de entrega").
//
// Lee TODO de MercenariesGameMode (que es quien sincroniza el estado) y no toca nada:
// es una vista, sin lógica de juego.
// ============================================================
public class UI_MercenariesHUD : MonoBehaviour
{
    // Las piezas de la fila de un equipo.
    [System.Serializable]
    public class TeamRowPieces
    {
        [Tooltip("El fondo de la fila: se pinta con el color de tu equipo si es el tuyo.")]
        [Label("Fondo")]
        public Image Background;

        [Tooltip("La franja de color del equipo, a la izquierda.")]
        [Label("Franja de color")]
        public Image Chip;

        [Label("Nombre")]
        public TMP_Text Label;

        [Label("Nivel")]
        public TMP_Text Level;

        [Tooltip("La barrita de progreso hacia el próximo nivel (Image Filled horizontal).")]
        [Label("Barrita de experiencia")]
        public Image XpFill;

        [Tooltip("Un tramo por punto de victoria, de izquierda a derecha (Image Filled horizontal). " +
                 "Su fondo es otra Image detrás.")]
        [Label("Tramos de entrega")]
        public Image[] Segments;
    }

    [Header("Piezas")]
    [Tooltip("Todo el marcador: se esconde entero en la sala y si el espectador lo apaga.")]
    [Label("Panel")]
    public RectTransform Panel;

    [Label("Reloj")]
    public TMP_Text ClockText;

    [Tooltip("La línea de debajo: dónde está el Objetivo.")]
    [Label("Estado del Objetivo")]
    public TMP_Text ObjectiveText;

    [Tooltip("Una por equipo, en orden: equipo 1, 2 y 3.")]
    [Label("Filas de los equipos")]
    public TeamRowPieces[] Rows = new TeamRowPieces[MercenariesGameMode.TeamCount];

    [Header("Colores")]
    [Tooltip("El fondo de las filas de los otros equipos.")]
    [Label("Fondo de las filas")]
    public Color PanelBackground     = new Color(0f, 0f, 0f, 0.55f);

    [Tooltip("El fondo de la fila de tu equipo, para encontrarte rápido.")]
    [Label("Fondo de tu equipo")]
    public Color LocalTeamBackground = new Color(1f, 1f, 1f, 0.16f);

    // =========================================================
    // REFRESCO
    // =========================================================

    // True mientras la partida todavia no arranco: seguimos en la sala de espera.
    //
    // Se mira MatchStarted y NO el "listo" del jugador local: con el panel nuevo el que
    // confirma primero sigue en la sala esperando a los demas, y ver el marcador encima
    // del panel era justo el empalme que molestaba.
    //
    // Sin LobbyManager en la escena devuelve false, asi que las escenas que no usan la
    // sala (Test_Network) se comportan igual que antes: el marcador aparece apenas hay
    // partida.
    private static bool IsStillChoosingInLobby()
    {
        LobbyManager lobby = LobbyManager.Instance;
        if (lobby == null) return false;

        return !lobby.MatchStarted;
    }

    // El ESPECTADOR puede apagar el marcador para grabar limpio (ver SpectatorCamera).
    // Es un pedido explícito y no un estado del juego, así que gana sobre el cálculo
    // normal de visibilidad.
    public static bool ScoreboardHiddenByViewer;

    private void Update()
    {
        if (Panel == null) return;

        MercenariesGameMode gm = MercenariesGameMode.Instance;

        // Sin partida (o antes de que llegue el objeto de red) el marcador no se muestra.
        //
        // Y TAMPOCO mientras sigas en la SALA eligiendo: el marcador aparecia apenas se
        // conectaba el objeto de red, o sea encima del menu de clases y del panel de la
        // sala. Se espera a que el jugador local haya confirmado.
        bool show = gm != null && !IsStillChoosingInLobby() && !ScoreboardHiddenByViewer;
        if (Panel.gameObject.activeSelf != show) Panel.gameObject.SetActive(show);
        if (!show) return;

        int localTeam = MercUIFactory.LocalTeam();

        UpdateClock(gm);
        UpdateObjectiveLine(gm);

        for (int i = 0; i < Rows.Length; i++)
        {
            TeamRowPieces row = Rows[i];
            if (row == null) continue;
            int team = i + 1;
            Color teamColor = gm.GetTeamColor(team);

            if (row.Chip != null) row.Chip.color = teamColor;
            if (row.Level != null) row.Level.text = Loc.T("match.level_short", ("level", gm.GetLevel(team)));
            if (row.XpFill != null)
            {
                row.XpFill.color      = teamColor;
                row.XpFill.fillAmount = gm.GetXpNormalized(team);
            }

            int score = gm.GetScore(team);
            if (row.Segments != null)
                for (int s = 0; s < row.Segments.Length; s++)
                {
                    if (row.Segments[s] == null) continue;
                    row.Segments[s].color      = teamColor;
                    row.Segments[s].fillAmount = s < score ? 1f : 0f;
                }

            // La fila del equipo propio se resalta para encontrarse rápido.
            if (row.Background != null) row.Background.color = team == localTeam ? LocalTeamBackground : PanelBackground;
            if (row.Label != null)
                row.Label.text = team == localTeam
                    ? Loc.T("match.team_you", ("team", MercenariesGameMode.TeamName(team)))
                    : MercenariesGameMode.TeamName(team);
        }
    }

    private void UpdateClock(MercenariesGameMode gm)
    {
        if (ClockText == null) return;
        switch (gm.State)
        {
            case EMatchState.Warmup:
                ClockText.text  = Loc.T("match.get_ready", ("time", MercUIFactory.FormatTime(gm.PhaseTimeRemaining)));
                ClockText.color = new Color(1f, 0.9f, 0.4f);
                break;

            case EMatchState.Playing:
                ClockText.text  = MercUIFactory.FormatTime(gm.PhaseTimeRemaining);
                ClockText.color = Color.white;
                break;

            case EMatchState.Ended:
                int winner = gm.WinnerTeam;
                ClockText.text = winner > 0
                    ? Loc.T("match.wins", ("team", MercenariesGameMode.TeamName(winner)))
                    : Loc.T("match.draw");
                ClockText.color = winner > 0 ? gm.GetTeamColor(winner) : Color.white;
                break;
        }
    }

    // Dónde está el Objetivo, en una línea. El diseño pide que TODOS sepan siempre
    // dónde está — esto es la mitad de esa promesa; la otra mitad es UI_ObjectiveMarker,
    // que lo señala en el mundo.
    private void UpdateObjectiveLine(MercenariesGameMode gm)
    {
        if (ObjectiveText == null) return;
        if (gm.State != EMatchState.Playing)
        {
            ObjectiveText.text = gm.State == EMatchState.Warmup
                ? Loc.T("match.choose_class_in_base")
                : "";
            ObjectiveText.color = new Color(1f, 1f, 1f, 0.8f);
            return;
        }

        MercObjective objective = gm.ActiveObjective;

        if (objective != null && objective.IsCarried)
        {
            int team = objective.CarrierTeam;
            ObjectiveText.text  = Loc.T("match.objective_carried", ("team", MercenariesGameMode.TeamName(team)));
            ObjectiveText.color = gm.GetTeamColor(team);
            return;
        }

        if (objective != null)
        {
            ObjectiveText.text  = Loc.T("match.objective_on_map");
            ObjectiveText.color = new Color(1f, 0.92f, 0.55f);
            return;
        }

        float eta = gm.ObjectiveEta;
        ObjectiveText.text  = eta > 0f
            ? Loc.T("match.next_objective", ("time", MercUIFactory.FormatTime(eta)))
            : "";
        ObjectiveText.color = new Color(1f, 1f, 1f, 0.75f);
    }
}
