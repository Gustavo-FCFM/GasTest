using UnityEngine;
using System.Collections.Generic;

// ============================================================
// GameplayAbilityRegistry
//
// Tabla compartida de todas las GameplayAbility cuyos VFX necesitan
// replicarse por red. Análogo a GameplayEffectRegistry: FishNet no
// puede mandar la referencia a un ScriptableObject por RPC, así que el
// servidor manda el ÍNDICE de esta lista y cada peer lo resuelve
// contra su propia copia local (cargada vía Resources, ver Instance).
//
// Reemplaza al viejo esquema "por slot" para replicar VFX: identificar
// por slot fallaba para (a) subclases que el observador no tiene
// equipadas y (b) pasos de combo, que no viven en ningún slot. El
// índice de registro no depende de la clase equipada del observador.
//
// Mantenimiento: cualquier GameplayAbility que reproduzca un VFX de
// impacto (PlayImpactVFX) o una VisualsSequence tiene que estar en la
// lista Abilities. Si falta, la habilidad sigue funcionando pero su VFX
// solo se ve en el servidor/host (se avisa con un LogWarning). Usá el
// menú contextual "Auto-Fill From Project" del asset para rellenarla.
// ============================================================
[CreateAssetMenu(fileName = "GameplayAbilityRegistry", menuName = "GAS/Gameplay Ability Registry")]
public class GameplayAbilityRegistry : ScriptableObject
{
    // Todas las habilidades registradas; su posición en la lista ES el índice
    // que viaja por red. No reordenar mientras el juego esté corriendo en red
    // (el índice tiene que significar lo mismo en todos los peers de un build).
    public List<GameplayAbility> Abilities = new List<GameplayAbility>();

    private static GameplayAbilityRegistry _instance;

    // Acceso global al registro. El asset vive en
    // Assets/Resources/GameplayAbilityRegistry.asset — Resources.Load lo
    // encuentra en cualquier proceso (servidor o cliente) sin referencia manual.
    public static GameplayAbilityRegistry Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<GameplayAbilityRegistry>("GameplayAbilityRegistry");
            return _instance;
        }
    }

    // Índice de una habilidad en la lista. Las instancias otorgadas y los pasos
    // de combo son CLONES (Instantiate) del template, así que matcheamos contra
    // SourceTemplate cuando existe (el clon lo apunta al asset original).
    public int GetIndex(GameplayAbility ability)
    {
        if (ability == null) return -1;
        GameplayAbility key = ability.SourceTemplate != null ? ability.SourceTemplate : ability;
        return Abilities.IndexOf(key);
    }

    // Inverso de GetIndex: resuelve un índice recibido por red de vuelta al
    // asset-template real (el observador lo usa con PlayImpactVFXFor /
    // PlayVisualsSequence pasándole su propio ASC como dueño puntual).
    public GameplayAbility GetAbility(int index)
    {
        if (index < 0 || index >= Abilities.Count) return null;
        return Abilities[index];
    }

#if UNITY_EDITOR
    // Rellena la lista con TODAS las GameplayAbility del proyecto, ordenadas por
    // nombre (orden determinístico). Click derecho sobre el asset → esta opción.
    [ContextMenu("Auto-Fill From Project")]
    private void AutoFillFromProject()
    {
        var found = new List<GameplayAbility>();
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:GameplayAbility"))
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var ga = UnityEditor.AssetDatabase.LoadAssetAtPath<GameplayAbility>(path);
            if (ga != null) found.Add(ga);
        }
        found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        Abilities.Clear();
        Abilities.AddRange(found);
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[GameplayAbilityRegistry] Auto-Fill: {Abilities.Count} habilidades registradas.");
    }
#endif
}
