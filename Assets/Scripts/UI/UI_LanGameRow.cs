using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_LanGameRow
//
// Una fila de la lista de partidas de la pantalla de conexión: un botón con el nombre de la
// sala y los detalles (jugadores, si ya están jugando, dónde está). Apretarla conecta.
//
// El MOLDE vive en el prefab de menús (Menus_UI ▸ Connect ▸ … ▸ GamesList ▸ GameRow), apagado:
// UI_ConnectPanel lo copia una vez por partida encontrada. Se acomoda a mano como cualquier
// botón, y las copias salen iguales.
// ============================================================
public class UI_LanGameRow : MonoBehaviour
{
    [Label("Botón")]
    public Button Button;

    [Tooltip("\"Sala de Gustavo\".")]
    [Label("Texto del nombre")]
    public TMP_Text NameText;

    [Tooltip("Jugadores, si ya están jugando y dónde está la partida (esta PC o su IP).")]
    [Label("Texto de los detalles")]
    public TMP_Text InfoText;
}
