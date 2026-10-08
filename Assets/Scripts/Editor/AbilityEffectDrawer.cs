using UnityEngine;
using UnityEditor;

// ============================================================
// AbilityEffectDrawer
//
// Cómo se ve una entrada de la lista de efectos de una habilidad (AbilityEffect): todo
// en UNA línea — CUÁNDO · A QUIÉN · el GameplayEffect — y, si se despliega con la
// flechita (o si ya tiene una), la CONDICIÓN de tag en una segunda línea y el ESCALADO
// con las acumulaciones (sección Acumulaciones de la habilidad) en una tercera.
//
// Lo que nunca se va a aplicar se marca en rojo con el motivo en el tooltip: un "al
// golpear" en una habilidad que no golpea a nadie, un "al activarse" para enemigos... El
// mismo motivo sale en los avisos de arriba del inspector (GameplayAbilityEditor).
// ============================================================
[CustomPropertyDrawer(typeof(AbilityEffect))]
public class AbilityEffectDrawer : PropertyDrawer
{
    private static readonly Color BadColor   = new Color(1f, 0.45f, 0.45f);
    private static readonly Color EmptyColor = new Color(1f, 0.85f, 0.4f);

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight;
        return ShowDetails(property) ? line * 3f + 6f : line + 2f;
    }

    // La condición y el escalado se ven desplegando la flechita, o solos si ya tienen algo.
    private static bool ShowDetails(SerializedProperty property)
        => property.isExpanded
        || property.FindPropertyRelative("OnlyIfTargetHas").intValue != (int)EGameplayTag.None
        || property.FindPropertyRelative("StackScaling").intValue != (int)EStackScaling.None;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty when    = property.FindPropertyRelative("When");
        SerializedProperty applyTo = property.FindPropertyRelative("ApplyTo");
        SerializedProperty effect  = property.FindPropertyRelative("Effect");
        SerializedProperty onlyIf  = property.FindPropertyRelative("OnlyIfTargetHas");
        SerializedProperty scaling = property.FindPropertyRelative("StackScaling");
        SerializedProperty perStack = property.FindPropertyRelative("PerStack");

        EditorGUI.BeginProperty(position, label, property);
        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        float line = EditorGUIUtility.singleLineHeight;
        Rect row = new Rect(position.x, position.y + 1f, position.width, line);

        // Flechita para la condición.
        Rect fold = new Rect(row.x, row.y, 14f, line);
        property.isExpanded = EditorGUI.Foldout(fold, property.isExpanded, GUIContent.none, true);

        float x = row.x + 14f;
        float w = row.width - 14f;
        Rect whenRect   = new Rect(x, row.y, w * 0.30f - 2f, line);
        Rect toRect     = new Rect(whenRect.xMax + 2f, row.y, w * 0.30f - 2f, line);
        Rect effectRect = new Rect(toRect.xMax + 2f, row.y, row.xMax - toRect.xMax - 2f, line);

        // ¿Se va a aplicar alguna vez? (solo se sabe con un único asset elegido)
        GameplayAbility ability = property.serializedObject.targetObject as GameplayAbility;
        string problem = ability != null && !property.serializedObject.isEditingMultipleObjects
            ? Problem(ability, ReadEntry(when, applyTo, effect))
            : null;

        Color prev = GUI.backgroundColor;
        if (problem != null && effect.objectReferenceValue != null) GUI.backgroundColor = BadColor;
        EditorGUI.PropertyField(whenRect, when, new GUIContent("", problem ?? when.tooltip));
        EditorGUI.PropertyField(toRect, applyTo, new GUIContent("", problem ?? applyTo.tooltip));
        GUI.backgroundColor = effect.objectReferenceValue == null ? EmptyColor : prev;
        EditorGUI.PropertyField(effectRect, effect, GUIContent.none);
        GUI.backgroundColor = prev;

        if (ShowDetails(property))
        {
            Rect cond = new Rect(x, row.y + line + 2f, w, line);
            float labelW = Mathf.Min(170f, cond.width * 0.45f);
            EditorGUI.LabelField(new Rect(cond.x, cond.y, labelW, line),
                                 new GUIContent("Solo si el objetivo tiene", onlyIf.tooltip), EditorStyles.miniLabel);
            EditorGUI.PropertyField(new Rect(cond.x + labelW, cond.y, cond.width - labelW, line), onlyIf, GUIContent.none);

            // Escalado con las acumulaciones: el modo y, si es por duración, los segundos.
            Rect st = new Rect(x, cond.y + line + 2f, w, line);
            EditorGUI.LabelField(new Rect(st.x, st.y, labelW, line),
                                 new GUIContent("Con las acumulaciones", scaling.tooltip), EditorStyles.miniLabel);

            bool perDuration = scaling.intValue == (int)EStackScaling.DurationPerStack;
            float modeW = perDuration ? (st.width - labelW) * 0.6f : st.width - labelW;
            EditorGUI.PropertyField(new Rect(st.x + labelW, st.y, modeW - 2f, line), scaling, GUIContent.none);
            if (perDuration)
            {
                float px = st.x + labelW + modeW;
                EditorGUI.PropertyField(new Rect(px, st.y, st.xMax - px - 46f, line), perStack,
                                        new GUIContent("", perStack.tooltip));
                EditorGUI.LabelField(new Rect(st.xMax - 44f, st.y, 44f, line), "s c/u", EditorStyles.miniLabel);
            }
        }

        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }

    private static AbilityEffect ReadEntry(SerializedProperty when, SerializedProperty applyTo, SerializedProperty effect)
        => new AbilityEffect
        {
            When    = (EEffectWhen)when.intValue,
            ApplyTo = (EEffectTarget)applyTo.intValue,
            Effect  = effect.objectReferenceValue as GameplayEffect,
        };

    // Por qué esta entrada nunca se va a aplicar en esta habilidad, o null si está bien.
    public static string Problem(GameplayAbility ability, AbilityEffect e)
    {
        if (e.Effect == null) return "está vacía (le falta el GameplayEffect).";

        if (!ability.SupportsEffectTiming(e.When))
            return $"'{Pretty(e.When)}' no aplica: {ability.GetType().Name} no golpea a nadie. Usá 'Al activarse'.";

        if (e.When == EEffectWhen.OnActivate && e.ApplyTo != EEffectTarget.Self)
            return "'Al activarse' solo se le aplica al lanzador (todavía no alcanzó a nadie). Poné 'El lanzador'.";

        if (e.When == EEffectWhen.OnKill && e.ApplyTo != EEffectTarget.Self)
            return "'Al matar' solo se le aplica al lanzador (el objetivo ya está muerto). Poné 'El lanzador'.";

        return null;
    }

    private static string Pretty(EEffectWhen when)
    {
        switch (when)
        {
            case EEffectWhen.OnHit:      return "Al golpear";
            case EEffectWhen.OnFirstHit: return "Al primer golpe";
            case EEffectWhen.OnActivate: return "Al activarse";
            default:                     return "Al matar";
        }
    }
}
