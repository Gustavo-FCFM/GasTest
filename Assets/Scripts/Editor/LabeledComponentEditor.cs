using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

// ============================================================
// LabeledComponentEditor
//
// El Inspector de los COMPONENTES nuestros (pasivas, tótems, proyectiles, el
// PlayerController, la UI, el modo de juego...) con los nombres en español de sus
// [Label] (13 de octubre de 2026, pedido de Gustavo). Es el mismo inspector de siempre
// de Unity: los campos en el mismo orden, con sus [Header], [Range] y tooltips; solo
// cambian los nombres.
//
// Vale para cualquier MonoBehaviour que no tenga un inspector propio: si el script no
// tiene ningún [Label] (los de los packs, los de Unity), se dibuja exactamente el
// inspector por defecto. Un componente con inspector propio (MercEnemySpawnerEditor,
// AbilityPreviewEditor) usa DrawFields para lo mismo.
// ============================================================
[CustomEditor(typeof(MonoBehaviour), true)]
[CanEditMultipleObjects]
public class LabeledComponentEditor : Editor
{
    // El nombre en español de cada campo, por tipo (buscado una sola vez por tipo).
    private static readonly Dictionary<Type, Dictionary<string, string>> _labelsByType =
        new Dictionary<Type, Dictionary<string, string>>();

    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        if (target == null) return root;

        Dictionary<string, string> labels = LabelsOf(target.GetType());
        if (labels.Count == 0)
        {
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            return root;
        }

        SerializedProperty it = serializedObject.GetIterator();
        bool enter = true;
        while (it.NextVisible(enter))
        {
            enter = false;
            var field = labels.TryGetValue(it.name, out string label)
                ? new PropertyField(it.Copy(), label)
                : new PropertyField(it.Copy());
            if (it.propertyPath == "m_Script") field.SetEnabled(false);
            root.Add(field);
        }
        return root;
    }

    // Lo mismo para un inspector propio hecho con IMGUI: en vez de DrawDefaultInspector().
    public static void DrawFields(SerializedObject so)
    {
        so.Update();
        Dictionary<string, string> labels = LabelsOf(so.targetObject.GetType());

        SerializedProperty it = so.GetIterator();
        bool enter = true;
        while (it.NextVisible(enter))
        {
            enter = false;
            using (new EditorGUI.DisabledScope(it.propertyPath == "m_Script"))
            {
                if (labels.TryGetValue(it.name, out string label))
                    EditorGUILayout.PropertyField(it, new GUIContent(label, it.tooltip), true);
                else
                    EditorGUILayout.PropertyField(it, true);
            }
        }
        so.ApplyModifiedProperties();
    }

    private static Dictionary<string, string> LabelsOf(Type type)
    {
        if (_labelsByType.TryGetValue(type, out Dictionary<string, string> labels)) return labels;

        labels = new Dictionary<string, string>();
        for (Type t = type; t != null && t != typeof(MonoBehaviour) && t != typeof(Object); t = t.BaseType)
            foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                                BindingFlags.DeclaredOnly))
            {
                LabelAttribute attr = f.GetCustomAttribute<LabelAttribute>();
                if (attr != null && !labels.ContainsKey(f.Name)) labels[f.Name] = attr.Text;
            }

        _labelsByType[type] = labels;
        return labels;
    }
}
