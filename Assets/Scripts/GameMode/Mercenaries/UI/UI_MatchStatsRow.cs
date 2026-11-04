using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Una fila de la tabla de la partida (UI_MatchStats): el molde vive en el prefab de partida
// y el script lo copia, una fila por integrante.
public class UI_MatchStatsRow : MonoBehaviour
{
    [Label("Fondo")]
    public Image Background;

    [Tooltip("El ícono de su clase.")]
    [Label("Ícono")]
    public Image Icon;

    [Label("Nombre")]
    public TMP_Text PlayerName;

    [Label("Bajas")]
    public TMP_Text Kills;

    [Label("Muertes")]
    public TMP_Text Deaths;

    [Label("Asistencias")]
    public TMP_Text Assists;

    public void Show(string playerName, Sprite icon, int kills, int deaths, int assists, Color background)
    {
        if (Background != null) Background.color = background;
        if (Icon != null)
        {
            Icon.sprite  = icon;
            Icon.enabled = icon != null;
        }
        if (PlayerName != null) PlayerName.text = playerName;
        if (Kills != null) Kills.text = kills.ToString();
        if (Deaths != null) Deaths.text = deaths.ToString();
        if (Assists != null) Assists.text = assists.ToString();
    }
}
