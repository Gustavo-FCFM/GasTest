using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// ============================================================
// MercZombieSetup
//
// Herramienta de UNA VEZ (12 de noviembre de 2026, pedido de Gustavo): el ZOMBI HINCHADO. Es el fantasma,
// verde y más gordo, más lento y con más vida. Al morir REVIENTA (daño a los de alrededor) y deja una NUBE
// DE VENENO unos segundos: obliga a matarlo de lejos o a alejarse.
//   · Net_BloatedZombie: variante de Net_Enemy (ASDef_Zombie: 45 de vida, 10 de ataque, 2.6 m/s).
//   · El estallido: la sección "Al morir" de MercEnemyAI (3.5 m, GE_ZombieBurst = 1.5 × su ataque).
//   · La nube: Summon_ZombiePoison (variante de SummonZone_Base, "Sigue sin su dueño"): 3 m, 6 s, cada
//     1 s GE_ZombiePoison (4 de daño fijo) a los enemigos que estén dentro.
// Lo agrega al catálogo y actualiza los registros de red. No toca la escena: el campamento se elige a mano.
// Usa las piezas de MercJamEnemiesSetup. Lo que ya existe no lo vuelve a crear.
// ============================================================
public static class MercZombieSetup
{
    private static readonly Color ZombieTint = new Color(0.45f, 0.85f, 0.30f);

    [MenuItem("Mercenarios/Enemigos/Armar el zombi hinchado")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        var log = new StringBuilder("[Zombi hinchado] Armado:\n");

        // --- efectos ---
        GameplayEffect burst = MercJamEnemiesSetup.CopyAsset<GameplayEffect>("Assets/Effects/Damage/GE_Class_Damage.asset",
            "Assets/Effects/Damage/GE_ZombieBurst.asset", log, so =>
                so.FindProperty("Modifiers.Array.data[0].AttributeCoefficient").floatValue = -1.5f);

        // El veneno no tiene autor (el zombi ya no está): daño fijo, sin escalar con nadie.
        GameplayEffect poison = MercJamEnemiesSetup.CopyAsset<GameplayEffect>("Assets/Effects/Damage/GE_Class_Damage.asset",
            "Assets/Effects/Damage/GE_ZombiePoison.asset", log, so =>
            {
                SerializedProperty mod = so.FindProperty("Modifiers.Array.data[0]");
                mod.FindPropertyRelative("UseAttributeScaling").boolValue = false;
                mod.FindPropertyRelative("Magnitude").floatValue = -4f;
                mod.FindPropertyRelative("MinMagnitude").floatValue = 0f;
            });

        // --- sus stats ---
        AttributeSetDefinition stats = MercJamEnemiesSetup.CopyAsset<AttributeSetDefinition>(
            "Assets/Attributes/Enemies/ASDef_Enemy.asset", "Assets/Attributes/Enemies/ASDef_Zombie.asset", log, so =>
            {
                SerializedProperty list = so.FindProperty("InitialAttributes");
                for (int i = 0; i < list.arraySize; i++)
                {
                    SerializedProperty a = list.GetArrayElementAtIndex(i);
                    switch ((EAttributeType)a.FindPropertyRelative("Attribute").intValue)
                    {
                        case EAttributeType.Health:   a.FindPropertyRelative("BaseValue").floatValue = 45f;  break;
                        case EAttributeType.MovSpeed: a.FindPropertyRelative("BaseValue").floatValue = 2.6f; break;
                        case EAttributeType.Attack:   a.FindPropertyRelative("BaseValue").floatValue = 10f;  break;
                    }
                }
            });

        // --- la nube de veneno ---
        const string poisonPath = "Assets/Prefabs/Summons/Summon_ZombiePoison.prefab";
        GameObject cloud = MercJamEnemiesSetup.Load<GameObject>(poisonPath);
        if (cloud == null)
        {
            cloud = MercJamEnemiesSetup.MakeVariant(MercJamEnemiesSetup.Load<GameObject>("Assets/Prefabs/Summons/SummonZone_Base.prefab"),
                poisonPath, go =>
                {
                    Entity_Summon summon = go.GetComponent<Entity_Summon>();
                    summon.LifeSeconds   = 6f;
                    summon.MaxPerOwner   = 0;
                    summon.OutlivesOwner = true;
                    summon.IfOwnerDies   = Entity_Summon.EOwnerDeath.Stays;

                    Summon_Zone zone = go.GetComponent<Summon_Zone>();
                    zone.StartActive = true;
                    zone.Seconds     = 0f;
                    zone.Radius      = 3f;
                    zone.TickSeconds = 1f;
                    zone.Targets     = Summon_Zone.ETargets.Enemies;
                    zone.Author      = Summon_Zone.EAuthor.Owner;
                    zone.Effects     = new List<GameplayEffect> { poison };
                    zone.VFX         = MercJamEnemiesSetup.Load<GameObject>("Assets/AssetsExtra/Matthew Guz/Status and Auras FREE/Prefab/Aura Smell.prefab");
                    zone.VfxScale    = 1f;
                });
            log.AppendLine("· Summon_ZombiePoison creado (nube de 3 m, 6 s, 4 de daño por segundo).");
        }

        // --- el zombi ---
        const string zombiePath = "Assets/Prefabs/Enemies/Net_BloatedZombie.prefab";
        GameObject zombie = MercJamEnemiesSetup.Load<GameObject>(zombiePath);
        if (zombie == null)
        {
            zombie = MercJamEnemiesSetup.MakeVariant(MercJamEnemiesSetup.Load<GameObject>("Assets/Prefabs/Enemies/Net_Enemy.prefab"),
                zombiePath, go =>
                {
                    go.GetComponent<AbilitySystemComponent>().CharacterRoleDefinition = stats;

                    MercEnemyAI ai = go.GetComponent<MercEnemyAI>();
                    ai.ExperienceReward = 15f;
                    ai.DeathBurstRadius = 3.5f;
                    ai.DeathBurstEffect = burst;
                    ai.DeathBurstVFX    = MercJamEnemiesSetup.Load<GameObject>(
                        "Assets/AssetsExtra/Hovl Studio/Magic effects pack/Prefabs/Sparks/Sparks explode green.prefab");
                    ai.DeathSummon      = cloud;

                    // Hinchado: el modelo más ancho que alto. Y verde.
                    foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                    {
                        if (r is ParticleSystemRenderer || r.sharedMaterial == null) continue;
                        r.sharedMaterial = MercJamEnemiesSetup.TintedMaterial(r.sharedMaterial, "Net_BloatedZombie", ZombieTint);
                        break;
                    }
                    FloatingVisual model = go.GetComponentInChildren<FloatingVisual>(true);
                    Transform body = model != null ? model.transform : null;
                    if (body != null) body.localScale = Vector3.Scale(body.localScale, new Vector3(1.35f, 1.15f, 1.35f));
                });
            log.AppendLine("· Net_BloatedZombie creado (verde, hinchado, 45 de vida, revienta al morir).");
        }

        MercEnemyCatalog catalog = MercEnemyCatalog.Instance;
        if (catalog != null)
        {
            MercJamEnemiesSetup.SetCatalog(catalog, EMercEnemyType.BloatedZombie, zombie);
            EditorUtility.SetDirty(catalog);
            log.AppendLine("· Catálogo: zombi hinchado.");
        }

        AssetDatabase.SaveAssets();
        MercRegistryTools.RefreshNetworkRegistries();
        log.AppendLine("· Registros de red actualizados.");
        return log.ToString();
    }
}
