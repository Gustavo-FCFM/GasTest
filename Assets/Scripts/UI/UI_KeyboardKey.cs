using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Qué hace una tecla del teclado en pantalla. Se agrega SIEMPRE AL FINAL (el inspector guarda
// el número).
public enum EKeyboardKey
{
    Character,   // escribe su letra
    Shift,       // la próxima letra en mayúscula
    Space,
    Backspace,   // borra la última
    Done,        // acepta lo escrito y cierra
    Cancel,      // cierra sin cambiar nada
}

// ============================================================
// UI_KeyboardKey
//
// Una tecla del teclado en pantalla (UI_OnScreenKeyboard). Vive en el prefab de menús
// (Menus_UI ▸ Keyboard ▸ … ▸ Keys): para agregar una letra, se duplica una tecla y se le cambia
// la "Letra"; para sacarla, se borra. El teclado encuentra todas solo.
// ============================================================
public class UI_KeyboardKey : MonoBehaviour
{
    [Tooltip("Qué hace: escribir su letra, o una de las teclas especiales.")]
    [Label("Tecla")]
    public EKeyboardKey Kind;

    [Tooltip("Lo que escribe (solo si la tecla es Character). Va en minúscula: con Mayús sale en mayúscula.")]
    [Label("Letra")]
    public string Character = "a";

    [Label("Botón")]
    public Button Button;

    [Tooltip("El texto de la tecla. En las letras lo escribe el teclado (cambia con Mayús); en las especiales lo pone su LocalizedText.")]
    [Label("Texto")]
    public TMP_Text Label;
}
