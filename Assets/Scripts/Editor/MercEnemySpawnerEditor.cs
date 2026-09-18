using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ============================================================
// MercEnemySpawnerEditor
//
// El inspector de los campamentos. Además de los campos normales, muestra LA MEZCLA
// QUE VA A SALIR en cada momento de la partida.
//
// Por qué hace falta: los pesos sueltos no se leen. "70 y 30" y "7 y 3" son exactamente
// lo mismo, y con tres o cuatro renglones que se desbloquean en distintos minutos, el
// porcentaje real de cada uno ya no se saca de cabeza. Que el inspector lo calcule
// evita el clásico "puse 10 al jefe y me salen jefes todo el tiempo".
// ============================================================
[CustomEditor(typeof(MercEnemySpawner))]
[CanEditMultipleObjects]
public class MercEnemySpawnerEditor : Editor
{
    private readonly List<EMercEnemyType> _types = new List<EMercEnemyType>();
    private readonly List<float> _percents = new List<float>();

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var spawner = (MercEnemySpawner)target;

        EditorGUILayout.Space(8);
        DrawMixPreview(spawner);
        EditorGUILayout.Space(4);
        DrawTools(spawner);
    }

    // =========================================================
    // VISTA PREVIA DE LA MEZCLA
    // =========================================================

    private void DrawMixPreview(MercEnemySpawner spawner)
    {
        EditorGUILayout.LabelField("Qué va a salir de acá", EditorStyles.boldLabel);

        if (MercEnemyCatalog.Instance == null)
        {
            EditorGUILayout.HelpBox(
                "No hay catálogo de enemigos todavía, así que este campamento va a usar su " +
                "prefab de respaldo.\n\nCrealo con  Mercenarios ▸ 8 · Crear o actualizar el " +
                "catálogo de enemigos.", MessageType.Warning);
            return;
        }

        // Los momentos que importan son los minutos en que se desbloquea algo. Mostrar
        // otros sería ruido: entre desbloqueo y desbloqueo la mezcla no cambia.
        foreach (float minute in CollectCheckpoints(spawner))
        {
            spawner.GetMixAt(minute, _types, _percents);

            string label = minute <= 0f ? "Desde el arranque" : $"Desde el minuto {minute:0.#}";

            if (_types.Count == 0)
            {
                EditorGUILayout.LabelField(label, "— nada disponible (usa el prefab de respaldo)");
                continue;
            }

            var line = new System.Text.StringBuilder();
            for (int i = 0; i < _types.Count; i++)
            {
                if (i > 0) line.Append("   ");
                line.Append($"{_types[i]} {_percents[i]:0}%");
            }

            EditorGUILayout.LabelField(label, line.ToString());
        }

        WarnAboutMissingTypes(spawner);
    }

    // Minuto 0 siempre, más cada UnlockMinute distinto de la tabla, ordenados.
    private static List<float> CollectCheckpoints(MercEnemySpawner spawner)
    {
        var checkpoints = new List<float> { 0f };

        if (spawner.SpawnTable != null)
        {
            foreach (MercEnemySpawner.SpawnRule rule in spawner.SpawnTable)
            {
                if (rule == null || rule.UnlockMinute <= 0f) continue;
                if (!checkpoints.Contains(rule.UnlockMinute)) checkpoints.Add(rule.UnlockMinute);
            }
        }

        checkpoints.Sort();
        return checkpoints;
    }

    // Un tipo elegido en la tabla pero que el catálogo no resuelve nunca va a aparecer, y
    // eso desde afuera se ve como "el campamento no saca magos" sin ninguna pista.
    private void WarnAboutMissingTypes(MercEnemySpawner spawner)
    {
        if (spawner.SpawnTable == null) return;

        var missing = new List<string>();
        foreach (MercEnemySpawner.SpawnRule rule in spawner.SpawnTable)
        {
            if (rule == null || rule.Weight <= 0f) continue;
            if (!MercEnemyCatalog.Instance.Has(rule.Type)) missing.Add(rule.Type.ToString());
        }

        if (missing.Count == 0) return;

        EditorGUILayout.HelpBox(
            $"El catálogo no tiene prefab para: {string.Join(", ", missing)}.\n" +
            "Esos tipos NO van a aparecer. Convertilos con el menú 7 y actualizá el catálogo " +
            "con el menú 8.", MessageType.Warning);
    }

    // =========================================================
    // HERRAMIENTAS
    // =========================================================

    private void DrawTools(MercEnemySpawner spawner)
    {
        // Con nueve campamentos, ajustar la curva de dificultad a mano en cada uno es
        // garantía de que queden distintos sin querer.
        if (!GUILayout.Button("Copiar esta tabla a TODOS los campamentos de la escena")) return;

        var all = Object.FindObjectsByType<MercEnemySpawner>(FindObjectsSortMode.None);
        int changed = 0;

        foreach (MercEnemySpawner other in all)
        {
            if (other == null || other == spawner) continue;

            Undo.RecordObject(other, "Copiar tabla de aparición");
            other.SpawnTable = CloneTable(spawner.SpawnTable);
            EditorUtility.SetDirty(other);
            changed++;
        }

        Debug.Log($"[Mercenarios] Tabla copiada a {changed} campamentos. " +
                  "La CANTIDAD de enemigos de cada uno no se toca: eso es propio de cada campamento.");
    }

    // Copia profunda: si se compartieran las mismas instancias, tocar un peso en un
    // campamento los cambiaría todos y sería imposible después hacer uno distinto.
    private static List<MercEnemySpawner.SpawnRule> CloneTable(List<MercEnemySpawner.SpawnRule> source)
    {
        var copy = new List<MercEnemySpawner.SpawnRule>();
        if (source == null) return copy;

        foreach (MercEnemySpawner.SpawnRule rule in source)
        {
            if (rule == null) continue;
            copy.Add(new MercEnemySpawner.SpawnRule
            {
                Type         = rule.Type,
                Weight       = rule.Weight,
                UnlockMinute = rule.UnlockMinute,
                MaxAlive     = rule.MaxAlive,
            });
        }
        return copy;
    }
}
