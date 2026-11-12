using System.Text;
using UnityEditor;
using UnityEngine;

// ============================================================
// MercSkeletonSetup
//
// Herramienta de UNA VEZ (12 de noviembre de 2026, pedido de Gustavo): el ESQUELETO CON ESCUDO. Es el
// fantasma, gris, con un escudo chico al frente. Ataca de cerca en un cono muy amplio (180° a 2.5 m) y
// después SE CUBRE: levanta el escudo hacia quien lo estaba atacando y se queda así, quieto, 3 s. De
// frente no le entra nada (ni proyectiles); hay que rodearlo o pegarle con área por detrás.
//   · Net_ShieldSkeleton: variante de Net_Enemy (ASDef_Skeleton: 40 de vida, 11 de ataque, 3.2 m/s).
//   · GA_SkeletonSweep: el golpe del Bárbaro (GA_AxeAttack, ya trae el momento del golpe) a 180° y 2.5 m.
//   · GE_SkeletonBlock: 3 s de Status_Blocking, que levanta su barrera (Entity_ShieldBarrier, la del
//     Paladín, con el panel de su escudo como aviso): frena el 100 % de lo que la cruce, sin energía.
//   · El bloqueo lo hace MercEnemyAI ("Bloqueo después de atacar").
// Lo agrega al catálogo y actualiza los registros de red. No toca la escena.
// Usa las piezas de MercJamEnemiesSetup. Lo que ya existe no lo vuelve a crear.
// ============================================================
public static class MercSkeletonSetup
{
    private static readonly Color SkeletonTint = new Color(0.55f, 0.55f, 0.58f);

    [MenuItem("Mercenarios/Enemigos/Armar el esqueleto con escudo")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        var log = new StringBuilder("[Esqueleto con escudo] Armado:\n");

        GameplayEffect classDamage = MercJamEnemiesSetup.Load<GameplayEffect>("Assets/Effects/Damage/GE_Class_Damage.asset");

        GameplayEffect block = MercJamEnemiesSetup.CopyAsset<GameplayEffect>("Assets/Effects/CC/GE_Stun.asset",
            "Assets/Effects/Buffs/GE_SkeletonBlock.asset", log, so =>
            {
                so.FindProperty("Duration").floatValue = 3f;
                so.FindProperty("EffectType").enumValueIndex = (int)GameplayEffect.EEffectType.Hidden;
                so.FindProperty("IsCrowdControl").boolValue = false;
                so.FindProperty("TargetVFX").objectReferenceValue = null;
                so.FindProperty("Icon").objectReferenceValue = null;
                SerializedProperty tags = so.FindProperty("GrantedTags");
                tags.arraySize = 1;
                tags.GetArrayElementAtIndex(0).intValue = (int)EGameplayTag.Status_Blocking;
            });

        GameplayAbility sweep = MercJamEnemiesSetup.CopyAsset<GameplayAbility>("Assets/GameplayAbilities/Barbarian/GA_AxeAttack.asset",
            "Assets/GameplayAbilities/Enemies/GA_SkeletonSweep.asset", log, so =>
            {
                so.FindProperty("AbilityName").stringValue   = "Skeleton Sweep";
                so.FindProperty("AbilityNameEs").stringValue = "Barrido del esqueleto";
                so.FindProperty("Range").floatValue     = 2.5f;
                so.FindProperty("ConeAngle").floatValue = 180f;
                so.FindProperty("ImpactZone").objectReferenceValue = null;
                SerializedProperty effects = so.FindProperty("Effects");
                while (effects.arraySize > 1) effects.DeleteArrayElementAtIndex(effects.arraySize - 1);
                if (effects.arraySize == 0) effects.arraySize = 1;
                SerializedProperty e = effects.GetArrayElementAtIndex(0);
                e.FindPropertyRelative("When").intValue = 0;
                e.FindPropertyRelative("ApplyTo").intValue = 0;
                e.FindPropertyRelative("Effect").objectReferenceValue = classDamage;
                e.FindPropertyRelative("OnlyIfTargetHas").intValue = 0;
            });

        AttributeSetDefinition stats = MercJamEnemiesSetup.CopyAsset<AttributeSetDefinition>(
            "Assets/Attributes/Enemies/ASDef_Enemy.asset", "Assets/Attributes/Enemies/ASDef_Skeleton.asset", log, so =>
            {
                SerializedProperty list = so.FindProperty("InitialAttributes");
                for (int i = 0; i < list.arraySize; i++)
                {
                    SerializedProperty a = list.GetArrayElementAtIndex(i);
                    switch ((EAttributeType)a.FindPropertyRelative("Attribute").intValue)
                    {
                        case EAttributeType.Health:   a.FindPropertyRelative("BaseValue").floatValue = 40f;  break;
                        case EAttributeType.MovSpeed: a.FindPropertyRelative("BaseValue").floatValue = 3.2f; break;
                        case EAttributeType.Attack:   a.FindPropertyRelative("BaseValue").floatValue = 11f;  break;
                    }
                }
            });

        const string path = "Assets/Prefabs/Enemies/Net_ShieldSkeleton.prefab";
        GameObject skeleton = MercJamEnemiesSetup.Load<GameObject>(path);
        if (skeleton == null)
        {
            GameObject shieldModel = MercJamEnemiesSetup.Load<GameObject>("Assets/Art/Models/Weapons/shield 1.prefab");
            GameObject paladin = MercJamEnemiesSetup.Load<GameObject>("Assets/GameplayAbilities/Paladin/PaladinBehaviours.prefab");
            Entity_ShieldBarrier paladinBarrier = paladin != null ? paladin.GetComponentInChildren<Entity_ShieldBarrier>(true) : null;

            skeleton = MercJamEnemiesSetup.MakeVariant(MercJamEnemiesSetup.Load<GameObject>("Assets/Prefabs/Enemies/Net_Enemy.prefab"), path, go =>
            {
                go.GetComponent<AbilitySystemComponent>().CharacterRoleDefinition = stats;

                MercEnemyAI ai = go.GetComponent<MercEnemyAI>();
                ai.AbilityToUse      = sweep;
                ai.AttackRange       = 2.3f;
                ai.AttackCooldown    = 4f;    // 3 s cubriéndose + 1
                ai.BlockAfterAttack  = block;
                ai.BlockSeconds      = 3f;
                ai.ExperienceReward  = 15f;

                foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer || r.sharedMaterial == null) continue;
                    r.sharedMaterial = MercJamEnemiesSetup.TintedMaterial(r.sharedMaterial, "Net_ShieldSkeleton", SkeletonTint);
                    break;
                }

                // El escudo, al frente y un poco a un lado, más chico que el del Paladín. Cuelga del modelo:
                // flota con él.
                FloatingVisual model = go.GetComponentInChildren<FloatingVisual>(true);
                Transform holder = new GameObject("ShieldHolder").transform;
                holder.SetParent(go.transform, false);
                holder.localPosition = new Vector3(0.2f, 0.75f, 0.55f);
                if (model != null) holder.SetParent(model.transform, true);
                if (shieldModel != null)
                {
                    var shield = (GameObject)PrefabUtility.InstantiatePrefab(shieldModel, holder);
                    shield.transform.localPosition = Vector3.zero;
                    shield.transform.localRotation = Quaternion.identity;
                    shield.transform.localScale = Vector3.one * 0.7f;
                    shield.transform.localScale = new Vector3(shield.transform.localScale.x / holder.lossyScale.x,
                                                              shield.transform.localScale.y / holder.lossyScale.y,
                                                              shield.transform.localScale.z / holder.lossyScale.z);
                }

                // La barrera: la del Paladín, con el frente del esqueleto.
                if (paladinBarrier != null)
                {
                    GameObject barrier = Object.Instantiate(paladinBarrier.gameObject, go.transform);
                    barrier.name = "ShieldBarrier";
                    barrier.transform.localPosition = new Vector3(0f, 0.9f, 0.7f);
                    barrier.transform.localRotation = Quaternion.identity;
                    var b = barrier.GetComponent<Entity_ShieldBarrier>();
                    b.ActiveTag                  = EGameplayTag.Status_Blocking;
                    b.DamageReduction            = 1f;
                    b.EnergyPerDamageBlocked     = 0f;
                    b.EnergyPerProjectileBlocked = 0f;
                    b.ParryWindow                = 0f;
                    b.UltimateSecondsPerBlock    = 0f;
                    b.ReflectMeleeFraction       = 0f;
                    b.OnBlockAttackerEffect      = null;
                    b.Omnidirectional            = false;
                    b.FollowAimPitch             = false;
                    b.ProtectAllies              = false;
                    if (barrier.GetComponent<Collider>() is BoxCollider box)
                    {
                        box.center = Vector3.zero;
                        box.size   = new Vector3(1.8f, 1.9f, 0.25f);
                    }
                    foreach (Transform child in barrier.transform)
                        child.localScale = new Vector3(1.8f, 1.9f, 0.25f);
                }
            });
            log.AppendLine(paladinBarrier != null
                ? "· Net_ShieldSkeleton creado (gris, escudo al frente, barrera del Paladín)."
                : "  ¡Ojo! Net_ShieldSkeleton sin barrera: no encontré la del Paladín.");
        }

        EnsureSword(path, log);

        MercEnemyCatalog catalog = MercEnemyCatalog.Instance;
        if (catalog != null)
        {
            MercJamEnemiesSetup.SetCatalog(catalog, EMercEnemyType.ShieldSkeleton, skeleton);
            EditorUtility.SetDirty(catalog);
            log.AppendLine("· Catálogo: esqueleto con escudo.");
        }

        AssetDatabase.SaveAssets();
        MercRegistryTools.RefreshNetworkRegistries();
        log.AppendLine("· Registros de red actualizados.");
        return log.ToString();
    }

    // La espadita (12 de noviembre de 2026): la ShortSword, del lado contrario al escudo, inclinada
    // hacia adelante. Cuelga del modelo, como el escudo: flota con él. Solo adorno.
    private static void EnsureSword(string prefabPath, StringBuilder log)
    {
        GameObject swordModel = MercJamEnemiesSetup.Load<GameObject>("Assets/Art/Models/Weapons/ShortSword.prefab");
        if (swordModel == null) { log.AppendLine("  ¡Ojo! No encontré ShortSword: el esqueleto queda sin espada."); return; }

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            if (root.transform.Find("SwordHolder") != null || root.GetComponentInChildren<FloatingVisual>(true)?.transform.Find("SwordHolder") != null)
            {
                log.AppendLine("· El esqueleto ya tenía su espada.");
                return;
            }

            FloatingVisual model = root.GetComponentInChildren<FloatingVisual>(true);
            Transform holder = new GameObject("SwordHolder").transform;
            holder.SetParent(root.transform, false);
            holder.localPosition = new Vector3(-0.78f, 0.75f, 0.35f);   // afuera del cuerpo (mide 1.7 m de ancho)
            holder.localRotation = Quaternion.Euler(35f, 0f, 0f);   // la punta hacia adelante
            if (model != null) holder.SetParent(model.transform, true);

            var sword = (GameObject)PrefabUtility.InstantiatePrefab(swordModel, holder);
            sword.transform.localPosition = Vector3.zero;
            sword.transform.localRotation = Quaternion.identity;
            Vector3 lossy = holder.lossyScale;
            sword.transform.localScale = new Vector3(1.1f / lossy.x, 1.1f / lossy.y, 1.1f / lossy.z);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            log.AppendLine("· Espadita agregada al esqueleto (ShortSword).");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
