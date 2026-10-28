// ============================================================
// InputGlyphs
//
// Qué botón mostrar en pantalla: "Q" con teclado, "RB" con un control de Xbox, "R1"
// con uno de PlayStation. Es UNA SOLA TABLA, y todo lo que muestre un botón la
// consulta — el HUD de habilidades, los avisos ("Presiona V para elegir una
// Subclase"), los números de las tarjetas de clase.
//
// LO QUE ELIGE EL JUGADOR ES LO QUE SE DIBUJA, NADA MÁS. El juego sigue escuchando
// teclado Y control a la vez, como siempre: esto no cambia un solo binding. Si alguien
// pone "Control de Xbox" y después agarra el teclado, la Q le va a funcionar igual,
// solo que el HUD dirá RB. Se hace así a propósito: adivinar el dispositivo en cada
// frame hace que el HUD parpadee entre un esquema y otro cuando se apoya la mano en el
// teclado sin querer.
//
// POR QUÉ NO HAY SÍMBOLOS DE PLAYSTATION (△ ○ ✕ □): la fuente TMP del proyecto no los
// tiene y saldrían como cuadraditos — el mismo problema que tuvimos con las flechas
// ◀ ▶ del panel de ajustes. Por eso los de arriba van como texto corto. El día que
// haya un sprite asset con los botones dibujados, se cambia SOLO esta tabla (TMP los
// mete en línea con <sprite name="...">) y todo el juego lo hereda.
// ============================================================

// Qué está usando el jugador. Se guarda por NÚMERO en PlayerPrefs: agregar valores
// SOLO AL FINAL, igual que con los enums que se serializan en los assets.
public enum EInputScheme
{
    KeyboardMouse = 0,
    Xbox          = 1,
    PlayStation   = 2
}

public static class InputGlyphs
{
    // Lo elegido en Ajustes, salvo que la ventana tenga un puesto (LocalSeat): con varias
    // ventanas en una PC los Ajustes son los mismos para todas, y la de control tiene que
    // dibujar RB aunque la de teclado dibuje Q.
    public static EInputScheme Scheme => LocalSeat.Scheme ?? GameSettings.InputScheme;

    // True si hay que mostrar cosas que solo existen en teclado, como el número que
    // elige una tarjeta de clase (1/2/3). Con control esas teclas siguen funcionando,
    // pero mostrarlas confundiría: ahí se elige con el stick y el botón de confirmar.
    public static bool ShowKeyboardHints => Scheme == EInputScheme.KeyboardMouse;

    // El nombre que se ve en el panel de ajustes, en el idioma elegido (Loc).
    public static string SchemeName(EInputScheme scheme)
    {
        switch (scheme)
        {
            case EInputScheme.Xbox:        return Loc.T("input.xbox");
            case EInputScheme.PlayStation: return Loc.T("input.playstation");
            default:                       return Loc.T("input.keyboard_mouse");
        }
    }

    // =========================================================
    // LA TABLA
    //
    // Tiene que coincidir con InputSystem_Actions.inputactions. Si ahí se cambia un
    // binding, acá también.
    // =========================================================

    // Habilidades del HUD.
    public static string For(EAbilityInput slot)
    {
        switch (Scheme)
        {
            case EInputScheme.Xbox:
                switch (slot)
                {
                    case EAbilityInput.PrimaryAttack:   return "RT";   // rightTrigger
                    case EAbilityInput.SecondaryAttack: return "LT";   // leftTrigger
                    case EAbilityInput.Action1:         return "RB";   // rightShoulder
                    case EAbilityInput.Action2:         return "LB";   // leftShoulder
                    case EAbilityInput.Action3:         return "Y";    // buttonNorth
                    case EAbilityInput.Movement:        return "B";    // buttonEast
                    default:                            return "";
                }

            case EInputScheme.PlayStation:
                switch (slot)
                {
                    case EAbilityInput.PrimaryAttack:   return "R2";
                    case EAbilityInput.SecondaryAttack: return "L2";
                    case EAbilityInput.Action1:         return "R1";
                    case EAbilityInput.Action2:         return "L1";
                    case EAbilityInput.Action3:         return "TRI";  // Triángulo
                    case EAbilityInput.Movement:        return "CIR";  // Círculo
                    default:                            return "";
                }

            default:
                switch (slot)
                {
                    case EAbilityInput.PrimaryAttack:   return Loc.T("glyph.lmb");
                    case EAbilityInput.SecondaryAttack: return Loc.T("glyph.rmb");
                    case EAbilityInput.Action1:         return "Q";
                    case EAbilityInput.Action2:         return "E";
                    case EAbilityInput.Action3:         return "R";
                    case EAbilityInput.Movement:        return "Shift";
                    default:                            return "";
                }
        }
    }

    // Elegir subclase (acción "Cheat" en el asset de input).
    public static string Subclass
    {
        get
        {
            switch (Scheme)
            {
                case EInputScheme.Xbox:        return "VIEW";     // select
                case EInputScheme.PlayStation: return "CREATE";   // select
                default:                       return "V";
            }
        }
    }

    // Cambiar de clase base (acción "ChangeClass").
    public static string ChangeClass
    {
        get
        {
            switch (Scheme)
            {
                case EInputScheme.Xbox:
                case EInputScheme.PlayStation: return Loc.T("glyph.dpad_up");   // dpad/up
                default:                       return "C";
            }
        }
    }

    // Saltar.
    public static string Jump
    {
        get
        {
            switch (Scheme)
            {
                case EInputScheme.Xbox:        return "A";   // buttonSouth
                case EInputScheme.PlayStation: return "X";   // Cruz
                default:                       return Loc.T("glyph.space");
            }
        }
    }

    // Confirmar en un menú.
    public static string Confirm
    {
        get
        {
            switch (Scheme)
            {
                case EInputScheme.Xbox:        return "A";
                case EInputScheme.PlayStation: return "X";
                default:                       return Loc.T("glyph.click");
            }
        }
    }

    // La cámara de espectador (mapa "Spectator" del asset de input).
    public static string Spectator(ESpectatorInput input)
    {
        switch (Scheme)
        {
            case EInputScheme.Xbox:
                switch (input)
                {
                    case ESpectatorInput.Move:       return Loc.T("glyph.left_stick");
                    case ESpectatorInput.Up:         return "RT";
                    case ESpectatorInput.Down:       return "LT";
                    case ESpectatorInput.Fast:       return "L3";     // leftStickPress
                    case ESpectatorInput.Next:       return "RB";
                    case ESpectatorInput.Previous:   return "LB";
                    case ESpectatorInput.FreeCam:    return "Y";      // buttonNorth
                    case ESpectatorInput.Info:       return "X";      // buttonWest
                    case ESpectatorInput.Scoreboard: return "VIEW";   // select
                    default:                         return "";
                }

            case EInputScheme.PlayStation:
                switch (input)
                {
                    case ESpectatorInput.Move:       return Loc.T("glyph.left_stick");
                    case ESpectatorInput.Up:         return "R2";
                    case ESpectatorInput.Down:       return "L2";
                    case ESpectatorInput.Fast:       return "L3";
                    case ESpectatorInput.Next:       return "R1";
                    case ESpectatorInput.Previous:   return "L1";
                    case ESpectatorInput.FreeCam:    return "TRI";    // Triángulo
                    case ESpectatorInput.Info:       return "CUA";    // Cuadrado
                    case ESpectatorInput.Scoreboard: return "CREATE";
                    default:                         return "";
                }

            default:
                switch (input)
                {
                    case ESpectatorInput.Move:       return "WASD";
                    case ESpectatorInput.Up:         return Loc.T("glyph.space");
                    case ESpectatorInput.Down:       return "Ctrl";
                    case ESpectatorInput.Fast:       return "Shift";
                    case ESpectatorInput.Next:       return Loc.T("glyph.lmb");
                    case ESpectatorInput.Previous:   return Loc.T("glyph.rmb");
                    case ESpectatorInput.FreeCam:    return "F";
                    case ESpectatorInput.Info:       return "H";
                    case ESpectatorInput.Scoreboard: return "M";
                    default:                         return "";
                }
        }
    }
}

// Las acciones de la cámara de espectador que se muestran en su ayuda.
public enum ESpectatorInput
{
    Move,
    Up,
    Down,
    Fast,
    Next,
    Previous,
    FreeCam,
    Info,
    Scoreboard,
}
