using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

// ============================================================
// GameplayAbilityEditor
//
// Inspector de TODAS las GameplayAbility (y sus subclases), sobre la base común por
// secciones (SectionedInspector: secciones plegables, buscador, campos condicionales,
// avisos, "¿Quién lo usa?"). Lo propio de las habilidades:
//
//  · Las secciones comunes (General, Costo y cooldown, Efectos, VFX...) salen SIEMPRE en
//    el mismo orden y con el mismo nombre (AbilitySection.Order).
//  · Tarjeta de arriba: ícono, nombre, tipo, cooldown, cargas, cuántos efectos y VFX.
//  · Se esconden TargetLayer en las que no buscan a nadie y el esquema viejo de animación
//    cuando ya hay AnimationClip.
//  · Avisos: entradas de efectos o VFX que nunca se van a aplicar, cooldown sin tag,
//    TargetLayer vacío, y si el asset todavía está sin guardar en el formato nuevo.
//  · Debajo del AnimationClip, el RESUMEN del clip (duración y frames de impacto).
//  · VISTA PREVIA EN ESCENA: con un muñeco de prueba (AbilityPreview) en la escena, la
//    forma de la habilidad se dibuja ahí y se ajusta ARRASTRANDO las manijas.
// ============================================================
[CustomEditor(typeof(GameplayAbility), true)]
[CanEditMultipleObjects]
public class GameplayAbilityEditor : SectionedInspector
{
    private const string HandlesPrefs = "GAS.Inspector.SceneHandles";

    protected override string[] SectionOrder => AbilitySection.Order;
    protected override string PrefsPrefix => "GAS.Inspector.Section.";

    private GameplayAbility Ability => (GameplayAbility)target;

    // =========================================================
    // CICLO DE VIDA
    // =========================================================

    protected override void OnEnable()
    {
        base.OnEnable();
        SceneView.duringSceneGui += OnSceneGUIForAbility;
        SceneView.RepaintAll();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUIForAbility;
        SceneView.RepaintAll();
    }

    // La forma en la escena se redibuja con cada número que se toca.
    protected override void OnValuesChanged() => SceneView.RepaintAll();

    // =========================================================
    // TARJETA, AVISOS Y CAMPOS
    // =========================================================

    protected override void DrawHeaderCard()
    {
        GameplayAbility a = Ability;
        HeaderCard(a.AbilityIcon, string.IsNullOrEmpty(a.AbilityName) ? a.name : a.AbilityName,
                   a.GetType().Name, QuickFacts(a));

        if (a.UpgradedOnLoad)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.HelpBox("Este asset tenía el formato viejo (DamageEffect, HitVFX...) y ya se pasó a " +
                                        "las listas nuevas, con los mismos valores. Se guarda en disco la próxima vez " +
                                        "que lo toques, o ahora:", MessageType.Info);
                if (GUILayout.Button("Guardar", GUILayout.Width(64), GUILayout.Height(38)))
                {
                    EditorUtility.SetDirty(a);
                    AssetDatabase.SaveAssetIfDirty(a);
                    a.UpgradedOnLoad = false;
                }
            }
        }
    }

    private static string QuickFacts(GameplayAbility a)
    {
        var parts = new List<string>();

        if (a.UseAttackSpeedAsCooldown) parts.Add("Cooldown: ritmo de ataque");
        else if (a.CooldownDuration > 0f) parts.Add($"Cooldown {a.CooldownDuration:0.##} s");
        else if (a.CooldownEffect != null) parts.Add($"Cooldown {a.CooldownEffect.Duration:0.##} s (del GE)");
        else parts.Add("Sin cooldown");

        if (a.MaxCharges > 1) parts.Add($"{a.MaxCharges} cargas");
        if (a.CostEffect != null) parts.Add($"Costo: {a.CostEffect.name}");

        int effects = 0;
        if (a.Effects != null) foreach (AbilityEffect e in a.Effects) if (e.Effect != null) effects++;
        int visuals = 0;
        if (a.Visuals != null) foreach (AbilityVisual v in a.Visuals) if (v.VFXPrefab != null) visuals++;
        parts.Add($"{effects} efectos · {visuals} VFX");

        return string.Join("  ·  ", parts);
    }

    protected override void CollectWarnings(List<string> into) => CollectAbilityWarnings(Ability, into);

    // Lo que está configurado pero nunca va a hacer nada (o va a fallar).
    public static void CollectAbilityWarnings(GameplayAbility a, List<string> into)
    {
        if (a.CooldownEffect != null && (a.CooldownEffect.GrantedTags == null || a.CooldownEffect.GrantedTags.Count == 0))
            into.Add($"• El CooldownEffect '{a.CooldownEffect.name}' no tiene GrantedTags: la habilidad no va a tener cooldown real.");

        if (a.UsesTargetLayer && a.TargetLayer.value == 0)
            into.Add("• TargetLayer está en 'Nothing': no va a alcanzar a nadie (normalmente va 'Character').");

        if (a.Effects != null)
            for (int i = 0; i < a.Effects.Count; i++)
            {
                string problem = AbilityEffectDrawer.Problem(a, a.Effects[i]);
                if (problem != null) into.Add($"• Efecto {i}: {problem}");
            }

        if (a.Visuals != null)
            for (int i = 0; i < a.Visuals.Count; i++)
            {
                string problem = AbilityVisualDrawer.Problem(a, a.Visuals[i]);
                if (problem != null) into.Add($"• VFX {i}: {problem}");
            }
    }

    protected override bool IsPropertyVisible(SerializedProperty prop)
    {
        // El esquema viejo de animación: solo si NO hay clip (con clip el código lo ignora).
        if ((prop.propertyPath == "AnimationTriggerName" || prop.propertyPath == "AnimationID") &&
            Ability.AnimationClip != null)
            return false;

        return prop.propertyPath != "TargetLayer" || Ability.UsesTargetLayer;
    }

    protected override void AfterProperty(SerializedProperty prop)
    {
        if (prop.propertyPath == "AnimationClip" && Ability.AnimationClip != null && !prop.hasMultipleDifferentValues)
            DrawClipSummary(Ability);
    }

    protected override bool? IsInNetworkRegistry()
    {
        var registry = Resources.Load<GameplayAbilityRegistry>("GameplayAbilityRegistry");
        return registry != null ? registry.Abilities.Contains(Ability) : (bool?)null;
    }

    protected override Color SectionColor(string title)
    {
        switch (title)
        {
            case AbilitySection.General:      return new Color(0.75f, 0.75f, 0.75f);
            case AbilitySection.CostCooldown: return new Color(0.35f, 0.65f, 1f);
            case AbilitySection.Rules:        return new Color(0.6f, 0.6f, 0.6f);
            case AbilitySection.Targeting:    return new Color(1f, 0.85f, 0.2f);
            case AbilitySection.Shape:        return new Color(1f, 0.55f, 0.15f);
            case AbilitySection.Movement:     return new Color(0.3f, 0.9f, 0.9f);
            case AbilitySection.Timing:       return new Color(0.7f, 0.7f, 1f);
            case AbilitySection.Effects:      return new Color(1f, 0.35f, 0.35f);
            case AbilitySection.Visuals:      return new Color(0.85f, 0.45f, 1f);
            case AbilitySection.Animation:    return new Color(0.45f, 0.85f, 0.45f);
            case AbilitySection.Sound:        return new Color(0.4f, 0.8f, 0.7f);
            default:                          return base.SectionColor(title);
        }
    }

    // Duración del clip y momentos de impacto leídos de sus Animation Events. Es la
    // misma lectura que hace el servidor en runtime (GameplayAbility.GetHitFrameTimes).
    private static void DrawClipSummary(GameplayAbility ability)
    {
        AnimationClip clip = ability.AnimationClip;
        var times = ability.GetHitFrameTimes();

        if (times.Count > 0)
        {
            string list = string.Join("s, ", times.ConvertAll(t => t.ToString("0.00"))) + "s";
            EditorGUILayout.HelpBox(
                $"Clip: {clip.length:0.00}s\n" +
                $"Frames de impacto ({times.Count}): {list}\n" +
                (times.Count > 1 ? "Varios eventos = golpe escalonado (cada enemigo recibe uno solo)."
                                 : "El golpe cae en ese momento del clip."),
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                $"Clip: {clip.length:0.00}s\n" +
                $"Este clip no tiene eventos '{GameplayAbility.HitFrameEventName}', así que el golpe usa el " +
                $"delay fijo de la habilidad.\n" +
                $"Para que caiga en el frame exacto: seleccioná el FBX → pestaña Animation → sección " +
                $"Events → agregá un evento con esa función.",
                MessageType.None);
        }
    }

    // =========================================================
    // VISTA PREVIA EN ESCENA
    // =========================================================

    protected override void DrawAfterSections()
    {
        EditorGUILayout.Space(4);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Vista previa en escena", EditorStyles.boldLabel);

            AbilityPreview dummy = FindDummy();
            if (dummy == null)
            {
                EditorGUILayout.LabelField(
                    "Con un muñeco de prueba en la escena, la forma de esta habilidad se dibuja ahí (con anillos " +
                    "de distancia) y se ajusta arrastrando las manijas.", EditorStyles.wordWrappedMiniLabel);

                if (GUILayout.Button("Poner un muñeco de prueba en la escena"))
                    CreateDummy();
                return;
            }

            bool handles = EditorPrefs.GetBool(HandlesPrefs, true);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Muñeco: {dummy.name}", EditorStyles.miniLabel);
                bool newHandles = GUILayout.Toggle(handles, "Manijas", EditorStyles.miniButtonLeft, GUILayout.Width(64));
                if (newHandles != handles) { EditorPrefs.SetBool(HandlesPrefs, newHandles); SceneView.RepaintAll(); }

                if (GUILayout.Button("Ir al muñeco", EditorStyles.miniButtonRight, GUILayout.Width(84)))
                    FrameDummy(dummy);
            }

            EditorGUILayout.LabelField(
                "La forma sale desde el muñeco, hacia donde mira (la flecha azul). Las manijas cambian el asset " +
                "(con Ctrl+Z).", EditorStyles.wordWrappedMiniLabel);
        }
    }

    private static AbilityPreview FindDummy()
        => Object.FindFirstObjectByType<AbilityPreview>(FindObjectsInactive.Exclude);

    // Lo pone donde mira la Scene view, apoyado en el piso. Es un objeto de escena que se
    // puede borrar cuando quieras (no hace nada en el juego).
    private static void CreateDummy()
    {
        SceneView view = SceneView.lastActiveSceneView;
        Vector3 at = view != null ? view.pivot : Vector3.zero;
        if (Physics.Raycast(at + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 200f)) at = hit.point;

        var go = new GameObject("AbilityPreview (muñeco de prueba)");
        go.transform.position = at;
        go.AddComponent<AbilityPreview>();
        Undo.RegisterCreatedObjectUndo(go, "Poner muñeco de prueba");

        SceneView.RepaintAll();
    }

    private static void FrameDummy(AbilityPreview dummy)
    {
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null) return;
        view.Frame(new Bounds(dummy.transform.position + Vector3.up, Vector3.one * 12f), false);
    }

    // Las manijas de las habilidades elegidas, sobre el muñeco. La FORMA la dibuja el
    // propio muñeco con sus gizmos (AbilityPreview.OnDrawGizmos).
    private void OnSceneGUIForAbility(SceneView view)
    {
        if (!EditorPrefs.GetBool(HandlesPrefs, true)) return;

        AbilityPreview dummy = FindDummy();
        if (dummy == null) return;

        foreach (Object t in targets)
            if (t is GameplayAbility ability) ability.DrawSceneHandles(dummy.transform);

        // Los números del inspector siguen a las manijas mientras se arrastra.
        if (GUI.changed) Repaint();
    }
}
