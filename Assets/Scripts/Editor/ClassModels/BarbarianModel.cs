using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ============================================================
// BarbarianModel
//
// La receta del modelo de relleno de la Bárbara (la referencia es la guerrera del hacha que eligió
// Gustavo el 6 de noviembre de 2026): pelo largo naranja con trenzas, manto de piel, corsé de cuero,
// falda con abertura, botas con vendas y pintura de guerra azul. Sin el hacha: el arma es de la clase.
//
// Mercenarios ▸ Modelos de clase ▸ Generar la Bárbara: rehace malla, textura y prefab.
// ============================================================
public sealed class BarbarianModel : ClassModelRecipe
{
    private const string Name = "Barbarian";
    private const string ArtFolder = "Assets/Art/Models/Classes/Barbarian";
    private const string PrefabFolder = "Assets/Prefabs/Player/Classes";

    // ---------- Colores ----------
    private static readonly Color SkinTone     = new Color(0.90f, 0.60f, 0.43f);
    private static readonly Color SkinShade    = new Color(0.62f, 0.32f, 0.22f);
    private static readonly Color HairRoot     = new Color(0.50f, 0.13f, 0.03f);
    private static readonly Color HairMid      = new Color(0.90f, 0.33f, 0.05f);
    private static readonly Color HairTip      = new Color(1.00f, 0.52f, 0.12f);
    private static readonly Color FurRoot      = new Color(0.30f, 0.16f, 0.08f);
    private static readonly Color FurMid       = new Color(0.62f, 0.37f, 0.18f);
    private static readonly Color FurTip       = new Color(0.90f, 0.68f, 0.40f);
    private static readonly Color Leather      = new Color(0.38f, 0.20f, 0.10f);
    private static readonly Color LeatherDark  = new Color(0.17f, 0.10f, 0.07f);
    private static readonly Color LeatherLight = new Color(0.58f, 0.34f, 0.17f);
    private static readonly Color Charcoal     = new Color(0.10f, 0.10f, 0.13f);
    private static readonly Color Sleeve       = new Color(0.08f, 0.26f, 0.60f);
    private static readonly Color CapeTone     = new Color(0.46f, 0.18f, 0.09f);
    private static readonly Color Teal         = new Color(0.03f, 0.48f, 0.55f);
    private static readonly Color WarPaint     = new Color(0.05f, 0.33f, 0.85f);
    private static readonly Color Silver       = new Color(0.72f, 0.74f, 0.80f);
    private static readonly Color GemTone      = new Color(0.05f, 0.80f, 0.95f);

    // ---------- Medidas del torso: alto → medio ancho, media profundidad, centro en Z ----------
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

    // ---------- Brazo y pierna: distancia desde el nacimiento → radio ----------
    private static readonly ModelKeys ArmRadius = new ModelKeys(
        -0.05f, 0.040f, 0f, 0.055f, 0.06f, 0.058f, 0.12f, 0.050f, 0.18f, 0.043f, 0.234f, 0.038f, 0.27f, 0.042f,
        0.32f, 0.040f, 0.38f, 0.032f, 0.43f, 0.026f, 0.445f, 0.025f);

    private static readonly ModelKeys LegRadius = new ModelKeys(
        -0.07f, 0.082f, 0f, 0.090f, 0.08f, 0.092f, 0.16f, 0.085f, 0.26f, 0.072f, 0.35f, 0.060f, 0.41f, 0.056f,
        0.46f, 0.055f, 0.53f, 0.060f, 0.62f, 0.053f, 0.72f, 0.043f, 0.79f, 0.040f, 0.82f, 0.041f);

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
        0f, 0.79f, 20f, 0.76f, 40f, 0.70f, 60f, 0.62f, 90f, 0.42f, 120f, 0.25f, 180f, 0.12f);

    // El borde de la falda: en punta adelante, con la abertura sobre la pierna izquierda.
    private static readonly ModelKeys SkirtHem = new ModelKeys(
        -180f, 0.60f, -140f, 0.60f, -115f, 0.72f, -96f, 0.90f, -74f, 0.92f, -52f, 0.80f, -30f, 0.64f, -12f, 0.53f,
        4f, 0.47f, 20f, 0.54f, 44f, 0.60f, 90f, 0.59f, 140f, 0.60f, 180f, 0.60f);

    private readonly ModelRegion _head, _torso, _arm, _leg, _hair, _fur, _skirt, _cape, _under, _belt, _boot, _silver, _gem, _skin, _lining;

    protected override ModelRegion HairRegion => _hair;

    [MenuItem("Mercenarios/Modelos de clase/Generar la Bárbara")]
    public static void Generate()
    {
        var model = new BarbarianModel();
        model.Build();
    }

    private BarbarianModel() : base(4811)
    {
        _head   = Atlas.Region(0, 1024, 1024, 1024);
        _torso  = Atlas.Region(1024, 1024, 1024, 1024);
        _arm    = Atlas.Region(0, 0, 512, 1024);
        _leg    = Atlas.Region(512, 0, 512, 1024);
        _hair   = Atlas.Region(1024, 0, 256, 1024);
        _fur    = Atlas.Region(1280, 512, 256, 512);
        _skirt  = Atlas.Region(1536, 512, 256, 512);
        _cape   = Atlas.Region(1792, 512, 256, 512);
        _under  = Atlas.Region(1280, 256, 256, 256);
        _boot   = Atlas.Region(1536, 256, 256, 256);
        _silver = Atlas.Region(1792, 384, 128, 128);
        _gem    = Atlas.Region(1920, 384, 128, 128);
        _skin   = Atlas.Region(1792, 256, 128, 128);
        _belt   = Atlas.Region(1280, 128, 768, 128);
        _lining = Atlas.Region(1920, 256, 128, 128);
    }

    private void Build()
    {
        PaintAtlas();

        BuildTorso();
        BuildHead();
        foreach (float side in new[] { 1f, -1f })
        {
            BuildArm(side);
            BuildHand(side);
            BuildLeg(side);
            BuildBoot(side);
        }

        BuildCape();
        BuildSkirt();
        BuildBelts();
        BuildMantle();
        BuildJewelry();
        BuildHair();

        Kit.Save(Name, ArtFolder, PrefabFolder, Atlas);
    }

    // =========================================================
    // CUERPO
    // =========================================================

    private static Vector3 TorsoDirection(float deg)
    {
        float a = deg * Mathf.Deg2Rad;
        float s = Mathf.Sin(a), c = Mathf.Cos(a);
        const float round = 2f / 2.5f;
        return new Vector3(Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), round), 0f, Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), round));
    }

    private static float Corset(float y) => 0.007f * Smooth(1.075f, 1.09f, y) * (1f - Smooth(1.265f, 1.28f, y));

    private static Vector3 TorsoPoint(float y, float deg, float offset)
    {
        Vector3 d = TorsoDirection(deg);
        float width = TorsoWidth.At(y) + offset;
        float depth = TorsoDepth.At(y) + offset;
        float bust = 0.036f * Bell(y, 1.345f, 0.05f) * (Bell(deg, 32f, 24f) + Bell(deg, -32f, 24f));
        return new Vector3(d.x * (width + bust), y, TorsoCenter.At(y) + d.z * (depth + bust));
    }

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

    private const float TorsoBottom = 0.845f;
    private const float TorsoTop = 1.62f;

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
                Vector3 p = TorsoPoint(y, (u - 0.5f) * 360f, Corset(y));
                rings[r][i] = new ModelPoint(p, _torso.Uv(u, Mathf.InverseLerp(TorsoBottom, TorsoTop, y)), TorsoSkin(p));
            }
        }
        Kit.Loft(rings, capStart: true);
    }

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

    private const float FaceLeft = -0.11f, FaceRight = 0.11f, FaceBottom = 1.54f, FaceTop = 1.83f;

    private Vector2 HeadUv(Vector3 p) =>
        _head.Uv(Mathf.InverseLerp(FaceLeft, FaceRight, p.x), Mathf.InverseLerp(FaceBottom, FaceTop, p.y));

    private ModelSkin ArmSkin(string side, float d)
    {
        ModelSkin skin = Kit.Bone("B-shoulder" + side).To(Kit.Bone("B-upperArm" + side), Smooth(-0.04f, 0.03f, d));
        skin = skin.To(Kit.Bone("B-forearm" + side), Smooth(0.205f, 0.26f, d));
        return skin.To(Kit.Bone("B-hand" + side), Smooth(0.415f, 0.44f, d));
    }

    private static float ArmBulge(float d) =>
        ArmRadius.At(d)
        + 0.004f * Smooth(0.055f, 0.06f, d) * (1f - Smooth(0.125f, 0.13f, d))     // brazalete
        + 0.007f * Smooth(0.262f, 0.27f, d) * (1f - Smooth(0.415f, 0.422f, d));   // brazal

    private void BuildArm(float side)
    {
        Kit.BeginPiece(55f);
        string name = SideName(side);
        Vector3[] chain = ArmChain(name);

        float[] stops =
        {
            -0.05f, -0.02f, 0.01f, 0.04f, 0.055f, 0.06f, 0.09f, 0.125f, 0.13f, 0.16f, 0.19f, 0.215f, 0.234f, 0.25f, 0.262f,
            0.27f, 0.30f, 0.33f, 0.36f, 0.39f, 0.415f, 0.422f, 0.43f, 0.445f,
        };
        const int columns = 10;

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
                rings[r][i] = new ModelPoint(p, _arm.Uv(u, Mathf.InverseLerp(-0.05f, 0.445f, d)), ArmSkin(name, d));
            }
        }
        Kit.Loft(rings);

        FurCuff(chain, 0.262f, 12, 0.042f, 0.022f, ArmBulge, d => ArmSkin(name, d), 1f, 0.5f);
        FurCuff(chain, 0.43f, 12, 0.03f, 0.018f, ArmBulge, d => ArmSkin(name, d), -1f, 0.5f);
        FurCuff(chain, 0.056f, 12, 0.03f, 0.02f, ArmBulge, d => ArmSkin(name, d), 1f, 0.4f);
    }

    // Una vuelta de matas alrededor de un miembro. along: hacia dónde del camino apuntan (+1 sigue el
    // camino, -1 vuelve).
    private void FurCuff(Vector3[] chain, float distance, int count, float length, float width,
                         Func<float, float> radius, Func<float, ModelSkin> skin, float along, float flare)
    {
        Kit.BeginPiece(30f);
        AlongChain(chain, distance, out Vector3 center, out Vector3 tangent);
        Vector3 reference = Vector3.ProjectOnPlane(Vector3.forward, tangent).normalized;
        Vector3 other = Vector3.Cross(tangent, reference);
        float r = radius(distance);

        for (int i = 0; i < count; i++)
        {
            float a = (i + Range(-0.2f, 0.2f)) / count * Mathf.PI * 2f;
            Vector3 radial = reference * Mathf.Cos(a) + other * Mathf.Sin(a);
            Vector3 root = center + radial * (r + 0.001f) - tangent * (along * length * 0.35f);
            Vector3 direction = (tangent * along + radial * flare).normalized;
            Kit.Tuft(root, direction, radial, length * Range(0.85f, 1.25f), width, 0.009f, 0.25f, _fur, Range(0.15f, 0.85f), skin(distance));
        }
    }

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

        Kit.BeginPiece(55f);
        float[] along = { -0.01f, 0.3f, 0.7f, 1f, 1.1f };
        float[] widths = { 0.028f, 0.047f, 0.059f, 0.062f, 0.054f };
        float[] thick = { 0.024f, 0.025f, 0.023f, 0.019f, 0.014f };
        const int columns = 10;

        var rings = new ModelPoint[along.Length][];
        for (int r = 0; r < along.Length; r++)
        {
            rings[r] = new ModelPoint[columns];
            for (int i = 0; i < columns; i++)
            {
                float a = i / (float)columns * Mathf.PI * 2f;
                Vector3 p = wrist + axis * (along[r] * length) + wide * (Mathf.Cos(a) * widths[r]) + flat * (Mathf.Sin(a) * thick[r]);
                rings[r][i] = new ModelPoint(p, _skin.Uv(0.5f, 0.5f), hand);
            }
        }
        Kit.Loft(rings, wrap: true, capEnd: true);

        foreach (string finger in new[] { "B-indexFinger", "B-middleFinger", "B-ringFinger", "B-pinky", "B-thumb" })
            BuildFinger(finger, name, finger == "B-thumb" ? 0.0178f : 0.0148f, 6, (t, a) => _skin.Uv(0.5f, 0.5f));
    }

    private ModelSkin LegSkin(string side, float d)
    {
        ModelSkin skin = Hips.To(Kit.Bone("B-thigh" + side), Smooth(-0.06f, 0.03f, d));
        skin = skin.To(Kit.Bone("B-shin" + side), Smooth(0.38f, 0.44f, d));
        return skin.To(Kit.Bone("B-foot" + side), Smooth(0.77f, 0.81f, d));
    }

    private const float BootTop = 0.445f;

    private static float LegBulge(float d) => LegRadius.At(d) + 0.008f * Smooth(BootTop - 0.004f, BootTop + 0.004f, d);

    private void BuildLeg(float side)
    {
        Kit.BeginPiece(55f);
        string name = SideName(side);
        Vector3[] chain = LegChain(name);

        float[] stops =
        {
            -0.07f, -0.03f, 0.01f, 0.05f, 0.1f, 0.15f, 0.2f, 0.25f, 0.3f, 0.35f, 0.38f, 0.41f, 0.44f, 0.45f, 0.47f,
            0.5f, 0.54f, 0.58f, 0.62f, 0.66f, 0.7f, 0.74f, 0.78f, 0.82f,
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

        FurCuff(chain, BootTop + 0.012f, 15, 0.05f, 0.024f, LegBulge, d => LegSkin(name, d), -1f, 0.55f);
        FurCuff(chain, 0.77f, 14, 0.04f, 0.022f, LegBulge, d => LegSkin(name, d), 1f, 0.8f);
    }

    private void BuildBoot(float side)
    {
        Kit.BeginPiece(50f);
        string name = SideName(side);
        ModelSkin foot = Kit.Bone("B-foot" + name);
        ModelSkin toe = Kit.Bone("B-toe" + name);
        float x = Kit.Pos("B-foot" + name).x;

        float[] z      = { -0.094f, -0.084f, -0.05f, 0.0f, 0.05f, 0.095f, 0.14f, 0.185f, 0.214f, 0.232f };
        float[] half   = { 0.024f, 0.039f, 0.046f, 0.048f, 0.049f, 0.051f, 0.053f, 0.046f, 0.031f, 0.011f };
        float[] bottom = { 0.014f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.004f, 0.012f };
        float[] top    = { 0.085f, 0.115f, 0.15f, 0.165f, 0.13f, 0.098f, 0.082f, 0.064f, 0.046f, 0.03f };
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
                rings[r][i] = new ModelPoint(p, _boot.Uv(0.5f + 0.45f * c, Mathf.Lerp(Mathf.InverseLerp(-0.1f, 0.25f, z[r]) * 0.8f + 0.2f, 0.05f, sole)), skin);
            }
        }
        Kit.Loft(rings, wrap: true, capStart: true, capEnd: true);
    }

    // =========================================================
    // ROPA
    // =========================================================

    private ModelSkin LowerBackSkin(float y)
    {
        ModelSkin skin = Chest.To(Spine, Smooth(1.32f, 1.14f, y));
        return skin.To(Hips, Smooth(1.14f, 0.98f, y));
    }

    private void BuildCape()
    {
        Kit.BeginPiece(28f);
        const int columns = 14, rows = 9;
        const float top = 1.485f, bottom = 0.72f;

        var hem = new float[columns + 1];
        for (int i = 0; i <= columns; i++) hem[i] = Range(-0.045f, 0.03f);

        var rings = new ModelPoint[rows + 1][];
        for (int r = 0; r <= rows; r++)
        {
            float t = r / (float)rows;
            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float u = i / (float)columns;
                float s = u * 2f - 1f;
                float y = Mathf.Lerp(top, bottom, t) + (r == rows ? hem[i] : 0f);
                float width = Mathf.Lerp(0.20f, 0.27f, t);
                float fold = Mathf.Sin(s * 8.5f + 0.7f) * 0.032f * Smooth(0.05f, 0.7f, t);
                float z = -0.135f - 0.105f * t * t + s * s * Mathf.Lerp(0.07f, 0.045f, t) + fold;
                var p = new Vector3(s * width, y, z);
                rings[r][i] = new ModelPoint(p, _cape.Uv(u, 1f - t), LowerBackSkin(y));
            }
        }
        Kit.Sheet(rings, _lining.Uv(0.25f, 0.5f));
    }

    private static Vector3 SkirtPoint(float deg, float t, float offset)
    {
        float top = 1.065f;
        float y = Mathf.Lerp(top, SkirtHem.At(Wrap(deg)), t);
        Vector3 d = TorsoDirection(deg);
        Vector3 p = TorsoPoint(Mathf.Max(y, 0.95f), deg, 0.012f + offset);
        float flare = Mathf.Max(0f, 0.95f - y) * 0.42f + Mathf.Sin(deg * 0.175f) * 0.022f * t;
        p += new Vector3(d.x, 0f, d.z) * flare;
        p.y = y;
        return p;
    }

    private ModelSkin SkirtSkin(Vector3 p, float t)
    {
        float reach = Mathf.Min(1f, Mathf.Pow(t, 0.8f) * 1.1f) * 0.95f * Smooth(1.03f, 0.84f, p.y);
        float right = Smooth(-0.05f, 0.05f, p.x);
        return Hips.To(Kit.Bone("B-thigh.R"), reach * right).To(Kit.Bone("B-thigh.L"), reach * (1f - right) / Mathf.Max(0.001f, 1f - reach * right));
    }

    private void BuildSkirt()
    {
        const int columns = 44, rows = 7;

        Kit.BeginPiece(26f);
        var under = new ModelPoint[rows + 1][];
        var over = new ModelPoint[rows + 1][];
        for (int r = 0; r <= rows; r++)
        {
            float t = r / (float)rows;
            under[r] = new ModelPoint[columns + 1];
            over[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float u = i / (float)columns;
                float deg = (u - 0.5f) * 360f;

                Vector3 inner = SkirtPoint(deg, t * 1.09f, -0.006f);
                under[r][i] = new ModelPoint(inner, _under.Uv(u, 1f - t), SkirtSkin(inner, t));

                Vector3 p = SkirtPoint(deg, t, 0f);
                over[r][i] = new ModelPoint(p, _skirt.Uv(u, 1f - t), SkirtSkin(p, t));
            }
        }
        Kit.Sheet(under, _lining.Uv(0.75f, 0.5f));
        Kit.Sheet(over, _lining.Uv(0.75f, 0.5f));

        // Piel en el borde delantero
        Kit.BeginPiece(30f);
        for (float deg = -66f; deg <= 54f; deg += 4.2f)
        {
            float jitter = deg + Range(-1f, 1f);
            Vector3 p = SkirtPoint(jitter, 0.985f, 0.006f);
            Vector3 along = (SkirtPoint(jitter + 2f, 1f, 0f) - SkirtPoint(jitter - 2f, 1f, 0f)).normalized;
            Vector3 radial = TorsoDirection(jitter).normalized;
            Vector3 direction = (Vector3.down + radial * 0.45f + along * Range(-0.25f, 0.25f)).normalized;
            Kit.Tuft(p, direction, radial, Range(0.05f, 0.07f), 0.024f, 0.010f, 0.2f, _fur, Range(0.2f, 0.8f), SkirtSkin(p, 1f));
        }
    }

    private void Band(Func<float, float> height, float tall, float offset, float crease)
    {
        Kit.BeginPiece(crease);
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
                Vector3 p = TorsoPoint(Mathf.Max(y, 0.96f), deg, offset + lift[r] * 0.006f + Corset(y));
                p.y = y;
                rings[r][i] = new ModelPoint(p, _belt.Uv(u, 0.5f + rows[r]), TorsoSkin(p));
            }
        }
        Kit.Loft(rings);
    }

    private void BuildBelts()
    {
        // La cinta del corsé y el cinturón caído sobre la cadera
        Band(deg => 1.083f, 0.03f, 0.004f, 40f);
        Func<float, float> hipBelt = deg => 1.035f + 0.026f * Mathf.Cos((deg - 50f) * Mathf.Deg2Rad);
        Band(hipBelt, 0.05f, 0.022f, 40f);

        Kit.BeginPiece(35f);
        const float buckleDeg = 10f;
        Vector3 buckle = TorsoPoint(hipBelt(buckleDeg), buckleDeg, 0.03f);
        Kit.Disc(buckle, Vector3.forward, Vector3.up, 0.040f, 0.036f, 0.012f, 4, _silver, Hips);

        // Tiras que cuelgan del cinturón, cada una con su remate de metal
        foreach (float deg in new[] { -24f, -6f, 26f, 44f })
        {
            float end = Range(0.72f, 0.82f);
            var path = new Vector3[6];
            for (int i = 0; i < path.Length; i++)
            {
                float t = Mathf.Lerp(0.02f, (1.05f - end) / (1.065f - SkirtHem.At(deg)), i / (float)(path.Length - 1));
                path[i] = SkirtPoint(deg, t, 0.012f);
            }

            Kit.BeginPiece(25f);
            Kit.Sweep(path, p => TorsoDirection(deg), t => new Vector2(0.016f, 0.0035f), 4,
                (t, a) => _belt.Uv(0.1f + 0.4f * t, a < 0.5f ? 0.3f : 0.7f),
                (p, t) => SkirtSkin(p, t * 0.5f));

            Vector3 tipUp = (path[path.Length - 2] - path[path.Length - 1]).normalized;
            Vector3 tipOut = TorsoDirection(deg).normalized;
            Kit.Gem(path[path.Length - 1], tipUp, tipOut, 0.026f, 0.018f, 0.006f, 6, _silver, SkirtSkin(path[path.Length - 1], 0.5f));
        }

        // Los escudos de la cadera y los tachones de adelante
        Kit.BeginPiece(30f);
        foreach (float deg in new[] { 68f, -68f })
        {
            Vector3 p = TorsoPoint(1.0f, deg, 0.04f);
            Kit.Disc(p, TorsoDirection(deg).normalized, Vector3.up, 0.044f, 0.060f, 0.016f, 10, _silver, Hips);
        }
        Kit.Disc(SkirtPoint(-12f, 0.27f, 0.018f), TorsoDirection(-12f).normalized, Vector3.up, 0.028f, 0.028f, 0.012f, 9, _silver, Hips);
        Kit.Disc(SkirtPoint(-12f, 0.43f, 0.018f), TorsoDirection(-12f).normalized, Vector3.up, 0.023f, 0.023f, 0.010f, 9, _silver, Hips);
    }

    // =========================================================
    // MANTO DE PIEL
    // =========================================================

    private static Vector3 Mantle(float deg, float s)
    {
        float a = deg * Mathf.Deg2Rad;
        float sin = Mathf.Sin(a), cos = Mathf.Cos(a);
        float rx = Mathf.Lerp(0.085f, 0.325f, s);
        float rz = Mathf.Lerp(0.080f, cos > 0f ? 0.170f : 0.205f, s);

        float abs = Mathf.Abs(Wrap(deg));
        float y = 1.56f - 0.03f * s - 0.17f * Mathf.Pow(s, 5f);
        y -= 0.05f * Bell(abs, 40f, 28f) * Smooth(0.25f, 1f, s);    // las solapas caen por el pecho
        y -= 0.05f * Smooth(120f, 180f, abs) * s;                  // y atrás baja sobre la capa
        return new Vector3(sin * rx, y, cos * rz);
    }

    private ModelSkin MantleSkin(Vector3 p)
    {
        string side = p.x > 0f ? ".R" : ".L";
        return Chest.To(Kit.Bone("B-upperArm" + side), 0.35f * Smooth(0.24f, 0.33f, Mathf.Abs(p.x)));
    }

    private static float MantleOpening(float s) => Mathf.Lerp(16f, 36f, s);

    private void BuildMantle()
    {
        // La base: tapa lo que los mechones dejen ver
        Kit.BeginPiece(40f);
        const int columns = 30;
        float[] stops = { 0f, 0.2f, 0.4f, 0.6f, 0.8f, 0.95f };
        var rings = new ModelPoint[stops.Length][];
        for (int r = 0; r < stops.Length; r++)
        {
            float s = stops[r];
            float open = MantleOpening(s) + 2f;
            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float deg = Mathf.Lerp(open, 360f - open, i / (float)columns);
                Vector3 p = Mantle(deg, s);
                rings[r][i] = new ModelPoint(p, _fur.Uv(0.5f, 0.1f), MantleSkin(p));
            }
        }
        Kit.Sheet(rings, _fur.Uv(0.5f, 0.05f));

        // Las matas, de afuera hacia adentro: cada fila tapa la raíz de la anterior
        Kit.BeginPiece(48f);
        float[] rows = { 0.98f, 0.84f, 0.70f, 0.56f, 0.42f, 0.28f, 0.14f };
        for (int r = 0; r < rows.Length; r++)
        {
            float s = rows[r];
            int count = Mathf.RoundToInt(Mathf.Lerp(18f, 40f, s));
            float open = MantleOpening(s) + 4f;
            for (int i = 0; i < count; i++)
            {
                float deg = Mathf.Lerp(open, 360f - open, (i + 0.5f + Range(-0.3f, 0.3f)) / count);
                float jitter = s + Range(-0.03f, 0.03f);
                Vector3 p = Mantle(deg, jitter);
                Vector3 outward = (Mantle(deg, jitter + 0.06f) - p).normalized;
                Vector3 across = (Mantle(deg + 3f, jitter) - p).normalized;
                Vector3 normal = Vector3.Cross(across, outward).normalized;
                if (normal.y < 0f) normal = -normal;

                Vector3 direction = (outward + Vector3.down * Mathf.Lerp(0.15f, 0.65f, s) + across * Range(-0.35f, 0.35f)).normalized;
                float length = Mathf.Lerp(0.07f, 0.105f, s) * Range(0.8f, 1.25f);
                float width = Mathf.Lerp(0.024f, 0.034f, s);
                Kit.Tuft(p + normal * 0.012f, direction, normal, length, width, 0.016f, 0.3f, _fur, Range(0.12f, 0.88f), MantleSkin(p));
            }
        }
    }

    private void BuildJewelry()
    {
        // Collar de cuentas en V y la gema del pecho
        Kit.BeginPiece(30f);
        float gemY = 1.402f;
        Vector3 gem = TorsoPoint(gemY, 0f, 0.012f);
        for (int i = 0; i < 2; i++)
        {
            float sign = i == 0 ? 1f : -1f;
            for (float t = 0.08f; t <= 1f; t += 0.115f)
            {
                float deg = sign * Mathf.Lerp(0f, 38f, t);
                float y = Mathf.Lerp(gemY + 0.022f, 1.50f, t * t);
                Vector3 p = TorsoPoint(y, deg, 0.006f);
                Kit.Gem(p, Vector3.up, TorsoDirection(deg).normalized, 0.009f, 0.008f, 0.005f, 5, _silver, Chest);
            }
        }

        Kit.Disc(gem + Vector3.back * 0.004f, Vector3.forward, Vector3.up, 0.024f, 0.033f, 0.006f, 8, _silver, Chest);
        Kit.Gem(gem + Vector3.forward * 0.008f, Vector3.up, Vector3.forward, 0.030f, 0.015f, 0.009f, 4, _gem, Chest);
    }

    // =========================================================
    // PELO
    // =========================================================

    private void BuildHair()
    {
        BuildHairCap();
        BuildBackHair();
        BuildSideHair();
        BuildBangs();
        Braid(1f);
        Braid(-1f);
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
                float line = Hairline.At(Mathf.Abs(deg));
                float h = Mathf.Lerp(line, 1f, t);
                float swell = 0.009f + 0.012f * Mathf.PerlinNoise(u * 13f, t * 3.1f) * Smooth(0f, 0.15f, t);
                Vector3 p = HeadPoint(h, deg, swell);
                float strip = 0.12f + 0.76f * Mathf.Repeat(u * 5f, 1f);
                rings[r][i] = new ModelPoint(p, _hair.Uv(strip, Mathf.Lerp(0.34f, 0.05f, t)), HeadBone);
            }
        }
        Kit.Loft(rings);
    }

    private void BuildBackHair()
    {
        // Debajo: los largos, hasta la cintura
        const int deep = 15;
        for (int i = 0; i < deep; i++)
        {
            float s = i / (float)(deep - 1) * 2f - 1f;
            float deg = 180f + s * 96f;
            Vector3 root = HeadPoint(Range(0.5f, 0.78f), deg, 0.002f);
            Vector3 out1 = Outward(root);
            var tip = new Vector3(s * 0.215f + Range(-0.02f, 0.02f), Range(1.0f, 1.12f) + Mathf.Abs(s) * 0.1f, -0.215f + Range(-0.01f, 0.01f) + s * s * 0.04f);
            Lock(root, root + out1 * 0.06f + Vector3.down * 0.05f,
                new Vector3(s * 0.235f, 1.46f, -0.285f + s * s * 0.03f), tip, Range(0.05f, 0.062f), Range(0.018f, 0.024f), 9);
        }

        // Encima: más cortos, para que se vean capas
        const int top = 11;
        for (int i = 0; i < top; i++)
        {
            float s = (i + 0.5f) / top * 2f - 1f;
            float deg = 180f + s * 88f;
            Vector3 root = HeadPoint(Range(0.7f, 0.9f), deg, 0.006f);
            Vector3 out1 = Outward(root);
            var tip = new Vector3(s * 0.2f + Range(-0.02f, 0.02f), Range(1.2f, 1.34f), -0.25f + s * s * 0.04f);
            Lock(root, root + out1 * 0.07f + Vector3.down * 0.04f,
                new Vector3(s * 0.215f, 1.5f, -0.31f + s * s * 0.03f), tip, Range(0.044f, 0.056f), Range(0.016f, 0.02f), 9);
        }
    }

    private void BuildSideHair()
    {
        foreach (float side in new[] { 1f, -1f })
        {
            // Por delante del hombro, sobre el manto
            for (int i = 0; i < 5; i++)
            {
                float deg = side * Mathf.Lerp(66f, 112f, i / 4f);
                Vector3 root = HeadPoint(Range(0.6f, 0.74f), deg, 0.004f);
                float reach = Range(1.33f, 1.42f);
                var tip = new Vector3(side * (0.125f + 0.022f * i), reach, 0.20f - 0.055f * i);
                Lock(root, root + Outward(root) * 0.03f + Vector3.down * 0.06f,
                    new Vector3(side * (0.098f + 0.016f * i), 1.52f, 0.13f - 0.05f * i), tip, Range(0.028f, 0.036f), Range(0.012f, 0.015f), 9);
            }
        }
    }

    private void BuildBangs()
    {
        // La raya va un poco corrida a su derecha: de ahí salen los mechones hacia cada lado
        const float part = 8f;
        for (int i = 0; i < 9; i++)
        {
            bool right = i >= 6;
            float s = right ? (i - 6) / 2f : i / 5f;
            float side = right ? 1f : -1f;

            float startDeg = part + side * Mathf.Lerp(3f, 22f, s);
            float startH = Mathf.Lerp(0.95f, 0.86f, s);
            Vector3 root = HeadPoint(startH, startDeg, 0.015f);

            float endDeg = side * Mathf.Lerp(52f, 88f, s);
            float endH = Mathf.Lerp(0.56f, 0.40f, s) + Range(-0.03f, 0.03f);
            Vector3 mid = HeadPoint(0.80f, Mathf.Lerp(startDeg, endDeg, 0.55f), 0.03f);
            Vector3 tip = HeadPoint(endH, endDeg, 0.012f);

            Lock(root, mid, HeadPoint(Mathf.Lerp(0.8f, endH, 0.5f), endDeg, 0.02f), tip, Range(0.024f, 0.03f), 0.011f, 8);
        }
    }

    private void Braid(float side)
    {
        Vector3 start = HeadPoint(0.5f, side * 74f, 0.012f);
        Vector3 end = new Vector3(side * 0.086f, 1.385f, 0.205f);
        Vector3 control1 = start + Vector3.down * 0.05f + Vector3.forward * 0.01f + Vector3.right * (side * 0.015f);
        Vector3 control2 = new Vector3(side * 0.098f, 1.47f, 0.14f);

        const int lumps = 17;
        for (int i = 0; i < lumps; i++)
        {
            float t = i / (float)(lumps - 1);
            Vector3 p = Bezier(start, control1, control2, end, t);
            Vector3 next = Bezier(start, control1, control2, end, Mathf.Min(1f, t + 0.08f));
            Vector3 along = (next - p).normalized;
            Vector3 wide = Vector3.Cross(along, Vector3.forward).normalized;
            float size = Mathf.Lerp(0.017f, 0.011f, t);
            Vector3 tilt = (along + wide * (i % 2 == 0 ? 0.45f : -0.45f)).normalized;

            Kit.BeginPiece(45f);
            Kit.Lump(p + wide * (i % 2 == 0 ? 0.0035f : -0.0035f), tilt, Vector3.forward, size * 1.5f, size, size * 0.85f, 6, _hair, 0.15f + 0.04f * i, HairSkin(p));
        }

        // Atadura y la punta suelta
        Kit.BeginPiece(30f);
        Kit.Gem(end + Vector3.down * 0.012f, Vector3.up, Vector3.forward, 0.010f, 0.013f, 0.012f, 6, _belt, HairSkin(end));
        for (int i = 0; i < 3; i++)
        {
            Vector3 direction = (Vector3.down + Vector3.right * ((i - 1) * 0.3f)).normalized;
            Kit.Tuft(end + Vector3.down * 0.018f, direction, Vector3.forward, 0.05f, 0.011f, 0.006f, 0.1f, _hair, 0.3f + i * 0.2f, HairSkin(end));
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
        Atlas.Paint(_fur, PaintFur);
        Atlas.Paint(_skirt, PaintSkirt);
        Atlas.Paint(_cape, PaintCape);
        Atlas.Paint(_under, PaintUnder);
        Atlas.Paint(_belt, PaintBelt);
        Atlas.Paint(_boot, PaintBootFoot);
        Atlas.Paint(_silver, (u, v) => Color.Lerp(Silver * 0.55f, Silver * 1.2f, v));
        Atlas.Paint(_gem, (u, v) => Color.Lerp(GemTone * 0.55f, GemTone * 1.15f, v * 0.6f + 0.4f * u));
        Atlas.Paint(_skin, (u, v) => SkinTone);
        Atlas.Paint(_lining, (u, v) => u < 0.5f ? CapeTone * 0.5f : Charcoal * 0.6f);
    }

    private static Color PaintHead(float u, float v)
    {
        float x = Mathf.Lerp(FaceLeft, FaceRight, u);
        float y = Mathf.Lerp(FaceBottom, FaceTop, v);
        float ax = Mathf.Abs(x);

        Color c = Grain(SkinTone, u, v, 24f, 0.05f);

        c = Over(c, SkinShade, 0.45f * Smooth(0.056f, 0.085f, ax));
        c = Over(c, SkinShade, 0.25f * Smooth(1.578f, 1.558f, y));
        c = Over(c, new Color(0.93f, 0.40f, 0.30f), 0.28f * Bell(ax, 0.047f, 0.02f) * Bell(y, 1.648f, 0.016f));

        // Sombras de la cara
        c = Over(c, SkinShade, 0.5f * Bell(ax, 0.036f, 0.024f) * Bell(y, 1.692f, 0.012f));
        c = Over(c, SkinShade, 0.16f * Bell(ax, 0.010f, 0.004f) * Bell(y, 1.66f, 0.02f));
        c = Over(c, SkinShade, 0.6f * Bell(x, 0f, 0.0095f) * Bell(y, 1.6325f, 0.003f));
        c = Over(c, SkinShade, 0.4f * Bell(x, 0f, 0.02f) * Bell(y, 1.585f, 0.005f));

        // Pintura de guerra: la banda con el rombo de la frente y las rayas bajo los ojos
        float band = Smooth(0.0045f, 0.003f, Mathf.Abs(y - 1.733f)) * Smooth(0.066f, 0.060f, ax);
        float zig = Smooth(0.0045f, 0.003f, Mathf.Abs(y - 1.722f - 0.006f * Mathf.Abs(Mathf.Repeat(ax * 90f, 2f) - 1f))) * Smooth(0.058f, 0.052f, ax);
        float diamond = Smooth(0.0185f, 0.0165f, ax + Mathf.Abs(y - 1.727f) * 0.72f);
        float hole = Smooth(0.0065f, 0.0055f, ax + Mathf.Abs(y - 1.727f) * 0.72f);
        c = Over(c, WarPaint, Mathf.Max(Mathf.Max(band, zig), diamond * (1f - hole)));

        foreach (float stripe in new[] { 0.026f, 0.039f })
        {
            float length = stripe < 0.03f ? 1.640f : 1.628f;
            float width = 0.0048f * Smooth(length, 1.668f, y);
            float mask = Smooth(width + 0.001f, width, Mathf.Abs(ax - stripe - (1.672f - y) * 0.1f)) * Smooth(1.676f, 1.672f, y) * Smooth(length, length + 0.002f, y);
            c = Over(c, WarPaint, mask);
        }

        // Labios
        float lipY = y - 1.608f - 2.2f * ax * ax;
        float lipHalf = 0.0205f;
        float cupid = 0.0012f * Bell(ax, 0.005f, 0.004f);
        float upper = Smooth(0f, 0.001f, lipY) * Smooth(0.0078f + cupid - 0.0065f * ax / lipHalf, 0.0068f + cupid - 0.0065f * ax / lipHalf, lipY);
        float lower = Smooth(0f, -0.001f, lipY) * Smooth(-0.0105f + 0.0095f * ax / lipHalf, -0.0095f + 0.0095f * ax / lipHalf, lipY);
        float inside = Smooth(lipHalf, lipHalf - 0.0015f, ax);
        c = Over(c, new Color(0.72f, 0.16f, 0.18f), upper * inside);
        c = Over(c, new Color(0.86f, 0.28f, 0.27f), lower * inside);
        c = Over(c, new Color(0.97f, 0.55f, 0.50f), 0.6f * Bell(ax, 0.004f, 0.006f) * Bell(lipY, -0.0052f, 0.0014f));
        c = Over(c, new Color(0.26f, 0.06f, 0.07f), Smooth(0.0011f, 0.0004f, Mathf.Abs(lipY)) * inside);

        // Ojos: un poco alzados por fuera, para que mire con cara de pelea
        float ex = (ax - 0.0362f) / 0.0195f;
        float ey = (y - 1.6835f - (ax - 0.0362f) * 0.2f) / 0.0096f;
        float open = 1f - ex * ex;
        if (open > 0f)
        {
            float lid = Mathf.Sqrt(open);
            float eye = Smooth(lid * 1.02f, lid * 0.92f, Mathf.Abs(ey));
            float ix = (ax - 0.0352f) / 0.0088f;
            float iy = (y - 1.6838f) / 0.0088f;
            float iris = Mathf.Sqrt(ix * ix + iy * iy);

            Color ball = new Color(0.95f, 0.93f, 0.90f);
            ball = Over(ball, Color.Lerp(new Color(0.04f, 0.44f, 0.86f), new Color(0.02f, 0.14f, 0.42f), iris), Smooth(1.05f, 0.95f, iris));
            ball = Over(ball, new Color(0.02f, 0.02f, 0.05f), Smooth(0.52f, 0.44f, iris));
            ball = Over(ball, Color.white, Smooth(0.28f, 0.2f, Vector2.Distance(new Vector2(ix, iy), new Vector2(-0.3f, 0.35f))));
            c = Over(c, ball, eye);
        }

        // Pestañas de arriba y línea de abajo
        float upperLine = Mathf.Abs(ey - Mathf.Sqrt(Mathf.Max(0f, open)));
        c = Over(c, new Color(0.13f, 0.05f, 0.04f), Smooth(0.46f, 0.2f, upperLine) * Smooth(1.3f, 1.05f, Mathf.Abs(ex)) * Smooth(-0.3f, 0.1f, ey));

        // Cejas
        float bx = ax - 0.0145f;
        float browY = 1.7005f + bx * 0.5f - bx * bx * 5.5f;
        float browThick = Mathf.Lerp(0.0034f, 0.0011f, Smooth(0.0f, 0.045f, bx));
        float brow = Smooth(browThick + 0.001f, browThick, Mathf.Abs(y - browY)) * Smooth(0.0f, 0.003f, bx) * Smooth(0.047f, 0.042f, bx);
        return Over(c, new Color(0.42f, 0.12f, 0.04f), brow);
    }

    private static Color PaintTorso(float u, float v)
    {
        float deg = (u - 0.5f) * 360f;
        float abs = Mathf.Abs(deg);
        float y = Mathf.Lerp(TorsoBottom, TorsoTop, v);

        // El vestido oscuro
        Color c = Grain(Charcoal, u * 3f, v, 120f, 0.5f);
        float folds = Mathf.PerlinNoise(u * 22f, v * 2.2f) - 0.5f;
        c *= 1f + folds * 0.9f;
        c = Over(c, new Color(0.17f, 0.17f, 0.22f), 0.4f * Bell(y, 1.345f, 0.045f) * (Bell(deg, 32f, 16f) + Bell(deg, -32f, 16f)));

        // Escote en V y cuello
        float neckline = Mathf.Lerp(0f, 27f, Mathf.InverseLerp(1.375f, 1.50f, y));
        float skinMask = Mathf.Max(Smooth(neckline + 0.8f, neckline - 0.8f, abs) * Smooth(1.373f, 1.379f, y), Smooth(1.497f, 1.503f, y));
        Color skin = Over(Grain(SkinTone, u * 3f, v, 30f, 0.05f), SkinShade, 0.4f * Smooth(1.47f, 1.40f, y) * Smooth(14f, 2f, abs));
        skin = Over(skin, SkinShade, 0.55f * Smooth(1.585f, 1.545f, y) * Smooth(1.495f, 1.53f, y));
        c = Over(c, skin, skinMask);

        // Ribete del escote
        float edge = Mathf.Abs(abs - neckline);
        float trim = Smooth(2.4f, 1.8f, edge) * Smooth(1.373f, 1.38f, y) * Smooth(1.502f, 1.496f, y);
        c = Over(c, LeatherLight, trim);
        c = Over(c, Over(LeatherDark, LeatherLight, 0.3f), Smooth(0.8f, 0.4f, edge) * trim);

        // Corsé
        const float corsetBottom = 1.078f, corsetTop = 1.272f;
        float corset = Smooth(corsetBottom - 0.002f, corsetBottom + 0.002f, y) * Smooth(corsetTop + 0.002f, corsetTop - 0.002f, y);
        if (corset > 0f)
        {
            Color leather = Grain(Leather, u * 3f, v, 90f, 0.5f);
            leather *= 1f + (Mathf.PerlinNoise(u * 40f, v * 26f) - 0.5f) * 0.3f;

            // Las correas anchas y sus costuras
            float strap = Mathf.Repeat((y - corsetBottom) / 0.0485f, 1f);
            leather = Over(leather, LeatherDark, Smooth(0.12f, 0.0f, strap) + Smooth(0.88f, 1f, strap) * 0.8f);
            float stitch = Smooth(0.09f, 0.05f, Mathf.Abs(strap - 0.2f)) * Smooth(0.25f, 0.2f, Mathf.Abs(Mathf.Repeat(deg / 3.2f, 1f) - 0.5f));
            leather = Over(leather, LeatherLight * 1.35f, stitch);
            stitch = Smooth(0.09f, 0.05f, Mathf.Abs(strap - 0.8f)) * Smooth(0.25f, 0.2f, Mathf.Abs(Mathf.Repeat(deg / 3.2f, 1f) - 0.5f));
            leather = Over(leather, LeatherLight * 1.35f, stitch);

            // Hebillas al frente y borde oscuro
            leather = Over(leather, LeatherDark, 0.7f * Smooth(1.8f, 1.2f, Mathf.Abs(abs - 40f)));
            float buckle = Smooth(9f, 8.2f, abs) * Smooth(0.34f, 0.3f, Mathf.Abs(strap - 0.5f));
            float inner = Smooth(5.6f, 4.8f, abs) * Smooth(0.2f, 0.16f, Mathf.Abs(strap - 0.5f));
            leather = Over(leather, Silver, buckle * (1f - inner));
            leather = Over(leather, LeatherDark, buckle * inner);
            leather = Over(leather, Silver * 1.1f, Smooth(0.08f, 0.05f, Mathf.Abs(strap - 0.5f)) * Smooth(7f, 6f, abs));

            c = Over(c, leather, corset);
            c = Over(c, LeatherDark * 0.7f, Smooth(0.004f, 0.0f, Mathf.Abs(y - corsetTop)));
        }

        // Sombra del manto sobre el pecho
        c = Over(c, new Color(0.04f, 0.03f, 0.05f), 0.35f * Smooth(1.40f, 1.47f, y) * Smooth(10f, 40f, abs) * Smooth(1.52f, 1.5f, y));
        return c;
    }

    private static Color PaintArm(float u, float v)
    {
        float d = Mathf.Lerp(-0.05f, 0.445f, v);
        float deg = (u - 0.5f) * 360f;

        Color c = Grain(Sleeve, u * 2f, v, 70f, 0.5f);
        c *= 1f + (Mathf.PerlinNoise(u * 7f, v * 9f) - 0.5f) * 0.8f;
        c = Over(c, Sleeve * 0.45f, Smooth(0.14f, 0.0f, d) * 0.5f);

        // Brazalete de arriba
        float band = Smooth(0.054f, 0.058f, d) * Smooth(0.128f, 0.124f, d);
        if (band > 0f)
        {
            Color leather = Grain(LeatherDark, u * 3f, v, 60f, 0.6f);
            leather = Over(leather, Leather, Smooth(0.008f, 0.004f, Mathf.Abs(d - 0.09f)));
            float rivet = Smooth(0.0052f, 0.0038f, Mathf.Sqrt(Sq(d - 0.09f) + Sq(Mathf.Repeat(deg, 45f) / 45f * 0.26f - 0.13f) ));
            leather = Over(leather, Silver, rivet);
            c = Over(c, leather, band);
        }

        // Brazal del antebrazo
        float bracer = Smooth(0.260f, 0.266f, d) * Smooth(0.424f, 0.418f, d);
        if (bracer > 0f)
        {
            Color leather = Grain(LeatherDark, u * 3f, v, 80f, 0.6f);
            float line = Mathf.Min(Mathf.Abs(d - 0.285f), Mathf.Abs(d - 0.398f));
            leather = Over(leather, LeatherLight, Smooth(0.0052f, 0.003f, line));

            // Tiras cruzadas sobre el cuero
            float cross1 = Mathf.Repeat(deg / 72f + d / 0.05f, 1f);
            float cross2 = Mathf.Repeat(deg / 72f - d / 0.05f, 1f);
            float cross = Mathf.Max(Smooth(0.16f, 0.1f, Mathf.Abs(cross1 - 0.5f)), Smooth(0.16f, 0.1f, Mathf.Abs(cross2 - 0.5f)));
            leather = Over(leather, Leather * 1.2f, cross * Smooth(0.292f, 0.298f, d) * Smooth(0.392f, 0.386f, d));
            c = Over(c, leather, bracer);
        }

        // Muñeca y mano
        float bare = Smooth(0.423f, 0.427f, d);
        c = Over(c, SkinTone, bare);

        // El antebrazo entre la manga y el brazal
        return c;
    }

    private static Color PaintLeg(float u, float v)
    {
        float d = Mathf.Lerp(-0.07f, 0.82f, v);
        float deg = (u - 0.5f) * 360f;

        Color c = Grain(SkinTone, u * 2f, v, 26f, 0.05f);
        c = Over(c, SkinShade, 0.55f * Smooth(0.13f, -0.02f, d));
        c = Over(c, SkinShade, 0.25f * Bell(Mathf.Abs(deg), 180f, 70f));
        c = Over(c, SkinShade, 0.32f * Bell(d, 0.435f, 0.02f));
        c = Over(c, SkinShade, 0.22f * Bell(d, 0.375f, 0.012f) * Bell(deg, 0f, 60f));

        // Bota
        float boot = Smooth(BootTop - 0.003f, BootTop + 0.003f, d);
        if (boot > 0f)
        {
            Color leather = Grain(LeatherDark, u * 2f, v, 70f, 0.7f);
            leather *= 1f + (Mathf.PerlinNoise(u * 5f, v * 30f) - 0.5f) * 0.5f;

            // Vendas: dos espirales que se cruzan
            float pitch = d / 0.092f;
            float wrapA = Mathf.Abs(Mathf.Repeat(deg / 360f + pitch, 1f) - 0.5f);
            float wrapB = Mathf.Abs(Mathf.Repeat(-deg / 360f + pitch, 1f) - 0.5f);
            float strapA = Smooth(0.17f, 0.13f, wrapA);
            float strapB = Smooth(0.17f, 0.13f, wrapB);
            Color strapColor = Grain(new Color(0.46f, 0.25f, 0.12f), u * 2f, v, 45f, 0.5f);
            leather = Over(leather, LeatherDark * 0.5f, Smooth(0.21f, 0.17f, wrapB));
            leather = Over(leather, strapColor * 0.86f, strapB);
            leather = Over(leather, LeatherDark * 0.5f, Smooth(0.21f, 0.17f, wrapA) * 0.8f);
            leather = Over(leather, strapColor, strapA);
            c = Over(c, leather, boot);
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
        c *= 0.72f + 0.42f * strands + 0.2f * (fine - 0.5f);
        c = Over(c, new Color(1f, 0.70f, 0.28f), 0.5f * Smooth(0.68f, 0.82f, fine) * Bell(v, 0.5f, 0.3f));
        return c;
    }

    private static Color PaintFur(float u, float v)
    {
        Color c = v < 0.32f
            ? Color.Lerp(FurRoot, FurMid, Smooth(0.0f, 0.32f, v))
            : Color.Lerp(FurMid, FurTip, Smooth(0.45f, 0.95f, v));

        float hairs = Mathf.PerlinNoise(u * 60f, v * 3f);
        c *= 0.86f + 0.3f * hairs;
        return Over(c, FurMid * 0.8f, 0.4f * Smooth(0.55f, 0.72f, Mathf.PerlinNoise(u * 5f + 3f, 2.2f)));
    }

    private static Color PaintSkirt(float u, float v)
    {
        Color c = Grain(Charcoal, u, v, 160f, 0.45f);
        c *= 1f + (Mathf.PerlinNoise(u * 38f, v * 1.4f) - 0.5f) * 1.1f;
        c = Over(c, new Color(0.03f, 0.03f, 0.05f), 0.5f * Smooth(0.6f, 1f, v));
        c = Over(c, LeatherLight, Smooth(0.03f, 0.024f, Mathf.Abs(v - 0.05f)) * 0.8f);
        return c;
    }

    private static Color PaintCape(float u, float v)
    {
        Color c = Grain(CapeTone, u, v, 90f, 0.3f);
        c *= 0.75f + 0.7f * Mathf.PerlinNoise(u * 16f, v * 1.2f + 4f);
        c = Over(c, CapeTone * 0.4f, 0.5f * Smooth(0.25f, 0f, v));
        c = Over(c, CapeTone * 0.5f, 0.6f * Smooth(0.8f, 1f, v));

        // La guarda de abajo: dos líneas y un zigzag
        float line = Mathf.Min(Mathf.Abs(v - 0.13f), Mathf.Abs(v - 0.235f));
        float zig = Mathf.Abs(v - 0.1825f - 0.035f * (Mathf.Abs(Mathf.Repeat(u * 18f, 2f) - 1f) * 2f - 1f));
        c = Over(c, new Color(0.86f, 0.56f, 0.20f), Mathf.Max(Smooth(0.0085f, 0.0055f, line), Smooth(0.0105f, 0.0075f, zig)));
        return c;
    }

    private static Color PaintUnder(float u, float v)
    {
        Color c = Grain(Teal, u, v, 90f, 0.3f);
        c = Over(c, FurTip, Smooth(0.03f, 0.022f, Mathf.Abs(v - 0.1f)));
        return Over(c, new Color(0.04f, 0.04f, 0.06f), Smooth(0.16f, 0.26f, v));
    }

    private static Color PaintBelt(float u, float v)
    {
        Color c = Grain(Leather, u * 6f, v, 40f, 0.45f);
        float edge = Mathf.Abs(v - 0.5f) * 2f;
        c = Over(c, LeatherDark, Smooth(0.75f, 1f, edge));
        float stitch = Smooth(0.08f, 0.04f, Mathf.Abs(edge - 0.68f)) * Smooth(0.28f, 0.2f, Mathf.Abs(Mathf.Repeat(u * 150f, 1f) - 0.5f));
        return Over(c, LeatherLight * 1.4f, stitch);
    }

    private static Color PaintBootFoot(float u, float v)
    {
        Color c = Grain(LeatherDark, u, v, 60f, 0.6f);
        c = Over(c, new Color(0.08f, 0.05f, 0.04f), Smooth(0.14f, 0.08f, v));
        return Over(c, Leather, 0.6f * Smooth(0.03f, 0.015f, Mathf.Abs(v - 0.62f)));
    }
}
