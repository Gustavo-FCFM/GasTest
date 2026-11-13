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

        EnsurePoisonDoT(cloud, log);
        EnsureCloudVfx(cloud, log);

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

    // =========================================================
    // LA NUBE QUE SE VE (13 de noviembre de 2026, pedido de Gustavo: "la nube se ve angosta")
    // =========================================================
    //
    // Aura Smell es una columna de olor: no cubría los 3 m de la zona. VFX_PoisonCloud (Art/VFX) es propio:
    //   · Cloud: humo verde bajo que llena todo el círculo (bocanadas grandes que suben despacio y se apagan).
    //   · Rim: volutas más claras en el BORDE, para que se lea dónde empieza el veneno.
    // Mide 1 m de radio a escala 1 (VFX_AreaVisual): la zona lo agranda sola a su radio. Usa el material de humo de
    // Hovl (SmokeFree1); el color va en las partículas.
    private const string CloudVfxPath = "Assets/Art/VFX/VFX_PoisonCloud.prefab";

    [MenuItem("Mercenarios/Enemigos/Zombi: rehacer el VFX de la nube")]
    public static void RebuildCloudVfxMenu()
    {
        var log = new StringBuilder("[Zombi hinchado] ");
        AssetDatabase.DeleteAsset(CloudVfxPath);
        EnsureCloudVfx(MercJamEnemiesSetup.Load<GameObject>("Assets/Prefabs/Summons/Summon_ZombiePoison.prefab"), log);
        Debug.Log(log.ToString());
    }

    private static void EnsureCloudVfx(GameObject cloud, StringBuilder log)
    {
        GameObject vfx = MercJamEnemiesSetup.Load<GameObject>(CloudVfxPath);
        if (vfx == null)
        {
            Material smoke = MercJamEnemiesSetup.Load<Material>("Assets/AssetsExtra/Hovl Studio/Magic effects pack/Materials/SmokeFree1.mat");
            GameObject root = new GameObject("VFX_PoisonCloud");
            try
            {
                VFX_AreaVisual area = root.AddComponent<VFX_AreaVisual>();
                area.RadiusAtScaleOne = 1f;
                area.FadeOutTime = 1.5f;

                // El cuerpo de la nube: llena el círculo.
                ParticleSystem body = NewSystem(root.transform, "Cloud", smoke, 0.12f);
                var main = body.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.04f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.55f, 0.9f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.20f, 0.42f, 0.10f, 0.26f), new Color(0.30f, 0.55f, 0.14f, 0.18f));
                main.maxParticles = 120;
                var emission = body.emission;
                emission.rateOverTime = 16f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });   // que aparezca entera al reventar
                var shape = body.shape;
                shape.radius = 0.75f;
                shape.radiusThickness = 1f;
                var vel = body.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.Local;
                vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
                vel.y = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
                vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
                var size = body.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.25f));

                // El borde: volutas claras que suben donde termina la zona.
                ParticleSystem rim = NewSystem(root.transform, "Rim", smoke, 0.15f);
                var rmain = rim.main;
                rmain.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.8f);
                rmain.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.03f);
                rmain.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.4f);
                rmain.startColor = new ParticleSystem.MinMaxGradient(new Color(0.42f, 0.78f, 0.22f, 0.42f), new Color(0.32f, 0.65f, 0.16f, 0.3f));
                rmain.maxParticles = 120;
                var remission = rim.emission;
                remission.rateOverTime = 28f;
                var rshape = rim.shape;
                rshape.radius = 1f;
                rshape.radiusThickness = 0f;   // solo el borde
                var rvel = rim.velocityOverLifetime;
                rvel.enabled = true;
                rvel.space = ParticleSystemSimulationSpace.Local;
                rvel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
                rvel.y = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
                rvel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

                if (!AssetDatabase.IsValidFolder("Assets/Art/VFX")) AssetDatabase.CreateFolder("Assets/Art", "VFX");
                vfx = PrefabUtility.SaveAsPrefabAsset(root, CloudVfxPath);
                log.AppendLine("· VFX_PoisonCloud creado (humo verde bajo en todo el círculo y volutas en el borde).");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        if (cloud == null || vfx == null) return;
        Summon_Zone zone = cloud.GetComponent<Summon_Zone>();
        if (zone == null || zone.VFX == vfx) return;
        zone.VFX = vfx;
        zone.VfxScale = 1f;
        EditorUtility.SetDirty(zone);
        PrefabUtility.SavePrefabAsset(cloud);
        log.AppendLine("· La nube del zombi usa VFX_PoisonCloud (antes Aura Smell, una columna angosta).");
    }

    // Un sistema de humo plano en el piso: círculo acostado, escala por jerarquía (la zona lo agranda), apagándose
    // al principio y al final de su vida, girando despacio.
    private static ParticleSystem NewSystem(Transform parent, string name, Material material, float height)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, height, 0f);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 2f;
        main.loop = true;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.playOnAwake = true;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.rotation = new Vector3(90f, 0f, 0f);   // el círculo acostado en el piso

        var color = ps.colorOverLifetime;
        color.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.65f), new GradientAlphaKey(0f, 1f) });
        color.color = fade;

        var spin = ps.rotationOverLifetime;
        spin.enabled = true;
        spin.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = material;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    // El veneno como DEBUFF (12 de noviembre de 2026, pedido de Gustavo): cada segundo, el 5 % de la vida
    // FALTANTE (mínimo 1) por 10 s. No se acumula: estar en la nube lo refresca cada segundo, y al salir
    // sigue los 10 s. Reemplaza a los 4 de daño fijo por segundo de la nube.
    private static void EnsurePoisonDoT(GameObject cloud, StringBuilder log)
    {
        GameplayEffect dot = MercJamEnemiesSetup.CopyAsset<GameplayEffect>("Assets/Effects/Damage/GE_ZombiePoison.asset",
            "Assets/Effects/Debuffs/GE_ZombiePoisonDoT.asset", log, so =>
            {
                so.FindProperty("Duration").floatValue = 10f;
                so.FindProperty("Period").floatValue   = 1f;
                so.FindProperty("StackingPolicy").enumValueIndex = (int)GameplayEffect.EStackingType.Refresh;
                so.FindProperty("EffectType").enumValueIndex     = (int)GameplayEffect.EEffectType.Debuff;
                so.FindProperty("Icon").objectReferenceValue =
                    MercJamEnemiesSetup.Load<Sprite>("Assets/Art/Icons/Abilities/Conditions/Bleeding_Icon.png");
                so.FindProperty("TargetVFX").objectReferenceValue = MercJamEnemiesSetup.Load<GameObject>(
                    "Assets/AssetsExtra/Matthew Guz/Status and Auras FREE/Prefab/Aura Smell.prefab");
                so.FindProperty("TargetVFXLoop").boolValue = true;

                SerializedProperty mod = so.FindProperty("Modifiers.Array.data[0]");
                mod.FindPropertyRelative("Magnitude").floatValue = 0f;
                mod.FindPropertyRelative("UseAttributeScaling").boolValue = false;
                mod.FindPropertyRelative("UseTargetHealthScaling").boolValue = true;
                mod.FindPropertyRelative("TargetHealthMode").enumValueIndex = (int)Modifier.ETargetHealthMode.MissingHealth;
                mod.FindPropertyRelative("TargetHealthCoefficient").floatValue = -0.05f;   // 5 % (12 de noviembre: subido de 1 %)
                mod.FindPropertyRelative("MinMagnitude").floatValue = 1f;   // al menos 1 de daño (a vida llena, 0 faltante)
            });
        if (dot == null || cloud == null) return;

        Summon_Zone zone = cloud.GetComponent<Summon_Zone>();
        if (zone == null || (zone.Effects.Count == 1 && zone.Effects[0] == dot)) return;

        zone.Effects = new List<GameplayEffect> { dot };
        EditorUtility.SetDirty(zone);
        PrefabUtility.SavePrefabAsset(cloud);
        log.AppendLine("· La nube ahora aplica GE_ZombiePoisonDoT (5 % de la vida faltante por segundo, 10 s, se refresca).");
    }
}
