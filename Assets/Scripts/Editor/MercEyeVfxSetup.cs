using System.IO;
using UnityEditor;
using UnityEngine;

// ============================================================
// MercEyeVfxSetup
//
// Arma el VFX del OJO que flota encima de un personaje (pensado para GE_Revealed: "te
// están viendo aunque seas invisible", pero sirve para cualquier marca). Tres capas de
// partículas, todas en espacio LOCAL para que sigan al personaje:
//
//   - Eye:      el ícono eye_Icon, un solo cuadro que aparece, brilla y se apaga cada
//               2 s (como un parpadeo lento), con un pulso de tamaño.
//   - Glow:     un halo rosado detrás del ojo que respira al mismo ritmo.
//   - Sparkles: chispas que salen de alrededor del ojo y suben.
//
// Usa el shader aditivo de los packs de Hovl (ERB/Particles/Add_CenterGlow), el mismo
// de los VFX que ya andan en el proyecto, así se ve igual en URP. El ojo está a 2.6 m
// del pivote: puesto como Target VFX de un GE, va con Offset en cero.
//
// Para ajustarlo después: abrir el prefab y tocar los Particle System a mano (tamaño,
// color, velocidad del parpadeo en Duration/Start Lifetime del "Eye" y del "Glow").
// Volver a correr esto lo PISA.
// ============================================================
public static class MercEyeVfxSetup
{
    private const string Folder       = "Assets/Art/VFX/Eye";
    private const string PrefabPath   = Folder + "/VFX_EyeReveal.prefab";
    private const string EyeMatPath   = Folder + "/Mat_EyeIcon.mat";
    private const string GlowMatPath  = Folder + "/Mat_EyeGlow.mat";

    private const string ShaderName   = "ERB/Particles/Add_CenterGlow";
    private const float  EyeHeight    = 2.6f;
    private const float  BlinkSeconds = 2f;

    private static readonly Color Pink = new Color(1f, 0.45f, 0.9f, 1f);

    [MenuItem("Mercenarios/Crear VFX del ojo (revelado)", false, 40)]
    public static void Create()
    {
        Texture2D eyeTex  = FindAsset<Texture2D>("eye_Icon", "t:Texture2D");
        Texture2D glowTex = FindGlowTexture();
        Shader shader     = Shader.Find(ShaderName);

        if (eyeTex == null) { Debug.LogError("[VFX ojo] No encontré la textura eye_Icon."); return; }
        if (shader == null) { Debug.LogError($"[VFX ojo] No encontré el shader {ShaderName} (pack de Hovl)."); return; }

        if (File.Exists(PrefabPath) &&
            !EditorUtility.DisplayDialog("VFX del ojo", "VFX_EyeReveal ya existe. Rehacerlo pisa los cambios a mano. ¿Seguir?",
                                         "Rehacer", "Cancelar"))
            return;

        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Art/VFX", "Eye");

        Material eyeMat  = MakeMaterial(EyeMatPath, shader, eyeTex, Color.white, 2.5f);
        Material glowMat = MakeMaterial(GlowMatPath, shader, glowTex, Color.white, 2f);

        var root = new GameObject("VFX_EyeReveal");
        BuildEye(root.transform, eyeMat);
        BuildGlow(root.transform, glowMat);
        BuildSparkles(root.transform, glowMat);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"[VFX ojo] Listo: {PrefabPath}. Ponelo como Target VFX de un GE (Offset en 0) o " +
                  "arrastralo a la escena para verlo.");
    }

    // =========================================================
    // CAPAS
    // =========================================================

    private static void BuildEye(Transform parent, Material mat)
    {
        ParticleSystem ps = NewSystem(parent, "Eye", mat, new Vector3(0f, EyeHeight, 0f), 1);
        ParticleSystem.MainModule main = ps.main;
        main.duration      = BlinkSeconds;
        main.startLifetime = BlinkSeconds;
        main.startSize     = 0.9f;
        main.startColor    = Color.white;

        Burst(ps, 1);
        FadeInOut(ps, Color.white, Color.white, 1f);
        Pulse(ps, 0.9f, 1.05f);

        ps.GetComponent<ParticleSystemRenderer>().sortingFudge = -2f;   // delante del halo
    }

    private static void BuildGlow(Transform parent, Material mat)
    {
        ParticleSystem ps = NewSystem(parent, "Glow", mat, new Vector3(0f, EyeHeight, 0f), 1);
        ParticleSystem.MainModule main = ps.main;
        main.duration      = BlinkSeconds;
        main.startLifetime = BlinkSeconds;
        main.startSize     = 2.2f;
        main.startColor    = Color.white;   // el rosado lo pone el color en el tiempo

        Burst(ps, 1);
        FadeInOut(ps, Pink, Pink, 0.45f);
        Pulse(ps, 0.8f, 1.15f);
    }

    private static void BuildSparkles(Transform parent, Material mat)
    {
        ParticleSystem ps = NewSystem(parent, "Sparkles", mat, new Vector3(0f, EyeHeight, 0f), 50);
        ParticleSystem.MainModule main = ps.main;
        main.duration      = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
        main.startSpeed    = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startSize     = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
        main.startColor    = new ParticleSystem.MinMaxGradient(Color.white, Pink);

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 18f;

        // Salen de la superficie de una esfera alrededor del ojo, hacia afuera.
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled         = true;
        shape.shapeType       = ParticleSystemShapeType.Sphere;
        shape.radius          = 0.6f;
        shape.radiusThickness = 0f;

        // Y suben un poco, como chispas de luz.
        ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space   = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(0f);
        vel.y = new ParticleSystem.MinMaxCurve(0.3f);
        vel.z = new ParticleSystem.MinMaxCurve(0f);

        FadeInOut(ps, Color.white, Pink, 1f);
        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
    }

    // =========================================================
    // AYUDANTES
    // =========================================================

    private static ParticleSystem NewSystem(Transform parent, string name, Material mat, Vector3 localPos, int maxParticles)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.loop            = true;
        main.playOnAwake     = true;
        main.startSpeed      = 0f;
        main.maxParticles    = maxParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;   // sigue al personaje
        main.scalingMode     = ParticleSystemScalingMode.Hierarchy;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = false;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode     = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = mat;
        return ps;
    }

    private static void Burst(ParticleSystem ps, int count)
    {
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
    }

    // Aparece, se queda y se apaga: la vida entera de la partícula es un "parpadeo".
    private static void FadeInOut(ParticleSystem ps, Color from, Color to, float peakAlpha)
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, 0.2f),
                    new GradientAlphaKey(peakAlpha, 0.8f), new GradientAlphaKey(0f, 1f) });

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        col.color   = new ParticleSystem.MinMaxGradient(gradient);
    }

    private static void Pulse(ParticleSystem ps, float min, float max)
    {
        var curve = new AnimationCurve(new Keyframe(0f, min), new Keyframe(0.5f, max), new Keyframe(1f, min));
        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size    = new ParticleSystem.MinMaxCurve(1f, curve);
    }

    private static Material MakeMaterial(string path, Shader shader, Texture tex, Color color, float emission)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        mat.SetTexture("_MainTex", tex);
        mat.SetColor("_Color", color);
        mat.SetFloat("_Emission", emission);
        mat.SetFloat("_Usedepth", 0f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // El punto de luz suave de los packs (el de Point6cg); si no está, el de Unity.
    private static Texture2D FindGlowTexture()
    {
        Material point = FindAsset<Material>("Point6cg", "t:Material");
        if (point != null && point.HasProperty("_MainTex") && point.GetTexture("_MainTex") is Texture2D t) return t;
        return AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
    }

    private static T FindAsset<T>(string name, string filter) where T : Object
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " " + filter))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<T>(path);
        }
        return null;
    }
}
