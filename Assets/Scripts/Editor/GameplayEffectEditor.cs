using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// ============================================================
// GameplayEffectEditor
//
// Inspector de los GameplayEffect (GE_*), sobre la base común por secciones
// (SectionedInspector). Lo propio de los efectos:
//
//  · Tarjeta de arriba: ícono, tipo (Buff / Debuff / oculto), cuánto dura, cada cuánto
//    tickea, acumulaciones, qué tags da y la fórmula de cada modificador.
//  · Secciones: General · Duración y acumulación · Modificadores · Tags · Control y
//    desplazamiento · Exclusión (grupo) · VFX en el objetivo · Sonido.
//  · Se esconde lo que no aplica: el período en un instantáneo, el tope de acumulaciones
//    si no acumula, lo del desplazamiento si no desplaza, lo del VFX si no tiene VFX...
//  · Avisos: un instantáneo con tags o VFX (duran un frame), un buff sin ícono, etc.
// ============================================================
[CustomEditor(typeof(GameplayEffect))]
[CanEditMultipleObjects]
public class GameplayEffectEditor : SectionedInspector
{
    protected override string[] SectionOrder => EffectSection.Order;

    private GameplayEffect Effect => (GameplayEffect)target;

    protected override void DrawHeaderCard()
    {
        GameplayEffect e = Effect;

        string kind = e.EffectType == GameplayEffect.EEffectType.Buff ? "Buff"
                    : e.EffectType == GameplayEffect.EEffectType.Debuff ? "Debuff" : "Oculto (no sale en la barra)";

        var facts = new List<string>();
        facts.Add(e.Duration > 0f ? $"Dura {e.Duration:0.##} s" : "Instantáneo");
        if (e.Duration > 0f && e.Period > 0f) facts.Add($"cada {e.Period:0.##} s");
        if (e.StackingPolicy == GameplayEffect.EStackingType.Stack && e.MaxStacks > 0) facts.Add($"hasta {e.MaxStacks} acumulaciones");
        else if (e.Duration > 0f) facts.Add(e.StackingPolicy == GameplayEffect.EStackingType.Stack ? "se acumula" :
                                            e.StackingPolicy == GameplayEffect.EStackingType.Refresh ? "se refresca" : "se reemplaza");
        if (e.CountsAsCrowdControl) facts.Add("es control");
        if (e.KnockbackDistance > 0f) facts.Add($"desplaza {e.KnockbackDistance:0.##} m");

        string tags = e.GrantedTags != null && e.GrantedTags.Count > 0 ? "Da: " + string.Join(", ", e.GrantedTags) : null;

        // La fórmula de cada modificador, en una línea cada uno.
        var modLines = new List<string>();
        SerializedProperty mods = serializedObject.FindProperty("Modifiers");
        for (int i = 0; i < mods.arraySize && i < 4; i++)
        {
            SerializedProperty m = mods.GetArrayElementAtIndex(i);
            modLines.Add($"{(EAttributeType)m.FindPropertyRelative("Attribute").intValue} {ModifierDrawer.Formula(m)}");
        }
        if (mods.arraySize > 4) modLines.Add($"(y {mods.arraySize - 4} más)");

        var lines = new List<string> { string.Join("  ·  ", facts) };
        if (tags != null) lines.Add(tags);
        lines.AddRange(modLines);

        HeaderCard(e.Icon, e.name, kind, lines.ToArray());
    }

    protected override void CollectWarnings(List<string> into)
    {
        GameplayEffect e = Effect;
        bool instant = e.Duration <= 0f;

        // Un GE de cooldown no lleva duración propia (se la da la habilidad): no avisar ahí.
        bool looksLikeCooldown = e.GrantedTags != null && e.GrantedTags.Exists(t => t.ToString().Contains("Cooldown"));

        if (instant && e.TargetVFX != null)
            into.Add("• Tiene VFX en el objetivo pero es instantáneo: el VFX se iría en el mismo frame (salvo que la habilidad le pase una duración).");

        if (instant && e.Period > 0f)
            into.Add("• Tiene período pero es instantáneo: nunca va a tickear.");

        if (instant && !looksLikeCooldown && e.GrantedTags != null && e.GrantedTags.Count > 0)
            into.Add("• Da tags pero es instantáneo: el tag dura un frame (salvo que la habilidad le pase una duración, como el escudo del Golpe final).");

        if (!instant && e.EffectType != GameplayEffect.EEffectType.Hidden && e.Icon == null)
            into.Add("• Es un Buff/Debuff con duración pero sin ícono: en la barra de efectos sale un cuadro vacío.");

        if (e.OnMaxStacksEffect != null && (e.StackingPolicy != GameplayEffect.EStackingType.Stack || e.MaxStacks <= 0))
            into.Add("• Tiene OnMaxStacksEffect pero no acumula con tope (Stack + MaxStacks > 0): nunca se dispara.");

        if (e.Modifiers != null)
            for (int i = 0; i < e.Modifiers.Count; i++)
                if (e.Modifiers[i] != null && e.Modifiers[i].Attribute == EAttributeType.None)
                    into.Add($"• Modificador {i}: no tiene atributo (None): no cambia nada.");
    }

    protected override bool? IsInNetworkRegistry()
    {
        var registry = Resources.Load<GameplayEffectRegistry>("GameplayEffectRegistry");
        return registry != null ? registry.Effects.Contains(Effect) : (bool?)null;
    }

    protected override Color SectionColor(string title)
    {
        switch (title)
        {
            case EffectSection.General:   return new Color(0.75f, 0.75f, 0.75f);
            case EffectSection.Duration:  return new Color(0.7f, 0.7f, 1f);
            case EffectSection.Modifiers: return new Color(1f, 0.35f, 0.35f);
            case EffectSection.Tags:      return new Color(1f, 0.85f, 0.2f);
            case EffectSection.Control:   return new Color(1f, 0.55f, 0.15f);
            case EffectSection.Group:     return new Color(0.6f, 0.6f, 0.6f);
            case EffectSection.Visuals:   return new Color(0.85f, 0.45f, 1f);
            case EffectSection.Sound:     return new Color(0.4f, 0.8f, 0.7f);
            default:                      return base.SectionColor(title);
        }
    }
}
