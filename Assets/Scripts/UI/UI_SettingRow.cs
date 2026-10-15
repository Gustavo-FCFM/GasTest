using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Qué ajuste maneja una fila del panel de Ajustes. Se guarda por número en el prefab: se
// agrega SOLO al final.
public enum ESettingKind
{
    [InspectorName("Idioma")]                    Language       = 0,
    [InspectorName("Control (teclado o mando)")] InputScheme    = 1,
    [InspectorName("Sensibilidad de la cámara")] Sensitivity    = 2,
    [InspectorName("Invertir eje Y")]            InvertY        = 3,
    [InspectorName("Volumen general")]           MasterVolume   = 4,
    [InspectorName("Música")]                    MusicVolume    = 5,
    [InspectorName("Efectos")]                   EffectsVolume  = 6,
    [InspectorName("Modo de pantalla")]          ScreenMode     = 7,
    [InspectorName("Resolución")]                Resolution     = 8,
    [InspectorName("Sincronización vertical")]   VSync          = 9,
}

// ============================================================
// UI_SettingRow
//
// Una fila del panel de Ajustes (Menus_UI ▸ Settings): dice QUÉ ajuste maneja y dónde
// están sus controles. El panel (UI_SettingsPanel) busca todas las filas al abrirse y
// conecta cada una según su ajuste; la fila no tiene lógica propia.
//
// Según el ajuste, la fila usa:
//   · una BARRA (sensibilidad, volúmenes) y el texto del valor;
//   · una CASILLA (invertir Y, sincronización vertical);
//   · las flechas < > y el texto del valor (idioma, control, modo de pantalla, resolución).
// Lo que no use, puede quedar vacío.
//
// Para agregar un ajuste que ya existe (por ejemplo prender la sección de sonido):
// duplicar una fila del mismo tipo, moverla y cambiarle el ajuste y la etiqueta.
// ============================================================
public class UI_SettingRow : MonoBehaviour
{
    [Label("Ajuste")]
    public ESettingKind Setting;

    [Tooltip("La barra (solo los ajustes que son un número: sensibilidad, volúmenes).")]
    [Label("Barra")]
    public Slider Slider;

    [Tooltip("La casilla (solo los de sí/no).")]
    [Label("Casilla")]
    public Toggle Toggle;

    [Tooltip("La flecha de la izquierda (solo los de una lista).")]
    [Label("Anterior")]
    public Button Previous;

    [Tooltip("La flecha de la derecha (solo los de una lista).")]
    [Label("Siguiente")]
    public Button Next;

    [Tooltip("El texto del valor (\"1.0×\", \"Español\", \"1920 × 1080\").")]
    [Label("Valor")]
    public TMP_Text Value;
}
