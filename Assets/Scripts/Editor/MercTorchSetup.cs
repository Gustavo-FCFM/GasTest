using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

// ============================================================
// MercTorchSetup
//
// Herramienta de UNA VEZ (12 de noviembre de 2026, pedido de Gustavo): las ANTORCHAS del Cementerio.
//   1. El modelo (Prefabs/Map/Torch): poste de madera, copa de hierro y una llama de dos capas que brilla
//      sola, con una luz puntual cálida que titila (TorchFlicker). Sin partículas ni sombras: son muchas.
//   2. Las reparte DENTRO del prefab del sector (Graveyard_Sector ▸ Torches), así salen en los tres. Busca
//      sobre el NavMesh puntos planos, con lugar arriba y una pared al lado (una antorcha suelta en medio de
//      un camino estorba), separados entre sí. Arriba muchas; bajo tierra (las catacumbas y la cripta) pocas.
// Necesita la escena del Cementerio abierta (mide sobre el sector "Sector_Team1"); no la modifica.
// Después se mueven, se borran o se copian a mano. Si el sector ya tiene antorchas, no las vuelve a poner.
// El proyecto usa Forward+ (PC_Renderer): no hay tope de luces por objeto.
// ============================================================
public static class MercTorchSetup
{
    private const string ModelsFolder    = "Assets/Art/Models/Torch";
    private const string MaterialsFolder = "Assets/Art/Materials/Torch";
    private const string TorchPath       = "Assets/Prefabs/Map/Torch.prefab";
    private const string SectorPath      = "Assets/Prefabs/Map/Graveyard_Sector.prefab";

    // Cuántas y qué tan separadas, por sector.
    private const int   SurfaceMax       = 70;    // pegadas a una pared
    private const int   OpenMax          = 25;    // sueltas en lo abierto
    private const float OpenSpacing      = 10f;   // las sueltas, en lo abierto (la plaza del centro)
    private const float SurfaceSpacing   = 5.5f;
    private const int   UndergroundMax   = 4;
    private const float UndergroundSpacing = 10f;

    [MenuItem("Mercenarios/Mapa/Armar y repartir las antorchas del Cementerio")]
    public static void RunMenu() => Debug.Log(Run());

    // Vuelve a repartirlas desde cero (borra las del sector): para cambiar cuántas hay. Pisa lo movido a mano.
    public static string Rebuild() => Run(rebuild: true);

    public static string Run(bool rebuild = false)
    {
        var log = new StringBuilder("[Antorchas] Armado:\n");
        GameObject torch = BuildTorch(log);
        PlaceTorches(torch, rebuild, log);
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    // =========================================================
    // EL MODELO
    // =========================================================

    private static GameObject BuildTorch(StringBuilder log)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(TorchPath);
        if (existing != null) { log.AppendLine("· La antorcha ya existía."); return existing; }

        EnsureFolder(ModelsFolder);
        EnsureFolder(MaterialsFolder);

        Mesh pole  = SaveMesh(Prism(8, 0.055f, 0.045f, 0f, 1.85f), "Torch_Pole");
        Mesh cup   = SaveMesh(Prism(8, 0.07f, 0.16f, 1.85f, 2.05f, 0.04f), "Torch_Cup");
        Mesh flame = SaveMesh(Flame(), "Torch_Flame");

        Material wood  = Lit("Mat_TorchWood", new Color(0.33f, 0.21f, 0.12f), 0.15f, 0f);
        Material iron  = Lit("Mat_TorchIron", new Color(0.18f, 0.18f, 0.2f), 0.45f, 0.7f);
        Material outer = Unlit("Mat_TorchFlame", new Color(1f, 0.45f, 0.08f));
        Material inner = Unlit("Mat_TorchFlameCore", new Color(1f, 0.85f, 0.35f));

        var root = new GameObject("Torch");
        Part(root.transform, "Pole", pole, wood, true);
        Part(root.transform, "Cup", cup, iron, true);

        Transform flameRoot = new GameObject("Flame").transform;
        flameRoot.SetParent(root.transform, false);
        flameRoot.localPosition = new Vector3(0f, 2.0f, 0f);
        Part(flameRoot, "Outer", flame, outer, false);
        Transform core = Part(flameRoot, "Core", flame, inner, false).transform;
        core.localScale = new Vector3(0.6f, 0.7f, 0.6f);
        core.localPosition = new Vector3(0f, 0.02f, 0f);

        Transform glowT = new GameObject("Light").transform;
        glowT.SetParent(root.transform, false);
        glowT.localPosition = new Vector3(0f, 2.3f, 0f);
        Light light = glowT.gameObject.AddComponent<Light>();
        light.type      = LightType.Point;
        light.color     = new Color(1f, 0.62f, 0.3f);
        light.range     = 10f;
        light.intensity = 3.5f;
        light.shadows   = LightShadows.None;

        var flicker = root.AddComponent<TorchFlicker>();
        flicker.Glow  = light;
        flicker.Flame = flameRoot;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, TorchPath);
        Object.DestroyImmediate(root);
        log.AppendLine($"· Antorcha creada: {TorchPath}.");
        return prefab;
    }

    private static GameObject Part(Transform parent, string name, Mesh mesh, Material mat, bool solid)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial    = mat;
        r.shadowCastingMode = solid ? ShadowCastingMode.On : ShadowCastingMode.Off;
        r.receiveShadows    = solid;
        return go;
    }

    // Prisma de caras planas (radio r0 abajo, r1 arriba), con tapa arriba y abajo. "inset" hunde la tapa de
    // arriba (la copa queda hueca a la vista).
    private static Mesh Prism(int sides, float r0, float r1, float y0, float y1, float inset = 0f)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
        void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 nn = Vector3.Cross(b - a, c - a);
            nn = nn.sqrMagnitude > 1e-12f ? nn / nn.magnitude : Vector3.up;
            foreach (Vector3 p in new[] { a, b, c }) { t.Add(v.Count); v.Add(p); n.Add(nn); }
        }
        Vector3 Ring(float a, float r, float y) => new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
        var top = new Vector3(0f, y1 - inset, 0f); var bottom = new Vector3(0f, y0, 0f);
        for (int i = 0; i < sides; i++)
        {
            float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
            Vector3 b0 = Ring(a0, r0, y0), b1 = Ring(a1, r0, y0), t0 = Ring(a0, r1, y1), t1 = Ring(a1, r1, y1);
            Tri(b0, t0, t1); Tri(b0, t1, b1);
            Tri(top, t1, t0);
            Tri(bottom, b0, b1);
        }
        return NewMesh(v, n, t);
    }

    // La llama: una gota de seis caras, ancha abajo y en punta arriba.
    private static Mesh Flame()
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
        const int sides = 6;
        var tip = new Vector3(0f, 0.5f, 0f); var bottom = new Vector3(0f, 0f, 0f);
        for (int i = 0; i < sides; i++)
        {
            float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
            Vector3 e0 = new Vector3(Mathf.Cos(a0) * 0.13f, 0.12f, Mathf.Sin(a0) * 0.13f);
            Vector3 e1 = new Vector3(Mathf.Cos(a1) * 0.13f, 0.12f, Mathf.Sin(a1) * 0.13f);
            foreach (var tri in new[] { (e0, tip, e1), (e0, e1, bottom) })
            {
                Vector3 nn = Vector3.Cross(tri.Item2 - tri.Item1, tri.Item3 - tri.Item1).normalized;
                foreach (Vector3 p in new[] { tri.Item1, tri.Item2, tri.Item3 }) { t.Add(v.Count); v.Add(p); n.Add(nn); }
            }
        }
        return NewMesh(v, n, t);
    }

    private static Mesh NewMesh(List<Vector3> v, List<Vector3> n, List<int> t)
    {
        var m = new Mesh();
        m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0); m.RecalculateBounds();
        return m;
    }

    private static Mesh SaveMesh(Mesh mesh, string name)
    {
        string path = $"{ModelsFolder}/{name}.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;
        mesh.name = name;
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static Material Lit(string name, Color color, float smoothness, float metallic)
    {
        string path = $"{MaterialsFolder}/{name}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", metallic);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static Material Unlit(string name, Color color)
    {
        string path = $"{MaterialsFolder}/{name}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
        mat.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // =========================================================
    // REPARTIRLAS
    // =========================================================

    private struct Spot { public Vector3 Local; public bool Underground; public float Score; }

    private static void PlaceTorches(GameObject torch, bool rebuild, StringBuilder log)
    {
        // Se mide sobre un sector de la escena abierta (NavMesh y paredes reales) y se guarda en el prefab.
        Transform sector = null;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t.name == "Sector_Team1") { sector = t; break; }
        if (sector == null) { log.AppendLine("  ¡Ojo! Abre la escena del Cementerio: no encontré Sector_Team1."); return; }

        GameObject root = PrefabUtility.LoadPrefabContents(SectorPath);
        try
        {
            Transform old = root.transform.Find("Torches");
            if (old != null && !rebuild) { log.AppendLine("· El sector ya tenía antorchas."); return; }
            if (old != null) Object.DestroyImmediate(old.gameObject);

            Physics.SyncTransforms();
            int wallMask = ~((1 << 7) | (1 << 5) | (1 << 2));   // todo menos personajes, UI e Ignore Raycast
            var candidates = new List<Spot>();
            var seen = new HashSet<Vector3Int>();
            float[] heights = { 8.2f, 4.2f, 0.3f, -5.8f };

            for (float x = -90f; x <= 90f; x += 2f)
            for (float z = -90f; z <= 90f; z += 2f)
            {
                // Solo la cuña de este sector (los otros dos la repiten girada).
                float angle = Mathf.Atan2(x, z) * Mathf.Rad2Deg;
                if (angle < -60f || angle >= 60f || x * x + z * z < 16f) continue;

                foreach (float y in heights)
                {
                    Vector3 world = sector.TransformPoint(new Vector3(x, y, z));
                    if (!NavMesh.SamplePosition(world, out NavMeshHit hit, 0.9f, NavMesh.AllAreas)) continue;
                    if (Mathf.Abs(hit.position.y - world.y) > 0.9f) continue;

                    Vector3 local = sector.InverseTransformPoint(hit.position);
                    if (!seen.Add(Vector3Int.RoundToInt(local))) continue;

                    // Piso plano (nada de escalones ni rampas) y lugar arriba para la antorcha.
                    if (!Physics.Raycast(hit.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit floor, 1.5f, wallMask, QueryTriggerInteraction.Ignore)) continue;
                    if (floor.normal.y < 0.97f) continue;
                    if (Physics.Raycast(hit.position + Vector3.up * 0.2f, Vector3.up, 2.6f, wallMask, QueryTriggerInteraction.Ignore)) continue;

                    // Una pared al lado: la más cercana en ocho direcciones, a la altura del pecho.
                    float nearest = float.MaxValue;
                    for (int d = 0; d < 8; d++)
                    {
                        Vector3 dir = Quaternion.Euler(0f, d * 45f, 0f) * Vector3.forward;
                        if (Physics.Raycast(hit.position + Vector3.up * 1.1f, dir, out RaycastHit wall, 1.8f, wallMask, QueryTriggerInteraction.Ignore))
                            nearest = Mathf.Min(nearest, wall.distance);
                    }
                    if (nearest < 0.45f) continue;

                    // Sin pared al lado: una antorcha suelta, pero solo después de las de pared y más separada.
                    bool open = nearest > 1.8f;
                    candidates.Add(new Spot { Local = local, Underground = local.y < -3f, Score = open ? 10f : nearest });
                }
            }

            // Las más pegadas a una pared primero; después, separadas entre sí.
            candidates.Sort((a, b) => a.Score.CompareTo(b.Score));
            var chosen = new List<Spot>();
            int surface = 0, underground = 0, openCount = 0;
            foreach (Spot c in candidates)
            {
                float spacing = c.Underground ? UndergroundSpacing : c.Score >= 10f ? OpenSpacing : SurfaceSpacing;
                bool isOpen = !c.Underground && c.Score >= 10f;
                if (c.Underground ? underground >= UndergroundMax : isOpen ? openCount >= OpenMax : surface >= SurfaceMax) continue;
                bool tooClose = false;
                foreach (Spot o in chosen)
                    if ((o.Local - c.Local).sqrMagnitude < spacing * spacing) { tooClose = true; break; }
                if (tooClose) continue;
                chosen.Add(c);
                if (c.Underground) underground++; else if (isOpen) openCount++; else surface++;
            }

            Transform group = new GameObject("Torches").transform;
            group.SetParent(root.transform, false);
            var rng = new System.Random(1234);
            foreach (Spot s in chosen)
            {
                var t = (GameObject)PrefabUtility.InstantiatePrefab(torch, group);
                t.name = s.Underground ? "Torch_Under" : "Torch";
                t.transform.localPosition = s.Local;
                t.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            }

            PrefabUtility.SaveAsPrefabAsset(root, SectorPath);
            log.AppendLine($"· {surface} antorchas en paredes, {openCount} sueltas y {underground} bajo tierra por sector (×3 = {chosen.Count * 3} en el mapa), " +
                           $"de {candidates.Count} lugares posibles. En {SectorPath} ▸ Torches.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
