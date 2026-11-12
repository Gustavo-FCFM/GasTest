using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// ============================================================
// MercBossSetup
//
// Herramienta de UNA VEZ (11 de noviembre de 2026) que arma el JEFE de mitad de partida. Después todo
// se edita a mano:
//   1. GE_EventBoss (Effects/Buffs): lo que vuelve al jefe de siempre el jefe del evento (vida máxima ×3,
//      +4 de ataque y el aura roja de los NPCs del santuario).
//   2. GE_BossBlessing (Effects/Buffs): la bendición para el equipo que lo mata (+15 % de daño físico y
//      mágico, 15 % menos daño recibido).
//   3. El evento (hijo "Boss") en el prefab de los eventos: a los 7 minutos, se va a los 3 si nadie lo mata.
//   4. La guarida (BossLair) en la escena del Cementerio, en la capilla del centro (donde está el
//      campamento "Camp_Chapel"; si no lo encuentra, en el centro del mapa).
//   5. Los registros de red (por los GE nuevos).
// Lo que ya existe no lo vuelve a crear: correrla dos veces no duplica nada.
// ============================================================
public static class MercBossSetup
{
    private const string EffectsFolder = "Assets/Effects/Buffs";
    private const string IconsFolder   = "Assets/Art/Icons/Abilities";

    // Solo atajos: si no están ahí, se buscan por nombre.
    private const string GraveyardScene = "Assets/Scenes/Mercenaries_Graveyard.unity";
    private const string ChapelCamp     = "Camp_Chapel";

    [MenuItem("Mercenarios/Jefe/Armar el jefe de mitad de partida")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        var log = new StringBuilder("[Jefe] Armado:\n");

        GameplayEffect empower  = BuildEmpower(log);
        GameplayEffect blessing = BuildBlessing(log);
        AddEvent(empower, blessing, log);
        PlaceLair(log);

        AssetDatabase.SaveAssets();
        MercRegistryTools.RefreshNetworkRegistries();
        log.AppendLine("· Registros de red actualizados.");
        return log.ToString();
    }

    // =========================================================
    // 1 y 2. LOS EFECTOS
    // =========================================================

    private static GameplayEffect BuildEmpower(StringBuilder log)
    {
        const string name = "GE_EventBoss";
        GameplayEffect existing = FindAssetIn<GameplayEffect>(name, "Assets/Effects");
        if (existing != null) { log.AppendLine($"· {name}: ya existía."); return existing; }
        EnsureFolder(EffectsFolder);

        var ge = ScriptableObject.CreateInstance<GameplayEffect>();
        ge.Duration       = 9999f;   // no vence: dura lo que vive el jefe
        ge.StackingPolicy = GameplayEffect.EStackingType.Refresh;
        ge.EffectType     = GameplayEffect.EEffectType.Buff;
        ge.TargetVFX      = FindAssetIn<GameObject>("VFX_ShrineEmpowered", "Assets/Art");
        ge.Modifiers      = new List<Modifier>
        {
            Mod(EAttributeType.MaxHealth, Modifier.EModificationType.Multiply, 3f),
            Mod(EAttributeType.Attack,    Modifier.EModificationType.Add, 4f),
        };
        AssetDatabase.CreateAsset(ge, $"{EffectsFolder}/{name}.asset");
        log.AppendLine($"· {name} creado: vida máxima ×3 y +4 de ataque" +
                       (ge.TargetVFX == null ? " (sin aura: no encontré VFX_ShrineEmpowered)." : ", con el aura roja."));
        return ge;
    }

    private static GameplayEffect BuildBlessing(StringBuilder log)
    {
        const string name = "GE_BossBlessing";
        GameplayEffect existing = FindAssetIn<GameplayEffect>(name, "Assets/Effects");
        if (existing != null) { log.AppendLine($"· {name}: ya existía."); return existing; }
        EnsureFolder(EffectsFolder);

        var ge = ScriptableObject.CreateInstance<GameplayEffect>();
        ge.Duration       = 180f;   // lo pisa "Dura la bendición (s)" del evento
        ge.StackingPolicy = GameplayEffect.EStackingType.Refresh;
        ge.EffectType     = GameplayEffect.EEffectType.Buff;   // Buff: al morir se pierde
        ge.Icon           = AssetDatabase.LoadAssetAtPath<Sprite>($"{IconsFolder}/Fighter/Battle Inspiration.png");
        ge.Modifiers      = new List<Modifier>
        {
            Mod(EAttributeType.Attack,      Modifier.EModificationType.Multiply, 1.15f),
            Mod(EAttributeType.MagicDamage, Modifier.EModificationType.Multiply, 1.15f),
            Mod(EAttributeType.Resistance,  Modifier.EModificationType.Add, 0.15f),
        };
        AssetDatabase.CreateAsset(ge, $"{EffectsFolder}/{name}.asset");
        log.AppendLine($"· {name} creado: +15 % de daño físico y mágico, 15 % menos daño recibido." +
                       (ge.Icon == null ? " ¡Ojo! Sin ícono." : ""));
        return ge;
    }

    private static Modifier Mod(EAttributeType attribute, Modifier.EModificationType type, float magnitude) =>
        new Modifier { Attribute = attribute, Type = type, Magnitude = magnitude };

    // =========================================================
    // 3. EL EVENTO
    // =========================================================

    private static void AddEvent(GameplayEffect empower, GameplayEffect blessing, StringBuilder log)
    {
        GameObject eventsPrefab = FindPrefabWith<GameModeEventManager>();
        if (eventsPrefab == null) { log.AppendLine("  ¡Ojo! No encontré el prefab de los eventos (GameModeEventManager)."); return; }
        string path = AssetDatabase.GetAssetPath(eventsPrefab);

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.GetComponentInChildren<MatchEvent_Boss>(true) != null)
            {
                log.AppendLine("· El prefab de los eventos ya tenía el jefe.");
                return;
            }

            var go = new GameObject("Boss");
            go.transform.SetParent(root.transform, false);
            var boss = go.AddComponent<MatchEvent_Boss>();
            boss.StartAt           = 420f;
            boss.BossType          = EMercEnemyType.Boss;
            boss.EmpowerEffect     = empower;
            boss.SizeMultiplier    = 1.4f;
            boss.RevealToAll       = true;
            boss.LeaveAfterSeconds = 180f;
            boss.RepeatSeconds     = 0f;
            boss.Blessing          = blessing;
            boss.BlessingSeconds   = 180f;
            boss.BonusXp           = 100f;

            PrefabUtility.SaveAsPrefabAsset(root, path);
            log.AppendLine($"· Evento del jefe agregado a {path} ▸ Boss (a los 7 min; se va a los 3 si nadie lo mata; una vez).");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // =========================================================
    // 4. LA GUARIDA EN LA ESCENA
    // =========================================================

    private static void PlaceLair(StringBuilder log)
    {
        string path = File.Exists(GraveyardScene) ? GraveyardScene : FindScenePath("Mercenaries_Graveyard");
        if (path == null) { log.AppendLine("  ¡Ojo! No encontré la escena del Cementerio: poner un BossLair a mano."); return; }

        Scene scene = SceneManager.GetSceneByPath(path);
        bool openedHere = !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

        try
        {
            foreach (GameObject top in scene.GetRootGameObjects())
                if (top.GetComponentInChildren<BossLair>(true) != null)
                {
                    log.AppendLine("· La escena del Cementerio ya tenía su guarida.");
                    return;
                }

            Vector3 at = Vector3.zero;
            string where = "el centro del mapa";
            foreach (GameObject top in scene.GetRootGameObjects())
                foreach (MercEnemySpawner camp in top.GetComponentsInChildren<MercEnemySpawner>(true))
                    if (camp.name == ChapelCamp) { at = camp.transform.position; where = $"la capilla ({ChapelCamp})"; }

            var lair = new GameObject("BossLair");
            SceneManager.MoveGameObjectToScene(lair, scene);
            lair.transform.position = at;
            lair.AddComponent<BossLair>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine($"· Guarida del jefe puesta en {path}, en {where} ({at}). Se mueve a mano.");
        }
        finally
        {
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }
    }

    // =========================================================
    // BÚSQUEDAS
    // =========================================================

    private static T FindAssetIn<T>(string name, string folder) where T : Object
    {
        foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name} {name}"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (p.StartsWith(folder) && Path.GetFileNameWithoutExtension(p) == name)
                return AssetDatabase.LoadAssetAtPath<T>(p);
        }
        return null;
    }

    private static GameObject FindPrefabWith<T>() where T : Component
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab != null && prefab.GetComponent<T>() != null) return prefab;
        }
        return null;
    }

    private static string FindScenePath(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets($"t:Scene {name}"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == name) return p;
        }
        return null;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
