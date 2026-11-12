using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// ============================================================
// MercOrbSetup
//
// Herramienta de UNA VEZ (11 de noviembre de 2026) que arma los ORBES DE DEFINITIVA. Después todo se
// edita a mano:
//   1. Los materiales (Art/Materials/Orb): el núcleo negro, la cáscara blanca casi transparente (en espacio lineal, más alfa aclara el núcleo) y el anillo.
//   2. El modelo del orbe (Prefabs/GameMode/UltOrb): solo se ve, no tiene lógica (flota con FloatingVisual).
//   3. Un lugar para orbe (OrbSpot) en cada sarcófago del sector del Cementerio (los "Tomb_*"): 8 por
//      sector, 24 en el mapa.
//   4. El evento de los orbes (hijo "Orbs") en el prefab de los eventos, con el modelo cargado.
// Lo que ya existe no lo vuelve a crear: correrla dos veces no duplica nada.
// ============================================================
public static class MercOrbSetup
{
    private const string ModelsFolder    = "Assets/Art/Models/Orb";
    private const string MaterialsFolder = "Assets/Art/Materials/Orb";
    private const string PrefabFolder    = "Assets/Prefabs/GameMode";

    // Solo un atajo: si no está ahí, se busca por nombre.
    private const string SectorPrefabPath = "Assets/Prefabs/Map/Graveyard_Sector.prefab";

    // Los sarcófagos del sector se llaman así.
    private const string TombPrefix = "Tomb_";

    private const string UnlitShader = "Universal Render Pipeline/Unlit";

    [MenuItem("Mercenarios/Orbes/Armar los orbes de definitiva")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        var log = new StringBuilder("[Orbes] Armado:\n");

        GameObject orb = BuildOrbPrefab(log);
        PlaceSpotsInSector(log);
        AddEvent(orb, log);

        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    // =========================================================
    // 1 y 2. LOS MATERIALES Y EL MODELO
    // =========================================================

    private static GameObject BuildOrbPrefab(StringBuilder log)
    {
        GameObject existing = FindAssetIn<GameObject>("UltOrb", PrefabFolder);
        if (existing != null)
        {
            log.AppendLine($"· Orbe: ya existía ({AssetDatabase.GetAssetPath(existing)}).");
            return existing;
        }
        EnsureFolder(PrefabFolder);

        Material core = MakeMaterial("Mat_OrbCore", m => m.SetColor("_BaseColor", new Color(0.02f, 0.02f, 0.03f, 1f)));
        Material shell = MakeMaterial("Mat_OrbShell", m =>
        {
            m.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.08f));
            MakeTransparent(m);
        });
        Material ring = MakeMaterial("Mat_OrbRing", m =>
        {
            m.SetColor("_BaseColor", new Color(0.95f, 0.95f, 1f, 1f));
            m.SetFloat("_Cull", (float)CullMode.Off);   // el anillo se ve de los dos lados
        });
        Mesh torus = BuildTorus(log);
        Mesh sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");

        var root = new GameObject("UltOrb");
        var floating = root.AddComponent<FloatingVisual>();
        floating.BobHeight = 0.12f;
        floating.BobSpeed  = 0.5f;
        floating.SwayAngle = 25f;
        floating.SwaySpeed = 0.6f;
        floating.LeanAngle = 0f;

        MeshChild(root.transform, "Core",  sphere, core,  Vector3.zero, Vector3.one * 0.32f, Quaternion.identity);
        MeshChild(root.transform, "Shell", sphere, shell, Vector3.zero, Vector3.one * 0.55f, Quaternion.identity);
        MeshChild(root.transform, "Ring_A", torus, ring, Vector3.zero, Vector3.one, Quaternion.Euler(70f, 0f, 20f));
        MeshChild(root.transform, "Ring_B", torus, ring, Vector3.zero, Vector3.one * 0.85f, Quaternion.Euler(-60f, 40f, 0f));

        var light = new GameObject("Glow").AddComponent<Light>();
        light.transform.SetParent(root.transform, false);
        light.type      = LightType.Point;
        light.color     = Color.white;
        light.range     = 3.5f;
        light.intensity = 1.2f;
        light.shadows   = LightShadows.None;

        string path = $"{PrefabFolder}/UltOrb.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        log.AppendLine($"· Orbe creado: {path} (núcleo negro, cáscara blanca y dos anillos).");
        return prefab;
    }

    // Un anillo fino de 0.42 m de radio.
    private static Mesh BuildTorus(StringBuilder log)
    {
        string path = $"{ModelsFolder}/OrbRing.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;
        EnsureFolder(ModelsFolder);

        const float radius = 0.42f, tube = 0.018f;
        const int around = 48, sides = 8;
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var tris = new List<int>();

        for (int i = 0; i <= around; i++)
        {
            float a = i / (float)around * Mathf.PI * 2f;
            Vector3 center = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            Vector3 outward = center.normalized;
            for (int j = 0; j <= sides; j++)
            {
                float b = j / (float)sides * Mathf.PI * 2f;
                Vector3 n = outward * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                verts.Add(center + n * tube);
                normals.Add(n);
            }
        }
        for (int i = 0; i < around; i++)
        for (int j = 0; j < sides; j++)
        {
            int a = i * (sides + 1) + j, b = a + sides + 1;
            tris.Add(a); tris.Add(a + 1); tris.Add(b);
            tris.Add(b); tris.Add(a + 1); tris.Add(b + 1);
        }

        var mesh = new Mesh { name = "OrbRing" };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        log.AppendLine($"· Malla del anillo: {path}.");
        return mesh;
    }

    private static Material MakeMaterial(string name, System.Action<Material> setup)
    {
        EnsureFolder(MaterialsFolder);
        string path = $"{MaterialsFolder}/{name}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var mat = new Material(Shader.Find(UnlitShader)) { name = name };
        setup(mat);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // Lo mismo que elegir "Transparent" en el Surface Type del inspector de URP.
    private static void MakeTransparent(Material mat)
    {
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_AlphaClip", 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.renderQueue = (int)RenderQueue.Transparent;
    }

    private static void MeshChild(Transform parent, string name, Mesh mesh, Material material,
                                  Vector3 position, Vector3 scale, Quaternion rotation)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = rotation;
        go.transform.localScale    = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial    = material;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows    = false;
        r.lightProbeUsage   = LightProbeUsage.Off;
    }

    // =========================================================
    // 3. UN LUGAR EN CADA SARCÓFAGO
    // =========================================================

    private static void PlaceSpotsInSector(StringBuilder log)
    {
        string path = AssetDatabase.LoadAssetAtPath<GameObject>(SectorPrefabPath) != null
            ? SectorPrefabPath : AssetDatabase.GetAssetPath(FindAssetIn<GameObject>("Graveyard_Sector", "Assets/Prefabs"));
        if (string.IsNullOrEmpty(path)) { log.AppendLine("  ¡Ojo! No encontré el prefab Graveyard_Sector: no hay lugares para orbes."); return; }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            int added = 0, already = 0;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith(TombPrefix)) continue;
                if (t.GetComponentInChildren<OrbSpot>(true) != null) { already++; continue; }

                var spot = new GameObject("OrbSpot");
                spot.transform.SetParent(t, false);
                spot.transform.localPosition = Vector3.zero;
                spot.AddComponent<OrbSpot>();
                added++;
            }

            if (added > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
            if (added + already == 0)
                log.AppendLine($"  ¡Ojo! No hay ningún \"{TombPrefix}*\" en {path}: no quedaron lugares para orbes.");
            else
                log.AppendLine($"· Lugares para orbes en {path}: {added} nuevos ({already} ya estaban), uno por sarcófago; tres sectores.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // =========================================================
    // 4. EL EVENTO
    // =========================================================

    private static void AddEvent(GameObject orb, StringBuilder log)
    {
        GameObject eventsPrefab = FindPrefabWith<GameModeEventManager>();
        if (eventsPrefab == null)
        {
            log.AppendLine("  ¡Ojo! No encontré el prefab de los eventos (GameModeEventManager).");
            return;
        }
        string path = AssetDatabase.GetAssetPath(eventsPrefab);

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.GetComponentInChildren<MatchEvent_Orbs>(true) != null)
            {
                log.AppendLine("· El prefab de los eventos ya tenía los orbes.");
                return;
            }

            var go = new GameObject("Orbs");
            go.transform.SetParent(root.transform, false);
            var orbs = go.AddComponent<MatchEvent_Orbs>();
            orbs.StartAt        = 45f;
            orbs.SecondsBetween = 60f;
            orbs.MaxActive      = 3;
            orbs.TakeRadius     = 2.5f;
            orbs.ChannelSeconds = 1.5f;
            orbs.UltimateCharge = 0.25f;
            orbs.OrbVisual      = orb;

            PrefabUtility.SaveAsPrefabAsset(root, path);
            log.AppendLine($"· Evento de los orbes agregado a {path} ▸ Orbs (a los 45 s y cada 60 s, hasta 3; canalizar 1.5 s; 25 % de definitiva).");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // =========================================================
    // BÚSQUEDAS
    // =========================================================

    private static T FindAssetIn<T>(string name, string folder) where T : Object
    {
        foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name} {name}"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith(folder) && Path.GetFileNameWithoutExtension(path) == name)
                return AssetDatabase.LoadAssetAtPath<T>(path);
        }
        return null;
    }

    private static GameObject FindPrefabWith<T>() where T : Component
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab != null && prefab.GetComponent<T>() != null) return prefab;
        }
        return null;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
