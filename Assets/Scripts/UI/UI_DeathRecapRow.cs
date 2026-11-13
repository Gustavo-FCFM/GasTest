using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Una línea del resumen de muerte (UI_ScreenFeedback): el ícono y el nombre de la habilidad, quién la usó (del color
// de su equipo) y cuánto daño hizo. El molde vive en el prefab de partida (Match_UI ▸ ScreenFeedback ▸ DeathPanel ▸
// Recap) y el script lo copia, una por línea. 13 de noviembre de 2026.
public class UI_DeathRecapRow : MonoBehaviour
{
    [Label("Ícono de la habilidad")]
    public Image Icon;

    [Label("Habilidad")]
    public TMP_Text Ability;

    [Tooltip("Quién la usó: un jugador, un NPC, tú mismo o el entorno.")]
    [Label("Quién")]
    public TMP_Text Attacker;

    [Label("Daño")]
    public TMP_Text Amount;

    public void Show(UI_ScreenFeedback.RecapLine line)
    {
        if (Icon != null)
        {
            Icon.sprite  = line.Icon;
            Icon.enabled = line.Icon != null;
        }
        if (Ability != null) Ability.text = line.Ability;
        if (Attacker != null)
        {
            Attacker.text  = line.Attacker;
            Attacker.color = line.Team >= 1 && line.Team <= 3 ? MercUIFactory.TeamColor(line.Team)   // un equipo
                           : line.Team > 3 ? new Color(0.95f, 0.6f, 0.45f)                       // los monstruos
                                           : Color.white;                                      // tú mismo, el entorno
        }
        if (Amount != null) Amount.text = line.Amount.ToString();
    }
}
