using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Qué estadística muestra una fila de la tarjeta de clase. Se guarda por número en el prefab: se agrega SOLO al final.
public enum EClassStat
{
    [InspectorName("Vida máxima")]          MaxHealth   = 0,
    [InspectorName("Velocidad de movimiento")] MoveSpeed = 1,
    [InspectorName("Velocidad de ataque")]  AttackSpeed = 2,
    [InspectorName("Armadura (defensa)")]   Armor       = 3,
}

// ============================================================
// UI_ClassStatRow
//
// Una fila de estadísticas de la tarjeta de clase (13 de noviembre de 2026, pedido de Gustavo): el nombre y una barra
// partida en segmentos, llenos según la clase, para comparar de un vistazo sin leer números (los jugadores casuales
// se confunden con ellos). Al pasar el mouse por la fila, en vez del nombre aparece el número real.
//
// Las piezas están ARMADAS en el prefab ClassCard (Stats ▸ una fila por estadística, con sus segmentos); este script
// solo las pinta. La escala ("Vacía con" / "Llena con") se cambia acá: si "Vacía con" es mayor que "Llena con", la
// barra va al revés (la velocidad de ataque es el TIEMPO entre ataques: menos es mejor).
// ============================================================
public class UI_ClassStatRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Label("Estadística")]
    public EClassStat Stat;

    [Tooltip("El valor con el que la barra queda vacía.")]
    [Label("Vacía con")]
    public float EmptyAt = 0f;

    [Tooltip("El valor con el que la barra queda llena. Si es menor que \"Vacía con\", la barra va al revés.")]
    [Label("Llena con")]
    public float FullAt = 300f;

    [Header("Piezas")]
    [Label("Nombre")]
    public TMP_Text Label;

    [Tooltip("El número real: aparece en el lugar del nombre al pasar el mouse.")]
    [Label("Número")]
    public TMP_Text Value;

    [Tooltip("Los segmentos de la barra, de izquierda a derecha.")]
    [Label("Segmentos")]
    public Image[] Segments;

    [Header("Colores")]
    [Label("Lleno")]
    public Color FilledColor = new Color(0.93f, 0.27f, 0.48f);

    [Label("Vacío")]
    public Color EmptyColor = new Color(0.22f, 0.25f, 0.36f);

    public void Show(CharacterClassDefinition def)
    {
        float value = Read(def, Stat, out bool found);
        gameObject.SetActive(found);
        if (!found) return;

        float t = Mathf.Approximately(FullAt, EmptyAt) ? 1f : Mathf.Clamp01((value - EmptyAt) / (FullAt - EmptyAt));
        int count = Segments != null ? Segments.Length : 0;
        int filled = count == 0 ? 0 : Mathf.Clamp(Mathf.RoundToInt(t * count), 1, count);   // al menos uno: nada es "cero"
        for (int i = 0; i < count; i++)
            if (Segments[i] != null) Segments[i].color = i < filled ? FilledColor : EmptyColor;

        if (Value != null) Value.text = Format(Stat, value);
        ShowNumber(false);
    }

    public void OnPointerEnter(PointerEventData eventData) => ShowNumber(true);
    public void OnPointerExit(PointerEventData eventData)  => ShowNumber(false);

    private void ShowNumber(bool on)
    {
        if (Label != null) Label.enabled = !on;
        if (Value != null) Value.enabled = on;
    }

    // El valor de la clase a nivel 1 (sus stats base). La vida máxima, si el ASDef no la trae aparte, es la vida inicial.
    public static float Read(CharacterClassDefinition def, EClassStat stat, out bool found)
    {
        found = false;
        if (def == null || def.BaseAttributes == null || def.BaseAttributes.InitialAttributes == null) return 0f;

        EAttributeType wanted = stat switch
        {
            EClassStat.MaxHealth   => EAttributeType.MaxHealth,
            EClassStat.MoveSpeed   => EAttributeType.MovSpeed,
            EClassStat.AttackSpeed => EAttributeType.AtkSpeed,
            _                      => EAttributeType.Def,
        };

        float health = 0f; bool hasHealth = false;
        foreach (AttributeSetDefinition.BaseAttribute a in def.BaseAttributes.InitialAttributes)
        {
            if (a.Attribute == wanted) { found = true; return a.BaseValue; }
            if (a.Attribute == EAttributeType.Health) { health = a.BaseValue; hasHealth = true; }
        }
        if (stat == EClassStat.MaxHealth && hasHealth) { found = true; return health; }
        return 0f;
    }

    private static string Format(EClassStat stat, float value) => stat switch
    {
        EClassStat.MaxHealth   => Mathf.RoundToInt(value).ToString(),
        EClassStat.MoveSpeed   => $"{value:0.#} m/s",
        EClassStat.AttackSpeed => $"{value:0.##} s",
        _                      => Mathf.RoundToInt(value).ToString(),
    };
}
