using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_ShrineHud
//
// El panel de los SANTUARIOS, debajo del marcador (11 de noviembre de 2026): el mismo para todos.
//   · Con un santuario activo: "SANTUARIO DE DAÑO" del color del tipo (y "DISPUTADO" si hay dos
//     equipos dentro), y una columna por equipo con su porcentaje. La columna de un equipo con
//     alguien dentro de la zona se aclara.
//   · Entre uno y otro: solo la línea "Siguiente santuario en 0:42".
//   · Sin santuarios en la escena, en la sala o con el marcador apagado por el espectador: nada.
//
// VIVE EN EL PREFAB DE PARTIDA (Match_UI ▸ Scoreboard ▸ ShrinePanel): las piezas están armadas ahí
// y se acomodan a mano. Este script va en Scoreboard (que nunca se apaga) y solo escribe, pinta y
// llena. Lee todo de MatchEvent_Shrines; no toca nada.
// ============================================================
public class UI_ShrineHud : MonoBehaviour
{
    // Las piezas de la columna de un equipo.
    [System.Serializable]
    public class TeamColumnPieces
    {
        [Tooltip("La columna entera: se esconde mientras no hay santuario activo.")]
        [Label("Raíz")]
        public RectTransform Root;

        [Tooltip("El fondo: se aclara mientras ese equipo tiene a alguien vivo dentro de la zona.")]
        [Label("Fondo")]
        public Image Background;

        [Tooltip("La franja de color del equipo, a la izquierda.")]
        [Label("Franja de color")]
        public Image Chip;

        [Tooltip("La barra del porcentaje (Image Filled horizontal).")]
        [Label("Barra")]
        public Image Fill;

        [Label("Porcentaje")]
        public TMP_Text Percent;
    }

    [Header("Piezas")]
    [Tooltip("Todo el panel: solo se ve con un santuario activo o con la cuenta atrás del siguiente.")]
    [Label("Panel")]
    public RectTransform Panel;

    [Tooltip("El título: el tipo de santuario, o cuánto falta para el siguiente.")]
    [Label("Título")]
    public TMP_Text Title;

    [Tooltip("Una por equipo, en orden: equipo 1, 2 y 3.")]
    [Label("Columnas de los equipos")]
    public TeamColumnPieces[] Columns = new TeamColumnPieces[MercenariesGameMode.TeamCount];

    [Header("Colores")]
    [Label("Fondo de las columnas")]
    public Color ColumnBackground = new Color(0f, 0f, 0f, 0.55f);

    [Tooltip("El fondo de la columna de un equipo con alguien vivo dentro de la zona.")]
    [Label("Fondo con alguien dentro")]
    public Color InsideBackground = new Color(1f, 1f, 1f, 0.22f);

    [Tooltip("El color de \"DISPUTADO\" en el título.")]
    [Label("Color de disputado")]
    public Color ContestedColor = new Color(1f, 0.3f, 0.22f);

    [Tooltip("El color del título entre un santuario y otro.")]
    [Label("Color de la cuenta atrás")]
    public Color WaitingColor = new Color(1f, 1f, 1f, 0.8f);

    private void Update()
    {
        if (Panel == null) return;

        MatchEvent_Shrines shrines = MatchEvent_Shrines.Instance;
        MercenariesGameMode gm = MercenariesGameMode.Instance;

        bool inLobby = LobbyManager.Instance != null && !LobbyManager.Instance.MatchStarted;
        bool active  = shrines != null && shrines.IsActive;
        float eta    = shrines != null ? shrines.NextShrineEta : 0f;

        bool show = shrines != null && gm != null && gm.State == EMatchState.Playing && !inLobby &&
                    !UI_MercenariesHUD.ScoreboardHiddenByViewer && (active || eta > 0f);
        if (Panel.gameObject.activeSelf != show) Panel.gameObject.SetActive(show);
        if (!show) return;

        if (active) ShowActive(shrines, gm);
        else        ShowWaiting(eta);
    }

    private void ShowActive(MatchEvent_Shrines shrines, MercenariesGameMode gm)
    {
        EShrineType type = shrines.ActiveType;

        if (Title != null)
        {
            string title = Loc.T("shrine.title", ("type", MatchEvent_Shrines.TypeName(type)));
            if (shrines.IsContested)
                title += $"   <color=#{ColorUtility.ToHtmlStringRGB(ContestedColor)}>{Loc.T("shrine.contested")}</color>";
            Title.text  = title;
            Title.color = shrines.GetColor(type);
        }

        for (int i = 0; i < Columns.Length; i++)
        {
            TeamColumnPieces column = Columns[i];
            if (column == null) continue;
            int team = i + 1;
            Color teamColor = gm.GetTeamColor(team);
            int progress = shrines.GetProgress(team);

            if (column.Root != null && !column.Root.gameObject.activeSelf) column.Root.gameObject.SetActive(true);
            if (column.Background != null)
                column.Background.color = shrines.IsTeamInside(team) ? InsideBackground : ColumnBackground;
            if (column.Chip != null) column.Chip.color = teamColor;
            if (column.Fill != null)
            {
                column.Fill.color      = teamColor;
                column.Fill.fillAmount = progress / (float)ShrineCaptureRules.Full;
            }
            if (column.Percent != null) column.Percent.text = progress + "%";
        }
    }

    private void ShowWaiting(float eta)
    {
        if (Title != null)
        {
            Title.text  = Loc.T("shrine.next", ("time", MercUIFactory.FormatTime(eta)));
            Title.color = WaitingColor;
        }

        foreach (TeamColumnPieces column in Columns)
            if (column != null && column.Root != null && column.Root.gameObject.activeSelf)
                column.Root.gameObject.SetActive(false);
    }
}
