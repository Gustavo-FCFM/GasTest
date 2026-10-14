using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;

// ============================================================
// LabeledFieldsDrawer
//
// Las entradas de lista que Unity dibuja campo por campo (una opción de tótem, una etapa
// de carga, un paso de combo, una variante por tag, un anillo de aura, una habilidad extra
// de un NPC, una regla de aparición), con los nombres en español de sus
// [Label]. Se ve igual que el dibujo de siempre: el renglón plegable y, abierto, un campo
// por renglón; solo cambian los nombres. Un campo sin [Label] sale con el de siempre.
//
// Las listas de efectos y de VFX, los modificadores y el kit de la clase tienen su propio
// dibujo y no pasan por acá. Un tipo nuevo de entrada con [Label] se agrega con otra
// línea [CustomPropertyDrawer] arriba de la clase.
// ============================================================
[CustomPropertyDrawer(typeof(GA_SpawnTotem.TotemOption))]
[CustomPropertyDrawer(typeof(GA_ChargedAttack.ChargeStage))]
[CustomPropertyDrawer(typeof(GA_ComboSequence.ComboStep))]
[CustomPropertyDrawer(typeof(GA_TagSwitch.TagVariant))]
[CustomPropertyDrawer(typeof(MercEnemyAI.ExtraAbility))]
[CustomPropertyDrawer(typeof(MercEnemySpawner.SpawnRule))]
[CustomPropertyDrawer(typeof(PaladinAuraPassive.AuraRing))]
[CustomPropertyDrawer(typeof(CompanionManager.CompanionOption))]
public class LabeledFieldsDrawer : PropertyDrawer
{
    private const float Spacing = 2f;

    // El nombre en español de cada campo del tipo, buscado una sola vez por tipo.
    private static readonly Dictionary<Type, Dictionary<string, string>> _labelsByType =
        new Dictionary<Type, Dictionary<string, string>>();

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded) return height;

        foreach (SerializedProperty child in Children(property))
            height += EditorGUI.GetPropertyHeight(child, true) + Spacing;
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);

        if (property.isExpanded)
        {
            Dictionary<string, string> labels = LabelsOf(ElementType());
            EditorGUI.indentLevel++;
            float y = row.yMax + Spacing;
            foreach (SerializedProperty child in Children(property))
            {
                float h = EditorGUI.GetPropertyHeight(child, true);
                GUIContent childLabel = labels.TryGetValue(child.name, out string text)
                    ? new GUIContent(text, child.tooltip)
                    : new GUIContent(child.displayName, child.tooltip);
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, h), child, childLabel, true);
                y += h + Spacing;
            }
            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    // Los campos visibles de la entrada, uno por uno (sin entrar en los de adentro).
    private static IEnumerable<SerializedProperty> Children(SerializedProperty property)
    {
        SerializedProperty it  = property.Copy();
        SerializedProperty end = property.GetEndProperty();
        bool enter = true;
        while (it.NextVisible(enter) && !SerializedProperty.EqualContents(it, end))
        {
            enter = false;
            yield return it.Copy();
        }
    }

    // El tipo de la entrada: el del campo, o el de los elementos si el campo es una lista.
    private Type ElementType()
    {
        Type t = fieldInfo.FieldType;
        if (t.IsArray) return t.GetElementType();
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)) return t.GetGenericArguments()[0];
        return t;
    }

    private static Dictionary<string, string> LabelsOf(Type type)
    {
        if (_labelsByType.TryGetValue(type, out Dictionary<string, string> labels)) return labels;

        labels = new Dictionary<string, string>();
        foreach (FieldInfo f in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            LabelAttribute attr = f.GetCustomAttribute<LabelAttribute>();
            if (attr != null) labels[f.Name] = attr.Text;
        }
        _labelsByType[type] = labels;
        return labels;
    }
}
