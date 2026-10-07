using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// ============================================================
// CharacterClassDefinitionEditor
//
// Inspector de las clases (Class_*), sobre la base común por secciones
// (SectionedInspector). Lo propio de las clases:
//
//  · Tarjeta de arriba: ícono, nombre, rol (con su color: Tanque azul, Daño rojo, Soporte
//    verde), los stats de partida y una FILA CON EL KIT: el ícono de la habilidad de cada
//    botón (clic izq., clic der., Q, E, R, Shift). Clic en un ícono = ir a esa
//    habilidad.
//  · Secciones: Identidad · Stats y progresión · Habilidades · Pasivas · Evolución ·
//    Animación · Armas · Crecer (Avatar).
//  · Se esconde lo que no aplica: los ajustes del arma secundaria sin arma secundaria, la
//    posición a la espalda sin tag para guardarla, la escala del Avatar sin tag...
//  · Avisos: dos habilidades en el mismo botón, una entrada vacía, una habilidad que no
//    está en el registro de red, una definitiva sin rol...
// ============================================================
[CustomEditor(typeof(CharacterClassDefinition))]
[CanEditMultipleObjects]
public class CharacterClassDefinitionEditor : SectionedInspector
{
    protected override string[] SectionOrder => ClassSection.Order;

    private CharacterClassDefinition Class => (CharacterClassDefinition)target;

    // Los botones, en el orden en que se muestran en la fila del kit.
    private static readonly (EAbilityInput slot, string label)[] KitSlots =
    {
        (EAbilityInput.PrimaryAttack,   "Clic izq."),
        (EAbilityInput.SecondaryAttack, "Clic der."),
        (EAbilityInput.Action1,         "Q"),
        (EAbilityInput.Action2,         "E"),
        (EAbilityInput.Action3,         "R"),
        (EAbilityInput.Movement,        "Shift"),
    };

    public static Color RoleColor(EClassRole role)
    {
        switch (role)
        {
            case EClassRole.Tank:    return new Color(0.35f, 0.6f, 1f);
            case EClassRole.Damage:  return new Color(1f, 0.4f, 0.4f);
            case EClassRole.Support: return new Color(0.4f, 0.9f, 0.45f);
            default:                 return new Color(0.7f, 0.7f, 0.7f);
        }
    }

    private static string RoleName(EClassRole role)
    {
        switch (role)
        {
            case EClassRole.Tank:    return "Tanque";
            case EClassRole.Damage:  return "Daño";
            case EClassRole.Support: return "Soporte";
            default:                 return "Sin rol (clase base)";
        }
    }

    protected override void DrawHeaderCard()
    {
        CharacterClassDefinition c = Class;

        HeaderCard(c.ClassIcon, string.IsNullOrEmpty(c.ClassName) ? c.name : c.ClassName,
                   RoleName(c.Role), AttributeSetDefinitionEditor.StatsLine(c.BaseAttributes));

        // La rayita del color del rol, debajo de la tarjeta.
        Rect stripe = GUILayoutUtility.GetRect(1f, 3f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(stripe, RoleColor(c.Role));

        DrawKitRow(c);
    }

    // Una fila con el ícono de la habilidad de cada botón.
    private static void DrawKitRow(CharacterClassDefinition c)
    {
        const float size = 36f;
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            foreach (var (slot, label) in KitSlots)
            {
                GameplayAbility ability = null;
                if (c.Abilities != null)
                    foreach (var a in c.Abilities)
                        if (a.InputSlot == slot && a.Ability != null) { ability = a.Ability; break; }

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(size + 8f)))
                {
                    Rect r = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
                    DrawSprite(r, ability != null ? ability.CurrentIcon : null);

                    string tip = ability != null ? $"{label}: {ability.AbilityName} ({ability.name})" : $"{label}: vacío";
                    GUI.Label(r, new GUIContent("", tip));
                    if (ability != null && GUI.Button(r, GUIContent.none, GUIStyle.none))
                        EditorGUIUtility.PingObject(ability);

                    GUILayout.Label(label, KitLabelStyle, GUILayout.Width(size + 8f));
                }
            }
            GUILayout.FlexibleSpace();
        }
    }

    private static GUIStyle _kitLabelStyle;
    private static GUIStyle KitLabelStyle => _kitLabelStyle ??= new GUIStyle(EditorStyles.miniLabel)
    {
        alignment = TextAnchor.UpperCenter, clipping = TextClipping.Clip,
    };

    protected override void CollectWarnings(List<string> into)
    {
        CharacterClassDefinition c = Class;

        if (c.BaseAttributes == null) into.Add("• No tiene stats base (BaseAttributes): arranca sin vida ni ataque.");
        if (c.ClassIcon == null)      into.Add("• No tiene ícono de clase.");

        var registry = Resources.Load<GameplayAbilityRegistry>("GameplayAbilityRegistry");
        var used = new HashSet<EAbilityInput>();
        bool hasUltimate = false;

        if (c.Abilities != null)
            for (int i = 0; i < c.Abilities.Count; i++)
            {
                var a = c.Abilities[i];
                if (a.InputSlot == EAbilityInput.None) into.Add($"• Habilidad {i}: no tiene botón (None).");
                else if (!used.Add(a.InputSlot))     into.Add($"• Habilidad {i}: el botón {a.InputSlot} ya tiene otra habilidad.");

                if (a.Ability == null) { into.Add($"• Habilidad {i} ({a.InputSlot}): está vacía."); continue; }
                if (a.InputSlot == EAbilityInput.Action3) hasUltimate = true;

                if (registry != null && !registry.Abilities.Contains(a.Ability))
                    into.Add($"• {a.Ability.name} no está en el registro de red: su VFX se ve solo en el host " +
                             "(Mercenarios ▸ Actualizar los registros de red).");
            }

        if (hasUltimate && c.Role == EClassRole.None)
            into.Add("• Tiene definitiva (R) pero no tiene rol: la definitiva no va a cargar por rol.");

        if (c.AvailableSubclasses != null && c.AvailableSubclasses.Contains(c))
            into.Add("• Está en su propia lista de subclases.");
    }

    protected override Color SectionColor(string title)
    {
        switch (title)
        {
            case ClassSection.Identity:  return RoleColor(Class.Role);
            case ClassSection.Stats:     return new Color(0.35f, 0.65f, 1f);
            case ClassSection.Abilities: return new Color(1f, 0.55f, 0.15f);
            case ClassSection.Passives:  return new Color(1f, 0.85f, 0.2f);
            case ClassSection.Evolution: return new Color(0.85f, 0.45f, 1f);
            case ClassSection.Animation: return new Color(0.45f, 0.85f, 0.45f);
            case ClassSection.Weapons:   return new Color(0.7f, 0.7f, 0.7f);
            default:                     return base.SectionColor(title);
        }
    }
}

// Una entrada del kit en una línea: [botón] [habilidad] y su ícono.
[CustomPropertyDrawer(typeof(CharacterClassDefinition.AbilityAssignment))]
public class AbilityAssignmentDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => EditorGUIUtility.singleLineHeight + 2f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty slot    = property.FindPropertyRelative("InputSlot");
        SerializedProperty ability = property.FindPropertyRelative("Ability");

        EditorGUI.BeginProperty(position, label, property);
        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        float h = EditorGUIUtility.singleLineHeight;
        Rect r = new Rect(position.x, position.y + 1f, position.width, h);

        Rect iconRect = new Rect(r.x, r.y, h, h);
        var a = ability.objectReferenceValue as GameplayAbility;
        SectionedInspector.DrawSprite(iconRect, a != null ? a.AbilityIcon : null);

        float x = r.x + h + 4f, w = r.width - h - 4f;
        EditorGUI.PropertyField(new Rect(x, r.y, w * 0.32f - 2f, h), slot, GUIContent.none);
        EditorGUI.PropertyField(new Rect(x + w * 0.32f, r.y, w * 0.68f, h), ability, GUIContent.none);

        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }
}

// Lo que sube un stat por nivel, en una línea: [atributo] +[cantidad] por nivel.
[CustomPropertyDrawer(typeof(AttributeGrowth))]
public class AttributeGrowthDrawer : PropertyDrawer
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

        EditorGUI.PropertyField(new Rect(r.x, r.y, r.width * 0.5f - 2f, h), property.FindPropertyRelative("Attribute"), GUIContent.none);
        EditorGUI.LabelField(new Rect(r.x + r.width * 0.5f, r.y, 14f, h), "+");
        EditorGUI.PropertyField(new Rect(r.x + r.width * 0.5f + 14f, r.y, r.width * 0.3f - 14f, h),
                                property.FindPropertyRelative("AmountPerLevel"), GUIContent.none);
        EditorGUI.LabelField(new Rect(r.x + r.width * 0.8f + 4f, r.y, r.width * 0.2f - 4f, h), "por nivel", EditorStyles.miniLabel);

        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }
}
