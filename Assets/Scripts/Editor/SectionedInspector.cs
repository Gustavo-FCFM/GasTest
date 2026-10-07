using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

// ============================================================
// SectionedInspector
//
// La base de los inspectores "por secciones" de los datos del juego: habilidades (GA),
// efectos (GE), clases y sets de atributos (ASDef). Todos se ven y se manejan igual:
//
//  · SECCIONES PLEGABLES. Los campos se agrupan por su [Section] (ver AbilityAuthoring.cs);
//    un campo sin [Section] va en la última sección abierta por su misma clase (como un
//    [Header]). Las secciones salen en el orden que da cada inspector (SectionOrder), y
//    qué está abierto se recuerda por sección.
//  · BUSCADOR y "Abrir todo / Cerrar todo" arriba.
//  · CAMPOS QUE SE ESCONDEN cuando no aplican ([ShowIf], se pueden poner varios: tienen
//    que cumplirse todos). Lo escondido CONSERVA su valor.
//  · AVISOS de lo que está configurado pero no va a funcionar (CollectWarnings).
//  · "¿QUIÉN LO USA?": busca qué habilidades, efectos, clases y prefabs lo referencian, y
//    si está en el registro de red.
//
// Cada inspector concreto pone su tarjeta de arriba (DrawHeaderCard), sus avisos y, si
// hace falta, esconde campos (IsPropertyVisible) o dibuja algo debajo de uno (AfterProperty).
// Es solo presentación: no cambia los datos ni cómo corre el juego.
// ============================================================
public abstract class SectionedInspector : Editor
{
    private class SectionGroup
    {
        public string Title;
        public bool StartCollapsed;
        public float Order;
        public readonly List<string> Paths = new List<string>();
    }

    private List<SectionGroup> _sections;
    private readonly Dictionary<string, ShowIfAttribute[]> _showIf = new Dictionary<string, ShowIfAttribute[]>();

    // Lo escrito en el buscador. Compartido entre todos los inspectores de este tipo.
    private static readonly Dictionary<Type, string> _searchByEditor = new Dictionary<Type, string>();
    private string Search
    {
        get => _searchByEditor.TryGetValue(GetType(), out string s) ? s : "";
        set => _searchByEditor[GetType()] = value;
    }

    // El resultado de "¿Quién lo usa?" (se calcula al apretar el botón).
    private List<Object> _usages;
    private bool? _inRegistry;

    // ---------------------------------------------------------
    // Lo que define cada inspector
    // ---------------------------------------------------------

    // Orden de las secciones. "*" = el lugar de las que no están en la lista.
    protected abstract string[] SectionOrder { get; }

    // Prefijo para recordar qué secciones están abiertas (uno por tipo de asset).
    protected virtual string PrefsPrefix => "Merc.Inspector." + GetType().Name + ".";

    protected virtual void DrawHeaderCard() { }
    protected virtual void CollectWarnings(List<string> into) { }
    protected virtual void DrawAfterSections() { }
    protected virtual bool IsPropertyVisible(SerializedProperty prop) => true;
    protected virtual void AfterProperty(SerializedProperty prop) { }

    // ¿En qué registro de red tiene que estar este asset? null = en ninguno.
    protected virtual bool? IsInNetworkRegistry() => null;

    // Color de la rayita de cada sección.
    protected virtual Color SectionColor(string title) => new Color(0.95f, 0.95f, 0.5f);

    // ---------------------------------------------------------
    // Ciclo de vida
    // ---------------------------------------------------------

    protected virtual void OnEnable() => BuildSections();

    private void BuildSections()
    {
        _sections = new List<SectionGroup>();
        _showIf.Clear();
        if (serializedObject == null || target == null) return;

        var byTitle    = new Dictionary<string, SectionGroup>();
        var lastByType = new Dictionary<Type, string>();
        Type type      = target.GetType();
        int custom     = 0;

        SerializedProperty it = serializedObject.GetIterator();
        bool enter = true;
        while (it.NextVisible(enter))
        {
            enter = false;
            if (it.propertyPath == "m_Script") continue;

            FieldInfo field = FindField(type, it.name);
            SectionAttribute attr = field != null ? field.GetCustomAttribute<SectionAttribute>() : null;

            if (field != null)
            {
                var showIf = (ShowIfAttribute[])field.GetCustomAttributes(typeof(ShowIfAttribute), true);
                if (showIf.Length > 0) _showIf[it.propertyPath] = showIf;
            }

            string title;
            bool collapsed = false;
            if (attr != null)
            {
                title = attr.Title;
                collapsed = attr.StartCollapsed;
                lastByType[field.DeclaringType] = title;
            }
            else if (field != null && lastByType.TryGetValue(field.DeclaringType, out string last))
                title = last;
            else
                title = "Otros";

            if (!byTitle.TryGetValue(title, out SectionGroup group))
            {
                group = new SectionGroup { Title = title, Order = OrderOf(title, custom++) };
                byTitle[title] = group;
                _sections.Add(group);
            }
            group.StartCollapsed |= collapsed;
            group.Paths.Add(it.propertyPath);
        }

        _sections.Sort((a, b) => a.Order.CompareTo(b.Order));
    }

    private float OrderOf(string title, int appearance)
    {
        string[] order = SectionOrder;
        int index = Array.IndexOf(order, title);
        if (index >= 0) return index;
        if (title == "Otros") return order.Length;
        int star = Array.IndexOf(order, "*");
        return (star >= 0 ? star : order.Length) + 0.001f * (appearance + 1);
    }

    protected static FieldInfo FindField(Type type, string name)
    {
        for (Type t = type; t != null && t != typeof(ScriptableObject) && t != typeof(Object); t = t.BaseType)
        {
            FieldInfo f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                           BindingFlags.DeclaredOnly);
            if (f != null) return f;
        }
        return null;
    }

    // ---------------------------------------------------------
    // Dibujo
    // ---------------------------------------------------------

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

        if (!serializedObject.isEditingMultipleObjects) DrawHeaderCard();
        DrawToolbar();

        if (!serializedObject.isEditingMultipleObjects)
        {
            var warnings = new List<string>();
            CollectWarnings(warnings);
            if (warnings.Count > 0) EditorGUILayout.HelpBox(string.Join("\n", warnings), MessageType.Warning);
        }

        EditorGUI.BeginChangeCheck();
        bool searching = !string.IsNullOrEmpty(Search);
        foreach (SectionGroup section in _sections)
            DrawSection(section, searching);
        bool changed = EditorGUI.EndChangeCheck();

        serializedObject.ApplyModifiedProperties();
        if (changed) OnValuesChanged();

        DrawAfterSections();
        if (!serializedObject.isEditingMultipleObjects) DrawUsages();
    }

    protected virtual void OnValuesChanged() { }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            Search = GUILayout.TextField(Search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(80));
            if (!string.IsNullOrEmpty(Search) && GUILayout.Button("×", EditorStyles.toolbarButton, GUILayout.Width(20)))
            {
                Search = "";
                GUI.FocusControl(null);
            }
            if (GUILayout.Button("Abrir todo", EditorStyles.toolbarButton, GUILayout.Width(70)))  SetAllExpanded(true);
            if (GUILayout.Button("Cerrar todo", EditorStyles.toolbarButton, GUILayout.Width(72))) SetAllExpanded(false);
        }
        EditorGUILayout.Space(2);
    }

    private void SetAllExpanded(bool expanded)
    {
        foreach (SectionGroup s in _sections) EditorPrefs.SetBool(PrefsPrefix + s.Title, expanded);
    }

    private void DrawSection(SectionGroup section, bool searching)
    {
        var visible = new List<SerializedProperty>();
        bool titleMatches = searching && Matches(section.Title);
        foreach (string path in section.Paths)
        {
            SerializedProperty prop = serializedObject.FindProperty(path);
            if (prop == null || !PassesShowIf(prop) || !IsPropertyVisible(prop)) continue;
            if (searching && !titleMatches && !Matches(prop.displayName) && !Matches(prop.name)) continue;
            visible.Add(prop);
        }
        if (visible.Count == 0) return;

        string key = PrefsPrefix + section.Title;
        bool expanded = searching || EditorPrefs.GetBool(key, !section.StartCollapsed);

        Rect bar = GUILayoutUtility.GetRect(1f, 22f, GUILayout.ExpandWidth(true));
        bar.xMin -= 2f;
        EditorGUI.DrawRect(bar, EditorGUIUtility.isProSkin ? new Color(0.19f, 0.19f, 0.19f) : new Color(0.78f, 0.78f, 0.78f));
        EditorGUI.DrawRect(new Rect(bar.x, bar.y, 3f, bar.height), SectionColor(section.Title));

        bool newExpanded = EditorGUI.Foldout(new Rect(bar.x + 16f, bar.y + 3f, bar.width - 60f, 16f), expanded,
                                             section.Title, true, HeaderStyle);
        if (!searching && newExpanded != expanded) EditorPrefs.SetBool(key, newExpanded);

        string badge = visible.Count == 1 && visible[0].isArray && visible[0].propertyType == SerializedPropertyType.Generic
            ? visible[0].arraySize.ToString() : visible.Count.ToString();
        GUI.Label(new Rect(bar.xMax - 40f, bar.y + 3f, 36f, 16f), badge, BadgeStyle);

        if (!newExpanded) { EditorGUILayout.Space(2); return; }

        EditorGUILayout.Space(3);
        EditorGUI.indentLevel++;
        foreach (SerializedProperty prop in visible)
        {
            EditorGUILayout.PropertyField(prop, true);
            AfterProperty(prop);
        }
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(6);
    }

    private bool Matches(string text)
        => !string.IsNullOrEmpty(text) && text.IndexOf(Search, StringComparison.OrdinalIgnoreCase) >= 0;

    // ¿Se cumplen TODOS los [ShowIf] del campo?
    private bool PassesShowIf(SerializedProperty prop)
    {
        if (!_showIf.TryGetValue(prop.propertyPath, out ShowIfAttribute[] conditions)) return true;

        foreach (ShowIfAttribute c in conditions)
        {
            SerializedProperty other = serializedObject.FindProperty(c.Field);
            if (other == null) continue;
            bool pass = Evaluate(other, c.Value);
            if (c.Invert) pass = !pass;
            if (!pass) return false;
        }
        return true;
    }

    private static bool Evaluate(SerializedProperty other, object value)
    {
        bool positive = value is string s && s == ShowIfAttribute.Positive;

        switch (other.propertyType)
        {
            case SerializedPropertyType.Boolean:
                return value is bool b ? other.boolValue == b : other.boolValue;
            case SerializedPropertyType.Enum:
                return other.intValue == Convert.ToInt32(value);
            case SerializedPropertyType.Integer:
                return positive ? other.intValue > 0 : other.intValue == Convert.ToInt32(value);
            case SerializedPropertyType.Float:
                return positive ? other.floatValue > 0f : Mathf.Approximately(other.floatValue, Convert.ToSingle(value));
            case SerializedPropertyType.ObjectReference:
                return (other.objectReferenceValue != null) == (!(value is bool r) || r);
            default:
                return true;
        }
    }

    private static GUIStyle _headerStyle, _badgeStyle;
    private static GUIStyle HeaderStyle => _headerStyle ??= new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
    private static GUIStyle BadgeStyle  => _badgeStyle  ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };

    // ---------------------------------------------------------
    // Piezas para las tarjetas de arriba
    // ---------------------------------------------------------

    // Tarjeta con ícono a la izquierda y renglones de texto a la derecha.
    protected static void HeaderCard(Sprite icon, string title, string subtitle, params string[] lines)
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            Rect iconRect = GUILayoutUtility.GetRect(44, 44, GUILayout.Width(44), GUILayout.Height(44));
            DrawSprite(iconRect, icon);

            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                if (!string.IsNullOrEmpty(subtitle)) EditorGUILayout.LabelField(subtitle, EditorStyles.miniLabel);
                foreach (string line in lines)
                    if (!string.IsNullOrEmpty(line)) EditorGUILayout.LabelField(line, EditorStyles.wordWrappedMiniLabel);
            }
        }
    }

    // Dibuja un Sprite (respetando su recorte dentro de la textura), o un cuadro vacío.
    public static void DrawSprite(Rect rect, Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
        {
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.15f));
            return;
        }

        Rect tr = sprite.textureRect;
        Texture2D tex = sprite.texture;
        Rect uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);

        float aspect = tr.width / Mathf.Max(1f, tr.height);
        Rect fit = rect;
        if (aspect > 1f) { fit.height = rect.width / aspect; fit.y += (rect.height - fit.height) * 0.5f; }
        else             { fit.width = rect.height * aspect; fit.x += (rect.width - fit.width) * 0.5f; }

        GUI.DrawTextureWithTexCoords(fit, tex, uv);
    }

    // ---------------------------------------------------------
    // ¿Quién lo usa?
    // ---------------------------------------------------------

    private static readonly string[] UsageFolders =
    {
        "Assets/GameplayAbilities", "Assets/Effects", "Assets/Attributes", "Assets/Prefabs", "Assets/48toPlay",
    };

    private void DrawUsages()
    {
        EditorGUILayout.Space(4);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("¿Quién lo usa?", EditorStyles.boldLabel);
                if (GUILayout.Button(_usages == null ? "Buscar" : "Buscar de nuevo", EditorStyles.miniButton, GUILayout.Width(110)))
                    FindUsages();
            }

            if (_usages == null)
            {
                EditorGUILayout.LabelField("Busca las habilidades, efectos, clases y prefabs que lo tienen cargado.",
                                           EditorStyles.wordWrappedMiniLabel);
                return;
            }

            if (_inRegistry.HasValue)
                EditorGUILayout.LabelField(_inRegistry.Value
                    ? "✔ Está en el registro de red."
                    : "✘ NO está en el registro de red: su VFX/ícono se ve solo en el host. Corré " +
                      "Mercenarios ▸ Actualizar los registros de red.", EditorStyles.wordWrappedMiniLabel);

            if (_usages.Count == 0)
                EditorGUILayout.LabelField("Nadie lo usa.", EditorStyles.miniLabel);

            foreach (Object o in _usages)
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField(o, typeof(Object), false);
        }
    }

    private void FindUsages()
    {
        _usages = new List<Object>();
        string self = AssetDatabase.GetAssetPath(target);

        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject t:Prefab", UsageFolders))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path == self) continue;
            foreach (string dep in AssetDatabase.GetDependencies(path, false))
            {
                if (dep != self) continue;
                Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset != null) _usages.Add(asset);
                break;
            }
        }

        _usages.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        _inRegistry = IsInNetworkRegistry();
    }
}
