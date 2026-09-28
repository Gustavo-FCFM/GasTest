using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// ============================================================
// MercOffHandPoseSetup  (herramienta de un solo uso: correr y borrar)
//
// Prepara la pose de la mano secundaria (ver OffHandPose en CharacterClassDefinition):
//   1. Crea el clip vacío PLACEHOLDER_OffHandPose junto a los otros placeholders.
//   2. Agrega a AC_Player la capa "OffHandPose" AL FINAL, con la máscara del brazo
//      izquierdo (Human Arm Left Mask, incluye los dedos), peso 0 y un solo estado
//      con el placeholder.
//   3. Al Clérigo le pone la pose del libro (HumanM@ObjectBook01_L) y, si la mano
//      izquierda está vacía, el prefab Book.
//
// Es idempotente: lo que ya esté hecho se saltea.
// ============================================================
public static class MercOffHandPoseSetup
{
    [MenuItem("Mercenarios/Instalar la pose de la mano secundaria (libro del Clérigo)", false, 7)]
    public static void Install()
    {
        AnimatorController controller = FindAsset<AnimatorController>("AC_Player t:AnimatorController", exactName: "AC_Player");
        if (controller == null) { Debug.LogError("[OffHandPose] No encontré AC_Player."); return; }

        AnimationClip placeholder = FindOrCreatePlaceholder(controller);
        AvatarMask mask = FindAsset<AvatarMask>("\"Human Arm Left Mask\" t:AvatarMask", exactName: "Human Arm Left Mask");
        if (mask == null) Debug.LogWarning("[OffHandPose] No encontré 'Human Arm Left Mask': la capa queda sin máscara, ponésela a mano.");

        // --- La capa ---
        if (controller.layers.Any(l => l.name == PlayerController.OffHandPoseLayerName))
        {
            Debug.Log("[OffHandPose] AC_Player ya tiene la capa OffHandPose.");
        }
        else
        {
            controller.AddLayer(PlayerController.OffHandPoseLayerName);

            AnimatorControllerLayer[] layers = controller.layers;   // es una copia
            AnimatorControllerLayer layer = layers[layers.Length - 1];
            layer.avatarMask    = mask;
            layer.defaultWeight = 0f;                               // la prende el código según la clase
            layer.blendingMode  = AnimatorLayerBlendingMode.Override;
            controller.layers   = layers;

            AnimatorState state = layer.stateMachine.AddState("OffHandPose");
            state.motion = placeholder;
            layer.stateMachine.defaultState = state;

            EditorUtility.SetDirty(controller);
            Debug.Log("[OffHandPose] Capa OffHandPose agregada al final de AC_Player.");
        }

        // --- El Clérigo ---
        CharacterClassDefinition cleric = FindAsset<CharacterClassDefinition>("Class_Cleric t:CharacterClassDefinition", exactName: "Class_Cleric");
        if (cleric == null)
        {
            Debug.LogWarning("[OffHandPose] No encontré Class_Cleric.");
        }
        else
        {
            if (cleric.OffHandPose == null)
            {
                cleric.OffHandPose = FindClipInModel("HumanM@ObjectBook01_L");
                if (cleric.OffHandPose == null) Debug.LogWarning("[OffHandPose] No encontré el clip HumanM@ObjectBook01_L.");
            }

            if (cleric.OffHandWeaponPrefab == null)
                cleric.OffHandWeaponPrefab = FindAsset<GameObject>("Book t:Prefab", exactName: "Book");

            EditorUtility.SetDirty(cleric);
            Debug.Log($"[OffHandPose] Clérigo: pose = {(cleric.OffHandPose != null ? cleric.OffHandPose.name : "—")}, " +
                      $"mano izquierda = {(cleric.OffHandWeaponPrefab != null ? cleric.OffHandWeaponPrefab.name : "—")}.");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[OffHandPose] Listo. Ya podés borrar esta herramienta (MercOffHandPoseSetup.cs).");
    }

    // El placeholder va en la misma carpeta que PLACEHOLDER_Hit (o la del controller).
    private static AnimationClip FindOrCreatePlaceholder(AnimatorController controller)
    {
        AnimationClip existing = FindAsset<AnimationClip>(PlayerController.OffHandPoseSlotName + " t:AnimationClip",
                                                          exactName: PlayerController.OffHandPoseSlotName);
        if (existing != null) return existing;

        AnimationClip sibling = FindAsset<AnimationClip>("PLACEHOLDER_Hit t:AnimationClip", exactName: "PLACEHOLDER_Hit");
        string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(sibling != null ? (Object)sibling : controller));

        var clip = new AnimationClip { name = PlayerController.OffHandPoseSlotName };
        string path = Path.Combine(folder, PlayerController.OffHandPoseSlotName + ".anim").Replace('\\', '/');
        AssetDatabase.CreateAsset(clip, path);
        Debug.Log($"[OffHandPose] Placeholder creado en {path}.");
        return clip;
    }

    // El clip de una pose de Kevin Iglesias vive DENTRO de su .fbx.
    private static AnimationClip FindClipInModel(string modelName)
    {
        foreach (string guid in AssetDatabase.FindAssets(modelName))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;

            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (clip != null) return clip;
        }
        return null;
    }

    private static T FindAsset<T>(string filter, string exactName = null) where T : Object
    {
        foreach (string guid in AssetDatabase.FindAssets(filter))
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset == null) continue;
            if (exactName != null && asset.name != exactName) continue;
            return asset;
        }
        return null;
    }
}
