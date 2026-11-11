using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// ============================================================
// BlenderModelImport
//
// Trae los modelos de clase hechos en Blender (10 de noviembre de 2026, empezando por la Paladina).
// Blender/merc_export.py deja en Blender/Out/<Clase>/ la malla en la pose T (en el espacio del cuerpo del
// jugador), los pesos por NOMBRE de hueso y las dos texturas ya horneadas. Acá se arma la malla con los
// bindposes del jugador y se guarda igual que las recetas (ClassModelKit.SaveBaked): mismas rutas, mismos
// GUID, así el campo "Modelo" de la clase sigue apuntando al prefab Model_<Clase>.
//
// No pasa por FBX: el importador de FBX de Blender cambia la orientación de los huesos, y ClassModelSwap
// pone la malla sobre los huesos del jugador con los bindposes que trae. Con los del jugador no hay
// nada que pueda quedar corrido.
//
// Antes de guardar compara dónde cree Blender que está cada hueso con dónde está en el jugador: si no
// coinciden, el modelo saldría corrido o retorcido, y no se guarda.
// ============================================================
public static class BlenderModelImport
{
    private const string ArtRoot = "Assets/Art/Models/Classes";
    private const string PrefabFolder = "Assets/Prefabs/Player/Classes";

    // Hasta cuánto puede no coincidir un hueso (m) antes de negarse a importar.
    private const float BoneTolerance = 0.01f;

    [Serializable]
    private class ModelData
    {
        public string name;
        public string finish;
        public float glowIntensity;
        public string[] boneNames;
        public float[] bonePositions;
        public float[] positions;
        public float[] normals;
        public float[] uvs;
        public int[] boneIndices;
        public float[] boneWeights;
        public int[] triangles;
    }

    [MenuItem("Mercenarios/Modelos de clase/Importar los modelos de Blender")]
    public static void ImportAll()
    {
        string root = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Blender", "Out");
        if (!Directory.Exists(root))
        {
            Debug.LogWarning($"[BlenderModelImport] No existe {root}: primero hay que exportar desde Blender (ver Blender/LEEME.md).");
            return;
        }

        int count = 0;
        foreach (string folder in Directory.GetDirectories(root))
        {
            string json = Path.Combine(folder, Path.GetFileName(folder) + ".json");
            if (!File.Exists(json)) continue;
            if (Import(json)) count++;
        }
        Debug.Log($"[BlenderModelImport] Importados: {count}.");
    }

    private static bool Import(string jsonPath)
    {
        string folder = Path.GetDirectoryName(jsonPath);
        ModelData data = JsonUtility.FromJson<ModelData>(File.ReadAllText(jsonPath));
        string name = data.name;

        var kit = new ClassModelKit();

        // Los huesos de Blender, por nombre, a los del jugador
        var map = new int[data.boneNames.Length];
        float worst = 0f;
        string worstName = "";
        for (int i = 0; i < data.boneNames.Length; i++)
        {
            string bone = data.boneNames[i];
            if (!kit.HasBone(bone))
            {
                Debug.LogError($"[BlenderModelImport] {name}: el hueso '{bone}' no está en el jugador. No se importó.");
                return false;
            }
            map[i] = kit.BoneIndex(bone);

            var theirs = new Vector3(data.bonePositions[i * 3], data.bonePositions[i * 3 + 1], data.bonePositions[i * 3 + 2]);
            float off = (kit.Pos(bone) - theirs).magnitude;
            if (off > worst)
            {
                worst = off;
                worstName = bone;
            }
        }
        if (worst > BoneTolerance)
        {
            Debug.LogError($"[BlenderModelImport] {name}: '{worstName}' está a {worst * 100f:0.0} cm de donde lo tiene el jugador. " +
                           "¿El esqueleto de Blender es el del Dummy? No se importó.");
            return false;
        }

        int vertexCount = data.positions.Length / 3;
        var positions = new Vector3[vertexCount];
        var normals = new Vector3[vertexCount];
        var uvs = new Vector2[vertexCount];
        var weights = new BoneWeight[vertexCount];
        for (int v = 0; v < vertexCount; v++)
        {
            positions[v] = new Vector3(data.positions[v * 3], data.positions[v * 3 + 1], data.positions[v * 3 + 2]);
            Vector3 n = new Vector3(data.normals[v * 3], data.normals[v * 3 + 1], data.normals[v * 3 + 2]);
            // Una normal en cero se ve bien en la vista previa pero en juego sale blanca (ver CLAUDE.md)
            normals[v] = n.sqrMagnitude > 1e-12f ? n / n.magnitude : Vector3.up;
            uvs[v] = new Vector2(data.uvs[v * 2], data.uvs[v * 2 + 1]);
            weights[v] = Weight(data, map, v);
        }

        string atlasPath = Path.Combine(folder, $"{name}_Atlas.png");
        string glowPath = Path.Combine(folder, $"{name}_Glow.png");
        byte[] atlas = File.ReadAllBytes(atlasPath);
        byte[] glow = data.glowIntensity > 0f && File.Exists(glowPath) ? File.ReadAllBytes(glowPath) : null;
        ModelFinish finish = data.finish == "Polished" ? ModelFinish.Polished : ModelFinish.Matte;

        kit.SaveBaked(name, $"{ArtRoot}/{name}", PrefabFolder, mesh =>
        {
            mesh.indexFormat = vertexCount > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = positions;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = data.triangles;
            mesh.boneWeights = weights;
        }, atlas, glow, finish, data.glowIntensity);

        Debug.Log($"[BlenderModelImport] {name}: los huesos coinciden (el que más se aleja, '{worstName}', a {worst * 1000f:0.0} mm).");
        return true;
    }

    // Los cuatro pesos de ese vértice, de mayor a menor, sumando 1.
    private static BoneWeight Weight(ModelData data, int[] map, int v)
    {
        var list = new List<KeyValuePair<int, float>>(4);
        for (int k = 0; k < 4; k++)
        {
            int bone = data.boneIndices[v * 4 + k];
            float w = data.boneWeights[v * 4 + k];
            if (bone >= 0 && w > 0f) list.Add(new KeyValuePair<int, float>(map[bone], w));
        }
        list.Sort((a, b) => b.Value.CompareTo(a.Value));

        float total = 0f;
        foreach (var p in list) total += p.Value;
        if (total <= 0f) return new BoneWeight { boneIndex0 = map.Length > 0 ? map[0] : 0, weight0 = 1f };

        var bw = new BoneWeight();
        if (list.Count > 0) { bw.boneIndex0 = list[0].Key; bw.weight0 = list[0].Value / total; }
        if (list.Count > 1) { bw.boneIndex1 = list[1].Key; bw.weight1 = list[1].Value / total; }
        if (list.Count > 2) { bw.boneIndex2 = list[2].Key; bw.weight2 = list[2].Value / total; }
        if (list.Count > 3) { bw.boneIndex3 = list[3].Key; bw.weight3 = list[3].Value / total; }
        return bw;
    }
}
