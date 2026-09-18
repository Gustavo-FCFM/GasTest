using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// ============================================================
// MercMainMenuSetup
//
// Instala el menú principal en la escena de la arena: el panel (UI_MainMenu), la órbita
// con blur sobre la cámara de la sala (MenuOrbitCamera) y un UI_SettingsPanel para poder
// tocarle colores en el Inspector. Todo se dibuja por código, así que no hay nada más
// que cablear.
//
// Se puede correr las veces que haga falta: solo agrega lo que falte. Si en el Build
// Settings quedó una escena "MainMenu" de la versión anterior (menú en escena aparte),
// la saca de la lista — el archivo se puede borrar a mano.
// ============================================================
public static class MercMainMenuSetup
{
    private const string OldMenuScenePath = "Assets/Scenes/MainMenu.unity";

    [MenuItem("Mercenarios/Instalar el menú principal en la arena", false, 4)]
    public static void Install()
    {
        Scene scene = EditorSceneManager.GetActiveScene();

        if (Object.FindFirstObjectByType<MercenariesGameMode>(FindObjectsInactive.Include) == null)
        {
            Debug.LogError("[Menú principal] Esta no es la escena de la arena (no hay MercenariesGameMode). " +
                           "Abrí Mercenaries_Gamemode y volvé a correrlo.");
            return;
        }

        bool changed = false;

        // El panel.
        if (Object.FindFirstObjectByType<UI_MainMenu>(FindObjectsInactive.Include) == null)
        {
            GameObject go = new GameObject("UI_MainMenu");
            go.AddComponent<UI_MainMenu>();
            Undo.RegisterCreatedObjectUndo(go, "Instalar menú principal");
            changed = true;
            Debug.Log("[Menú principal] UI_MainMenu agregado.");
        }

        // La órbita, sobre la cámara de la sala (la única cámara de escena; la del jugador
        // es un prefab que se instancia al spawnear).
        if (Object.FindFirstObjectByType<MenuOrbitCamera>(FindObjectsInactive.Include) == null)
        {
            Camera lobbyCam = FindLobbyCamera();
            if (lobbyCam == null)
            {
                Debug.LogWarning("[Menú principal] No encontré la cámara de la sala: agregá MenuOrbitCamera " +
                                 "a mano a la cámara de la escena.");
            }
            else
            {
                Undo.AddComponent<MenuOrbitCamera>(lobbyCam.gameObject);
                changed = true;
                Debug.Log($"[Menú principal] MenuOrbitCamera agregado a '{lobbyCam.name}'.");
            }
        }

        // El panel de ajustes se crea solo al abrirlo; se deja uno en la escena para poder
        // tocarle colores y medidas.
        if (Object.FindFirstObjectByType<UI_SettingsPanel>(FindObjectsInactive.Include) == null)
        {
            GameObject go = new GameObject("UI_SettingsPanel");
            go.AddComponent<UI_SettingsPanel>();
            Undo.RegisterCreatedObjectUndo(go, "Instalar panel de ajustes");
            changed = true;
            Debug.Log("[Menú principal] UI_SettingsPanel agregado.");
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        RemoveOldMenuSceneFromBuild();

        Debug.Log(changed ? $"[Menú principal] Listo y escena guardada: {scene.path}. Dale Play."
                          : "[Menú principal] Ya estaba todo instalado. No se tocó nada.");
    }

    private static Camera FindLobbyCamera()
    {
        Camera main = Camera.main;
        if (main != null) return main;

        Camera[] cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        return cams.Length > 0 ? cams[0] : null;
    }

    // La versión anterior del menú vivía en una escena aparte, primera del Build. Ya no
    // hace falta: la arena vuelve a ser la única escena.
    private static void RemoveOldMenuSceneFromBuild()
    {
        var scenes = new List<EditorBuildSettingsScene>();
        bool removed = false;

        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
        {
            if (s.path == OldMenuScenePath) { removed = true; continue; }
            scenes.Add(s);
        }

        if (!removed) return;

        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"[Menú principal] Se sacó {OldMenuScenePath} del Build Settings: el menú ahora vive en la " +
                  "arena. El archivo de esa escena se puede borrar.");
    }
}
