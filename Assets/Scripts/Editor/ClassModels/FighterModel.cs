using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ============================================================
// FighterModel
//
// La receta del modelo de relleno del Guerrero (referencias elegidas por Gustavo el 6 de noviembre de
// 2026): un montaraz con armadura pesada. Pelo oscuro a los hombros, barba, capa oscura sobre el acero,
// peto con detalles de oro, hombreras por láminas, guanteletes, grebas y escarpes. Sin espada ni escudo:
// son de la clase. Es un personaje propio, no la copia de uno conocido.
//
// El acabado es Polished: el alfa de cada color dice qué tan pulido es, y al pintar se mezcla solo con
// el color. Por eso la paleta lleva el cuarto número.
//
// Mercenarios ▸ Modelos de clase ▸ Generar el Guerrero: rehace malla, textura y prefab.
// ============================================================
public sealed class FighterModel : ClassModelRecipe
{
    private const string Name = "Fighter";
    private const string ArtFolder = "Assets/Art/Models/Classes/Fighter";
    private const string PrefabFolder = "Assets/Prefabs/Player/Classes";

    // ---------- Colores (el alfa es el pulido) ----------
    private static readonly Color SkinTone    = new Color(0.80f, 0.54f, 0.40f, 0.22f);
    private static readonly Color SkinShade   = new Color(0.50f, 0.27f, 0.20f, 0.15f);
    private static readonly Color HairRoot    = new Color(0.08f, 0.04f, 0.02f, 0.10f);
    private static readonly Color HairMid     = new Color(0.20f, 0.11f, 0.05f, 0.14f);
    private static readonly Color HairTip     = new Color(0.33f, 0.18f, 0.08f, 0.14f);
    private static readonly Color Steel       = new Color(0.33f, 0.37f, 0.45f, 0.80f);
    private static readonly Color SteelDark   = new Color(0.12f, 0.14f, 0.19f, 0.60f);
    private static readonly Color SteelLight  = new Color(0.56f, 0.62f, 0.73f, 0.90f);
    private static readonly Color Gold        = new Color(0.92f, 0.62f, 0.13f, 0.90f);
    private static readonly Color GoldDark    = new Color(0.50f, 0.28f, 0.06f, 0.70f);
    private static readonly Color Mail        = new Color(0.30f, 0.33f, 0.40f, 0.60f);
    private static readonly Color MailDark    = new Color(0.07f, 0.08f, 0.11f, 0.20f);
    private static readonly Color Navy        = new Color(0.05f, 0.10f, 0.26f, 0.05f);
    private static readonly Color CollarBlue  = new Color(0.04f, 0.24f, 0.46f, 0.05f);
    private static readonly Color CloakTone   = new Color(0.10f, 0.14f, 0.23f, 0.03f);
    private static readonly Color Leather     = new Color(0.34f, 0.19f, 0.10f, 0.30f);
    private static readonly Color LeatherDark = new Color(0.14f, 0.09f, 0.06f, 0.25f);
    private static readonly Color Trousers    = new Color(0.11f, 0.09f, 0.09f, 0.05f);

    // ---------- Torso: alto → medio ancho, media profundidad, centro en Z ----------
    private static readonly ModelKeys TorsoWidth = new ModelKeys(
        0.845f, 0.06f, 0.90f, 0.150f, 0.95f, 0.172f, 1.00f, 0.172f, 1.05f, 0.160f, 1.09f, 0.148f, 1.13f, 0.150f,
        1.19f, 0.165f, 1.25f, 0.180f, 1.31f, 0.192f, 1.37f, 0.198f, 1.42f, 0.198f, 1.46f, 0.190f, 1.49f, 0.150f,
        1.515f, 0.090f, 1.54f, 0.058f, 1.57f, 0.055f, 1.62f, 0.050f);

    private static readonly ModelKeys TorsoDepth = new ModelKeys(
        0.845f, 0.06f, 0.90f, 0.100f, 0.95f, 0.112f, 1.00f, 0.114f, 1.05f, 0.108f, 1.09f, 0.100f, 1.13f, 0.104f,
        1.19f, 0.116f, 1.25f, 0.128f, 1.31f, 0.136f, 1.37f, 0.138f, 1.42f, 0.128f, 1.46f, 0.110f, 1.49f, 0.085f,
        1.515f, 0.070f, 1.54f, 0.058f, 1.57f, 0.055f, 1.62f, 0.052f);

    private static readonly ModelKeys TorsoCenter = new ModelKeys(
        0.845f, 0.015f, 0.95f, 0f, 1.05f, 0.01f, 1.13f, 0.02f, 1.25f, 0.032f, 1.37f, 0.04f, 1.46f, 0.02f,
        1.515f, 0.004f, 1.62f, 0.016f);

    private const float TorsoBottom = 0.845f;
    private const float TorsoTop = 1.62f;

    // ---------- Brazo y pierna: distancia desde el nacimiento → radio ----------
    private static readonly ModelKeys ArmRadius = new ModelKeys(
        -0.05f, 0.044f, 0f, 0.058f, 0.06f, 0.062f, 0.12f, 0.056f, 0.18f, 0.049f, 0.234f, 0.045f, 0.265f, 0.048f,
        0.32f, 0.046f, 0.38f, 0.038f, 0.43f, 0.030f, 0.445f, 0.030f);

    private static readonly ModelKeys LegRadius = new ModelKeys(
        -0.07f, 0.088f, 0f, 0.096f, 0.08f, 0.098f, 0.16f, 0.092f, 0.26f, 0.080f, 0.35f, 0.068f, 0.41f, 0.062f,
        0.46f, 0.060f, 0.53f, 0.066f, 0.62f, 0.060f, 0.72f, 0.050f, 0.79f, 0.046f, 0.82f, 0.046f);

    // Dónde empiezan y terminan las piezas del brazo y la pierna.
    private const float CouterStart = 0.205f, VambraceStart = 0.262f, GauntletStart = 0.352f, WristEnd = 0.436f;
    private const float CuisseStart = 0.10f, PoleynStart = 0.365f, GreaveStart = 0.462f;

    // ---------- Cabeza: alto de 0 (mentón) a 1 (coronilla) ----------
    private const float HeadBottom = 1.555f;
    private const float HeadHeight = 0.255f;

    private static readonly ModelKeys HeadWidth = new ModelKeys(
        0f, 0.018f, 0.04f, 0.036f, 0.1f, 0.052f, 0.2f, 0.065f, 0.35f, 0.073f, 0.5f, 0.079f, 0.7f, 0.082f, 0.85f, 0.068f,
        0.95f, 0.040f, 1f, 0.005f);

    private static readonly ModelKeys HeadDepth = new ModelKeys(
        0f, 0.024f, 0.1f, 0.044f, 0.22f, 0.062f, 0.35f, 0.082f, 0.5f, 0.098f, 0.7f, 0.104f, 0.85f, 0.090f,
        0.95f, 0.055f, 1f, 0.006f);

    private static readonly ModelKeys HeadOffset = new ModelKeys(
        0f, 0.062f, 0.1f, 0.050f, 0.22f, 0.034f, 0.35f, 0.022f, 0.5f, 0.012f, 0.7f, 0.005f, 0.85f, 0f, 1f, 0f);

    private static readonly ModelKeys Hairline = new ModelKeys(
        0f, 0.80f, 20f, 0.78f, 40f, 0.71f, 60f, 0.62f, 90f, 0.44f, 120f, 0.27f, 180f, 0.13f);

    private const float FaceLeft = -0.11f, FaceRight = 0.11f, FaceBottom = 1.54f, FaceTop = 1.83f;

    // Hasta dónde sube la barba, por distancia al centro de la cara.
    private static readonly ModelKeys BeardTop = new ModelKeys(
        0f, 1.634f, 0.016f, 1.633f, 0.03f, 1.628f, 0.045f, 1.628f, 0.058f, 1.648f, 0.068f, 1.676f, 0.08f, 1.70f);

    private readonly ModelRegion _head, _torso, _arm, _leg, _hair, _cloak, _tabard, _mail, _plate, _dome, _boot, _belt,
                                 _glove, _gold, _steel, _lining, _collar, _boss;

    protected override ModelRegion HairRegion => _hair;

    [MenuItem("Mercenarios/Modelos de clase/Generar el Guerrero")]
    public static void Generate()
    {
        var model = new FighterModel();
        model.Build();
    }

    private FighterModel() : base(7316)
    {
        _head   = Atlas.Region(0, 1024, 1024, 1024);
        _torso  = Atlas.Region(1024, 1024, 1024, 1024);
        _arm    = Atlas.Region(0, 0, 512, 1024);
        _leg    = Atlas.Region(512, 0, 512, 1024);
        _hair   = Atlas.Region(1024, 0, 256, 1024);
        _cloak  = Atlas.Region(1280, 512, 256, 512);
        _tabard = Atlas.Region(1536, 512, 256, 512);
        _mail   = Atlas.Region(1792, 512, 256, 512);
        _plate  = Atlas.Region(1280, 256, 256, 256);
        _dome   = Atlas.Region(1536, 256, 256, 256);
        _boot   = Atlas.Region(1792, 256, 256, 256);
        _belt   = Atlas.Region(1280, 128, 768, 128);
        _glove  = Atlas.Region(1280, 0, 256, 128);
        _gold   = Atlas.Region(1536, 0, 128, 128);
        _steel  = Atlas.Region(1664, 0, 128, 128);
        _lining = Atlas.Region(1792, 0, 128, 128);
        _collar = Atlas.Region(1920, 0, 64, 128);
        _boss   = Atlas.Region(1984, 0, 64, 128);
    }

    private void Build()
    {
        PaintAtlas();

        BuildTorso();
        BuildCuirass();
        BuildHead();
        foreach (float side in new[] { 1f, -1f })
        {
            BuildArm(side);
            BuildGauntlet(side);
            BuildPauldron(side);
            BuildLeg(side);
            BuildSabaton(side);
        }

        BuildCape();
        BuildMailSkirt();
        BuildTabard();
        BuildTasset(1f);
        BuildTasset(-1f);
        BuildBelts();
        BuildGorget();
        BuildCowl();
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
            0.845f, 0.87f, 0.90f, 0.95f, 1.0f, 1.05f, 1.09f, 1.13f, 1.19f, 1.25f, 1.31f, 1.37f, 1.42f, 1.46f, 1.49f,
            1.515f, 1.54f, 1.57f, 1.62f,
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

    // El borde de abajo del peto: en punta adelante, recto atrás.
    private static float CuirassBottom(float deg) => 1.128f - 0.058f * Smooth(40f, 0f, Mathf.Abs(Wrap(deg)));

    // El de arriba: un escote en V que deja ver el cuello de tela.
    private static float CuirassTop(float deg) => 1.492f - 0.066f * Smooth(44f, 0f, Mathf.Abs(Wrap(deg)));

    private static Vector3 CuirassPoint(float y, float deg, float offset)
    {
        float a = Mathf.Abs(Wrap(deg));
        float keel = 0.013f * Bell(a, 0f, 6.5f) * Smooth(1.1f, 1.2f, y);                       // la arista del centro
        float chest = 0.017f * Bell(a, 36f, 20f) * Bell(y, 1.355f, 0.06f);                     // pectorales
        float spine = 0.005f * Bell(a, 180f, 7f);
        return TorsoPoint(y, deg, offset + keel + chest + spine);
    }

    private void BuildCuirass()
    {
        Kit.BeginPiece(48f);
        const int columns = 48;
        float[] rows = { 0f, 0f, 0.012f, 0.06f, 0.16f, 0.28f, 0.4f, 0.52f, 0.64f, 0.76f, 0.88f, 0.985f, 1f, 1f };

        var rings = new ModelPoint[rows.Length][];
        for (int r = 0; r < rows.Length; r++)
        {
            bool lip = r == 0 || r == rows.Length - 1;
            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float u = i / (float)columns;
                float deg = (u - 0.5f) * 360f;
                float y = Mathf.Lerp(CuirassBottom(deg), CuirassTop(deg), rows[r]);
                Vector3 p = CuirassPoint(y, deg, lip ? 0.001f : 0.013f);
                rings[r][i] = new ModelPoint(p, _torso.Uv(u, Mathf.InverseLerp(TorsoBottom, TorsoTop, y)), TorsoSkin(p));
            }
        }
        Kit.Loft(rings);
    }

    // La gola: un cuello alto de acero, abierto adelante.
    private void BuildGorget()
    {
        Kit.BeginPiece(36f);
        const int columns = 26;
        const float open = 26f;
        float[] rows = { 0f, 0.5f, 0.92f, 1f };

        var rings = new ModelPoint[rows.Length][];
        for (int r = 0; r < rows.Length; r++)
        {
            float t = rows[r];
            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float s = i / (float)columns;
                float deg = Mathf.Lerp(open, 360f - open, s);
                float a = Mathf.Abs(Wrap(deg));
                float top = 1.585f - 0.052f * Smooth(120f, open, a);
                float y = Mathf.Lerp(1.492f, top, t);
                float radius = Mathf.Lerp(0.094f, 0.076f, Smooth(0f, 0.6f, t)) + 0.012f * Smooth(0.55f, 1f, t);
                float rad = deg * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Sin(rad) * radius, y, 0.012f + Mathf.Cos(rad) * radius * 1.02f);
                rings[r][i] = new ModelPoint(p, _plate.Uv(s, Mathf.Lerp(0.1f, 0.97f, t)), Chest.To(Neck, 0.5f * t));
            }
        }
        Kit.Sheet(rings, _collar.Uv(0.5f, 0.5f), new Vector3(0f, 1.54f, 0.012f));
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
            z += 0.019f * Bell(deg, 0f, 7.5f) * Bell(h, 0.35f, 0.055f);                      // nariz
            z += 0.008f * Bell(deg, 0f, 6f) * Smooth(0.36f, 0.42f, h) * (1f - Smooth(0.5f, 0.57f, h));
            z += 0.009f * Bell(h, 0.57f, 0.035f) * Bell(deg, 0f, 42f);                       // ceño
            z -= 0.009f * Bell(abs, 21f, 13f) * Bell(h, 0.5f, 0.04f);                        // cuenca del ojo
            z += 0.004f * Bell(deg, 0f, 16f) * Bell(h, 0.21f, 0.03f);                        // labios
            z += 0.011f * Bell(deg, 0f, 26f) * Bell(h, 0.07f, 0.06f);                        // barba del mentón
            x *= 1f + 0.05f * Bell(h, 0.37f, 0.06f) + 0.06f * Bell(h, 0.14f, 0.09f);         // pómulos y barba
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

    private Vector2 HeadUv(Vector3 p) =>
        _head.Uv(Mathf.InverseLerp(FaceLeft, FaceRight, p.x), Mathf.InverseLerp(FaceBottom, FaceTop, p.y));

    // =========================================================
    // BRAZOS
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
                     + 0.011f * Smooth(VambraceStart - 0.003f, VambraceStart, d);

        // El puño del guantelete se abre hacia el codo
        float flare = Mathf.Lerp(0.064f, 0.040f, Smooth(GauntletStart, 0.425f, d));
        return d >= GauntletStart ? flare : radius;
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

        // La cubierta del codo y la placa de oro del antebrazo
        Kit.BeginPiece(32f);
        AlongChain(chain, 0.234f, out Vector3 elbow, out Vector3 armAxis);
        Vector3 back = Vector3.ProjectOnPlane(Vector3.back, armAxis).normalized;
        Kit.Disc(elbow + back * 0.05f, back, armAxis, 0.05f, 0.058f, 0.024f, 10, _boss, ArmSkin(name, 0.234f));

        AlongChain(chain, 0.308f, out Vector3 forearm, out Vector3 foreAxis);
        Vector3 top = Vector3.ProjectOnPlane(Vector3.up, foreAxis).normalized;
        Kit.Disc(forearm + top * 0.054f, top, foreAxis, 0.026f, 0.036f, 0.008f, 6, _gold, ArmSkin(name, 0.308f));
    }

    private void BuildGauntlet(float side)
    {
        string name = SideName(side);
        ModelSkin hand = Kit.Bone("B-hand" + name);
        Vector3 wrist = Kit.Pos("B-hand" + name);
        Vector3 knuckle = (Kit.Pos("B-indexFinger01" + name) + Kit.Pos("B-pinky01" + name)) * 0.5f;
        Vector3 axis = (knuckle - wrist).normalized;
        Vector3 flat = Vector3.ProjectOnPlane(Vector3.up, axis).normalized;
        Vector3 wide = Vector3.Cross(axis, flat).normalized;
        float length = Vector3.Distance(wrist, knuckle);

        Kit.BeginPiece(45f);
        float[] along = { -0.05f, 0.3f, 0.7f, 1f, 1.1f };
        float[] widths = { 0.033f, 0.050f, 0.062f, 0.064f, 0.056f };
        float[] thick = { 0.030f, 0.030f, 0.027f, 0.022f, 0.016f };
        const int columns = 12;

        var rings = new ModelPoint[along.Length][];
        for (int r = 0; r < along.Length; r++)
        {
            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float a = i / (float)columns * Mathf.PI * 2f;
                Vector3 p = wrist + axis * (along[r] * length) + wide * (Mathf.Cos(a) * widths[r]) + flat * (Mathf.Sin(a) * thick[r]);
                rings[r][i] = new ModelPoint(p, _glove.Uv(i / (float)columns, 0.02f + 0.1f * r / (along.Length - 1f)), hand);
            }
        }
        Kit.Loft(rings, capEnd: true);

        // Los nudillos, en una placa
        Kit.BeginPiece(30f);
        Kit.Disc(wrist + axis * (length * 0.62f) + flat * 0.026f, flat, axis, 0.052f, 0.048f, 0.007f, 8, _plate, hand);

        foreach (string finger in new[] { "B-indexFinger", "B-middleFinger", "B-ringFinger", "B-pinky", "B-thumb" })
        {
            Kit.BeginPiece(40f);
            BuildFinger(finger, name, finger == "B-thumb" ? 0.0182f : 0.0152f, 6, (t, a) => _glove.Uv(a, 0.14f + t * 0.84f));
        }
    }

    // =========================================================
    // HOMBRERAS
    // =========================================================

    private ModelSkin PauldronSkin(string side, float d) =>
        Chest.To(Kit.Bone("B-shoulder" + side), Smooth(-0.11f, -0.05f, d))
             .To(Kit.Bone("B-upperArm" + side), Smooth(-0.05f, 0.06f, d) * 0.92f);

    private void BuildPauldron(float side)
    {
        string name = SideName(side);
        Vector3 joint = Kit.Pos("B-upperArm" + name);
        Vector3 axis = (Kit.Pos("B-forearm" + name) - joint).normalized;
        Vector3 up = Vector3.ProjectOnPlane(Vector3.up, axis).normalized;
        Vector3 front = Vector3.Cross(axis, up).normalized;
        if (front.z < 0f) front = -front;
        Vector3 origin = joint + up * 0.004f;

        // De abajo hacia arriba: cada lámina queda tapada por la de arriba
        Lame(origin, axis, up, front, name, 0.135f, 0.205f, 0.083f, 0.090f, 124f, _plate);
        Lame(origin, axis, up, front, name, 0.085f, 0.150f, 0.090f, 0.097f, 124f, _plate);
        Lame(origin, axis, up, front, name, 0.035f, 0.100f, 0.097f, 0.104f, 124f, _plate);

        // La cúpula de arriba, que se cierra hacia el cuello
        Kit.BeginPiece(30f);
        const int columns = 18;
        float[] stops = { -0.118f, -0.105f, -0.08f, -0.05f, -0.02f, 0.01f, 0.03f, 0.05f, 0.058f };
        const float halfArc = 128f;
        var rings = new ModelPoint[stops.Length][];
        for (int r = 0; r < stops.Length; r++)
        {
            float d = stops[r];
            float closing = Mathf.Sqrt(Mathf.Max(0.02f, 1f - Sq(Mathf.Min(0f, d - 0.015f) / 0.137f)));
            float radius = 0.108f * closing + (r == stops.Length - 1 ? -0.007f : 0f);
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

    private void Lame(Vector3 origin, Vector3 axis, Vector3 up, Vector3 front, string side, float from, float to,
                      float radiusFrom, float radiusTo, float halfArc, ModelRegion region)
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
                rings[r][i] = new ModelPoint(p, region.Uv(s, Mathf.Lerp(0.08f, 0.97f, t)), PauldronSkin(side, d));
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
        radius += 0.013f * Smooth(GreaveStart - 0.008f, GreaveStart, d);
        radius += 0.008f * Bell(abs, 0f, 19f) * Smooth(GreaveStart, GreaveStart + 0.03f, d);   // la arista de la greba
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

        // La rodillera
        Kit.BeginPiece(32f);
        AlongChain(chain, 0.412f, out Vector3 knee, out Vector3 legAxis);
        Vector3 forward = Vector3.ProjectOnPlane(Vector3.forward, legAxis).normalized;
        ModelSkin kneeSkin = Kit.Bone("B-thigh" + name).To(Kit.Bone("B-shin" + name), 0.55f);
        Kit.Disc(knee + forward * 0.07f, forward, -legAxis, 0.062f, 0.066f, 0.024f, 10, _boss, kneeSkin);
    }

    private void BuildSabaton(float side)
    {
        Kit.BeginPiece(38f);
        string name = SideName(side);
        ModelSkin foot = Kit.Bone("B-foot" + name);
        ModelSkin toe = Kit.Bone("B-toe" + name);
        float x = Kit.Pos("B-foot" + name).x;

        float[] z      = { -0.100f, -0.090f, -0.05f, 0.0f, 0.05f, 0.095f, 0.14f, 0.185f, 0.222f, 0.245f };
        float[] half   = { 0.028f, 0.046f, 0.054f, 0.056f, 0.056f, 0.058f, 0.060f, 0.054f, 0.038f, 0.012f };
        float[] bottom = { 0.016f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.004f, 0.014f };
        float[] top    = { 0.09f, 0.125f, 0.165f, 0.18f, 0.145f, 0.112f, 0.094f, 0.074f, 0.054f, 0.032f };
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
                rings[r][i] = new ModelPoint(p, _boot.Uv(0.5f + 0.45f * c, Mathf.Lerp(0.12f + 0.86f * Mathf.InverseLerp(-0.1f, 0.245f, z[r]), 0.04f, sole)), skin);
            }
        }
        Kit.Loft(rings, wrap: true, capStart: true, capEnd: true);
    }

    // =========================================================
    // CAPA Y TELA
    // =========================================================

    private ModelSkin CapeSkin(Vector3 p)
    {
        ModelSkin skin = Chest.To(Spine, Smooth(1.32f, 1.14f, p.y));
        skin = skin.To(Hips, Smooth(1.14f, 0.98f, p.y));

        // Abajo cada mitad acompaña a su pierna: así el talón no la atraviesa al correr
        float reach = 0.34f * Smooth(0.95f, 0.6f, p.y);
        float right = Smooth(-0.22f, 0.22f, p.x);
        skin = skin.To(Kit.Bone("B-thigh.R"), reach * right);
        return skin.To(Kit.Bone("B-thigh.L"), reach * (1f - right) / Mathf.Max(0.001f, 1f - reach * right));
    }

    private void BuildCape()
    {
        Kit.BeginPiece(30f);
        const int columns = 20, rows = 12;
        const float top = 1.50f, bottom = 0.47f;

        var hem = new float[columns + 1];
        for (int i = 0; i <= columns; i++) hem[i] = Range(-0.05f, 0.035f);

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
                float width = Mathf.Lerp(0.21f, 0.33f, Smooth(0f, 0.7f, t));
                float fold = Mathf.Sin(s * 9.5f + 0.9f) * 0.036f * Smooth(0.08f, 0.6f, t)
                           + Mathf.Sin(s * 19f + 2.1f) * 0.012f * Smooth(0.3f, 0.9f, t);
                float z = -0.135f - 0.11f * Smooth(0f, 1f, t) + s * s * Mathf.Lerp(0.085f, 0.05f, t) + fold;
                var p = new Vector3(s * width, y, z);
                rings[r][i] = new ModelPoint(p, _cloak.Uv(u, 1f - t), CapeSkin(p));
            }
        }
        Kit.Sheet(rings, _lining.Uv(0.75f, 0.5f));
    }

    // El cuello de la capa: tres vueltas de tela que bajan a un broche.
    private void BuildCowl()
    {
        const float broochDeg = -24f;

        CowlRoll(0.172f, 0.146f, 1.474f, 0.078f, 0.052f, 0.034f, broochDeg, 1.7f, 0.30f);
        CowlRoll(0.140f, 0.122f, 1.506f, 0.062f, 0.050f, 0.034f, broochDeg, 0.1f, 0.15f);

        // El broche
        Kit.BeginPiece(30f);
        float rad = broochDeg * Mathf.Deg2Rad;
        var center = new Vector3(Mathf.Sin(rad) * 0.175f, 1.402f, 0.022f + Mathf.Cos(rad) * 0.186f);
        Vector3 normal = new Vector3(Mathf.Sin(rad), 0.25f, Mathf.Cos(rad)).normalized;
        Kit.Disc(center, normal, Vector3.up, 0.036f, 0.046f, 0.016f, 6, _gold, Chest);
        Kit.Gem(center + normal * 0.016f, Vector3.up, normal, 0.016f, 0.010f, 0.007f, 6, _collar, Chest);
    }

    private void CowlRoll(float radiusX, float radiusZ, float y, float dip, float wide, float thick, float frontDeg, float phase, float strip)
    {
        Kit.BeginPiece(70f);
        const int steps = 30;
        var path = new Vector3[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            float deg = -180f + 360f * i / steps;
            float rad = deg * Mathf.Deg2Rad;
            float front = Bell(Mathf.Abs(Wrap(deg - frontDeg)), 0f, 62f);
            float wobble = Mathf.Sin(deg * 0.052f + phase) * 0.006f;
            path[i] = new Vector3(Mathf.Sin(rad) * (radiusX + wobble), y - dip * front + wobble, 0.022f + Mathf.Cos(rad) * (radiusZ + wobble));
        }

        Kit.Sweep(path, p => (p - new Vector3(0f, 1.38f, 0.022f)).normalized,
            t => new Vector2(wide * (0.95f + 0.1f * Mathf.Sin(t * 9f + phase)), thick), 8,
            (t, a) => _cloak.Uv(strip + 0.5f * t, 0.8f + 0.18f * Mathf.Abs(a * 2f - 1f)),
            (p, t) => Chest, capStart: true);
    }

    private ModelSkin SkirtSkin(Vector3 p, float t)
    {
        float reach = Mathf.Min(1f, Mathf.Pow(t, 0.8f) * 1.1f) * 0.95f * Smooth(1.03f, 0.84f, p.y);
        float right = Smooth(-0.05f, 0.05f, p.x);
        return Hips.To(Kit.Bone("B-thigh.R"), reach * right).To(Kit.Bone("B-thigh.L"), reach * (1f - right) / Mathf.Max(0.001f, 1f - reach * right));
    }

    private static Vector3 SkirtPoint(float y, float deg, float offset, float flare)
    {
        Vector3 d = TorsoDirection(deg);
        Vector3 p = TorsoPoint(Mathf.Max(y, 0.97f), deg, offset);
        p += d * (Mathf.Max(0f, 0.97f - y) * flare);
        p.y = y;
        return p;
    }

    // Solo a los costados: por detrás del tabardo y de la capa los atravesaría con las piernas abiertas.
    private void BuildMailSkirt()
    {
        MailSide(26f, 128f);
        MailSide(-128f, -26f);
    }

    private void MailSide(float fromDeg, float toDeg)
    {
        Kit.BeginPiece(30f);
        const int columns = 14, rows = 5;
        var rings = new ModelPoint[rows + 1][];
        for (int r = 0; r <= rows; r++)
        {
            float t = r / (float)rows;
            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float u = i / (float)columns;
                float deg = Mathf.Lerp(fromDeg, toDeg, u);
                float y = Mathf.Lerp(1.06f, 0.735f + 0.012f * Mathf.Sin(deg * 0.3f), t);
                Vector3 p = SkirtPoint(y, deg, 0.012f, 0.42f);
                rings[r][i] = new ModelPoint(p, _mail.Uv(u, 1f - t), SkirtSkin(p, t));
            }
        }
        Kit.Sheet(rings, _lining.Uv(0.25f, 0.5f));
    }

    // La caída de tela de adelante (atrás va la capa). El centro sigue a las dos piernas por igual: así
    // con las piernas abiertas cuelga derecha y no se estira como una sábana.
    private ModelSkin TabardSkin(Vector3 p, float t)
    {
        float reach = Mathf.Min(1f, Mathf.Pow(t, 0.8f) * 1.1f) * 0.8f * Smooth(1.03f, 0.84f, p.y);
        float right = Smooth(-0.17f, 0.17f, p.x);
        return Hips.To(Kit.Bone("B-thigh.R"), reach * right).To(Kit.Bone("B-thigh.L"), reach * (1f - right) / Mathf.Max(0.001f, 1f - reach * right));
    }

    private void BuildTabard()
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
                float deg = s * Mathf.Lerp(36f, 30f, t);
                float hem = 0.57f + 0.10f * Mathf.Abs(s);
                float y = Mathf.Lerp(1.05f, hem, t);
                Vector3 p = SkirtPoint(y, deg, 0.024f + 0.006f * Mathf.Sin(s * 7f) * t, 0.42f);
                p += Vector3.forward * (0.012f * t);
                rings[r][i] = new ModelPoint(p, _tabard.Uv(u, 1f - t), TabardSkin(p, t));
            }
        }
        Kit.Sheet(rings, _lining.Uv(0.75f, 0.5f));
    }

    // Las escarcelas: láminas de acero sobre el muslo.
    private void BuildTasset(float side)
    {
        string name = SideName(side);
        Vector3[] chain = LegChain(name);
        float[,] plates = { { 0.135f, 0.225f }, { 0.07f, 0.16f }, { 0.0f, 0.095f } };

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
                AlongChain(chain, d, out Vector3 center, out Vector3 tangent);
                Vector3 front = Vector3.ProjectOnPlane(Vector3.forward, tangent).normalized;
                Vector3 outer = Vector3.Cross(front, tangent).normalized;
                if (Mathf.Sign(outer.x) != Mathf.Sign(side)) outer = -outer;

                float radius = LegRadius.At(d) + 0.042f + 0.008f * t - (r == rows.Length - 1 ? 0.007f : 0f);
                rings[r] = new ModelPoint[columns + 1];
                for (int i = 0; i <= columns; i++)
                {
                    float s = i / (float)columns;
                    float a = Mathf.Lerp(-30f, 118f, s) * Mathf.Deg2Rad;
                    float tuck = 0.02f * Sq(s * 2f - 1f) * t;
                    Vector3 p = center - tangent * tuck + front * (Mathf.Cos(a) * radius) + outer * (Mathf.Sin(a) * radius);
                    ModelSkin skin = Hips.To(Kit.Bone("B-thigh" + name), Smooth(-0.04f, 0.06f, d));
                    rings[r][i] = new ModelPoint(p, _plate.Uv(s, Mathf.Lerp(0.08f, 0.97f, t)), skin);
                }
            }
            AlongChain(chain, (from + to) * 0.5f, out Vector3 inside, out _);
            Kit.Sheet(rings, _lining.Uv(0.25f, 0.5f), inside);
        }
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

    private void BuildBelts()
    {
        Func<float, float> waist = deg => 1.085f;
        Func<float, float> hip = deg => 1.03f + 0.03f * Mathf.Cos((deg + 40f) * Mathf.Deg2Rad);
        Band(waist, 0.052f, 0.018f);
        Band(hip, 0.048f, 0.032f);

        // Hebillas: chapas de oro
        Kit.BeginPiece(30f);
        Vector3 up = (Vector3.up + Vector3.right).normalized;
        Kit.Disc(TorsoPoint(1.085f, 0f, 0.026f), Vector3.forward, up, 0.050f, 0.050f, 0.012f, 4, _gold, Spine);
        Kit.Disc(TorsoPoint(hip(22f), 22f, 0.04f), TorsoDirection(22f).normalized, up, 0.046f, 0.046f, 0.012f, 4, _gold, Hips);
    }

    // =========================================================
    // PELO
    // =========================================================

    private void BuildHair()
    {
        BuildHairCap();

        // Atrás: hasta los hombros, por encima del cuello alto
        const int deep = 14;
        for (int i = 0; i < deep; i++)
        {
            float s = i / (float)(deep - 1) * 2f - 1f;
            float deg = 180f + s * 96f;
            Vector3 root = HeadPoint(Range(0.5f, 0.8f), deg, 0.002f);
            var tip = new Vector3(s * 0.135f + Range(-0.012f, 0.012f), Range(1.45f, 1.53f), -0.135f + s * s * 0.05f);
            Lock(root, root + Outward(root) * 0.05f + Vector3.down * 0.04f,
                new Vector3(s * 0.125f, 1.60f, -0.125f + s * s * 0.03f), tip, Range(0.05f, 0.062f), Range(0.018f, 0.022f), 8);
        }

        // A los lados de la cara
        foreach (float side in new[] { 1f, -1f })
            for (int i = 0; i < 5; i++)
            {
                float deg = side * Mathf.Lerp(58f, 112f, i / 4f);
                Vector3 root = HeadPoint(Range(0.6f, 0.76f), deg, 0.004f);
                var tip = new Vector3(side * (0.102f + 0.012f * i), Range(1.50f, 1.56f), 0.085f - 0.04f * i);
                Lock(root, root + Outward(root) * 0.03f + Vector3.down * 0.05f,
                    new Vector3(side * (0.098f + 0.01f * i), 1.63f, 0.08f - 0.04f * i), tip, Range(0.036f, 0.044f), Range(0.012f, 0.015f), 8);
            }

        // De la raya hacia cada lado, despejando la frente
        const float part = -7f;
        for (int i = 0; i < 8; i++)
        {
            float side = i < 4 ? 1f : -1f;
            float s = (i % 4) / 3f;
            float startDeg = part + side * Mathf.Lerp(3f, 24f, s);
            Vector3 root = HeadPoint(Mathf.Lerp(0.95f, 0.86f, s), startDeg, 0.014f);
            float endDeg = side * Mathf.Lerp(62f, 92f, s);
            float endH = Mathf.Lerp(0.52f, 0.36f, s) + Range(-0.03f, 0.03f);
            Vector3 mid = HeadPoint(0.8f, Mathf.Lerp(startDeg, endDeg, 0.55f), 0.03f);
            Lock(root, mid, HeadPoint(Mathf.Lerp(0.8f, endH, 0.5f), endDeg, 0.02f), HeadPoint(endH, endDeg, 0.012f),
                Range(0.024f, 0.03f), 0.011f, 8);
        }
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
        Atlas.Paint(_cloak, PaintCloak);
        Atlas.Paint(_tabard, PaintTabard);
        Atlas.Paint(_mail, (u, v) => MailRings(u * 26f, v * 22f));
        Atlas.Paint(_plate, PaintPlate);
        Atlas.Paint(_dome, PaintDome);
        Atlas.Paint(_boot, PaintSabaton);
        Atlas.Paint(_belt, PaintBelt);
        Atlas.Paint(_glove, PaintGlove);
        Atlas.Paint(_gold, (u, v) => Color.Lerp(GoldDark, Gold * 1.15f, v * 0.7f + 0.3f * u));
        Atlas.Paint(_steel, (u, v) => Color.Lerp(SteelDark, SteelLight, v));
        Atlas.Paint(_lining, (u, v) => u < 0.5f ? SteelDark * 0.6f : CloakTone * 0.7f);
        Atlas.Paint(_collar, (u, v) => CollarBlue);
        Atlas.Paint(_boss, PaintBoss);
    }

    // El acero: un brillo pintado que no depende de la luz de la escena.
    private static Color SteelAt(float u, float v, float sheen)
    {
        Color c = Color.Lerp(SteelDark, Steel, 0.3f + 0.7f * sheen);
        c = Over(c, SteelLight, 0.6f * Smooth(0.74f, 1f, sheen));
        c *= 1f + (Mathf.PerlinNoise(u * 9f + 1f, v * 9f + 2f) - 0.5f) * 0.16f;     // manchas
        c *= 1f + (Mathf.PerlinNoise(u * 180f, v * 12f) - 0.5f) * 0.05f;            // cepillado

        // Rayones: las curvas de nivel de un ruido dan líneas finas y torcidas
        float scratch = Mathf.Abs(Mathf.PerlinNoise(u * 26f + 5f, v * 20f + 9f) - 0.5f);
        float lines = Smooth(0.0045f, 0.0015f, scratch) * Smooth(0.42f, 0.6f, Mathf.PerlinNoise(u * 7f + 3f, v * 7f));
        return Over(c, SteelLight, 0.5f * lines);
    }

    private static Color MailRings(float x, float y)
    {
        float row = Mathf.Floor(y);
        float shift = Mathf.Repeat(row, 2f) * 0.5f;
        float cx = Mathf.Repeat(x + shift, 1f) - 0.5f;
        float cy = Mathf.Repeat(y, 1f) - 0.5f;
        float ring = Mathf.Sqrt(cx * cx + cy * cy * 1.3f);
        float metal = Smooth(0.2f, 0.3f, ring) * Smooth(0.52f, 0.42f, ring);
        Color c = Over(MailDark, Mail, metal);
        return Over(c, SteelLight, 0.5f * metal * Smooth(-0.1f, -0.4f, cy));
    }

    private static Color PaintHead(float u, float v)
    {
        float x = Mathf.Lerp(FaceLeft, FaceRight, u);
        float y = Mathf.Lerp(FaceBottom, FaceTop, v);
        float ax = Mathf.Abs(x);

        Color c = Grain(SkinTone, u, v, 24f, 0.06f);
        c = Over(c, SkinShade, 0.45f * Smooth(0.056f, 0.085f, ax));
        c = Over(c, new Color(0.80f, 0.36f, 0.27f, 0.2f), 0.2f * Bell(ax, 0.05f, 0.02f) * Bell(y, 1.655f, 0.016f));

        // Sombras de la cara: ojos hundidos y ceño
        c = Over(c, SkinShade, 0.7f * Bell(ax, 0.036f, 0.026f) * Bell(y, 1.692f, 0.011f));
        c = Over(c, SkinShade, 0.3f * Bell(ax, 0.009f, 0.004f) * Bell(y, 1.665f, 0.022f));
        c = Over(c, SkinShade, 0.35f * Bell(x, 0f, 0.007f) * Bell(y, 1.706f, 0.008f));
        c = Over(c, SkinShade, 0.7f * Bell(x, 0f, 0.011f) * Bell(y, 1.6325f, 0.0035f));
        c = Over(c, SkinShade, 0.22f * Bell(ax, 0.06f, 0.012f) * Bell(y, 1.66f, 0.03f));
        c = Over(c, SkinShade, 0.55f * Smooth(1.735f, 1.775f, y));                                       // sombra del pelo
        c = Over(c, SkinShade * 0.8f, 0.8f * Bell(ax, 0.0105f, 0.0032f) * Bell(y, 1.6365f, 0.0024f));    // fosas de la nariz
        c = Over(c, SkinShade, 0.4f * Bell(ax, 0.0165f, 0.003f) * Bell(y, 1.643f, 0.008f));              // aletas
        c = Over(c, new Color(0.96f, 0.72f, 0.56f, 0.3f), 0.5f * Bell(x, 0f, 0.0045f) * Bell(y, 1.66f, 0.028f));

        // Una cicatriz que cruza la ceja
        float scar = Smooth(0.0016f, 0.0008f, Mathf.Abs((x + 0.046f) - (y - 1.70f) * 0.34f)) * Smooth(1.67f, 1.675f, y) * Smooth(1.726f, 1.72f, y);
        c = Over(c, new Color(0.62f, 0.30f, 0.26f, 0.2f), 0.75f * scar);

        // Boca
        float lipY = y - 1.6095f + 1.2f * ax * ax;
        const float lipHalf = 0.0195f;
        float upper = Smooth(0f, 0.001f, lipY) * Smooth(0.0044f - 0.0036f * ax / lipHalf, 0.0034f - 0.0036f * ax / lipHalf, lipY);
        float lower = Smooth(0f, -0.001f, lipY) * Smooth(-0.0072f + 0.0066f * ax / lipHalf, -0.0062f + 0.0066f * ax / lipHalf, lipY);
        float inside = Smooth(lipHalf, lipHalf - 0.0015f, ax);
        c = Over(c, new Color(0.55f, 0.27f, 0.23f, 0.3f), upper * inside);
        c = Over(c, new Color(0.66f, 0.35f, 0.29f, 0.3f), lower * inside);
        c = Over(c, new Color(0.17f, 0.07f, 0.06f, 0.1f), Smooth(0.0012f, 0.0005f, Mathf.Abs(lipY)) * inside);

        // Barba y bigote: todo lo que está bajo la línea, menos la boca
        float noise = Mathf.PerlinNoise(u * 150f, v * 34f) - 0.5f;
        float top = BeardTop.At(ax) + noise * 0.006f;
        float beard = Smooth(top + 0.0035f, top - 0.0035f, y);
        float mouthX = ax / 0.0205f, mouthY = (y - 1.6078f + 1.2f * ax * ax) / 0.0072f;
        float mouth = Smooth(0.82f, 1.0f, Mathf.Sqrt(mouthX * mouthX + mouthY * mouthY));
        float soul = Bell(x, 0f, 0.008f) * Bell(y, 1.593f, 0.0065f);
        float hair = beard * Mathf.Max(mouth, soul * 0.9f);
        Color strands = Color.Lerp(HairRoot, HairTip * 1.15f, Mathf.PerlinNoise(u * 210f, v * 26f));
        c = Over(c, strands, hair);

        // Sombra de barba arriba de la línea
        c = Over(c, HairMid, 0.22f * Smooth(top + 0.018f, top + 0.002f, y) * Smooth(0.03f, 0.05f, ax));

        // Ojos: chicos, entrecerrados, con el párpado pesado
        float ex = (ax - 0.0355f) / 0.0172f;
        float ey = (y - 1.6825f - (ax - 0.0355f) * 0.13f) / 0.0056f;
        float open = 1f - ex * ex;
        if (open > 0f)
        {
            float lid = Mathf.Sqrt(open);
            float eye = Smooth(lid * 1.02f, lid * 0.92f, Mathf.Abs(ey));
            float ix = (ax - 0.0345f) / 0.0072f;
            float iy = (y - 1.6832f) / 0.0072f;
            float iris = Mathf.Sqrt(ix * ix + iy * iy);

            Color ball = new Color(0.86f, 0.84f, 0.80f, 0.8f);
            ball = Over(ball, Color.Lerp(new Color(0.20f, 0.42f, 0.56f, 0.9f), new Color(0.06f, 0.14f, 0.24f, 0.9f), iris), Smooth(1.05f, 0.95f, iris));
            ball = Over(ball, new Color(0.02f, 0.02f, 0.04f, 0.9f), Smooth(0.5f, 0.42f, iris));
            ball = Over(ball, Color.white, Smooth(0.26f, 0.18f, Vector2.Distance(new Vector2(ix, iy), new Vector2(-0.3f, 0.35f))));
            ball = Over(ball, SkinShade * 0.6f, Smooth(0.2f, 1f, ey) * 0.55f);   // el párpado le da sombra al ojo
            c = Over(c, ball, eye);
        }
        float upperLine = Mathf.Abs(ey - Mathf.Sqrt(Mathf.Max(0f, open)));
        c = Over(c, new Color(0.09f, 0.05f, 0.04f, 0.2f), Smooth(0.5f, 0.24f, upperLine) * Smooth(1.25f, 1.02f, Mathf.Abs(ex)) * Smooth(-0.3f, 0.1f, ey));

        // Cejas: gruesas y bajas hacia el centro
        float bx = ax - 0.012f;
        float browY = 1.6955f + bx * 0.34f - bx * bx * 3.2f;
        float browThick = Mathf.Lerp(0.0048f, 0.002f, Smooth(0f, 0.046f, bx));
        float browNoise = (Mathf.PerlinNoise(u * 260f, v * 90f) - 0.5f) * 0.0018f;
        float brow = Smooth(browThick + 0.0012f, browThick, Mathf.Abs(y - browY) + browNoise) * Smooth(0f, 0.004f, bx) * Smooth(0.05f, 0.044f, bx);
        return Over(c, HairRoot * 1.3f, brow);
    }

    private static Color PaintTorso(float u, float v)
    {
        float deg = (u - 0.5f) * 360f;
        float abs = Mathf.Abs(deg);
        float y = Mathf.Lerp(TorsoBottom, TorsoTop, v);

        // Debajo del acero: tela azul al cuello, cuero en la cintura, piel arriba
        Color c = Grain(CollarBlue, u * 3f, v, 80f, 0.3f);
        c = Over(c, Grain(LeatherDark, u * 3f, v, 80f, 0.4f), Smooth(1.26f, 1.2f, y));
        c = Over(c, Grain(SkinTone, u * 3f, v, 30f, 0.05f), Smooth(1.532f, 1.538f, y));
        c = Over(c, SkinShade, 0.6f * Smooth(1.6f, 1.55f, y) * Smooth(1.53f, 1.55f, y));

        float bottom = CuirassBottom(deg), top = CuirassTop(deg);
        float inside = Smooth(bottom - 0.002f, bottom + 0.002f, y) * Smooth(top + 0.002f, top - 0.002f, y);
        if (inside <= 0f) return c;

        // Brillos del peto, como si reflejara el cielo
        float sheen = 0.5f + 0.42f * Bell(abs, 30f, 17f) * Bell(y, 1.37f, 0.11f) + 0.2f * Bell(abs, 5f, 8f) * Smooth(1.1f, 1.3f, y)
                    - 0.28f * Smooth(1.30f, 1.14f, y) - 0.22f * Smooth(60f, 130f, abs);
        Color plate = SteelAt(u * 3f, v, Mathf.Clamp01(sheen));
        plate *= 1f + (Mathf.PerlinNoise(u * 11f, v * 7f) - 0.5f) * 0.14f;

        // La arista del centro y la línea de la espalda
        plate = Over(plate, SteelLight, 0.55f * Bell(deg, 1.8f, 1.5f) * Smooth(1.14f, 1.2f, y));
        plate = Over(plate, SteelDark, 0.6f * Bell(deg, -1.8f, 1.5f) * Smooth(1.14f, 1.2f, y));
        plate = Over(plate, SteelDark, 0.5f * Bell(abs, 180f, 1.6f));

        // Los bordes: oro, una línea grabada y tachuelas
        float toBottom = y - bottom, toTop = top - y;
        float edge = Mathf.Min(toBottom, toTop);
        Color gold = Color.Lerp(GoldDark, Gold * 1.12f, 0.35f + 0.65f * Mathf.PerlinNoise(u * 60f, v * 40f));
        plate = Over(plate, SteelDark * 0.8f, Smooth(0.0225f, 0.02f, edge) * Smooth(0.0165f, 0.019f, edge));
        plate = Over(plate, gold, Smooth(0.0135f, 0.0115f, edge));
        plate = Over(plate, GoldDark, Smooth(0.0035f, 0.0015f, edge));

        // Las puntas de oro del pecho, una dentro de la otra
        float slope = (y - 1.33f) / 0.15f;
        for (int i = 0; i < 2; i++)
        {
            float line = 15f + 26f * slope - i * 9f;
            float mask = Smooth(1.9f, 1.3f, Mathf.Abs(abs - line)) * Smooth(1.285f + i * 0.03f, 1.29f + i * 0.03f, y) * Smooth(1.47f, 1.45f, y);
            plate = Over(plate, gold, mask);
        }

        // La placa de abajo: sube en punta por el centro, con su borde y sus remaches
        float seam = 1.30f - 0.0016f * abs - (abs > 80f ? (abs - 80f) * 0.0002f : 0f);
        float below = y - seam;
        plate = Over(plate, SteelDark * 0.5f, Smooth(0.0034f, 0.0014f, Mathf.Abs(below)));
        plate = Over(plate, SteelLight, 0.5f * Smooth(0.0084f, 0.0046f, below) * Smooth(-0.0014f, 0.0016f, below) * Smooth(100f, 50f, abs));
        plate = Over(plate, SteelDark, 0.35f * Smooth(0f, -0.018f, below));

        float rivetX = (Mathf.Repeat(abs + 6f, 16f) - 8f) * 0.0032f;
        float rivet = Smooth(0.0052f, 0.0036f, Mathf.Sqrt(rivetX * rivetX + Sq(below + 0.011f)));
        plate = Over(plate, Color.Lerp(SteelLight, Gold, 0.6f), rivet * Smooth(4f, 8f, abs));

        return Over(c, plate, inside);
    }

    private static Color PaintArm(float u, float v)
    {
        float d = Mathf.Lerp(-0.05f, 0.446f, v);
        float deg = (u - 0.5f) * 360f;
        float abs = Mathf.Abs(deg);

        // Manga de malla
        Color c = MailRings(u * 34f, v * 44f);
        c = Over(c, MailDark, 0.45f * Smooth(0.12f, 0.03f, d));

        float sheen = 0.46f + 0.4f * Bell(abs, 26f, 26f) - 0.28f * Smooth(90f, 170f, abs);

        // Codera
        float couter = Smooth(CouterStart - 0.002f, CouterStart + 0.002f, d) * Smooth(VambraceStart + 0.002f, VambraceStart - 0.002f, d);
        if (couter > 0f)
        {
            Color plate = SteelAt(u * 2f, v, Mathf.Clamp01(sheen - 0.08f));
            plate = Over(plate, SteelDark, Smooth(0.006f, 0.002f, Mathf.Abs(d - 0.234f)) * 0.8f);
            plate = Over(plate, Gold, Smooth(0.006f, 0.004f, d - CouterStart));
            c = Over(c, plate, couter);
        }

        // Brazal
        float vambrace = Smooth(VambraceStart - 0.002f, VambraceStart + 0.002f, d) * Smooth(GauntletStart + 0.002f, GauntletStart - 0.002f, d);
        if (vambrace > 0f)
        {
            Color plate = SteelAt(u * 2f, v, Mathf.Clamp01(sheen));
            plate = Over(plate, SteelLight, 0.5f * Bell(deg, 2f, 3f));

            // Dos correas por debajo
            float strap = Mathf.Min(Mathf.Abs(d - 0.283f), Mathf.Abs(d - 0.335f));
            Color leather = Over(Grain(Leather, u * 3f, v, 60f, 0.4f), LeatherDark, Smooth(0.006f, 0.0085f, strap));
            plate = Over(plate, leather, Smooth(0.0095f, 0.0075f, strap) * Smooth(40f, 54f, abs));
            c = Over(c, plate, vambrace);
        }

        // Puño del guantelete
        float cuff = Smooth(GauntletStart - 0.002f, GauntletStart + 0.002f, d);
        if (cuff > 0f)
        {
            Color plate = SteelAt(u * 2f, v, Mathf.Clamp01(sheen - 0.1f + 0.22f * Smooth(GauntletStart, 0.42f, d)));
            plate = Over(plate, Gold, Smooth(0.0105f, 0.0085f, d - GauntletStart));
            plate = Over(plate, GoldDark, Smooth(0.0025f, 0.001f, d - GauntletStart));
            plate = Over(plate, SteelDark, Smooth(0.0035f, 0.0015f, Mathf.Abs(d - 0.398f)));
            c = Over(c, plate, cuff);
        }
        return c;
    }

    private static Color PaintLeg(float u, float v)
    {
        float d = Mathf.Lerp(-0.07f, 0.82f, v);
        float deg = (u - 0.5f) * 360f;
        float abs = Mathf.Abs(deg);

        // Pantalón oscuro
        Color c = Grain(Trousers, u * 2f, v, 70f, 0.6f);
        c *= 1f + (Mathf.PerlinNoise(u * 6f, v * 22f) - 0.5f) * 0.5f;
        c = Over(c, Trousers * 0.4f, 0.5f * Smooth(0.14f, 0.0f, d));

        float sheen = 0.46f + 0.42f * Bell(abs, 22f, 26f) - 0.3f * Smooth(70f, 150f, abs);

        // Quijote del muslo: solo el frente
        float cuisse = Smooth(CuisseStart - 0.002f, CuisseStart + 0.002f, d) * Smooth(PoleynStart + 0.002f, PoleynStart - 0.002f, d) * Smooth(102f, 94f, abs);
        if (cuisse > 0f)
        {
            Color plate = SteelAt(u * 2f, v, Mathf.Clamp01(sheen - 0.05f));
            float edge = Mathf.Min(d - CuisseStart, (98f - abs) * 0.0026f);
            plate = Over(plate, Gold, Smooth(0.0085f, 0.0065f, edge));
            plate = Over(plate, SteelDark, Smooth(0.016f, 0.014f, edge) * Smooth(0.0115f, 0.0135f, edge));
            c = Over(c, plate, cuisse);
        }

        // Rodilla
        float poleyn = Smooth(PoleynStart - 0.002f, PoleynStart + 0.002f, d) * Smooth(GreaveStart + 0.002f, GreaveStart - 0.002f, d);
        if (poleyn > 0f)
        {
            Color plate = SteelAt(u * 2f, v, Mathf.Clamp01(sheen - 0.12f));
            plate = Over(plate, SteelDark, Smooth(0.0045f, 0.002f, Mathf.Abs(d - 0.388f)));
            plate = Over(plate, SteelDark, Smooth(0.0045f, 0.002f, Mathf.Abs(d - 0.438f)));
            plate = Over(plate, LeatherDark, Smooth(120f, 135f, abs));
            c = Over(c, plate, poleyn);
        }

        // Greba: de acero adelante, cerrada con correas atrás
        float greave = Smooth(GreaveStart - 0.002f, GreaveStart + 0.002f, d);
        if (greave > 0f)
        {
            Color plate = SteelAt(u * 2f, v, Mathf.Clamp01(sheen));
            plate = Over(plate, SteelLight, 0.45f * Bell(deg, 3f, 4f));
            plate = Over(plate, SteelDark, 0.55f * Bell(deg, -4f, 4f));
            plate = Over(plate, Gold, Smooth(0.0115f, 0.0095f, d - GreaveStart));
            plate = Over(plate, SteelDark, Smooth(0.0035f, 0.0015f, Mathf.Abs(d - 0.752f)));

            float strap = Mathf.Abs(Mathf.Repeat(d, 0.09f) - 0.045f);
            Color back = Over(Grain(LeatherDark, u * 3f, v, 60f, 0.5f), Leather, Smooth(0.016f, 0.012f, strap));
            plate = Over(plate, back, Smooth(134f, 142f, abs));
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
        c *= 0.72f + 0.42f * strands + 0.2f * (fine - 0.5f);
        return Over(c, new Color(0.46f, 0.26f, 0.12f, 0.2f), 0.5f * Smooth(0.66f, 0.82f, fine) * Bell(v, 0.5f, 0.3f));
    }

    private static Color PaintCloak(float u, float v)
    {
        Color c = Grain(CloakTone, u, v, 110f, 0.35f);
        c *= 0.72f + 0.75f * Mathf.PerlinNoise(u * 14f, v * 1.3f + 4f);
        c = Over(c, CloakTone * 0.45f, 0.45f * Smooth(0.22f, 0f, v));

        // Los pliegues, con la misma onda de la malla (BuildCape): oscurece los valles y aclara las crestas
        float s = u * 2f - 1f;
        float depth = Smooth(0.95f, 0.4f, v);
        float folds = Mathf.Cos(s * 9.5f + 0.9f) * 0.8f + Mathf.Cos(s * 19f + 2.1f) * 0.45f * Smooth(0.7f, 0.1f, v);
        c *= 1f + folds * 0.34f * depth;

        // La marca de la espalda: dos puntas, como las del peto
        float abs = Mathf.Abs(u - 0.5f);
        float mark = Mathf.Min(Mathf.Abs((v - 0.64f) - abs * 0.62f), Mathf.Abs((v - 0.57f) - abs * 0.62f));
        c = Over(c, Gold * 0.72f, Smooth(0.0095f, 0.0065f, mark) * Smooth(0.2f, 0.185f, abs));

        // La guarda de abajo
        float line = Mathf.Min(Mathf.Abs(v - 0.075f), Mathf.Abs(v - 0.125f));
        return Over(c, GoldDark * 1.2f, Smooth(0.0065f, 0.004f, line));
    }

    private static Color PaintTabard(float u, float v)
    {
        Color c = Grain(Navy, u, v, 130f, 0.4f);
        c *= 0.8f + 0.5f * Mathf.PerlinNoise(u * 18f, v * 1.4f);

        // Borde de oro y una punta como la del peto
        float side = Mathf.Min(u, 1f - u);
        float edge = Mathf.Min(side * 0.55f, v);
        c = Over(c, Gold * 0.92f, Smooth(0.03f, 0.024f, edge));
        c = Over(c, GoldDark, Smooth(0.012f, 0.006f, edge));

        float abs = Mathf.Abs(u - 0.5f);
        float chevron = Mathf.Abs((v - 0.70f) - abs * 0.5f);
        return Over(c, Gold * 0.92f, Smooth(0.02f, 0.014f, chevron) * Smooth(0.33f, 0.3f, abs));
    }

    private static Color PaintPlate(float u, float v)
    {
        float arc = Mathf.Abs(u - 0.5f) * 2f;
        float sheen = 0.5f + 0.42f * Bell(arc, 0.28f, 0.26f) * Smooth(0.05f, 0.5f, v) - 0.34f * Smooth(0.6f, 1f, arc);
        Color c = SteelAt(u, v, Mathf.Clamp01(sheen));

        c = Over(c, SteelDark, Smooth(0.2f, 0.02f, v) * 0.7f);

        float rx = (Mathf.Repeat(u * 7f, 1f) - 0.5f) / 7f;
        float rivet = Smooth(0.031f, 0.022f, Mathf.Sqrt(rx * rx + Sq(v - 0.7f)));
        c = Over(c, Color.Lerp(SteelLight, Gold, 0.5f), rivet);

        c = Over(c, Gold, Smooth(0.82f, 0.85f, v));
        c = Over(c, GoldDark, Smooth(0.95f, 0.98f, v));
        return Over(c, SteelDark, Smooth(0.03f, 0.015f, Mathf.Abs(v - 0.8f)) * 0.8f);
    }

    private static Color PaintDome(float u, float v)
    {
        float x = (u - 0.5f) * 2f;
        float y = (v - 0.5f) * 2f;
        float sheen = 0.46f + 0.42f * Bell(Mathf.Abs(x), 0.3f, 0.3f) - 0.3f * Smooth(0.6f, 1f, Mathf.Abs(x));
        Color c = SteelAt(u, v, Mathf.Clamp01(sheen));

        // Una hoja de oro en el centro
        float leaf = Mathf.Abs(x) * 2.1f + Mathf.Abs(y) * 1.2f;
        float mask = Smooth(1.0f, 0.94f, leaf);
        c = Over(c, Color.Lerp(GoldDark, Gold * 1.15f, 0.4f + 0.6f * Mathf.PerlinNoise(u * 30f, v * 30f)), mask);
        c = Over(c, GoldDark, Smooth(0.6f, 0.64f, leaf) * Smooth(0.7f, 0.66f, leaf));
        c = Over(c, GoldDark, Bell(x, 0f, 0.02f) * mask);

        c = Over(c, Gold, Smooth(0.9f, 0.93f, v));
        return Over(c, GoldDark, Smooth(0.975f, 0.99f, v));
    }

    // Los tachones: acero con un aro de oro. Kit.Disc pinta cada anillo con su v.
    private static Color PaintBoss(float u, float v)
    {
        Color c = Color.Lerp(SteelDark, SteelLight, v * v);
        return Over(c, Gold, Smooth(0.78f, 0.8f, v) * Smooth(0.88f, 0.86f, v));
    }

    private static Color PaintSabaton(float u, float v)
    {
        float sheen = 0.5f + 0.38f * Bell(Mathf.Abs(u - 0.5f), 0.14f, 0.16f) - 0.32f * Smooth(0.25f, 0.47f, Mathf.Abs(u - 0.5f));
        sheen = Mathf.Lerp(0.4f, sheen, Smooth(0.2f, 0.4f, v));   // el talón va parejo: ahí todas las u se juntan
        Color c = SteelAt(u, v, Mathf.Clamp01(sheen));

        // Las láminas del pie y la punta
        float lame = Mathf.Repeat(v * 9f, 1f);
        c = Over(c, SteelDark * 0.7f, Smooth(0.1f, 0.03f, lame) * Smooth(0.5f, 0.55f, v) * Smooth(0.9f, 0.85f, v));
        c = Over(c, Gold, Smooth(0.86f, 0.885f, v) * Smooth(0.925f, 0.905f, v));
        return Over(c, new Color(0.08f, 0.06f, 0.05f, 0.1f), Smooth(0.1f, 0.07f, v));
    }

    private static Color PaintBelt(float u, float v)
    {
        Color c = Grain(Leather, u * 6f, v, 40f, 0.45f);
        float edge = Mathf.Abs(v - 0.5f) * 2f;
        c = Over(c, LeatherDark, Smooth(0.75f, 1f, edge));
        float stitch = Smooth(0.08f, 0.04f, Mathf.Abs(edge - 0.68f)) * Smooth(0.28f, 0.2f, Mathf.Abs(Mathf.Repeat(u * 150f, 1f) - 0.5f));
        return Over(c, new Color(0.72f, 0.50f, 0.28f, 0.3f), stitch);
    }

    // La parte de abajo de la franja es la palma y el resto los dedos: placas sobre cuero.
    private static Color PaintGlove(float u, float v)
    {
        float angle = u * Mathf.PI * 2f;
        float back = Smooth(-0.25f, 0.2f, Mathf.Sin(angle));
        Color leather = Grain(LeatherDark, u, v, 60f, 0.5f);

        if (v < 0.13f)
            return Over(leather, SteelAt(u, v, 0.55f), back);

        // Cada falange, una placa
        float t = (v - 0.14f) / 0.84f;
        float plate = Mathf.Repeat(t * 3.6f, 1f);
        Color steel = SteelAt(u, v, 0.35f + 0.4f * plate);
        steel = Over(steel, SteelDark * 0.7f, Smooth(0.14f, 0.05f, plate));
        return Over(leather, steel, back);
    }
}
