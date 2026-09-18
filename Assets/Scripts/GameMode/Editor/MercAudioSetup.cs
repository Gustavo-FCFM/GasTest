using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// ============================================================
// MercAudioSetup
//
// Deja el audio listo para cargarle clips:
//   · Crea Assets/Resources/AudioLibrary.asset si no existe (ahí van pasos, golpes,
//     avisos, UI y música; los sonidos de cada habilidad y efecto van en su propio asset).
//   · Le pone un AudioListener a la cámara de la sala, que no tenía: sin eso, en el menú
//     y en la sala de espera no sonaría nada.
//
// Se puede correr las veces que haga falta.
// ============================================================
public static class MercAudioSetup
{
    private const string LibraryPath = "Assets/Resources/" + AudioLibrary.ResourceName + ".asset";

    [MenuItem("Mercenarios/Crear la biblioteca de audio", false, 5)]
    public static void Install()
    {
        // La biblioteca.
        AudioLibrary lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
        if (lib == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            lib = ScriptableObject.CreateInstance<AudioLibrary>();
            AssetDatabase.CreateAsset(lib, LibraryPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Audio] Biblioteca creada: {LibraryPath}. Arrastrale clips desde el Inspector.");
        }
        else
        {
            Debug.Log("[Audio] La biblioteca ya existía. No se toca.");
        }

        // El oído de la sala.
        Scene scene = EditorSceneManager.GetActiveScene();
        Camera lobbyCam = Camera.main;
        if (lobbyCam == null)
        {
            Camera[] cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            if (cams.Length > 0) lobbyCam = cams[0];
        }

        if (lobbyCam != null && lobbyCam.GetComponent<AudioListener>() == null)
        {
            Undo.AddComponent<AudioListener>(lobbyCam.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Audio] AudioListener agregado a '{lobbyCam.name}' y escena guardada.");
        }

        Selection.activeObject = lib;
        EditorGUIUtility.PingObject(lib);
    }
}
