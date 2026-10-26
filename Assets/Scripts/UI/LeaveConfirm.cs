using UnityEngine;
using UnityEngine.EventSystems;

// ============================================================
// LeaveConfirm
//
// El "¿seguro?" del botón Salir siendo host (26 de octubre de 2026, para el showcase). Siendo
// host, Salir cierra la partida para TODOS, y con gente que no conoce el juego agarrando el
// control se aprieta sin querer. Así que el host lo aprieta dos veces: la primera cambia el
// texto del botón a la pregunta y la segunda sale. Los demás salen de una (solo se van ellos).
//
// La pregunta se retira sola si pasan los segundos o si la selección se va a otro botón.
//
// Lo usan la pausa (UI_PauseMenu) y la sala (UI_LobbyPanel): cada uno tiene el suyo y
// escribe su texto según Asking.
// ============================================================
public sealed class LeaveConfirm
{
    private float _until;   // hasta cuándo el botón está preguntando
    private bool  _was;     // Asking en el frame anterior (para saber cuándo redibujar)

    public bool Asking => Time.unscaledTime < _until;

    // Apretaron Salir. True = salir ya. False = recién preguntó: hay que redibujar el botón.
    public bool Press(bool host, float seconds)
    {
        if (!host || Asking) { Cancel(); return true; }
        _until = Time.unscaledTime + seconds;
        return false;
    }

    public void Cancel() => _until = 0f;

    // Cada frame, con el menú a la vista. True si el texto del botón tiene que cambiar: se
    // venció la pregunta, o la selección se fue a otro botón (con el control, moverse ya es
    // decir que no).
    public bool Changed(GameObject button)
    {
        if (Asking)
        {
            GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (sel != null && sel != button) Cancel();
        }

        bool now = Asking;
        bool changed = now != _was;
        _was = now;
        return changed;
    }
}
