using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ============================================================
// PaladinModel
//
// La receta del modelo de relleno de la Paladina (referencias elegidas por Gustavo el 9 de noviembre de 2026):
// una aasimar guerrera de la fe. Armadura de placas blanca con filos de oro, al estilo de las monjas guerreras
// pero más medieval: peto con brigantina de remaches en la cintura, gola alta con medalla, hombreras grandes,
// escarcelas, grebas, tabardo y capa carmesí y guantes de cuero. Detalles de fanática: flores de lis, sellos
// de cera con tiras de pergamino, un rosario y una aureola de rayos detrás de la cabeza. Pelo rubio, largo y
// abundante. El rasgo aasimar: los ojos dorados brillan con luz propia (la textura de brillo del atlas).
// Sin alas por ahora, y sin el martillo ni el escudo: son de la clase. Los símbolos son genéricos (flor de
// lis, sol): nada de los de Warhammer.
//
// Acabado Polished: el alfa de cada color es el pulido (el oro brilla más que el esmalte blanco). El
// Paladín no se hace invisible, así que no le afecta lo del fantasma (ver CLAUDE.md).
//
// Mercenarios ▸ Modelos de clase ▸ Generar la Paladina: rehace malla, texturas y prefab.
// ============================================================
public sealed class PaladinModel : ClassModelRecipe
{
    private const string Name = "Paladin";
    private const string ArtFolder = "Assets/Art/Models/Classes/Paladin";
    private const string PrefabFolder = "Assets/Prefabs/Player/Classes";

    // La aureola de rayos detrás de la cabeza. false = sin aureola (si en juego tapa la vista).
    private static readonly bool WithHalo = true;

    // Cuánto brillan los ojos (×). El juego no tiene bloom: con más de ~1.6 el dorado se vuelve blanco.
    private const float EyeGlow = 1.25f;

    // ---------- Colores (el alfa es el pulido) ----------
    private static readonly Color SkinTone    = new Color(0.950f, 0.760f, 0.640f, 0.25f);
    private static readonly Color SkinShade   = new Color(0.700f, 0.440f, 0.360f, 0.20f);
    private static readonly Color HairRoot    = new Color(0.560f, 0.350f, 0.100f, 0.20f);
    private static readonly Color HairMid     = new Color(0.930f, 0.710f, 0.290f, 0.35f);
    private static readonly Color HairTip     = new Color(1.000f, 0.870f, 0.500f, 0.40f);
    private static readonly Color White       = new Color(0.840f, 0.820f, 0.780f, 0.55f);
    private static readonly Color WhiteShade  = new Color(0.540f, 0.540f, 0.590f, 0.45f);
    private static readonly Color WhiteLight  = new Color(0.960f, 0.950f, 0.910f, 0.65f);
    private static readonly Color Gold        = new Color(0.920f, 0.660f, 0.180f, 0.88f);
    private static readonly Color GoldDark    = new Color(0.500f, 0.280f, 0.060f, 0.70f);
    private static readonly Color GoldLight   = new Color(1.000f, 0.860f, 0.450f, 0.92f);
    private static readonly Color Crimson     = new Color(0.560f, 0.050f, 0.070f, 0.08f);
    private static readonly Color CrimsonDark = new Color(0.250f, 0.020f, 0.040f, 0.05f);
    private static readonly Color Under       = new Color(0.075f, 0.070f, 0.090f, 0.10f);
    private static readonly Color Leather     = new Color(0.360f, 0.190f, 0.090f, 0.30f);
    private static readonly Color LeatherDark = new Color(0.150f, 0.080f, 0.050f, 0.25f);
    private static readonly Color GloveTone   = new Color(0.120f, 0.100f, 0.105f, 0.35f);
    private static readonly Color Parchment   = new Color(0.900f, 0.830f, 0.620f, 0.05f);
    private static readonly Color Ink         = new Color(0.300f, 0.170f, 0.080f, 0.05f);
    private static readonly Color Wax         = new Color(0.700f, 0.070f, 0.060f, 0.60f);
    private static readonly Color WaxDark     = new Color(0.340f, 0.020f, 0.030f, 0.50f);
    private static readonly Color Wood        = new Color(0.620f, 0.400f, 0.180f, 0.55f);
    private static readonly Color EyeGold     = new Color(1.000f, 0.780f, 0.250f, 0.90f);
    private static readonly Color EyeCore     = new Color(1.000f, 0.960f, 0.760f, 0.90f);

    // ---------- Torso: alto → medio ancho, media profundidad, centro en Z ----------
    private static readonly ModelKeys TorsoWidth = new ModelKeys(
        0.845f, 0.06f, 0.88f, 0.135f, 0.93f, 0.170f, 0.98f, 0.168f, 1.03f, 0.150f, 1.08f, 0.126f, 1.14f, 0.112f,
        1.19f, 0.116f, 1.25f, 0.134f, 1.31f, 0.152f, 1.36f, 0.162f, 1.41f, 0.172f, 1.46f, 0.170f, 1.495f, 0.112f,
        1.52f, 0.062f, 1.55f, 0.050f, 1.62f, 0.046f);

    private static readonly ModelKeys TorsoDepth = new ModelKeys(
        0.845f, 0.06f, 0.88f, 0.095f, 0.93f, 0.116f, 0.98f, 0.116f, 1.03f, 0.104f, 1.08f, 0.090f, 1.14f, 0.083f,
        1.19f, 0.086f, 1.25f, 0.096f, 1.31f, 0.108f, 1.36f, 0.112f, 1.41f, 0.104f, 1.46f, 0.090f, 1.495f, 0.072f,
        1.52f, 0.058f, 1.55f, 0.052f, 1.62f, 0.050f);

    private static readonly ModelKeys TorsoCenter = new ModelKeys(
        0.845f, 0.015f, 0.93f, 0.0f, 1.03f, 0.008f, 1.14f, 0.02f, 1.25f, 0.028f, 1.36f, 0.036f, 1.46f, 0.015f,
        1.52f, 0.005f, 1.62f, 0.016f);

    private const float TorsoBottom = 0.845f;
    private const float TorsoTop = 1.62f;

    // El peto va de la cintura a la gola; abajo de BreastBottom es brigantina (tela con remaches).
    private const float CuirassBottom = 1.035f;
    private const float BreastBottom = 1.205f;

    // ---------- Brazo y pierna: distancia desde el nacimiento → radio ----------
    private static readonly ModelKeys ArmRadius = new ModelKeys(
        -0.05f, 0.040f, 0f, 0.055f, 0.06f, 0.058f, 0.12f, 0.050f, 0.18f, 0.043f, 0.234f, 0.038f, 0.27f, 0.042f,
        0.32f, 0.040f, 0.38f, 0.032f, 0.43f, 0.026f, 0.445f, 0.025f);

    private static readonly ModelKeys LegRadius = new ModelKeys(
        -0.07f, 0.082f, 0f, 0.090f, 0.08f, 0.092f, 0.16f, 0.085f, 0.26f, 0.072f, 0.35f, 0.060f, 0.41f, 0.056f,
        0.46f, 0.055f, 0.53f, 0.060f, 0.62f, 0.053f, 0.72f, 0.043f, 0.79f, 0.040f, 0.82f, 0.041f);

    // Dónde empiezan las piezas del brazo y la pierna.
    private const float CouterStart = 0.205f, VambraceStart = 0.262f, CuffStart = 0.352f, GloveStart = 0.425f;
    private const float CuisseStart = 0.10f, PoleynStart = 0.365f, GreaveStart = 0.462f;

    // ---------- Cabeza: alto de 0 (mentón) a 1 (coronilla) ----------
    private const float HeadBottom = 1.555f;
    private const float HeadHeight = 0.255f;

    private static readonly ModelKeys HeadWidth = new ModelKeys(
        0f, 0.020f, 0.08f, 0.039f, 0.2f, 0.055f, 0.35f, 0.068f, 0.5f, 0.077f, 0.7f, 0.081f, 0.85f, 0.068f,
        0.95f, 0.042f, 1f, 0.005f);

    private static readonly ModelKeys HeadDepth = new ModelKeys(
        0f, 0.018f, 0.1f, 0.040f, 0.22f, 0.060f, 0.35f, 0.082f, 0.5f, 0.098f, 0.7f, 0.104f, 0.85f, 0.090f,
        0.95f, 0.055f, 1f, 0.006f);

    private static readonly ModelKeys HeadOffset = new ModelKeys(
        0f, 0.060f, 0.1f, 0.047f, 0.22f, 0.032f, 0.35f, 0.022f, 0.5f, 0.012f, 0.7f, 0.005f, 0.85f, 0f, 1f, 0f);

    // Dónde termina el pelo, por ángulo desde el frente.
    private static readonly ModelKeys Hairline = new ModelKeys(
        0f, 0.80f, 20f, 0.77f, 40f, 0.71f, 60f, 0.63f, 90f, 0.43f, 120f, 0.26f, 180f, 0.13f);

    private const float FaceLeft = -0.11f, FaceRight = 0.11f, FaceBottom = 1.54f, FaceTop = 1.83f;

    // ---------- Hombreras, faldas y aureola ----------
    private const float DomeRadius = 0.114f;
    private const float SkirtFlare = 0.36f;
    private static readonly Vector3 HaloCenter = new Vector3(0f, 1.73f, -0.24f);

    private readonly ModelRegion _head, _torso, _arm, _leg, _hair, _cape, _tabard, _plate, _dome, _sabaton, _glove,
                                 _parchment, _medal, _belt, _kneeCop, _bead, _gold, _wax, _lining, _boss, _halo;

    protected override ModelRegion HairRegion => _hair;

    [MenuItem("Mercenarios/Modelos de clase/Generar la Paladina")]
    public static void Generate()
    {
        // Desde el 10 de noviembre de 2026 la Paladina sale de Blender (Blender/paladin.py): esta receta la pisaría
        if (!EditorUtility.DisplayDialog("Generar la Paladina",
                "La Paladina ahora se hace en Blender (Mercenarios ▸ Modelos de clase ▸ Importar los modelos de Blender). " +
                "Esto la reemplaza por el modelo viejo de relleno. ¿Seguir?", "Reemplazar", "Cancelar"))
            return;

        var model = new PaladinModel();
        model.Build();
    }

    private PaladinModel() : base(5531)
    {
        _head      = Atlas.Region(0, 1024, 1024, 1024);
        _torso     = Atlas.Region(1024, 1024, 1024, 1024);
        _arm       = Atlas.Region(0, 0, 512, 1024);
        _leg       = Atlas.Region(512, 0, 512, 1024);
        _hair      = Atlas.Region(1024, 0, 256, 1024);
        _cape      = Atlas.Region(1280, 512, 256, 512);
        _tabard    = Atlas.Region(1536, 512, 256, 512);
        _plate     = Atlas.Region(1792, 512, 256, 256);
        _dome      = Atlas.Region(1792, 768, 256, 256);
        _sabaton   = Atlas.Region(1280, 256, 256, 256);
        _glove     = Atlas.Region(1536, 256, 256, 128);
        _parchment = Atlas.Region(1536, 384, 256, 128);
        _medal     = Atlas.Region(1792, 256, 256, 256);
        _belt      = Atlas.Region(1280, 128, 512, 128);
        _kneeCop   = Atlas.Region(1792, 128, 128, 128);
        _bead      = Atlas.Region(1920, 128, 128, 128);
        _gold      = Atlas.Region(1280, 0, 128, 128);
        _wax       = Atlas.Region(1408, 0, 128, 128);
        _lining    = Atlas.Region(1536, 0, 128, 128);
        _boss      = Atlas.Region(1664, 0, 128, 128);
        _halo      = Atlas.Region(1792, 0, 256, 128);
    }

    private void Build()
    {
        PaintAtlas();

        BuildTorso();
        BuildCuirass();
        BuildGorget();
        BuildHead();
        foreach (float side in new[] { 1f, -1f })
        {
            BuildArm(side);
            BuildHand(side);
            BuildPauldron(side);
            BuildLeg(side);
            BuildSabaton(side);
            BuildTassets(side);
        }

        BuildCape();
        BuildTabard();
        BuildBelts();
        BuildRosary();
        BuildSeals();
        if (WithHalo) BuildHalo();
        BuildHair();

        Kit.Save(Name, ArtFolder, PrefabFolder, Atlas, ModelFinish.Polished);
    }

    // =========================================================
    // TORSO Y PETO
    // =========================================================

    private static Vector3 TorsoDirection(float deg)
    {
        float a = deg * Mathf.Deg2Rad;
        float s = Mathf.Sin(a), c = Mathf.Cos(a);
        const float round = 2f / 2.5f;
        return new Vector3(Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), round), 0f, Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), round));
    }

    private static Vector3 TorsoPoint(float y, float deg, float offset)
    {
        Vector3 d = TorsoDirection(deg);
        return new Vector3(d.x * (TorsoWidth.At(y) + offset), y, TorsoCenter.At(y) + d.z * (TorsoDepth.At(y) + offset));
    }

    // Hacia afuera del eje del cuerpo, a esa altura.
    private static Vector3 Radial(Vector3 p) => new Vector3(p.x, 0f, p.z - TorsoCenter.At(p.y)).normalized;

    private ModelSkin TorsoSkin(Vector3 p)
    {
        ModelSkin skin = Hips.To(Spine, Smooth(1.0f, 1.1f, p.y));
        skin = skin.To(Chest, Smooth(1.19f, 1.3f, p.y));
        skin = skin.To(Neck, Smooth(1.5f, 1.54f, p.y));
        skin = skin.To(HeadBone, Smooth(1.58f, 1.6f, p.y));

        string side = p.x > 0f ? ".R" : ".L";
        float shoulder = Smooth(0.12f, 0.19f, Mathf.Abs(p.x)) * Smooth(1.36f, 1.42f, p.y);
        skin = skin.To(Kit.Bone("B-shoulder" + side), shoulder * 0.5f);
        skin = skin.To(Kit.Bone("B-upperArm" + side), shoulder * 0.3f);

        float thigh = Smooth(0.95f, 0.86f, p.y) * Smooth(0.02f, 0.09f, Mathf.Abs(p.x));
        return skin.To(Kit.Bone("B-thigh" + side), thigh * 0.6f);
    }

    private void BuildTorso()
    {
        Kit.BeginPiece(55f);

        float[] heights =
        {
            0.845f, 0.87f, 0.90f, 0.93f, 0.97f, 1.01f, 1.05f, 1.09f, 1.13f, 1.17f, 1.21f, 1.25f, 1.28f, 1.31f, 1.335f,
            1.36f, 1.385f, 1.41f, 1.44f, 1.47f, 1.495f, 1.52f, 1.55f, 1.58f, 1.62f,
        };
        const int columns = 26;

        var rings = new ModelPoint[heights.Length][];
        for (int r = 0; r < heights.Length; r++)
        {
            float y = heights[r];
            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float u = i / (float)columns;
                Vector3 p = TorsoPoint(y, (u - 0.5f) * 360f, 0f);
                rings[r][i] = new ModelPoint(p, _torso.Uv(u, Mathf.InverseLerp(TorsoBottom, TorsoTop, y)), TorsoSkin(p));
            }
        }
        Kit.Loft(rings, capStart: true);
    }

    // El borde de arriba del peto, donde nace la gola: un poco más bajo adelante.
    private static float CuirassTop(float deg) => 1.488f - 0.014f * Smooth(70f, 0f, Mathf.Abs(Wrap(deg)));

    // El peto con forma: dos copas.
    private static float Bust(float y, float deg) =>
        0.040f * Bell(y, 1.345f, 0.058f) * (Bell(deg, 30f, 21f) + Bell(deg, -30f, 21f));

    private static Vector3 CuirassPoint(float y, float deg, float offset)
    {
        float plate = Smooth(BreastBottom - 0.008f, BreastBottom + 0.004f, y);
        float a = Mathf.Abs(Wrap(deg));
        float shell = Mathf.Lerp(0.008f, 0.016f, plate) + plate * (Bust(y, deg) + 0.004f * Bell(a, 180f, 7f));
        return TorsoPoint(y, deg, offset + shell);
    }

    private void BuildCuirass()
    {
        Kit.BeginPiece(48f);
        const int columns = 56;

        // Cada fila: su altura según el ángulo, y si es el labio que se cierra contra el cuerpo
        var rows = new List<Func<float, (float y, bool lip)>>();
        rows.Add(deg => (CuirassBottom, true));
        foreach (float h in new[] { 1.035f, 1.06f, 1.10f, 1.14f, 1.175f, 1.198f, 1.206f, 1.22f, 1.25f, 1.28f, 1.305f, 1.325f, 1.345f, 1.368f, 1.392f, 1.418f, 1.442f })
        {
            float height = h;
            rows.Add(deg => (height, false));
        }
        foreach (float k in new[] { 0.5f, 0.88f, 1f })
        {
            float fraction = k;
            rows.Add(deg => (Mathf.Lerp(1.442f, CuirassTop(deg), fraction), false));
        }
        rows.Add(deg => (CuirassTop(deg), true));

        var rings = new ModelPoint[rows.Count][];
        for (int r = 0; r < rows.Count; r++)
        {
            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float u = i / (float)columns;
                float deg = (u - 0.5f) * 360f;
                (float y, bool lip) = rows[r](deg);
                Vector3 p = lip ? TorsoPoint(y, deg, 0.002f) : CuirassPoint(y, deg, 0f);
                rings[r][i] = new ModelPoint(p, _torso.Uv(u, Mathf.InverseLerp(TorsoBottom, TorsoTop, y)), TorsoSkin(p));
            }
        }
        Kit.Loft(rings);
    }

    // La gola: un cuello alto y cerrado, más alto atrás, con la medalla adelante.
    private static float GorgetTop(float deg) => 1.535f + 0.065f * Smooth(40f, 150f, Mathf.Abs(Wrap(deg)));

    private void BuildGorget()
    {
        Kit.BeginPiece(36f);
        const int columns = 32;
        float[] rows = { 0f, 0.45f, 0.85f, 0.97f, 1f };

        var rings = new ModelPoint[rows.Length][];
        for (int r = 0; r < rows.Length; r++)
        {
            float t = rows[r];
            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float s = i / (float)columns;
                float deg = (s - 0.5f) * 360f;
                float y = Mathf.Lerp(1.47f, GorgetTop(deg), t);
                float radius = Mathf.Lerp(0.100f, 0.088f, Smooth(0f, 0.6f, t)) + 0.011f * Smooth(0.82f, 1f, t);
                float rad = deg * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Sin(rad) * radius, y, 0.012f + Mathf.Cos(rad) * radius * 1.04f);
                rings[r][i] = new ModelPoint(p, _plate.Uv(s, Mathf.Lerp(0.1f, 0.97f, t)), Chest.To(Neck, 0.45f * t));
            }
        }
        Kit.Sheet(rings, _plate.Uv(0.5f, 0.35f), new Vector3(0f, 1.54f, 0.012f));

        // La medalla con la flor de lis
        Kit.BeginPiece(40f);
        Medallion(new Vector3(0f, 1.502f, 0.111f), new Vector3(0f, 0.12f, 1f).normalized, Vector3.up, 0.022f, 0.006f, _medal, Chest.To(Neck, 0.2f));
    }

    // Un disco abombado con la imagen de su región de frente (Kit.Disc pinta por anillos; este, plano).
    private void Medallion(Vector3 center, Vector3 normal, Vector3 up, float radius, float height, ModelRegion region, ModelSkin skin)
    {
        Vector3 side = Vector3.Cross(normal, up).normalized;
        up = Vector3.Cross(side, normal).normalized;
        float[] scale = { 1f, 1f, 0.92f, 0.7f, 0.45f, 0.2f, 0f };
        float[] lift = { -0.25f, 0.45f, 0.8f, 0.95f, 1.02f, 1.06f, 1.08f };
        const int sides = 16;

        var rings = new ModelPoint[scale.Length][];
        for (int r = 0; r < scale.Length; r++)
        {
            rings[r] = new ModelPoint[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                float cx = Mathf.Cos(a) * scale[r], cy = Mathf.Sin(a) * scale[r];
                Vector3 p = center + side * (cx * radius) + up * (cy * radius) + normal * (height * lift[r]);
                rings[r][i] = new ModelPoint(p, region.Uv(0.5f + 0.5f * cx, 0.5f + 0.5f * cy), skin);
            }
        }
        Kit.Loft(rings, wrap: true);
    }

    // =========================================================
    // CABEZA
    // =========================================================

    private static Vector3 HeadPoint(float h, float deg, float offset)
    {
        float a = deg * Mathf.Deg2Rad;
        float s = Mathf.Sin(a), c = Mathf.Cos(a);
        const float round = 2f / 2.3f;
        float x = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), round) * HeadWidth.At(h);
        float z = HeadOffset.At(h) + Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), round) * HeadDepth.At(h);

        float abs = Mathf.Abs(deg);
        if (offset <= 0f && abs < 90f)
        {
            z += 0.015f * Bell(deg, 0f, 7.5f) * Bell(h, 0.345f, 0.05f);                      // nariz
            z += 0.005f * Bell(deg, 0f, 6f) * Smooth(0.36f, 0.42f, h) * (1f - Smooth(0.48f, 0.56f, h));
            z += 0.006f * Bell(h, 0.565f, 0.04f) * Bell(deg, 0f, 38f);                       // ceja
            z -= 0.007f * Bell(abs, 21f, 13f) * Bell(h, 0.5f, 0.045f);                       // cuenca del ojo
            z += 0.005f * Bell(deg, 0f, 16f) * Bell(h, 0.2f, 0.03f);                         // labios
            z -= 0.003f * Bell(deg, 0f, 20f) * Bell(h, 0.14f, 0.02f);                        // bajo el labio
            z += 0.004f * Bell(deg, 0f, 18f) * Bell(h, 0.07f, 0.04f);                        // mentón
            x *= 1f + 0.05f * Bell(h, 0.36f, 0.06f);                                         // pómulos
        }

        var p = new Vector3(x, HeadBottom + HeadHeight * h, 0.02f + z);
        return p + (p - HeadCenter).normalized * offset;
    }

    private Vector2 HeadUv(Vector3 p) =>
        _head.Uv(Mathf.InverseLerp(FaceLeft, FaceRight, p.x), Mathf.InverseLerp(FaceBottom, FaceTop, p.y));

    private void BuildHead()
    {
        Kit.BeginPiece(85f);

        float[] heights =
        {
            0f, 0.05f, 0.1f, 0.15f, 0.2f, 0.25f, 0.3f, 0.34f, 0.38f, 0.42f, 0.46f, 0.5f, 0.55f, 0.6f, 0.66f, 0.72f,
            0.8f, 0.88f, 0.95f, 1f,
        };
        float[] half = { 0f, 5f, 11f, 20f, 32f, 46f, 62f, 82f, 105f, 130f, 155f, 180f };

        var degrees = new List<float>();
        for (int i = half.Length - 1; i > 0; i--) degrees.Add(-half[i]);
        degrees.AddRange(half);

        var rings = new ModelPoint[heights.Length][];
        for (int r = 0; r < heights.Length; r++)
        {
            rings[r] = new ModelPoint[degrees.Count];
            for (int i = 0; i < degrees.Count; i++)
            {
                Vector3 p = HeadPoint(heights[r], degrees[i], 0f);
                rings[r][i] = new ModelPoint(p, HeadUv(p), HeadBone);
            }
        }
        Kit.Loft(rings, capStart: true, capEnd: true);
    }

    // =========================================================
    // BRAZOS Y MANOS
    // =========================================================

    private ModelSkin ArmSkin(string side, float d)
    {
        ModelSkin skin = Kit.Bone("B-shoulder" + side).To(Kit.Bone("B-upperArm" + side), Smooth(-0.04f, 0.03f, d));
        skin = skin.To(Kit.Bone("B-forearm" + side), Smooth(0.205f, 0.26f, d));
        return skin.To(Kit.Bone("B-hand" + side), Smooth(0.415f, 0.44f, d));
    }

    private static float ArmBulge(float d)
    {
        float radius = ArmRadius.At(d)
                     + 0.008f * Smooth(CouterStart - 0.004f, CouterStart, d) * (1f - Smooth(VambraceStart - 0.01f, VambraceStart, d))
                     + 0.010f * Smooth(VambraceStart - 0.003f, VambraceStart, d);
        if (d < CuffStart) return radius;

        // El puño del guantelete se abre hacia el codo y se cierra en la muñeca, donde empieza el guante
        return Mathf.Lerp(0.056f, 0.032f, Smooth(CuffStart, GloveStart, d));
    }

    private void BuildArm(float side)
    {
        Kit.BeginPiece(42f);
        string name = SideName(side);
        Vector3[] chain = ArmChain(name);

        float[] stops =
        {
            -0.05f, -0.02f, 0.01f, 0.05f, 0.09f, 0.13f, 0.17f, 0.198f, 0.205f, 0.222f, 0.234f, 0.246f, 0.254f, 0.259f, 0.262f,
            0.285f, 0.31f, 0.335f, 0.346f, 0.35f, 0.352f, 0.358f, 0.38f, 0.405f, 0.425f, 0.436f, 0.446f,
        };
        const int columns = 12;

        var rings = new ModelPoint[stops.Length][];
        for (int r = 0; r < stops.Length; r++)
        {
            float d = stops[r];
            AlongChain(chain, d, out Vector3 center, out Vector3 tangent);
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, tangent).normalized;
            Vector3 front = Vector3.Cross(tangent, up).normalized * side;
            float radius = ArmBulge(d);

            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float u = i / (float)columns;
                float a = (u - 0.5f) * Mathf.PI * 2f;
                Vector3 p = center + up * (Mathf.Cos(a) * radius) + front * (Mathf.Sin(a) * radius * 0.94f);
                rings[r][i] = new ModelPoint(p, _arm.Uv(u, Mathf.InverseLerp(-0.05f, 0.446f, d)), ArmSkin(name, d));
            }
        }
        Kit.Loft(rings);

        // La cubierta del codo
        Kit.BeginPiece(32f);
        AlongChain(chain, 0.234f, out Vector3 elbow, out Vector3 armAxis);
        Vector3 back = Vector3.ProjectOnPlane(Vector3.back, armAxis).normalized;
        Kit.Disc(elbow + back * 0.044f, back, armAxis, 0.044f, 0.05f, 0.02f, 10, _boss, ArmSkin(name, 0.234f));
    }

    // Guantes de cuero oscuro: la palma y los dedos, un poco más cortos que los del esqueleto.
    private void BuildHand(float side)
    {
        string name = SideName(side);
        ModelSkin hand = Kit.Bone("B-hand" + name);
        Vector3 wrist = Kit.Pos("B-hand" + name);
        Vector3 knuckle = (Kit.Pos("B-indexFinger01" + name) + Kit.Pos("B-pinky01" + name)) * 0.5f;
        Vector3 axis = (knuckle - wrist).normalized;
        Vector3 flat = Vector3.ProjectOnPlane(Vector3.up, axis).normalized;
        Vector3 wide = Vector3.Cross(axis, flat).normalized;
        float length = Vector3.Distance(wrist, knuckle);

        Kit.BeginPiece(60f);
        float[] along = { -0.05f, 0.3f, 0.7f, 1f, 1.1f };
        float[] widths = { 0.028f, 0.045f, 0.056f, 0.058f, 0.050f };
        float[] thick = { 0.024f, 0.024f, 0.022f, 0.018f, 0.013f };
        const int columns = 10;

        var rings = new ModelPoint[along.Length][];
        for (int r = 0; r < along.Length; r++)
        {
            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float a = i / (float)columns * Mathf.PI * 2f;
                Vector3 p = wrist + axis * (along[r] * length) + wide * (Mathf.Cos(a) * widths[r]) + flat * (Mathf.Sin(a) * thick[r]);
                rings[r][i] = new ModelPoint(p, _glove.Uv(i / (float)columns, 0.05f + 0.12f * r / (along.Length - 1f)), hand);
            }
        }
        Kit.Loft(rings, capEnd: true);

        foreach (string finger in new[] { "B-indexFinger", "B-middleFinger", "B-ringFinger", "B-pinky", "B-thumb" })
        {
            Kit.BeginPiece(60f);
            bool thumb = finger == "B-thumb";
            BuildFinger(finger, name, thumb ? 0.0172f : 0.0145f, 6, (t, a) => _glove.Uv(a, 0.22f + t * 0.74f), thumb ? 0.7f : 0.82f, 0.55f);
        }
    }

    // =========================================================
    // HOMBRERAS
    // =========================================================

    private ModelSkin PauldronSkin(string side, float d) =>
        Chest.To(Kit.Bone("B-shoulder" + side), Smooth(-0.11f, -0.05f, d))
             .To(Kit.Bone("B-upperArm" + side), Smooth(-0.05f, 0.06f, d) * 0.92f);

    // El marco de la hombrera: el hueso del brazo, hacia dónde va el brazo, arriba y adelante.
    private void PauldronFrame(float side, out Vector3 joint, out Vector3 axis, out Vector3 up, out Vector3 front)
    {
        string name = SideName(side);
        joint = Kit.Pos("B-upperArm" + name);
        axis = (Kit.Pos("B-forearm" + name) - joint).normalized;
        up = Vector3.ProjectOnPlane(Vector3.up, axis).normalized;
        front = Vector3.Cross(axis, up).normalized;
        if (front.z < 0f) front = -front;
    }

    private void BuildPauldron(float side)
    {
        string name = SideName(side);
        PauldronFrame(side, out Vector3 joint, out Vector3 axis, out Vector3 up, out Vector3 front);
        Vector3 origin = joint + up * 0.006f;

        // De abajo hacia arriba: cada lámina queda tapada por la de arriba
        Lame(origin, axis, up, front, name, 0.128f, 0.19f, 0.088f, 0.096f, 118f);
        Lame(origin, axis, up, front, name, 0.078f, 0.14f, 0.096f, 0.104f, 120f);
        Lame(origin, axis, up, front, name, 0.03f, 0.092f, 0.104f, 0.112f, 122f);

        // La cúpula de arriba, que se cierra hacia el cuello
        Kit.BeginPiece(30f);
        const int columns = 18;
        float[] stops = { -0.118f, -0.105f, -0.08f, -0.05f, -0.02f, 0.01f, 0.03f, 0.05f, 0.058f };
        const float halfArc = 128f;
        var rings = new ModelPoint[stops.Length][];
        for (int r = 0; r < stops.Length; r++)
        {
            float d = stops[r];
            float radius = DomeAt(d) + (r == stops.Length - 1 ? -0.007f : 0f);
            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float s = i / (float)columns;
                float a = (s - 0.5f) * 2f * halfArc * Mathf.Deg2Rad;
                Vector3 p = origin + axis * d + up * (Mathf.Cos(a) * radius) + front * (Mathf.Sin(a) * radius * 0.96f);
                rings[r][i] = new ModelPoint(p, _dome.Uv(s, Mathf.InverseLerp(-0.118f, 0.058f, d)), PauldronSkin(name, d));
            }
        }
        Kit.Sheet(rings, _lining.Uv(0.25f, 0.5f), joint);
    }

    // El radio de la cúpula a esa distancia por el brazo: se cierra hacia el cuello.
    private static float DomeAt(float d) =>
        DomeRadius * Mathf.Sqrt(Mathf.Max(0.02f, 1f - Sq(Mathf.Min(0f, d - 0.015f) / 0.137f)));

    private void Lame(Vector3 origin, Vector3 axis, Vector3 up, Vector3 front, string side, float from, float to,
                      float radiusFrom, float radiusTo, float halfArc)
    {
        Kit.BeginPiece(30f);
        const int columns = 16;
        float[] rows = { 0f, 0.5f, 0.92f, 1f };
        var rings = new ModelPoint[rows.Length][];
        for (int r = 0; r < rows.Length; r++)
        {
            float t = rows[r];
            float d = Mathf.Lerp(from, to, t);
            float radius = Mathf.Lerp(radiusFrom, radiusTo, t) - (r == rows.Length - 1 ? 0.008f : 0f);
            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float s = i / (float)columns;
                float a = (s - 0.5f) * 2f * halfArc * Mathf.Deg2Rad;

                // Las puntas de la lámina se acortan: la hombrera no termina en una línea recta
                float tuck = 0.018f * Sq(s * 2f - 1f) * t;
                Vector3 p = origin + axis * (d - tuck) + up * (Mathf.Cos(a) * radius) + front * (Mathf.Sin(a) * radius * 0.96f);
                rings[r][i] = new ModelPoint(p, _plate.Uv(s, Mathf.Lerp(0.08f, 0.97f, t)), PauldronSkin(side, d));
            }
        }
        Kit.Sheet(rings, _lining.Uv(0.25f, 0.5f), origin + axis * ((from + to) * 0.5f));
    }

    // =========================================================
    // PIERNAS
    // =========================================================

    private ModelSkin LegSkin(string side, float d)
    {
        ModelSkin skin = Hips.To(Kit.Bone("B-thigh" + side), Smooth(-0.06f, 0.03f, d));
        skin = skin.To(Kit.Bone("B-shin" + side), Smooth(0.38f, 0.44f, d));
        return skin.To(Kit.Bone("B-foot" + side), Smooth(0.77f, 0.81f, d));
    }

    private static float LegBulge(float d, float angle)
    {
        float abs = Mathf.Abs(angle);
        float front = Smooth(110f, 60f, abs);
        float radius = LegRadius.At(d);
        radius += 0.008f * front * Smooth(CuisseStart - 0.01f, CuisseStart, d) * (1f - Smooth(PoleynStart - 0.012f, PoleynStart, d));
        radius += 0.009f * Smooth(PoleynStart - 0.012f, PoleynStart, d) * (1f - Smooth(GreaveStart - 0.01f, GreaveStart, d));
        radius += 0.012f * Smooth(GreaveStart - 0.008f, GreaveStart, d);
        radius += 0.007f * Bell(abs, 0f, 19f) * Smooth(GreaveStart, GreaveStart + 0.03f, d);   // la arista de la greba
        return radius;
    }

    private void BuildLeg(float side)
    {
        Kit.BeginPiece(42f);
        string name = SideName(side);
        Vector3[] chain = LegChain(name);

        float[] stops =
        {
            -0.07f, -0.03f, 0.01f, 0.05f, 0.09f, 0.10f, 0.15f, 0.2f, 0.25f, 0.3f, 0.34f, 0.353f, 0.365f, 0.39f, 0.41f, 0.435f,
            0.452f, 0.462f, 0.48f, 0.5f, 0.54f, 0.58f, 0.62f, 0.66f, 0.7f, 0.74f, 0.78f, 0.82f,
        };
        const int columns = 16;

        var rings = new ModelPoint[stops.Length][];
        for (int r = 0; r < stops.Length; r++)
        {
            float d = stops[r];
            AlongChain(chain, d, out Vector3 center, out Vector3 tangent);
            Vector3 front = Vector3.ProjectOnPlane(Vector3.forward, tangent).normalized;
            Vector3 outer = Vector3.Cross(front, tangent).normalized;
            if (Mathf.Sign(outer.x) != Mathf.Sign(side)) outer = -outer;

            center += front * (0.008f * Bell(d, 0.41f, 0.03f) - 0.012f * Bell(d, 0.54f, 0.09f));

            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float u = i / (float)columns;
                float a = (u - 0.5f) * Mathf.PI * 2f;
                float radius = LegBulge(d, (u - 0.5f) * 360f);
                Vector3 p = center + front * (Mathf.Cos(a) * radius) + outer * (Mathf.Sin(a) * radius * 0.95f);
                rings[r][i] = new ModelPoint(p, _leg.Uv(u, Mathf.InverseLerp(-0.07f, 0.82f, d)), LegSkin(name, d));
            }
        }
        Kit.Loft(rings);

        // La rodillera, con su flor de lis
        Kit.BeginPiece(40f);
        AlongChain(chain, 0.412f, out Vector3 knee, out Vector3 legAxis);
        Vector3 forward = Vector3.ProjectOnPlane(Vector3.forward, legAxis).normalized;
        ModelSkin kneeSkin = Kit.Bone("B-thigh" + name).To(Kit.Bone("B-shin" + name), 0.55f);
        Medallion(knee + forward * 0.07f, forward, -legAxis, 0.048f, 0.02f, _kneeCop, kneeSkin);
    }

    private void BuildSabaton(float side)
    {
        Kit.BeginPiece(38f);
        string name = SideName(side);
        ModelSkin foot = Kit.Bone("B-foot" + name);
        ModelSkin toe = Kit.Bone("B-toe" + name);
        float x = Kit.Pos("B-foot" + name).x;

        float[] z      = { -0.096f, -0.086f, -0.05f, 0.0f, 0.05f, 0.095f, 0.14f, 0.18f, 0.212f, 0.232f };
        float[] half   = { 0.026f, 0.043f, 0.050f, 0.052f, 0.052f, 0.054f, 0.056f, 0.050f, 0.035f, 0.011f };
        float[] bottom = { 0.015f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.004f, 0.013f };
        float[] top    = { 0.088f, 0.122f, 0.16f, 0.172f, 0.138f, 0.106f, 0.088f, 0.07f, 0.051f, 0.03f };
        const int columns = 14;

        var rings = new ModelPoint[z.Length][];
        for (int r = 0; r < z.Length; r++)
        {
            rings[r] = new ModelPoint[columns];
            ModelSkin skin = foot.To(toe, Smooth(0.08f, 0.13f, z[r]));
            for (int i = 0; i < columns; i++)
            {
                float a = i / (float)columns * Mathf.PI * 2f;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                float round = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 0.75f);
                var p = new Vector3(x + c * half[r], Mathf.Lerp(bottom[r], top[r], 0.5f + 0.5f * round), z[r]);
                float sole = Smooth(0.03f, 0.0f, p.y);
                float v = Mathf.Lerp(0.12f + 0.86f * Mathf.InverseLerp(-0.1f, 0.232f, z[r]), 0.04f, sole);
                rings[r][i] = new ModelPoint(p, _sabaton.Uv(0.5f + 0.45f * c, v), skin);
            }
        }
        Kit.Loft(rings, wrap: true, capStart: true, capEnd: true);
    }

    // Las escarcelas: láminas blancas sobre la cadera y el muslo, por fuera.
    private void BuildTassets(float side)
    {
        string name = SideName(side);
        Vector3[] chain = LegChain(name);
        float[,] plates = { { 0.07f, 0.17f }, { 0.0f, 0.105f }, { -0.075f, 0.035f } };

        for (int n = 0; n < plates.GetLength(0); n++)
        {
            Kit.BeginPiece(30f);
            float from = plates[n, 0], to = plates[n, 1];
            const int columns = 10;
            float[] rows = { 0f, 0.5f, 0.9f, 1f };

            var rings = new ModelPoint[rows.Length][];
            for (int r = 0; r < rows.Length; r++)
            {
                float t = rows[r];
                float d = Mathf.Lerp(from, to, t);
                TassetFrame(chain, side, d, out Vector3 center, out Vector3 tangent, out Vector3 front, out Vector3 outer);

                float radius = TassetRadius(d, t) - (r == rows.Length - 1 ? 0.007f : 0f);
                rings[r] = new ModelPoint[columns + 1];
                for (int i = 0; i <= columns; i++)
                {
                    float s = i / (float)columns;
                    float a = Mathf.Lerp(-12f, 120f, s) * Mathf.Deg2Rad;
                    float tuck = 0.02f * Sq(s * 2f - 1f) * t;
                    Vector3 p = center - tangent * tuck + front * (Mathf.Cos(a) * radius) + outer * (Mathf.Sin(a) * radius);
                    rings[r][i] = new ModelPoint(p, _plate.Uv(s, Mathf.Lerp(0.08f, 0.97f, t)), TassetSkin(name, d));
                }
            }
            AlongChain(chain, (from + to) * 0.5f, out Vector3 inside, out _);
            Kit.Sheet(rings, _lining.Uv(0.25f, 0.5f), inside);
        }
    }

    private static float TassetRadius(float d, float t) => LegRadius.At(d) + 0.04f + 0.008f * t;

    private ModelSkin TassetSkin(string side, float d) => Hips.To(Kit.Bone("B-thigh" + side), Smooth(-0.04f, 0.06f, d));

    private static void TassetFrame(Vector3[] chain, float side, float d, out Vector3 center, out Vector3 tangent, out Vector3 front, out Vector3 outer)
    {
        AlongChain(chain, d, out center, out tangent);
        front = Vector3.ProjectOnPlane(Vector3.forward, tangent).normalized;
        outer = Vector3.Cross(front, tangent).normalized;
        if (Mathf.Sign(outer.x) != Mathf.Sign(side)) outer = -outer;
    }

    // =========================================================
    // CAPA, TABARDO Y CINTURONES
    // =========================================================

    private ModelSkin CapeSkin(Vector3 p)
    {
        ModelSkin skin = Chest.To(Spine, Smooth(1.32f, 1.14f, p.y));
        skin = skin.To(Hips, Smooth(1.14f, 0.98f, p.y));

        // Abajo cada mitad acompaña un poco a su pierna: así el talón no la atraviesa al correr
        float reach = 0.34f * Smooth(0.95f, 0.6f, p.y);
        float right = Smooth(-0.22f, 0.22f, p.x);
        skin = skin.To(Kit.Bone("B-thigh.R"), reach * right);
        return skin.To(Kit.Bone("B-thigh.L"), reach * (1f - right) / Mathf.Max(0.001f, 1f - reach * right));
    }

    private void BuildCape()
    {
        Kit.BeginPiece(30f);
        const int columns = 20, rows = 12;
        const float top = 1.49f, bottom = 0.52f;

        var hem = new float[columns + 1];
        for (int i = 0; i <= columns; i++) hem[i] = Range(-0.04f, 0.03f);

        var rings = new ModelPoint[rows + 1][];
        for (int r = 0; r <= rows; r++)
        {
            float t = r / (float)rows;
            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float u = i / (float)columns;
                float s = u * 2f - 1f;
                float y = Mathf.Lerp(top, bottom, t) - 0.03f * s * s * (1f - t) + (r == rows ? hem[i] : 0f);
                float width = Mathf.Lerp(0.16f, 0.30f, Smooth(0f, 0.7f, t));
                float fold = Mathf.Sin(s * 9.5f + 0.9f) * 0.034f * Smooth(0.08f, 0.6f, t)
                           + Mathf.Sin(s * 19f + 2.1f) * 0.011f * Smooth(0.3f, 0.9f, t);
                float z = -0.135f - 0.11f * Smooth(0f, 1f, t) + s * s * Mathf.Lerp(0.08f, 0.05f, t) + fold;
                var p = new Vector3(s * width, y, z);
                rings[r][i] = new ModelPoint(p, _cape.Uv(u, 1f - t), CapeSkin(p));
            }
        }
        Kit.Sheet(rings, _lining.Uv(0.75f, 0.5f));
    }

    private static Vector3 SkirtPoint(float y, float deg, float offset, float flare)
    {
        Vector3 d = TorsoDirection(deg);
        Vector3 p = TorsoPoint(Mathf.Max(y, 0.97f), deg, offset);
        p += d * (Mathf.Max(0f, 0.97f - y) * flare);
        p.y = y;
        return p;
    }

    // El centro sigue a las dos piernas por igual: con las piernas abiertas cuelga derecho.
    private ModelSkin TabardSkin(Vector3 p, float t)
    {
        float reach = Mathf.Min(1f, Mathf.Pow(Mathf.Max(0f, t), 0.8f) * 1.1f) * 0.8f * Smooth(1.03f, 0.84f, p.y);
        float right = Smooth(-0.17f, 0.17f, p.x);
        return Hips.To(Kit.Bone("B-thigh.R"), reach * right).To(Kit.Bone("B-thigh.L"), reach * (1f - right) / Mathf.Max(0.001f, 1f - reach * right));
    }

    private void BuildTabard()
    {
        Kit.BeginPiece(26f);
        const int columns = 10, rows = 8;
        var rings = new ModelPoint[rows + 1][];
        for (int r = 0; r <= rows; r++)
        {
            float t = r / (float)rows;
            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float u = i / (float)columns;
                float s = u * 2f - 1f;
                float deg = s * Mathf.Lerp(34f, 28f, t);
                float hem = 0.47f + 0.08f * Mathf.Abs(s);
                float y = Mathf.Lerp(1.022f, hem, t);
                float offset = Mathf.Lerp(0.012f, 0.026f, Smooth(0f, 0.25f, t)) + 0.006f * Mathf.Sin(s * 7f) * t;
                Vector3 p = SkirtPoint(y, deg, offset, SkirtFlare) + Vector3.forward * (0.012f * t);
                rings[r][i] = new ModelPoint(p, _tabard.Uv(u, 1f - t), TabardSkin(p, t));
            }
        }
        Kit.Sheet(rings, _lining.Uv(0.75f, 0.5f));
    }

    private void Band(Func<float, float> height, float tall, float offset)
    {
        Kit.BeginPiece(40f);
        const int columns = 32;
        float[] rows = { -0.5f, -0.42f, 0.42f, 0.5f };
        float[] lift = { 0f, 1f, 1f, 0f };

        var rings = new ModelPoint[rows.Length][];
        for (int r = 0; r < rows.Length; r++)
        {
            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float u = i / (float)columns;
                float deg = (u - 0.5f) * 360f;
                float y = height(deg) + rows[r] * tall;
                Vector3 p = TorsoPoint(Mathf.Max(y, 0.97f), deg, offset + lift[r] * 0.007f);
                p.y = y;
                rings[r][i] = new ModelPoint(p, _belt.Uv(u, 0.5f + rows[r]), TorsoSkin(p));
            }
        }
        Kit.Loft(rings);
    }

    private static float HipBelt(float deg) => 0.985f + 0.03f * Mathf.Cos((deg + 35f) * Mathf.Deg2Rad);

    private void BuildBelts()
    {
        Band(deg => 1.04f, 0.044f, 0.018f);
        Band(HipBelt, 0.036f, 0.034f);

        // Las hebillas: chapas de oro
        Kit.BeginPiece(30f);
        Vector3 turned = (Vector3.up + Vector3.right).normalized;
        Kit.Disc(TorsoPoint(1.04f, 0f, 0.026f), Vector3.forward, turned, 0.034f, 0.034f, 0.01f, 4, _gold, Spine.To(Hips, 0.5f));
        Kit.Disc(TorsoPoint(HipBelt(-35f), -35f, 0.042f), TorsoDirection(-35f).normalized, turned, 0.028f, 0.028f, 0.009f, 4, _gold, Hips);
    }

    // =========================================================
    // FANÁTICA: ROSARIO, SELLOS Y AUREOLA
    // =========================================================

    // Un rosario colgado en U del cinturón, sobre el borde izquierdo del tabardo, con una cruz.
    private void BuildRosary()
    {
        Func<Vector3, float, ModelSkin> skin = (p, t) => TabardSkin(p, Mathf.InverseLerp(1.022f, 0.47f, p.y));

        // Las cuentas: un tubo que se ensancha y se angosta
        const int beads = 22;
        const int steps = beads * 5;
        var loop = new Vector3[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            float s = i / (float)steps;
            float deg = Mathf.Lerp(-16f, -42f, s);
            float y = 1.022f - 0.15f * Mathf.Sin(Mathf.PI * s);
            loop[i] = SkirtPoint(y, deg, 0.044f, SkirtFlare);
        }
        Kit.BeginPiece(70f);
        Kit.Sweep(loop, Radial, t => Vector2.one * (0.0072f * (0.45f + 0.55f * Mathf.Abs(Mathf.Sin(t * beads * Mathf.PI)))), 6,
            (t, a) => _bead.Uv(a, 0.5f), skin, capStart: true, capEnd: true);

        // De lo más bajo cuelgan cinco cuentas más y la cruz
        const float dropDeg = -29f;
        var drop = new Vector3[26];
        for (int i = 0; i < drop.Length; i++)
            drop[i] = SkirtPoint(Mathf.Lerp(0.873f, 0.812f, i / (drop.Length - 1f)), dropDeg, 0.046f, SkirtFlare);
        Kit.Sweep(drop, Radial, t => Vector2.one * (0.0056f * (0.45f + 0.55f * Mathf.Abs(Mathf.Sin(t * 5f * Mathf.PI)))), 6,
            (t, a) => _bead.Uv(a, 0.5f), skin, capStart: true, capEnd: true);

        Vector3 top = SkirtPoint(0.81f, dropDeg, 0.048f, SkirtFlare);
        Vector3 bottom = SkirtPoint(0.752f, dropDeg, 0.048f, SkirtFlare);
        Vector3 normal = Radial(top);
        Vector3 across = Vector3.Cross(Vector3.up, normal).normalized;
        Vector3 middle = Vector3.Lerp(top, bottom, 0.3f);
        ModelSkin crossSkin = skin(top, 0f);
        Bar(top, bottom, normal, 0.0042f, 0.003f, crossSkin);
        Bar(middle - across * 0.017f, middle + across * 0.017f, normal, 0.0042f, 0.003f, crossSkin);
    }

    // Una barra de oro (los brazos de la cruz).
    private void Bar(Vector3 from, Vector3 to, Vector3 normal, float half, float thick, ModelSkin skin)
    {
        Kit.BeginPiece(40f);
        Kit.Sweep(new[] { from, to }, p => normal, t => new Vector2(half, thick), 4,
            (t, a) => _gold.Uv(a, 0.3f + 0.6f * t), (p, t) => skin, capStart: true, capEnd: true);
    }

    // Los sellos de cera con tiras de pergamino: en la hombrera izquierda, sobre el pecho derecho y en la
    // escarcela derecha. Cada tira sigue la superficie de la pieza de la que cuelga.
    private void BuildSeals()
    {
        // 1. La hombrera izquierda: las tiras caen por el brazo, así cuelgan con el brazo abajo
        {
            const float side = -1f;
            string name = SideName(side);
            PauldronFrame(side, out Vector3 joint, out Vector3 axis, out Vector3 up, out Vector3 front);
            Vector3 origin = joint + up * 0.006f;
            Vector3 Around(float angle) => up * Mathf.Cos(angle * Mathf.Deg2Rad) + front * Mathf.Sin(angle * Mathf.Deg2Rad);
            Vector3 Tangent(float angle) => -up * Mathf.Sin(angle * Mathf.Deg2Rad) + front * Mathf.Cos(angle * Mathf.Deg2Rad);

            const float sealAngle = 52f;
            Vector3 normal = Around(sealAngle);
            Vector3 center = origin + axis * 0.03f + normal * (DomeAt(0.03f) + 0.006f);
            WaxSeal(center, normal, -axis, PauldronSkin(name, 0.03f));

            float[] angles = { 45f, 59f };
            for (int k = 0; k < angles.Length; k++)
            {
                float angle = angles[k];
                Strip(t =>
                    {
                        float d = Mathf.Lerp(0.03f, 0.17f, t);
                        float a = angle + 3f * Mathf.Sin(t * 2.6f + k);
                        return origin + axis * d + Around(a) * Mathf.Lerp(DomeAt(0.03f) + 0.003f, 0.103f, Smooth(0f, 1f, t));
                    },
                    t => Tangent(angle), t => PauldronSkin(name, Mathf.Lerp(0.03f, 0.17f, t)), origin + axis * 0.1f, 0.019f, 0.08f * k);
            }
        }

        // 2. Arriba del pecho derecho: las tiras bajan por encima de la copa
        {
            const float sealDeg = 34f;
            Vector3 center = CuirassPoint(1.428f, sealDeg, 0.006f);
            Vector3 normal = (CuirassPoint(1.428f, sealDeg, 0.02f) - CuirassPoint(1.428f, sealDeg, 0f)).normalized;
            WaxSeal(center, normal, Vector3.up, Chest);

            float[] degrees = { 30f, 39f };
            for (int k = 0; k < degrees.Length; k++)
            {
                float deg = degrees[k];
                Strip(t => CuirassPoint(Mathf.Lerp(1.418f, 1.285f, t), deg + 1.5f * Mathf.Sin(t * 3f + k), 0.004f + 0.003f * Mathf.Sin(t * Mathf.PI)),
                    t =>
                    {
                        float y = Mathf.Lerp(1.418f, 1.285f, t);
                        return (CuirassPoint(y, deg + 2f, 0.004f) - CuirassPoint(y, deg - 2f, 0.004f)).normalized;
                    },
                    t => Chest, new Vector3(0f, 1.35f, 0.03f), 0.018f, 0.12f + 0.07f * k);
            }
        }

        // 3. La escarcela derecha: las tiras bajan por las láminas del muslo
        {
            const float side = 1f;
            string name = SideName(side);
            Vector3[] chain = LegChain(name);
            Vector3 At(float d, float angle, float lift)
            {
                TassetFrame(chain, side, d, out Vector3 c, out _, out Vector3 front, out Vector3 outer);
                float a = angle * Mathf.Deg2Rad;
                return c + (front * Mathf.Cos(a) + outer * Mathf.Sin(a)) * (TassetRadius(d, 1f) + lift);
            }

            const float sealAngle = 34f;
            Vector3 center = At(-0.035f, sealAngle, 0.004f);
            Vector3 normal = (At(-0.035f, sealAngle, 0.02f) - center).normalized;
            WaxSeal(center, normal, (At(-0.06f, sealAngle, 0.004f) - center).normalized, TassetSkin(name, -0.035f));

            float[] angles = { 27f, 38f, 49f };
            for (int k = 0; k < angles.Length; k++)
            {
                float angle = angles[k];
                Strip(t => At(Mathf.Lerp(-0.03f, 0.13f + 0.02f * k, t), angle + 2f * Mathf.Sin(t * 2.4f + k), 0.006f),
                    t =>
                    {
                        float d = Mathf.Lerp(-0.03f, 0.13f + 0.02f * k, t);
                        return (At(d, angle + 4f, 0.006f) - At(d, angle - 4f, 0.006f)).normalized;
                    },
                    t => TassetSkin(name, Mathf.Lerp(-0.03f, 0.13f, t)), At(0.05f, angle, -0.1f), 0.018f, 0.05f * k);
            }
        }
    }

    // La cera: un disco rojo abombado. up apunta a donde NO cuelgan las tiras.
    private void WaxSeal(Vector3 center, Vector3 normal, Vector3 up, ModelSkin skin)
    {
        Kit.BeginPiece(30f);
        Kit.Disc(center + normal * 0.002f, normal, up, 0.02f, 0.019f, 0.007f, 10, _wax, skin);
    }

    // Una tira de pergamino: path(t) va de 0 (debajo de la cera) a 1 (la punta, cortada en V).
    private void Strip(Func<float, Vector3> path, Func<float, Vector3> across, Func<float, ModelSkin> skin, Vector3 inside, float width, float text)
    {
        Kit.BeginPiece(35f);
        const int rows = 6;
        var rings = new ModelPoint[rows + 1][];
        for (int r = 0; r <= rows; r++)
        {
            float t = r / (float)rows;
            Vector3 p = path(t);
            Vector3 w = across(t) * (width * 0.5f);
            float v = Mathf.Lerp(0.98f, 0.32f, t) - text;
            Vector3 notch = r == rows ? path(0.93f) - p : Vector3.zero;
            ModelSkin k = skin(t);
            rings[r] = new[]
            {
                new ModelPoint(p - w, _parchment.Uv(0.05f, v), k),
                new ModelPoint(p + notch, _parchment.Uv(0.5f, v), k),
                new ModelPoint(p + w, _parchment.Uv(0.95f, v), k),
            };
        }
        Kit.Sheet(rings, _parchment.Uv(0.5f, 0.02f), inside);
    }

    // Una aureola de rayos que flota detrás de la cabeza.
    private void BuildHalo()
    {
        ModelSkin skin = HeadBone.To(Neck, 0.4f);
        const float radius = 0.13f;
        const int steps = 48;
        var ring = new Vector3[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            float a = i / (float)steps * Mathf.PI * 2f;
            ring[i] = HaloCenter + new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0f) * radius;
        }
        Kit.BeginPiece(60f);
        Kit.Sweep(ring, p => (p - HaloCenter).normalized, t => new Vector2(0.0045f, 0.0085f), 6,
            (t, a) => _halo.Uv(t, 0.2f + 0.6f * Mathf.Abs(a * 2f - 1f)), (p, t) => skin, capStart: false, capEnd: false);

        // Los rayos: largos y cortos, alternados
        Kit.BeginPiece(30f);
        const int spikes = 12;
        for (int i = 0; i < spikes; i++)
        {
            float a = i / (float)spikes * Mathf.PI * 2f;
            var direction = new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0f);
            bool big = i % 2 == 0;
            float length = big ? 0.06f : 0.034f;
            Vector3 root = HaloCenter + direction * (radius + 0.004f);
            Kit.Gem(root + direction * (length * 0.5f), direction, Vector3.back, length * 0.5f, big ? 0.011f : 0.008f, 0.004f, 4, _halo, skin);
        }
    }

    // =========================================================
    // PELO
    // =========================================================

    private void BuildHair()
    {
        BuildHairCap();
        BuildBackHair();
        BuildSideHair();
        BuildFringe();
    }

    private void BuildHairCap()
    {
        Kit.BeginPiece(34f);
        const int columns = 30, rows = 7;
        var rings = new ModelPoint[rows + 1][];
        for (int r = 0; r <= rows; r++)
        {
            float t = r / (float)rows;
            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float u = i / (float)columns;
                float deg = (u - 0.5f) * 360f;
                float h = Mathf.Lerp(Hairline.At(Mathf.Abs(deg)), 1f, t);
                float swell = 0.002f + 0.018f * Smooth(0f, 0.7f, t) + 0.012f * Mathf.PerlinNoise(u * 13f, t * 3.1f) * Smooth(0.1f, 0.6f, t);
                Vector3 p = HeadPoint(h, deg, swell);
                float strip = 0.12f + 0.76f * Mathf.Repeat(u * 5f, 1f);
                rings[r][i] = new ModelPoint(p, _hair.Uv(strip, Mathf.Lerp(0.45f, 0.25f, t)), HeadBone);
            }
        }
        Kit.Loft(rings);
    }

    private void BuildBackHair()
    {
        // Debajo: los largos, hasta la mitad de la espalda, por encima de la capa
        const int deep = 19;
        for (int i = 0; i < deep; i++)
        {
            float s = i / (float)(deep - 1) * 2f - 1f;
            float deg = 180f + s * 92f;
            Vector3 root = HeadPoint(Range(0.48f, 0.76f), deg, 0.004f);
            Vector3 out1 = Outward(root);
            var tip = new Vector3(s * 0.23f + Range(-0.02f, 0.02f), Range(1.02f, 1.12f) + Mathf.Abs(s) * 0.07f,
                                  -0.19f + s * s * 0.04f + Range(-0.012f, 0.012f));
            Lock(root, root + out1 * 0.075f + Vector3.down * 0.05f,
                new Vector3(s * 0.22f, 1.44f, -0.215f + s * s * 0.04f), tip, Range(0.055f, 0.07f), Range(0.02f, 0.026f), 12, 5, false, 2.2f);
        }

        // Encima: más cortos, para que se vean las capas y el volumen
        const int top = 13;
        for (int i = 0; i < top; i++)
        {
            float s = (i + 0.5f) / top * 2f - 1f;
            float deg = 180f + s * 88f;
            Vector3 root = HeadPoint(Range(0.68f, 0.9f), deg, 0.01f);
            Vector3 out1 = Outward(root);
            var tip = new Vector3(s * 0.21f + Range(-0.02f, 0.02f), Range(1.2f, 1.32f), -0.215f + s * s * 0.05f);
            Lock(root, root + out1 * 0.08f + Vector3.down * 0.03f,
                new Vector3(s * 0.21f, 1.49f, -0.23f + s * s * 0.05f), tip, Range(0.05f, 0.064f), Range(0.018f, 0.022f), 10, 5, false, 2f);
        }
    }

    private void BuildSideHair()
    {
        foreach (float side in new[] { 1f, -1f })
        {
            // Adelante del hombro, sobre el peto: por arriba de la gola y por dentro de la hombrera
            for (int i = 0; i < 4; i++)
            {
                float deg = side * Mathf.Lerp(62f, 100f, i / 3f);
                Vector3 root = HeadPoint(Range(0.58f, 0.72f), deg, 0.006f);
                var tip = new Vector3(side * (0.118f + 0.014f * i), Range(1.29f, 1.36f), 0.222f - 0.042f * i);
                Lock(root, root + Outward(root) * 0.035f + Vector3.down * 0.05f,
                    new Vector3(side * (0.112f + 0.01f * i), 1.535f, 0.088f - 0.032f * i), tip,
                    Range(0.032f, 0.04f), Range(0.013f, 0.016f), 10, 5, false, 1.5f);
            }

            // Detrás del hombro: llenan el hueco entre lo de adelante y lo de atrás
            for (int i = 0; i < 3; i++)
            {
                float deg = side * Mathf.Lerp(112f, 140f, i / 2f);
                Vector3 root = HeadPoint(Range(0.5f, 0.66f), deg, 0.004f);
                var tip = new Vector3(side * (0.15f + 0.02f * i), Range(1.12f, 1.22f), -0.165f - 0.025f * i);
                Lock(root, root + Outward(root) * 0.05f + Vector3.down * 0.04f,
                    new Vector3(side * (0.14f + 0.01f * i), 1.5f, -0.15f - 0.025f * i), tip,
                    Range(0.045f, 0.055f), Range(0.016f, 0.02f), 10, 5, false, 1.8f);
            }
        }
    }

    private void BuildFringe()
    {
        // La raya al medio: de ahí salen los mechones hacia cada lado, enmarcando la cara
        for (int i = 0; i < 10; i++)
        {
            float side = i < 5 ? 1f : -1f;
            float s = (i % 5) / 4f;
            float startDeg = side * Mathf.Lerp(3f, 24f, s);
            float startH = Mathf.Lerp(0.96f, 0.87f, s);
            Vector3 root = HeadPoint(startH, startDeg, 0.012f);
            float endDeg = side * Mathf.Lerp(54f, 84f, s);
            float endH = Mathf.Lerp(0.5f, 0.36f, s) + Range(-0.03f, 0.03f);
            Vector3 middle = HeadPoint(0.82f, Mathf.Lerp(startDeg, endDeg, 0.5f), 0.026f);
            Vector3 tip = HeadPoint(endH, endDeg, 0.014f);
            Lock(root, middle, HeadPoint(Mathf.Lerp(0.8f, endH, 0.5f), endDeg, 0.02f), tip,
                Range(0.026f, 0.032f), 0.012f, 8, 5, false, 1.2f);
        }
    }

    // =========================================================
    // TEXTURA
    // =========================================================

    private void PaintAtlas()
    {
        Atlas.Paint(_head, PaintHead);
        Atlas.Paint(_torso, PaintTorso);
        Atlas.Paint(_arm, PaintArm);
        Atlas.Paint(_leg, PaintLeg);
        Atlas.Paint(_hair, PaintHair);
        Atlas.Paint(_cape, PaintCape);
        Atlas.Paint(_tabard, PaintTabard);
        Atlas.Paint(_plate, PaintPlate);
        Atlas.Paint(_dome, PaintDome);
        Atlas.Paint(_sabaton, PaintSabaton);
        Atlas.Paint(_glove, PaintGlove);
        Atlas.Paint(_parchment, PaintParchment);
        Atlas.Paint(_medal, PaintMedal);
        Atlas.Paint(_belt, PaintBelt);
        Atlas.Paint(_kneeCop, PaintKneeCop);
        Atlas.Paint(_bead, (u, v) => Color.Lerp(Wood * 0.7f, Wood * 1.3f, Smooth(0.15f, 0.85f, Mathf.Abs(u - 0.5f) * 2f)));
        Atlas.Paint(_gold, (u, v) => GoldAt(v));
        Atlas.Paint(_wax, (u, v) => Color.Lerp(WaxDark, Wax * 1.1f, v));
        Atlas.Paint(_lining, (u, v) => u < 0.5f ? WhiteShade * 0.55f : CrimsonDark);
        Atlas.Paint(_boss, PaintBoss);
        Atlas.Paint(_halo, (u, v) => GoldAt(0.35f + 0.6f * v));

        // El rasgo aasimar: los ojos brillan con luz propia
        Atlas.PaintGlow(_head, PaintHeadGlow);
        Atlas.GlowIntensity = EyeGlow;
    }

    // ---------- Pinceles de la receta ----------

    private static Color WhiteAt(float u, float v, float sheen)
    {
        Color c = Color.Lerp(WhiteShade, White, Mathf.Clamp01(0.25f + 0.75f * sheen));
        c = Over(c, WhiteLight, 0.6f * Smooth(0.75f, 1f, sheen));
        c *= 1f + (Mathf.PerlinNoise(u * 9f + 1f, v * 9f + 2f) - 0.5f) * 0.06f;

        // Desgaste: puntitos sueltos donde el esmalte se saltó
        float chip = Mathf.PerlinNoise(u * 120f + 7f, v * 120f + 3f);
        return Over(c, WhiteShade * 0.8f, 0.35f * Smooth(0.78f, 0.84f, chip));
    }

    private static Color GoldAt(float sheen)
    {
        Color c = Color.Lerp(GoldDark, Gold, Mathf.Clamp01(0.35f + 0.65f * sheen));
        return Over(c, GoldLight, 0.55f * Smooth(0.75f, 1f, sheen));
    }

    // El traje acolchado de abajo: negro, con costuras en rombos.
    private static Color Quilted(float u, float v)
    {
        Color c = Grain(Under, u, v, 60f, 0.3f);
        float a = Mathf.Repeat(u * 22f + v * 14f, 1f), b = Mathf.Repeat(u * 22f - v * 14f, 1f);
        float line = Mathf.Min(Mathf.Min(a, 1f - a), Mathf.Min(b, 1f - b));
        return Over(c, Under * 0.5f, 0.6f * Smooth(0.06f, 0f, line));
    }

    // Una flor de lis en coordenadas propias: x de -1 a 1, y de -1 (abajo) a 1 (arriba). Da cuánto la cubre.
    private static float FleurDeLis(float x, float y)
    {
        const float aa = 0.05f;
        float ax = Mathf.Abs(x);

        // El pétalo del centro, en punta arriba
        float w = 0.23f * Mathf.Pow(Mathf.Clamp01(Mathf.Sin(Mathf.PI * Mathf.Clamp01((y + 0.12f) / 1.12f))), 0.75f);
        float center = Smooth(w + aa, w - aa, ax) * Smooth(-0.14f, -0.1f, y) * Smooth(1.02f, 0.98f, y);

        // La banda que los ata
        float band = Smooth(0.48f + aa, 0.48f - aa, ax) * Smooth(-0.31f - aa, -0.31f + aa, y) * Smooth(-0.11f + aa, -0.11f - aa, y);

        // Los de los costados: un arco que sale de la banda, sube y cae hacia afuera
        float px = ax - 0.58f, py = y - 0.18f;
        float angle = Mathf.Atan2(py, px) * Mathf.Rad2Deg;
        if (angle < -150f) angle += 360f;
        float s = (228f - angle) / 248f;
        float halfWidth = Mathf.Lerp(0.10f, 0.02f, Mathf.Clamp01(s));
        float ring = Mathf.Abs(Mathf.Sqrt(px * px + py * py) - 0.32f);
        float sidePetal = s >= 0f && s <= 1f ? Smooth(halfWidth + aa, halfWidth - aa, ring) : 0f;

        // Abajo: un pétalo chico en punta y dos gotas hacia afuera
        float stemWidth = 0.10f * Mathf.Clamp01((y + 0.66f) / 0.36f);
        float stem = Smooth(stemWidth + aa, stemWidth - aa, ax) * Smooth(-0.29f, -0.33f, y) * Smooth(-0.7f, -0.64f, y);
        float dx = ax - 0.25f, dy = y + 0.44f;
        float rx = dx * 0.82f + dy * 0.57f, ry = -dx * 0.57f + dy * 0.82f;
        float drop = Smooth(1f + aa * 4f, 1f - aa * 4f, Sq(rx / 0.14f) + Sq(ry / 0.075f));

        return Mathf.Max(Mathf.Max(center, band), Mathf.Max(sidePetal, Mathf.Max(stem, drop)));
    }

    // La forma del ojo (abierto de 0 a 1) y qué tan lejos está del centro del iris (1 = su borde).
    private static float EyeMask(float ax, float y, float shrink, out float radial)
    {
        float ex = (ax - 0.0362f) / 0.0195f;
        float ey = (y - 1.6835f - (ax - 0.0362f) * 0.12f) / 0.0094f;
        radial = Mathf.Sqrt(Sq((ax - 0.0352f) / 0.0105f) + Sq((y - 1.6838f) / 0.0095f));
        float open = 1f - ex * ex;
        if (open <= 0f) return 0f;
        float lid = Mathf.Sqrt(open) * shrink;
        return Smooth(lid * 1.02f, lid * 0.9f, Mathf.Abs(ey));
    }

    private static Color PaintHead(float u, float v)
    {
        float x = Mathf.Lerp(FaceLeft, FaceRight, u);
        float y = Mathf.Lerp(FaceBottom, FaceTop, v);
        float ax = Mathf.Abs(x);

        Color c = Grain(SkinTone, u, v, 24f, 0.05f);
        c = Over(c, new Color(0.86f, 0.60f, 0.54f, 0.22f), 0.3f * Smooth(0.06f, 0.09f, ax));
        c = Over(c, SkinShade, 0.22f * Smooth(1.578f, 1.558f, y));
        c = Over(c, new Color(0.96f, 0.55f, 0.50f, 0.25f), 0.24f * Bell(ax, 0.047f, 0.02f) * Bell(y, 1.648f, 0.016f));

        // La luz de los ojos tiñe de dorado las cuencas
        c = Over(c, new Color(1f, 0.82f, 0.48f, 0.3f), 0.3f * Bell(ax, 0.036f, 0.028f) * Bell(y, 1.686f, 0.016f));
        c = Over(c, SkinShade, 0.16f * Bell(ax, 0.010f, 0.004f) * Bell(y, 1.66f, 0.02f));
        c = Over(c, SkinShade, 0.5f * Bell(x, 0f, 0.0095f) * Bell(y, 1.6325f, 0.003f));
        c = Over(c, SkinShade * 0.85f, 0.45f * Bell(ax, 0.0095f, 0.003f) * Bell(y, 1.6355f, 0.0022f));     // fosas de la nariz
        c = Over(c, new Color(1f, 0.86f, 0.76f, 0.3f), 0.35f * Bell(x, 0f, 0.0045f) * Bell(y, 1.66f, 0.028f));

        // Labios
        float lipY = y - 1.608f - 2.2f * ax * ax;
        const float lipHalf = 0.0205f;
        float cupid = 0.0012f * Bell(ax, 0.005f, 0.004f);
        float upper = Smooth(0f, 0.001f, lipY) * Smooth(0.0074f + cupid - 0.0062f * ax / lipHalf, 0.0064f + cupid - 0.0062f * ax / lipHalf, lipY);
        float lower = Smooth(0f, -0.001f, lipY) * Smooth(-0.0098f + 0.0088f * ax / lipHalf, -0.0088f + 0.0088f * ax / lipHalf, lipY);
        float inside = Smooth(lipHalf, lipHalf - 0.0015f, ax);
        c = Over(c, new Color(0.80f, 0.36f, 0.36f, 0.45f), upper * inside);
        c = Over(c, new Color(0.88f, 0.46f, 0.44f, 0.5f), lower * inside);
        c = Over(c, new Color(0.40f, 0.14f, 0.14f, 0.3f), Smooth(0.0011f, 0.0004f, Mathf.Abs(lipY)) * inside);
        c = Over(c, new Color(1f, 0.70f, 0.66f, 0.6f), 0.5f * Bell(ax, 0.004f, 0.006f) * Bell(lipY, -0.005f, 0.0013f));

        // Los ojos: dorados y sin pupila, con un aro apenas más oscuro donde iría el iris
        float eye = EyeMask(ax, y, 1f, out float radial);
        if (eye > 0f)
        {
            Color glow = Color.Lerp(EyeCore, EyeGold, Smooth(0.15f, 0.9f, radial));
            glow = Over(glow, new Color(0.80f, 0.50f, 0.10f, 0.9f), 0.45f * Smooth(0.8f, 0.96f, radial) * Smooth(1.18f, 1.02f, radial));
            c = Over(c, glow, eye);
        }

        // El párpado de arriba y las pestañas
        float ex = (ax - 0.0362f) / 0.0195f;
        float ey = (y - 1.6835f - (ax - 0.0362f) * 0.12f) / 0.0094f;
        float open = 1f - ex * ex;
        float upperLine = Mathf.Abs(ey - Mathf.Sqrt(Mathf.Max(0f, open)));
        c = Over(c, new Color(0.32f, 0.19f, 0.08f, 0.2f), Smooth(0.44f, 0.18f, upperLine) * Smooth(1.3f, 1.05f, Mathf.Abs(ex)) * Smooth(-0.3f, 0.1f, ey));

        // Cejas doradas
        float bx = ax - 0.0145f;
        float browY = 1.7005f + bx * 0.5f - bx * bx * 5.5f;
        float browThick = Mathf.Lerp(0.0034f, 0.0011f, Smooth(0f, 0.045f, bx));
        float brow = Smooth(browThick + 0.001f, browThick, Mathf.Abs(y - browY)) * Smooth(0f, 0.003f, bx) * Smooth(0.047f, 0.042f, bx);
        return Over(c, new Color(0.68f, 0.45f, 0.16f, 0.3f), brow);
    }

    // La textura de brillo de la cabeza: solo los ojos, más blancos al centro.
    private static Color PaintHeadGlow(float u, float v)
    {
        float x = Mathf.Lerp(FaceLeft, FaceRight, u);
        float y = Mathf.Lerp(FaceBottom, FaceTop, v);
        float eye = EyeMask(Mathf.Abs(x), y, 0.86f, out float radial);
        Color glow = Color.Lerp(new Color(1f, 0.88f, 0.55f), new Color(1f, 0.62f, 0.14f), Smooth(0.1f, 0.95f, radial));
        return new Color(glow.r * eye, glow.g * eye, glow.b * eye, 1f);
    }

    private static Color PaintTorso(float u, float v)
    {
        float deg = (u - 0.5f) * 360f;
        float abs = Mathf.Abs(deg);
        float y = Mathf.Lerp(TorsoBottom, TorsoTop, v);

        // Debajo: el traje negro acolchado; arriba, el cuello
        Color c = Quilted(u * 3f, v);
        c = Over(c, Grain(SkinTone, u * 3f, v, 30f, 0.05f), Smooth(1.495f, 1.505f, y));
        c = Over(c, SkinShade, 0.4f * Smooth(1.585f, 1.55f, y) * Smooth(1.5f, 1.53f, y));

        float top = CuirassTop(deg);
        float inside = Smooth(CuirassBottom - 0.002f, CuirassBottom + 0.002f, y) * Smooth(top + 0.002f, top - 0.002f, y);
        if (inside <= 0f) return c;

        Color armor = y < BreastBottom ? Brigandine(u, v, deg, y) : Breastplate(u, v, deg, abs, y, top);
        return Over(c, armor, inside);
    }

    // La cintura: tela oscura por paños, con remaches de latón en filas.
    private static Color Brigandine(float u, float v, float deg, float y)
    {
        Color c = Grain(new Color(0.17f, 0.16f, 0.19f, 0.25f), u * 3f, v, 70f, 0.25f);
        float seam = Mathf.Abs(Mathf.Repeat(deg + 7.5f, 15f) - 7.5f);
        c = Over(c, Under, 0.6f * Smooth(0.9f, 0.3f, seam));

        float dx = (Mathf.Repeat(deg, 7.5f) - 3.75f) * 0.0024f;
        float dy = Mathf.Repeat(y - 1.04f, 0.026f) - 0.013f;
        float rivet = Smooth(0.0042f, 0.0026f, Mathf.Sqrt(dx * dx + dy * dy));
        return Over(c, GoldAt(0.35f + 0.6f * Smooth(-0.002f, 0.003f, dy)), rivet);
    }

    private static Color Breastplate(float u, float v, float deg, float abs, float y, float top)
    {
        float cup = Bell(y, 1.345f, 0.06f) * Bell(abs, 30f, 20f);
        float sheen = 0.45f + 0.5f * cup + 0.15f * Bell(abs, 3f, 6f) * Smooth(1.21f, 1.3f, y)
                    - 0.3f * Smooth(1.30f, 1.215f, y) * Smooth(10f, 30f, abs) * (1f - cup)
                    - 0.28f * Smooth(65f, 140f, abs);
        Color c = WhiteAt(u * 3f, v, Mathf.Clamp01(sheen));

        // Filigrana de oro bajo cada copa
        float arc = 1.292f + 0.00011f * Sq(abs - 30f);
        float filigree = Smooth(0.0032f, 0.0016f, Mathf.Abs(y - arc)) * Smooth(6f, 10f, abs) * Smooth(60f, 54f, abs);
        c = Over(c, Gold, filigree);

        // La flor de lis del esternón
        float fleur = FleurDeLis(deg / 13f, (y - 1.335f) / 0.044f);
        c = Over(c, GoldAt(0.5f + 0.5f * Mathf.Clamp01((y - 1.30f) / 0.08f)), fleur);

        // Filos de oro arriba y abajo
        float edge = Mathf.Min(y - BreastBottom, top - y);
        c = Over(c, GoldDark, Smooth(0.0145f, 0.0125f, edge));
        c = Over(c, Gold, Smooth(0.0118f, 0.0098f, edge));
        return Over(c, GoldLight, 0.5f * Smooth(0.006f, 0.004f, Mathf.Abs(edge - 0.008f)));
    }

    private static Color PaintArm(float u, float v)
    {
        float d = Mathf.Lerp(-0.05f, 0.446f, v);
        float deg = (u - 0.5f) * 360f;
        float abs = Mathf.Abs(deg);

        Color c = Quilted(u * 2f, v * 2f);
        float sheen = 0.5f + 0.4f * Bell(abs, 20f, 30f) - 0.3f * Smooth(90f, 170f, abs);

        // La codera
        float couter = Smooth(CouterStart - 0.002f, CouterStart + 0.002f, d) * Smooth(VambraceStart + 0.002f, VambraceStart - 0.002f, d);
        if (couter > 0f)
        {
            Color plate = WhiteAt(u * 2f, v, Mathf.Clamp01(sheen - 0.08f));
            plate = Over(plate, WhiteShade, 0.7f * Smooth(0.006f, 0.002f, Mathf.Abs(d - 0.234f)));
            plate = Over(plate, Gold, Smooth(0.006f, 0.004f, d - CouterStart));
            c = Over(c, plate, couter);
        }

        // El brazal, con una crucecita de oro arriba
        float vambrace = Smooth(VambraceStart - 0.002f, VambraceStart + 0.002f, d) * Smooth(CuffStart + 0.002f, CuffStart - 0.002f, d);
        if (vambrace > 0f)
        {
            Color plate = WhiteAt(u * 2f, v, Mathf.Clamp01(sheen));
            plate = Over(plate, WhiteLight, 0.4f * Bell(deg, 2f, 3f));
            float edge = Mathf.Min(d - VambraceStart, CuffStart - d);
            plate = Over(plate, Gold, Smooth(0.0065f, 0.0045f, edge));
            float cross = Mathf.Max(Smooth(4f, 3f, abs) * Smooth(0.285f, 0.29f, d) * Smooth(0.335f, 0.33f, d),
                                    Smooth(11f, 10f, abs) * Smooth(0.0035f, 0.0025f, Mathf.Abs(d - 0.301f)));
            plate = Over(plate, GoldAt(0.7f), cross);
            c = Over(c, plate, vambrace);
        }

        // El puño del guantelete
        float cuff = Smooth(CuffStart - 0.002f, CuffStart + 0.002f, d) * Smooth(GloveStart + 0.002f, GloveStart - 0.002f, d);
        if (cuff > 0f)
        {
            Color plate = WhiteAt(u * 2f, v, Mathf.Clamp01(sheen - 0.1f + 0.2f * Smooth(CuffStart, 0.42f, d)));
            plate = Over(plate, Gold, Smooth(0.0105f, 0.0085f, d - CuffStart));
            plate = Over(plate, GoldDark, Smooth(0.0025f, 0.001f, d - CuffStart));
            c = Over(c, plate, cuff);
        }

        // El guante
        return Over(c, Grain(GloveTone, u * 2f, v, 50f, 0.35f), Smooth(GloveStart - 0.002f, GloveStart + 0.002f, d));
    }

    private static Color PaintLeg(float u, float v)
    {
        float d = Mathf.Lerp(-0.07f, 0.82f, v);
        float deg = (u - 0.5f) * 360f;
        float abs = Mathf.Abs(deg);

        Color c = Quilted(u * 2f, v * 2f);
        float sheen = 0.46f + 0.42f * Bell(abs, 22f, 26f) - 0.3f * Smooth(70f, 150f, abs);

        // El quijote del muslo: solo adelante
        float cuisse = Smooth(CuisseStart - 0.002f, CuisseStart + 0.002f, d) * Smooth(PoleynStart + 0.002f, PoleynStart - 0.002f, d) * Smooth(102f, 94f, abs);
        if (cuisse > 0f)
        {
            Color plate = WhiteAt(u * 2f, v, Mathf.Clamp01(sheen - 0.05f));
            float edge = Mathf.Min(d - CuisseStart, (98f - abs) * 0.0026f);
            plate = Over(plate, Gold, Smooth(0.0085f, 0.0065f, edge));
            plate = Over(plate, WhiteShade, Smooth(0.016f, 0.014f, edge) * Smooth(0.0115f, 0.0135f, edge));
            c = Over(c, plate, cuisse);
        }

        // La rodilla (la rodillera con su flor de lis la tapa por delante)
        float poleyn = Smooth(PoleynStart - 0.002f, PoleynStart + 0.002f, d) * Smooth(GreaveStart + 0.002f, GreaveStart - 0.002f, d);
        if (poleyn > 0f)
        {
            Color plate = WhiteAt(u * 2f, v, Mathf.Clamp01(sheen - 0.12f));
            plate = Over(plate, WhiteShade, Smooth(0.0045f, 0.002f, Mathf.Abs(d - 0.388f)));
            plate = Over(plate, WhiteShade, Smooth(0.0045f, 0.002f, Mathf.Abs(d - 0.438f)));
            plate = Over(plate, Under, Smooth(120f, 135f, abs));
            c = Over(c, plate, poleyn);
        }

        // La greba: blanca adelante, cerrada con correas atrás
        float greave = Smooth(GreaveStart - 0.002f, GreaveStart + 0.002f, d);
        if (greave > 0f)
        {
            Color plate = WhiteAt(u * 2f, v, Mathf.Clamp01(sheen));
            plate = Over(plate, WhiteLight, 0.45f * Bell(deg, 3f, 4f));
            plate = Over(plate, WhiteShade, 0.5f * Bell(deg, -4f, 4f));
            plate = Over(plate, Gold, Smooth(0.0115f, 0.0095f, d - GreaveStart));
            plate = Over(plate, Gold, Smooth(0.0035f, 0.0015f, Mathf.Abs(d - 0.752f)));

            plate = Over(plate, WhiteShade, 0.6f * Smooth(3f, 1f, 180f - abs));
            float strap = Mathf.Min(Mathf.Abs(d - 0.56f), Mathf.Abs(d - 0.69f));
            plate = Over(plate, Leather, Smooth(0.008f, 0.006f, strap) * Smooth(140f, 150f, abs));
            c = Over(c, plate, greave);
        }
        return c;
    }

    private static Color PaintHair(float u, float v)
    {
        Color c = v < 0.5f
            ? Color.Lerp(HairRoot, HairMid, Smooth(0f, 0.45f, v))
            : Color.Lerp(HairMid, HairTip, Smooth(0.5f, 1f, v));

        float strands = Mathf.PerlinNoise(u * 34f, v * 2.4f);
        float fine = Mathf.PerlinNoise(u * 90f + 9f, v * 3.5f);
        c *= 0.78f + 0.36f * strands + 0.18f * (fine - 0.5f);
        return Over(c, new Color(1f, 0.93f, 0.66f, 0.5f), 0.5f * Smooth(0.68f, 0.82f, fine) * Bell(v, 0.5f, 0.3f));
    }

    private static Color PaintCape(float u, float v)
    {
        Color c = Grain(Crimson, u, v, 70f, 0.14f);
        c *= 0.78f + 0.5f * Mathf.PerlinNoise(u * 14f, v * 1.3f + 4f);
        c = Over(c, CrimsonDark, 0.4f * Smooth(0.25f, 0f, v));

        // Los pliegues, con la misma onda de la malla (BuildCape)
        float s = u * 2f - 1f;
        float depth = Smooth(0.95f, 0.4f, v);
        float folds = Mathf.Cos(s * 9.5f + 0.9f) * 0.8f + Mathf.Cos(s * 19f + 2.1f) * 0.45f * Smooth(0.7f, 0.1f, v);
        c *= 1f + folds * 0.3f * depth;

        // La flor de lis de oro en la espalda, abajo del pelo
        float fleur = FleurDeLis((u - 0.5f) * 0.58f / 0.05f, (v - 0.36f) / 0.062f);
        c = Over(c, GoldAt(0.55f), fleur);

        // La guarda de abajo
        float line = Mathf.Min(Mathf.Abs(v - 0.06f), Mathf.Abs(v - 0.1f));
        return Over(c, Gold, Smooth(0.007f, 0.0045f, line));
    }

    private static Color PaintTabard(float u, float v)
    {
        Color c = Grain(Crimson, u, v, 60f, 0.14f);
        c *= 0.82f + 0.36f * Mathf.PerlinNoise(u * 16f, v * 1.6f);
        c = Over(c, CrimsonDark, 0.5f * Smooth(0.3f, 0f, v));

        // Ribete de oro a los costados y abajo, con una línea más adentro
        float edge = Mathf.Min(Mathf.Min(u, 1f - u) * 0.2f, v * 0.6f);
        c = Over(c, GoldAt(0.6f), Smooth(0.0105f, 0.0085f, edge));
        c = Over(c, Gold, Smooth(0.0026f, 0.0012f, Mathf.Abs(edge - 0.017f)));

        // La flor de lis blanca, bordada en oro
        float fx = (u - 0.5f) * 0.2f / 0.038f, fy = (v - 0.7f) * 0.6f / 0.048f;
        c = Over(c, Gold, FleurDeLis(fx * 0.92f, fy * 0.92f));
        return Over(c, WhiteAt(u, v, 0.7f), FleurDeLis(fx, fy));
    }

    // Láminas de las hombreras, escarcelas y gola: blancas con remaches y el filo de abajo en oro.
    private static Color PaintPlate(float u, float v)
    {
        float arc = Mathf.Abs(u - 0.5f) * 2f;
        float sheen = 0.5f + 0.42f * Bell(arc, 0.28f, 0.26f) * Smooth(0.05f, 0.5f, v) - 0.34f * Smooth(0.6f, 1f, arc);
        Color c = WhiteAt(u, v, Mathf.Clamp01(sheen));
        c = Over(c, WhiteShade, Smooth(0.2f, 0.02f, v) * 0.6f);

        float rx = (Mathf.Repeat(u * 9f, 1f) - 0.5f) / 9f;
        float rivet = Smooth(0.03f, 0.021f, Mathf.Sqrt(rx * rx + Sq(v - 0.7f)));
        c = Over(c, GoldAt(0.6f), rivet);

        c = Over(c, Gold, Smooth(0.8f, 0.83f, v));
        return Over(c, GoldDark, Smooth(0.95f, 0.98f, v));
    }

    // La cúpula de la hombrera, con la flor de lis mirando hacia el cuello (arriba con el brazo abajo).
    private static Color PaintDome(float u, float v)
    {
        float x = (u - 0.5f) * 2f;
        float sheen = 0.46f + 0.42f * Bell(Mathf.Abs(x), 0.3f, 0.3f) - 0.3f * Smooth(0.6f, 1f, Mathf.Abs(x));
        Color c = WhiteAt(u, v, Mathf.Clamp01(sheen));

        float fleur = FleurDeLis((u - 0.5f) * 15f, (0.6f - v) * 4.2f);
        c = Over(c, GoldAt(0.4f + 0.6f * v), fleur);

        c = Over(c, Gold, Smooth(0.9f, 0.93f, v));
        c = Over(c, GoldDark, Smooth(0.975f, 0.99f, v));
        return Over(c, Gold * 0.9f, Smooth(0.07f, 0.04f, v));
    }

    private static Color PaintSabaton(float u, float v)
    {
        float sheen = 0.5f + 0.38f * Bell(Mathf.Abs(u - 0.5f), 0.14f, 0.16f) - 0.32f * Smooth(0.25f, 0.47f, Mathf.Abs(u - 0.5f));
        sheen = Mathf.Lerp(0.4f, sheen, Smooth(0.2f, 0.4f, v));
        Color c = WhiteAt(u, v, Mathf.Clamp01(sheen));

        // Las láminas del pie y la punta de oro
        float lame = Mathf.Repeat(v * 9f, 1f);
        c = Over(c, WhiteShade, Smooth(0.1f, 0.03f, lame) * Smooth(0.5f, 0.55f, v) * Smooth(0.9f, 0.85f, v));
        c = Over(c, Gold, Smooth(0.86f, 0.885f, v) * Smooth(0.925f, 0.905f, v));
        return Over(c, new Color(0.08f, 0.06f, 0.05f, 0.1f), Smooth(0.1f, 0.07f, v));
    }

    private static Color PaintGlove(float u, float v)
    {
        Color c = Grain(GloveTone, u, v, 50f, 0.35f);
        float knuckle = Mathf.Repeat(v * 3.4f, 1f);
        return Over(c, GloveTone * 0.55f, 0.5f * Smooth(0.92f, 1f, knuckle));
    }

    private static Color PaintParchment(float u, float v)
    {
        Color c = Grain(Parchment, u, v, 40f, 0.12f);
        c = Over(c, Parchment * 0.75f, 0.5f * Smooth(0.1f, 0f, Mathf.Min(u, 1f - u)));

        // Renglones de letra: rayitas cortas
        float row = Mathf.Repeat(v * 26f, 1f);
        float word = Mathf.PerlinNoise(u * 18f, Mathf.Floor(v * 26f) * 3.7f);
        float text = Smooth(0.35f, 0.25f, Mathf.Abs(row - 0.5f)) * Smooth(0.42f, 0.48f, word)
                   * Smooth(0.08f, 0.14f, u) * Smooth(0.92f, 0.86f, u) * Smooth(0.05f, 0.08f, v);
        return Over(c, Ink, 0.75f * text);
    }

    // La medalla de la gola: oro, con la flor de lis en blanco.
    private static Color PaintMedal(float u, float v)
    {
        float x = (u - 0.5f) * 2f, y = (v - 0.5f) * 2f;
        float r = Mathf.Sqrt(x * x + y * y);
        Color c = GoldAt(0.55f + 0.35f * y);
        c = Over(c, GoldDark, Smooth(0.84f, 0.88f, r) * Smooth(0.97f, 0.93f, r));
        c = Over(c, GoldLight, 0.4f * Smooth(0.93f, 0.97f, r));
        return Over(c, WhiteAt(u, v, 0.75f), FleurDeLis(x * 1.45f, y * 1.45f));
    }

    // La rodillera: blanca, con la flor de lis y el borde en oro.
    private static Color PaintKneeCop(float u, float v)
    {
        float x = (u - 0.5f) * 2f, y = (v - 0.5f) * 2f;
        float r = Mathf.Sqrt(x * x + y * y);
        Color c = WhiteAt(u, v, 0.5f + 0.4f * y);
        c = Over(c, GoldAt(0.6f), Smooth(0.84f, 0.88f, r));
        c = Over(c, GoldDark, Smooth(0.97f, 1f, r));
        return Over(c, GoldAt(0.5f + 0.4f * y), FleurDeLis(x * 1.5f, y * 1.5f));
    }

    // La cubierta del codo: Kit.Disc pinta por anillos (v).
    private static Color PaintBoss(float u, float v)
    {
        Color c = WhiteAt(u, v, v);
        return Over(c, Gold, Smooth(0.78f, 0.8f, v) * Smooth(0.88f, 0.86f, v));
    }

    private static Color PaintBelt(float u, float v)
    {
        Color c = Grain(Leather, u * 6f, v, 40f, 0.45f);
        float edge = Mathf.Abs(v - 0.5f) * 2f;
        c = Over(c, LeatherDark, Smooth(0.75f, 1f, edge));
        float stitch = Smooth(0.08f, 0.04f, Mathf.Abs(edge - 0.68f)) * Smooth(0.28f, 0.2f, Mathf.Abs(Mathf.Repeat(u * 150f, 1f) - 0.5f));
        return Over(c, new Color(0.78f, 0.58f, 0.32f, 0.3f), stitch);
    }
}
