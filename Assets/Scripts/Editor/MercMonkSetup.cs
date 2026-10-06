using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ============================================================
// MercMonkSetup  (herramienta de un solo uso: correr y borrar)
//
// El kit base del Monje, con lo que pidió Gustavo el 6 de octubre de 2026. Los números que
// no vinieron los puso Claude (⚙ en PENDIENTES). Se puede correr desde el menú o en batch
// con Unity cerrado:
//   Unity.exe -batchmode -quit -nographics -projectPath ... -executeMethod MercMonkSetup.Create
//
//   · Stats (ASDef_Monk): 80 de vida (+50 por nivel), 4 de ataque (+1 por nivel), 4 de
//     armadura, 50 de energía, 1 s entre ataques y la velocidad del Pícaro.
//   · Pasiva, Artes marciales (MonkBehaviours → MartialArtsPassive): cada ataque que pega
//     suma GE_MartialArts: 5 % de velocidad de ataque (⚙ y 5 % de movimiento) por
//     acumulación, hasta 4, 8 s.
//   · Q, Ki (GA_Ki): 3 cargas (⚙ 8 s cada una). Deja GE_Ki (Status_Ki, ⚙ 10 s): la
//     PRÓXIMA acción sale con Ki y lo gasta.
//   · Clic izq. (GA_MonkAttack, un GA_TagSwitch): dos puñetazos (GA_MonkStrikes). Con Ki,
//     la Ráfaga de golpes (GA_FlurryOfBlows): primero las 4 acumulaciones de una, después
//     los dos puñetazos.
//   · Clic der. (GA_MonkGuard, un GA_HoldTagSwitch): el bloqueo frontal con menos
//     reducción que los demás (⚙ 40 %), por daño frenado. Con Ki, la Defensa paciente
//     (GA_PatientDefense): 6 s de una cápsula alrededor que frena el 40 % desde cualquier
//     lado gastando la misma energía, sin mantener el botón (puede atacar a la vez).
//   · Shift (GA_MonkKick, un GA_TagSwitch): la Patada voladora (GA_FlyingKick, una
//     GA_RushAttack larga y lenta: ⚙ 14 m a 9 m/s; izquierda/derecha la tuercen, volver a
//     apretar la corta; frena en el primero). Con Ki, la Patada del dragón (GA_DragonKick):
//     ⚙ 16 m a 16 m/s y aturde al primero (GE_Stun).
//   · Lo agrega al jugador (MainBaseClasses) y a la sala de las escenas del modo, y
//     actualiza los registros de red.
//
// Busca todo por NOMBRE y tipo. Lo que ya exista se deja como está.
// ============================================================
public static class MercMonkSetup
{
    private const string Tag = "[Monje]";

    [MenuItem("Mercenarios/Crear el Monje (kit base, una sola vez)", false, 8)]
    public static void Create()
    {
        GA_ComboSequence comboTemplate = Find<GA_ComboSequence>("GA_ClericStaffCombo");
        GA_ConeAttack    coneTemplate  = Find<GA_ConeAttack>("GA_PaladinPrimaryAttack");
        GA_ShieldBlock   blockTemplate = Find<GA_ShieldBlock>("GA_FighterShieldBlock") ?? Find<GA_ShieldBlock>("GA_PaladinShieldBlock");
        GA_Dash          dashTemplate  = Find<GA_Dash>("GA_FighterChargeDefensive");
        GameplayEffect stun        = Find<GameplayEffect>("GE_Stun");
        GameplayEffect classDmg    = Find<GameplayEffect>("GE_Class_Damage");
        GameplayEffect cdSpecial   = Find<GameplayEffect>("GE_Cooldown_Special");
        GameplayEffect cdMove      = Find<GameplayEffect>("GE_Cooldown_MoveAbility");
        GameplayEffect energyRegen = Find<GameplayEffect>("GE_EnergyRegen");
        GameplayEffect blessing    = Find<GameplayEffect>("GE_Blessing");
        AttributeSetDefinition rogueStats = Find<AttributeSetDefinition>("ASDef_Rogue");
        CharacterClassDefinition rogue    = Find<CharacterClassDefinition>("Class_Rogue");
        GameObject paladinBehaviours      = Find<GameObject>("PaladinBehaviours");
        AnimationClip punchRight = FindClipInModel("HumanM@AttackPunch01_R");
        AnimationClip punchLeft  = FindClipInModel("HumanM@AttackPunch01_L");
        AnimationClip kickClip   = FindClipInModel("HumanM@AttackKick01_R");

        if (comboTemplate == null || coneTemplate == null || blockTemplate == null || dashTemplate == null ||
            stun == null || classDmg == null || cdSpecial == null || cdMove == null || energyRegen == null ||
            blessing == null || rogueStats == null || rogue == null || paladinBehaviours == null ||
            punchRight == null || punchLeft == null || kickClip == null)
        {
            Debug.LogError($"{Tag} Falta alguna pieza base (GA_ClericStaffCombo, GA_PaladinPrimaryAttack, " +
                           "GA_FighterShieldBlock, GA_FighterChargeDefensive, GE_Stun, GE_Class_Damage, los " +
                           "cooldowns, GE_EnergyRegen, ASDef_Rogue, Class_Rogue, PaladinBehaviours o los clips " +
                           "de puño y patada de Kevin Iglesias). No se creó nada.");
            return;
        }

        string abilityFolder = EnsureFolder("Assets/GameplayAbilities", "Monk");
        string attrFolder    = EnsureFolder("Assets/Attributes", "Monk");
        string buffFolder    = Folder(blessing);   // Effects/Buffs

        // ---------------- EFECTOS ----------------
        GameplayEffect martialArts = Find<GameplayEffect>("GE_MartialArts");
        if (martialArts == null)
        {
            martialArts = ScriptableObject.CreateInstance<GameplayEffect>();
            martialArts.Duration       = 8f;
            martialArts.StackingPolicy = GameplayEffect.EStackingType.Stack;
            martialArts.MaxStacks      = 4;
            martialArts.EffectType     = GameplayEffect.EEffectType.Buff;
            martialArts.Icon           = FindSprite("Martial_Arts_Icon");
            martialArts.Modifiers      = new List<Modifier>
            {
                // AtkSpeed es el TIEMPO entre ataques: × 0.95 = 5 % más rápido. Se multiplican
                // entre acumulaciones (4 = × 0.81).
                new Modifier { Attribute = EAttributeType.AtkSpeed, Type = Modifier.EModificationType.Multiply, Magnitude = 0.95f },
                new Modifier { Attribute = EAttributeType.MovSpeed, Type = Modifier.EModificationType.Multiply, Magnitude = 1.05f },
            };
            Save(martialArts, buffFolder, "GE_MartialArts");
        }

        GameplayEffect ki = Find<GameplayEffect>("GE_Ki");
        if (ki == null)
        {
            ki = ScriptableObject.CreateInstance<GameplayEffect>();
            ki.Duration       = 10f;
            ki.StackingPolicy = GameplayEffect.EStackingType.Refresh;
            ki.EffectType     = GameplayEffect.EEffectType.Buff;
            ki.Icon           = FindSprite("Ki_Icon");
            ki.GrantedTags    = new List<EGameplayTag> { EGameplayTag.Status_Ki };
            Save(ki, buffFolder, "GE_Ki");
        }

        GameplayEffect patientDefense = Find<GameplayEffect>("GE_PatientDefense");
        if (patientDefense == null)
        {
            patientDefense = ScriptableObject.CreateInstance<GameplayEffect>();
            patientDefense.Duration       = 6f;
            patientDefense.StackingPolicy = GameplayEffect.EStackingType.Refresh;
            patientDefense.EffectType     = GameplayEffect.EEffectType.Buff;
            patientDefense.Icon           = FindSprite("Patient_Defense_Icon");
            patientDefense.GrantedTags    = new List<EGameplayTag> { EGameplayTag.Status_PatientDefense };
            Save(patientDefense, buffFolder, "GE_PatientDefense");
        }

        // ---------------- CLIC IZQUIERDO: puñetazos / Ráfaga de golpes ----------------
        GA_ConeAttack punchR = MakePunch(coneTemplate, punchRight, "Right Punch", "GA_MonkPunchRight", abilityFolder);
        GA_ConeAttack punchL = MakePunch(coneTemplate, punchLeft,  "Left Punch",  "GA_MonkPunchLeft",  abilityFolder);
        float firstDelay = comboTemplate.Sequence != null && comboTemplate.Sequence.Count > 0
            ? Mathf.Max(0.2f, comboTemplate.Sequence[0].DelayAfter) : 0.4f;

        GA_ComboSequence strikes = Find<GA_ComboSequence>("GA_MonkStrikes");
        if (strikes == null)
        {
            strikes = Object.Instantiate(comboTemplate);   // ritmo por velocidad de ataque
            strikes.AbilityName = "Strikes";
            strikes.AbilityIcon = FindSprite("Strikes_Icon") ?? comboTemplate.AbilityIcon;
            strikes.Sequence = new List<GA_ComboSequence.ComboStep>
            {
                new GA_ComboSequence.ComboStep { AbilityToCast = punchR, DelayAfter = firstDelay },
                new GA_ComboSequence.ComboStep { AbilityToCast = punchL, DelayAfter = 0f },
            };
            Save(strikes, abilityFolder, "GA_MonkStrikes");
        }

        GA_SelfBuff flurryBuff = Find<GA_SelfBuff>("GA_FlurryStacks");
        if (flurryBuff == null)
        {
            flurryBuff = ScriptableObject.CreateInstance<GA_SelfBuff>();
            flurryBuff.AbilityName          = "Flurry Stacks";
            flurryBuff.BuffEffect           = martialArts;   // cuatro veces: las 4 acumulaciones de una
            flurryBuff.AdditionalEffects    = new List<GameplayEffect> { martialArts, martialArts, martialArts };
            flurryBuff.AnimationTriggerName = "";
            Save(flurryBuff, abilityFolder, "GA_FlurryStacks");
        }

        GA_ComboSequence flurry = Find<GA_ComboSequence>("GA_FlurryOfBlows");
        if (flurry == null)
        {
            flurry = Object.Instantiate(comboTemplate);
            flurry.AbilityName = "Flurry of Blows";
            flurry.AbilityIcon = FindSprite("Flurry_Of_Blows_Icon") ?? strikes.AbilityIcon;
            flurry.Sequence = new List<GA_ComboSequence.ComboStep>
            {
                new GA_ComboSequence.ComboStep { AbilityToCast = flurryBuff, DelayAfter = 0f },
                new GA_ComboSequence.ComboStep { AbilityToCast = punchR,     DelayAfter = firstDelay },
                new GA_ComboSequence.ComboStep { AbilityToCast = punchL,     DelayAfter = 0f },
            };
            Save(flurry, abilityFolder, "GA_FlurryOfBlows");
        }

        GA_TagSwitch attack = Find<GA_TagSwitch>("GA_MonkAttack");
        if (attack == null)
        {
            attack = ScriptableObject.CreateInstance<GA_TagSwitch>();
            attack.AbilityName              = "Strikes";
            attack.AbilityIcon              = strikes.AbilityIcon;
            attack.CooldownEffect           = comboTemplate.CooldownEffect;
            attack.CooldownDuration         = comboTemplate.CooldownDuration;
            attack.UseAttackSpeedAsCooldown = comboTemplate.UseAttackSpeedAsCooldown;
            attack.ActivationBlockedTags    = new List<EGameplayTag>(comboTemplate.ActivationBlockedTags ?? new List<EGameplayTag>());
            attack.TargetLayer              = comboTemplate.TargetLayer;
            attack.AnimationTriggerName     = "";
            attack.DefaultAbility           = strikes;
            attack.Variants = new List<GA_TagSwitch.TagVariant>
            {
                new GA_TagSwitch.TagVariant { RequiredTag = EGameplayTag.Status_Ki, Ability = flurry, ConsumeTag = true },
            };
            attack.ShowVariantIcon = true;
            Save(attack, abilityFolder, "GA_MonkAttack");
        }

        // ---------------- CLIC DERECHO: bloqueo / Defensa paciente ----------------
        GA_ShieldBlock block = Find<GA_ShieldBlock>("GA_MonkBlock");
        if (block == null)
        {
            block = Object.Instantiate(blockTemplate);
            block.AbilityName = "Block";
            block.AbilityIcon = FindSprite("Monk_Block_Icon") ?? blockTemplate.AbilityIcon;
            Save(block, abilityFolder, "GA_MonkBlock");
        }

        GA_SelfBuff patient = Find<GA_SelfBuff>("GA_PatientDefense");
        if (patient == null)
        {
            patient = ScriptableObject.CreateInstance<GA_SelfBuff>();
            patient.AbilityName           = "Patient Defense";
            patient.AbilityIcon           = patientDefense.Icon;
            patient.BuffEffect            = patientDefense;
            patient.ActivationBlockedTags = new List<EGameplayTag> { EGameplayTag.State_Stunned };
            patient.AnimationTriggerName  = "";
            Save(patient, abilityFolder, "GA_PatientDefense");
        }

        GA_HoldTagSwitch guard = Find<GA_HoldTagSwitch>("GA_MonkGuard");
        if (guard == null)
        {
            guard = ScriptableObject.CreateInstance<GA_HoldTagSwitch>();
            guard.AbilityName           = "Block";
            guard.AbilityIcon           = block.AbilityIcon;
            guard.ActivationBlockedTags = new List<EGameplayTag>(blockTemplate.ActivationBlockedTags ?? new List<EGameplayTag>());
            guard.TargetLayer           = blockTemplate.TargetLayer;
            guard.AnimationTriggerName  = "";
            guard.DefaultAbility        = block;
            guard.Variants = new List<GA_TagSwitch.TagVariant>
            {
                new GA_TagSwitch.TagVariant { RequiredTag = EGameplayTag.Status_Ki, Ability = patient, ConsumeTag = true },
            };
            guard.ShowVariantIcon = true;
            Save(guard, abilityFolder, "GA_MonkGuard");
        }

        // ---------------- Q: Ki ----------------
        GA_SelfBuff kiAbility = Find<GA_SelfBuff>("GA_Ki");
        if (kiAbility == null)
        {
            kiAbility = ScriptableObject.CreateInstance<GA_SelfBuff>();
            kiAbility.AbilityName           = "Ki";
            kiAbility.AbilityIcon           = ki.Icon;
            kiAbility.BuffEffect            = ki;
            kiAbility.MaxCharges            = 3;
            kiAbility.CooldownEffect        = cdSpecial;
            kiAbility.CooldownDuration      = 8f;   // lo que tarda en volver CADA carga
            // Con Ki ya preparado no se gasta otra carga encima.
            kiAbility.ActivationBlockedTags = new List<EGameplayTag> { EGameplayTag.State_Stunned, EGameplayTag.Status_Ki };
            kiAbility.AnimationTriggerName  = "";
            Save(kiAbility, abilityFolder, "GA_Ki");
        }

        // ---------------- SHIFT: Patada voladora / Patada del dragón ----------------
        GA_RushAttack flyingKick = Find<GA_RushAttack>("GA_FlyingKick");
        if (flyingKick == null)
        {
            flyingKick = MakeKick(dashTemplate, classDmg, kickClip, "Flying Kick", 14f, 9f);
            Save(flyingKick, abilityFolder, "GA_FlyingKick");
        }

        GA_RushAttack dragonKick = Find<GA_RushAttack>("GA_DragonKick");
        if (dragonKick == null)
        {
            dragonKick = MakeKick(dashTemplate, classDmg, kickClip, "Dragon Kick", 16f, 16f);
            dragonKick.FirstHitEffects = new List<GameplayEffect> { stun };
            Save(dragonKick, abilityFolder, "GA_DragonKick");
        }

        GA_TagSwitch kick = Find<GA_TagSwitch>("GA_MonkKick");
        if (kick == null)
        {
            kick = ScriptableObject.CreateInstance<GA_TagSwitch>();
            kick.AbilityName           = "Flying Kick";
            kick.AbilityIcon           = flyingKick.AbilityIcon;
            kick.CooldownEffect        = cdMove;
            kick.CooldownDuration      = 0f;   // manda el GE_Cooldown_MoveAbility
            kick.ActivationBlockedTags = new List<EGameplayTag>(dashTemplate.ActivationBlockedTags ?? new List<EGameplayTag>());
            kick.TargetLayer           = dashTemplate.TargetLayer;
            kick.AnimationTriggerName  = "";
            kick.DefaultAbility        = flyingKick;
            kick.Variants = new List<GA_TagSwitch.TagVariant>
            {
                new GA_TagSwitch.TagVariant { RequiredTag = EGameplayTag.Status_Ki, Ability = dragonKick, ConsumeTag = true },
            };
            kick.ShowVariantIcon = true;
            Save(kick, abilityFolder, "GA_MonkKick");
        }

        // ---------------- PASIVAS: Artes marciales + las dos barreras ----------------
        GameObject behaviours = Find<GameObject>("MonkBehaviours");
        if (behaviours == null)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath($"{abilityFolder}/MonkBehaviours.prefab");
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(paladinBehaviours), path);

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            root.name = "MonkBehaviours";

            // El aura es del Paladín: el Monje se queda con el escudo y su destello.
            foreach (PaladinAuraPassive aura in root.GetComponentsInChildren<PaladinAuraPassive>(true))
                Object.DestroyImmediate(aura, true);

            Entity_ShieldBarrier barrier = root.GetComponentInChildren<Entity_ShieldBarrier>(true);
            if (barrier != null)
            {
                // El bloqueo: personal y con menos reducción que los escudos de verdad.
                barrier.DamageReduction        = 0.4f;
                barrier.EnergyPerDamageBlocked = 1f;
                barrier.ParryWindow            = 0f;
                barrier.ReflectMeleeFraction   = 0f;
                barrier.ProtectAllies          = false;

                MakeCapsule(barrier);
            }
            else Debug.LogWarning($"{Tag} PaladinBehaviours no tiene Entity_ShieldBarrier: el Monje no va a bloquear.");

            MartialArtsPassive passive = root.GetComponent<MartialArtsPassive>();
            if (passive == null) passive = root.AddComponent<MartialArtsPassive>();
            passive.StackEffect = martialArts;

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);

            behaviours = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Debug.Log($"{Tag} Creado {path}.");
        }

        // ---------------- STATS ----------------
        AttributeSetDefinition stats = Find<AttributeSetDefinition>("ASDef_Monk");
        if (stats == null)
        {
            stats = Object.Instantiate(rogueStats);   // trae la velocidad del Pícaro
            SetStat(stats, EAttributeType.Health,    80f);
            SetStat(stats, EAttributeType.MaxHealth, 80f);
            SetStat(stats, EAttributeType.Attack,    4f);
            SetStat(stats, EAttributeType.Def,       4f);
            SetStat(stats, EAttributeType.AtkSpeed,  1f);
            SetStat(stats, EAttributeType.Energy,    50f);
            SetStat(stats, EAttributeType.MaxEnergy, 50f);
            Save(stats, attrFolder, "ASDef_Monk");
        }

        // ---------------- LA CLASE ----------------
        CharacterClassDefinition monk = Find<CharacterClassDefinition>("Class_Monk");
        if (monk == null)
        {
            monk = Object.Instantiate(rogue);   // Daño, la velocidad y el crecimiento del Pícaro (+50 vida, +1 ataque)
            monk.ClassName      = "Monk";
            monk.ClassIcon      = FindSprite("Class_Monk_Icon") ?? rogue.ClassIcon;
            monk.Description    = "A martial artist who fights with bare hands. Every hit that lands makes him " +
                                  "faster, and Ki empowers his next move: a flurry of blows, a defense that " +
                                  "guards him from every side, or a kick that stuns.";
            monk.Role           = EClassRole.Damage;
            monk.BaseAttributes = stats;
            monk.PassiveBehaviorsPrefab = behaviours;
            monk.PassiveEffects = new List<GameplayEffect> { energyRegen };
            monk.AvailableSubclasses = new List<CharacterClassDefinition>();
            monk.MainHandWeaponPrefab = null;   // puños
            monk.OffHandWeaponPrefab  = null;

            monk.Abilities = new List<CharacterClassDefinition.AbilityAssignment>
            {
                new CharacterClassDefinition.AbilityAssignment { InputSlot = EAbilityInput.PrimaryAttack,   Ability = attack },
                new CharacterClassDefinition.AbilityAssignment { InputSlot = EAbilityInput.SecondaryAttack, Ability = guard },
                new CharacterClassDefinition.AbilityAssignment { InputSlot = EAbilityInput.Action1,         Ability = kiAbility },
                new CharacterClassDefinition.AbilityAssignment { InputSlot = EAbilityInput.Movement,        Ability = kick },
            };
            Save(monk, attrFolder, "Class_Monk");
        }

        AssetDatabase.SaveAssets();

        RegisterInPlayerPrefab(monk);
        RegisterInLobbies(monk);

        MercRegistryTools.RefreshNetworkRegistries();
        Debug.Log($"{Tag} Listo. Ya podés borrar esta herramienta (MercMonkSetup.cs).");
    }

    // =========================================================
    // PIEZAS
    // =========================================================

    private static GA_ConeAttack MakePunch(GA_ConeAttack template, AnimationClip clip, string displayName,
                                           string assetName, string folder)
    {
        GA_ConeAttack punch = Find<GA_ConeAttack>(assetName);
        if (punch != null) return punch;

        punch = Object.Instantiate(template);   // GE_Class_Damage y el sonido del golpe del Paladín
        punch.AbilityName   = displayName;
        punch.AnimationClip = clip;
        punch.Range         = 2f;
        punch.ConeAngle     = 90f;
        Save(punch, folder, assetName);
        return punch;
    }

    private static GA_RushAttack MakeKick(GA_Dash dashTemplate, GameplayEffect damage, AnimationClip clip,
                                          string displayName, float distance, float speed)
    {
        GA_RushAttack kick = ScriptableObject.CreateInstance<GA_RushAttack>();
        kick.AbilityName           = displayName;
        kick.AbilityIcon           = FindSprite(displayName.Replace(' ', '_') + "_Icon") ?? dashTemplate.AbilityIcon;
        kick.AnimationClip         = clip;
        kick.Distance              = distance;
        kick.Speed                 = speed;
        kick.TurnRate              = 70f;
        kick.Cancelable            = true;
        kick.ExcludePlayerLayer    = dashTemplate.ExcludePlayerLayer;
        kick.TargetLayer           = dashTemplate.TargetLayer;
        kick.ActivationBlockedTags = new List<EGameplayTag>(dashTemplate.ActivationBlockedTags ?? new List<EGameplayTag>());
        kick.DamageEffect          = damage;
        kick.HitRadius             = 1.2f;
        kick.StopAtFirstEnemy      = true;
        kick.HitVFX                = dashTemplate.HitVFX;
        return kick;
    }

    // La cápsula de la Defensa paciente: una copia de la barrera del bloqueo que se prende con
    // Status_PatientDefense, frena desde cualquier lado y envuelve al Monje. El visual es una
    // esfera con el MISMO material del escudo (nada de VFX nuevos).
    private static void MakeCapsule(Entity_ShieldBarrier barrier)
    {
        Material shieldMat = null;
        Renderer shieldRenderer = barrier.GetComponentInChildren<Renderer>(true);
        if (shieldRenderer != null) shieldMat = shieldRenderer.sharedMaterial;

        GameObject capsule = Object.Instantiate(barrier.gameObject, barrier.transform.parent);
        capsule.name = "PatientDefenseBarrier";
        capsule.transform.localPosition = Vector3.zero;
        capsule.transform.localRotation = Quaternion.identity;
        capsule.transform.localScale    = Vector3.one;

        // Fuera los visuales del escudo: la cápsula tiene el suyo.
        for (int i = capsule.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(capsule.transform.GetChild(i).gameObject, true);

        Entity_ShieldBarrier cap = capsule.GetComponent<Entity_ShieldBarrier>();
        cap.ActiveTag            = EGameplayTag.Status_PatientDefense;
        cap.Omnidirectional      = true;
        cap.ProtectAllies        = false;
        cap.FollowAimPitch       = false;
        cap.ParryWindow          = 0f;
        cap.ReflectMeleeFraction = 0f;
        cap.DamageReduction      = 0.4f;

        BoxCollider box = capsule.GetComponent<BoxCollider>();
        box.isTrigger = true;
        box.center    = new Vector3(0f, 1.1f, 0f);
        box.size      = new Vector3(2f, 2.4f, 2f);

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "CapsuleVisual";
        Object.DestroyImmediate(sphere.GetComponent<Collider>());
        sphere.transform.SetParent(capsule.transform, false);
        sphere.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        sphere.transform.localScale    = new Vector3(1.8f, 2.3f, 1.8f);
        if (shieldMat != null) sphere.GetComponent<Renderer>().sharedMaterial = shieldMat;
        sphere.SetActive(false);
    }

    // El jugador conoce sus clases por MainBaseClasses (de ahí arma la lista de red).
    private static void RegisterInPlayerPrefab(CharacterClassDefinition cls)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/Prefabs")) continue;
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || asset.GetComponent<PlayerController>() == null) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            PlayerController pc = root.GetComponent<PlayerController>();
            var list = new List<CharacterClassDefinition>(pc.MainBaseClasses ?? new CharacterClassDefinition[0]);
            if (!list.Contains(cls))
            {
                list.Add(cls);
                pc.MainBaseClasses = list.ToArray();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"{Tag} Agregado a MainBaseClasses de {path}.");
            }
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // La sala de espera de cada escena del modo ofrece las clases de SelectableClasses.
    private static void RegisterInLobbies(CharacterClassDefinition cls)
    {
        string previous = EditorSceneManager.GetActiveScene().path;

        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            bool changed = false;
            foreach (UI_LobbyPanel panel in Object.FindObjectsByType<UI_LobbyPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var list = new List<CharacterClassDefinition>(panel.SelectableClasses ?? new CharacterClassDefinition[0]);
                if (list.Contains(cls)) continue;
                list.Add(cls);
                panel.SelectableClasses = list.ToArray();
                EditorUtility.SetDirty(panel);
                changed = true;
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"{Tag} Agregado a la sala de {path}.");
            }
        }

        if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
    }

    // =========================================================
    // AYUDAS
    // =========================================================

    private static void SetStat(AttributeSetDefinition set, EAttributeType attribute, float value)
    {
        foreach (var a in set.InitialAttributes)
            if (a.Attribute == attribute) { a.BaseValue = value; return; }
        set.InitialAttributes.Add(new AttributeSetDefinition.BaseAttribute { Attribute = attribute, BaseValue = value });
    }

    private static AnimationClip FindClipInModel(string modelName)
    {
        foreach (string guid in AssetDatabase.FindAssets(modelName))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
            if (Path.GetFileNameWithoutExtension(path) != modelName) continue;

            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (clip != null) return clip;
        }
        Debug.LogWarning($"{Tag} No encontré el clip {modelName}.");
        return null;
    }

    private static void Save(Object asset, string folder, string name)
    {
        asset.name = name;
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name}.asset");
        AssetDatabase.CreateAsset(asset, path);
        Debug.Log($"{Tag} Creado {path}.");
    }

    private static string Folder(Object asset)
    {
        if (asset == null) return null;
        return Path.GetDirectoryName(AssetDatabase.GetAssetPath(asset)).Replace('\\', '/');
    }

    private static string EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        return path;
    }

    private static T Find<T>(string name) where T : Object
    {
        string type = typeof(T) == typeof(GameObject) ? "Prefab" : typeof(T).Name;
        foreach (string guid in AssetDatabase.FindAssets($"{name} t:{type}"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) != name) continue;
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
        }
        return null;
    }

    private static Sprite FindSprite(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets($"{name} t:Sprite"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) != name) continue;
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;
        }
        return null;   // sin ícono: se pone a mano
    }
}
