using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// ============================================================
// MercActionTagsSetup  (herramienta de un solo uso: correr y borrar)
//
// 1 de octubre de 2026, pedido de Gustavo: qué bloquea a cada habilidad pasa a decirlo
// la propia habilidad (ActivationBlockedTags), por TIPO de acción:
//
//   · State_Stunned  → TODAS las que se aprietan.
//   · State_Disarmed → las de ARMA: golpear con ella o lanzarla. NO el bloqueo con escudo.
//   · State_Silenced → las de MAGIA o fantasía (furia, auras, hechizos, invocaciones).
//   · State_Rooted   → las de MOVIMIENTO (dashes, saltos, teletransportes, cargas).
//
// La tabla de abajo es la clasificación, habilidad por habilidad. Para cada una deja
// EXACTAMENTE esos tags (reemplaza lo que tuviera). Las que no están en la tabla no se
// tocan y se listan al final, para revisarlas a mano (así una habilidad nueva no queda
// clasificada de casualidad).
//
// De paso, los ajustes del Maestro de batalla que pidió Gustavo:
//   · El clic izquierdo en ofensiva pasa a ser UN tajo de mandoble (Attack2H01), no el
//     combo alternado.
//   · La pose de carga del clic derecho es la del Golpe final del Inmortal (el arma arriba).
//   · El escudo en la espalda gira 180° en Y (salía al revés).
// ============================================================
public static class MercActionTagsSetup
{
    private const string W = "W";   // State_Disarmed (arma)
    private const string M = "M";   // State_Silenced (magia)
    private const string R = "R";   // State_Rooted   (movimiento)
    private const string O = "";    // solo el aturdido

    // Habilidad → tipo. Una habilidad puede ser de dos tipos (un teletransporte mágico).
    private static readonly Dictionary<string, string> Table = new Dictionary<string, string>
    {
        // ---- Bárbaro ----
        { "GA_AxeAttack", W }, { "GA_AxeThrow", W },
        { "GA_Rage", M },                                   // solo el silencio
        { "GA_BarbarianLeap", R },                          // solo el enraizado
        // Berserker
        { "GA_AlternatingBerserker", W }, { "GA_Berserker_AxeAttack", W }, { "GA_ConeReckless 1", W },
        { "GA_ConeReckless2", W }, { "GA_LineReckless", W }, { "GA_RecklessAttack", W },
        { "GA_WhirlwindAttack", W }, { "GA_Frenzy", M },
        // Inmortal (GA_InmortalWrath NO: se activa sola al morir)
        { "GA_SwordAttack", W }, { "GA_FinalBlow", W }, { "GA_Inmortal_Rage", M },
        // Chamán
        { "GA_SpawnTotem", M }, { "GA_ElementalFury", M },

        // ---- Clérigo ----
        { "GA_ClericStaffCombo", W }, { "GA_OrderStaffCombo", W }, { "GA_RadiantStaffCombo", W },
        { "GA_ClericStaffSwing", W },
        { "GA_ClericLightArc", M }, { "GA_OrderLightArc", M }, { "GA_RadiantLightArc", M },
        { "GA_HealingWord", M }, { "GA_RadiantHealingWord", M },
        { "GA_GuidingLight", M }, { "GA_RadiantGuidingBolt", M },
        { "GA_ClericFlash", R + M },                        // teletransporte mágico
        { "GA_PreserveLife", M }, { "GA_Resurrection", M },
        { "GA_CommandHalt", M }, { "GA_ZoneOfTruth", M },
        { "GA_DawnLight", M }, { "GA_HolyFire", M },

        // ---- Guerrero ----
        { "GA_FighterPrimaryCombo", W }, { "GA_FighterSlash", W },
        { "GA_FighterShieldBlock", O },                     // el escudo no es "arma"
        { "GA_FighterStance", O }, { "GA_EnterDefensiveStance", O }, { "GA_EnterOffensiveStance", O },
        { "GA_FighterCharge", R }, { "GA_FighterChargeDefensive", R }, { "GA_FighterChargeOffensive", R },
        // Maestro de batalla
        { "GA_BattleMasterPrimary", W }, { "GA_GreatswordCombo", W }, { "GA_GreatswordSlash", W },
        { "GA_BattleMasterGuard", O },                      // escudo o carga: lo decide la variante
        { "GA_GreatswordChargedStrike", W },
        { "GA_ChargedStrikeStage1", W }, { "GA_ChargedStrikeStage2", W }, { "GA_ChargedStrikeStage3", W },
        { "GA_Disarm", W },
        { "GA_LimitBreak", O }, { "GA_BreakLimits", O },

        // ---- Paladín ----
        { "GA_PaladinSmiteSwitch", W }, { "GA_PaladinPrimaryAttack", W }, { "GA_AttackWithSmite", W },
        { "GA_SmiteBeam", M }, { "GA_DivineSmite", M },
        { "GA_PaladinShieldBlock", O },
        { "GA_HeroicIntervention", R },
        // Conquista
        { "GA_PaladinSwitchConquest", W }, { "GA_PaladinPrimaryAttackConquest", W },
        { "GA_PaladinPrimaryAttackConquestUltimate", W }, { "GA_AttackWithSmiteConquest", W },
        { "GA_SmiteBeamConquest", M }, { "GA_ConqueringPresence", M }, { "GA_ShieldOfFaith", M },
        { "GA_InvencibleConqueror", O },
        // Devoción
        { "GA_LayOnHands", M }, { "GA_DivineProtection", O },   // para salir de apuros, como la Comida
        // Venganza
        { "GA_PaladinSwitchVengeance", W }, { "GA_PaladinPrimaryAttackVengeance", W },
        { "GA_AttackWithSmiteVengeance", W }, { "GA_AvengingAngelAttack", W },
        { "GA_DivineSmiteVengeane", M }, { "GA_SwornEnemy", M }, { "GA_AvengingAngel", M },

        // ---- Pícaro ----
        { "GA_RoguePrimaryAttack", W }, { "GA_DaggerCone", W }, { "GA_DaggerLine", W }, { "GA_DaggerShoot", W },
        { "GA_Dash", R }, { "GA_Blink", R },                // solo el enraizado
        // Asesino
        { "GA_Assassin_Primary_Attack", W }, { "GA_Assassin_Line_Attack", W }, { "GA_Assassin_DaggerShoot", W },
        { "GA_Assassin_Dash", R }, { "GA_MarkedForDeath", R + W + M },   // aparece detrás y apuñala
        { "GA_ShadowAmbush", M },
        // Ilusionista
        { "GA_IllusionistPrimaryAttack", W }, { "GA_IllusionitDaggerCone", W }, { "GA_IllusionistDaggerShoot", W },
        { "GA_Illusionist_Dash", R }, { "GA_ExactCopy", M }, { "GA_CopyParty", M },
        // Pirata
        { "GA_AlternatingComboPirate", W }, { "GA_RapierCone", W }, { "GA_RapierLine", W }, { "GA_PirateGun", W },
        { "GA_Pirate_Dash", R }, { "GA_CannonBarrage", W },   // cañonazos: armas, no magia
        { "GA_EmergencyFood", O },                           // para salir de apuros
    };

    [MenuItem("Mercenarios/Aplicar los bloqueos por tipo de acción (una sola vez)", false, 8)]
    public static void Apply()
    {
        int changed = 0;
        var untouched = new List<string>();

        foreach (string guid in AssetDatabase.FindAssets("t:GameplayAbility", new[] { "Assets/GameplayAbilities" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("/Enemies/")) continue;

            GameplayAbility ability = AssetDatabase.LoadAssetAtPath<GameplayAbility>(path);
            if (ability == null) continue;

            string name = Path.GetFileNameWithoutExtension(path);
            if (!Table.TryGetValue(name, out string kind))
            {
                untouched.Add(name);
                continue;
            }

            var tags = new List<EGameplayTag> { EGameplayTag.State_Stunned };
            if (kind.Contains(W)) tags.Add(EGameplayTag.State_Disarmed);
            if (kind.Contains(M)) tags.Add(EGameplayTag.State_Silenced);
            if (kind.Contains(R)) tags.Add(EGameplayTag.State_Rooted);

            ability.ActivationBlockedTags = tags;
            EditorUtility.SetDirty(ability);
            changed++;
        }

        Debug.Log($"[Bloqueos] {changed} habilidades actualizadas.");
        if (untouched.Count > 0)
            Debug.LogWarning($"[Bloqueos] Sin clasificar (no se tocaron, revisarlas a mano): {string.Join(", ", untouched)}");

        BattleMasterTweaks();

        AssetDatabase.SaveAssets();
        Debug.Log("[Bloqueos] Listo. Ya podés borrar esta herramienta (MercActionTagsSetup.cs).");
    }

    // Los ajustes del Maestro de batalla (ver cabecera).
    private static void BattleMasterTweaks()
    {
        GA_TagSwitch primary = Find<GA_TagSwitch>("GA_BattleMasterPrimary");
        GameplayAbility slash = Find<GameplayAbility>("GA_GreatswordSlash");
        if (primary != null && slash != null && primary.Variants != null)
        {
            for (int i = 0; i < primary.Variants.Count; i++)
            {
                var v = primary.Variants[i];
                if (v.RequiredTag != EGameplayTag.Stance_Offensive) continue;
                v.Ability = slash;   // un solo tajo (Attack2H01)
                primary.Variants[i] = v;
            }
            EditorUtility.SetDirty(primary);
            Debug.Log("[Bloqueos] Maestro: el clic izquierdo en ofensiva es un solo tajo de mandoble.");
        }

        GA_ChargedAttack charged = Find<GA_ChargedAttack>("GA_GreatswordChargedStrike");
        GameplayAbility finalBlow = Find<GameplayAbility>("GA_FinalBlow");
        if (charged != null && finalBlow != null)
        {
            var clip = new SerializedObject(finalBlow).FindProperty("ChargeLoopAnimation")?.objectReferenceValue as AnimationClip;
            if (clip != null)
            {
                charged.ChargeLoopClip = clip;
                EditorUtility.SetDirty(charged);
                Debug.Log($"[Bloqueos] Maestro: la pose de carga ahora es {clip.name} (la del Golpe final).");
            }
            else Debug.LogWarning("[Bloqueos] GA_FinalBlow no tiene ChargeLoopAnimation: la pose de carga no cambió.");
        }

        CharacterClassDefinition master = Find<CharacterClassDefinition>("Class_BattleMasterFighter");
        if (master != null)
        {
            master.StowedOffHandRotationOffset = new Vector3(0f, 180f, 0f);
            EditorUtility.SetDirty(master);
            Debug.Log("[Bloqueos] Maestro: el escudo de la espalda gira 180° en Y.");
        }
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
}
