using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using FishNet.Object;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ============================================================
// MercShrineSetup
//
// Herramienta de UNA VEZ (11 de noviembre de 2026) que arma los SANTUARIOS y el administrador de
// eventos de la partida. Después todo se edita a mano:
//   1. Los cuatro beneficios (Effects/Buffs/GE_Shrine_*): daño, curación, defensa y velocidad.
//   2. Las mallas, las texturas y los materiales de la zona y del altar (Art/Models/Shrine y
//      Art/Materials/Shrine; la piedra del altar es la del mapa).
//   3. El prefab del santuario (Prefabs/GameMode/Shrine): el altar y la zona negra.
//   4. El prefab de los eventos (Prefabs/GameMode/MatchEvents): el administrador con las rejas, el
//      Objetivo y los santuarios.
//   5. Un santuario abajo de la torre del sector del Cementerio (los tres sectores lo heredan).
//   6. Los eventos en la escena del Cementerio, con su SceneId de FishNet.
//   7. El panel de los santuarios y su marcador en Match_UI.
//   8. Los NPCs de la zona: el aura (Art/VFX/VFX_ShrineEmpowered), su efecto (GE_Shrine_NpcEmpower:
//      +50 % de vida máxima y +2 de ataque), el campamento de enemigos extra dentro del prefab del
//      santuario y el efecto cargado en el evento.
//   9. Los registros de red (por los GE nuevos).
// Lo que ya existe no lo vuelve a crear: correrla dos veces no duplica nada.
// ============================================================
public static class MercShrineSetup
{
    private const string EffectsFolder   = "Assets/Effects/Buffs";
    private const string IconsFolder     = "Assets/Art/Icons/Abilities";
    private const string ModelsFolder    = "Assets/Art/Models/Shrine";
    private const string MaterialsFolder = "Assets/Art/Materials/Shrine";
    private const string PrefabFolder    = "Assets/Prefabs/GameMode";

    // Solo atajos: si no están ahí, se buscan por componente o por nombre.
    private const string SectorPrefabPath = "Assets/Prefabs/Map/Graveyard_Sector.prefab";
    private const string MatchUiPath      = "Assets/Prefabs/UI/Match_UI.prefab";
    private const string GraveyardScene   = "Assets/Scenes/Mercenaries_Graveyard.unity";

    private const string UnlitShader = "Universal Render Pipeline/Unlit";
    private const string LitShader   = "Universal Render Pipeline/Lit";

    // Los colores de los tipos: lejos del rojo, verde y azul de los equipos y del dorado del Objetivo.
    private static readonly Color DamageColor  = new Color(1.00f, 0.50f, 0.10f);   // naranja
    private static readonly Color HealingColor = new Color(0.10f, 0.90f, 0.80f);   // turquesa
    private static readonly Color DefenseColor = new Color(0.60f, 0.40f, 1.00f);   // violeta
    private static readonly Color SpeedColor   = new Color(1.00f, 0.40f, 0.80f);   // rosa

    // La zona: casi negra, un poco morada.
    private static readonly Color ZoneTint = new Color(0.03f, 0.02f, 0.06f, 1f);

    [MenuItem("Mercenarios/Santuarios/Armar santuarios y eventos de partida")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        var log = new StringBuilder("[Santuarios] Armado:\n");

        GameplayEffect[] effects = BuildEffects(log);
        ShrineArt art            = BuildArt(log);
        GameObject shrinePrefab  = BuildShrinePrefab(art, log);
        GameObject eventsPrefab  = BuildEventsPrefab(effects, log);
        PlaceShrineInSector(shrinePrefab, log);
        PlaceEventsInScene(eventsPrefab, log);
        BuildMatchUi(log);

        // Los NPCs de la zona (segunda tanda, el mismo 11 de noviembre).
        GameObject empowerVfx = BuildEmpowerVfx(log);
        GameplayEffect npcEffect = BuildNpcEmpowerEffect(empowerVfx, log);
        EnsureExtraEnemies(shrinePrefab, log);
        EnsureEventsNpcEffect(eventsPrefab, npcEffect, log);

        AssetDatabase.SaveAssets();
        MercRegistryTools.RefreshNetworkRegistries();
        log.AppendLine("· Registros de red actualizados.");
        return log.ToString();
    }

    // =========================================================
    // 1. LOS BENEFICIOS
    // =========================================================

    private static GameplayEffect[] BuildEffects(StringBuilder log)
    {
        EnsureFolder(EffectsFolder);
        return new[]
        {
            MakeEffect("GE_Shrine_Damage", "Barbarian/Rage.png", 0f, log,
                Mod(EAttributeType.Attack,      Modifier.EModificationType.Multiply, 1.2f),
                Mod(EAttributeType.MagicDamage, Modifier.EModificationType.Multiply, 1.2f)),
            MakeEffect("GE_Shrine_Healing", "Cleric/Healing_Word_Icon.png", 1f, log,
                HealPerSecond(0.015f)),
            MakeEffect("GE_Shrine_Defense", "Paladin/Aura_of_Protection_Icon.png", 0f, log,
                Mod(EAttributeType.Resistance, Modifier.EModificationType.Add, 0.2f)),
            MakeEffect("GE_Shrine_Speed", "Rogue/Dash_Icon.png", 0f, log,
                Mod(EAttributeType.MovSpeed, Modifier.EModificationType.Multiply, 1.15f)),
        };
    }

    private static Modifier Mod(EAttributeType attribute, Modifier.EModificationType type, float magnitude) =>
        new Modifier { Attribute = attribute, Type = type, Magnitude = magnitude };

    // Cura una fracción de la vida máxima en cada período (como la regeneración del Cazador paciente).
    private static Modifier HealPerSecond(float fractionOfMaxHealth) => new Modifier
    {
        Attribute               = EAttributeType.Health,
        Type                    = Modifier.EModificationType.Add,
        Magnitude               = 0f,
        UseTargetHealthScaling  = true,
        TargetHealthMode        = Modifier.ETargetHealthMode.MaxHealth,
        TargetHealthCoefficient = fractionOfMaxHealth,
    };

    private static GameplayEffect MakeEffect(string name, string icon, float period, StringBuilder log,
                                             params Modifier[] modifiers)
    {
        GameplayEffect existing = FindAsset<GameplayEffect>(name);
        if (existing != null)
        {
            log.AppendLine($"· {name}: ya existía ({AssetDatabase.GetAssetPath(existing)}).");
            return existing;
        }

        var ge = ScriptableObject.CreateInstance<GameplayEffect>();
        ge.Duration       = 180f;   // lo pisa "Dura el beneficio (s)" del evento de los santuarios
        ge.Period         = period;
        ge.StackingPolicy = GameplayEffect.EStackingType.Refresh;
        ge.EffectType     = GameplayEffect.EEffectType.Buff;   // Buff: al morir se pierde solo
        ge.Icon           = AssetDatabase.LoadAssetAtPath<Sprite>(IconsFolder + "/" + icon);
        ge.Modifiers      = new List<Modifier>(modifiers);
        AssetDatabase.CreateAsset(ge, $"{EffectsFolder}/{name}.asset");

        log.AppendLine($"· {name} creado." + (ge.Icon == null ? $" ¡Ojo! Sin ícono: no encontré {icon}." : ""));
        return ge;
    }

    // =========================================================
    // 2. MALLAS, TEXTURAS Y MATERIALES
    // =========================================================

    private class ShrineArt
    {
        public Mesh Floor, Edge, Cloud, Pedestal, Crystal;
        public Material FloorMat, EdgeMat, CloudMat, StoneMat, CrystalMat;
    }

    private static ShrineArt BuildArt(StringBuilder log)
    {
        EnsureFolder(ModelsFolder);
        EnsureFolder(MaterialsFolder);

        var art = new ShrineArt
        {
            Floor    = SaveMesh(BuildRings(new[] { 0f, 0.5f, 0.8f, 0.92f, 0.97f, 1f }, 72), "Shrine_ZoneFloor"),
            Edge     = SaveMesh(BuildRings(new[] { 0.975f, 1f }, 96), "Shrine_ZoneEdge"),
            Cloud    = SaveMesh(BuildCylinder(96), "Shrine_ZoneCloud"),
            Pedestal = SaveMesh(BuildPedestal(), "Shrine_Pedestal"),
            Crystal  = SaveMesh(BuildCrystal(), "Shrine_Crystal"),
        };

        // El piso: más oscuro hacia el borde. u = distancia al centro (0 a 1).
        Texture2D floorTex = SaveTexture("Tex_ShrineZoneFloor", 64, 4, TextureWrapMode.Clamp, (u, v) =>
            0.34f + 0.26f * Smooth(0.45f, 0.97f, u) + 0.14f * Smooth(0.95f, 1f, u));

        // La nube del borde: densa abajo, se deshace hacia arriba, con jirones. u = vuelta, v = alto.
        // Va sobre el piso ya oscurecido: con menos de esto no se distingue (probado el 11 de noviembre).
        Texture2D cloudTex = SaveTexture("Tex_ShrineZoneCloud", 128, 64, TextureWrapMode.Repeat, (u, v) =>
        {
            float wisps = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * (u * 7f + v * 0.8f)) *
                                        Mathf.Sin(2f * Mathf.PI * (u * 3f - v * 0.5f + 0.3f));
            return 0.88f * Mathf.Pow(1f - v, 1.15f) * (0.7f + 0.3f * wisps);
        });

        art.FloorMat = MakeMaterial("Mat_ShrineZoneFloor", UnlitShader, m =>
        {
            MakeTransparent(m, doubleSided: false);
            m.SetTexture("_BaseMap", floorTex);
            m.SetColor("_BaseColor", ZoneTint);
        });
        art.CloudMat = MakeMaterial("Mat_ShrineZoneCloud", UnlitShader, m =>
        {
            MakeTransparent(m, doubleSided: true);
            m.SetTexture("_BaseMap", cloudTex);
            m.SetColor("_BaseColor", ZoneTint);
            m.renderQueue = (int)RenderQueue.Transparent + 1;   // después del piso
        });
        art.EdgeMat = MakeMaterial("Mat_ShrineZoneEdge", UnlitShader, m => m.SetColor("_BaseColor", Color.white));
        art.CrystalMat = MakeMaterial("Mat_ShrineCrystal", LitShader, m =>
        {
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.75f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);   // lo pinta MatchShrine con el color del tipo
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        });

        art.StoneMat = FindAsset<Material>("Mat_ArenaStone");
        if (art.StoneMat == null)
            art.StoneMat = MakeMaterial("Mat_ShrineStone", LitShader, m => m.SetColor("_BaseColor", new Color(0.42f, 0.42f, 0.45f)));

        log.AppendLine($"· Mallas en {ModelsFolder}, texturas y materiales en {MaterialsFolder} (piedra: {art.StoneMat.name}).");
        return art;
    }

    private static float Smooth(float from, float to, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, x));

    // Anillos concéntricos en el piso, mirando arriba, de radio 1. uv.x = radio (para el degradé).
    private static Mesh BuildRings(float[] radii, int segments)
    {
        var verts = new List<Vector3>();
        var uvs   = new List<Vector2>();
        var tris  = new List<int>();

        foreach (float r in radii)
            for (int i = 0; i <= segments; i++)   // el último repite el primero (costura)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                uvs.Add(new Vector2(r, 0.5f));
            }

        int row = segments + 1;
        for (int k = 0; k < radii.Length - 1; k++)
            for (int i = 0; i < segments; i++)
            {
                int inner0 = k * row + i, inner1 = inner0 + 1;
                int outer0 = inner0 + row, outer1 = outer0 + 1;
                tris.Add(inner0); tris.Add(inner1); tris.Add(outer0);
                tris.Add(outer0); tris.Add(inner1); tris.Add(outer1);
            }

        var normals = new List<Vector3>();
        foreach (Vector3 _ in verts) normals.Add(Vector3.up);
        return NewMesh(verts, normals, uvs, tris);
    }

    // La pared de la nube: un tubo abierto de radio 1 y alto 1. uv = (vuelta, alto).
    private static Mesh BuildCylinder(int segments)
    {
        var verts   = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs     = new List<Vector2>();
        var tris    = new List<int>();

        for (int y = 0; y <= 1; y++)
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts.Add(dir + Vector3.up * y);
                normals.Add(dir);
                uvs.Add(new Vector2(i / (float)segments, y));
            }

        int row = segments + 1;
        for (int i = 0; i < segments; i++)
        {
            int b0 = i, b1 = i + 1, t0 = i + row, t1 = t0 + 1;
            tris.Add(b0); tris.Add(t0); tris.Add(t1);
            tris.Add(b0); tris.Add(t1); tris.Add(b1);
        }
        return NewMesh(verts, normals, uvs, tris);
    }

    // El pedestal: prismas octogonales apilados (base, escalón, columna y capitel), con caras planas.
    private static Mesh BuildPedestal()
    {
        var m = new FlatMesh();
        const int sides = 8;
        const float twist = Mathf.PI / sides;   // una cara plana mirando al frente
        m.Prism(sides, 0.95f, 0.95f, 0.00f, 0.22f, twist);
        m.Prism(sides, 0.72f, 0.72f, 0.22f, 0.40f, twist);
        m.Prism(sides, 0.36f, 0.30f, 0.40f, 1.15f, twist);
        m.Prism(sides, 0.55f, 0.62f, 1.15f, 1.32f, twist);
        return m.ToMesh();
    }

    // El cristal: una bipirámide hexagonal, más larga hacia arriba.
    private static Mesh BuildCrystal()
    {
        var m = new FlatMesh();
        const int sides = 6;
        var top    = new Vector3(0f, 0.62f, 0f);
        var bottom = new Vector3(0f, -0.42f, 0f);
        for (int i = 0; i < sides; i++)
        {
            Vector3 e0 = FlatMesh.Ring(i / (float)sides * Mathf.PI * 2f, 0.30f, 0f);
            Vector3 e1 = FlatMesh.Ring((i + 1) / (float)sides * Mathf.PI * 2f, 0.30f, 0f);
            m.Tri(e0, top, e1);
            m.Tri(e0, e1, bottom);
        }
        return m.ToMesh();
    }

    // Una malla de caras planas: cada triángulo con sus vértices y la normal de su cara.
    private class FlatMesh
    {
        private readonly List<Vector3> _verts   = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Vector2> _uvs     = new List<Vector2>();
        private readonly List<int>     _tris    = new List<int>();

        public static Vector3 Ring(float angle, float radius, float y) =>
            new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);

        public void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            n = n.sqrMagnitude > 1e-12f ? n / n.magnitude : Vector3.up;   // nunca una normal en cero
            foreach (Vector3 p in new[] { a, b, c })
            {
                _tris.Add(_verts.Count);
                _verts.Add(p);
                _normals.Add(n);
                // Proyección por cara: arriba/abajo con x,z; los costados con lo horizontal y el alto.
                _uvs.Add(Mathf.Abs(n.y) > 0.5f ? new Vector2(p.x, p.z)
                         : Mathf.Abs(n.x) > Mathf.Abs(n.z) ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y));
            }
        }

        // Un prisma de radio r0 abajo y r1 arriba, con sus dos tapas.
        public void Prism(int sides, float r0, float r1, float y0, float y1, float twist)
        {
            var bottomCenter = new Vector3(0f, y0, 0f);
            var topCenter    = new Vector3(0f, y1, 0f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f + twist;
                float a1 = (i + 1) / (float)sides * Mathf.PI * 2f + twist;
                Vector3 b0 = Ring(a0, r0, y0), b1 = Ring(a1, r0, y0);
                Vector3 t0 = Ring(a0, r1, y1), t1 = Ring(a1, r1, y1);
                Tri(b0, t0, t1); Tri(b0, t1, b1);   // costado, hacia afuera
                Tri(topCenter, t1, t0);             // tapa de arriba
                Tri(bottomCenter, b0, b1);          // tapa de abajo
            }
        }

        public Mesh ToMesh() => NewMesh(_verts, _normals, _uvs, _tris);
    }

    private static Mesh NewMesh(List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<int> tris)
    {
        var mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
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

    // Una textura blanca con el alfa que diga la función (u, v de 0 a 1). Si ya existe, se reescribe
    // el dibujo y se conserva su .meta: los materiales la siguen encontrando.
    private static Texture2D SaveTexture(string name, int width, int height, TextureWrapMode wrapU,
                                         System.Func<float, float, float> alpha)
    {
        string path = $"{MaterialsFolder}/{name}.png";
        bool existed = File.Exists(path);

        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float u = width  > 1 ? x / (float)(width - 1)  : 0f;
                float v = height > 1 ? y / (float)(height - 1) : 0f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(u, v))));
            }
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        if (existed) return AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType         = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled       = false;
        importer.wrapModeU           = wrapU;
        importer.wrapModeV           = TextureWrapMode.Clamp;
        importer.textureCompression  = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Material MakeMaterial(string name, string shaderName, System.Action<Material> setup)
    {
        string path = $"{MaterialsFolder}/{name}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var mat = new Material(Shader.Find(shaderName)) { name = name };
        setup(mat);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // Lo mismo que elegir "Transparent" en el Surface Type del inspector de URP.
    private static void MakeTransparent(Material mat, bool doubleSided)
    {
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_AlphaClip", 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_Cull", doubleSided ? (float)CullMode.Off : (float)CullMode.Back);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.renderQueue = (int)RenderQueue.Transparent;
    }

    // =========================================================
    // 3. EL PREFAB DEL SANTUARIO
    // =========================================================

    private static GameObject BuildShrinePrefab(ShrineArt art, StringBuilder log)
    {
        GameObject existing = FindPrefabWith<MatchShrine>();
        if (existing != null)
        {
            log.AppendLine($"· Santuario: ya existía ({AssetDatabase.GetAssetPath(existing)}).");
            return existing;
        }
        EnsureFolder(PrefabFolder);

        var root = new GameObject("Shrine");
        var shrine = root.AddComponent<MatchShrine>();
        shrine.Radius = 12f;
        shrine.Height = 6f;
        shrine.Below  = 1f;

        // --- el altar ---
        Transform altar = NewChild(root.transform, "Altar");
        GameObject pedestal = MeshChild(altar, "Pedestal", art.Pedestal, art.StoneMat, solid: true);
        var collider = pedestal.AddComponent<MeshCollider>();
        collider.sharedMesh = art.Pedestal;
        collider.convex     = true;
        var obstacle = pedestal.AddComponent<NavMeshObstacle>();   // los NPCs y los bots lo rodean
        obstacle.shape               = NavMeshObstacleShape.Capsule;
        obstacle.center              = new Vector3(0f, 0.66f, 0f);
        obstacle.radius              = 0.95f;
        obstacle.height              = 1.32f;
        obstacle.carving             = true;
        obstacle.carveOnlyStationary = true;

        Transform floating = NewChild(altar, "CrystalFloat");
        floating.localPosition = new Vector3(0f, 2f, 0f);
        var floatVisual = floating.gameObject.AddComponent<FloatingVisual>();
        floatVisual.BobHeight = 0.12f;
        floatVisual.BobSpeed  = 0.35f;
        floatVisual.SwayAngle = 3f;
        floatVisual.LeanAngle = 0f;
        GameObject crystal = MeshChild(floating, "Crystal", art.Crystal, art.CrystalMat, solid: false);

        Transform glow = NewChild(altar, "Glow");
        glow.localPosition = new Vector3(0f, 2f, 0f);
        var light = glow.gameObject.AddComponent<Light>();
        light.type      = LightType.Point;
        light.range     = 10f;
        light.intensity = 2.5f;
        light.shadows   = LightShadows.None;
        light.enabled   = false;   // la prende MatchShrine mientras está activo

        // --- la zona (mallas de radio 1; MatchShrine las escala al radio) ---
        Transform zone = NewChild(root.transform, "Zone");
        GameObject floor = MeshChild(zone, "Floor", art.Floor, art.FloorMat, solid: false);
        floor.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        GameObject edge = MeshChild(zone, "Edge", art.Edge, art.EdgeMat, solid: false);
        edge.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        GameObject cloud = MeshChild(zone, "Cloud", art.Cloud, art.CloudMat, solid: false);

        zone.localScale             = new Vector3(shrine.Radius, 1f, shrine.Radius);
        cloud.transform.localScale  = new Vector3(1f, shrine.CloudHeight, 1f);
        zone.gameObject.SetActive(false);   // en el editor no tapa nada: se prende sola en juego

        shrine.Zone    = zone;
        shrine.Crystal = crystal.GetComponent<Renderer>();
        shrine.Edge    = edge.GetComponent<Renderer>();
        shrine.Glow    = light;

        string path = $"{PrefabFolder}/Shrine.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        log.AppendLine($"· Santuario creado: {path}.");
        return prefab;
    }

    private static Transform NewChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    // solid = parte del mapa (proyecta sombra); si no, adorno o zona (sin sombras ni sondas de luz).
    private static GameObject MeshChild(Transform parent, string name, Mesh mesh, Material material, bool solid)
    {
        Transform t = NewChild(parent, name);
        t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = t.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial    = material;
        renderer.shadowCastingMode = solid ? ShadowCastingMode.On : ShadowCastingMode.Off;
        renderer.receiveShadows    = solid;
        if (!solid) renderer.lightProbeUsage = LightProbeUsage.Off;
        return t.gameObject;
    }

    // =========================================================
    // 4. EL PREFAB DE LOS EVENTOS
    // =========================================================

    private static GameObject BuildEventsPrefab(GameplayEffect[] effects, StringBuilder log)
    {
        GameObject existing = FindPrefabWith<GameModeEventManager>();
        if (existing != null)
        {
            log.AppendLine($"· Eventos: ya existía ({AssetDatabase.GetAssetPath(existing)}).");
            return existing;
        }
        EnsureFolder(PrefabFolder);

        // Primero la raíz con su NetworkObject: un NetworkBehaviour que se agrega busca uno hacia
        // arriba y, si no hay, se pone uno propio (los eventos quedarían como objetos de red aparte).
        var root = new GameObject("MatchEvents");
        root.AddComponent<NetworkObject>();
        root.AddComponent<GameModeEventManager>();

        var gates = NewChild(root.transform, "Gates").gameObject.AddComponent<MatchEvent_Gates>();
        gates.StartAt = 0f;

        var objective = NewChild(root.transform, "Objective").gameObject.AddComponent<MatchEvent_Objective>();
        objective.StartAt = 120f;

        var shrines = NewChild(root.transform, "Shrines").gameObject.AddComponent<MatchEvent_Shrines>();
        shrines.StartAt              = 60f;
        shrines.SecondsBetween       = 60f;
        shrines.MaxActiveSeconds     = 0f;
        shrines.PercentPerSecond     = 1;
        shrines.PercentPerNpcKill    = 5;
        shrines.PercentPerPlayerKill = 10;
        shrines.ContestedCap         = 99;
        shrines.CaptureXp            = 75f;
        shrines.BlessingSeconds      = 180f;
        shrines.Blessings = new[]
        {
            new ShrineBlessing { Type = EShrineType.Damage,  Effect = effects[0], Color = DamageColor  },
            new ShrineBlessing { Type = EShrineType.Healing, Effect = effects[1], Color = HealingColor },
            new ShrineBlessing { Type = EShrineType.Defense, Effect = effects[2], Color = DefenseColor },
            new ShrineBlessing { Type = EShrineType.Speed,   Effect = effects[3], Color = SpeedColor   },
        };

        string path = $"{PrefabFolder}/MatchEvents.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        log.AppendLine($"· Eventos creados: {path} (rejas a los 0 s, Objetivo a los 120 s, santuarios a los 60 s).");
        return prefab;
    }

    // =========================================================
    // 5. EL SANTUARIO EN EL SECTOR DEL CEMENTERIO
    // =========================================================

    private static void PlaceShrineInSector(GameObject shrinePrefab, StringBuilder log)
    {
        string path = FindPrefabPath("Graveyard_Sector", SectorPrefabPath);
        if (path == null) { log.AppendLine("  ¡Ojo! No encontré el prefab Graveyard_Sector: el santuario no quedó en el mapa."); return; }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.GetComponentInChildren<MatchShrine>(true) != null)
            {
                log.AppendLine("· El sector ya tenía su santuario.");
                return;
            }

            // Al centro de la torre, en el piso: la zona la rodea entera y llega al campamento de atrás.
            Transform tower = root.transform.Find("Tower");
            Vector3 at = tower != null ? new Vector3(tower.localPosition.x, 0f, tower.localPosition.z)
                                       : new Vector3(38.54f, 0f, 22.25f);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(shrinePrefab, root.transform);
            instance.name = "Shrine";
            instance.transform.localPosition = at;
            instance.transform.localRotation = tower != null ? tower.localRotation : Quaternion.identity;

            PrefabUtility.SaveAsPrefabAsset(root, path);
            log.AppendLine($"· Santuario puesto en {path}, abajo de la torre ({at}): uno por sector, tres en el mapa.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // =========================================================
    // 6. LOS EVENTOS EN LA ESCENA
    // =========================================================

    private static void PlaceEventsInScene(GameObject eventsPrefab, StringBuilder log)
    {
        string scenePath = FindScenePath("Mercenaries_Graveyard", GraveyardScene);
        if (scenePath == null) { log.AppendLine("  ¡Ojo! No encontré la escena del Cementerio."); return; }

        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool openedHere = false;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            openedHere = true;
        }

        try
        {
            foreach (GameObject go in scene.GetRootGameObjects())
                if (go.GetComponentInChildren<GameModeEventManager>(true) != null)
                {
                    log.AppendLine($"· La escena ya tenía los eventos ('{go.name}').");
                    return;
                }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(eventsPrefab, scene);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            // Creado por código queda en SceneId 0 y FishNet no lo spawnearía (ver CLAUDE.md).
            int changed = AssignSceneIds(scene);
            ulong sceneId = ReadSceneId(instance.GetComponent<NetworkObject>());

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine($"· Eventos puestos en {scenePath} (SceneId {sceneId}; ids nuevos: {changed}). Escena guardada.");
        }
        finally
        {
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static int AssignSceneIds(Scene scene)
    {
        MethodInfo create = typeof(NetworkObject).GetMethod("CreateSceneId",
            BindingFlags.Static | BindingFlags.NonPublic, null,
            new[] { typeof(Scene), typeof(bool), typeof(int).MakeByRefType() }, null);
        if (create == null) return -1;

        object[] args = { scene, false, 0 };
        create.Invoke(null, args);
        return (int)args[2];
    }

    // El SceneId es interno de FishNet: se lee por reflexión, solo para el registro.
    private static ulong ReadSceneId(NetworkObject nob)
    {
        FieldInfo field = typeof(NetworkObject).GetField("SceneId", BindingFlags.Instance | BindingFlags.NonPublic);
        return field != null && nob != null ? (ulong)field.GetValue(nob) : 0UL;
    }

    // =========================================================
    // 7. EL PANEL Y EL MARCADOR EN MATCH_UI
    // =========================================================

    private static void BuildMatchUi(StringBuilder log)
    {
        string path = FindPrefabPathWith<UI_MercenariesHUD>(MatchUiPath);
        if (path == null) { log.AppendLine("  ¡Ojo! No encontré el prefab de partida (Match_UI)."); return; }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            bool changed = false;

            UI_MercenariesHUD hud = root.GetComponentInChildren<UI_MercenariesHUD>(true);
            if (hud != null && hud.GetComponent<UI_ShrineHud>() == null)
                changed |= BuildShrinePanel(hud, log);
            else
                log.AppendLine("· El panel de los santuarios ya estaba.");

            UI_ObjectiveMarker markers = root.GetComponentInChildren<UI_ObjectiveMarker>(true);
            if (markers != null && (markers.Shrine == null || markers.Shrine.Root == null))
                changed |= BuildShrineMarker(markers, log);
            else
                log.AppendLine("· El marcador del santuario ya estaba.");

            if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Debajo del marcador: el título y una columna por equipo, copiando las piezas de las filas.
    private static bool BuildShrinePanel(UI_MercenariesHUD hud, StringBuilder log)
    {
        UI_MercenariesHUD.TeamRowPieces row = hud.Rows != null && hud.Rows.Length > 0 ? hud.Rows[0] : null;
        if (hud.Panel == null || hud.ObjectiveText == null || row == null || row.Background == null ||
            row.Chip == null || row.Level == null || row.Segments == null || row.Segments.Length == 0 ||
            row.Segments[0] == null)
        {
            log.AppendLine("  ¡Ojo! Al marcador le faltan piezas para copiar: no armé el panel de los santuarios.");
            return false;
        }
        Transform segmentBack = row.Segments[0].transform.parent.Find(row.Segments[0].name + "Background");

        RectTransform panel = NewRect("ShrinePanel", hud.transform);
        float top = hud.Panel.anchoredPosition.y - hud.Panel.sizeDelta.y - 8f;   // 8 px debajo del marcador
        SetRect(panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, top), new Vector2(520f, 62f));

        TMP_Text title = CloneText(hud.ObjectiveText, panel, "Title");
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(520f, 26f));
        title.fontSize  = 19f;
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Center;
        title.richText  = true;
        title.color     = Color.white;
        title.text      = "";

        var columns = new UI_ShrineHud.TeamColumnPieces[MercenariesGameMode.TeamCount];
        for (int i = 0; i < columns.Length; i++)
        {
            RectTransform column = NewRect($"Team{i + 1}", panel);
            SetRect(column, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(i * 176f, -30f), new Vector2(168f, 30f));

            Image background = CloneImage(row.Background, column, "Background");
            SetLeft(background.rectTransform, 0f, new Vector2(168f, 30f));
            Image chip = CloneImage(row.Chip, column, "TeamColor");
            SetLeft(chip.rectTransform, 0f, new Vector2(6f, 30f));
            if (segmentBack != null && segmentBack.TryGetComponent(out Image segmentBackImage))
                SetLeft(CloneImage(segmentBackImage, column, "BarBackground").rectTransform, 14f, new Vector2(100f, 12f));
            Image fill = CloneImage(row.Segments[0], column, "Fill");
            SetLeft(fill.rectTransform, 14f, new Vector2(100f, 12f));
            fill.fillAmount = 0f;

            TMP_Text percent = CloneText(row.Level, column, "Percent");
            SetLeft(percent.rectTransform, 118f, new Vector2(44f, 30f));
            percent.alignment = TextAlignmentOptions.Right;
            percent.fontSize  = 17f;
            percent.fontStyle = FontStyles.Bold;
            percent.text      = "0%";

            columns[i] = new UI_ShrineHud.TeamColumnPieces
            {
                Root = column, Background = background, Chip = chip, Fill = fill, Percent = percent,
            };
        }

        var shrineHud = hud.gameObject.AddComponent<UI_ShrineHud>();
        shrineHud.Panel   = panel;
        shrineHud.Title   = title;
        shrineHud.Columns = columns;
        panel.gameObject.SetActive(false);   // lo prende el script cuando hay santuario

        log.AppendLine($"· Panel de los santuarios en Match_UI ▸ {hud.name} ▸ ShrinePanel (y UI_ShrineHud en {hud.name}).");
        return true;
    }

    // Una copia del marcador del Objetivo, sin la barra de entrega.
    private static bool BuildShrineMarker(UI_ObjectiveMarker markers, StringBuilder log)
    {
        UI_ObjectiveMarker.MarkerPieces source = markers.Objective;
        if (source == null || source.Root == null || source.Diamond == null || source.Label == null)
        {
            log.AppendLine("  ¡Ojo! Al marcador del Objetivo le faltan piezas: no armé el del santuario.");
            return false;
        }

        GameObject clone = Object.Instantiate(source.Root.gameObject, source.Root.parent);
        clone.name = "ShrineMarker";
        if (source.BarBack != null)
        {
            Transform bar = clone.transform.Find(source.BarBack.name);
            if (bar != null) Object.DestroyImmediate(bar.gameObject);
        }

        Transform diamond = clone.transform.Find(source.Diamond.name);
        Transform label   = clone.transform.Find(source.Label.name);

        // "SANTUARIO DE VELOCIDAD  34m" no entra en los 140 px del Objetivo: partido en dos líneas se
        // monta sobre el rombo.
        if (label is RectTransform labelRect) labelRect.sizeDelta = new Vector2(300f, labelRect.sizeDelta.y);
        markers.Shrine = new UI_ObjectiveMarker.MarkerPieces
        {
            Root    = (RectTransform)clone.transform,
            Diamond = diamond != null ? diamond.GetComponent<Image>() : null,
            Label   = label   != null ? label.GetComponent<TMP_Text>() : null,
        };
        clone.SetActive(false);

        log.AppendLine($"· Marcador del santuario en Match_UI ▸ {markers.name} ▸ ShrineMarker.");
        return true;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private static void SetRect(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rt.anchorMin        = anchor;
        rt.anchorMax        = anchor;
        rt.pivot            = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta        = size;
    }

    // Anclado a la izquierda y centrado en alto, a x px del borde.
    private static void SetLeft(RectTransform rt, float x, Vector2 size) =>
        SetRect(rt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), size);

    private static Image CloneImage(Image source, Transform parent, string name)
    {
        GameObject go = Object.Instantiate(source.gameObject, parent);
        go.name = name;
        for (int i = go.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
        return go.GetComponent<Image>();
    }

    private static TMP_Text CloneText(TMP_Text source, Transform parent, string name)
    {
        GameObject go = Object.Instantiate(source.gameObject, parent);
        go.name = name;
        return go.GetComponent<TMP_Text>();
    }

    // =========================================================
    // 8. LOS NPCs DE LA ZONA
    // =========================================================

    private const string EmpowerVfxPath = "Assets/Art/VFX/VFX_ShrineEmpowered.prefab";

    // El aura de un NPC potenciado: la furia roja (la del Bárbaro) con humo oscuro a los pies, que es
    // el de la zona. Un GE tiene un solo VFX, así que van juntas en un prefab.
    private static GameObject BuildEmpowerVfx(StringBuilder log)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(EmpowerVfxPath);
        if (existing != null)
        {
            log.AppendLine("· El aura de los NPCs ya existía.");
            return existing;
        }

        // Hay dos RageBuff (el de la jam y el de Art/VFX): se elige por carpeta.
        GameObject rage  = FindPrefabIn("RageBuff", "Assets/Art/VFX/");
        GameObject smoke = FindPrefabIn("Aura Smoke", "Assets/AssetsExtra/");
        if (rage == null && smoke == null)
        {
            log.AppendLine("  ¡Ojo! No encontré RageBuff ni Aura Smoke: el efecto de los NPCs queda sin aura.");
            return null;
        }

        var root = new GameObject("VFX_ShrineEmpowered");
        foreach (GameObject piece in new[] { rage, smoke })
        {
            if (piece == null) continue;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(piece, root.transform);
            instance.transform.localPosition = Vector3.zero;
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, EmpowerVfxPath);
        Object.DestroyImmediate(root);
        log.AppendLine($"· Aura de los NPCs creada: {EmpowerVfxPath}.");
        return prefab;
    }

    private static GameplayEffect BuildNpcEmpowerEffect(GameObject vfx, StringBuilder log)
    {
        const string name = "GE_Shrine_NpcEmpower";
        GameplayEffect existing = FindAsset<GameplayEffect>(name);
        if (existing != null)
        {
            log.AppendLine($"· {name}: ya existía.");
            return existing;
        }

        var ge = ScriptableObject.CreateInstance<GameplayEffect>();
        ge.Duration       = 9999f;   // no vence: el evento se lo quita al salir de la zona
        ge.StackingPolicy = GameplayEffect.EStackingType.Refresh;
        ge.EffectType     = GameplayEffect.EEffectType.Buff;   // Buff: si muere, se va con él
        ge.TargetVFX      = vfx;
        ge.Modifiers      = new List<Modifier>
        {
            Mod(EAttributeType.MaxHealth, Modifier.EModificationType.Multiply, 1.5f),   // +50 %: la mitad de su vida, extra
            Mod(EAttributeType.Attack,    Modifier.EModificationType.Add, 2f),
        };
        AssetDatabase.CreateAsset(ge, $"{EffectsFolder}/{name}.asset");
        log.AppendLine($"· {name} creado: +50 % de vida máxima y +2 de ataque.");
        return ge;
    }

    // El campamento de enemigos extra, hijo del santuario: solo trabaja con su santuario activo.
    private static void EnsureExtraEnemies(GameObject shrinePrefab, StringBuilder log)
    {
        if (shrinePrefab == null) return;
        string path = AssetDatabase.GetAssetPath(shrinePrefab);

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.GetComponentInChildren<MercEnemySpawner>(true) != null)
            {
                log.AppendLine("· El santuario ya tenía su campamento de enemigos extra.");
                return;
            }

            var spawner = NewChild(root.transform, "ExtraEnemies").gameObject.AddComponent<MercEnemySpawner>();
            spawner.OnlyWhileShrineActive = true;
            spawner.PopulateDuringWarmup  = false;
            spawner.Count                 = 4;
            spawner.SpawnRadius           = 4f;
            spawner.RespawnSeconds        = 12f;
            spawner.SpawnInterval         = 1f;
            spawner.SpawnTable = new List<MercEnemySpawner.SpawnRule>
            {
                new MercEnemySpawner.SpawnRule { Type = EMercEnemyType.Ghost, Weight = 100f, UnlockMinute = 0f },
                new MercEnemySpawner.SpawnRule { Type = EMercEnemyType.Mage,  Weight = 40f,  UnlockMinute = 0f, MaxAlive = 1 },
            };

            PrefabUtility.SaveAsPrefabAsset(root, path);
            log.AppendLine($"· Campamento de enemigos extra en {path} ▸ ExtraEnemies (4 vivos: fantasmas y hasta un mago).");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void EnsureEventsNpcEffect(GameObject eventsPrefab, GameplayEffect npcEffect, StringBuilder log)
    {
        if (eventsPrefab == null || npcEffect == null) return;
        string path = AssetDatabase.GetAssetPath(eventsPrefab);

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            MatchEvent_Shrines shrines = root.GetComponentInChildren<MatchEvent_Shrines>(true);
            if (shrines == null || shrines.NpcEmpowerEffect != null)
            {
                log.AppendLine("· El evento de los santuarios ya tenía su efecto para los NPCs.");
                return;
            }

            shrines.NpcEmpowerEffect = npcEffect;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            log.AppendLine("· Efecto para los NPCs cargado en el evento de los santuarios.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Un prefab por nombre, solo dentro de una carpeta (para los nombres repetidos).
    private static GameObject FindPrefabIn(string name, string folder)
    {
        foreach (string guid in AssetDatabase.FindAssets($"t:Prefab {name}"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith(folder) && Path.GetFileNameWithoutExtension(path) == name)
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        return null;
    }

    // =========================================================
    // BÚSQUEDAS (por componente o por nombre; la ruta es solo un atajo)
    // =========================================================

    private static T FindAsset<T>(string name) where T : Object
    {
        foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name} {name}"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<T>(path);
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
        GameObject found = FindAsset<GameObject>(name);
        return found != null ? AssetDatabase.GetAssetPath(found) : null;
    }

    private static string FindPrefabPathWith<T>(string shortcut) where T : Component
    {
        var atShortcut = AssetDatabase.LoadAssetAtPath<GameObject>(shortcut);
        if (atShortcut != null && atShortcut.GetComponentInChildren<T>(true) != null) return shortcut;

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && prefab.GetComponentInChildren<T>(true) != null) return path;
        }
        return null;
    }

    private static string FindScenePath(string name, string shortcut)
    {
        if (File.Exists(shortcut)) return shortcut;
        foreach (string guid in AssetDatabase.FindAssets($"t:Scene {name}"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == name) return path;
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
