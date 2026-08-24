using System.Collections.Generic;
using System.IO;
using FishNet.Component.Spawning;
using FishNet.Component.Transforming;
using FishNet.Object;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// ============================================================
// MercSetupTools
//
// Los menús de editor del modo Mercenarios. Acá está la PLOMERÍA (prefabs, escena,
// cableado); la forma del escenario vive aparte, en MercArenaBuilder, para poder
// iterar el diseño de la arena sin tocar nada de esto.
//
// Menú "Mercenarios":
//   1 · Crear prefabs de la demo    → el Objetivo y el fantasma EN RED.
//   2 · Crear la escena de la arena → escena NUEVA y completa (Assets/Scenes/Arena_Mercenaries).
//   3 · Regenerar la arena          → rehace solo la geometría en la escena abierta.
//
// La escena de pruebas (Test_Network) NO se toca nunca: la arena vive aparte, así
// podés seguir probando clases en la de siempre.
// ============================================================
public static class MercSetupTools
{
    // --- rutas de lo que generamos ---
    public const string PrefabFolder   = "Assets/GameMode/Prefabs";
    public const string MaterialFolder = "Assets/GameMode/Materials";
    private const string SceneFolder   = "Assets/Scenes";
    private const string ScenePath     = SceneFolder + "/Arena_Mercenaries.unity";

    public const string ObjectivePrefabPath = PrefabFolder + "/Objective_GoldBag.prefab";
    public const string EnemyPrefabPath     = PrefabFolder + "/Enemy_Ghost_Networked.prefab";

    // --- rutas de lo que YA existe en el proyecto y reusamos ---
    private const string GhostSourcePath    = "Assets/48toPlay/Enemy_Ghost.prefab";
    private const string PlayerPrefabPath   = "Assets/Scripts/Player/Player.prefab";
    private const string LobbyPrefabPath    = "Assets/Scripts/UI/UI_LobbyMenu.prefab";
    private const string NetworkManagerPath = "Assets/FishNet/Demos/Prefabs/NetworkManager.prefab";

    // =========================================================
    // 1 · PREFABS
    // =========================================================

    [MenuItem("Mercenarios/1 · Crear prefabs de la demo", false, 1)]
    public static void CreateDemoPrefabs()
    {
        EnsureFolder(PrefabFolder);
        EnsureFolder(MaterialFolder);

        CreateObjectivePrefab();
        CreateNetworkedGhostPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void CreateObjectivePrefab()
    {
        GameObject root = new GameObject("Objective_GoldBag");

        root.AddComponent<NetworkObject>();
        MercObjective objective = root.AddComponent<MercObjective>();
        objective.CharacterLayer = 1 << 7; // capa "Character"

        // Caja dorada como marcador provisional (hasta que haya un modelo de bolsa).
        // Sin collider a propósito: se levanta por cercanía, y un collider sólido en el
        // camino sería un obstáculo que trabaría a quien corre a buscarlo.
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Visual";
        cube.transform.SetParent(root.transform, false);
        cube.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
        Object.DestroyImmediate(cube.GetComponent<BoxCollider>());
        cube.GetComponent<MeshRenderer>().sharedMaterial =
            GetOrCreateMaterial("Mat_Objective", new Color(1f, 0.78f, 0.2f), new Color(0.8f, 0.55f, 0.05f));

        // Una luz propia: en un escenario sin arte todavía, es lo que hace que se vea
        // DÓNDE está el Objetivo desde la otra punta del mapa.
        GameObject lightGo = new GameObject("Glow");
        lightGo.transform.SetParent(root.transform, false);
        Light light = lightGo.AddComponent<Light>();
        light.type      = LightType.Point;
        light.color     = new Color(1f, 0.8f, 0.3f);
        light.range     = 14f;
        light.intensity = 3f;

        PrefabUtility.SaveAsPrefabAsset(root, ObjectivePrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log($"[Mercenarios] Prefab del Objetivo creado en {ObjectivePrefabPath}");
    }

    // Toma el fantasma de la game jam y hace una versión EN RED: le saca la IA vieja
    // (que era de un solo jugador y corría en todas las máquinas) y le pone la de red.
    private static void CreateNetworkedGhostPrefab()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(GhostSourcePath);
        if (source == null)
        {
            Debug.LogWarning($"[Mercenarios] No encontré {GhostSourcePath}. Salteo el prefab del enemigo: " +
                             "creá el tuyo con NetworkObject + NetworkTransform + NetworkAbilitySystemComponent + MercEnemyAI.");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instance.name = "Enemy_Ghost_Networked";

        // Rescatamos la configuración de daño de la IA vieja antes de tirarla.
        GameplayEffect damageEffect = null;
        GameObject hitVfx = null;
        float damage = 8f;

        EnemyAI legacy = instance.GetComponent<EnemyAI>();
        if (legacy != null)
        {
            damageEffect = legacy.DamageEffect;
            hitVfx       = legacy.HitVFX;
            damage       = legacy.Damage;
            Object.DestroyImmediate(legacy);
        }

        // NPC_WaveEnemy era del modo oleadas: le daba la experiencia al PRIMER jugador
        // que encontrara en la escena. En Mercenarios la baja la reparte el core.
        NPC_WaveEnemy wave = instance.GetComponent<NPC_WaveEnemy>();
        if (wave != null) Object.DestroyImmediate(wave);

        if (instance.GetComponent<NetworkObject>() == null)    instance.AddComponent<NetworkObject>();
        if (instance.GetComponent<NetworkTransform>() == null) instance.AddComponent<NetworkTransform>();
        if (instance.GetComponent<NetworkAbilitySystemComponent>() == null)
            instance.AddComponent<NetworkAbilitySystemComponent>();

        MercEnemyAI ai = instance.GetComponent<MercEnemyAI>();
        if (ai == null) ai = instance.AddComponent<MercEnemyAI>();
        ai.DamageEffect   = damageEffect;
        ai.HitVFX         = hitVfx;
        ai.FallbackDamage = damage;

        PrefabUtility.SaveAsPrefabAsset(instance, EnemyPrefabPath);
        Object.DestroyImmediate(instance);
        Debug.Log($"[Mercenarios] Prefab del enemigo en red creado en {EnemyPrefabPath}");
    }

    // =========================================================
    // 2 · ESCENA COMPLETA
    // =========================================================

    [MenuItem("Mercenarios/2 · Crear la escena de la arena", false, 2)]
    public static void CreateArenaScene()
    {
        // Lo primero: que no se pierda nada de lo que esté abierto.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("La escena ya existe",
                $"{ScenePath} ya existe. ¿La reemplazo por una nueva?", "Reemplazar", "Cancelar"))
            return;

        // Si faltan los prefabs de la demo, los generamos ahora: así el paso 2 funciona
        // solo aunque te hayas salteado el 1.
        if (FindObjectivePrefab() == null || FindEnemyPrefab() == null)
            CreateDemoPrefabs();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        ConfigureCameraAndLight();
        CreateNetworkStack();
        BuildArenaAndHud();

        EnsureFolder(SceneFolder);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings();

        Debug.Log($"[Mercenarios] Escena creada y guardada en {ScenePath}. " +
                  "Dale Play, tocá 'Iniciar Host' en el recuadro de red y elegí nombre, equipo y clase.");
    }

    // La cámara de la escena es solo el punto de vista MIENTRAS elegís en el lobby:
    // apenas aparece tu personaje, PlayerController la apaga y usa la suya.
    private static void ConfigureCameraAndLight()
    {
        Camera cam = Object.FindFirstObjectByType<Camera>();
        if (cam != null)
        {
            cam.transform.position = new Vector3(0f, 38f, -52f);
            cam.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
            cam.gameObject.name    = "LobbyCamera";
            cam.tag                = "MainCamera";
        }

        Light light = Object.FindFirstObjectByType<Light>();
        if (light != null)
        {
            light.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            light.intensity = 1.1f;
            light.shadows   = LightShadows.Soft;
        }
    }

    // Todo lo que hace falta para que la escena sea JUGABLE en red. Es lo mismo que
    // tiene la escena de pruebas, armado acá para no tener que copiarla ni ensuciarla.
    private static void CreateNetworkStack()
    {
        // --- NetworkManager de FishNet ---
        GameObject nmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkManagerPath);
        if (nmPrefab != null)
        {
            GameObject nm = (GameObject)PrefabUtility.InstantiatePrefab(nmPrefab);
            nm.name = "NetworkManager";

            // El PlayerSpawner del prefab de FishNet spawnea un jugador apenas te
            // conectás. Acá NO lo queremos: el personaje aparece recién cuando confirmás
            // el menú de entrada (nombre + equipo), y lo crea NetworkGameManager. Si lo
            // dejáramos, cada jugador entraría DOS veces.
            PlayerSpawner spawner = nm.GetComponent<PlayerSpawner>();
            if (spawner != null)
            {
                // Sacar un componente de una INSTANCIA de prefab a veces no se puede
                // (según cómo esté armado el prefab). Si Unity se queja, desarmamos la
                // instancia y ahí sí se puede borrar: perder el vínculo con el prefab de
                // FishNet no nos cuesta nada en una escena de demo.
                try
                {
                    Object.DestroyImmediate(spawner);
                }
                catch
                {
                    PrefabUtility.UnpackPrefabInstance(nm, PrefabUnpackMode.Completely,
                                                       InteractionMode.AutomatedAction);
                    Object.DestroyImmediate(nm.GetComponent<PlayerSpawner>());
                }
            }
        }
        else
        {
            Debug.LogError($"[Mercenarios] No encontré {NetworkManagerPath}. Agregá a mano un NetworkManager " +
                           "de FishNet a la escena o no va a haber red.");
        }

        // --- Administrador de partida (comparte NetworkObject con el modo de juego) ---
        GameObject managerGo = new GameObject("NetworkGameManager");
        managerGo.AddComponent<NetworkObject>();
        NetworkGameManager manager = managerGo.AddComponent<NetworkGameManager>();
        manager.PlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (manager.PlayerPrefab == null)
            Debug.LogWarning($"[Mercenarios] No encontré el prefab del jugador en {PlayerPrefabPath} — asignalo a mano.");

        managerGo.AddComponent<MercenariesGameMode>();

        // --- Recuadro de conexión (Host / Cliente) ---
        GameObject hudGo = new GameObject("ConnectionHUD");
        hudGo.AddComponent<ConnectionHUD>();

        // --- Menú de entrada: nombre, equipo y clase ---
        GameObject lobbyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyPrefabPath);
        if (lobbyPrefab != null)
        {
            GameObject lobby = (GameObject)PrefabUtility.InstantiatePrefab(lobbyPrefab);
            lobby.name = "UI_LobbyMenu";
        }
        else
        {
            Debug.LogWarning($"[Mercenarios] No encontré {LobbyPrefabPath} — sin menú de entrada no vas a " +
                             "poder elegir equipo, y sin equipo no arranca nada.");
        }

        // --- EventSystem (el lobby se clickea con el mouse) ---
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
    }

    // Para que tus amigos puedan entrar con una build, la escena tiene que estar en
    // Build Settings. La agregamos activada; ponela PRIMERA cuando compiles la demo.
    private static void AddSceneToBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var s in scenes)
            if (s.path == ScenePath) return;

        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("[Mercenarios] Escena agregada a Build Settings. Para la demo, movela al PRIMER lugar.");
    }

    // =========================================================
    // 3 · SOLO LA ARENA (para iterar el diseño sin rehacer la escena)
    // =========================================================

    [MenuItem("Mercenarios/3 · Regenerar la arena en la escena actual", false, 3)]
    public static void RebuildArena()
    {
        GameObject previous = GameObject.Find(MercArenaBuilder.ArenaRootName);
        if (previous != null)
        {
            if (!EditorUtility.DisplayDialog("Ya hay una arena",
                    "Esta escena ya tiene un ARENA_MERCENARIOS. ¿Lo reemplazo?", "Reemplazar", "Cancelar"))
                return;
            Object.DestroyImmediate(previous);
        }

        BuildArenaAndHud();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[Mercenarios] Arena regenerada. Acordate de guardar la escena.");
    }

    // Construye la arena (ver MercArenaBuilder), deja el modo de juego cableado y se
    // asegura de que el HUD exista.
    private static void BuildArenaAndHud()
    {
        EnsureFolder(MaterialFolder);

        GameObject root = MercArenaBuilder.Build(out MercTeamBase[] bases, out Transform objectivePoint);

        WireGameMode(bases, objectivePoint);
        BuildHud();

        Selection.activeGameObject = root;
    }

    // Engancha el modo de juego al NetworkGameManager de la escena: comparten
    // NetworkObject, así no hace falta un objeto de red más.
    private static void WireGameMode(MercTeamBase[] bases, Transform objectivePoint)
    {
        NetworkGameManager manager = Object.FindFirstObjectByType<NetworkGameManager>();
        if (manager == null)
        {
            Debug.LogError("[Mercenarios] No hay NetworkGameManager en la escena. El MercenariesGameMode " +
                           "tiene que vivir en un GameObject con NetworkObject.");
            return;
        }

        MercenariesGameMode gm = manager.GetComponent<MercenariesGameMode>();
        if (gm == null) gm = manager.gameObject.AddComponent<MercenariesGameMode>();

        gm.TeamBases           = bases;
        gm.ObjectiveSpawnPoint = objectivePoint;
        gm.ObjectivePrefab     = FindObjectivePrefab();
        gm.TeamColors = new[]
        {
            MercArenaBuilder.DefaultTeamColor(1),
            MercArenaBuilder.DefaultTeamColor(2),
            MercArenaBuilder.DefaultTeamColor(3),
        };

        if (gm.ObjectivePrefab == null)
            Debug.LogWarning("[Mercenarios] Falta el prefab del Objetivo — corré '1 · Crear prefabs de la demo'.");

        // Respaldo por si algún día se juega sin modo de juego: los puntos de aparición
        // sueltos del NetworkGameManager apuntan al primero de cada base.
        var fallback = new List<Transform>();
        foreach (MercTeamBase b in bases)
            if (b != null && b.SpawnPoints != null && b.SpawnPoints.Length > 0) fallback.Add(b.SpawnPoints[0]);
        manager.SpawnPoints = fallback.ToArray();

        EditorUtility.SetDirty(gm);
        EditorUtility.SetDirty(manager);
    }

    // El HUD de partida: marcador, avisos y marcador del Objetivo. Los tres se dibujan
    // solos, así que alcanza con un objeto vacío con los tres componentes.
    private static void BuildHud()
    {
        if (Object.FindFirstObjectByType<UI_MercenariesHUD>() != null) return;

        GameObject hud = new GameObject("MatchHUD");
        hud.AddComponent<UI_MercenariesHUD>();
        hud.AddComponent<UI_MatchAnnouncer>();
        hud.AddComponent<UI_ObjectiveMarker>();
    }

    // =========================================================
    // BÚSQUEDA DE PREFABS
    //
    // POR QUÉ NO SE CABLEA POR RUTA FIJA: el asset se renombra o se mueve (cosa normal)
    // y el campo queda en null sin que nada avise — el Objetivo simplemente no aparece
    // nunca y no hay forma de saber por qué. Buscar por COMPONENTE es a prueba de eso:
    // el prefab del Objetivo es "el que tiene MercObjective", se llame como se llame.
    // =========================================================

    public static GameObject FindObjectivePrefab() => FindPrefabWith<MercObjective>(ObjectivePrefabPath);
    public static GameObject FindEnemyPrefab()     => FindPrefabWith<MercEnemyAI>(EnemyPrefabPath);

    private static GameObject FindPrefabWith<T>(string preferredPath) where T : Component
    {
        // Atajo: la ruta donde lo generamos nosotros.
        GameObject byPath = AssetDatabase.LoadAssetAtPath<GameObject>(preferredPath);
        if (byPath != null && byPath.GetComponent<T>() != null) return byPath;

        // Si no está ahí, se busca: primero en nuestra carpeta, después en todo el
        // proyecto (más lento, pero solo pasa cuando hace falta).
        foreach (string folder in new[] { PrefabFolder, "Assets" })
        {
            if (!AssetDatabase.IsValidFolder(folder)) continue;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (go != null && go.GetComponent<T>() != null) return go;
            }
        }
        return null;
    }

    // =========================================================
    // AYUDANTES COMPARTIDOS
    // =========================================================

    // Material URP reutilizable. Si ya existe el asset se devuelve ese, así regenerar
    // la arena no llena el proyecto de materiales repetidos (y respeta los cambios que
    // le hayas hecho a mano al material).
    public static Material GetOrCreateMaterial(string name, Color color, Color? emission = null)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader) { color = color };
        if (mat.HasProperty("_BaseColor"))  mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.12f);

        if (emission.HasValue)
        {
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emission.Value);
        }

        EnsureFolder(MaterialFolder);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    public static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;

        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        string leaf   = Path.GetFileName(folder);

        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
