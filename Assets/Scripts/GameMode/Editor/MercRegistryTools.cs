using UnityEditor;
using UnityEngine;

// ============================================================
// MercRegistryTools
//
// Rellena de una los dos registros de red: el de habilidades y el de efectos.
//
// POR QUE HACE FALTA: FishNet no puede mandar la referencia a un ScriptableObject por
// RPC. El servidor manda el INDICE dentro de estas listas y cada peer lo resuelve contra
// su propia copia (cargada por Resources.Load). Si una habilidad o un efecto NO esta en
// su registro, el indice viaja en -1 y su VFX —o el icono del buff— se ve SOLO en el host.
//
// O sea: cada vez que creas un GA_* o un GE_* nuevo, hay que correr esto. Antes se hacia
// con el boton derecho sobre cada asset ("Auto-Fill From Project"), pero habia que
// acordarse de los DOS y encontrarlos dentro de Resources.
//
// EL ORDEN es alfabetico y deterministico, asi que el indice significa lo mismo en todos
// lados MIENTRAS el conjunto de assets sea el mismo. Despues de rellenarlos hay que
// repartir el mismo build a todos: un cliente viejo resolveria los indices contra su
// lista vieja y veria el VFX equivocado.
// ============================================================
public static class MercRegistryTools
{
    [MenuItem("Mercenarios/Actualizar los registros de red (habilidades y efectos)", false, 1)]
    public static void RefreshNetworkRegistries()
    {
        int abilities = RefreshRegistry<GameplayAbilityRegistry>("GameplayAbilityRegistry");
        int effects   = RefreshRegistry<GameplayEffectRegistry>("GameplayEffectRegistry");

        AssetDatabase.SaveAssets();

        if (abilities < 0 || effects < 0) return;
        Debug.Log($"[Mercenarios] Registros actualizados: {abilities} habilidades y {effects} efectos. " +
                  "Acordate de repartir el mismo build a todos.");
    }

    // Llama al "Auto-Fill From Project" del asset. Es un ContextMenu privado, asi que se
    // invoca por reflexion — mejor eso que duplicar aca la logica de rellenado y que las
    // dos versiones se separen con el tiempo.
    private static int RefreshRegistry<T>(string resourceName) where T : ScriptableObject
    {
        // Se busca POR TIPO, no por ruta: el asset se puede mover dentro de Resources.
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        if (guids.Length == 0)
        {
            Debug.LogError($"[Mercenarios] No encontré ningún {typeof(T).Name} en el proyecto. " +
                           $"Creá uno en Assets/Resources/{resourceName}.asset (menú Create ▸ GAS).");
            return -1;
        }

        if (guids.Length > 1)
            Debug.LogWarning($"[Mercenarios] Hay {guids.Length} assets de {typeof(T).Name}. " +
                             "El juego carga el de Resources; actualizo solo ese.");

        T target = null;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T candidate = AssetDatabase.LoadAssetAtPath<T>(path);
            if (candidate == null) continue;

            // El que vale es el de Resources: es el unico que Resources.Load encuentra en
            // runtime. Si no hay ninguno ahi, se usa el primero y se avisa.
            if (path.Contains("/Resources/")) { target = candidate; break; }
            if (target == null) target = candidate;
        }

        if (target == null) return -1;

        string assetPath = AssetDatabase.GetAssetPath(target);
        if (!assetPath.Contains("/Resources/"))
            Debug.LogWarning($"[Mercenarios] '{assetPath}' no está en una carpeta Resources: " +
                             "Resources.Load no lo va a encontrar y el juego se queda sin registro.");

        var fill = typeof(T).GetMethod("AutoFillFromProject",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public);

        if (fill == null)
        {
            Debug.LogError($"[Mercenarios] {typeof(T).Name} ya no tiene AutoFillFromProject. " +
                           "Si le cambiaste el nombre, actualizá esta herramienta.");
            return -1;
        }

        fill.Invoke(target, null);
        EditorUtility.SetDirty(target);

        // La cuenta sale de la lista, que es lo que de verdad quedo guardado.
        var list = typeof(T).GetField("Abilities") ?? typeof(T).GetField("Effects");
        return list != null && list.GetValue(target) is System.Collections.ICollection c ? c.Count : 0;
    }
}
