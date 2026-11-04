using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// ============================================================
// AbilityHelpNumbers
//
// Cuánto daña y cuánto cura una habilidad, para la ventana de ayuda. No hay un campo con ese
// número: el daño vive en sus GE, que cuelgan de la lista Effects, de campos propios de cada
// GA, de los pasos de un combo o del prefab de un proyectil. Así que se recorre lo que la
// habilidad tiene cargado, buscando los GE que tocan la vida, y se calcula con los stats del
// jugador: si alguien cambia un GE, la ayuda ya sale bien.
// ============================================================
public static class AbilityHelpNumbers
{
    private const int MaxDepth = 12;
    private const int MaxShown = 4;

    private static readonly Dictionary<GameplayAbility, List<GameplayEffect>> _cache =
        new Dictionary<GameplayAbility, List<GameplayEffect>>();

    // Sin recarga de dominio al darle Play, el caché sobreviviría de la vez anterior.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _cache.Clear();

    // "Daño: 12 · 20" y "Cura: 30", con los stats de `asc`. Vacío si no daña ni cura. Cada número
    // es un golpe distinto (un combo, las fases de un arco). Lo que pega de a poco (una herida,
    // una quemadura) va aparte y por segundo, para no confundirlo con el golpe.
    public static string Describe(GameplayAbility ability, AbilitySystemComponent asc)
    {
        if (ability == null) return "";

        var damage = new List<int>();
        var heal   = new List<int>();
        var damageOverTime = new List<int>();
        var healOverTime   = new List<int>();
        bool scalesWithTarget = false;

        foreach (GameplayEffect effect in EffectsOf(ability))
        {
            foreach (Modifier m in effect.Modifiers)
            {
                if (m.Attribute != EAttributeType.Health || m.Type != Modifier.EModificationType.Add) continue;

                if (m.UseTargetHealthScaling) scalesWithTarget = true;

                float scaled = m.UseAttributeScaling && asc != null
                    ? m.AttributeCoefficient * asc.GetAttributeValue(m.SourceAttribute) : 0f;
                float total = m.Magnitude + scaled;

                bool overTime = effect.Period > 0f;
                int shown = Mathf.RoundToInt(Mathf.Abs(overTime ? total / effect.Period : total));
                if (shown == 0) continue;

                List<int> list = total < 0f ? (overTime ? damageOverTime : damage) : (overTime ? healOverTime : heal);
                if (!list.Contains(shown)) list.Add(shown);
            }
        }

        string text = "";
        if (damage.Count > 0) text = Loc.T("help.damage", ("value", Join(damage)));
        if (heal.Count > 0)   text = Append(text, Loc.T("help.heal", ("value", Join(heal))));
        if (damageOverTime.Count > 0) text = Append(text, Loc.T("help.damage_over_time", ("value", Join(damageOverTime))));
        if (healOverTime.Count > 0)   text = Append(text, Loc.T("help.heal_over_time", ("value", Join(healOverTime))));
        if (scalesWithTarget) text = Append(text, Loc.T("help.target_health"));
        return text;
    }

    private static string Append(string text, string more) => text.Length > 0 ? text + "   " + more : more;

    private static string Join(List<int> values)
    {
        values.Sort();
        int count = Mathf.Min(values.Count, MaxShown);
        return string.Join(" · ", values.GetRange(0, count));
    }

    private static List<GameplayEffect> EffectsOf(GameplayAbility ability)
    {
        if (_cache.TryGetValue(ability, out List<GameplayEffect> found)) return found;

        found = new List<GameplayEffect>();
        Walk(ability, 0, new HashSet<object>(), found);
        _cache[ability] = found;
        return found;
    }

    private static void Walk(object value, int depth, HashSet<object> seen, List<GameplayEffect> found)
    {
        if (value == null || depth > MaxDepth) return;
        if (value is Object unityObject && unityObject == null) return;

        if (value is GameplayEffect effect)
        {
            if (!found.Contains(effect)) found.Add(effect);
            return;
        }

        if (value is IList list)
        {
            foreach (object item in list) Walk(item, depth + 1, seen, found);
            return;
        }

        if (!seen.Add(value)) return;

        if (value is GameObject go)
        {
            foreach (MonoBehaviour behaviour in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (IsOurs(behaviour != null ? behaviour.GetType() : null)) Walk(behaviour, depth + 1, seen, found);
            return;
        }

        for (System.Type type = value.GetType(); IsOurs(type); type = type.BaseType)
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                                       BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (ShouldFollow(field)) Walk(field.GetValue(value), depth + 1, seen, found);
            }
        }
    }

    // Lo que se ve en el Inspector y puede llevar a un GE. El costo y la espera no son daño.
    private static bool ShouldFollow(FieldInfo field)
    {
        if (field.IsNotSerialized) return false;
        if (!field.IsPublic && !field.IsDefined(typeof(SerializeField), true)) return false;
        if (field.Name == nameof(GameplayAbility.CostEffect) || field.Name == nameof(GameplayAbility.CooldownEffect)) return false;

        System.Type type = field.FieldType;
        if (type.IsGenericType) type = type.GetGenericArguments()[0];
        else if (type.IsArray)  type = type.GetElementType();

        return type == typeof(GameObject) || IsOurs(type);
    }

    private static bool IsOurs(System.Type type)
        => type != null && !type.IsPrimitive && !type.IsEnum && type.Assembly == typeof(GameplayAbility).Assembly;
}
