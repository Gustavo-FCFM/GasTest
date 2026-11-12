using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// ============================================================
// MercNecromancerSetup
//
// Herramienta de UNA VEZ (12 de noviembre de 2026, pedido de Gustavo): el NIGROMANTE, un jefe secundario. Es
// el mago, negro y 1.3 veces más grande. Ataca de lejos (GA_NecroBolt, el misil arcano) e INVOCA NPCs
// mientras pelea, con un campamento normal dentro de su prefab ("Summons"): su tabla dice qué puede invocar,
// con qué probabilidad y cuántos de cada uno a la vez. Cuando muere, sus invocados caen con él.
//   · Net_Necromancer: variante de Net_Mage (ASDef_Necromancer: 90 de vida, 22 de ataque, 2.6 m/s), 150 de exp.
//   · Summons (MercEnemySpawner, "Solo mientras su dueño viva" + "Solo si su dueño pelea"): hasta 5 a la vez;
//     fantasmas (máx. 3), zombis (2), esqueletos (2) y magos (2); uno cada 4 s, repone a los 8 s.
// Lo agrega al catálogo y actualiza los registros de red. No toca la escena.
// Usa las piezas de MercJamEnemiesSetup. Lo que ya existe no lo vuelve a crear.
// ============================================================
public static class MercNecromancerSetup
{
    private static readonly Color NecroTint = new Color(0.16f, 0.15f, 0.19f);

    [MenuItem("Mercenarios/Enemigos/Armar el Nigromante")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        var log = new StringBuilder("[Nigromante] Armado:\n");

        GameplayEffect classDamage = MercJamEnemiesSetup.Load<GameplayEffect>("Assets/Effects/Damage/GE_Class_Damage.asset");

        GameplayAbility bolt = MercJamEnemiesSetup.CopyAsset<GameplayAbility>("Assets/GameplayAbilities/Enemies/GA_FireballEnemy.asset",
            "Assets/GameplayAbilities/Enemies/GA_NecroBolt.asset", log, so =>
            {
                so.FindProperty("AbilityName").stringValue   = "Necrotic Bolt";
                so.FindProperty("AbilityNameEs").stringValue = "Proyectil necrótico";
                SerializedProperty effects = so.FindProperty("Effects");
                while (effects.arraySize > 1) effects.DeleteArrayElementAtIndex(effects.arraySize - 1);
                effects.GetArrayElementAtIndex(0).FindPropertyRelative("Effect").objectReferenceValue = classDamage;
                so.FindProperty("ProjectilePrefab").objectReferenceValue =
                    MercJamEnemiesSetup.Load<GameObject>("Assets/Prefabs/Projectiles/PF_ArcaneMissile.prefab");
                SerializedProperty visuals = so.FindProperty("Visuals");
                if (visuals.arraySize > 0)
                    visuals.GetArrayElementAtIndex(0).FindPropertyRelative("VFXPrefab").objectReferenceValue =
                        MercJamEnemiesSetup.Load<GameObject>("Assets/AssetsExtra/Hovl Studio/Magic effects pack/Prefabs/Sparks/Sparks explode pink.prefab");
            });

        AttributeSetDefinition stats = MercJamEnemiesSetup.CopyAsset<AttributeSetDefinition>(
            "Assets/Attributes/Enemies/ASDef_Mage.asset", "Assets/Attributes/Enemies/ASDef_Necromancer.asset", log, so =>
            {
                SerializedProperty list = so.FindProperty("InitialAttributes");
                for (int i = 0; i < list.arraySize; i++)
                {
                    SerializedProperty a = list.GetArrayElementAtIndex(i);
                    switch ((EAttributeType)a.FindPropertyRelative("Attribute").intValue)
                    {
                        case EAttributeType.Health:   a.FindPropertyRelative("BaseValue").floatValue = 90f;  break;
                        case EAttributeType.MovSpeed: a.FindPropertyRelative("BaseValue").floatValue = 2.6f; break;
                        case EAttributeType.Attack:   a.FindPropertyRelative("BaseValue").floatValue = 22f;  break;
                    }
                }
            });

        const string path = "Assets/Prefabs/Enemies/Net_Necromancer.prefab";
        GameObject necro = MercJamEnemiesSetup.Load<GameObject>(path);
        if (necro == null)
        {
            necro = MercJamEnemiesSetup.MakeVariant(MercJamEnemiesSetup.Load<GameObject>("Assets/Prefabs/Enemies/Net_Mage.prefab"), path, go =>
            {
                go.transform.localScale *= 1.3f;
                go.GetComponent<AbilitySystemComponent>().CharacterRoleDefinition = stats;

                MercEnemyAI ai = go.GetComponent<MercEnemyAI>();
                ai.AbilityToUse     = bolt;
                ai.DetectionRadius  = 16f;
                ai.LeashRadius      = 22f;
                ai.AttackRange      = 14f;
                ai.AttackCooldown   = 2.2f;
                ai.KeepDistance     = 7f;     // pelea de lejos, detrás de sus invocados
                ai.ExperienceReward = 150f;
                ai.SummonVFX        = MercJamEnemiesSetup.Load<GameObject>(
                    "Assets/AssetsExtra/Hovl Studio/Magic effects pack/Prefabs/Magic circles/Magic circle 2.prefab");

                foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer || r.sharedMaterial == null) continue;
                    r.sharedMaterial = MercJamEnemiesSetup.TintedMaterial(r.sharedMaterial, "Net_Necromancer", NecroTint);
                    break;
                }

                // Sus invocados: un campamento que lo sigue.
                var summons = new GameObject("Summons").AddComponent<MercEnemySpawner>();
                summons.transform.SetParent(go.transform, false);
                summons.OnlyWhileOwnerAlive    = true;
                summons.OnlyWhileOwnerFighting = true;
                summons.PopulateDuringWarmup   = false;
                summons.Count          = 5;
                summons.SpawnRadius    = 3.5f;
                summons.SpawnInterval  = 4f;     // uno cada 4 s mientras le falten
                summons.RespawnSeconds = 8f;
                summons.SpawnTable = new List<MercEnemySpawner.SpawnRule>
                {
                    new MercEnemySpawner.SpawnRule { Type = EMercEnemyType.Ghost,          Weight = 100f, MaxAlive = 3 },
                    new MercEnemySpawner.SpawnRule { Type = EMercEnemyType.BloatedZombie,  Weight = 50f,  MaxAlive = 2 },
                    new MercEnemySpawner.SpawnRule { Type = EMercEnemyType.ShieldSkeleton, Weight = 50f,  MaxAlive = 2 },
                    new MercEnemySpawner.SpawnRule { Type = EMercEnemyType.Mage,           Weight = 40f,  MaxAlive = 2 },
                };
            });
            log.AppendLine("· Net_Necromancer creado (negro, ×1.3, ataca de lejos, invoca hasta 5).");
        }

        MercEnemyCatalog catalog = MercEnemyCatalog.Instance;
        if (catalog != null)
        {
            MercJamEnemiesSetup.SetCatalog(catalog, EMercEnemyType.Necromancer, necro);
            EditorUtility.SetDirty(catalog);
            log.AppendLine("· Catálogo: Nigromante.");
        }

        AssetDatabase.SaveAssets();
        MercRegistryTools.RefreshNetworkRegistries();
        log.AppendLine("· Registros de red actualizados.");
        return log.ToString();
    }
}
