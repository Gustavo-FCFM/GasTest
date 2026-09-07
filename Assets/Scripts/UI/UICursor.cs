using System.Collections.Generic;
using UnityEngine;

// ============================================================
// UICursor
//
// UN SOLO dueño del cursor (y del modo de input). Antes cada menú lo fijaba y lo
// soltaba por su cuenta —PlayerController, UI_ClassMenu, UI_RadialMenu, la sala— y el
// último en cerrarse ganaba: cerrar el menú de clases con el recuadro de red abierto te
// volvía a tragar el mouse, y no había forma de llegar al botón de Desconectar.
//
// Acá cada menú PIDE el cursor mientras lo necesita y lo SUELTA al cerrarse. Queda
// libre si lo pide aunque sea uno.
//
//   void OnEnable()  => UICursor.Request(this);
//   void OnDisable() => UICursor.Release(this);
//
// blockGameplayInput apaga además el mapa de juego del Input System, para que la cámara
// no gire mientras clickeás un panel. La RUEDA de habilidades es la excepción y pide el
// cursor SIN bloquear: elige la opción con el stick/las teclas de movimiento, así que
// necesita el mapa de juego prendido (ver PlayerController).
//
// Los que se destruyen sin soltarlo no lo dejan trabado: se limpian solos en Apply().
// ============================================================
public static class UICursor
{
    private static readonly HashSet<Object> _cursorHolders = new HashSet<Object>();
    private static readonly HashSet<Object> _inputBlockers = new HashSet<Object>();

    // True si algún menú tiene el mouse pedido.
    public static bool Free { get; private set; }

    public static void Request(Object owner, bool blockGameplayInput = true)
    {
        if (owner == null) return;

        bool changed = _cursorHolders.Add(owner);
        changed |= blockGameplayInput ? _inputBlockers.Add(owner) : _inputBlockers.Remove(owner);

        // Solo se reaplica si algo CAMBIÓ: el recuadro de red lo pide en cada Update, y
        // prender y apagar los mapas de input sesenta veces por segundo no hace falta.
        if (changed) Apply();
    }

    public static void Release(Object owner)
    {
        if (owner == null) return;

        bool changed = _cursorHolders.Remove(owner);
        changed |= _inputBlockers.Remove(owner);

        if (changed) Apply();
    }

    // Se puede llamar suelto para reafirmar el estado — lo hace el jugador al spawnear,
    // que antes fijaba el cursor a mano y pisaba lo que hubiera abierto.
    public static void Apply()
    {
        // Un menú destruido no puede soltar nada: se lo saca acá. El == de Unity da true
        // para objetos destruidos, que es justo lo que hace falta.
        _cursorHolders.RemoveWhere(o => o == null);
        _inputBlockers.RemoveWhere(o => o == null);

        Free = _cursorHolders.Count > 0;
        Cursor.lockState = Free ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible   = Free;

        // Local es null en la sala (todavía no hay personaje): no pasa nada, el jugador
        // llama a Apply() al spawnear y ahí se acomoda.
        if (PlayerInputProvider.Local != null)
            PlayerInputProvider.Local.SetUIMode(_inputBlockers.Count > 0);
    }
}
