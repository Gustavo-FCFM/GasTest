using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Pone la pared invisible en las tres bases de la escena abierta. Es un componente sin
// nada que cablear: se cuelga del MercTeamBase y saca de ahi el equipo y el tamano.
public static class MercBarrierSetup
{
    [MenuItem("Mercenarios/Instalar las paredes de las salas seguras", false, 3)]
    public static void Install()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        int added = 0;

        foreach (MercTeamBase b in Object.FindObjectsByType<MercTeamBase>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (b.GetComponent<MercSafeRoomBarrier>() != null) continue;

            b.gameObject.AddComponent<MercSafeRoomBarrier>();
            added++;
            Debug.Log($"[Salas] Pared puesta en la base del equipo {b.TeamID}.");
        }

        if (added == 0)
        {
            Debug.Log("[Salas] Todas las bases ya tenian su pared. No se toca nada.");
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Salas] Listo: {added} paredes. Escena guardada.");
    }

    public static void InstallInArenaScene()
    {
        string[] guids = AssetDatabase.FindAssets("Mercenaries_Gamemode t:Scene");
        if (guids.Length == 0)
        {
            Debug.LogError("[Salas] No encontre la escena Mercenaries_Gamemode.");
            EditorApplication.Exit(1);
            return;
        }

        EditorSceneManager.OpenScene(AssetDatabase.GUIDToAssetPath(guids[0]), OpenSceneMode.Single);
        Install();
    }
}
