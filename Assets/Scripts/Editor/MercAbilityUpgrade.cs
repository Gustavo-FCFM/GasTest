using UnityEditor;
using UnityEngine;

// ============================================================
// MercAbilityUpgrade
//
// Guarda en disco TODAS las habilidades en el formato nuevo (listas de efectos y de VFX
// con condiciones, tótems en una lista, etc.).
//
// No es obligatorio: cada habilidad se pasa sola al formato nuevo cada vez que se carga
// (GameplayAbility.UpgradeLegacyData), en el editor y en la build, así que el juego ya
// funciona igual. Esto solo hace que los .asset queden escritos con lo nuevo de una vez,
// en vez de ir guardándose de a uno a medida que se tocan — así el cambio sale entero en
// un solo commit y no aparece de a pedacitos después.
//
// Las marca TODAS para guardar y no solo las que tenían datos viejos: después de una
// recompilación, una habilidad ya pasada en memoria no recuerda que en disco sigue vieja.
// Las que no cambian se reescriben iguales (git no muestra nada).
// ============================================================
public static class MercAbilityUpgrade
{
    [MenuItem("Mercenarios/Guardar las habilidades en el formato nuevo", false, 2)]
    public static void SaveAllInNewFormat()
    {
        int total = 0, upgraded = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:GameplayAbility"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameplayAbility ability = AssetDatabase.LoadAssetAtPath<GameplayAbility>(path);
            if (ability == null) continue;
            total++;

            // Al cargarse ya se pasó (UpgradedOnLoad); por las dudas se corre de nuevo.
            if (ability.UpgradeLegacyData() | ability.UpgradedOnLoad) upgraded++;

            EditorUtility.SetDirty(ability);
            ability.UpgradedOnLoad = false;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Mercenarios] {total} habilidades guardadas en el formato nuevo " +
                  $"({upgraded} se pasaron recién en esta sesión).");
    }
}
