using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ============================================================
// WizardModel
//
// La receta del modelo de relleno de la Maga (referencias elegidas por Gustavo el 9 de noviembre de 2026): una
// humana de piel morena y pelo largo y rizado, con lentes redondos, un sombrero de bruja enorme y ropa mística con
// un toque de bibliotecaria. Abrigo índigo largo y abierto adelante, con mangas de campana y bordados de oro
// (estrellas, lunas y un círculo mágico en los faldones de atrás); suéter de cuello de tortuga verde azulado,
// corsé de cuero, falda, medias y botas altas; un libro colgado de la cadera, frascos y bolsas en el cinturón y
// una pluma de oro al cuello. El sombrero lleva una cinta ciruela, una luna de oro y una pluma. Es un personaje
// propio: toma el tipo de las referencias, no copia a ninguno.
//
// El pelo son rizos de verdad: tubos que dan vueltas como resortes (Ringlet), encima de una cortina con bultos
// que tapa los huecos (BuildCurlVolume).
//
// Acabado Polished: el oro brilla y la tela no (el alfa de cada color es el pulido). El Mago no se hace
// invisible (ver CLAUDE.md).
//
// Mercenarios ▸ Modelos de clase ▸ Generar la Maga: rehace malla, textura y prefab.
// ============================================================
public sealed class WizardModel : ClassModelRecipe
{
    private const string Name = "Wizard";
    private const string ArtFolder = "Assets/Art/Models/Classes/Wizard";
    private const string PrefabFolder = "Assets/Prefabs/Player/Classes";

    // ---------- El sombrero: si en juego tapa la vista, se achica acá ----------
    private const float BrimRadius = 0.26f;   // radio del ala (m)
    private const float HatHeight  = 0.40f;   // alto del cono (m)
    private const float HatTilt    = 9f;      // cuánto se inclina hacia atrás (°): deja ver la cara

    // ---------- Colores (el alfa es el pulido) ----------
    private static readonly Color SkinTone     = new Color(0.460f, 0.280f, 0.180f, 0.30f);
    private static readonly Color SkinShade    = new Color(0.270f, 0.150f, 0.095f, 0.22f);
    private static readonly Color SkinLight    = new Color(0.640f, 0.420f, 0.290f, 0.40f);
    private static readonly Color Lip          = new Color(0.480f, 0.170f, 0.190f, 0.45f);
    private static readonly Color HairRoot     = new Color(0.030f, 0.022f, 0.020f, 0.25f);
    private static readonly Color HairMid      = new Color(0.085f, 0.055f, 0.042f, 0.35f);
    private static readonly Color HairTip      = new Color(0.230f, 0.140f, 0.080f, 0.40f);
    private static readonly Color Coat         = new Color(0.120f, 0.130f, 0.420f, 0.12f);
    private static readonly Color CoatDark     = new Color(0.045f, 0.045f, 0.170f, 0.08f);
    private static readonly Color HatTone      = new Color(0.200f, 0.110f, 0.380f, 0.10f);
    private static readonly Color HatDark      = new Color(0.075f, 0.040f, 0.150f, 0.06f);
    private static readonly Color Plum         = new Color(0.440f, 0.080f, 0.270f, 0.15f);
    private static readonly Color Sweater      = new Color(0.040f, 0.380f, 0.330f, 0.05f);
    private static readonly Color SweaterDark  = new Color(0.015f, 0.180f, 0.165f, 0.04f);
    private static readonly Color Skirt        = new Color(0.030f, 0.230f, 0.220f, 0.06f);
    private static readonly Color Tights       = new Color(0.100f, 0.075f, 0.120f, 0.20f);
    private static readonly Color Leather      = new Color(0.380f, 0.210f, 0.100f, 0.30f);
    private static readonly Color LeatherDark  = new Color(0.160f, 0.090f, 0.050f, 0.25f);
    private static readonly Color LeatherLight = new Color(0.560f, 0.340f, 0.160f, 0.35f);
    private static readonly Color Gold         = new Color(0.920f, 0.660f, 0.180f, 0.88f);
    private static readonly Color GoldDark     = new Color(0.500f, 0.280f, 0.060f, 0.70f);
    private static readonly Color GoldLight    = new Color(1.000f, 0.860f, 0.450f, 0.92f);
    private static readonly Color FeatherTone  = new Color(0.040f, 0.560f, 0.560f, 0.20f);
    private static readonly Color BookRed      = new Color(0.430f, 0.060f, 0.080f, 0.30f);
    private static readonly Color Pages        = new Color(0.920f, 0.860f, 0.700f, 0.05f);
    private static readonly Color Potion       = new Color(0.300f, 0.850f, 0.450f, 0.85f);
    private static readonly Color PotionPurple = new Color(0.620f, 0.220f, 0.850f, 0.85f);

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

    // ---------- Brazo y pierna: distancia desde el nacimiento → radio ----------
    private static readonly ModelKeys ArmRadius = new ModelKeys(
        -0.05f, 0.040f, 0f, 0.055f, 0.06f, 0.058f, 0.12f, 0.050f, 0.18f, 0.043f, 0.234f, 0.038f, 0.27f, 0.042f,
        0.32f, 0.040f, 0.38f, 0.032f, 0.43f, 0.026f, 0.445f, 0.025f);

    private static readonly ModelKeys LegRadius = new ModelKeys(
        -0.07f, 0.082f, 0f, 0.090f, 0.08f, 0.092f, 0.16f, 0.085f, 0.26f, 0.072f, 0.35f, 0.060f, 0.41f, 0.056f,
        0.46f, 0.055f, 0.53f, 0.060f, 0.62f, 0.053f, 0.72f, 0.043f, 0.79f, 0.040f, 0.82f, 0.041f);

    private const float SleeveEnd = 0.395f;               // la manga de campana llega a la muñeca
    private const float BootTop = 0.432f, BootCuff = 0.478f;

    // ---------- Cabeza: alto de 0 (mentón) a 1 (coronilla) ----------
    private const float HeadBottom = 1.555f;
    private const float HeadHeight = 0.255f;

    private static readonly ModelKeys HeadWidth = new ModelKeys(
        0f, 0.022f, 0.08f, 0.041f, 0.2f, 0.057f, 0.35f, 0.069f, 0.5f, 0.077f, 0.7f, 0.081f, 0.85f, 0.068f,
        0.95f, 0.042f, 1f, 0.005f);

    private static readonly ModelKeys HeadDepth = new ModelKeys(
        0f, 0.018f, 0.1f, 0.040f, 0.22f, 0.060f, 0.35f, 0.082f, 0.5f, 0.098f, 0.7f, 0.104f, 0.85f, 0.090f,
        0.95f, 0.055f, 1f, 0.006f);

    private static readonly ModelKeys HeadOffset = new ModelKeys(
        0f, 0.058f, 0.1f, 0.046f, 0.22f, 0.032f, 0.35f, 0.022f, 0.5f, 0.012f, 0.7f, 0.005f, 0.85f, 0f, 1f, 0f);

    private static readonly ModelKeys Hairline = new ModelKeys(
        0f, 0.80f, 20f, 0.78f, 40f, 0.72f, 60f, 0.64f, 90f, 0.44f, 120f, 0.27f, 180f, 0.14f);

    private const float FaceLeft = -0.11f, FaceRight = 0.11f, FaceBottom = 1.54f, FaceTop = 1.83f;

    // ---------- Abrigo, faldas y sombrero ----------
    private const float LapelDeg = 7f;
    private const float CoatFlare = 0.40f, ApronFlare = 0.34f;
    private const float VolumeTop = 1.768f;   // dónde empieza la cortina de rizos, bajo el sombrero
    private static readonly Vector3 HatBase = new Vector3(0f, 1.774f, 0.014f);

    private readonly ModelRegion _head, _torso, _arm, _leg, _hair, _hatCone, _brim, _coat, _sleeve, _apron, _boot,
                                 _belt, _glove, _gold, _lining, _band, _feather, _book, _trim, _skin, _glass;

    protected override ModelRegion HairRegion => _hair;

    [MenuItem("Mercenarios/Modelos de clase/Generar la Maga")]
    public static void Generate()
    {
        var model = new WizardModel();
        model.Build();
    }

    private WizardModel() : base(6127)
    {
        _head    = Atlas.Region(0, 1024, 1024, 1024);
        _torso   = Atlas.Region(1024, 1024, 1024, 1024);
        _arm     = Atlas.Region(0, 0, 512, 1024);
        _leg     = Atlas.Region(512, 0, 512, 1024);
        _hair    = Atlas.Region(1024, 0, 256, 1024);
        _hatCone = Atlas.Region(1280, 512, 256, 512);
        _brim    = Atlas.Region(1536, 512, 256, 512);
        _coat    = Atlas.Region(1792, 512, 256, 512);
        _sleeve  = Atlas.Region(1280, 256, 256, 256);
        _apron   = Atlas.Region(1536, 256, 256, 256);
        _boot    = Atlas.Region(1792, 256, 256, 256);
        _belt    = Atlas.Region(1280, 128, 512, 128);
        _glove   = Atlas.Region(1792, 128, 128, 128);
        _gold    = Atlas.Region(1920, 128, 128, 128);
        _lining  = Atlas.Region(1280, 0, 128, 128);
        _band    = Atlas.Region(1408, 0, 128, 128);
        _feather = Atlas.Region(1536, 0, 128, 128);
        _book    = Atlas.Region(1664, 0, 128, 128);
        _trim    = Atlas.Region(1792, 0, 128, 128);
        _skin    = Atlas.Region(1920, 0, 64, 128);
        _glass   = Atlas.Region(1984, 0, 64, 128);
    }

    private void Build()
    {
        PaintAtlas();

        BuildTorso();
        BuildHead();
        foreach (float side in new[] { 1f, -1f })
        {
            BuildArm(side);
            BuildSleeve(side);
            BuildHand(side);
            BuildLeg(side);
            BuildBoot(side);
        }

        BuildApron();
        BuildCoatSkirt();
        BuildLapels();
        BuildTurtleneck();
        BuildBelts();
        BuildBook();
        BuildNecklace();
        BuildHair();
        BuildGlasses();
        BuildHat();

        Kit.Save(Name, ArtFolder, PrefabFolder, Atlas, ModelFinish.Polished);
    }

    // =========================================================
    // TORSO
    // =========================================================

    private static Vector3 TorsoDirection(float deg)
    {
        float a = deg * Mathf.Deg2Rad;
        float s = Mathf.Sin(a), c = Mathf.Cos(a);
        const float round = 2f / 2.5f;
        return new Vector3(Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), round), 0f, Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), round));
    }

    // El corsé abulta un poco sobre el cuerpo.
    private static float Corset(float y) => 0.006f * Smooth(1.0f, 1.012f, y) * (1f - Smooth(1.225f, 1.237f, y));

    private static float Bust(float y, float deg) =>
        0.028f * Bell(y, 1.345f, 0.052f) * (Bell(deg, 31f, 23f) + Bell(deg, -31f, 23f));

    private static Vector3 TorsoPoint(float y, float deg, float offset)
    {
        Vector3 d = TorsoDirection(deg);
        float extra = offset + Bust(y, deg) + Corset(y);
        return new Vector3(d.x * (TorsoWidth.At(y) + extra), y, TorsoCenter.At(y) + d.z * (TorsoDepth.At(y) + extra));
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
            0.845f, 0.87f, 0.90f, 0.93f, 0.97f, 1.0f, 1.012f, 1.05f, 1.09f, 1.13f, 1.17f, 1.21f, 1.225f, 1.237f, 1.27f,
            1.30f, 1.325f, 1.345f, 1.37f, 1.395f, 1.42f, 1.445f, 1.47f, 1.495f, 1.52f, 1.55f, 1.58f, 1.62f,
        };
        const int columns = 28;

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

    // El abrigo va abierto adelante: el ancho de la abertura (°), más angosta en la cintura.
    private static float OpenDeg(float y) => Mathf.Lerp(21f, 34f, Smooth(1.05f, 1.43f, y));

    // Hasta dónde sube el abrigo: más alto atrás. Arriba de esto se ve el cuello de tortuga.
    private static float CoatTop(float deg) => 1.488f + 0.024f * Smooth(60f, 150f, Mathf.Abs(Wrap(deg)));

    // Las solapas: una tira de cada lado, de la cintura por el borde de la abertura hasta la nuca.
    private void BuildLapels()
    {
        foreach (float side in new[] { 1f, -1f })
        {
            const int steps = 30;
            var path = new Vector3[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float s = i / (float)steps;
                float y, deg;
                if (s < 0.62f)
                {
                    y = Mathf.Lerp(1.04f, 1.47f, s / 0.62f);
                    deg = OpenDeg(y) + LapelDeg * 0.5f;
                }
                else
                {
                    float t = (s - 0.62f) / 0.38f;
                    deg = Mathf.Lerp(OpenDeg(1.47f) + LapelDeg * 0.5f, 180f, Smooth(0f, 1f, t));
                    y = Mathf.Lerp(1.47f, CoatTop(180f) - 0.004f, Mathf.Sin(t * Mathf.PI * 0.5f));
                }
                path[i] = TorsoPoint(y, side * deg, 0.006f);
            }

            Kit.BeginPiece(40f);
            Kit.Sweep(path, Radial, t => new Vector2(Mathf.Lerp(0.013f, 0.010f, Smooth(0.6f, 1f, t)), 0.0045f), 6,
                (t, a) => _trim.Uv(a, t), (p, t) => TorsoSkin(p), capStart: true, capEnd: true);
        }
    }

    // El cuello de tortuga, doblado: un rollo de tejido alrededor del cuello.
    private void BuildTurtleneck()
    {
        const int steps = 28;
        var path = new Vector3[steps + 1];
        for (int i = 0; i <= steps; i++) path[i] = TorsoPoint(1.566f, -180f + 360f * i / steps, 0.011f);

        float v = Mathf.InverseLerp(TorsoBottom, TorsoTop, 1.555f);
        Kit.BeginPiece(70f);
        Kit.Sweep(path, Radial, t => new Vector2(0.014f, 0.0095f), 8, (t, a) => _torso.Uv(t, v),
            (p, t) => Chest.To(Neck, 0.5f), capStart: false, capEnd: false);
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
            z += 0.014f * Bell(deg, 0f, 9f) * Bell(h, 0.345f, 0.05f);                        // nariz, ancha
            z += 0.004f * Bell(abs, 9f, 4f) * Bell(h, 0.335f, 0.025f);                       // aletas de la nariz
            z += 0.004f * Bell(deg, 0f, 6f) * Smooth(0.36f, 0.42f, h) * (1f - Smooth(0.48f, 0.56f, h));
            z += 0.006f * Bell(h, 0.565f, 0.04f) * Bell(deg, 0f, 38f);                       // ceja
            z -= 0.007f * Bell(abs, 21f, 13f) * Bell(h, 0.5f, 0.045f);                       // cuenca del ojo
            z += 0.007f * Bell(deg, 0f, 17f) * Bell(h, 0.2f, 0.032f);                        // labios llenos
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

    // Lentes redondos de oro: dos aros, el puente y las patillas hasta las orejas (tapadas por el pelo).
    private void BuildGlasses()
    {
        ModelSkin skin = HeadBone;
        const float ringRadius = 0.0215f, eyeY = 1.6845f, eyeX = 0.0362f;
        float planeZ = HeadPoint(0.5f, 25f, 0f).z + 0.017f;
        Vector2 gold = _gold.Uv(0.25f, 0.75f);

        foreach (float side in new[] { 1f, -1f })
        {
            Vector3 normal = new Vector3(side * 0.2f, 0f, 1f).normalized;
            Vector3 across = Vector3.Cross(Vector3.up, normal).normalized;
            Vector3 outward = across * Mathf.Sign(across.x * side);
            var center = new Vector3(side * eyeX, eyeY, planeZ);

            const int steps = 22;
            var ring = new Vector3[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float a = i / (float)steps * Mathf.PI * 2f;
                ring[i] = center + outward * (Mathf.Cos(a) * ringRadius) + Vector3.up * (Mathf.Sin(a) * ringRadius * 0.95f);
            }
            Kit.BeginPiece(180f);
            Kit.Sweep(ring, p => normal, t => new Vector2(0.0024f, 0.0018f), 4, (t, a) => gold, (p, t) => skin, false, false);

            // La patilla, por el costado de la cara hasta detrás de la oreja
            Vector3 hinge = center + outward * ringRadius;
            var mid = new Vector3(side * 0.083f, eyeY + 0.003f, 0.075f);
            var ear = new Vector3(side * 0.087f, eyeY + 0.002f, 0.02f);
            Kit.BeginPiece(180f);
            Kit.Sweep(new[] { hinge, Vector3.Lerp(hinge, mid, 0.5f) + new Vector3(side * 0.006f, 0f, 0f), mid, ear, ear + new Vector3(0f, -0.012f, -0.016f) },
                p => Vector3.up, t => new Vector2(0.0016f, 0.0026f), 4, (t, a) => gold, (p, t) => skin, true, true);
        }

        // El puente sobre la nariz
        float bridgeZ = Mathf.Max(planeZ + 0.004f, HeadPoint(0.535f, 0f, 0f).z + 0.005f);
        var bridge = new Vector3[5];
        for (int i = 0; i < bridge.Length; i++)
        {
            float t = i / (bridge.Length - 1f);
            float x = Mathf.Lerp(eyeX - ringRadius, -(eyeX - ringRadius), t);
            bridge[i] = new Vector3(x, eyeY + 0.003f + 0.004f * Mathf.Sin(t * Mathf.PI), Mathf.Lerp(planeZ, bridgeZ, Mathf.Sin(t * Mathf.PI)));
        }
        Kit.BeginPiece(180f);
        Kit.Sweep(bridge, p => Vector3.forward, t => new Vector2(0.0016f, 0.0022f), 4, (t, a) => gold, (p, t) => skin, true, true);
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

    // El brazo es la manga del suéter: casi no se ve, la tapa la manga de campana.
    private void BuildArm(float side)
    {
        Kit.BeginPiece(55f);
        string name = SideName(side);
        Vector3[] chain = ArmChain(name);

        float[] stops =
        {
            -0.05f, -0.02f, 0.02f, 0.06f, 0.10f, 0.14f, 0.18f, 0.21f, 0.234f, 0.26f, 0.29f, 0.32f, 0.35f, 0.38f, 0.40f,
            0.42f, 0.435f, 0.446f,
        };
        const int columns = 10;

        var rings = new ModelPoint[stops.Length][];
        for (int r = 0; r < stops.Length; r++)
        {
            float d = stops[r];
            AlongChain(chain, d, out Vector3 center, out Vector3 tangent);
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, tangent).normalized;
            Vector3 front = Vector3.Cross(tangent, up).normalized * side;
            float radius = ArmRadius.At(d);

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
    }

    // La manga de campana del abrigo: angosta en el hombro, se abre hacia la muñeca y cuelga del lado de abajo.
    private void BuildSleeve(float side)
    {
        Kit.BeginPiece(32f);
        string name = SideName(side);
        Vector3[] chain = ArmChain(name);
        float[] stops = { -0.07f, -0.03f, 0.02f, 0.07f, 0.12f, 0.17f, 0.21f, 0.25f, 0.29f, 0.32f, 0.35f, 0.375f, SleeveEnd };
        const int columns = 18;

        var rings = new ModelPoint[stops.Length][];
        for (int r = 0; r < stops.Length; r++)
        {
            float d = stops[r];
            AlongChain(chain, d, out Vector3 center, out Vector3 tangent);
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, tangent).normalized;
            Vector3 front = Vector3.Cross(tangent, up).normalized * side;
            float t = Mathf.InverseLerp(stops[0], SleeveEnd, d);
            float bell = Smooth(0.5f, 1f, t);
            float radius = ArmRadius.At(d) + 0.014f + 0.05f * bell * bell;

            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float u = i / (float)columns;
                float a = (u - 0.5f) * Mathf.PI * 2f;
                float c = Mathf.Cos(a);
                float sag = c < 0f ? 1f + 0.5f * bell * -c : 1f;
                Vector3 p = center + up * (c * radius * sag) + front * (Mathf.Sin(a) * radius * 0.97f);
                rings[r][i] = new ModelPoint(p, _sleeve.Uv(u, t), ArmSkin(name, d));
            }
        }

        AlongChain(chain, 0.18f, out Vector3 inside, out _);
        Kit.Sheet(rings, _lining.Uv(0.25f, 0.5f), inside);
    }

    // Guantes de cuero sin dedos: la palma de cuero y los dedos de piel, un poco más cortos que los del esqueleto.
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
                rings[r][i] = new ModelPoint(p, _glove.Uv(i / (float)columns, 0.1f + 0.8f * r / (along.Length - 1f)), hand);
            }
        }
        Kit.Loft(rings, capEnd: true);

        foreach (string finger in new[] { "B-indexFinger", "B-middleFinger", "B-ringFinger", "B-pinky", "B-thumb" })
        {
            Kit.BeginPiece(60f);
            bool thumb = finger == "B-thumb";
            BuildFinger(finger, name, thumb ? 0.0172f : 0.0145f, 6, (t, a) => _skin.Uv(a, t), thumb ? 0.7f : 0.82f, 0.55f);
        }
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

    private static float LegBulge(float d)
    {
        float radius = LegRadius.At(d);
        radius += 0.010f * Smooth(BootTop - 0.003f, BootTop, d);
        radius += 0.010f * Smooth(BootTop - 0.003f, BootTop, d) * (1f - Smooth(BootCuff - 0.004f, BootCuff, d));   // el doblez
        return radius;
    }

    private void BuildLeg(float side)
    {
        Kit.BeginPiece(50f);
        string name = SideName(side);
        Vector3[] chain = LegChain(name);

        float[] stops =
        {
            -0.07f, -0.03f, 0.01f, 0.05f, 0.1f, 0.15f, 0.2f, 0.25f, 0.3f, 0.35f, 0.38f, 0.41f, 0.429f, 0.432f, 0.455f,
            0.474f, 0.478f, 0.51f, 0.55f, 0.59f, 0.63f, 0.67f, 0.71f, 0.75f, 0.78f, 0.82f,
        };
        const int columns = 12;

        var rings = new ModelPoint[stops.Length][];
        for (int r = 0; r < stops.Length; r++)
        {
            float d = stops[r];
            AlongChain(chain, d, out Vector3 center, out Vector3 tangent);
            Vector3 front = Vector3.ProjectOnPlane(Vector3.forward, tangent).normalized;
            Vector3 outer = Vector3.Cross(front, tangent).normalized;
            if (Mathf.Sign(outer.x) != Mathf.Sign(side)) outer = -outer;

            center += front * (0.008f * Bell(d, 0.41f, 0.03f) - 0.012f * Bell(d, 0.54f, 0.09f));
            float radius = LegBulge(d);

            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float u = i / (float)columns;
                float a = (u - 0.5f) * Mathf.PI * 2f;
                Vector3 p = center + front * (Mathf.Cos(a) * radius) + outer * (Mathf.Sin(a) * radius * 0.95f);
                rings[r][i] = new ModelPoint(p, _leg.Uv(u, Mathf.InverseLerp(-0.07f, 0.82f, d)), LegSkin(name, d));
            }
        }
        Kit.Loft(rings);
    }

    private void BuildBoot(float side)
    {
        Kit.BeginPiece(45f);
        string name = SideName(side);
        ModelSkin foot = Kit.Bone("B-foot" + name);
        ModelSkin toe = Kit.Bone("B-toe" + name);
        float x = Kit.Pos("B-foot" + name).x;

        float[] z      = { -0.094f, -0.084f, -0.05f, 0.0f, 0.05f, 0.095f, 0.14f, 0.18f, 0.208f, 0.226f };
        float[] half   = { 0.024f, 0.039f, 0.046f, 0.048f, 0.048f, 0.050f, 0.052f, 0.046f, 0.032f, 0.010f };
        float[] bottom = { 0.016f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.004f, 0.012f };
        float[] top    = { 0.086f, 0.118f, 0.152f, 0.164f, 0.128f, 0.098f, 0.080f, 0.064f, 0.047f, 0.028f };
        const int columns = 12;

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
                float v = Mathf.Lerp(0.2f + 0.78f * Mathf.InverseLerp(-0.1f, 0.23f, z[r]), 0.04f, sole);
                rings[r][i] = new ModelPoint(p, _boot.Uv(0.5f + 0.45f * c, v), skin);
            }
        }
        Kit.Loft(rings, wrap: true, capStart: true, capEnd: true);
    }

    // =========================================================
    // FALDA, FALDONES DEL ABRIGO Y CINTURONES
    // =========================================================

    private static Vector3 SkirtPoint(float y, float deg, float offset, float flare)
    {
        Vector3 d = TorsoDirection(deg);
        Vector3 p = TorsoPoint(Mathf.Max(y, 0.97f), deg, offset);
        p += d * (Mathf.Max(0f, 0.97f - y) * flare);
        p.y = y;
        return p;
    }

    // El centro sigue a las dos piernas por igual: con las piernas abiertas cuelga derecho.
    private ModelSkin ApronSkin(Vector3 p, float t)
    {
        float reach = Mathf.Min(1f, Mathf.Pow(Mathf.Max(0f, t), 0.8f) * 1.1f) * 0.8f * Smooth(1.03f, 0.84f, p.y);
        float right = Smooth(-0.17f, 0.17f, p.x);
        return Hips.To(Kit.Bone("B-thigh.R"), reach * right).To(Kit.Bone("B-thigh.L"), reach * (1f - right) / Mathf.Max(0.001f, 1f - reach * right));
    }

    // La falda que se ve por la abertura del abrigo.
    private void BuildApron()
    {
        Kit.BeginPiece(26f);
        const int columns = 10, rows = 7;
        var rings = new ModelPoint[rows + 1][];
        for (int r = 0; r <= rows; r++)
        {
            float t = r / (float)rows;
            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float u = i / (float)columns;
                float s = u * 2f - 1f;
                float deg = s * Mathf.Lerp(30f, 26f, t);
                float y = Mathf.Lerp(1.03f, 0.60f + 0.04f * Mathf.Abs(s), t);
                float offset = Mathf.Lerp(0.004f, 0.016f, Smooth(0f, 0.25f, t)) + 0.004f * Mathf.Sin(s * 7f) * t;
                Vector3 p = SkirtPoint(y, deg, offset, ApronFlare);
                rings[r][i] = new ModelPoint(p, _apron.Uv(u, 1f - t), ApronSkin(p, t));
            }
        }
        Kit.Sheet(rings, _lining.Uv(0.25f, 0.5f));
    }

    // Cada faldón sigue a su pierna: así una patada no lo atraviesa. Los de atrás, más largos, toman un poco de
    // la espinilla, para que el talón no los cruce al correr.
    private ModelSkin CoatSkin(Vector3 p, float t, float side, bool back)
    {
        string name = SideName(side);
        float reach = Mathf.Min(1f, Mathf.Pow(Mathf.Max(0f, t), 0.8f) * 1.1f) * 0.95f * Smooth(1.03f, 0.84f, p.y);
        ModelSkin skin = Hips.To(Kit.Bone("B-thigh" + name), reach);
        if (back) skin = skin.To(Kit.Bone("B-shin" + name), 0.4f * Smooth(0.52f, 0.36f, p.y));
        return skin;
    }

    // Adelante llega a la rodilla; atrás, a la pantorrilla.
    private static float CoatHem(float abs) =>
        abs < 97f ? Mathf.Lerp(0.50f, 0.45f, Smooth(20f, 97f, abs)) : Mathf.Lerp(0.45f, 0.35f, Smooth(97f, 180f, abs));

    // El abrigo sigue por debajo del cinturón en cuatro faldones: abierto adelante, a los costados y atrás.
    private void BuildCoatSkirt()
    {
        foreach (float side in new[] { 1f, -1f })
        {
            CoatPanel(side, 20f, 97f, false);
            CoatPanel(side, 97f, 179.4f, true);
        }
    }

    private void CoatPanel(float side, float fromDeg, float toDeg, bool back)
    {
        Kit.BeginPiece(28f);
        const int columns = 10, rows = 9;
        var rings = new ModelPoint[rows + 1][];
        for (int r = 0; r <= rows; r++)
        {
            float t = r / (float)rows;
            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float k = i / (float)columns;
                float abs = Mathf.Lerp(fromDeg, toDeg, k);
                float hem = CoatHem(abs) + 0.01f * Mathf.Sin(abs * 0.21f + side);
                float y = Mathf.Lerp(1.045f, hem, t);
                float fold = 0.009f * Mathf.Sin(k * 9f + fromDeg) * t;
                float offset = Mathf.Lerp(0.008f, 0.03f, Smooth(0f, 0.22f, t)) + fold;
                Vector3 p = SkirtPoint(y, side * abs, offset, CoatFlare);
                float u = back ? Mathf.Lerp(0.52f, 1f, k) : Mathf.Lerp(0f, 0.48f, k);
                rings[r][i] = new ModelPoint(p, _coat.Uv(u, 1f - t), CoatSkin(p, t, side, back));
            }
        }
        Kit.Sheet(rings, _lining.Uv(0.25f, 0.5f));
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

    // El cinturón de la cadera: cae adelante a la izquierda.
    private static float HipBelt(float deg) => 0.975f + 0.03f * Mathf.Cos((deg - 40f) * Mathf.Deg2Rad);

    private void BuildBelts()
    {
        Band(deg => 1.035f, 0.032f, 0.018f);
        Band(HipBelt, 0.03f, 0.04f);

        Kit.BeginPiece(30f);
        Vector3 turned = (Vector3.up + Vector3.right).normalized;
        Kit.Disc(TorsoPoint(1.035f, 0f, 0.026f), Vector3.forward, turned, 0.022f, 0.022f, 0.008f, 4, _gold, Spine.To(Hips, 0.5f));
        Vector3 buckle = TorsoPoint(0.97f, 40f, 0.048f);
        buckle.y = HipBelt(40f);
        Kit.Disc(buckle, TorsoDirection(40f).normalized, turned, 0.022f, 0.022f, 0.008f, 4, _gold, Hips);

        // Bolsas a la derecha y dos frascos adelante
        Pouch(78f);
        Pouch(102f);
        Vial(52f, 0.25f);
        Vial(64f, 0.75f);
    }

    private void Pouch(float deg)
    {
        Vector3 normal = TorsoDirection(deg).normalized;
        float y = HipBelt(deg) - 0.045f;
        Vector3 spot = SkirtPoint(y, deg, 0.06f, CoatFlare);
        ModelSkin skin = Hips.To(Kit.Bone(deg > 0f ? "B-thigh.R" : "B-thigh.L"), 0.3f);

        Kit.BeginPiece(44f);
        Kit.Lump(spot, Vector3.up, normal, 0.042f, 0.03f, 0.02f, 8, _glove, 0.5f, skin);
        Kit.BeginPiece(30f);
        Kit.Disc(spot + normal * 0.015f + Vector3.up * 0.02f, normal, Vector3.up, 0.028f, 0.024f, 0.005f, 8, _glove, skin);
        Kit.Gem(spot + normal * 0.024f + Vector3.up * 0.01f, Vector3.up, normal, 0.005f, 0.005f, 0.0035f, 6, _gold, skin);
    }

    // Un frasco colgado del cinturón: cuello angosto, cuerpo ancho y un corcho. glassU elige el color.
    private void Vial(float deg, float glassU)
    {
        Vector3 radial = TorsoDirection(deg).normalized;
        float top = HipBelt(deg) - 0.012f;
        Vector3 a = SkirtPoint(top, deg, 0.05f, CoatFlare);
        Vector3 b = SkirtPoint(top - 0.07f, deg, 0.05f, CoatFlare);
        ModelSkin skin = Hips.To(Kit.Bone("B-thigh.R"), 0.3f);

        float[] radius = { 0.005f, 0.005f, 0.011f, 0.012f, 0.010f };
        var path = new[] { a, Vector3.Lerp(a, b, 0.25f), Vector3.Lerp(a, b, 0.4f), Vector3.Lerp(a, b, 0.75f), b };
        Kit.BeginPiece(60f);
        Kit.Sweep(path, p => radial, t => Vector2.one * radius[Mathf.Clamp(Mathf.RoundToInt(t * 4f), 0, 4)], 8,
            (t, s) => _glass.Uv(glassU, Mathf.Lerp(0.95f, 0.1f, t)), (p, t) => skin, capStart: true, capEnd: true);

        Kit.BeginPiece(40f);
        Vector3 up = (a - b).normalized;
        Kit.Lump(a + up * 0.006f, up, radial, 0.007f, 0.0055f, 0.0055f, 6, _glove, 0.5f, skin);
    }

    // =========================================================
    // LA BIBLIOTECARIA: EL LIBRO Y LA PLUMA
    // =========================================================

    // Una caja cerrada (tapas, lomo y hojas del libro).
    private void Box(Vector3 center, Vector3 right, Vector3 up, Vector3 forward, Vector3 half, Vector2 uv, ModelSkin skin)
    {
        ModelPoint[] Face(float f)
        {
            Vector3 c = center + forward * (half.z * f);
            return new[]
            {
                new ModelPoint(c - right * half.x - up * half.y, uv, skin),
                new ModelPoint(c + right * half.x - up * half.y, uv, skin),
                new ModelPoint(c + right * half.x + up * half.y, uv, skin),
                new ModelPoint(c - right * half.x + up * half.y, uv, skin),
            };
        }

        Kit.BeginPiece(30f);
        Kit.Loft(new[] { Face(-1f), Face(1f) }, wrap: true, capStart: true, capEnd: true);
    }

    // Un libro gordo de tapas rojas, colgado de la cadera izquierda por una correa.
    private void BuildBook()
    {
        const float deg = -114f;
        Vector3 radial = TorsoDirection(deg).normalized;
        Vector3 up = (Vector3.up - radial * 0.32f).normalized;     // se inclina con el vuelo del abrigo
        Vector3 forward = Vector3.ProjectOnPlane(radial, up).normalized;
        Vector3 right = Vector3.Cross(up, forward).normalized;
        ModelSkin skin = Hips.To(Kit.Bone("B-thigh.L"), 0.45f);
        Vector3 center = SkirtPoint(0.86f, deg, 0.058f, CoatFlare) + forward * 0.02f;

        Vector2 cover = _book.Uv(0.25f, 0.5f), pages = _book.Uv(0.75f, 0.5f);
        Box(center, right, up, forward, new Vector3(0.047f, 0.064f, 0.0145f), pages, skin);
        Box(center + forward * 0.0165f, right, up, forward, new Vector3(0.051f, 0.068f, 0.0025f), cover, skin);
        Box(center - forward * 0.0165f, right, up, forward, new Vector3(0.051f, 0.068f, 0.0025f), cover, skin);
        Box(center - right * 0.0485f, right, up, forward, new Vector3(0.0035f, 0.068f, 0.019f), cover, skin);

        // Esquinas de oro, el emblema de la tapa y el broche
        Kit.BeginPiece(30f);
        foreach (float sx in new[] { 1f, -1f })
            foreach (float sy in new[] { 1f, -1f })
                Kit.Disc(center + forward * 0.019f + right * (sx * 0.043f) + up * (sy * 0.060f), forward, up, 0.009f, 0.009f, 0.0025f, 4, _gold, skin);
        Kit.Disc(center + forward * 0.019f, forward, up, 0.017f, 0.017f, 0.004f, 8, _gold, skin);
        Box(center + right * 0.0515f, right, up, forward, new Vector3(0.004f, 0.011f, 0.021f), _gold.Uv(0.25f, 0.7f), skin);

        // La correa, del cinturón al lomo
        Vector3 hook = TorsoPoint(0.97f, deg, 0.046f);
        hook.y = HipBelt(deg);
        Vector3 tie = center - right * 0.03f + up * 0.069f;
        Kit.BeginPiece(30f);
        Kit.Sweep(new[] { hook, Vector3.Lerp(hook, tie, 0.5f) + radial * 0.004f, tie }, p => radial, t => new Vector2(0.009f, 0.0025f), 4,
            (t, a) => _belt.Uv(0.3f * t, a < 0.5f ? 0.3f : 0.7f), (p, t) => skin, capStart: true, capEnd: true);
    }

    // Una pluma de oro colgada de un cordón rojo, sobre el suéter.
    private void BuildNecklace()
    {
        foreach (float side in new[] { 1f, -1f })
        {
            const int steps = 12;
            var path = new Vector3[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float y = Mathf.Lerp(1.52f, 1.352f, t);
                float deg = side * Mathf.Lerp(30f, 2.5f, Smooth(0f, 1f, t));
                path[i] = TorsoPoint(y, deg, 0.009f + 0.003f * Mathf.Sin(t * Mathf.PI));
            }
            Kit.BeginPiece(180f);
            Kit.Sweep(path, Radial, t => Vector2.one * 0.0019f, 4, (t, a) => _book.Uv(0.25f, 0.5f), (p, t) => Chest, true, true);
        }

        Kit.BeginPiece(30f);
        Kit.Gem(TorsoPoint(1.33f, 0f, 0.014f), Vector3.up, TorsoDirection(0f).normalized, 0.019f, 0.0075f, 0.0028f, 4, _gold, Chest);
    }

    // =========================================================
    // PELO: RIZOS
    // =========================================================

    private void BuildHair()
    {
        BuildHairCap();
        BuildCurlVolume();
        BuildRinglets();
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
                float swell = 0.006f + 0.014f * Smooth(0f, 0.5f, t) + 0.01f * Mathf.PerlinNoise(u * 15f, t * 4f);
                Vector3 p = HeadPoint(h, deg, swell);
                rings[r][i] = new ModelPoint(p, _hair.Uv(0.12f + 0.76f * Mathf.PingPong(u * 5f, 1f), Mathf.Lerp(0.3f, 0.1f, t)), HeadBone);
            }
        }
        Kit.Loft(rings);
    }

    // La cortina de los rizos. t va de 0 (bajo el sombrero) a 1 (la punta, en la espalda); s de -1 a 1 de un
    // costado de la cara al otro, pasando por atrás. Arriba rodea la cabeza; abajo cae detrás de la espalda.
    private static Vector3 VolumePoint(float t, float s, float lift)
    {
        float y = Mathf.Lerp(VolumeTop, 1.13f + 0.11f * s * s, t);
        float head = Smooth(1.50f, 1.64f, y);
        float a = s * Mathf.Lerp(78f, 116f, head) * Mathf.Deg2Rad;
        float rx = Mathf.Lerp(0.165f, 0.108f, head);
        float rz = Mathf.Lerp(0.056f, 0.128f, head);
        float cz = Mathf.Lerp(-0.07f, 0.015f, head);
        var dir = new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a));
        Vector3 normal = new Vector3(dir.x * rz, 0f, dir.z * rx).normalized;
        return new Vector3(dir.x * rx, y, cz + dir.z * rz) + normal * lift;
    }

    private void BuildCurlVolume()
    {
        Kit.BeginPiece(60f);
        const int columns = 22, rows = 16;
        var hem = new float[columns + 1];
        for (int i = 0; i <= columns; i++) hem[i] = Range(-0.025f, 0.02f);

        var rings = new ModelPoint[rows + 1][];
        for (int r = 0; r <= rows; r++)
        {
            float t = r / (float)rows;
            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float u = i / (float)columns;
                float bump = (Mathf.PerlinNoise(u * 7f + 1.3f, t * 9f + 2.1f) - 0.5f) * 0.02f;
                Vector3 p = VolumePoint(t, u * 2f - 1f, bump);
                if (r == rows) p.y += hem[i];
                rings[r][i] = new ModelPoint(p, _hair.Uv(0.1f + 0.8f * u, 0.3f + 0.4f * t), HairSkin(p));
            }
        }
        Kit.Sheet(rings, _hair.Uv(0.5f, 0.15f), new Vector3(0f, 1.5f, -0.02f));
    }

    // Un rizo: un tubo que da vueltas como un resorte alrededor de la curva center(t), de 0 (la raíz) a 1.
    // Más apretado en la raíz y más suelto en la punta.
    private void Ringlet(Func<float, Vector3> center, float coil, float tube, float turnsPerMeter, float uStrip)
    {
        float length = 0f;
        Vector3 last = center(0f);
        for (int i = 1; i <= 20; i++)
        {
            Vector3 p = center(i / 20f);
            length += Vector3.Distance(last, p);
            last = p;
        }

        int turns = Mathf.Max(2, Mathf.RoundToInt(length * turnsPerMeter));
        const int perTurn = 5;
        int samples = turns * perTurn + 1;
        float phase = Range(0f, Mathf.PI * 2f);

        var path = new Vector3[samples];
        Vector3 normal = Vector3.zero;
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)(samples - 1);
            Vector3 c = center(t);
            Vector3 tangent = (center(Mathf.Min(1f, t + 0.01f)) - center(Mathf.Max(0f, t - 0.01f))).normalized;

            // El marco se arrastra de una muestra a la otra: si no, la espiral salta de golpe
            normal = i == 0 ? Perpendicular(tangent) : Vector3.ProjectOnPlane(normal, tangent).normalized;
            if (normal.sqrMagnitude < 0.5f) normal = Perpendicular(tangent);
            Vector3 binormal = Vector3.Cross(tangent, normal);

            float angle = phase + t * turns * Mathf.PI * 2f;
            float radius = coil * Mathf.Lerp(0.3f, 1f, Smooth(0f, 0.18f, t)) * (0.9f + 0.3f * t);
            path[i] = c + (normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle)) * radius;
        }

        // Normales suaves del todo: la espiral gira 60° de una muestra a la otra, y con un corte de menos de 180°
        // cada cara elige otras vecinas y cada vértice sale repetido hasta cuatro veces.
        Kit.BeginPiece(180f);
        Kit.Sweep(path, p => Vector3.up,
            t => Vector2.one * Mathf.Max(0.0015f, tube * Mathf.Min(1f, 0.5f + t * 4f) * (1f - 0.6f * Mathf.Pow(t, 3f))),
            4, (t, a) => _hair.Uv(uStrip + 0.05f * Mathf.Abs(a * 2f - 1f), 0.25f + 0.7f * t),
            (p, t) => HairSkin(p), capStart: true, capEnd: true);
    }

    private static Vector3 Perpendicular(Vector3 v)
    {
        Vector3 reference = Mathf.Abs(Vector3.Dot(v, Vector3.right)) < 0.9f ? Vector3.right : Vector3.forward;
        return Vector3.Cross(v, reference).normalized;
    }

    private void BuildRinglets()
    {
        // La capa de abajo: siguen la cortina hasta la mitad de la espalda
        const int back = 16;
        for (int i = 0; i < back; i++)
        {
            float s = Mathf.Lerp(-0.94f, 0.94f, i / (float)(back - 1)) + Range(-0.03f, 0.03f);
            float from = Range(0.06f, 0.2f), to = Range(1.0f, 1.07f), lift = Range(0.012f, 0.02f);
            Ringlet(t => VolumePoint(Mathf.Lerp(from, to, t), s, lift), Range(0.011f, 0.014f), Range(0.0075f, 0.0095f), 15f, Range(0.15f, 0.85f));
        }

        // Arriba: más cortos y encima, para el volumen bajo el sombrero
        const int top = 12;
        for (int i = 0; i < top; i++)
        {
            float s = Mathf.Lerp(-1f, 1f, (i + 0.5f) / top) + Range(-0.04f, 0.04f);
            float from = Range(0f, 0.04f), to = Range(0.38f, 0.58f), lift = Range(0.026f, 0.034f);
            Ringlet(t => VolumePoint(Mathf.Lerp(from, to, t), s, lift), Range(0.012f, 0.015f), Range(0.008f, 0.0095f), 17f, Range(0.15f, 0.85f));
        }

        // Adelante de los hombros, sobre el pecho
        foreach (float side in new[] { 1f, -1f })
            for (int i = 0; i < 3; i++)
            {
                Vector3 root = HeadPoint(Range(0.58f, 0.68f), side * Mathf.Lerp(68f, 90f, i / 2f), 0.03f);
                var tip = new Vector3(side * (0.098f + 0.016f * i), Range(1.33f, 1.38f), 0.135f - 0.03f * i);
                Vector3 c1 = root + Outward(root) * 0.04f + Vector3.down * 0.04f;
                var c2 = new Vector3(side * (0.11f + 0.012f * i), 1.5f, 0.10f - 0.03f * i);
                Ringlet(t => Bezier(root, c1, c2, tip, t), Range(0.011f, 0.013f), Range(0.0075f, 0.009f), 17f, Range(0.15f, 0.85f));
            }

        // Bajo el ala: en la frente y a los costados de la cara
        foreach (float deg in new[] { -30f, -6f, 22f })
        {
            Vector3 root = HeadPoint(0.86f, deg, 0.022f);
            Vector3 tip = HeadPoint(Range(0.66f, 0.72f), deg + Range(-8f, 8f), 0.032f);
            Vector3 bulge = Outward(root) * 0.012f;
            Ringlet(t => Vector3.Lerp(root, tip, t) + bulge * Mathf.Sin(t * Mathf.PI), 0.009f, 0.0065f, 30f, Range(0.15f, 0.85f));
        }
        foreach (float side in new[] { 1f, -1f })
            foreach (float deg in new[] { 56f, 70f })
            {
                Vector3 root = HeadPoint(0.74f, side * deg, 0.024f);
                Vector3 tip = HeadPoint(Range(0.36f, 0.44f), side * (deg + 8f), 0.04f);
                Vector3 bulge = Outward(root) * 0.015f;
                Ringlet(t => Vector3.Lerp(root, tip, t) + bulge * Mathf.Sin(t * Mathf.PI), 0.011f, 0.0075f, 26f, Range(0.15f, 0.85f));
            }
    }

    // =========================================================
    // EL SOMBRERO
    // =========================================================

    private static Vector3 HatUp => new Vector3(0f, Mathf.Cos(HatTilt * Mathf.Deg2Rad), -Mathf.Sin(HatTilt * Mathf.Deg2Rad));
    private static Vector3 HatForward => Vector3.Cross(Vector3.right, HatUp);

    // Un punto del ala: angle 0 adelante, 90 a su derecha; k de 0 (la copa) a 1 (el borde). Cae más atrás.
    private static Vector3 BrimPoint(float angle, float k)
    {
        float a = angle * Mathf.Deg2Rad;
        float inner = 1f / Mathf.Sqrt(Sq(Mathf.Sin(a) / 0.113f) + Sq(Mathf.Cos(a) / 0.130f));
        float r = Mathf.Lerp(inner, BrimRadius, k);
        float back = 0.5f - 0.5f * Mathf.Cos(a);
        float droop = k * k * (0.016f + 0.034f * back) + 0.009f * Mathf.Sin(a * 5f + 0.6f) * k * k;
        return HatBase + HatUp * (0.006f - droop) + Vector3.right * (Mathf.Sin(a) * r) + HatForward * (Mathf.Cos(a) * r);
    }

    private void BuildHat()
    {
        ModelSkin skin = HeadBone;

        // El ala
        Kit.BeginPiece(40f);
        const int around = 44;
        float[] ks = { 0f, 0.15f, 0.35f, 0.55f, 0.72f, 0.86f, 0.95f, 1f };
        var brim = new ModelPoint[ks.Length][];
        for (int r = 0; r < ks.Length; r++)
        {
            brim[r] = new ModelPoint[around + 1];
            for (int i = 0; i <= around; i++)
            {
                float u = i / (float)around;
                brim[r][i] = new ModelPoint(BrimPoint(u * 360f, ks[r]), _brim.Uv(u, ks[r]), skin);
            }
        }
        Kit.Sheet(brim, _lining.Uv(0.75f, 0.5f), HatBase - HatUp * 0.3f);

        // La copa: sube, se dobla hacia atrás y la punta cae
        Vector3 b0 = HatBase + HatUp * 0.004f;
        Vector3 backward = -HatForward;
        Vector3 b1 = b0 + HatUp * (HatHeight * 0.45f);
        Vector3 b2 = b0 + HatUp * (HatHeight * 0.92f) + backward * 0.03f;
        Vector3 b3 = b0 + HatUp * (HatHeight * 0.78f) + backward * 0.21f;
        const int steps = 20;
        var path = new Vector3[steps + 1];
        for (int i = 0; i <= steps; i++) path[i] = Bezier(b0, b1, b2, b3, i / (float)steps);

        Kit.BeginPiece(42f);
        Kit.Sweep(path, p => Vector3.right,
            t =>
            {
                float shape = Mathf.Pow(1f - t, 1.05f) * (1f + 0.07f * Mathf.Sin(t * 15f)) + 0.012f * (1f - t);
                return new Vector2(0.130f * shape, 0.113f * shape);
            },
            18, (t, a) => _hatCone.Uv(a, t), (p, t) => skin, capStart: true, capEnd: true);

        // La cinta
        const int bandSteps = 36;
        Vector3 bandCenter = HatBase + HatUp * 0.024f;
        var band = new Vector3[bandSteps + 1];
        for (int i = 0; i <= bandSteps; i++)
        {
            float a = i / (float)bandSteps * Mathf.PI * 2f;
            band[i] = bandCenter + Vector3.right * (Mathf.Sin(a) * 0.1165f) + HatForward * (Mathf.Cos(a) * 0.1335f);
        }
        Kit.BeginPiece(40f);
        Kit.Sweep(band, p => (p - bandCenter).normalized, t => new Vector2(0.017f, 0.0035f), 4,
            (t, a) => _band.Uv(Mathf.PingPong(t * 4f, 1f), Mathf.Min(1f, a * 2f)), (p, t) => skin, false, false);

        // Una luna de oro adelante, sobre la cinta
        Vector3 moonCenter = bandCenter + HatForward * 0.1375f;
        const int moonSteps = 14;
        var moon = new Vector3[moonSteps + 1];
        for (int i = 0; i <= moonSteps; i++)
        {
            float a = Mathf.Lerp(45f, 315f, i / (float)moonSteps) * Mathf.Deg2Rad;
            moon[i] = moonCenter + Vector3.right * (Mathf.Cos(a) * 0.016f) + HatUp * (Mathf.Sin(a) * 0.016f);
        }
        Vector3 facing = HatForward;
        Kit.BeginPiece(45f);
        Kit.Sweep(moon, p => facing, t => new Vector2(0.001f + 0.0062f * Mathf.Sin(t * Mathf.PI), 0.0032f), 5,
            (t, a) => _gold.Uv(0.25f, 0.8f), (p, t) => skin, true, true);

        // Una pluma metida en la cinta, a su izquierda
        Vector3 featherRoot = bandCenter + Vector3.right * -0.117f + HatForward * 0.015f;
        Vector3 featherDir = (HatUp * 0.8f - HatForward * 0.45f - Vector3.right * 0.2f).normalized;
        Kit.BeginPiece(35f);
        Kit.Tuft(featherRoot, featherDir, -Vector3.right, 0.2f, 0.03f, 0.004f, 0.12f, _feather, 0.5f, skin);
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
        Atlas.Paint(_hatCone, PaintHatCone);
        Atlas.Paint(_brim, PaintBrim);
        Atlas.Paint(_coat, PaintCoat);
        Atlas.Paint(_sleeve, PaintSleeve);
        Atlas.Paint(_apron, PaintApron);
        Atlas.Paint(_boot, PaintBoot);
        Atlas.Paint(_belt, PaintBelt);
        Atlas.Paint(_glove, (u, v) => Grain(Color.Lerp(LeatherDark, Leather, 0.4f + 0.6f * v), u, v, 30f, 0.16f));
        Atlas.Paint(_gold, (u, v) => GoldAt(v));
        Atlas.Paint(_glass, PaintGlass);
        Atlas.Paint(_lining, (u, v) => u < 0.5f ? new Color(0.22f, 0.05f, 0.17f, 0.06f) : new Color(0.15f, 0.075f, 0.28f, 0.06f));
        Atlas.Paint(_band, PaintBand);
        Atlas.Paint(_feather, PaintFeather);
        Atlas.Paint(_book, PaintBook);
        Atlas.Paint(_trim, PaintTrim);
        Atlas.Paint(_skin, (u, v) => Grain(SkinTone, u, v, 20f, 0.05f));
    }

    // ---------- Pinceles de la receta ----------

    private static Color GoldAt(float sheen)
    {
        Color c = Color.Lerp(GoldDark, Gold, Mathf.Clamp01(0.35f + 0.65f * sheen));
        return Over(c, GoldLight, 0.55f * Smooth(0.75f, 1f, sheen));
    }

    private static Color CoatCloth(float u, float v)
    {
        Color c = Grain(Coat, u, v, 70f, 0.12f);
        return c * (0.86f + 0.28f * Mathf.PerlinNoise(u * 9f + 3f, v * 3f));
    }

    // Tejido de punto: costillas verticales.
    private static Color Knit(float u, float v, float deg)
    {
        float rib = 0.5f + 0.5f * Mathf.Cos(deg * Mathf.Deg2Rad * 60f);
        Color c = Color.Lerp(SweaterDark, Sweater, 0.62f + 0.38f * rib);
        return c * (0.9f + 0.2f * Mathf.PerlinNoise(u * 8f, v * 14f));
    }

    // Un número de 0 a 1 que cambia de celda a celda y se repite igual en cada pasada.
    private static float Hash(float a, float b)
    {
        float h = Mathf.Sin(a * 127.1f + b * 311.7f) * 43758.5453f;
        return h - Mathf.Floor(h);
    }

    // Una estrella de cuatro puntas: x, y en unidades de su radio. Da cuánto la cubre.
    private static float Star4(float x, float y) =>
        Smooth(1.02f, 0.92f, Mathf.Pow(Mathf.Abs(x), 0.5f) + Mathf.Pow(Mathf.Abs(y), 0.5f));

    // Una luna creciente, abierta hacia la derecha.
    private static float CrescentMask(float x, float y)
    {
        float outer = Smooth(1.04f, 0.96f, Mathf.Sqrt(x * x + y * y));
        float bite = Smooth(0.78f, 0.86f, Mathf.Sqrt(Sq(x - 0.42f) + Sq(y - 0.12f)));
        return outer * bite;
    }

    // Un círculo mágico: dos aros y dos estrellas cruzadas.
    private static float Sigil(float x, float y)
    {
        float r = Mathf.Sqrt(x * x + y * y);
        float rings = Mathf.Max(Smooth(0.07f, 0.035f, Mathf.Abs(r - 1f)), Smooth(0.05f, 0.025f, Mathf.Abs(r - 0.84f)));
        float star = Star4(x / 0.72f, y / 0.72f);
        float rx = (x + y) * 0.7071f, ry = (y - x) * 0.7071f;
        float star2 = Star4(rx / 0.5f, ry / 0.5f);
        float hole = Smooth(0.16f, 0.12f, r);
        return Mathf.Clamp01(Mathf.Max(rings, Mathf.Max(star, star2)) - hole);
    }

    // La forma del ojo (abierto de 0 a 1) y qué tan lejos está del centro del iris (1 = su borde).
    private static float EyeMask(float ax, float y, out float radial)
    {
        float ex = (ax - 0.0362f) / 0.0198f;
        float ey = (y - 1.6835f - (ax - 0.0362f) * 0.1f) / 0.0098f;
        radial = Mathf.Sqrt(Sq((ax - 0.0352f) / 0.0102f) + Sq((y - 1.6838f) / 0.0098f));
        float open = 1f - ex * ex;
        if (open <= 0f) return 0f;
        float lid = Mathf.Sqrt(open);
        return Smooth(lid * 1.02f, lid * 0.9f, Mathf.Abs(ey));
    }

    private static Color PaintHead(float u, float v)
    {
        float x = Mathf.Lerp(FaceLeft, FaceRight, u);
        float y = Mathf.Lerp(FaceBottom, FaceTop, v);
        float ax = Mathf.Abs(x);

        Color c = Grain(SkinTone, u, v, 24f, 0.05f);
        c = Over(c, SkinShade, 0.35f * Smooth(0.058f, 0.088f, ax));
        c = Over(c, SkinShade, 0.22f * Smooth(1.578f, 1.558f, y));
        c = Over(c, SkinLight, 0.25f * Bell(ax, 0.045f, 0.016f) * Bell(y, 1.652f, 0.014f));                 // pómulos
        c = Over(c, SkinLight, 0.30f * Bell(x, 0f, 0.0055f) * Bell(y, 1.662f, 0.026f));                     // la luz de la nariz
        c = Over(c, SkinLight, 0.18f * Bell(x, 0f, 0.02f) * Bell(y, 1.725f, 0.02f));                        // frente
        c = Over(c, new Color(0.55f, 0.22f, 0.17f, 0.3f), 0.16f * Bell(ax, 0.05f, 0.02f) * Bell(y, 1.645f, 0.016f));
        c = Over(c, SkinShade, 0.4f * Bell(ax, 0.036f, 0.026f) * Bell(y, 1.693f, 0.011f));
        c = Over(c, SkinShade, 0.45f * Bell(x, 0f, 0.011f) * Bell(y, 1.6325f, 0.0035f));
        c = Over(c, SkinShade * 0.8f, 0.6f * Bell(ax, 0.012f, 0.0035f) * Bell(y, 1.6365f, 0.0024f));       // fosas de la nariz

        // Labios llenos
        float lipY = y - 1.6075f - 2.0f * ax * ax;
        const float lipHalf = 0.0215f;
        float cupid = 0.0012f * Bell(ax, 0.005f, 0.004f);
        float upper = Smooth(0f, 0.001f, lipY) * Smooth(0.0082f + cupid - 0.0068f * ax / lipHalf, 0.0072f + cupid - 0.0068f * ax / lipHalf, lipY);
        float lower = Smooth(0f, -0.001f, lipY) * Smooth(-0.0108f + 0.0098f * ax / lipHalf, -0.0098f + 0.0098f * ax / lipHalf, lipY);
        float inside = Smooth(lipHalf, lipHalf - 0.0015f, ax);
        c = Over(c, Lip * 0.85f, upper * inside);
        c = Over(c, Lip, lower * inside);
        c = Over(c, Lip * 0.45f, Smooth(0.0011f, 0.0004f, Mathf.Abs(lipY)) * inside);
        c = Over(c, new Color(0.66f, 0.42f, 0.38f, 0.6f), 0.4f * Bell(ax, 0.004f, 0.006f) * Bell(lipY, -0.0055f, 0.0013f));

        // Ojos castaños, grandes
        float eye = EyeMask(ax, y, out float radial);
        float ex = (ax - 0.0362f) / 0.0198f;
        float ey = (y - 1.6835f - (ax - 0.0362f) * 0.1f) / 0.0098f;
        if (eye > 0f)
        {
            float ix = (ax - 0.0352f) / 0.0102f, iy = (y - 1.6838f) / 0.0098f;
            Color ball = new Color(0.90f, 0.86f, 0.82f, 0.8f);
            Color iris = Color.Lerp(new Color(0.62f, 0.38f, 0.14f, 0.9f), new Color(0.20f, 0.10f, 0.04f, 0.9f), Smooth(0.15f, 0.95f, radial));
            ball = Over(ball, iris, Smooth(1.05f, 0.95f, radial));
            ball = Over(ball, new Color(0.02f, 0.015f, 0.01f, 0.9f), Smooth(0.42f, 0.34f, radial));
            ball = Over(ball, Color.white, Smooth(0.24f, 0.16f, Vector2.Distance(new Vector2(ix, iy), new Vector2(-0.3f, 0.35f))));
            ball = Over(ball, SkinShade * 0.5f, Smooth(0.2f, 1f, ey) * 0.5f);   // el párpado le da sombra
            c = Over(c, ball, eye);
        }

        // Delineado y pestañas
        float open = 1f - ex * ex;
        float upperLine = Mathf.Abs(ey - Mathf.Sqrt(Mathf.Max(0f, open)));
        c = Over(c, new Color(0.05f, 0.03f, 0.025f, 0.2f), Smooth(0.5f, 0.22f, upperLine) * Smooth(1.32f, 1.05f, Mathf.Abs(ex)) * Smooth(-0.3f, 0.1f, ey));
        float wing = Smooth(0.0018f, 0.0008f, Mathf.Abs(y - (1.6905f + (ax - 0.052f) * 0.5f))) * Smooth(0.052f, 0.055f, ax) * Smooth(0.064f, 0.059f, ax);
        c = Over(c, new Color(0.05f, 0.03f, 0.025f, 0.2f), wing);

        // Cejas
        float bx = ax - 0.0145f;
        float browY = 1.7005f + bx * 0.5f - bx * bx * 5.5f;
        float browThick = Mathf.Lerp(0.0036f, 0.0012f, Smooth(0f, 0.045f, bx));
        float brow = Smooth(browThick + 0.001f, browThick, Mathf.Abs(y - browY)) * Smooth(0f, 0.003f, bx) * Smooth(0.047f, 0.042f, bx);
        return Over(c, new Color(0.06f, 0.04f, 0.03f, 0.25f), brow);
    }

    private static Color PaintTorso(float u, float v)
    {
        float deg = (u - 0.5f) * 360f;
        float abs = Mathf.Abs(deg);
        float y = Mathf.Lerp(TorsoBottom, TorsoTop, v);

        // Lo de debajo del abrigo: la piel del cuello, el suéter, el corsé y la falda
        Color under;
        if (y > 1.585f) under = Over(Grain(SkinTone, u * 3f, v, 30f, 0.05f), SkinShade, 0.45f * Smooth(1.62f, 1.59f, y));
        else if (y > 1.237f) under = Knit(u, v, deg);
        else if (y > 0.998f) under = CorsetAt(u, v, deg, abs, y);
        else under = Grain(Skirt, u * 3f, v, 60f, 0.2f);

        // El abrigo: todo menos la abertura de adelante y el cuello
        float open = OpenDeg(y);
        float top = CoatTop(deg);
        float coat = Smooth(open - 0.5f, open + 0.5f, abs) * Smooth(top + 0.003f, top - 0.003f, y);
        Color c = Over(under, CoatCloth(u * 3f, v), coat);

        // Ribete de oro en el borde de la abertura y arriba
        float distance = Mathf.Min((abs - open) * 0.0024f, top - y);
        c = Over(c, Gold, coat * Smooth(0.0095f, 0.0075f, distance));
        return Over(c, GoldDark, coat * Smooth(0.0022f, 0.001f, Mathf.Abs(distance - 0.0085f)));
    }

    // El corsé de cuero: varillas, cordones cruzados adelante con ojales de oro.
    private static Color CorsetAt(float u, float v, float deg, float abs, float y)
    {
        Color c = Grain(Leather, u * 3f, v, 50f, 0.18f);
        c *= 1f + (Mathf.PerlinNoise(u * 12f, v * 9f) - 0.5f) * 0.3f;

        float seam = Mathf.Abs(Mathf.Repeat(deg + 9f, 18f) - 9f);
        c = Over(c, LeatherDark, 0.6f * Smooth(1.2f, 0.4f, seam));
        float edge = Mathf.Min(y - 1.0f, 1.235f - y);
        c = Over(c, LeatherDark, Smooth(0.008f, 0.004f, edge));

        float row = Mathf.Repeat(y - 1.0f, 0.026f) / 0.026f;
        if (abs < 5.5f)
        {
            float x = deg / 4.2f;
            float lace = Mathf.Min(Mathf.Abs(x - (row * 2f - 1f)), Mathf.Abs(x + (row * 2f - 1f)));
            c = Over(c, new Color(0.85f, 0.75f, 0.55f, 0.1f), Smooth(0.22f, 0.12f, lace) * Smooth(1.05f, 0.95f, Mathf.Abs(x)));
        }
        float eyeX = (abs - 4.4f) * 0.0024f;
        float eyeY = Mathf.Min(row, 1f - row) * 0.026f;
        return Over(c, Gold, Smooth(0.0032f, 0.0022f, Mathf.Sqrt(eyeX * eyeX + eyeY * eyeY)));
    }

    private static Color PaintArm(float u, float v)
    {
        float d = Mathf.Lerp(-0.05f, 0.446f, v);
        float deg = (u - 0.5f) * 360f;
        Color c = Knit(u, v, deg * 0.5f);
        Color cuff = Color.Lerp(SweaterDark, Sweater, 0.5f + 0.5f * Mathf.Cos(deg * Mathf.Deg2Rad * 24f));
        return Over(c, cuff, Smooth(0.392f, 0.398f, d));
    }

    private static Color PaintLeg(float u, float v)
    {
        float d = Mathf.Lerp(-0.07f, 0.82f, v);
        float deg = (u - 0.5f) * 360f;
        float abs = Mathf.Abs(deg);

        // Medias
        Color c = Grain(Tights, u * 2f, v, 60f, 0.25f);
        c = Over(c, Tights * 1.6f, 0.35f * Bell(abs, 0f, 40f) * Bell(d, 0.3f, 0.2f));

        // Bota alta, con el doblez arriba, cordones adelante y una correa en el tobillo
        float boot = Smooth(BootTop - 0.002f, BootTop + 0.002f, d);
        if (boot <= 0f) return c;

        Color leather = Grain(Leather, u * 2f, v, 40f, 0.16f);
        leather *= 1f + (Mathf.PerlinNoise(u * 6f, v * 30f) - 0.5f) * 0.4f;
        leather = Over(leather, LeatherDark, 0.5f * Smooth(110f, 170f, abs));
        leather = Over(leather, LeatherLight, 0.7f * Smooth(BootCuff + 0.002f, BootCuff - 0.002f, d));
        leather = Over(leather, LeatherDark, Smooth(0.006f, 0.002f, Mathf.Abs(d - BootCuff)));

        float zig = Mathf.Abs(Mathf.Repeat(d / 0.028f, 1f) - 0.5f) * 2f;
        float lace = Smooth(3.2f, 2.2f, Mathf.Abs(abs - zig * 14f)) * Smooth(0.5f, 0.505f, d) * Smooth(0.72f, 0.715f, d);
        leather = Over(leather, new Color(0.80f, 0.62f, 0.36f, 0.1f), lace);

        float strap = Mathf.Abs(d - 0.745f);
        leather = Over(leather, LeatherDark, Smooth(0.011f, 0.009f, strap));
        leather = Over(leather, Gold, Smooth(9f, 7f, Mathf.Abs(deg - 60f)) * Smooth(0.012f, 0.009f, strap));
        return Over(c, leather, boot);
    }

    private static Color PaintHair(float u, float v)
    {
        Color c = Color.Lerp(HairRoot, HairMid, Smooth(0f, 0.5f, v));
        c = Color.Lerp(c, HairTip, 0.5f * Smooth(0.55f, 1f, v));
        float strands = Mathf.PerlinNoise(u * 40f, v * 6f);
        float curls = Mathf.PerlinNoise(u * 18f + 4f, v * 18f + 1f);
        c *= 0.75f + 0.45f * strands;
        return Over(c, HairTip * 1.25f, 0.45f * Smooth(0.62f, 0.8f, curls));
    }

    private static Color PaintHatCone(float u, float v)
    {
        Color c = Grain(HatTone, u, v, 60f, 0.14f);
        c *= 0.82f + 0.36f * Mathf.PerlinNoise(u * 10f, v * 6f + 2f);
        c = Over(c, HatDark, 0.45f * Smooth(0.55f, 0.75f, v) * Smooth(0.6f, 0.9f, Mathf.PerlinNoise(u * 14f, v * 3f)));   // arrugas del doblez

        // Estrellitas de oro sueltas
        float cx = Mathf.Floor(u * 9f), cy = Mathf.Floor(v * 5f + cx * 0.37f);
        if (Hash(cx, cy) < 0.55f)
        {
            float sx = Mathf.Repeat(u * 9f, 1f) - 0.5f, sy = Mathf.Repeat(v * 5f + cx * 0.37f, 1f) - 0.5f;
            c = Over(c, Gold, 0.9f * Star4(sx / 0.16f, sy / 0.16f * 0.55f) * Smooth(0.08f, 0.15f, v) * Smooth(0.7f, 0.6f, v));
        }
        return c;
    }

    private static Color PaintBrim(float u, float v)
    {
        Color c = Grain(HatTone, u, v, 60f, 0.12f);
        c *= 0.85f + 0.3f * Mathf.PerlinNoise(u * 16f, v * 4f);
        c = Over(c, HatDark, 0.4f * Smooth(0.25f, 0f, v));

        // El borde: dos líneas de oro y un zigzag entre ellas
        float line = Mathf.Min(Mathf.Abs(v - 0.9f), Mathf.Abs(v - 0.975f));
        c = Over(c, Gold, Smooth(0.009f, 0.005f, line));
        float zig = Mathf.Abs(v - 0.9375f - 0.022f * (Mathf.Abs(Mathf.Repeat(u * 64f, 2f) - 1f) * 2f - 1f));
        c = Over(c, Gold, Smooth(0.007f, 0.004f, zig));

        // Lunas y estrellas alrededor
        float index = Mathf.Floor(u * 12f);
        float cell = Mathf.Repeat(u * 12f, 1f) - 0.5f;
        float my = (v - 0.62f) / 0.12f;
        float shape = Mathf.Repeat(index, 2f) < 1f ? CrescentMask(cell / 0.22f, my) : Star4(cell / 0.2f, my / 0.85f);
        return Over(c, GoldAt(0.6f), shape);
    }

    // Los faldones: u de 0 a 0.48 son los de adelante (la abertura en 0) y de 0.52 a 1 los de atrás (el tajo en 1).
    private static Color PaintCoat(float u, float v)
    {
        bool back = u > 0.5f;
        float k = back ? Mathf.InverseLerp(0.52f, 1f, u) : Mathf.InverseLerp(0f, 0.48f, u);
        Color c = CoatCloth(u * 2f, v);
        c *= 1f + Mathf.Cos(k * 9f + (back ? 97f : 20f)) * 0.14f * Smooth(0.95f, 0.3f, v);
        c = Over(c, CoatDark, 0.4f * Smooth(0.85f, 1f, v));

        // Ribete abajo, con estrellas entre las dos líneas, y en el borde de la abertura
        float hemLine = Mathf.Min(Mathf.Abs(v - 0.035f), Mathf.Abs(v - 0.13f));
        c = Over(c, Gold, Smooth(0.008f, 0.005f, hemLine));
        float sx = Mathf.Repeat(k * 6f, 1f) - 0.5f, sy = (v - 0.0825f) / 0.035f;
        c = Over(c, Gold, Star4(sx / 0.3f, sy));
        float edge = back ? 1f - k : k;
        c = Over(c, Gold, Smooth(0.03f, 0.018f, edge) * Smooth(0.12f, 0.16f, v));

        // Un círculo mágico en cada faldón de atrás (unos 30 cm de ancho por 70 de alto)
        if (back) c = Over(c, GoldAt(0.6f), Sigil((k - 0.5f) * 0.30f / 0.075f, (v - 0.4f) * 0.70f / 0.075f));
        return c;
    }

    private static Color PaintSleeve(float u, float v)
    {
        Color c = CoatCloth(u, v * 2f);
        c *= 0.88f + 0.24f * Mathf.Cos(u * 14f) * Smooth(0.3f, 0.95f, v);
        float line = Mathf.Min(Mathf.Abs(v - 0.86f), Mathf.Abs(v - 0.96f));
        c = Over(c, Gold, Smooth(0.009f, 0.005f, line));
        float sx = Mathf.Repeat(u * 10f, 1f) - 0.5f, sy = (v - 0.91f) / 0.03f;
        c = Over(c, Gold, Star4(sx / 0.32f, sy));
        return Over(c, CoatDark, Smooth(0.975f, 0.995f, v));
    }

    private static Color PaintApron(float u, float v)
    {
        Color c = Grain(Skirt, u, v, 60f, 0.14f);
        c *= 0.85f + 0.3f * Mathf.Cos(u * 30f) * Smooth(0.9f, 0.2f, v);    // tablas de la falda
        c = Over(c, Gold, Smooth(0.012f, 0.008f, Mathf.Abs(v - 0.06f)));
        return Over(c, SweaterDark, 0.4f * Smooth(0.85f, 1f, v));
    }

    private static Color PaintBoot(float u, float v)
    {
        Color c = Grain(Leather, u, v, 40f, 0.18f);
        c = Over(c, LeatherDark, 0.4f * Smooth(0.35f, 0.47f, Mathf.Abs(u - 0.5f)));
        return Over(c, new Color(0.08f, 0.05f, 0.035f, 0.1f), Smooth(0.12f, 0.08f, v));
    }

    private static Color PaintBelt(float u, float v)
    {
        Color c = Grain(Leather, u * 6f, v, 40f, 0.45f);
        float edge = Mathf.Abs(v - 0.5f) * 2f;
        c = Over(c, LeatherDark, Smooth(0.75f, 1f, edge));
        float stitch = Smooth(0.08f, 0.04f, Mathf.Abs(edge - 0.68f)) * Smooth(0.28f, 0.2f, Mathf.Abs(Mathf.Repeat(u * 150f, 1f) - 0.5f));
        return Over(c, new Color(0.78f, 0.58f, 0.32f, 0.3f), stitch);
    }

    // El vidrio de los frascos: la mitad izquierda verde, la derecha morada. Va aparte del oro: Kit.Disc y Kit.Gem
    // pintan con el medio de su región, y cuando compartían una, las hebillas salían verdes.
    private static Color PaintGlass(float u, float v)
    {
        Color glass = u < 0.5f ? Potion : PotionPurple;
        return Color.Lerp(glass * 0.55f, glass * 1.15f, v);
    }

    private static Color PaintBand(float u, float v)
    {
        Color c = Grain(Plum, u, v, 50f, 0.15f);
        return Over(c, Gold, Smooth(0.14f, 0.1f, Mathf.Min(v, 1f - v)));
    }

    private static Color PaintFeather(float u, float v)
    {
        float x = Mathf.Abs(u - 0.5f);
        Color c = Color.Lerp(FeatherTone * 0.6f, FeatherTone * 1.2f, Smooth(0f, 0.09f, x));
        c = Over(c, new Color(0.05f, 0.25f, 0.45f, 0.2f), 0.5f * Smooth(0.5f, 1f, v));
        c *= 0.85f + 0.2f * (0.5f + 0.5f * Mathf.Sin(v * 60f + x * 300f));
        return Over(c, new Color(0.92f, 0.9f, 0.82f, 0.3f), Smooth(0.012f, 0.006f, x));   // el cañón
    }

    // La mitad izquierda son las tapas de cuero rojo; la derecha, las hojas.
    private static Color PaintBook(float u, float v)
    {
        if (u < 0.5f) return Grain(BookRed, u, v, 40f, 0.2f);
        return Pages * (0.88f + 0.12f * Mathf.PerlinNoise(u * 4f, v * 90f));
    }

    // Las solapas: la cara de afuera es del abrigo y los dos cantos son de oro (ver Sweep: la cara va de 1/6 a 2/6).
    private static Color PaintTrim(float u, float v)
    {
        float face = Smooth(0.13f, 0.17f, u) * Smooth(0.37f, 0.33f, u);
        return Over(GoldAt(0.6f), CoatCloth(u * 4f, v), face);
    }
}
