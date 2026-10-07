using UnityEngine;
using UnityEditor;

// ============================================================
// ModifierDrawer
//
// Cómo se ve un Modifier (una línea de "qué atributo cambia y cuánto" de un GE):
//
//   [Atributo] [Sumar/Multiplicar/Reemplazar] [Cantidad]
//   = −10 + 1.2 × Attack (del que lo aplica)            ← la fórmula, siempre a la vista
//
// Con la flechita se despliegan los escalados: con un stat del que lo aplica, con la vida
// del objetivo (vida faltante, actual o máxima) y el piso. Cada escalado se prende con
// su casilla y solo entonces muestra sus campos; apagado CONSERVA lo que tenía.
// ============================================================
[CustomPropertyDrawer(typeof(Modifier))]
public class ModifierDrawer : PropertyDrawer
{
    private static float Line => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float h = Line * 2f + 2f;                    // fila principal + fórmula
        if (!property.isExpanded) return h;

        h += Line;                                   // casilla: stat del que lo aplica
        if (property.FindPropertyRelative("UseAttributeScaling").boolValue) h += Line;
        h += Line;                                   // casilla: vida del objetivo
        if (property.FindPropertyRelative("UseTargetHealthScaling").boolValue) h += Line;
        h += Line;                                   // piso
        return h + 4f;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty attribute = property.FindPropertyRelative("Attribute");
        SerializedProperty type      = property.FindPropertyRelative("Type");
        SerializedProperty magnitude = property.FindPropertyRelative("Magnitude");
        SerializedProperty useAttr   = property.FindPropertyRelative("UseAttributeScaling");
        SerializedProperty srcAttr   = property.FindPropertyRelative("SourceAttribute");
        SerializedProperty attrCoef  = property.FindPropertyRelative("AttributeCoefficient");
        SerializedProperty useHealth = property.FindPropertyRelative("UseTargetHealthScaling");
        SerializedProperty healthMode = property.FindPropertyRelative("TargetHealthMode");
        SerializedProperty healthCoef = property.FindPropertyRelative("TargetHealthCoefficient");
        SerializedProperty min       = property.FindPropertyRelative("MinMagnitude");

        EditorGUI.BeginProperty(position, label, property);
        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        float h = EditorGUIUtility.singleLineHeight;
        Rect row = new Rect(position.x, position.y + 1f, position.width, h);

        property.isExpanded = EditorGUI.Foldout(new Rect(row.x, row.y, 14f, h), property.isExpanded, GUIContent.none, true);
        float x = row.x + 14f, w = row.width - 14f;

        EditorGUI.PropertyField(new Rect(x, row.y, w * 0.38f - 2f, h), attribute, GUIContent.none);
        EditorGUI.PropertyField(new Rect(x + w * 0.38f, row.y, w * 0.30f - 2f, h), type, GUIContent.none);
        EditorGUI.PropertyField(new Rect(x + w * 0.68f, row.y, w * 0.32f, h), magnitude, GUIContent.none);

        // La fórmula, para leer el efecto sin abrir nada.
        row.y += Line;
        EditorGUI.LabelField(new Rect(x, row.y, w, h), Formula(property), EditorStyles.miniLabel);

        if (property.isExpanded)
        {
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Min(190f, w * 0.5f);

            row.y += Line;
            EditorGUI.PropertyField(new Rect(x, row.y, w, h), useAttr,
                new GUIContent("Escala con un stat del que lo aplica", useAttr.tooltip));
            if (useAttr.boolValue)
            {
                row.y += Line;
                Pair(new Rect(x + 12f, row.y, w - 12f, h), srcAttr, attrCoef, "×");
            }

            row.y += Line;
            EditorGUI.PropertyField(new Rect(x, row.y, w, h), useHealth,
                new GUIContent("Escala con la vida del objetivo", useHealth.tooltip));
            if (useHealth.boolValue)
            {
                row.y += Line;
                Pair(new Rect(x + 12f, row.y, w - 12f, h), healthMode, healthCoef, "×");
            }

            row.y += Line;
            EditorGUI.PropertyField(new Rect(x, row.y, w, h), min, new GUIContent("Piso (mínimo)", min.tooltip));

            EditorGUIUtility.labelWidth = labelWidth;
        }

        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }

    // [campo] × [coeficiente], en una sola fila.
    private static void Pair(Rect r, SerializedProperty a, SerializedProperty coef, string sep)
    {
        EditorGUI.PropertyField(new Rect(r.x, r.y, r.width * 0.55f - 2f, r.height), a, GUIContent.none);
        EditorGUI.LabelField(new Rect(r.x + r.width * 0.55f, r.y, 16f, r.height), sep);
        EditorGUI.PropertyField(new Rect(r.x + r.width * 0.55f + 16f, r.y, r.width * 0.45f - 16f, r.height), coef, GUIContent.none);
    }

    // "= −10 + 1.2 × Attack (del que lo aplica) + −0.3 × vida faltante del objetivo"
    public static string Formula(SerializedProperty p)
    {
        var type = (Modifier.EModificationType)p.FindPropertyRelative("Type").intValue;
        float mag = p.FindPropertyRelative("Magnitude").floatValue;

        string text;
        if (type == Modifier.EModificationType.Multiply)      text = $"× {mag:0.###}";
        else if (type == Modifier.EModificationType.Override) text = $"= {mag:0.###}";
        else                                                  text = mag < 0f ? $"− {-mag:0.###}" : $"+ {mag:0.###}";

        if (p.FindPropertyRelative("UseAttributeScaling").boolValue)
            text += $" + {p.FindPropertyRelative("AttributeCoefficient").floatValue:0.###} × " +
                    $"{(EAttributeType)p.FindPropertyRelative("SourceAttribute").intValue} (del que lo aplica)";

        if (p.FindPropertyRelative("UseTargetHealthScaling").boolValue)
        {
            var mode = (Modifier.ETargetHealthMode)p.FindPropertyRelative("TargetHealthMode").intValue;
            string what = mode == Modifier.ETargetHealthMode.MissingHealth ? "vida faltante"
                        : mode == Modifier.ETargetHealthMode.CurrentHealth ? "vida actual" : "vida máxima";
            text += $" + {p.FindPropertyRelative("TargetHealthCoefficient").floatValue:0.###} × {what} del objetivo";
        }

        float min = p.FindPropertyRelative("MinMagnitude").floatValue;
        if (min > 0f) text += $"  (mínimo {min:0.###})";

        return text;
    }
}
