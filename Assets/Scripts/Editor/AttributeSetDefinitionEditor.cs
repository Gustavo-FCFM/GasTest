using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// ============================================================
// AttributeSetDefinitionEditor
//
// Inspector de los sets de atributos (ASDef_*), sobre la base común por secciones
// (SectionedInspector). Arriba, los stats de partida en una línea y qué clases usan este
// set; cada atributo en una línea ([atributo] [valor]); avisos de atributos repetidos o
// de vida sin vida máxima.
// ============================================================
[CustomEditor(typeof(AttributeSetDefinition))]
[CanEditMultipleObjects]
public class AttributeSetDefinitionEditor : SectionedInspector
{
    protected override string[] SectionOrder => new[] { "Atributos base", "*" };

    private AttributeSetDefinition Set => (AttributeSetDefinition)target;

    // Qué clases usan este set. Se busca una vez al abrir el inspector, no en cada repintado.
    private readonly List<string> _users = new List<string>();

    protected override void OnEnable()
    {
        base.OnEnable();
        _users.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:CharacterClassDefinition"))
        {
            var c = AssetDatabase.LoadAssetAtPath<CharacterClassDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (c != null && c.BaseAttributes == target) _users.Add(c.name);
        }
    }

    protected override void DrawHeaderCard()
    {
        var users = _users;
        HeaderCard(null, Set.name, "Stats de partida (nivel 1)", StatsLine(Set),
                   users.Count > 0 ? "Lo usan: " + string.Join(", ", users) : "No lo usa ninguna clase.");
    }

    // "Vida 80 · Ataque 4 · Defensa 4 · Energía 50 · 1 s entre ataques · Vel. 5" — los que
    // más se miran, en el orden de siempre. La usa también la tarjeta de las clases.
    public static string StatsLine(AttributeSetDefinition set)
    {
        if (set == null || set.InitialAttributes == null) return "Sin stats base.";

        var values = new Dictionary<EAttributeType, float>();
        foreach (var a in set.InitialAttributes) if (a != null) values[a.Attribute] = a.BaseValue;

        var parts = new List<string>();
        void Add(EAttributeType t, string name, string unit = "")
        {
            if (values.TryGetValue(t, out float v)) parts.Add($"{name} {v:0.##}{unit}");
        }

        Add(EAttributeType.MaxHealth, "Vida");
        Add(EAttributeType.Attack, "Ataque");
        Add(EAttributeType.MagicDamage, "Daño mágico");
        Add(EAttributeType.Def, "Defensa");
        Add(EAttributeType.MaxEnergy, "Energía");
        Add(EAttributeType.MaxMana, "Maná");
        Add(EAttributeType.AtkSpeed, "Entre ataques", " s");
        Add(EAttributeType.MovSpeed, "Velocidad");

        return parts.Count > 0 ? string.Join("  ·  ", parts) : "Sin stats base.";
    }

    protected override void CollectWarnings(List<string> into)
    {
        var seen = new HashSet<EAttributeType>();
        var values = new Dictionary<EAttributeType, float>();

        if (Set.InitialAttributes != null)
            foreach (var a in Set.InitialAttributes)
            {
                if (a == null) continue;
                if (a.Attribute == EAttributeType.None) into.Add("• Hay una entrada sin atributo (None).");
                else if (!seen.Add(a.Attribute))       into.Add($"• {a.Attribute} está repetido: vale el último.");
                values[a.Attribute] = a.BaseValue;
            }

        if (values.ContainsKey(EAttributeType.Health) && !values.ContainsKey(EAttributeType.MaxHealth))
            into.Add("• Tiene Health pero no MaxHealth.");
        if (values.TryGetValue(EAttributeType.Health, out float hp) && values.TryGetValue(EAttributeType.MaxHealth, out float max) && hp > max)
            into.Add("• Health es mayor que MaxHealth.");
    }

    protected override Color SectionColor(string title) => new Color(0.35f, 0.65f, 1f);
}

// Un atributo base en una línea: [atributo] [valor].
[CustomPropertyDrawer(typeof(AttributeSetDefinition.BaseAttribute))]
public class BaseAttributeDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => EditorGUIUtility.singleLineHeight + 2f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        float h = EditorGUIUtility.singleLineHeight;
        Rect r = new Rect(position.x, position.y + 1f, position.width, h);
        EditorGUI.PropertyField(new Rect(r.x, r.y, r.width * 0.6f - 2f, h), property.FindPropertyRelative("Attribute"), GUIContent.none);
        EditorGUI.PropertyField(new Rect(r.x + r.width * 0.6f, r.y, r.width * 0.4f, h), property.FindPropertyRelative("BaseValue"), GUIContent.none);

        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }
}
