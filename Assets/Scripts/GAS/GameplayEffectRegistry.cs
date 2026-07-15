using UnityEngine;
using System.Collections.Generic;

// ============================================================
// GameplayEffectRegistry
//
// Tabla compartida de todos los GameplayEffect que necesitan
// sincronizar su icono en red. FishNet no puede mandar la
// referencia a un ScriptableObject por SyncVar, así que el servidor
// manda el ÍNDICE de esta lista y cada cliente lo resuelve contra su
// propia copia local (cargada vía Resources, ver Instance). Usado
// por NetworkAbilitySystemComponent para la barra de buffs/debuffs.
//
// Mantenimiento: cualquier GameplayEffect con Duration > 0 que
// quieras que se vea en esa barra tiene que estar en la lista
// Effects. Si falta, el efecto sigue funcionando en gameplay, solo
// no le sincroniza el icono a los clientes remotos.
// ============================================================
[CreateAssetMenu(fileName = "GameplayEffectRegistry", menuName = "GAS/Gameplay Effect Registry")]
public class GameplayEffectRegistry : ScriptableObject
{
    // Todos los efectos registrados; su posición en la lista ES el índice
    // que viaja por red. No reordenar mientras el juego esté corriendo en
    // red (el índice tiene que significar lo mismo en todos los peers).
    public List<GameplayEffect> Effects = new List<GameplayEffect>();

    private static GameplayEffectRegistry _instance;

    // Acceso global al registro. El asset vive en
    // Assets/Resources/GameplayEffectRegistry.asset — Resources.Load lo
    // encuentra en cualquier proceso (servidor o cliente) sin que haga
    // falta una referencia manual en cada prefab.
    public static GameplayEffectRegistry Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<GameplayEffectRegistry>("GameplayEffectRegistry");
            return _instance;
        }
    }

    // Busca en qué posición de la lista está un efecto. Devuelve -1 si no
    // está registrado (ver nota de mantenimiento arriba).
    public int GetIndex(GameplayEffect effect)
    {
        if (effect == null) return -1;
        return Effects.IndexOf(effect);
    }

    // Inverso de GetIndex: resuelve un índice recibido por red de vuelta
    // al GameplayEffect real.
    public GameplayEffect GetEffect(int index)
    {
        if (index < 0 || index >= Effects.Count) return null;
        return Effects[index];
    }

#if UNITY_EDITOR
    // Rellena la lista con TODOS los GameplayEffect del proyecto, ordenados por
    // nombre (orden determinístico). Click derecho sobre el asset → esta opción.
    // Incluir efectos instantáneos/Hidden es inofensivo (nunca se los busca);
    // lo importante es que estén los buffs/debuffs con icono.
    [ContextMenu("Auto-Fill From Project")]
    private void AutoFillFromProject()
    {
        var found = new List<GameplayEffect>();
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:GameplayEffect"))
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var ge = UnityEditor.AssetDatabase.LoadAssetAtPath<GameplayEffect>(path);
            if (ge != null) found.Add(ge);
        }
        found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        Effects.Clear();
        Effects.AddRange(found);
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[GameplayEffectRegistry] Auto-Fill: {Effects.Count} efectos registrados.");
    }
#endif
}
