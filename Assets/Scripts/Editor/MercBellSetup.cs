using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// ============================================================
// MercBellSetup
//
// Herramienta de UNA VEZ (11 de noviembre de 2026) que arma las CAMPANAS de las torres. Después todo
// se edita a mano:
//   1. La malla de la campana (Art/Models/Bell) y su bronce (Art/Materials/Bell; la madera del marco
//      es la de la arena).
//   2. El prefab de la campana (Prefabs/GameMode/Bell): el marco de madera, la campana colgada de su
//      eje, el badajo y la luz.
//   3. Una campana sobre el techo de la torre del sector del Cementerio (los tres sectores la heredan).
//   4. El evento de las campanas (hijo "Bells") en el prefab de los eventos, con GE_Revealed para los
//      invisibles.
//   5. (desde el 11 de noviembre, tarde) Que se pueda GOLPEAR: un ASC y un StrikeTarget en la campana, y una
//      esfera de choque en la capa Character alrededor del bronce. A un prefab ya hecho se lo agrega.
// Lo que ya existe no lo vuelve a crear: correrla dos veces no duplica nada.
// ============================================================
public static class MercBellSetup
{
    private const string ModelsFolder    = "Assets/Art/Models/Bell";
    private const string MaterialsFolder = "Assets/Art/Materials/Bell";
    private const string PrefabFolder    = "Assets/Prefabs/GameMode";

    // Solo atajos: si no están ahí, se buscan por componente o por nombre.
    private const string SectorPrefabPath = "Assets/Prefabs/Map/Graveyard_Sector.prefab";

    private const string LitShader = "Universal Render Pipeline/Lit";

    // Dónde va la campana, en el espacio de la torre: sobre la losa grande del techo (Roof_A, arriba a 8 m),
    // lejos del hueco de la escalera.
    private static readonly Vector3 OnTowerRoof = new Vector3(0f, 8f, -2.3f);

    private static readonly Color Bronze    = new Color(0.55f, 0.37f, 0.17f);
    private static readonly Color ReadyGlow = new Color(1f, 0.72f, 0.35f);

    // Medidas del marco y de la campana (m).
    private const float PostHeight = 3.6f;
    private const float PostGap    = 1.1f;    // del centro a cada poste
    private const float PivotY     = 3.35f;   // el eje del que cuelga
    private const float BellHeight = 1.1f;
    private const float BellMouth  = 0.62f;   // radio de la boca

    [MenuItem("Mercenarios/Campanas/Armar las campanas de las torres")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        var log = new StringBuilder("[Campanas] Armado:\n");

        Mesh bellMesh   = BuildBellMesh(log);
        Material bronze = BuildBronze(log);
        Material wood   = FindAssetIn<Material>("Mat_ArenaWood", "Assets/Art/Materials") ?? bronze;

        GameObject prefab = BuildBellPrefab(bellMesh, bronze, wood, log);
        EnsureStrikeable(prefab, log);
        PlaceBellInSector(prefab, log);
        AddEvent(log);

        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    // =========================================================
    // 1. LA MALLA Y EL BRONCE
    // =========================================================

    // Una campana por revolución de un perfil: cerrada arriba, con hombro, cintura y labio abierto.
    private static Mesh BuildBellMesh(StringBuilder log)
    {
        string path = $"{ModelsFolder}/Bell.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;
        EnsureFolder(ModelsFolder);

        // (radio, altura desde arriba hacia abajo) por fuera; adentro se repite más chico.
        var outer = new List<Vector2>
        {
            new Vector2(0.00f, 0.00f),
            new Vector2(0.22f, 0.00f),
            new Vector2(0.32f, -0.06f),
            new Vector2(0.36f, -0.22f),
            new Vector2(0.37f, -0.50f),
            new Vector2(0.41f, -0.78f),
            new Vector2(0.52f, -0.98f),
            new Vector2(BellMouth, -BellHeight),
        };
        var inner = new List<Vector2>
        {
            new Vector2(BellMouth - 0.05f, -BellHeight + 0.01f),
            new Vector2(0.45f, -0.96f),
            new Vector2(0.35f, -0.76f),
            new Vector2(0.32f, -0.48f),
            new Vector2(0.30f, -0.20f),
            new Vector2(0.18f, -0.08f),
            new Vector2(0.00f, -0.08f),
        };
        var profile = new List<Vector2>(outer);
        profile.AddRange(inner);

        const int segments = 32;
        var verts = new List<Vector3>();
        var uvs   = new List<Vector2>();
        var tris  = new List<int>();

        // Cada tramo del perfil con sus propios vértices: las aristas quedan duras donde el perfil dobla.
        for (int p = 0; p < profile.Count - 1; p++)
        {
            Vector2 a = profile[p], b = profile[p + 1];
            int start = verts.Count;
            for (int s = 0; s <= segments; s++)
            {
                float ang = s / (float)segments * Mathf.PI * 2f;
                float c = Mathf.Cos(ang), n = Mathf.Sin(ang);
                verts.Add(new Vector3(a.x * c, a.y, a.x * n));
                verts.Add(new Vector3(b.x * c, b.y, b.x * n));
                uvs.Add(new Vector2(s / (float)segments, p / (float)profile.Count));
                uvs.Add(new Vector2(s / (float)segments, (p + 1) / (float)profile.Count));
            }
            for (int s = 0; s < segments; s++)
            {
                int i = start + s * 2;
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
                tris.Add(i + 1); tris.Add(i + 2); tris.Add(i + 3);
            }
        }

        var mesh = new Mesh { name = "Bell" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();

        AssetDatabase.CreateAsset(mesh, path);
        log.AppendLine($"· Malla de la campana: {path}.");
        return mesh;
    }

    private static Material BuildBronze(StringBuilder log)
    {
        string path = $"{MaterialsFolder}/Mat_BellBronze.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        EnsureFolder(MaterialsFolder);

        var mat = new Material(Shader.Find(LitShader)) { name = "Mat_BellBronze" };
        mat.SetColor("_BaseColor", Bronze);
        mat.SetFloat("_Metallic", 0.85f);
        mat.SetFloat("_Smoothness", 0.55f);
        mat.SetFloat("_Cull", (float)CullMode.Off);   // se ve por dentro al mirar desde abajo

        // La emisión prendida (en negro): MatchBell la pinta cuando la campana está lista.
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", Color.black);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

        AssetDatabase.CreateAsset(mat, path);
        log.AppendLine($"· Bronce: {path}.");
        return mat;
    }

    // =========================================================
    // 2. EL PREFAB
    // =========================================================

    private static GameObject BuildBellPrefab(Mesh bellMesh, Material bronze, Material wood, StringBuilder log)
    {
        GameObject existing = FindPrefabWith<MatchBell>();
        if (existing != null)
        {
            log.AppendLine($"· Campana: ya existía ({AssetDatabase.GetAssetPath(existing)}).");
            return existing;
        }
        EnsureFolder(PrefabFolder);

        Mesh cube   = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        Mesh sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");

        var root = new GameObject("Bell");
        var bell = root.AddComponent<MatchBell>();

        // --- el marco: dos postes y el travesaño, sólidos ---
        Transform frame = NewChild(root.transform, "Frame");
        Box(frame, "Post_L", cube, wood, new Vector3(-PostGap, PostHeight * 0.5f, 0f), new Vector3(0.24f, PostHeight, 0.24f), solid: true);
        Box(frame, "Post_R", cube, wood, new Vector3( PostGap, PostHeight * 0.5f, 0f), new Vector3(0.24f, PostHeight, 0.24f), solid: true);
        Box(frame, "Beam",   cube, wood, new Vector3(0f, PivotY + 0.12f, 0f), new Vector3(PostGap * 2f + 0.5f, 0.24f, 0.3f), solid: true);
        Box(frame, "Foot_L", cube, wood, new Vector3(-PostGap, 0.08f, 0f), new Vector3(0.36f, 0.16f, 1.0f), solid: true);
        Box(frame, "Foot_R", cube, wood, new Vector3( PostGap, 0.08f, 0f), new Vector3(0.36f, 0.16f, 1.0f), solid: true);

        // --- lo que se balancea: la campana y el badajo, colgados del eje ---
        Transform swing = NewChild(root.transform, "Swing");
        swing.localPosition = new Vector3(0f, PivotY, 0f);

        GameObject body = MeshChild(swing, "BellBody", bellMesh, bronze, shadows: true);
        body.transform.localPosition = Vector3.zero;

        GameObject yoke = Box(swing, "Yoke", cube, bronze, new Vector3(0f, 0.03f, 0f), new Vector3(0.18f, 0.12f, 0.18f), solid: false);
        yoke.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;

        GameObject rod = Box(swing, "Clapper_Rod", cube, bronze, new Vector3(0f, -0.5f, 0f), new Vector3(0.04f, 0.8f, 0.04f), solid: false);
        rod.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        GameObject ball = MeshChild(swing, "Clapper", sphere, bronze, shadows: false);
        ball.transform.localPosition = new Vector3(0f, -0.92f, 0f);
        ball.transform.localScale    = Vector3.one * 0.16f;

        // --- la luz: prendida mientras se puede tocar ---
        Transform glow = NewChild(root.transform, "Glow");
        glow.localPosition = new Vector3(0f, PivotY - BellHeight * 0.5f, 0f);
        var light = glow.gameObject.AddComponent<Light>();
        light.type      = LightType.Point;
        light.color     = ReadyGlow;
        light.range     = 6f;
        light.intensity = 1.5f;
        light.shadows   = LightShadows.None;
        light.enabled   = false;   // la prende MatchBell

        bell.Swing        = swing;
        bell.BellRenderer = body.GetComponent<Renderer>();
        bell.Glow         = light;
        bell.ReadyColor   = ReadyGlow;

        string path = $"{PrefabFolder}/Bell.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        log.AppendLine($"· Campana creada: {path}.");
        return prefab;
    }

    private static Transform NewChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static GameObject MeshChild(Transform parent, string name, Mesh mesh, Material material, bool shadows)
    {
        Transform t = NewChild(parent, name);
        t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = t.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial    = material;
        renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        return t.gameObject;
    }

    // solid = con colisión (parte del mapa).
    private static GameObject Box(Transform parent, string name, Mesh cube, Material material, Vector3 position, Vector3 size, bool solid)
    {
        GameObject go = MeshChild(parent, name, cube, material, shadows: solid);
        go.transform.localPosition = position;
        go.transform.localScale    = size;
        if (solid) go.AddComponent<BoxCollider>();
        return go;
    }

    // =========================================================
    // 3. LA CAMPANA EN LA TORRE DEL SECTOR
    // =========================================================

    private static void PlaceBellInSector(GameObject bellPrefab, StringBuilder log)
    {
        string path = FindPrefabPath("Graveyard_Sector", SectorPrefabPath);
        if (path == null) { log.AppendLine("  ¡Ojo! No encontré el prefab Graveyard_Sector: la campana no quedó en el mapa."); return; }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.GetComponentInChildren<MatchBell>(true) != null)
            {
                log.AppendLine("· El sector ya tenía su campana.");
                return;
            }

            // Hija de la torre: si se mueve la torre, la campana va con ella.
            Transform tower = root.transform.Find("Tower");
            Transform parent = tower != null ? tower : root.transform;
            if (tower == null) log.AppendLine("  ¡Ojo! El sector no tiene un hijo 'Tower': la campana quedó en la raíz, acomodarla a mano.");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(bellPrefab, parent);
            instance.name = "Bell";
            instance.transform.localPosition = tower != null ? OnTowerRoof : Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            PrefabUtility.SaveAsPrefabAsset(root, path);
            log.AppendLine($"· Campana puesta en {path}, sobre el techo de la torre: una por sector, tres en el mapa.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // =========================================================
    // 4. EL EVENTO
    // =========================================================

    private static void AddEvent(StringBuilder log)
    {
        GameObject eventsPrefab = FindPrefabWith<GameModeEventManager>();
        if (eventsPrefab == null)
        {
            log.AppendLine("  ¡Ojo! No encontré el prefab de los eventos (GameModeEventManager): correr antes " +
                           "Mercenarios ▸ Santuarios ▸ Armar santuarios y eventos de partida.");
            return;
        }
        string path = AssetDatabase.GetAssetPath(eventsPrefab);

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.GetComponentInChildren<MatchEvent_Bells>(true) != null)
            {
                log.AppendLine("· El prefab de los eventos ya tenía las campanas.");
                return;
            }

            var bells = NewChild(root.transform, "Bells").gameObject.AddComponent<MatchEvent_Bells>();
            bells.StartAt               = 0f;
            bells.RevealRadius          = 25f;
            bells.RevealSeconds         = 4f;
            bells.CooldownSeconds       = 45f;
            bells.InvisibleRevealEffect = FindAssetIn<GameplayEffect>("GE_Revealed", "Assets/Effects");
            if (bells.InvisibleRevealEffect == null)
                log.AppendLine("  ¡Ojo! No encontré GE_Revealed: los invisibles no se van a revelar (cargarlo a mano).");

            PrefabUtility.SaveAsPrefabAsset(root, path);
            log.AppendLine($"· Evento de las campanas agregado a {path} ▸ Bells (desde el segundo 0, revela 25 m por 4 s, descansa 45 s).");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // =========================================================
    // 5. QUE SE PUEDA GOLPEAR
    // =========================================================

    private static void EnsureStrikeable(GameObject prefab, StringBuilder log)
    {
        if (prefab == null) return;
        string path = AssetDatabase.GetAssetPath(prefab);

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.GetComponent<StrikeTarget>() != null)
            {
                log.AppendLine("· La campana ya se podía golpear.");
                return;
            }

            // El ASC (neutral, sin atributos) es lo que buscan los ataques; el StrikeTarget hace que no sea
            // enemigo ni aliado de nadie y que los golpes le avisen en vez de hacerle daño.
            var asc = root.GetComponent<AbilitySystemComponent>();
            if (asc == null) asc = root.AddComponent<AbilitySystemComponent>();
            asc.TeamID = 0;
            root.AddComponent<StrikeTarget>();

            // La esfera de choque cuelga del eje, así se balancea con el bronce.
            MatchBell bell = root.GetComponent<MatchBell>();
            Transform swing = bell != null && bell.Swing != null ? bell.Swing : root.transform;
            var hit = new GameObject("HitBox");
            hit.transform.SetParent(swing, false);
            hit.transform.localPosition = new Vector3(0f, -BellHeight * 0.5f, 0f);
            int layer = LayerMask.NameToLayer("Character");
            hit.layer = layer >= 0 ? layer : 7;
            var sphere = hit.AddComponent<SphereCollider>();
            sphere.radius = 0.6f;

            PrefabUtility.SaveAsPrefabAsset(root, path);
            log.AppendLine($"· La campana ya se puede golpear ({path}: ASC, StrikeTarget y HitBox en la capa Character).");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // =========================================================
    // BÚSQUEDAS (por componente o por nombre; la ruta es solo un atajo)
    // =========================================================

    // Por nombre, solo dentro de una carpeta (por los nombres repetidos).
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

    private static string FindPrefabPath(string name, string shortcut)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(shortcut) != null) return shortcut;
        GameObject found = FindAssetIn<GameObject>(name, "Assets/Prefabs");
        return found != null ? AssetDatabase.GetAssetPath(found) : null;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
