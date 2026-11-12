using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// ============================================================
// MercGraveyardCampsSetup
//
// Herramienta de UNA VEZ (12 de noviembre de 2026, pedido de Gustavo): más campamentos en el Cementerio,
// DENTRO del prefab del sector (así salen en los tres sectores y la escena no se toca):
//   · Camp_Ramp_R y Camp_Ramp_L: arriba de las dos rampas de los nichos (Ramp_Headstones, a 4 m). Magos.
//   · Camp_Catacomb_Tower: a la mitad del pasillo subterráneo que va a la torre (-6 m).
//   · Camp_Catacomb_Fosa: a la mitad del pasillo subterráneo largo que va al patio de la fosa.
//     Bajo tierra: fantasmas, zombis hinchados y esqueletos (en un pasillo, el veneno y el escudo pesan).
// Los puntos están probados contra el NavMesh. Después se mueven y se cambian a mano, como cualquier
// campamento. Lo que ya existe no lo vuelve a crear.
// ============================================================
public static class MercGraveyardCampsSetup
{
    private const string SectorPath = "Assets/Prefabs/Map/Graveyard_Sector.prefab";

    [MenuItem("Mercenarios/Enemigos/Agregar campamentos al Cementerio (rampas y catacumbas)")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        var log = new StringBuilder("[Campamentos del Cementerio] Armado:\n");

        var mages = new List<MercEnemySpawner.SpawnRule>
        {
            new MercEnemySpawner.SpawnRule { Type = EMercEnemyType.IceMage,  Weight = 60f },
            new MercEnemySpawner.SpawnRule { Type = EMercEnemyType.RockMage, Weight = 40f, MaxAlive = 1 },
            new MercEnemySpawner.SpawnRule { Type = EMercEnemyType.Mage,     Weight = 40f },
        };
        var underground = new List<MercEnemySpawner.SpawnRule>
        {
            new MercEnemySpawner.SpawnRule { Type = EMercEnemyType.Ghost,          Weight = 60f },
            new MercEnemySpawner.SpawnRule { Type = EMercEnemyType.BloatedZombie,  Weight = 50f, MaxAlive = 2 },
            new MercEnemySpawner.SpawnRule { Type = EMercEnemyType.ShieldSkeleton, Weight = 50f, MaxAlive = 1 },
        };

        GameObject root = PrefabUtility.LoadPrefabContents(SectorPath);
        try
        {
            Transform group = root.transform.Find("Camps");
            if (group == null)
            {
                group = new GameObject("Camps").transform;
                group.SetParent(root.transform, false);
            }

            int added = 0;
            added += AddCamp(group, "Camp_Ramp_R",         new Vector3(10.2f, 4.1f, 33.9f),  2, 2.5f, mages, log);
            added += AddCamp(group, "Camp_Ramp_L",         new Vector3(-10.2f, 4.1f, 33.9f), 2, 2.5f, mages, log);
            added += AddCamp(group, "Camp_Catacomb_Tower", new Vector3(17f, -6f, 10f),       3, 3f,   underground, log);
            added += AddCamp(group, "Camp_Catacomb_Fosa",  new Vector3(-12f, -6f, 38f),      3, 3f,   underground, log);

            if (added > 0) PrefabUtility.SaveAsPrefabAsset(root, SectorPath);
            log.AppendLine(added > 0 ? $"· {added} campamentos nuevos en {SectorPath} ▸ Camps (×3 sectores)."
                                     : "· Los campamentos ya estaban.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        return log.ToString();
    }

    private static int AddCamp(Transform group, string name, Vector3 localPosition, int count, float radius,
                               List<MercEnemySpawner.SpawnRule> table, StringBuilder log)
    {
        if (group.Find(name) != null) return 0;

        var go = new GameObject(name);
        go.transform.SetParent(group, false);
        go.transform.localPosition = localPosition;

        var camp = go.AddComponent<MercEnemySpawner>();
        camp.Count       = count;
        camp.SpawnRadius = radius;
        camp.SpawnTable  = new List<MercEnemySpawner.SpawnRule>();
        foreach (MercEnemySpawner.SpawnRule rule in table)
            camp.SpawnTable.Add(new MercEnemySpawner.SpawnRule
            {
                Type = rule.Type, Weight = rule.Weight, UnlockMinute = rule.UnlockMinute, MaxAlive = rule.MaxAlive,
            });

        log.AppendLine($"· {name} en {localPosition} ({count} vivos).");
        return 1;
    }
}
