using System.IO;
using System.Reflection;
using System.Text;
using FishNet.Object;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// ============================================================
// MercJamEnemiesSetup
//
// Herramienta de UNA VEZ (12 de noviembre de 2026, pedido de Gustavo): pasa a red a los enemigos de la
// jam que faltaban y rehace a las Banshees (antes "jefes"). Después todo se edita a mano.
//   · Mago de hielo (Net_IceMage): el mismo daño que la bola de fuego, pero en vez de quemar ralentiza
//     (GE_FrostSlow, el del Mago); proyectil PF_FrostBolt.
//   · Mago de roca (Net_RockMage): poco daño (0.4 × ataque), la roca sale más tarde (1 s de preparación)
//     y más lenta, y aturde 1 s (GE_Stun). Proyectil propio, PF_RockProjectile.
//   · Banshee de curación (Net_Boss, el jefe de siempre): cura a los NPCs de alrededor y daña alrededor;
//     ya no les da el buff de ataque y velocidad ni ralentiza.
//   · Banshee de daño (Net_DamageBanshee): les sube el ataque a los NPCs de alrededor y daña alrededor.
//   · Banshee del silencio (Net_SilenceBanshee): grita y silencia y daña a los de alrededor.
// Las habilidades y efectos nuevos van en GameplayAbilities/Enemies y Effects; los materiales teñidos,
// en Art/Materials/Enemies. Agrega los tres tipos nuevos al catálogo y actualiza los registros de red.
// NO toca la escena: en qué campamentos salen se elige a mano.
// Lo que ya existe no lo vuelve a crear.
// ============================================================
public static class MercJamEnemiesSetup
{
    private const string EnemyAbilities = "Assets/GameplayAbilities/Enemies";
    private const string EnemyPrefabs   = "Assets/Prefabs/Enemies";
    private const string EnemyMaterials = "Assets/Art/Materials/Enemies";
    private const string Projectiles    = "Assets/Prefabs/Projectiles";
    private const string HovlAoE        = "Assets/AssetsExtra/Hovl Studio/Magic effects pack/Prefabs/AoE effects/";
    private const string HovlSparks     = "Assets/AssetsExtra/Hovl Studio/Magic effects pack/Prefabs/Sparks/";

    // Tintes (saturados: a Gustavo no le gustan los colores deslavados).
    private static readonly Color IceTint     = new Color(0.35f, 0.70f, 1.00f);
    private static readonly Color RockTint    = new Color(0.62f, 0.46f, 0.30f);
    private static readonly Color DamageTint  = new Color(1.00f, 0.35f, 0.28f);
    private static readonly Color SilenceTint = new Color(0.62f, 0.38f, 1.00f);

    [MenuItem("Mercenarios/Enemigos/Armar los magos de hielo y roca y las Banshees")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        var log = new StringBuilder("[Enemigos de la jam] Armado:\n");
        EnsureFolder(EnemyMaterials);

        // --- efectos ---
        GameplayEffect classDamage = Load<GameplayEffect>("Assets/Effects/Damage/GE_Class_Damage.asset");
        GameplayEffect frostSlow   = Load<GameplayEffect>("Assets/Effects/CC/GE_FrostSlow.asset");
        GameplayEffect stun        = Load<GameplayEffect>("Assets/Effects/CC/GE_Stun.asset");
        GameplayEffect bossHeal    = Load<GameplayEffect>("Assets/48toPlay/GE_BossHeal.asset");

        GameplayEffect rockDamage = CopyAsset<GameplayEffect>("Assets/Effects/Damage/GE_Class_Damage.asset",
            "Assets/Effects/Damage/GE_RockMageDamage.asset", log, so =>
                so.FindProperty("Modifiers.Array.data[0].AttributeCoefficient").floatValue = -0.4f);

        GameplayEffect wailDamage = CopyAsset<GameplayEffect>("Assets/Effects/Damage/GE_Class_Damage.asset",
            "Assets/Effects/Damage/GE_BansheeWailDamage.asset", log, so =>
                so.FindProperty("Modifiers.Array.data[0].AttributeCoefficient").floatValue = -0.6f);

        // El buff de la Banshee de daño: el de los jefes de la jam, solo con el +5 de ataque y el aura roja.
        GameplayEffect fury = CopyAsset<GameplayEffect>("Assets/48toPlay/GE_BossBuffs.asset",
            "Assets/Effects/Buffs/GE_BansheeFury.asset", log, so =>
            {
                SerializedProperty mods = so.FindProperty("Modifiers");
                for (int i = mods.arraySize - 1; i >= 0; i--)
                    if (mods.GetArrayElementAtIndex(i).FindPropertyRelative("Attribute").intValue != (int)EAttributeType.Attack)
                        mods.DeleteArrayElementAtIndex(i);
                so.FindProperty("TargetVFX").objectReferenceValue = Load<GameObject>("Assets/Art/VFX/RageBuff.prefab");
            });

        // El silencio de la Banshee: un debuff de 2 s que da State_Silenced (el ícono, el de la Zona de la verdad).
        GameplayEffect silence = CopyAsset<GameplayEffect>("Assets/Effects/CC/GE_Stun.asset",
            "Assets/Effects/CC/GE_BansheeSilence.asset", log, so =>
            {
                so.FindProperty("Duration").floatValue = 2f;
                so.FindProperty("TargetVFX").objectReferenceValue = null;
                so.FindProperty("Icon").objectReferenceValue =
                    Load<GameplayEffect>("Assets/Effects/Debuffs/GE_ZoneOfTruth.asset")?.Icon;
                SerializedProperty tags = so.FindProperty("GrantedTags");
                tags.arraySize = 1;
                tags.GetArrayElementAtIndex(0).intValue = (int)EGameplayTag.State_Silenced;
            });

        // --- el proyectil de roca ---
        GameObject rockProjectile = BuildRockProjectile(log);

        // --- habilidades ---
        const string fireball = EnemyAbilities + "/GA_FireballEnemy.asset";

        GameplayAbility iceball = CopyAsset<GameplayAbility>(fireball, EnemyAbilities + "/GA_IceballEnemy.asset", log, so =>
        {
            Names(so, "Iceball", "Bola de hielo");
            SetEffects(so, (classDamage, 0), (frostSlow, 0));
            so.FindProperty("ProjectilePrefab").objectReferenceValue = Load<GameObject>(Projectiles + "/PF_FrostBolt.prefab");
            SetFirstVisual(so, Load<GameObject>(HovlSparks + "Sparks explode blue.prefab"));
        });

        GameplayAbility rock = CopyAsset<GameplayAbility>(fireball, EnemyAbilities + "/GA_RockEnemy.asset", log, so =>
        {
            Names(so, "Rock Throw", "Roca");
            SetEffects(so, (rockDamage, 0), (stun, 0));
            so.FindProperty("ProjectilePrefab").objectReferenceValue = rockProjectile;
            so.FindProperty("SpawnDelay").floatValue  = 1f;    // tarda más en salir
            so.FindProperty("LaunchForce").floatValue = 26f;   // y viaja más lenta: se puede esquivar
            SetFirstVisual(so, Load<GameObject>(HovlAoE + "Ground AOE explosion.prefab"));
        });

        // Banshee de curación: el aura cura (sin el buff) y la zona daña (sin la lentitud).
        GameplayAbility healAura = CopyAsset<GameplayAbility>("Assets/48toPlay/GA_BossAura.asset",
            EnemyAbilities + "/GA_BansheeHealAura.asset", log, so =>
            {
                Names(so, "Healing Aura", "Aura de curación");
                SetEffects(so, (bossHeal, 1));
            });
        GameplayAbility healBlast = CopyAsset<GameplayAbility>("Assets/48toPlay/GA_IceAoE.asset",
            EnemyAbilities + "/GA_BansheeHealBlast.asset", log, so =>
            {
                Names(so, "Banshee Blast", "Estallido de la Banshee");
                SetEffects(so, (classDamage, 0));
                SetFirstVisual(so, Load<GameObject>(HovlAoE + "Smoke AOE explosion.prefab"));
            });
        GameplayAbility healing = BuildSequence("GA_BansheeHealing", "Healing Banshee", "Banshee de curación", healAura, healBlast, log);

        // Banshee de daño: el aura potencia a los NPCs y la zona daña.
        GameplayAbility furyAura = CopyAsset<GameplayAbility>("Assets/48toPlay/GA_BossAura.asset",
            EnemyAbilities + "/GA_BansheeFuryAura.asset", log, so =>
            {
                Names(so, "Fury Aura", "Aura de furia");
                SetEffects(so, (fury, 1));
                SetFirstVisual(so, Load<GameObject>("Assets/AssetsExtra/Hovl Studio/Magic effects pack/Prefabs/Magic circles/Magic circle.prefab"));
            });
        GameplayAbility damageBlast = CopyAsset<GameplayAbility>("Assets/48toPlay/GA_IceAoE.asset",
            EnemyAbilities + "/GA_BansheeDamageBlast.asset", log, so =>
            {
                Names(so, "Fury Blast", "Estallido de furia");
                SetEffects(so, (classDamage, 0));
                SetFirstVisual(so, Load<GameObject>(HovlAoE + "Red energy explosion.prefab"));
            });
        GameplayAbility furySequence = BuildSequence("GA_BansheeFury", "Damage Banshee", "Banshee de daño", furyAura, damageBlast, log);

        // Banshee del silencio: un grito alrededor que daña y silencia.
        GameplayAbility wail = CopyAsset<GameplayAbility>("Assets/48toPlay/GA_IceAoE.asset",
            EnemyAbilities + "/GA_BansheeWail.asset", log, so =>
            {
                Names(so, "Silencing Wail", "Lamento silenciador");
                SetEffects(so, (wailDamage, 0), (silence, 0));
                so.FindProperty("Radius").floatValue = 7f;
                SetFirstVisual(so, Load<GameObject>(HovlAoE + "Smoke AOE explosion.prefab"));
            });

        // --- prefabs ---
        GameObject mage = Load<GameObject>(EnemyPrefabs + "/Net_Mage.prefab");
        GameObject boss = Load<GameObject>(EnemyPrefabs + "/Net_Boss.prefab");

        GameObject iceMage = BuildVariant(mage, "Net_IceMage", log, ai =>
        {
            ai.AbilityToUse = iceball;
            ai.ExperienceReward = 40f;
        }, IceTint);
        GameObject rockMage = BuildVariant(mage, "Net_RockMage", log, ai =>
        {
            ai.AbilityToUse = rock;
            ai.AttackCooldown = 3f;     // aturde: que no lo encadene
            ai.ExperienceReward = 40f;
        }, RockTint);
        GameObject damageBanshee = BuildVariant(boss, "Net_DamageBanshee", log, ai => ai.AbilityToUse = furySequence, DamageTint);
        GameObject silenceBanshee = BuildVariant(boss, "Net_SilenceBanshee", log, ai =>
        {
            ai.AbilityToUse   = wail;
            ai.AttackRange    = 7f;    // se acerca para gritar
            ai.AttackCooldown = 6f;
            ai.KeepDistance   = 0f;
        }, SilenceTint);

        // La Banshee de curación es el jefe de siempre con la secuencia nueva.
        MercEnemyAI bossAi = boss.GetComponent<MercEnemyAI>();
        if (bossAi.AbilityToUse != healing)
        {
            bossAi.AbilityToUse = healing;
            EditorUtility.SetDirty(bossAi);
            PrefabUtility.SavePrefabAsset(boss);
            log.AppendLine("· Net_Boss (Banshee de curación) usa GA_BansheeHealing: cura NPCs y daña alrededor.");
        }

        // --- catálogo ---
        MercEnemyCatalog catalog = MercEnemyCatalog.Instance;
        if (catalog != null)
        {
            SetCatalog(catalog, EMercEnemyType.IceMage, iceMage);
            SetCatalog(catalog, EMercEnemyType.RockMage, rockMage);
            SetCatalog(catalog, EMercEnemyType.DamageBanshee, damageBanshee);
            SetCatalog(catalog, EMercEnemyType.SilenceBanshee, silenceBanshee);
            EditorUtility.SetDirty(catalog);
            log.AppendLine("· Catálogo: mago de hielo, mago de roca, Banshee de daño y Banshee del silencio.");
        }

        AssetDatabase.SaveAssets();
        MercRegistryTools.RefreshNetworkRegistries();
        log.AppendLine("· Registros de red actualizados.");
        return log.ToString();
    }

    // =========================================================
    // PIEZAS
    // =========================================================

    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

    // Copia un asset y lo configura; si la copia ya existe, la devuelve sin tocarla.
    private static T CopyAsset<T>(string from, string to, StringBuilder log, System.Action<SerializedObject> setup) where T : Object
    {
        T existing = Load<T>(to);
        if (existing != null) return existing;

        if (!AssetDatabase.CopyAsset(from, to))
        {
            log.AppendLine($"  ¡Ojo! No pude copiar {from}.");
            return null;
        }
        T copy = Load<T>(to);
        var so = new SerializedObject(copy);
        setup(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(copy);
        log.AppendLine($"· {Path.GetFileNameWithoutExtension(to)} creado.");
        return copy;
    }

    private static void Names(SerializedObject so, string english, string spanish)
    {
        so.FindProperty("AbilityName").stringValue   = english;
        so.FindProperty("AbilityNameEs").stringValue = spanish;
    }

    // La lista "Efectos" de la habilidad: cada uno al golpear, a quién (0 = enemigos, 1 = aliados).
    private static void SetEffects(SerializedObject so, params (GameplayEffect effect, int applyTo)[] effects)
    {
        SerializedProperty list = so.FindProperty("Effects");
        while (list.arraySize > effects.Length) list.DeleteArrayElementAtIndex(list.arraySize - 1);
        int template = list.arraySize - 1;
        for (int i = 0; i < effects.Length; i++)
        {
            if (i >= list.arraySize)
            {
                if (template >= 0) list.InsertArrayElementAtIndex(template); else list.arraySize++;
            }
            SerializedProperty e = list.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("When").intValue = 0;
            e.FindPropertyRelative("ApplyTo").intValue = effects[i].applyTo;
            e.FindPropertyRelative("Effect").objectReferenceValue = effects[i].effect;
            e.FindPropertyRelative("OnlyIfTargetHas").intValue = 0;
        }
    }

    private static void SetFirstVisual(SerializedObject so, GameObject vfx)
    {
        SerializedProperty visuals = so.FindProperty("Visuals");
        if (vfx == null || visuals == null || visuals.arraySize == 0) return;
        visuals.GetArrayElementAtIndex(0).FindPropertyRelative("VFXPrefab").objectReferenceValue = vfx;
    }

    // Una Banshee: el aura (5 s), espera 5 s y la zona. Como la secuencia del jefe de la jam.
    private static GameplayAbility BuildSequence(string file, string english, string spanish,
                                                 GameplayAbility aura, GameplayAbility blast, StringBuilder log)
    {
        return CopyAsset<GameplayAbility>("Assets/48toPlay/GA_BossAbility.asset", $"{EnemyAbilities}/{file}.asset", log, so =>
        {
            Names(so, english, spanish);
            SerializedProperty seq = so.FindProperty("Sequence");
            seq.GetArrayElementAtIndex(0).FindPropertyRelative("AbilityToCast").objectReferenceValue = aura;
            seq.GetArrayElementAtIndex(1).FindPropertyRelative("AbilityToCast").objectReferenceValue = blast;
        });
    }

    // La roca: la bola de fuego con la piedra del mapa, un poco más grande.
    private static GameObject BuildRockProjectile(StringBuilder log)
    {
        string path = Projectiles + "/PF_RockProjectile.prefab";
        GameObject existing = Load<GameObject>(path);
        if (existing != null) return existing;

        Material stone = Load<Material>("Assets/Art/Materials/Arena/Mat_ArenaStone.mat");
        GameObject result = MakeVariant(Load<GameObject>(Projectiles + "/PF_Fireball.prefab"), path, go =>
        {
            foreach (MeshRenderer r in go.GetComponentsInChildren<MeshRenderer>(true))
                if (stone != null) r.sharedMaterial = stone;
            foreach (Transform child in go.transform)
                if (child.GetComponent<MeshRenderer>() != null) child.localScale *= 1.4f;
        });
        log.AppendLine("· PF_RockProjectile creado (la bola de fuego con piedra).");
        return result;
    }

    private static GameObject BuildVariant(GameObject source, string name, StringBuilder log,
                                           System.Action<MercEnemyAI> setup, Color tint)
    {
        string path = $"{EnemyPrefabs}/{name}.prefab";
        GameObject existing = Load<GameObject>(path);
        if (existing != null)
        {
            log.AppendLine($"· {name}: ya existía.");
            return existing;
        }

        GameObject result = MakeVariant(source, path, go =>
        {
            setup(go.GetComponent<MercEnemyAI>());

            // El cuerpo, teñido (el primer renderer: en la Banshee las cadenas quedan como están).
            Renderer body = null;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                if (!(r is ParticleSystemRenderer)) { body = r; break; }
            if (body != null && body.sharedMaterial != null)
                body.sharedMaterial = TintedMaterial(body.sharedMaterial, name, tint);
        });
        log.AppendLine($"· {name} creado.");
        return result;
    }

    // Variante de prefab, armada en una escena de vista previa (la escena abierta no se toca).
    private static GameObject MakeVariant(GameObject source, string path, System.Action<GameObject> setup)
    {
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, preview);
            instance.name = Path.GetFileNameWithoutExtension(path);
            setup(instance);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);

            // Un prefab no es un objeto de escena: su SceneId tiene que quedar en 0.
            NetworkObject nob = prefab != null ? prefab.GetComponent<NetworkObject>() : null;
            FieldInfo sceneId = typeof(NetworkObject).GetField("SceneId", BindingFlags.Instance | BindingFlags.NonPublic);
            if (nob != null && sceneId != null && (ulong)sceneId.GetValue(nob) != 0UL)
            {
                sceneId.SetValue(nob, 0UL);
                EditorUtility.SetDirty(nob);
                PrefabUtility.SavePrefabAsset(prefab);
            }
            return prefab;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    private static Material TintedMaterial(Material source, string name, Color tint)
    {
        string path = $"{EnemyMaterials}/Mat_{name.Replace("Net_", "")}.mat";
        Material existing = Load<Material>(path);
        if (existing != null) return existing;

        var mat = new Material(source) { name = Path.GetFileNameWithoutExtension(path) };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
        if (mat.HasProperty("_Color"))     mat.SetColor("_Color", tint);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static void SetCatalog(MercEnemyCatalog catalog, EMercEnemyType type, GameObject prefab)
    {
        if (prefab == null) return;
        for (int i = 0; i < catalog.Enemies.Count; i++)
            if (catalog.Enemies[i].Type == type)
            {
                catalog.Enemies[i] = new MercEnemyCatalog.Entry { Type = type, Prefab = prefab };
                return;
            }
        catalog.Enemies.Add(new MercEnemyCatalog.Entry { Type = type, Prefab = prefab });
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
