using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_MatchStats
//
// La tabla de la partida: cada equipo con sus integrantes y su KDA (bajas, muertes y
// asistencias), más el daño y la curación hechos (desde el 13 de noviembre de 2026). Se ve mientras se mantiene Tab (o Share / View en el control) y sola al terminar la
// partida, con quién ganó en el título.
//
// VIVE EN EL PREFAB DE PARTIDA (Match_UI ▸ MatchStats), desde el 4 de noviembre de 2026: el
// panel, el título y los tres bloques de equipo están armados ahí y se acomodan a mano. La
// fila es un MOLDE: se apaga y se copia una por integrante.
//
// Lee de los personajes (PlayerController.Kills/Deaths/Assists, que cuenta el servidor) y de
// MercenariesGameMode, y no toca nada: es una vista, sin lógica de juego.
// ============================================================
public class UI_MatchStats : MonoBehaviour
{
    // Las piezas del bloque de un equipo.
    [System.Serializable]
    public class TeamPieces
    {
        [Tooltip("La franja del título del equipo: se pinta de su color.")]
        [Label("Franja de color")]
        public Image Header;

        [Label("Nombre")]
        public TMP_Text Name;

        [Tooltip("El nivel y los puntos del equipo.")]
        [Label("Nivel y puntos")]
        public TMP_Text Info;

        [Tooltip("Dónde van las filas de sus integrantes.")]
        [Label("Filas")]
        public RectTransform Rows;
    }

    [Header("Piezas")]
    [Tooltip("Toda la tabla: se esconde entera cuando no se usa.")]
    [Label("Panel")]
    public RectTransform Panel;

    [Tooltip("Arriba de todo. Al terminar la partida dice quién ganó.")]
    [Label("Título")]
    public TMP_Text Title;

    [Tooltip("Uno por equipo, en orden: equipo 1, 2 y 3.")]
    [Label("Bloques de los equipos")]
    public TeamPieces[] Teams = new TeamPieces[MercenariesGameMode.TeamCount];

    [Tooltip("El molde de una fila. Se apaga solo; cada integrante es una copia.")]
    [Label("Molde de la fila")]
    public UI_MatchStatsRow RowTemplate;

    [Header("Colores")]
    [Tooltip("El fondo de las filas.")]
    [Label("Fondo de las filas")]
    public Color RowBackground = new Color(0f, 0f, 0f, 0.45f);

    [Tooltip("El fondo de tu fila, para encontrarte rápido.")]
    [Label("Fondo de tu fila")]
    public Color LocalRowBackground = new Color(1f, 0.75f, 0f, 0.45f);

    [Header("Ritmo")]
    [Tooltip("Cada cuánto se vuelve a leer la tabla mientras está abierta.")]
    [Label("Refresco (s)")]
    public float RefreshSeconds = 0.25f;

    private readonly List<UI_MatchStatsRow>[] _rows =
        { new List<UI_MatchStatsRow>(), new List<UI_MatchStatsRow>(), new List<UI_MatchStatsRow>() };
    private readonly List<PlayerController>[] _members =
        { new List<PlayerController>(), new List<PlayerController>(), new List<PlayerController>() };

    private float _nextRefresh;

    private void Awake()
    {
        if (RowTemplate != null) RowTemplate.gameObject.SetActive(false);
        if (Panel != null) Panel.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (Panel == null) return;

        bool show = ShouldShow(MercenariesGameMode.Instance);
        if (Panel.gameObject.activeSelf != show)
        {
            Panel.gameObject.SetActive(show);
            _nextRefresh = 0f;
        }
        if (!show || Time.unscaledTime < _nextRefresh) return;

        _nextRefresh = Time.unscaledTime + RefreshSeconds;
        Refresh(MercenariesGameMode.Instance);
    }

    // Al terminar la partida se ve sola; jugando, mientras se mantenga el botón (y no encima de
    // la pausa o los ajustes). En la sala, nunca.
    private static bool ShouldShow(MercenariesGameMode gm)
    {
        if (gm == null) return false;

        LobbyManager lobby = LobbyManager.Instance;
        if (lobby != null && !lobby.MatchStarted) return false;

        if (gm.State == EMatchState.Ended) return true;
        return MenuInput.Scoreboard != null && MenuInput.Scoreboard.IsPressed() && !MenuInput.HasOverlay;
    }

    private void Refresh(MercenariesGameMode gm)
    {
        if (Title != null) Title.text = TitleText(gm);

        foreach (List<PlayerController> team in _members) team.Clear();

        foreach (PlayerController pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            int team = TeamOf(pc);
            if (team >= 1 && team <= _members.Length) _members[team - 1].Add(pc);
        }

        for (int i = 0; i < Teams.Length && i < _members.Length; i++)
        {
            TeamPieces pieces = Teams[i];
            if (pieces == null) continue;
            int team = i + 1;

            if (pieces.Header != null) pieces.Header.color = gm.GetTeamColor(team);
            if (pieces.Name != null) pieces.Name.text = MercenariesGameMode.TeamName(team);
            if (pieces.Info != null)
                pieces.Info.text = Loc.T("stats.team_info", ("level", gm.GetLevel(team)), ("points", gm.GetScore(team)));

            _members[i].Sort(ByKills);
            ShowRows(i, pieces);
        }
    }

    private static string TitleText(MercenariesGameMode gm)
    {
        if (gm.State != EMatchState.Ended) return Loc.T("stats.title");
        return gm.WinnerTeam > 0
            ? Loc.T("match.wins", ("team", MercenariesGameMode.TeamName(gm.WinnerTeam)))
            : Loc.T("match.draw");
    }

    // Más bajas arriba; con las mismas, el que murió menos.
    private static int ByKills(PlayerController a, PlayerController b)
    {
        int kills = b.Kills.CompareTo(a.Kills);
        return kills != 0 ? kills : a.Deaths.CompareTo(b.Deaths);
    }

    private void ShowRows(int teamIndex, TeamPieces pieces)
    {
        if (RowTemplate == null || pieces.Rows == null) return;

        List<UI_MatchStatsRow> rows = _rows[teamIndex];
        List<PlayerController> members = _members[teamIndex];

        while (rows.Count < members.Count)
            rows.Add(Instantiate(RowTemplate, pieces.Rows));

        for (int i = 0; i < rows.Count; i++)
        {
            bool used = i < members.Count;
            if (rows[i].gameObject.activeSelf != used) rows[i].gameObject.SetActive(used);
            if (!used) continue;

            PlayerController pc = members[i];
            CharacterClassDefinition def = pc.GetClassByIndex(pc.VisualClassIndex);
            rows[i].Show(NameOf(pc), def != null ? def.ClassIcon : null, pc.Kills, pc.Deaths, pc.Assists, pc.DamageDone, pc.HealingDone,
                         pc == PlayerController.LocalPlayer ? LocalRowBackground : RowBackground);
        }
    }

    private static int TeamOf(PlayerController pc)
    {
        NetworkAbilitySystemComponent net = pc.GetComponent<NetworkAbilitySystemComponent>();
        return net != null ? net.NetTeamID : 0;
    }

    private static string NameOf(PlayerController pc)
    {
        NetworkAbilitySystemComponent net = pc.GetComponent<NetworkAbilitySystemComponent>();
        return net != null && !string.IsNullOrEmpty(net.PlayerName) ? net.PlayerName : pc.gameObject.name;
    }
}
