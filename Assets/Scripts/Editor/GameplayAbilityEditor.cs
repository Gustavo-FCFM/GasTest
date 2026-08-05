using UnityEngine;
using UnityEditor;

// ============================================================
// GameplayAbilityEditor
//
// Inspector a medida de TODAS las GameplayAbility (y sus subclases). Hace dos cosas:
//
//  1) ESCONDE los campos del esquema VIEJO de animación (AnimationTriggerName y
//     AnimationID) cuando la habilidad ya tiene un AnimationClip: en ese caso el
//     código los ignora por completo, así que solo ensucian el inspector. Si no hay
//     clip, se muestran normalmente (siguen siendo el fallback).
//
//  2) Muestra un RESUMEN del clip: su duración y en qué momentos tiene marcados los
//     frames de impacto (los Animation Events). Así se ve de un vistazo si el golpe
//     lo maneja la animación o si todavía depende del delay fijo de la habilidad.
//
// Es solo presentación: no cambia ningún dato ni cómo corre el juego.
// ============================================================
[CustomEditor(typeof(GameplayAbility), true)]
public class GameplayAbilityEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        GameplayAbility ability = (GameplayAbility)target;
        bool usingClip = ability.AnimationClip != null;

        SerializedProperty prop = serializedObject.GetIterator();
        bool enterChildren = true;

        while (prop.NextVisible(enterChildren))
        {
            enterChildren = false;

            // El campo del script va deshabilitado, como en un inspector normal.
            if (prop.propertyPath == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(prop);
                continue;
            }

            // Campos del esquema viejo: solo si NO hay clip.
            if (usingClip && IsLegacyAnimationField(prop.propertyPath)) continue;

            EditorGUILayout.PropertyField(prop, true);

            // Justo debajo del clip, el resumen de sus frames de impacto.
            if (prop.propertyPath == "AnimationClip" && usingClip)
                DrawClipSummary(ability);
        }

        serializedObject.ApplyModifiedProperties();
    }

    // Campos que el esquema nuevo (clip en el asset) deja sin uso.
    private static bool IsLegacyAnimationField(string path)
    {
        return path == "AnimationTriggerName" || path == "AnimationID";
    }

    // Duración del clip y momentos de impacto leídos de sus Animation Events. Es la
    // misma lectura que hace el servidor en runtime (GameplayAbility.GetHitFrameTimes),
    // así que lo que se ve acá es exactamente lo que se va a usar.
    private static void DrawClipSummary(GameplayAbility ability)
    {
        AnimationClip clip = ability.AnimationClip;
        var times = ability.GetHitFrameTimes();

        EditorGUI.indentLevel++;

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

        EditorGUI.indentLevel--;
    }
}
