using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Pone la camara de espectador en la escena abierta. Un solo componente, sin nada que
// cablear: se enciende solo cuando tu fila de la sala dice Spectator.
public static class MercSpectatorSetup
{
    [MenuItem("Mercenarios/Instalar la camara de espectador", false, 2)]
    public static void Install()
    {
        Scene scene = EditorSceneManager.GetActiveScene();

        if (Object.FindFirstObjectByType<SpectatorCamera>(FindObjectsInactive.Include) != null)
        {
            Debug.Log("[Espectador] Ya estaba en la escena. No se toca nada.");
            return;
        }

        GameObject go = new GameObject("SpectatorCamera");
        go.AddComponent<SpectatorCamera>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Espectador] Instalado y escena guardada: {scene.path}");
    }

    // Punto de entrada para batch mode: abre la escena del modo y corre lo de arriba.
    public static void InstallInArenaScene()
    {
        string[] guids = AssetDatabase.FindAssets("Mercenaries_Gamemode t:Scene");
        if (guids.Length == 0)
        {
            Debug.LogError("[Espectador] No encontre la escena Mercenaries_Gamemode.");
            EditorApplication.Exit(1);
            return;
        }

        EditorSceneManager.OpenScene(AssetDatabase.GUIDToAssetPath(guids[0]), OpenSceneMode.Single);
        Install();
    }
}
