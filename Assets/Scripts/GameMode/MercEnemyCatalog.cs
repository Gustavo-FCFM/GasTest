using System.Collections.Generic;
using UnityEngine;

// ============================================================
// MercEnemyCatalog
//
// La tabla que dice qué PREFAB le corresponde a cada EMercEnemyType. Es el único lugar
// del proyecto donde esa relación existe: los campamentos eligen tipos, no prefabs.
//
// Vive en Assets/Resources/MercEnemyCatalog.asset y se carga sola con Resources.Load,
// el mismo camino que ya usan GameplayAbilityRegistry y GameplayEffectRegistry. Así
// ningún componente necesita una referencia al catálogo en el Inspector — y no hay
// forma de que a un campamento se le olvide asignarlo.
//
// Cambiar el modelo de los fantasmas, o pasar todos los magos a una versión mejorada,
// es cambiar una línea acá y afecta a los nueve campamentos de una.
// ============================================================
[CreateAssetMenu(fileName = "MercEnemyCatalog", menuName = "Mercenarios/Catálogo de enemigos")]
public class MercEnemyCatalog : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public EMercEnemyType Type;

        [Tooltip("Prefab EN RED de ese enemigo (los que crea 'Mercenarios ▸ 7 · Convertir enemigos').")]
        public GameObject Prefab;
    }

    [Tooltip("Un renglón por tipo de enemigo. Los tipos que no estén acá simplemente no aparecen.")]
    public List<Entry> Enemies = new List<Entry>();

    private static MercEnemyCatalog _instance;

    // Acceso global. Si no encuentra el asset, avisa una sola vez y devuelve null: los
    // campamentos siguen funcionando con su prefab de respaldo.
    public static MercEnemyCatalog Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<MercEnemyCatalog>("MercEnemyCatalog");
                if (_instance == null)
                    Debug.LogWarning("[Mercenarios] No hay MercEnemyCatalog en Assets/Resources. " +
                                     "Crealo con 'Mercenarios ▸ 8 · Crear/actualizar el catálogo de enemigos' " +
                                     "o los campamentos van a usar solo su prefab de respaldo.");
            }
            return _instance;
        }
    }

    public GameObject GetPrefab(EMercEnemyType type)
    {
        foreach (Entry e in Enemies)
            if (e.Type == type && e.Prefab != null) return e.Prefab;
        return null;
    }

    public bool Has(EMercEnemyType type) => GetPrefab(type) != null;
}
