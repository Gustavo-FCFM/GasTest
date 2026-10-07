using UnityEngine;
using UnityEditor;

// ============================================================
// AbilityVisualDrawer
//
// Cómo se ve una entrada de la lista de VFX de una habilidad (AbilityVisual). Plegada:
// una línea — CUÁNDO · el prefab. Desplegada: solo los campos que tienen sentido para
// ESE momento (el fin por tag es del "al lanzar"; calzar con el área, del "en el
// impacto"...). Lo que se esconde al cambiar el momento CONSERVA su valor.
//
// Un momento que la habilidad no usa nunca (un "al golpear" en un buff propio) se marca
// en rojo, con el motivo en el tooltip y en los avisos del inspector.
// ============================================================
[CustomPropertyDrawer(typeof(AbilityVisual))]
public class AbilityVisualDrawer : PropertyDrawer
{
    private static readonly Color BadColor   = new Color(1f, 0.45f, 0.45f);
    private static readonly Color EmptyColor = new Color(1f, 0.85f, 0.4f);

    // Los campos que se muestran desplegada, según el momento.
    private static string[] FieldsFor(EVisualWhen when, SerializedProperty property)
    {
        switch (when)
        {
            case EVisualWhen.OnCast:
                return property.FindPropertyRelative("EndWhenAttributeDepleted").boolValue
                    ? new[] { "Delay", "Offset", "RotationOffset", "Scale", "Attach", "DestroyTime", "EndWithTag",
                              "EndWhenAttributeDepleted", "DepletedAttribute" }
                    : new[] { "Delay", "Offset", "RotationOffset", "Scale", "Attach", "DestroyTime", "EndWithTag",
                              "EndWhenAttributeDepleted" };

            case EVisualWhen.OnImpact:
                return property.FindPropertyRelative("MatchAreaSize").boolValue
                    ? new[] { "Delay", "Offset", "RotationOffset", "Scale", "Attach", "DestroyTime", "MatchAreaSize",
                              "AreaSizeMultiplier" }
                    : new[] { "Delay", "Offset", "RotationOffset", "Scale", "Attach", "DestroyTime", "MatchAreaSize" };

            default:
                return new[] { "Delay", "Offset", "RotationOffset", "Scale", "Attach", "DestroyTime" };
        }
    }

    // Nombre del campo "Attach" según el momento: qué es lo que sigue.
    private static string AttachLabel(EVisualWhen when)
    {
        switch (when)
        {
            case EVisualWhen.OnCast: return "Pegado al lanzador";
            case EVisualWhen.OnHit:  return "Pegado al objetivo";
            default:                 return "Sigue al lanzador (si el área lo sigue)";
        }
    }

    private static string FieldLabel(string field, EVisualWhen when)
    {
        switch (field)
        {
            case "Attach":                   return AttachLabel(when);
            case "DestroyTime":              return "Dura (s)";
            case "MatchAreaSize":            return "Calzar con el área";
            case "AreaSizeMultiplier":       return "Multiplicador del área";
            case "EndWithTag":               return "Termina sin el tag";
            case "EndWhenAttributeDepleted": return "Termina sin atributo";
            case "DepletedAttribute":        return "Atributo";
            case "RotationOffset":           return "Rotación";
            default:                         return ObjectNames.NicifyVariableName(field);
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        if (!property.isExpanded) return line + 2f;

        EVisualWhen when = (EVisualWhen)property.FindPropertyRelative("When").intValue;
        float height = line + 2f;
        foreach (string field in FieldsFor(when, property))
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field), true) +
                      EditorGUIUtility.standardVerticalSpacing;
        return height + 4f;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty whenProp = property.FindPropertyRelative("When");
        SerializedProperty prefab   = property.FindPropertyRelative("VFXPrefab");
        EVisualWhen when = (EVisualWhen)whenProp.intValue;

        EditorGUI.BeginProperty(position, label, property);
        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        float line = EditorGUIUtility.singleLineHeight;
        Rect row = new Rect(position.x, position.y + 1f, position.width, line);

        property.isExpanded = EditorGUI.Foldout(new Rect(row.x, row.y, 14f, line), property.isExpanded,
                                                GUIContent.none, true);

        float x = row.x + 14f;
        float w = row.width - 14f;
        Rect whenRect   = new Rect(x, row.y, w * 0.42f - 2f, line);
        Rect prefabRect = new Rect(whenRect.xMax + 2f, row.y, row.xMax - whenRect.xMax - 2f, line);

        GameplayAbility ability = property.serializedObject.targetObject as GameplayAbility;
        string problem = ability != null && !property.serializedObject.isEditingMultipleObjects
            ? Problem(ability, ReadEntry(property))
            : null;

        Color prev = GUI.backgroundColor;
        if (problem != null && prefab.objectReferenceValue != null) GUI.backgroundColor = BadColor;
        EditorGUI.PropertyField(whenRect, whenProp, new GUIContent("", problem ?? whenProp.tooltip));
        GUI.backgroundColor = prefab.objectReferenceValue == null ? EmptyColor : prev;
        EditorGUI.PropertyField(prefabRect, prefab, GUIContent.none);
        GUI.backgroundColor = prev;

        if (property.isExpanded)
        {
            float y = row.yMax + EditorGUIUtility.standardVerticalSpacing + 2f;
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Min(190f, w * 0.45f);

            foreach (string field in FieldsFor(when, property))
            {
                SerializedProperty p = property.FindPropertyRelative(field);
                float h = EditorGUI.GetPropertyHeight(p, true);
                EditorGUI.PropertyField(new Rect(x, y, w, h), p, new GUIContent(FieldLabel(field, when), p.tooltip), true);
                y += h + EditorGUIUtility.standardVerticalSpacing;
            }

            EditorGUIUtility.labelWidth = labelWidth;
        }

        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }

    private static AbilityVisual ReadEntry(SerializedProperty property)
        => new AbilityVisual
        {
            When          = (EVisualWhen)property.FindPropertyRelative("When").intValue,
            VFXPrefab     = property.FindPropertyRelative("VFXPrefab").objectReferenceValue as GameObject,
            MatchAreaSize = property.FindPropertyRelative("MatchAreaSize").boolValue,
        };

    // Por qué esta entrada nunca se va a ver en esta habilidad, o null si está bien.
    public static string Problem(GameplayAbility ability, AbilityVisual v)
    {
        if (v.VFXPrefab == null) return "está vacía (le falta el prefab).";

        if (!ability.SupportsVisualTiming(v.When))
            return v.When == EVisualWhen.OnHit
                ? $"'Al golpear' no aplica: {ability.GetType().Name} no golpea a nadie."
                : $"'En el impacto' no aplica: {ability.GetType().Name} no tiene un punto donde cae.";

        if (v.When == EVisualWhen.OnImpact && v.MatchAreaSize && ability.VisualAreaRadius <= 0f)
            return "'Calzar con el área' no aplica: esta habilidad no tiene radio de área.";

        return null;
    }
}
