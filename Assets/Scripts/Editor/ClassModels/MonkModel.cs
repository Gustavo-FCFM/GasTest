using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ============================================================
// MonkModel
//
// La receta del modelo de relleno del Monje (referencias elegidas por Gustavo el 9 de noviembre de 2026):
// un yuan-ti con cara humana. Piel amarillenta con escamas de serpiente, más marcadas en las sienes,
// alrededor de los ojos, en el cuello y en los antebrazos; ojos amarillos de pupila rasgada y pelo blanco
// despeinado. Gi negro abierto en el pecho con solapas ocre, faja ocre con nudo y puntas, mangas anchas
// hasta el codo, faldones con ribete, un collar de cuentas grandes, una hombrera de bronce, vendas en
// antebrazos, manos y espinillas, y zapatos de tela. Sin cola y sin armas: el Monje pelea a puño limpio.
//
// El acabado es Matte: el Shinobi se hace invisible (ver CLAUDE.md).
//
// Mercenarios ▸ Modelos de clase ▸ Generar el Monje: rehace malla, textura y prefab.
// ============================================================
public sealed class MonkModel : ClassModelRecipe
{
    private const string Name = "Monk";
    private const string ArtFolder = "Assets/Art/Models/Classes/Monk";
    private const string PrefabFolder = "Assets/Prefabs/Player/Classes";

    // ---------- Colores ----------
    private static readonly Color SkinTone    = new Color(0.860f, 0.680f, 0.400f);
    private static readonly Color SkinShade   = new Color(0.560f, 0.380f, 0.180f);
    private static readonly Color ScaleLine   = new Color(0.400f, 0.270f, 0.090f);
    private static readonly Color Blotch      = new Color(0.520f, 0.380f, 0.120f);
    private static readonly Color Robe        = new Color(0.045f, 0.042f, 0.050f);
    private static readonly Color RobeSheen   = new Color(0.150f, 0.140f, 0.150f);
    private static readonly Color Ochre       = new Color(0.880f, 0.580f, 0.090f);
    private static readonly Color OchreDark   = new Color(0.500f, 0.290f, 0.040f);
    private static readonly Color Bandage     = new Color(0.880f, 0.840f, 0.740f);
    private static readonly Color BandageDark = new Color(0.580f, 0.530f, 0.440f);
    private static readonly Color Strap       = new Color(0.300f, 0.170f, 0.080f);
    private static readonly Color Wood        = new Color(0.110f, 0.065f, 0.045f);
    private static readonly Color WoodLight   = new Color(0.340f, 0.210f, 0.130f);
    private static readonly Color Bronze      = new Color(0.500f, 0.340f, 0.160f);
    private static readonly Color BronzeDark  = new Color(0.180f, 0.110f, 0.050f);
    private static readonly Color BronzeLight = new Color(0.930f, 0.720f, 0.360f);
    private static readonly Color Cord        = new Color(0.640f, 0.090f, 0.060f);
    private static readonly Color HairWhite   = new Color(0.940f, 0.940f, 0.920f);
    private static readonly Color HairShade   = new Color(0.620f, 0.620f, 0.630f);
    private static readonly Color Cloth       = new Color(0.080f, 0.070f, 0.070f);

    // ---------- Torso: alto → medio ancho, media profundidad, centro en Z ----------
    private static readonly ModelKeys TorsoWidth = new ModelKeys(
        0.845f, 0.06f, 0.90f, 0.140f, 0.95f, 0.162f, 1.00f, 0.162f, 1.05f, 0.150f, 1.09f, 0.140f, 1.13f, 0.138f,
        1.19f, 0.148f, 1.25f, 0.160f, 1.31f, 0.172f, 1.37f, 0.180f, 1.42f, 0.182f, 1.46f, 0.176f, 1.49f, 0.138f,
        1.515f, 0.085f, 1.54f, 0.055f, 1.57f, 0.052f, 1.62f, 0.048f);

    private static readonly ModelKeys TorsoDepth = new ModelKeys(
        0.845f, 0.06f, 0.90f, 0.098f, 0.95f, 0.108f, 1.00f, 0.108f, 1.05f, 0.100f, 1.09f, 0.094f, 1.13f, 0.094f,
        1.19f, 0.102f, 1.25f, 0.112f, 1.31f, 0.122f, 1.37f, 0.126f, 1.42f, 0.118f, 1.46f, 0.102f, 1.49f, 0.084f,
        1.515f, 0.068f, 1.54f, 0.056f, 1.57f, 0.053f, 1.62f, 0.050f);

    private static readonly ModelKeys TorsoCenter = new ModelKeys(
        0.845f, 0.015f, 0.95f, 0f, 1.05f, 0.01f, 1.13f, 0.02f, 1.25f, 0.032f, 1.37f, 0.04f, 1.46f, 0.02f,
        1.515f, 0.004f, 1.62f, 0.016f);

    private const float TorsoBottom = 0.845f;
    private const float TorsoTop = 1.62f;

    // ---------- Brazo y pierna: distancia desde el nacimiento → radio ----------
    private static readonly ModelKeys ArmRadius = new ModelKeys(
        -0.05f, 0.044f, 0f, 0.058f, 0.06f, 0.062f, 0.12f, 0.056f, 0.18f, 0.049f, 0.234f, 0.045f, 0.27f, 0.049f,
        0.32f, 0.046f, 0.38f, 0.038f, 0.43f, 0.031f, 0.446f, 0.031f);

    // El pantalón es ancho y se mete en las vendas de la espinilla.
    private static readonly ModelKeys LegRadius = new ModelKeys(
        -0.07f, 0.095f, 0f, 0.104f, 0.08f, 0.108f, 0.16f, 0.104f, 0.26f, 0.096f, 0.35f, 0.090f, 0.41f, 0.086f,
        0.45f, 0.080f, 0.475f, 0.060f, 0.53f, 0.060f, 0.62f, 0.055f, 0.72f, 0.047f, 0.79f, 0.044f, 0.82f, 0.044f);

    private const float SleeveEnd = 0.232f, WrapStart = 0.318f, ShinWrap = 0.468f;
    private const float SkirtHem = 0.70f, SkirtFlare = 0.30f;

    // ---------- Cabeza: alto de 0 (mentón) a 1 (coronilla) ----------
    private const float HeadBottom = 1.555f;
    private const float HeadHeight = 0.255f;

    private static readonly ModelKeys HeadWidth = new ModelKeys(
        0f, 0.020f, 0.06f, 0.040f, 0.12f, 0.054f, 0.2f, 0.063f, 0.35f, 0.071f, 0.5f, 0.077f, 0.7f, 0.080f, 0.85f, 0.067f,
        0.95f, 0.040f, 1f, 0.005f);

    private static readonly ModelKeys HeadDepth = new ModelKeys(
        0f, 0.022f, 0.1f, 0.042f, 0.22f, 0.060f, 0.35f, 0.080f, 0.5f, 0.096f, 0.7f, 0.102f, 0.85f, 0.088f,
        0.95f, 0.054f, 1f, 0.006f);

    private static readonly ModelKeys HeadOffset = new ModelKeys(
        0f, 0.058f, 0.1f, 0.048f, 0.22f, 0.034f, 0.35f, 0.022f, 0.5f, 0.012f, 0.7f, 0.005f, 0.85f, 0f, 1f, 0f);

    private static readonly ModelKeys Hairline = new ModelKeys(
        0f, 0.84f, 25f, 0.82f, 45f, 0.74f, 70f, 0.62f, 95f, 0.46f, 130f, 0.30f, 180f, 0.22f);

    private const float FaceLeft = -0.11f, FaceRight = 0.11f, FaceBottom = 1.54f, FaceTop = 1.83f;

    // El gi se abre en V hasta la faja; la solapa mide eso de ancho en el pecho (°).
    private const float LapelDeg = 11f;

    private readonly ModelRegion _head, _torso, _arm, _leg, _hair, _sash, _robe, _bandage, _bead, _bronze, _shoe,
                                 _skinHand, _lining, _cord, _sleeve;

    protected override ModelRegion HairRegion => _hair;

    [MenuItem("Mercenarios/Modelos de clase/Generar el Monje")]
    public static void Generate()
    {
        var model = new MonkModel();
        model.Build();
    }

    private MonkModel() : base(9142)
    {
        _head     = Atlas.Region(0, 1024, 1024, 1024);
        _torso    = Atlas.Region(1024, 1024, 1024, 1024);
        _arm      = Atlas.Region(0, 0, 512, 1024);
        _leg      = Atlas.Region(512, 0, 512, 1024);
        _hair     = Atlas.Region(1024, 0, 256, 1024);
        _sash     = Atlas.Region(1280, 512, 256, 512);
        _robe     = Atlas.Region(1536, 512, 256, 512);
        _bandage  = Atlas.Region(1792, 512, 256, 512);
        _bead     = Atlas.Region(1280, 256, 256, 256);
        _bronze   = Atlas.Region(1536, 256, 256, 256);
        _shoe     = Atlas.Region(1792, 256, 256, 256);
        _skinHand = Atlas.Region(1280, 0, 256, 128);
        _lining   = Atlas.Region(1536, 0, 128, 128);
        _cord     = Atlas.Region(1664, 0, 128, 128);
        _sleeve   = Atlas.Region(1792, 0, 256, 128);
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
            BuildShoe(side);
        }

        BuildSkirt();
        BuildSash();
        BuildLapels();
        BuildPauldron(-1f);
        BuildBeads();
        BuildHair();

        Kit.Save(Name, ArtFolder, PrefabFolder, Atlas, ModelFinish.Matte);
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

    private static Vector3 TorsoPoint(float y, float deg, float offset)
    {
        Vector3 d = TorsoDirection(deg);
        float pecs = 0.012f * Bell(y, 1.355f, 0.05f) * (Bell(deg, 27f, 20f) + Bell(deg, -27f, 20f));
        return new Vector3(d.x * (TorsoWidth.At(y) + offset + pecs), y, TorsoCenter.At(y) + d.z * (TorsoDepth.At(y) + offset + pecs));
    }

    // Hacia afuera del eje del cuerpo, a esa altura.
    private static Vector3 Radial(Vector3 p) => new Vector3(p.x, 0f, p.z - TorsoCenter.At(p.y)).normalized;

    private ModelSkin TorsoSkin(Vector3 p)
    {
        ModelSkin skin = Hips.To(Spine, Smooth(1.0f, 1.1f, p.y));
        skin = skin.To(Chest, Smooth(1.14f, 1.24f, p.y));
        skin = skin.To(Neck, Smooth(1.52f, 1.55f, p.y));
        skin = skin.To(HeadBone, Smooth(1.58f, 1.6f, p.y));

        string side = p.x > 0f ? ".R" : ".L";
        float shoulder = Smooth(0.13f, 0.19f, Mathf.Abs(p.x)) * Smooth(1.38f, 1.43f, p.y);
        skin = skin.To(Kit.Bone("B-shoulder" + side), shoulder * 0.4f);

        float thigh = Smooth(0.95f, 0.86f, p.y) * Smooth(0.02f, 0.09f, Mathf.Abs(p.x));
        return skin.To(Kit.Bone("B-thigh" + side), thigh * 0.6f);
    }

    private void BuildTorso()
    {
        Kit.BeginPiece(55f);

        float[] heights =
        {
            0.845f, 0.87f, 0.90f, 0.95f, 1.0f, 1.05f, 1.09f, 1.13f, 1.19f, 1.25f, 1.31f, 1.34f, 1.37f, 1.42f, 1.46f, 1.49f,
            1.515f, 1.54f, 1.57f, 1.62f,
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

    // El escote del gi, en grados desde el frente: casi cerrado en la faja, abierto en el pecho.
    private static float OpenDeg(float y) => Mathf.Lerp(2.5f, 31f, Smooth(1.10f, 1.47f, y));

    // Hasta dónde sube el cuello del gi: más alto atrás.
    private static float CollarTop(float deg) => 1.535f - 0.045f * Smooth(110f, 30f, Mathf.Abs(Wrap(deg)));

    // Las solapas: una tira de cada lado, del nudo de la faja por el borde del escote hasta la nuca.
    private void BuildLapels()
    {
        foreach (float side in new[] { 1f, -1f })
        {
            const int steps = 28;
            var path = new Vector3[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float s = i / (float)steps;
                float y, deg;
                if (s < 0.6f)
                {
                    y = Mathf.Lerp(1.10f, 1.47f, s / 0.6f);
                    deg = OpenDeg(y) + LapelDeg * 0.5f;
                }
                else
                {
                    float t = (s - 0.6f) / 0.4f;
                    deg = Mathf.Lerp(OpenDeg(1.47f) + LapelDeg * 0.5f, 180f, Smooth(0f, 1f, t));
                    y = Mathf.Lerp(1.47f, 1.527f, Mathf.Sin(t * Mathf.PI * 0.5f));
                }
                path[i] = TorsoPoint(y, side * deg, 0.006f);
            }

            Kit.BeginPiece(40f);
            Kit.Sweep(path, Radial, t => new Vector2(Mathf.Lerp(0.017f, 0.012f, Smooth(0.6f, 1f, t)), 0.0045f), 6,
                (t, a) => _sash.Uv(0.1f + 0.35f * t, a < 0.5f ? 0.8f : 0.62f),
                (p, t) => TorsoSkin(p), capStart: true, capEnd: true);
        }
    }

    // =========================================================
    // CABEZA
    // =========================================================

    private static Vector3 HeadPoint(float h, float deg, float offset)
    {
        float a = deg * Mathf.Deg2Rad;
        float s = Mathf.Sin(a), c = Mathf.Cos(a);
        const float round = 2f / 2.25f;
        float x = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), round) * HeadWidth.At(h);
        float z = HeadOffset.At(h) + Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), round) * HeadDepth.At(h);

        float abs = Mathf.Abs(deg);
        if (offset <= 0f && abs < 95f)
        {
            z += 0.018f * Bell(deg, 0f, 7.5f) * Bell(h, 0.35f, 0.055f);                      // nariz
            z += 0.007f * Bell(deg, 0f, 6f) * Smooth(0.36f, 0.42f, h) * (1f - Smooth(0.5f, 0.57f, h));
            z += 0.010f * Bell(h, 0.57f, 0.035f) * Bell(deg, 0f, 42f);                       // ceño
            z -= 0.009f * Bell(abs, 21f, 13f) * Bell(h, 0.5f, 0.04f);                        // cuenca del ojo
            z += 0.004f * Bell(deg, 0f, 16f) * Bell(h, 0.21f, 0.03f);                        // labios
            z += 0.005f * Bell(deg, 0f, 20f) * Bell(h, 0.06f, 0.04f);                        // mentón
            x *= 1f + 0.06f * Bell(h, 0.38f, 0.05f);                                         // pómulos
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
    // BRAZOS, MANGAS Y MANOS
    // =========================================================

    private ModelSkin ArmSkin(string side, float d)
    {
        ModelSkin skin = Kit.Bone("B-shoulder" + side).To(Kit.Bone("B-upperArm" + side), Smooth(-0.04f, 0.03f, d));
        skin = skin.To(Kit.Bone("B-forearm" + side), Smooth(0.205f, 0.26f, d));
        return skin.To(Kit.Bone("B-hand" + side), Smooth(0.415f, 0.44f, d));
    }

    private static float ArmBulge(float d) => ArmRadius.At(d) + 0.004f * Smooth(WrapStart - 0.003f, WrapStart, d);

    private void BuildArm(float side)
    {
        Kit.BeginPiece(50f);
        string name = SideName(side);
        Vector3[] chain = ArmChain(name);

        float[] stops =
        {
            -0.05f, -0.02f, 0.01f, 0.05f, 0.09f, 0.13f, 0.17f, 0.205f, 0.234f, 0.25f, 0.27f, 0.29f, 0.315f, 0.318f, 0.34f,
            0.37f, 0.40f, 0.42f, 0.435f, 0.446f,
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
    }

    // La manga ancha del gi: nace en el hombro, se abre hasta el codo y abajo cuelga un poco. Sigue al
    // brazo de arriba; el antebrazo entra y sale de ella al doblar el codo.
    private void BuildSleeve(float side)
    {
        Kit.BeginPiece(30f);
        string name = SideName(side);
        Vector3[] chain = ArmChain(name);
        float[] stops = { -0.05f, -0.01f, 0.04f, 0.09f, 0.14f, 0.19f, 0.215f, SleeveEnd };
        const int columns = 16;

        var rings = new ModelPoint[stops.Length][];
        for (int r = 0; r < stops.Length; r++)
        {
            float d = stops[r];
            AlongChain(chain, d, out Vector3 center, out Vector3 tangent);
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, tangent).normalized;
            Vector3 front = Vector3.Cross(tangent, up).normalized * side;
            float t = Mathf.InverseLerp(-0.05f, SleeveEnd, d);
            float radius = ArmRadius.At(d) + Mathf.Lerp(0.012f, 0.040f, Smooth(0.1f, 1f, t));

            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float u = i / (float)columns;
                float a = (u - 0.5f) * Mathf.PI * 2f;
                float c = Mathf.Cos(a);
                float sag = c < 0f ? 1f + 0.35f * Smooth(0.2f, 1f, t) * -c : 1f;
                Vector3 p = center + up * (c * radius * sag) + front * (Mathf.Sin(a) * radius * 0.96f);
                rings[r][i] = new ModelPoint(p, _sleeve.Uv(u, t), ArmSkin(name, Mathf.Min(d, 0.18f)));
            }
        }

        AlongChain(chain, 0.1f, out Vector3 inside, out _);
        Kit.Sheet(rings, _lining.Uv(0.5f, 0.5f), inside);
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

        // La palma, vendada como la de un peleador
        Kit.BeginPiece(60f);
        float[] along = { -0.05f, 0.3f, 0.7f, 1f, 1.1f };
        float[] widths = { 0.030f, 0.047f, 0.059f, 0.061f, 0.053f };
        float[] thick = { 0.026f, 0.026f, 0.024f, 0.020f, 0.015f };
        const int columns = 10;

        var rings = new ModelPoint[along.Length][];
        for (int r = 0; r < along.Length; r++)
        {
            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float a = i / (float)columns * Mathf.PI * 2f;
                Vector3 p = wrist + axis * (along[r] * length) + wide * (Mathf.Cos(a) * widths[r]) + flat * (Mathf.Sin(a) * thick[r]);
                rings[r][i] = new ModelPoint(p, _bandage.Uv(i / (float)columns, 0.1f + 0.8f * r / (along.Length - 1f)), hand);
            }
        }
        Kit.Loft(rings, capEnd: true);

        foreach (string finger in new[] { "B-indexFinger", "B-middleFinger", "B-ringFinger", "B-pinky", "B-thumb" })
        {
            Kit.BeginPiece(60f);
            bool thumb = finger == "B-thumb";
            BuildFinger(finger, name, thumb ? 0.0188f : 0.0158f, 6, (t, a) => _skinHand.Uv(a, t), thumb ? 0.7f : 0.82f, 0.55f);
        }
    }

    // =========================================================
    // PIERNAS Y ZAPATOS
    // =========================================================

    private ModelSkin LegSkin(string side, float d)
    {
        ModelSkin skin = Hips.To(Kit.Bone("B-thigh" + side), Smooth(-0.06f, 0.03f, d));
        skin = skin.To(Kit.Bone("B-shin" + side), Smooth(0.38f, 0.44f, d));
        return skin.To(Kit.Bone("B-foot" + side), Smooth(0.77f, 0.81f, d));
    }

    private void BuildLeg(float side)
    {
        Kit.BeginPiece(50f);
        string name = SideName(side);
        Vector3[] chain = LegChain(name);

        float[] stops =
        {
            -0.07f, -0.03f, 0.01f, 0.05f, 0.1f, 0.15f, 0.2f, 0.25f, 0.3f, 0.35f, 0.39f, 0.42f, 0.445f, 0.46f, 0.468f, 0.475f,
            0.5f, 0.54f, 0.58f, 0.62f, 0.66f, 0.7f, 0.74f, 0.78f, 0.82f,
        };
        const int columns = 14;

        var rings = new ModelPoint[stops.Length][];
        for (int r = 0; r < stops.Length; r++)
        {
            float d = stops[r];
            AlongChain(chain, d, out Vector3 center, out Vector3 tangent);
            Vector3 front = Vector3.ProjectOnPlane(Vector3.forward, tangent).normalized;
            Vector3 outer = Vector3.Cross(front, tangent).normalized;
            if (Mathf.Sign(outer.x) != Mathf.Sign(side)) outer = -outer;

            center += front * (0.008f * Bell(d, 0.41f, 0.03f) - 0.012f * Bell(d, 0.54f, 0.09f));
            float radius = LegRadius.At(d);

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

    private void BuildShoe(float side)
    {
        Kit.BeginPiece(50f);
        string name = SideName(side);
        ModelSkin foot = Kit.Bone("B-foot" + name);
        ModelSkin toe = Kit.Bone("B-toe" + name);
        float x = Kit.Pos("B-foot" + name).x;

        float[] z      = { -0.098f, -0.086f, -0.05f, 0.0f, 0.05f, 0.10f, 0.15f, 0.195f, 0.222f, 0.238f };
        float[] half   = { 0.026f, 0.041f, 0.048f, 0.050f, 0.051f, 0.054f, 0.056f, 0.048f, 0.032f, 0.012f };
        float[] bottom = { 0.014f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.004f, 0.012f };
        float[] top    = { 0.082f, 0.112f, 0.146f, 0.156f, 0.122f, 0.092f, 0.076f, 0.060f, 0.044f, 0.028f };
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
                float v = Mathf.Lerp(0.2f + 0.78f * Mathf.InverseLerp(-0.1f, 0.24f, z[r]), 0.05f, sole);
                rings[r][i] = new ModelPoint(p, _shoe.Uv(0.5f + 0.45f * c, v), skin);
            }
        }
        Kit.Loft(rings, wrap: true, capStart: true, capEnd: true);
    }

    // =========================================================
    // FALDONES, FAJA Y COLLAR
    // =========================================================

    private static Vector3 SkirtPoint(float y, float deg, float offset, float flare)
    {
        Vector3 d = TorsoDirection(deg);
        Vector3 p = TorsoPoint(Mathf.Max(y, 0.97f), deg, offset);
        p += d * (Mathf.Max(0f, 0.97f - y) * flare);
        p.y = y;
        return p;
    }

    // Cada mitad acompaña a su pierna: así una patada no atraviesa el faldón.
    private ModelSkin SkirtSkin(Vector3 p, float t)
    {
        float reach = Mathf.Min(1f, Mathf.Pow(t, 0.8f) * 1.1f) * 0.95f * Smooth(1.03f, 0.84f, p.y);
        float right = Smooth(-0.05f, 0.05f, p.x);
        return Hips.To(Kit.Bone("B-thigh.R"), reach * right).To(Kit.Bone("B-thigh.L"), reach * (1f - right) / Mathf.Max(0.001f, 1f - reach * right));
    }

    // El gi sigue por debajo de la faja en cuatro faldones, abiertos a los costados y atrás. El de adelante
    // a la izquierda queda encima del otro, como se cruza un gi.
    private void BuildSkirt()
    {
        Panel(-86f, 4f, 0.018f);
        Panel(-4f, 86f, 0.013f);
        Panel(94f, 177f, 0.013f);
        Panel(183f, 266f, 0.013f);
    }

    private void Panel(float fromDeg, float toDeg, float offset)
    {
        Kit.BeginPiece(28f);
        const int columns = 10, rows = 6;
        var rings = new ModelPoint[rows + 1][];
        for (int r = 0; r <= rows; r++)
        {
            float t = r / (float)rows;
            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float u = i / (float)columns;
                float deg = Mathf.Lerp(fromDeg, toDeg, u);
                float hem = SkirtHem + 0.012f * Mathf.Sin(deg * 0.21f);
                float y = Mathf.Lerp(1.07f, hem, t);
                float fold = 0.008f * Mathf.Sin(u * 9f + fromDeg) * t;
                Vector3 p = SkirtPoint(y, deg, offset + fold, SkirtFlare);
                rings[r][i] = new ModelPoint(p, _robe.Uv(u, 1f - t), SkirtSkin(p, t));
            }
        }
        Kit.Sheet(rings, _lining.Uv(0.5f, 0.5f));
    }

    private void Band(Func<float, float> height, float tall, float offset, ModelRegion region)
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
                rings[r][i] = new ModelPoint(p, region.Uv(u, 0.5f + rows[r]), TorsoSkin(p));
            }
        }
        Kit.Loft(rings);
    }

    private const float KnotDeg = -24f;

    private void BuildSash()
    {
        Band(deg => 1.078f, 0.088f, 0.020f, _sash);

        // El nudo, adelante a la izquierda
        Vector3 knot = TorsoPoint(1.072f, KnotDeg, 0.044f);
        Vector3 normal = TorsoDirection(KnotDeg).normalized;
        Vector3 across = Vector3.Cross(Vector3.up, normal).normalized;
        ModelSkin waist = Spine.To(Hips, 0.5f);
        Kit.BeginPiece(45f);
        Kit.Lump(knot, Vector3.up, normal, 0.028f, 0.032f, 0.02f, 8, _sash, 0.5f, waist);
        Kit.Lump(knot - across * 0.034f + Vector3.up * 0.004f, (Vector3.up - across * 0.8f).normalized, normal, 0.025f, 0.02f, 0.015f, 8, _sash, 0.3f, waist);
        Kit.Lump(knot + across * 0.034f + Vector3.up * 0.004f, (Vector3.up + across * 0.8f).normalized, normal, 0.025f, 0.02f, 0.015f, 8, _sash, 0.7f, waist);

        // Las dos puntas, sobre el faldón de adelante
        Tail(KnotDeg - 2f, -7f, 0.60f, 0.062f, 0.3f);
        Tail(KnotDeg + 3f, 5f, 0.69f, 0.056f, 1.7f);
    }

    // Una punta de la faja: cae del nudo sobre el faldón hasta 'bottom', con una onda suave y el extremo en V.
    private void Tail(float startDeg, float driftDeg, float bottom, float width, float phase)
    {
        Kit.BeginPiece(30f);
        const int rows = 9;
        var rings = new ModelPoint[rows + 1][];
        for (int r = 0; r <= rows; r++)
        {
            float t = r / (float)rows;
            float y = Mathf.Lerp(1.05f, bottom, t);
            float deg = startDeg + driftDeg * t + 3f * Mathf.Sin(t * 5f + phase) * t;
            Vector3 c = SkirtPoint(y, deg, 0.034f + 0.006f * Mathf.Sin(t * 4f + phase), SkirtFlare);
            Vector3 across = (SkirtPoint(y, deg + 2f, 0.034f, SkirtFlare) - SkirtPoint(y, deg - 2f, 0.034f, SkirtFlare)).normalized;
            float w = width * Mathf.Lerp(1f, 0.85f, t) * 0.5f;
            float notch = r == rows ? 0.025f : 0f;
            ModelSkin skin = TailSkin(c, t);
            rings[r] = new[]
            {
                new ModelPoint(c - across * w, _sash.Uv(t * 0.6f, 0.05f), skin),
                new ModelPoint(c + Vector3.up * notch, _sash.Uv(t * 0.6f, 0.5f), skin),
                new ModelPoint(c + across * w, _sash.Uv(t * 0.6f, 0.95f), skin),
            };
        }
        Kit.Sheet(rings, _sash.Uv(0.5f, 0.3f));
    }

    // Con todo el peso de la pierna, al abrirla la punta se estiraba de costado.
    private ModelSkin TailSkin(Vector3 p, float t) =>
        Hips.To(Kit.Bone(p.x > 0f ? "B-thigh.R" : "B-thigh.L"), 0.35f * t * Smooth(1.03f, 0.84f, p.y));

    // El collar de cuentas: grandes y oscuras, del pecho a los hombros y por detrás del cuello. La de abajo
    // es más grande y lleva una borla.
    private static Vector3 NecklacePoint(float phi, float radius)
    {
        float a = Mathf.Abs(phi);
        float y = Mathf.Lerp(1.345f, 1.52f, Smooth(0f, 95f, a)) + 0.025f * Smooth(95f, 180f, a);
        return TorsoPoint(y, phi, radius + 0.004f);
    }

    private static float BeadRadius(float phi) => Mathf.Lerp(0.028f, 0.02f, Smooth(100f, 150f, Mathf.Abs(phi)));

    private void BuildBeads()
    {
        Kit.BeginPiece(70f);
        const float bottomRadius = 0.034f;
        Vector3 bottom = NecklacePoint(0f, bottomRadius);
        Bead(bottom, bottomRadius);

        foreach (float side in new[] { 1f, -1f })
        {
            Vector3 last = bottom;
            float lastRadius = bottomRadius;
            float phi = 0f;
            while (true)
            {
                float radius = BeadRadius(phi);
                Vector3 next = last;
                while (Mathf.Abs(phi) < 172f)
                {
                    phi += side * 0.5f;
                    radius = BeadRadius(phi);
                    next = NecklacePoint(phi, radius);
                    if (Vector3.Distance(next, last) >= (lastRadius + radius) * 0.97f) break;
                }
                if (Mathf.Abs(phi) >= 172f) break;
                Bead(next, radius);
                last = next;
                lastRadius = radius;
            }
        }

        // La borla
        Kit.BeginPiece(30f);
        Vector3 root = bottom + Vector3.down * (bottomRadius * 0.9f);
        for (int i = 0; i < 3; i++)
        {
            Vector3 direction = (Vector3.down + Vector3.right * ((i - 1) * 0.25f) + Vector3.forward * 0.15f).normalized;
            Kit.Tuft(root, direction, Vector3.forward, Range(0.06f, 0.075f), 0.012f, 0.005f, 0.1f, _cord, 0.3f + 0.2f * i, Chest);
        }
    }

    private void Bead(Vector3 center, float radius)
    {
        ModelSkin skin = Chest.To(Neck, 0.5f * Smooth(1.5f, 1.54f, center.y));
        float u = Range(0.2f, 0.8f);
        const int rings = 6, sides = 12;

        var rows = new ModelPoint[rings + 1][];
        for (int r = 0; r <= rings; r++)
        {
            float lat = Mathf.Lerp(-90f, 90f, r / (float)rings) * Mathf.Deg2Rad;
            rows[r] = new ModelPoint[sides];
            for (int i = 0; i < sides; i++)
            {
                float lon = i / (float)sides * Mathf.PI * 2f;
                var p = center + new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon)) * radius;
                rows[r][i] = new ModelPoint(p, _bead.Uv(u, (Mathf.Sin(lat) + 1f) * 0.5f), skin);
            }
        }
        Kit.Loft(rows, wrap: true);
    }

    // =========================================================
    // HOMBRERA
    // =========================================================

    private ModelSkin PauldronSkin(string side, float d) =>
        Chest.To(Kit.Bone("B-shoulder" + side), Smooth(-0.08f, -0.03f, d))
             .To(Kit.Bone("B-upperArm" + side), Smooth(-0.03f, 0.06f, d) * 0.92f);

    private void BuildPauldron(float side)
    {
        string name = SideName(side);
        Vector3 joint = Kit.Pos("B-upperArm" + name);
        Vector3 axis = (Kit.Pos("B-forearm" + name) - joint).normalized;
        Vector3 up = Vector3.ProjectOnPlane(Vector3.up, axis).normalized;
        Vector3 front = Vector3.Cross(axis, up).normalized;
        if (front.z < 0f) front = -front;
        Vector3 origin = joint + up * 0.006f + axis * 0.018f;

        // De abajo hacia arriba: cada lámina queda tapada por la de arriba
        Lame(origin, axis, up, front, name, 0.075f, 0.145f, 0.092f, 0.114f, 100f);
        Lame(origin, axis, up, front, name, 0.04f, 0.1f, 0.100f, 0.116f, 102f);

        // La cúpula de arriba: no llega al cuello, ahí van las cuentas
        Kit.BeginPiece(30f);
        const int columns = 16;
        float[] stops = { -0.075f, -0.066f, -0.05f, -0.03f, -0.01f, 0.01f, 0.03f, 0.045f, 0.052f };
        const float halfArc = 106f;
        var rings = new ModelPoint[stops.Length][];
        for (int r = 0; r < stops.Length; r++)
        {
            float d = stops[r];
            float closing = Mathf.Sqrt(Mathf.Max(0.04f, 1f - Sq(Mathf.Min(0f, d - 0.01f) / 0.09f)));
            float radius = 0.114f * closing + (r == stops.Length - 1 ? -0.007f : 0f);
            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float s = i / (float)columns;
                float a = (s - 0.5f) * 2f * halfArc * Mathf.Deg2Rad;
                Vector3 p = origin + axis * d + up * (Mathf.Cos(a) * radius) + front * (Mathf.Sin(a) * radius * 0.96f);
                rings[r][i] = new ModelPoint(p, _bronze.Uv(s, Mathf.InverseLerp(-0.075f, 0.052f, d)), PauldronSkin(name, d));
            }
        }
        Kit.Sheet(rings, _lining.Uv(0.5f, 0.5f), joint);
    }

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
                float tuck = 0.018f * Sq(s * 2f - 1f) * t;
                Vector3 p = origin + axis * (d - tuck) + up * (Mathf.Cos(a) * radius) + front * (Mathf.Sin(a) * radius * 0.96f);
                rings[r][i] = new ModelPoint(p, _bronze.Uv(s, Mathf.Lerp(0.08f, 0.97f, t)), PauldronSkin(side, d));
            }
        }
        Kit.Sheet(rings, _lining.Uv(0.5f, 0.5f), origin + axis * ((from + to) * 0.5f));
    }

    // =========================================================
    // PELO
    // =========================================================

    private void BuildHair()
    {
        BuildHairCap();

        // Atrás: hasta la nuca
        for (int i = 0; i < 16; i++)
        {
            float s = i / 15f * 2f - 1f;
            float deg = 180f + s * 105f;
            Vector3 root = HeadPoint(Range(0.6f, 0.86f), deg, 0.004f);
            Vector3 tip = HeadPoint(Range(0.2f, 0.3f), deg + Range(-8f, 8f), 0.03f);
            Strand(root, root + Outward(root) * 0.035f, tip + Outward(tip) * 0.012f + Vector3.up * 0.04f, tip,
                Range(0.028f, 0.036f), Range(0.011f, 0.015f), 7);
        }

        // A los lados, tapando las orejas
        foreach (float side in new[] { 1f, -1f })
            for (int i = 0; i < 5; i++)
            {
                float deg = side * Mathf.Lerp(64f, 106f, i / 4f);
                Vector3 root = HeadPoint(Range(0.72f, 0.86f), deg, 0.004f);
                Vector3 tip = HeadPoint(Range(0.3f, 0.38f), deg + side * Range(-4f, 6f), 0.024f);
                Strand(root, root + Outward(root) * 0.03f, tip + Outward(tip) * 0.01f + Vector3.up * 0.04f, tip,
                    Range(0.024f, 0.032f), Range(0.010f, 0.013f), 7);
            }

        // Arriba: mechones que se levantan un poco de la coronilla y caen hacia los lados, despeinados
        for (int i = 0; i < 13; i++)
        {
            float deg = Range(-180f, 180f);
            Vector3 root = HeadPoint(Range(0.93f, 0.99f), deg, 0.006f);
            Vector3 tip = HeadPoint(Range(0.7f, 0.8f), deg + Range(-25f, 25f), Range(0.012f, 0.02f));
            Strand(root, root + Outward(root) * 0.022f + Vector3.up * 0.004f, tip + Outward(tip) * 0.008f + Vector3.up * 0.015f, tip,
                Range(0.026f, 0.034f), Range(0.011f, 0.014f), 7);
        }

        // Flequillo: cae sobre la frente hasta arriba de los ojos
        for (int i = 0; i < 11; i++)
        {
            float s = i / 10f * 2f - 1f;
            float deg = s * 42f;
            Vector3 root = HeadPoint(Range(0.9f, 0.96f), deg * 0.5f, 0.012f);
            float end = Mathf.Abs(s) < 0.3f ? Range(0.55f, 0.58f) : Range(0.58f, 0.66f);
            Vector3 tip = HeadPoint(end, deg + Range(-6f, 6f), 0.016f);
            Vector3 mid = HeadPoint(0.8f, deg * 0.8f, 0.035f);
            Strand(root, mid, HeadPoint(Mathf.Lerp(0.8f, end, 0.5f), deg, 0.024f), tip, Range(0.026f, 0.032f), 0.011f, 8);
        }
    }

    // Un mechón con la raíz cerrada: los de la coronilla quedan a la vista.
    private void Strand(Vector3 root, Vector3 control1, Vector3 control2, Vector3 tip, float width, float thick, int segments) =>
        Lock(root, control1, control2, tip, width, thick, segments, 5, true);

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
                float swell = 0.009f + 0.012f * Mathf.PerlinNoise(u * 13f, t * 3.1f) * Smooth(0f, 0.15f, t);
                Vector3 p = HeadPoint(h, deg, swell);
                float strip = 0.12f + 0.76f * Mathf.Repeat(u * 5f, 1f);
                rings[r][i] = new ModelPoint(p, _hair.Uv(strip, Mathf.Lerp(0.34f, 0.05f, t)), HeadBone);
            }
        }
        Kit.Loft(rings);
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
        Atlas.Paint(_sash, PaintSash);
        Atlas.Paint(_robe, PaintRobe);
        Atlas.Paint(_bandage, (u, v) => BandageAt(u, v * 0.1f, u * 360f));
        Atlas.Paint(_bead, PaintBead);
        Atlas.Paint(_bronze, PaintBronze);
        Atlas.Paint(_shoe, PaintShoe);
        Atlas.Paint(_skinHand, (u, v) => Scaly(Grain(SkinTone, u, v, 20f, 0.05f), u * 0.1f, v * 0.09f, 0.0065f, 0.45f));
        Atlas.Paint(_lining, (u, v) => Robe * 0.7f);
        Atlas.Paint(_cord, (u, v) => Color.Lerp(Cord * 0.6f, Cord * 1.1f, v));
        Atlas.Paint(_sleeve, PaintSleeve);
    }

    // Escamas de serpiente, en metros. Cada escama es un rombo montado sobre la de abajo: la de arriba le deja
    // una sombra en la punta, su borde de abajo brilla un poco y cada una tiene su tono. Con una red de líneas
    // finas parecía una media de red. amount va de 0 (piel lisa) a 1 (escamas bien marcadas).
    private static Color Scaly(Color c, float x, float y, float size, float amount)
    {
        if (amount <= 0.001f) return c;

        float a = x / size + y / (size * 0.8f);
        float b = x / size - y / (size * 0.8f);
        float ia = Mathf.Floor(a), ib = Mathf.Floor(b);
        float fa = a - ia, fb = b - ib;

        float top = Mathf.Min(1f - fa, fb);      // cerca de la punta de arriba
        float bottom = Mathf.Min(fa, 1f - fb);   // cerca del borde de abajo
        float tone = Hash(ia, ib) - 0.5f;

        Color s = c * (1f - 0.34f * Smooth(0.36f, 0f, top) + 0.1f * Smooth(0.16f, 0f, bottom) + 0.14f * tone);
        s = Over(s, ScaleLine, 0.22f * Smooth(0.045f, 0f, top));

        // Manchas de pitón, sueltas
        float blot = Mathf.PerlinNoise(x * 18f + 11.3f, y * 14f + 4.1f);
        s = Over(s, Blotch, 0.4f * Smooth(0.64f, 0.7f, blot));
        return Color.Lerp(c, s, amount);
    }

    // Un número de 0 a 1 que cambia de escama a escama y se repite igual en cada pasada.
    private static float Hash(float a, float b)
    {
        float h = Mathf.Sin(a * 127.1f + b * 311.7f) * 43758.5453f;
        return h - Mathf.Floor(h);
    }

    private static Color RobeAt(float u, float v)
    {
        Color c = Grain(Robe, u, v, 70f, 0.25f);
        float folds = Mathf.PerlinNoise(u * 13f + 2f, v * 2.6f);
        c = Over(c, RobeSheen, 0.35f * Smooth(0.6f, 0.85f, folds));
        return c * (0.85f + 0.3f * Mathf.PerlinNoise(u * 4f, v * 7f));
    }

    // Vendas: tiras en espiral, con su sombra en cada vuelta.
    private static Color BandageAt(float u, float d, float deg)
    {
        float turn = Mathf.Repeat(deg / 360f + d / 0.026f, 1f);
        Color c = Grain(Bandage, u * 3f, d * 4f, 40f, 0.12f);
        c = Over(c, BandageDark, Smooth(0.16f, 0f, turn) * 0.7f);
        return Over(c, BandageDark * 0.8f, Smooth(0.9f, 1f, turn) * 0.35f);
    }

    private static Color PaintHead(float u, float v)
    {
        float x = Mathf.Lerp(FaceLeft, FaceRight, u);
        float y = Mathf.Lerp(FaceBottom, FaceTop, v);
        float ax = Mathf.Abs(x);

        Color c = Grain(SkinTone, u, v, 24f, 0.05f);
        c = Over(c, SkinShade, 0.4f * Smooth(0.056f, 0.085f, ax));
        c = Over(c, SkinShade, 0.4f * Smooth(1.585f, 1.56f, y));

        // Sombras de la cara: ojos hundidos y pómulos marcados
        c = Over(c, SkinShade, 0.6f * Bell(ax, 0.036f, 0.025f) * Bell(y, 1.693f, 0.011f));
        c = Over(c, SkinShade, 0.25f * Bell(ax, 0.009f, 0.004f) * Bell(y, 1.665f, 0.022f));
        c = Over(c, SkinShade, 0.32f * Bell(x, 0f, 0.009f) * Bell(y, 1.633f, 0.0028f));
        c = Over(c, SkinShade * 0.8f, 0.55f * Bell(ax, 0.0095f, 0.003f) * Bell(y, 1.6355f, 0.0022f));     // fosas de la nariz
        c = Over(c, SkinShade, 0.3f * Bell(ax, 0.052f, 0.012f) * Bell(y, 1.645f, 0.022f));
        c = Over(c, new Color(0.96f, 0.80f, 0.52f), 0.35f * Bell(x, 0f, 0.005f) * Bell(y, 1.66f, 0.028f));
        c = Over(c, SkinShade, 0.5f * Smooth(1.74f, 1.775f, y));

        // Escamas: en las sienes, por fuera de los ojos y en la mandíbula; el centro de la cara queda liso
        float temple = Smooth(0.048f, 0.074f, ax);
        float aroundEye = Bell(ax, 0.066f, 0.014f) * Bell(y, 1.69f, 0.024f);
        float forehead = Smooth(1.735f, 1.775f, y) * Smooth(0.01f, 0.045f, ax) * 0.5f;
        float jaw = Smooth(1.62f, 1.585f, y) * Smooth(0.03f, 0.062f, ax) * 0.5f;
        float patches = 0.65f + 0.35f * Mathf.PerlinNoise(x * 40f + 3f, y * 40f + 1f);
        c = Scaly(c, x, y, 0.0075f, Mathf.Clamp01((0.85f * temple + 0.5f * aroundEye + forehead + jaw) * patches));

        // La marca de la frente
        float mark = ax / 0.0045f + Mathf.Abs(y - 1.738f) / 0.0075f;
        c = Over(c, new Color(0.62f, 0.10f, 0.06f), Smooth(1.05f, 0.9f, mark));

        // Boca
        float lipY = y - 1.6095f + 1.4f * ax * ax;
        const float lipHalf = 0.0195f;
        float upper = Smooth(0f, 0.001f, lipY) * Smooth(0.0042f - 0.0034f * ax / lipHalf, 0.0032f - 0.0034f * ax / lipHalf, lipY);
        float lower = Smooth(0f, -0.001f, lipY) * Smooth(-0.0068f + 0.0062f * ax / lipHalf, -0.0058f + 0.0062f * ax / lipHalf, lipY);
        float inside = Smooth(lipHalf, lipHalf - 0.0015f, ax);
        c = Over(c, new Color(0.62f, 0.40f, 0.24f), upper * inside);
        c = Over(c, new Color(0.72f, 0.48f, 0.30f), lower * inside);
        c = Over(c, new Color(0.22f, 0.12f, 0.06f), Smooth(0.0012f, 0.0005f, Mathf.Abs(lipY)) * inside);

        // Ojos de serpiente: amarillos, con la pupila rasgada y las puntas de afuera un poco alzadas
        float ex = (ax - 0.0355f) / 0.0178f;
        float ey = (y - 1.6835f - (ax - 0.0355f) * 0.16f) / 0.0066f;
        float open = 1f - ex * ex;
        if (open > 0f)
        {
            float lid = Mathf.Sqrt(open);
            float eye = Smooth(lid * 1.02f, lid * 0.92f, Mathf.Abs(ey));
            float ix = (ax - 0.0348f) / 0.0072f;
            float iy = (y - 1.6835f) / 0.0072f;
            float iris = Mathf.Sqrt(ix * ix + iy * iy);

            Color ball = new Color(0.90f, 0.86f, 0.66f);
            ball = Over(ball, Color.Lerp(new Color(1f, 0.86f, 0.18f), new Color(0.78f, 0.44f, 0.04f), Smooth(0.2f, 1f, iris)), Smooth(1.06f, 0.96f, iris));
            float slit = Sq(ix / 0.2f) + Sq(iy / 0.88f);
            ball = Over(ball, new Color(0.02f, 0.02f, 0.02f), Smooth(1.15f, 0.85f, slit));
            ball = Over(ball, Color.white, Smooth(0.22f, 0.15f, Vector2.Distance(new Vector2(ix, iy), new Vector2(-0.3f, 0.36f))));
            ball = Over(ball, SkinShade * 0.6f, Smooth(0.2f, 1f, ey) * 0.45f);
            c = Over(c, ball, eye);
        }
        float upperLine = Mathf.Abs(ey - Mathf.Sqrt(Mathf.Max(0f, open)));
        c = Over(c, new Color(0.12f, 0.07f, 0.03f), Smooth(0.5f, 0.24f, upperLine) * Smooth(1.25f, 1.02f, Mathf.Abs(ex)) * Smooth(-0.3f, 0.1f, ey));

        // Cejas canosas, como el pelo, bajas hacia el centro
        float bx = ax - 0.012f;
        float browY = 1.6962f + bx * 0.36f - bx * bx * 3.4f;
        float browThick = Mathf.Lerp(0.0036f, 0.0016f, Smooth(0f, 0.046f, bx));
        float browNoise = (Mathf.PerlinNoise(u * 260f, v * 90f) - 0.5f) * 0.0016f;
        float brow = Smooth(browThick + 0.0011f, browThick, Mathf.Abs(y - browY) + browNoise) * Smooth(0f, 0.004f, bx) * Smooth(0.05f, 0.044f, bx);
        return Over(c, Color.Lerp(HairShade, HairWhite, 0.35f), brow);
    }

    private static Color PaintTorso(float u, float v)
    {
        float deg = (u - 0.5f) * 360f;
        float abs = Mathf.Abs(deg);
        float y = Mathf.Lerp(TorsoBottom, TorsoTop, v);

        // La piel que deja ver el gi: el pecho y el cuello
        Color skin = Grain(SkinTone, u * 3f, v, 30f, 0.05f);
        skin = Over(skin, SkinShade, 0.35f * Bell(y, 1.305f, 0.014f) * Smooth(36f, 6f, abs));
        skin = Over(skin, SkinShade, 0.2f * Bell(deg, 0f, 1.3f) * Smooth(1.13f, 1.2f, y) * Smooth(1.44f, 1.36f, y));
        foreach (float row in new[] { 1.165f, 1.215f, 1.262f })
            skin = Over(skin, SkinShade, 0.14f * Bell(y, row, 0.006f) * Smooth(10f, 3f, abs));
        skin = Over(skin, SkinShade, 0.4f * Smooth(1.585f, 1.548f, y) * Smooth(1.51f, 1.535f, y));
        float scales = Mathf.Lerp(0f, 0.45f, Smooth(18f, 34f, abs)) + 0.45f * Smooth(1.49f, 1.54f, y) * Smooth(15f, 50f, abs);
        scales *= 0.65f + 0.35f * Mathf.PerlinNoise(u * 30f, v * 12f);
        skin = Scaly(skin, deg / 360f * 0.88f, y, 0.014f, Mathf.Clamp01(scales));

        // El gi: debajo de la faja cubre todo; arriba deja el escote y el cuello
        float open = OpenDeg(y);
        float robeMask = Smooth(open - 0.7f, open + 0.7f, abs);
        robeMask = Mathf.Max(robeMask, Smooth(1.105f, 1.095f, y));
        robeMask *= Smooth(CollarTop(deg) + 0.004f, CollarTop(deg) - 0.004f, y);

        Color robe = RobeAt(u * 3f, v);
        robe = Over(robe, Ochre, Smooth(open + LapelDeg + 1f, open + LapelDeg - 0.5f, abs) * Smooth(1.09f, 1.1f, y));
        robe = Over(robe, Ochre, Smooth(1.03f, 1.04f, y) * Smooth(1.125f, 1.115f, y));
        return Over(skin, robe, robeMask);
    }

    private static Color PaintArm(float u, float v)
    {
        float d = Mathf.Lerp(-0.05f, 0.446f, v);
        float deg = (u - 0.5f) * 360f;
        float top = Mathf.Cos(deg * Mathf.Deg2Rad) * 0.5f + 0.5f;

        // Debajo de la manga, negro; del codo para abajo, la piel con las escamas más marcadas por fuera
        Color c = RobeAt(u * 2f, v);
        Color skin = Grain(SkinTone, u * 2f, v, 30f, 0.05f);
        skin = Over(skin, SkinShade, 0.3f * (1f - top));
        skin = Scaly(skin, u * 0.28f, d, 0.011f, Mathf.Lerp(0.35f, 0.8f, top));
        c = Over(c, skin, Smooth(SleeveEnd - 0.012f, SleeveEnd - 0.002f, d));

        float wrap = Smooth(WrapStart - 0.002f, WrapStart + 0.002f, d);
        return wrap > 0f ? Over(c, BandageAt(u, d, deg), wrap) : c;
    }

    private static Color PaintLeg(float u, float v)
    {
        float d = Mathf.Lerp(-0.07f, 0.82f, v);
        float deg = (u - 0.5f) * 360f;

        // El pantalón ancho, con sus pliegues
        Color c = RobeAt(u * 2f, v);
        c *= 1f + (Mathf.PerlinNoise(u * 9f, v * 2.2f) - 0.5f) * 0.5f;

        // Las vendas de la espinilla, con dos tiras de cuero que se cruzan
        float wrap = Smooth(ShinWrap - 0.002f, ShinWrap + 0.002f, d);
        if (wrap <= 0f) return c;

        Color w = BandageAt(u, d, deg);
        float pitch = d / 0.085f;
        float a = Mathf.Abs(Mathf.Repeat(deg / 360f + pitch, 1f) - 0.5f);
        float b = Mathf.Abs(Mathf.Repeat(-deg / 360f + pitch, 1f) - 0.5f);
        w = Over(w, Strap * 0.7f, Smooth(0.13f, 0.1f, b));
        w = Over(w, Strap, Smooth(0.1f, 0.07f, b));
        w = Over(w, Strap * 0.7f, Smooth(0.13f, 0.1f, a));
        w = Over(w, Strap * 1.15f, Smooth(0.1f, 0.07f, a));
        return Over(c, w, wrap);
    }

    private static Color PaintHair(float u, float v)
    {
        Color c = Color.Lerp(HairShade, HairWhite, Smooth(0.05f, 0.7f, v));
        float strands = Mathf.PerlinNoise(u * 34f, v * 2.4f);
        float fine = Mathf.PerlinNoise(u * 90f + 9f, v * 3.5f);
        c *= 0.82f + 0.24f * strands + 0.12f * (fine - 0.5f);
        return Over(c, HairShade * 0.85f, 0.35f * Smooth(0.6f, 0.75f, fine));
    }

    private static Color PaintSash(float u, float v)
    {
        Color c = Grain(Ochre, u * 4f, v, 50f, 0.15f);
        c *= 0.8f + 0.4f * Mathf.PerlinNoise(u * 22f, v * 3f);
        float edge = Mathf.Abs(v - 0.5f) * 2f;
        return Over(c, OchreDark, Smooth(0.8f, 1f, edge) * 0.8f);
    }

    // Los faldones: negros, con ribete ocre abajo y en los bordes de las aberturas.
    private static Color PaintRobe(float u, float v)
    {
        Color c = RobeAt(u * 2f, v);
        c *= 0.85f + 0.3f * Mathf.Cos(u * 18f + 1f) * Smooth(0.9f, 0.3f, v);
        c = Over(c, Ochre, Smooth(0.075f, 0.06f, v));
        c = Over(c, OchreDark, Smooth(0.02f, 0.008f, Mathf.Abs(v - 0.07f)));
        float side = Mathf.Min(u, 1f - u);
        return Over(c, Ochre * 0.85f, Smooth(0.04f, 0.03f, side) * Smooth(0.98f, 0.9f, v));
    }

    private static Color PaintSleeve(float u, float v)
    {
        Color c = RobeAt(u, v * 2f);
        c *= 0.85f + 0.3f * Mathf.Cos(u * 12f) * Smooth(0.2f, 0.9f, v);
        c = Over(c, Ochre, Smooth(0.9f, 0.93f, v));
        return Over(c, OchreDark, Smooth(0.015f, 0.006f, Mathf.Abs(v - 0.91f)));
    }

    private static Color PaintBead(float u, float v)
    {
        Color c = Color.Lerp(Wood, WoodLight, Smooth(0.45f, 0.85f, v) * 0.8f);
        return c * (0.85f + 0.3f * Mathf.PerlinNoise(u * 30f, v * 6f));
    }

    private static Color PaintBronze(float u, float v)
    {
        float arc = Mathf.Abs(u - 0.5f) * 2f;
        float sheen = 0.5f + 0.4f * Bell(arc, 0.28f, 0.26f) * Smooth(0.05f, 0.5f, v) - 0.34f * Smooth(0.6f, 1f, arc);
        Color c = Color.Lerp(BronzeDark, Bronze, Mathf.Clamp01(0.3f + 0.7f * sheen));
        c = Over(c, BronzeLight, 0.55f * Smooth(0.72f, 1f, sheen));
        c *= 1f + (Mathf.PerlinNoise(u * 9f, v * 9f) - 0.5f) * 0.18f;
        c = Over(c, BronzeDark, Smooth(0.2f, 0.02f, v) * 0.7f);
        c = Over(c, BronzeDark, Smooth(0.02f, 0.008f, Mathf.Abs(v - 0.74f)));
        c = Over(c, BronzeLight, Smooth(0.86f, 0.89f, v));
        return Over(c, BronzeDark, Smooth(0.965f, 0.985f, v));
    }

    // Zapatos de tela, con la suela gruesa y una correa de cuero sobre el empeine.
    private static Color PaintShoe(float u, float v)
    {
        Color c = Grain(Cloth, u, v, 60f, 0.35f);
        c = Over(c, new Color(0.16f, 0.12f, 0.09f), Smooth(0.13f, 0.09f, v));
        c = Over(c, new Color(0.05f, 0.04f, 0.03f), Smooth(0.045f, 0.03f, v));
        float strap = Mathf.Abs(v - 0.62f);
        c = Over(c, Strap, Smooth(0.045f, 0.035f, strap) * Smooth(0.2f, 0.25f, v));
        return Over(c, Strap * 0.6f, Smooth(0.01f, 0.004f, Mathf.Abs(strap - 0.04f)));
    }
}
