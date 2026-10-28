using TMPro;
using UnityEngine;

// ============================================================
// UI_PadClaim
//
// El cartel "Aprieta A en tu control" de una ventana abierta con -pad (LocalSeat; 28 de
// octubre de 2026, etapa 3 del showcase). Tapa todo mientras la ventana espera su control; al
// tomarlo dice "¡Listo!" un momento y se va. Si el control se desconecta, vuelve.
//
// Solo lo MUESTRA: quien escucha la A y se queda con el control es LocalSeat. Mientras espera,
// la ventana no responde a nada (no tiene ningún dispositivo), así que el cartel no necesita
// bloquear los menús de abajo.
//
// La ayuda cambia sola: con la ventana al frente, "esta ventana juega con el control que
// apriete A"; sin el foco, "haz clic en esta ventana y después aprieta A" (una ventana de
// atrás no toma el control: si no, la A de un jugador la tomarían todas).
//
// VIVE EN EL PREFAB DE MENÚS (Menus_UI ▸ PadClaim): el velo, el panel y los textos están
// armados ahí y se acomodan a mano. Va encima de todo (orden 600).
// ============================================================
public class UI_PadClaim : MonoBehaviour
{
    [Header("Piezas")]
    [Tooltip("El canvas del cartel: se prende mientras la ventana espera su control.")]
    [Label("Canvas del cartel")]
    public GameObject ClaimCanvas;

    [Tooltip("\"Aprieta A en tu control\", o \"¡Listo!\" al tomarlo.")]
    [Label("Título")]
    public TMP_Text TitleText;

    [Tooltip("La explicación de abajo: hacer clic en la ventana, o que se desconectó el control.")]
    [Label("Ayuda")]
    public TMP_Text HintText;

    [Header("Tiempos")]
    [Tooltip("Cuánto se ve el \"¡Listo!\" después de tomar el control.")]
    [Label("¡Listo! a la vista (s)")]
    public float ReadySeconds = 1.5f;

    private bool  _wasWaiting;
    private float _readyUntil = -1f;

    private void Awake()
    {
        if (ClaimCanvas != null) ClaimCanvas.SetActive(false);
    }

    private void Update()
    {
        bool waiting = LocalSeat.WaitingForPad;
        if (_wasWaiting && !waiting) _readyUntil = Time.unscaledTime + ReadySeconds;
        _wasWaiting = waiting;

        bool ready = !waiting && Time.unscaledTime < _readyUntil;
        bool show  = waiting || ready;
        if (ClaimCanvas != null && ClaimCanvas.activeSelf != show) ClaimCanvas.SetActive(show);
        if (!show) return;

        Write(TitleText, ready ? "seat.ready" : "seat.press_a");
        Write(HintText,  ready ? "seat.ready_hint"
                       : LocalSeat.LostPad    ? "seat.lost"
                       : Application.isFocused ? "seat.focused"
                                               : "seat.click");
    }

    // Se escribe cada frame (cambia el foco, cambia el idioma), pero solo se toca el texto si
    // de verdad cambió.
    private static void Write(TMP_Text text, string key)
    {
        if (text == null) return;
        string s = Loc.T(key);
        if (text.text != s) text.text = s;
    }
}
