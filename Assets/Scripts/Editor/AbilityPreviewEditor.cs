using UnityEngine;
using UnityEditor;

// ============================================================
// AbilityPreviewEditor
//
// Con el muñeco de prueba SELECCIONADO, las habilidades de su lista muestran sus
// manijas en la Scene view (las de la habilidad elegida en el Project las pone el
// inspector de esa habilidad: GameplayAbilityEditor). Cada manija cambia el asset de
// la habilidad, con Ctrl+Z.
// ============================================================
[CustomEditor(typeof(AbilityPreview))]
public class AbilityPreviewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.HelpBox(
            "Elegí una habilidad en el Project para verla acá (con sus manijas). Las de la lista se ven " +
            "siempre, para comparar; con este objeto seleccionado también se ajustan arrastrando.",
            MessageType.None);
    }

    private void OnSceneGUI()
    {
        AbilityPreview preview = (AbilityPreview)target;
        if (preview.Abilities == null) return;

        foreach (GameplayAbility ability in preview.Abilities)
            if (ability != null) ability.DrawSceneHandles(preview.transform);
    }
}
