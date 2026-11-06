using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ============================================================
// RogueModel
//
// La receta del modelo de relleno del Pícaro (referencias elegidas por Gustavo el 6 de noviembre de
// 2026): una gata tabaxi con el pelaje de un gato tuxedo, negro con hocico, pecho y patas blancas.
// Capucha con las orejas afuera, bufanda, chaleco de cuero, capa corta, botas abiertas y cola larga.
// Sin dagas: son de la clase.
//
// El acabado es Matte: es pelo, tela y cuero, nada que tenga que brillar.
//
// La cola no tiene huesos propios: reparte su peso entre la cadera, la columna y una pierna, y así se
// mueve con el cuerpo sin ningún script (ver TailSkin).
//
// Mercenarios ▸ Modelos de clase ▸ Generar la Pícara: rehace malla, textura y prefab.
// ============================================================
public sealed class RogueModel : ClassModelRecipe
{
    private const string Name = "Rogue";
    private const string ArtFolder = "Assets/Art/Models/Classes/Rogue";
    private const string PrefabFolder = "Assets/Prefabs/Player/Classes";

    // ---------- Colores ----------
    private static readonly Color FurBlack     = new Color(0.050f, 0.050f, 0.070f);
    private static readonly Color FurSheen     = new Color(0.190f, 0.200f, 0.270f);
    private static readonly Color FurWhite     = new Color(0.950f, 0.930f, 0.890f);
    private static readonly Color FurShade     = new Color(0.600f, 0.600f, 0.670f);
    private static readonly Color NoseTone     = new Color(0.950f, 0.470f, 0.540f);
    private static readonly Color HoodTone     = new Color(0.025f, 0.085f, 0.115f);
    private static readonly Color HoodDark     = new Color(0.008f, 0.028f, 0.042f);
    private static readonly Color ScarfTone    = new Color(0.020f, 0.250f, 0.270f);
    private static readonly Color Leather      = new Color(0.150f, 0.085f, 0.055f);
    private static readonly Color LeatherDark  = new Color(0.055f, 0.035f, 0.030f);
    private static readonly Color LeatherLight = new Color(0.300f, 0.175f, 0.090f);
    private static readonly Color Suit         = new Color(0.035f, 0.038f, 0.060f);
    private static readonly Color Brass        = new Color(0.900f, 0.640f, 0.200f);
    private static readonly Color BrassDark    = new Color(0.450f, 0.270f, 0.070f);

    // ---------- Torso: alto → medio ancho, media profundidad, centro en Z ----------
    private static readonly ModelKeys TorsoWidth = new ModelKeys(
        0.845f, 0.055f, 0.90f, 0.125f, 0.95f, 0.152f, 1.00f, 0.152f, 1.05f, 0.135f, 1.09f, 0.116f, 1.13f, 0.104f,
        1.19f, 0.110f, 1.25f, 0.126f, 1.31f, 0.142f, 1.36f, 0.152f, 1.41f, 0.160f, 1.46f, 0.156f, 1.495f, 0.105f,
        1.52f, 0.058f, 1.55f, 0.045f, 1.62f, 0.042f);

    private static readonly ModelKeys TorsoDepth = new ModelKeys(
        0.845f, 0.055f, 0.90f, 0.088f, 0.95f, 0.104f, 1.00f, 0.106f, 1.05f, 0.096f, 1.09f, 0.084f, 1.13f, 0.077f,
        1.19f, 0.082f, 1.25f, 0.092f, 1.31f, 0.102f, 1.36f, 0.106f, 1.41f, 0.098f, 1.46f, 0.085f, 1.495f, 0.068f,
        1.52f, 0.054f, 1.55f, 0.047f, 1.62f, 0.046f);

    private static readonly ModelKeys TorsoCenter = new ModelKeys(
        0.845f, 0.015f, 0.95f, 0f, 1.05f, 0.01f, 1.13f, 0.02f, 1.25f, 0.03f, 1.36f, 0.036f, 1.46f, 0.015f,
        1.52f, 0.005f, 1.62f, 0.016f);

    private const float TorsoBottom = 0.845f;
    private const float TorsoTop = 1.62f;

    // ---------- Brazo y pierna: distancia desde el nacimiento → radio ----------
    private static readonly ModelKeys ArmRadius = new ModelKeys(
        -0.05f, 0.036f, 0f, 0.046f, 0.06f, 0.046f, 0.12f, 0.041f, 0.18f, 0.036f, 0.234f, 0.033f, 0.27f, 0.036f,
        0.32f, 0.034f, 0.38f, 0.029f, 0.43f, 0.027f, 0.445f, 0.028f);

    private static readonly ModelKeys LegRadius = new ModelKeys(
        -0.07f, 0.075f, 0f, 0.084f, 0.08f, 0.085f, 0.16f, 0.078f, 0.26f, 0.066f, 0.35f, 0.055f, 0.41f, 0.051f,
        0.46f, 0.050f, 0.53f, 0.055f, 0.62f, 0.049f, 0.72f, 0.040f, 0.79f, 0.037f, 0.82f, 0.037f);

    private const float BracerStart = 0.268f, BracerEnd = 0.418f;
    private const float BootTop = 0.455f, BootCuffEnd = 0.505f, BootEnd = 0.752f;

    // ---------- Cabeza: alto de 0 (mentón) a 1 (coronilla) ----------
    private const float HeadBottom = 1.56f;
    private const float HeadHeight = 0.235f;

    private static readonly ModelKeys HeadWidth = new ModelKeys(
        0f, 0.016f, 0.08f, 0.036f, 0.2f, 0.062f, 0.33f, 0.084f, 0.5f, 0.092f, 0.7f, 0.088f, 0.85f, 0.070f,
        0.95f, 0.042f, 1f, 0.006f);

    private static readonly ModelKeys HeadDepth = new ModelKeys(
        0f, 0.018f, 0.1f, 0.036f, 0.22f, 0.056f, 0.35f, 0.076f, 0.5f, 0.090f, 0.7f, 0.094f, 0.85f, 0.082f,
        0.95f, 0.050f, 1f, 0.006f);

    private static readonly ModelKeys HeadOffset = new ModelKeys(
        0f, 0.060f, 0.1f, 0.052f, 0.22f, 0.034f, 0.35f, 0.018f, 0.5f, 0.010f, 0.7f, 0.004f, 0.85f, 0f, 1f, 0f);

    private const float FaceLeft = -0.12f, FaceRight = 0.12f, FaceBottom = 1.55f, FaceTop = 1.84f;

    // ---------- Capucha ----------
    private static readonly Vector3 HoodCenter = new Vector3(0f, 1.672f, 0.012f);
    private static readonly Vector3 HoodBack = new Vector3(0f, 1.66f, -0.122f);

    private readonly ModelRegion _head, _torso, _arm, _leg, _fur, _tail, _hood, _cape, _paw, _ear, _scarf, _belt, _flap,
                                 _brass, _claw, _lining, _pad, _glove;

    protected override ModelRegion HairRegion => _fur;

    [MenuItem("Mercenarios/Modelos de clase/Generar la Pícara")]
    public static void Generate()
    {
        var model = new RogueModel();
        model.Build();
    }

    private RogueModel() : base(2284)
    {
        _head   = Atlas.Region(0, 1024, 1024, 1024);
        _torso  = Atlas.Region(1024, 1024, 1024, 1024);
        _arm    = Atlas.Region(0, 0, 512, 1024);
        _leg    = Atlas.Region(512, 0, 512, 1024);
        _fur    = Atlas.Region(1024, 0, 256, 1024);
        _tail   = Atlas.Region(1280, 512, 256, 512);
        _hood   = Atlas.Region(1536, 512, 256, 512);
        _cape   = Atlas.Region(1792, 512, 256, 512);
        _paw    = Atlas.Region(1280, 256, 256, 256);
        _ear    = Atlas.Region(1536, 256, 256, 256);
        _scarf  = Atlas.Region(1792, 256, 256, 256);
        _belt   = Atlas.Region(1280, 128, 768, 128);
        _flap   = Atlas.Region(1280, 0, 256, 128);
        _brass  = Atlas.Region(1536, 0, 128, 128);
        _claw   = Atlas.Region(1664, 0, 128, 128);
        _lining = Atlas.Region(1792, 0, 128, 128);
        _pad    = Atlas.Region(1920, 0, 64, 128);
        _glove  = Atlas.Region(1984, 0, 64, 128);
    }

    private void Build()
    {
        PaintAtlas();

        BuildTorso();
        BuildVest();
        BuildHead();
        foreach (float side in new[] { 1f, -1f })
        {
            BuildCheek(side);
            BuildEar(side);
            BuildWhiskers(side);
            BuildArm(side);
            BuildHand(side);
            BuildLeg(side);
            BuildPaw(side);
        }

        BuildFlaps();
        BuildBelts();
        BuildTail();
        BuildCape();
        BuildCapelet();
        BuildHood();
        BuildScarf();

        Kit.Save(Name, ArtFolder, PrefabFolder, Atlas, ModelFinish.Matte);
    }

    // =========================================================
    // TORSO Y CHALECO
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
        float bust = 0.02f * Bell(y, 1.34f, 0.05f) * (Bell(deg, 32f, 24f) + Bell(deg, -32f, 24f));
        return new Vector3(d.x * (TorsoWidth.At(y) + offset + bust), y, TorsoCenter.At(y) + d.z * (TorsoDepth.At(y) + offset + bust));
    }

    private ModelSkin TorsoSkin(Vector3 p)
    {
        ModelSkin skin = Hips.To(Spine, Smooth(1.0f, 1.1f, p.y));
        skin = skin.To(Chest, Smooth(1.19f, 1.3f, p.y));
        skin = skin.To(Neck, Smooth(1.5f, 1.54f, p.y));
        skin = skin.To(HeadBone, Smooth(1.58f, 1.6f, p.y));

        string side = p.x > 0f ? ".R" : ".L";
        float shoulder = Smooth(0.11f, 0.17f, Mathf.Abs(p.x)) * Smooth(1.36f, 1.42f, p.y);
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
            0.845f, 0.87f, 0.90f, 0.95f, 1.0f, 1.05f, 1.09f, 1.13f, 1.19f, 1.25f, 1.31f, 1.34f, 1.37f, 1.41f, 1.46f,
            1.495f, 1.52f, 1.55f, 1.58f, 1.62f,
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

    // El chaleco: en punta abajo, y con un escote en V que deja ver el pecho blanco.
    private static float VestBottom(float deg) => 1.04f - 0.05f * Smooth(34f, 0f, Mathf.Abs(Wrap(deg)));
    private static float VestTop(float deg) => 1.46f - 0.095f * Smooth(44f, 0f, Mathf.Abs(Wrap(deg)));

    private void BuildVest()
    {
        Kit.BeginPiece(46f);
        const int columns = 40;
        float[] rows = { 0f, 0f, 0.012f, 0.1f, 0.22f, 0.34f, 0.46f, 0.58f, 0.7f, 0.82f, 0.92f, 0.985f, 1f, 1f };

        var rings = new ModelPoint[rows.Length][];
        for (int r = 0; r < rows.Length; r++)
        {
            bool lip = r == 0 || r == rows.Length - 1;
            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float u = i / (float)columns;
                float deg = (u - 0.5f) * 360f;
                float y = Mathf.Lerp(VestBottom(deg), VestTop(deg), rows[r]);
                Vector3 p = TorsoPoint(y, deg, lip ? 0.001f : 0.009f);
                rings[r][i] = new ModelPoint(p, _torso.Uv(u, Mathf.InverseLerp(TorsoBottom, TorsoTop, y)), TorsoSkin(p));
            }
        }
        Kit.Loft(rings);
    }

    // =========================================================
    // CABEZA
    // =========================================================

    private static Vector3 HeadPoint(float h, float deg, float offset)
    {
        float a = deg * Mathf.Deg2Rad;
        float s = Mathf.Sin(a), c = Mathf.Cos(a);
        const float round = 2f / 2.2f;
        float x = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), round) * HeadWidth.At(h);
        float z = HeadOffset.At(h) + Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), round) * HeadDepth.At(h);

        float abs = Mathf.Abs(deg);
        if (abs < 100f)
        {
            z += 0.040f * Bell(abs, 0f, 30f) * Bell(h, 0.255f, 0.115f);                       // hocico
            z += 0.012f * Bell(abs, 19f, 11f) * Bell(h, 0.255f, 0.055f);                      // las almohadillas de los bigotes
            z += 0.012f * Bell(abs, 0f, 8f) * Bell(h, 0.37f, 0.035f);                         // nariz
            z -= 0.005f * Bell(abs, 0f, 12f) * Bell(h, 0.2f, 0.016f);                         // la raya de la boca
            z += 0.018f * Bell(abs, 0f, 26f) * Bell(h, 0.09f, 0.075f);                        // mentón
            z -= 0.008f * Bell(abs, 28f, 13f) * Bell(h, 0.5f, 0.05f);                         // cuenca del ojo
            z += 0.005f * Bell(h, 0.6f, 0.04f) * Bell(abs, 0f, 44f);                          // frente
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
            0f, 0.05f, 0.1f, 0.15f, 0.19f, 0.22f, 0.26f, 0.3f, 0.34f, 0.38f, 0.42f, 0.46f, 0.5f, 0.55f, 0.6f, 0.66f, 0.72f,
            0.8f, 0.88f, 0.95f, 1f,
        };
        float[] half = { 0f, 5f, 11f, 19f, 28f, 40f, 54f, 70f, 88f, 110f, 135f, 158f, 180f };

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

    // Las mejillas: la cara de gato se ve ancha por esto. Son una sola hoja con el borde en zigzag;
    // con matas sueltas parecían dientes.
    private void BuildCheek(float side)
    {
        Kit.BeginPiece(75f);
        const int columns = 10;
        float[] rows = { 0f, 0.55f, 1f };

        var rings = new ModelPoint[rows.Length][];
        for (int r = 0; r < rows.Length; r++)
        {
            float t = rows[r];
            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float s = i / (float)columns;
                Vector3 root = HeadPoint(Mathf.Lerp(0.12f, 0.38f, s), side * Mathf.Lerp(38f, 80f, s), -0.003f);
                Vector3 radial = new Vector3(root.x, 0f, (root.z - 0.02f) * 0.35f).normalized;
                Vector3 direction = (radial + Vector3.down * Mathf.Lerp(0.75f, 0.35f, s)).normalized;
                float length = (i % 2 == 0 ? 0.05f : 0.026f) * Mathf.Sin(Mathf.Lerp(0.35f, 2.6f, s));
                Vector3 p = root + direction * (length * t);
                rings[r][i] = new ModelPoint(p, _fur.Uv(0.62f + 0.26f * s, 0.55f + 0.4f * t), HeadBone);
            }
        }
        Kit.Sheet(rings, _fur.Uv(0.75f, 0.6f), HeadCenter);
    }

    private static float EarWidth(float t) => 0.040f * (1f - Mathf.Pow(t, 1.35f));

    private void BuildEar(float side)
    {
        var root = new Vector3(side * 0.056f, 1.772f, -0.004f);
        var tip = new Vector3(side * 0.104f, 1.905f, -0.02f);
        Vector3 up = (tip - root).normalized;
        Vector3 front = Vector3.ProjectOnPlane(new Vector3(side * 0.4f, 0f, 1f), up).normalized;
        Vector3 across = Vector3.Cross(up, front).normalized;
        float length = Vector3.Distance(root, tip);

        Kit.BeginPiece(38f);
        float[] stops = { 0f, 0.3f, 0.6f, 0.85f };
        const int columns = 10;
        var rings = new ModelPoint[stops.Length + 1][];
        for (int r = 0; r < stops.Length; r++)
        {
            float t = stops[r];
            float width = EarWidth(t);
            float depth = 0.020f * (1f - t * 0.8f);
            Vector3 center = root + up * (length * t);
            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float s = i / (float)columns;
                float a = s * Mathf.PI * 2f;
                float c = Mathf.Cos(a), f = Mathf.Sin(a);

                // Adelante se hunde en forma de cuchara: de ahí sale la parte de adentro de la oreja
                float cup = f > 0f ? -1.3f * f * (1f - c * c) : f;
                Vector3 p = center + across * (c * width) + front * (cup * depth);
                rings[r][i] = new ModelPoint(p, _ear.Uv(s, t), HeadBone);
            }
        }
        var point = new ModelPoint(tip, _ear.Uv(0.75f, 1f), HeadBone);
        rings[stops.Length] = new ModelPoint[columns + 1];
        for (int i = 0; i <= columns; i++) rings[stops.Length][i] = point;
        Kit.Loft(rings);

        // Dos aritos en una sola oreja
        if (side > 0f)
        {
            Kit.BeginPiece(40f);
            for (int i = 0; i < 2; i++)
            {
                float t = 0.46f + 0.14f * i;
                Vector3 outer = across * Mathf.Sign(Vector3.Dot(across, Vector3.right) * side);
                Vector3 at = root + up * (length * t) + outer * EarWidth(t);
                Kit.Gem(at, up, front, 0.008f, 0.006f, 0.006f, 6, _brass, HeadBone);
            }
        }
    }

    private void BuildWhiskers(float side)
    {
        Kit.BeginPiece(60f);
        for (int i = 0; i < 3; i++)
        {
            float rise = (i - 1) * 0.022f;
            Vector3 root = HeadPoint(0.27f + (i - 1) * 0.035f, side * 25f, 0f);
            Vector3 tip = root + new Vector3(side * 0.12f, rise - 0.018f, -0.03f);
            Vector3 middle = (root + tip) * 0.5f + new Vector3(0f, 0.012f, 0.008f);
            Kit.Sweep(new[] { root, middle, tip }, p => Vector3.up, t => Vector2.one * Mathf.Lerp(0.0011f, 0.0004f, t), 3,
                (t, a) => _paw.Uv(0.5f, 0.7f), (p, t) => HeadBone);
        }
    }

    // =========================================================
    // CAPUCHA, CAPA Y BUFANDA
    // =========================================================

    // El borde de la capucha da toda la vuelta: s va de -1 a 1, con 0 arriba, y abajo pasa por adentro
    // del cuello, tapado por la bufanda. Así la capucha es un cuenco cerrado y la nuca no queda abierta.
    private static Vector3 HoodRim(float s)
    {
        float angle = s * 180f * Mathf.Deg2Rad;
        float top = Mathf.Cos(angle) * 0.5f + 0.5f;
        float peak = 0.016f * Bell(s, 0f, 0.147f);
        return new Vector3(Mathf.Sin(angle) * 0.108f, 1.664f + Mathf.Cos(angle) * 0.108f + peak, 0.048f + 0.072f * top * top);
    }

    private void BuildHood()
    {
        Kit.BeginPiece(44f);
        const int columns = 26;
        float[] rows = { 0f, 0.04f, 0.14f, 0.28f, 0.44f, 0.6f, 0.76f, 0.9f, 1f };

        var rings = new ModelPoint[rows.Length + 1][];
        for (int r = 0; r <= rows.Length; r++)
        {
            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float u = i / (float)columns;
                float s = u * 2f - 1f;
                Vector3 rim = HoodRim(s);

                // La primera fila se dobla hacia adentro: le da grosor al borde
                if (r == 0)
                {
                    Vector3 fold = rim + (HoodCenter - rim).normalized * 0.008f + Vector3.back * 0.012f;
                    rings[r][i] = new ModelPoint(fold, _hood.Uv(u, 0.012f), HoodSkin(fold));
                    continue;
                }

                float t = rows[r - 1];
                Vector3 from = rim - HoodCenter;
                Vector3 to = HoodBack - HoodCenter;
                Vector3 direction = Vector3.Slerp(from.normalized, to.normalized, t);
                float radius = Mathf.Lerp(from.magnitude, to.magnitude, t) + 0.016f * Mathf.Sin(t * Mathf.PI)
                             + 0.005f * Mathf.Sin(s * 3f * Mathf.PI + t * 3f) * Mathf.Sin(t * Mathf.PI);
                Vector3 p = HoodCenter + direction * radius;
                rings[r][i] = new ModelPoint(p, _hood.Uv(u, Mathf.Lerp(0.05f, 0.98f, t)), HoodSkin(p));
            }
        }
        Kit.Sheet(rings, _lining.Uv(0.25f, 0.5f), HoodCenter);
    }

    private ModelSkin HoodSkin(Vector3 p) => HeadBone.To(Neck, 0.8f * Smooth(1.64f, 1.56f, p.y));

    private ModelSkin CapeletSkin(Vector3 p)
    {
        string side = p.x > 0f ? ".R" : ".L";
        return Chest.To(Kit.Bone("B-upperArm" + side), 0.4f * Smooth(0.17f, 0.25f, Mathf.Abs(p.x)));
    }

    // La pelerina sobre los hombros, con el borde roto.
    private void BuildCapelet()
    {
        Kit.BeginPiece(32f);
        const int columns = 34, rows = 6;
        const float open = 20f;

        var hem = new float[columns + 1];
        for (int i = 0; i <= columns; i++) hem[i] = i % 2 == 0 ? Range(-0.05f, -0.025f) : Range(0f, 0.02f);

        var rings = new ModelPoint[rows + 1][];
        for (int r = 0; r <= rows; r++)
        {
            float t = r / (float)rows;
            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float u = i / (float)columns;
                float deg = Mathf.Lerp(open, 360f - open, u);
                float abs = Mathf.Abs(Wrap(deg));
                float rad = deg * Mathf.Deg2Rad;

                float rx = Mathf.Lerp(0.075f, 0.262f, Mathf.Pow(t, 0.7f));
                float rz = Mathf.Lerp(0.072f, Mathf.Lerp(0.186f, 0.170f, Smooth(60f, 120f, abs)), Mathf.Pow(t, 0.7f));
                float y = 1.555f - 0.085f * t - 0.135f * Mathf.Pow(t, 3f) - 0.06f * Smooth(40f, 0f, abs) * t;
                y -= 0.05f * Smooth(110f, 180f, abs) * t;
                if (r == rows) y += hem[i];

                float fold = 0.012f * Mathf.Sin(deg * 0.2f) * t;
                var p = new Vector3(Mathf.Sin(rad) * (rx + fold), y, 0.012f + Mathf.Cos(rad) * (rz + fold));
                rings[r][i] = new ModelPoint(p, _cape.Uv(Mathf.PingPong(u * 2f, 1f), 1f - t * 0.5f), CapeletSkin(p));
            }
        }
        Kit.Sheet(rings, _lining.Uv(0.25f, 0.5f));
    }

    private ModelSkin CapeSkin(float y)
    {
        ModelSkin skin = Chest.To(Spine, Smooth(1.32f, 1.14f, y));
        return skin.To(Hips, Smooth(1.14f, 0.98f, y));
    }

    // La capa de atrás: corta, para que se vea la cola, y más larga de un lado.
    private void BuildCape()
    {
        Kit.BeginPiece(30f);
        const int columns = 16, rows = 8;

        var hem = new float[columns + 1];
        for (int i = 0; i <= columns; i++) hem[i] = i % 2 == 0 ? Range(-0.07f, -0.03f) : Range(0f, 0.03f);

        var rings = new ModelPoint[rows + 1][];
        for (int r = 0; r <= rows; r++)
        {
            float t = r / (float)rows;
            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float u = i / (float)columns;
                float s = u * 2f - 1f;
                float bottom = 1.02f - 0.15f * Smooth(0.1f, -1f, s);
                float y = Mathf.Lerp(1.46f, bottom, t) + (r == rows ? hem[i] : 0f);
                float width = Mathf.Lerp(0.17f, 0.25f, t);
                float fold = Mathf.Sin(s * 8.5f + 1.3f) * 0.028f * Smooth(0.08f, 0.7f, t);
                float z = -0.115f - 0.075f * t + s * s * Mathf.Lerp(0.05f, 0.04f, t) + fold;
                var p = new Vector3(s * width, y, z);
                rings[r][i] = new ModelPoint(p, _cape.Uv(u, 1f - t), CapeSkin(y));
            }
        }
        Kit.Sheet(rings, _lining.Uv(0.25f, 0.5f));
    }

    private void BuildScarf()
    {
        ScarfRoll(0.104f, 0.098f, 1.525f, 0.040f, 0.042f, 0.028f, 1.1f, 0.1f);
        ScarfRoll(0.086f, 0.086f, 1.556f, 0.024f, 0.036f, 0.026f, 2.9f, 0.5f);
    }

    private void ScarfRoll(float radiusX, float radiusZ, float y, float dip, float wide, float thick, float phase, float strip)
    {
        Kit.BeginPiece(70f);
        const int steps = 26;
        var path = new Vector3[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            float deg = -180f + 360f * i / steps;
            float rad = deg * Mathf.Deg2Rad;
            float front = Bell(Mathf.Abs(deg), 0f, 60f);
            float wobble = Mathf.Sin(deg * 0.052f + phase) * 0.005f;
            path[i] = new Vector3(Mathf.Sin(rad) * (radiusX + wobble), y - dip * front + wobble, 0.024f + Mathf.Cos(rad) * (radiusZ + wobble));
        }

        Kit.Sweep(path, p => (p - new Vector3(0f, 1.42f, 0.024f)).normalized,
            t => new Vector2(wide * (0.95f + 0.1f * Mathf.Sin(t * 9f + phase)), thick), 8,
            (t, a) => _scarf.Uv(strip + 0.45f * t, 0.1f + 0.8f * Mathf.Abs(a * 2f - 1f)),
            (p, t) => Chest.To(Neck, 0.4f), capStart: true);
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

    private static float ArmBulge(float d) =>
        ArmRadius.At(d)
        + 0.012f * Smooth(BracerStart - 0.004f, BracerStart, d) * (1f - Smooth(BracerStart, BracerStart + 0.06f, d) * 0.45f) * (1f - Smooth(BracerEnd - 0.003f, BracerEnd, d));

    private void BuildArm(float side)
    {
        Kit.BeginPiece(50f);
        string name = SideName(side);
        Vector3[] chain = ArmChain(name);

        float[] stops =
        {
            -0.05f, -0.02f, 0.01f, 0.05f, 0.09f, 0.13f, 0.17f, 0.205f, 0.234f, 0.25f, 0.264f, 0.268f, 0.29f, 0.32f, 0.35f,
            0.38f, 0.40f, 0.415f, 0.418f, 0.43f, 0.446f,
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
                rings[r][i] = new ModelPoint(p, _arm.Uv(u, Mathf.InverseLerp(-0.05f, 0.446f, d)), ArmSkin(name, d));
            }
        }
        Kit.Loft(rings);

        FurRing(chain, BracerEnd + 0.001f, 12, 0.036f, 0.016f, ArmBulge, d => ArmSkin(name, d), 1f, 0.45f, _fur, 0.62f, 0.88f);
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

        Kit.BeginPiece(60f);
        float[] along = { -0.05f, 0.3f, 0.7f, 1f, 1.1f };
        float[] widths = { 0.028f, 0.046f, 0.058f, 0.060f, 0.052f };
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
                rings[r][i] = new ModelPoint(p, _glove.Uv(0.5f, Mathf.Sin(a) > 0f ? 0.85f : 0.3f), hand);
            }
        }
        Kit.Loft(rings, wrap: true, capEnd: true);

        Kit.BeginPiece(30f);
        Kit.Disc(wrist + axis * (length * 0.5f) + flat * 0.024f, flat, axis, 0.017f, 0.017f, 0.005f, 6, _brass, hand);

        foreach (string finger in new[] { "B-indexFinger", "B-middleFinger", "B-ringFinger", "B-pinky", "B-thumb" })
        {
            Kit.BeginPiece(60f);
            float shorter = finger == "B-thumb" ? 0.66f : 0.8f;
            BuildFinger(finger, name, finger == "B-thumb" ? 0.0185f : 0.0155f, 6, (t, a) => _paw.Uv(0.5f, 0.7f), shorter, 0.42f);

            // La garra
            Vector3 first = Kit.Pos(finger + "01" + name);
            Vector3 b = Kit.Pos(finger + "02" + name);
            Vector3 c = Kit.Pos(finger + "03" + name);
            Vector3 direction = (c - b).normalized;
            Vector3 tip = Vector3.LerpUnclamped(first, c + (c - b) * 0.85f, shorter);
            Kit.BeginPiece(30f);
            Kit.Gem(tip + direction * 0.006f, direction, Vector3.up, 0.016f, 0.0055f, 0.0045f, 4, _claw, Kit.Bone(finger + "03" + name));
        }
    }

    // =========================================================
    // PIERNAS Y PATAS
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
        radius += 0.011f * Smooth(BootTop - 0.003f, BootTop, d) * (1f - Smooth(BootEnd - 0.003f, BootEnd, d));
        radius += 0.010f * Smooth(BootTop - 0.003f, BootTop, d) * (1f - Smooth(BootCuffEnd - 0.004f, BootCuffEnd, d));   // el doblez de arriba
        return radius;
    }

    private void BuildLeg(float side)
    {
        Kit.BeginPiece(50f);
        string name = SideName(side);
        Vector3[] chain = LegChain(name);

        float[] stops =
        {
            -0.07f, -0.03f, 0.01f, 0.05f, 0.1f, 0.15f, 0.2f, 0.25f, 0.3f, 0.35f, 0.38f, 0.41f, 0.44f, 0.452f, 0.455f, 0.48f,
            0.501f, 0.505f, 0.54f, 0.58f, 0.62f, 0.66f, 0.7f, 0.73f, 0.749f, 0.752f, 0.78f, 0.82f,
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

        // El pelo que asoma de la bota, y la correa del muslo
        FurRing(chain, BootEnd + 0.002f, 12, 0.038f, 0.018f, LegBulge, d => LegSkin(name, d), 1f, 0.55f, _fur, 0.62f, 0.88f);
        if (side > 0f) LegStrap(chain, name, 0.215f);
    }

    private void LegStrap(Vector3[] chain, string name, float distance)
    {
        Kit.BeginPiece(40f);
        const int columns = 14;
        float[] rows = { -0.02f, -0.017f, 0.017f, 0.02f };
        float[] lift = { 0f, 0.007f, 0.007f, 0f };

        var rings = new ModelPoint[rows.Length][];
        for (int r = 0; r < rows.Length; r++)
        {
            float d = distance + rows[r];
            AlongChain(chain, d, out Vector3 center, out Vector3 tangent);
            Vector3 front = Vector3.ProjectOnPlane(Vector3.forward, tangent).normalized;
            Vector3 outer = Vector3.Cross(front, tangent).normalized;
            float radius = LegBulge(d) + lift[r];

            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float s = i / (float)columns;
                float a = s * Mathf.PI * 2f;
                Vector3 p = center + front * (Mathf.Cos(a) * radius) + outer * (Mathf.Sin(a) * radius * 0.95f);
                rings[r][i] = new ModelPoint(p, _belt.Uv(s * 0.4f, 0.5f + (r - 1.5f) * 0.3f), LegSkin(name, distance));
            }
        }
        Kit.Loft(rings);

        AlongChain(chain, distance, out Vector3 spot, out Vector3 axis);
        Vector3 forward = Vector3.ProjectOnPlane(Vector3.forward, axis).normalized;
        Vector3 turned = (Vector3.up + Vector3.right).normalized;
        Kit.BeginPiece(30f);
        Kit.Disc(spot + forward * (LegBulge(distance) + 0.008f), forward, turned, 0.03f, 0.03f, 0.008f, 4, _brass, LegSkin(name, distance));
    }

    private void BuildPaw(float side)
    {
        Kit.BeginPiece(60f);
        string name = SideName(side);
        ModelSkin foot = Kit.Bone("B-foot" + name);
        ModelSkin toe = Kit.Bone("B-toe" + name);
        float x = Kit.Pos("B-foot" + name).x;

        float[] z      = { -0.085f, -0.072f, -0.035f, 0.015f, 0.06f, 0.098f, 0.13f, 0.158f, 0.174f };
        float[] half   = { 0.022f, 0.035f, 0.041f, 0.043f, 0.045f, 0.050f, 0.054f, 0.050f, 0.036f };
        float[] bottom = { 0.014f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.002f, 0.008f };
        float[] top    = { 0.075f, 0.105f, 0.14f, 0.15f, 0.108f, 0.078f, 0.062f, 0.052f, 0.036f };
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
                rings[r][i] = new ModelPoint(p, _paw.Uv(0.5f, 0.42f + 0.3f * Mathf.InverseLerp(-1f, 1f, s)), skin);
            }
        }
        Kit.Loft(rings, wrap: true, capStart: true, capEnd: true);

        // La almohadilla grande, y los dedos con su garra y su almohadilla
        Kit.BeginPiece(60f);
        Kit.Lump(new Vector3(x, 0.003f, 0.09f), Vector3.right, Vector3.up, 0.03f, 0.03f, 0.006f, 8, _pad, 0.5f, foot.To(toe, 0.3f));
        float[] offsets = { -0.037f, -0.0128f, 0.0128f, 0.037f };
        foreach (float offset in offsets)
        {
            float outer = Mathf.Abs(offset) > 0.03f ? 1f : 0f;
            var center = new Vector3(x + offset, 0.026f, 0.170f - 0.016f * outer);
            Kit.BeginPiece(60f);
            Kit.Lump(center, Vector3.forward, Vector3.up, 0.034f, 0.0165f, 0.024f, 8, _paw, 0.5f, toe);
            Kit.Lump(new Vector3(center.x, 0.003f, center.z + 0.006f), Vector3.forward, Vector3.up, 0.014f, 0.0105f, 0.005f, 6, _pad, 0.5f, toe);
            Kit.BeginPiece(30f);
            Kit.Gem(center + new Vector3(0f, -0.004f, 0.042f), Vector3.forward, Vector3.up, 0.015f, 0.0058f, 0.0048f, 4, _claw, toe);
        }
    }

    // =========================================================
    // CINTURONES, FALDONES Y COLA
    // =========================================================

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
                Vector3 p = TorsoPoint(Mathf.Max(y, 0.96f), deg, offset + lift[r] * 0.006f);
                p.y = y;
                rings[r][i] = new ModelPoint(p, _belt.Uv(u, 0.5f + rows[r]), TorsoSkin(p));
            }
        }
        Kit.Loft(rings);
    }

    private void BuildBelts()
    {
        Func<float, float> waist = deg => 1.105f;
        Func<float, float> hip = deg => 1.0f + 0.034f * Mathf.Cos((deg - 40f) * Mathf.Deg2Rad);
        Band(waist, 0.036f, 0.012f);
        Band(hip, 0.04f, 0.022f);

        Kit.BeginPiece(30f);
        Vector3 turned = (Vector3.up + Vector3.right).normalized;
        Kit.Disc(TorsoPoint(1.105f, 0f, 0.018f), Vector3.forward, turned, 0.032f, 0.032f, 0.008f, 4, _brass, Spine);
        Kit.Disc(TorsoPoint(hip(-16f), -16f, 0.03f), TorsoDirection(-16f).normalized, turned, 0.036f, 0.036f, 0.010f, 4, _brass, Hips);

        // Bolsas
        foreach (float deg in new[] { 58f, 86f, -80f })
        {
            Vector3 normal = TorsoDirection(deg).normalized;
            Vector3 spot = TorsoPoint(Mathf.Max(hip(deg) - 0.052f, 0.96f), deg, 0.044f);
            spot.y = hip(deg) - 0.052f;
            Kit.BeginPiece(44f);
            Kit.Lump(spot, Vector3.up, normal, 0.05f, 0.034f, 0.022f, 8, _flap, 0.5f, Hips);
            Kit.BeginPiece(30f);
            Kit.Disc(spot + normal * 0.017f + Vector3.up * 0.022f, normal, Vector3.up, 0.031f, 0.026f, 0.006f, 8, _flap, Hips);
            Kit.Gem(spot + normal * 0.026f + Vector3.up * 0.012f, Vector3.up, normal, 0.006f, 0.006f, 0.004f, 6, _brass, Hips);
        }
    }

    private ModelSkin FlapSkin(Vector3 p, float t)
    {
        float reach = Mathf.Min(1f, Mathf.Pow(t, 0.8f) * 1.1f) * 0.8f * Smooth(1.03f, 0.84f, p.y);
        float right = Smooth(-0.14f, 0.14f, p.x);
        return Hips.To(Kit.Bone("B-thigh.R"), reach * right).To(Kit.Bone("B-thigh.L"), reach * (1f - right) / Mathf.Max(0.001f, 1f - reach * right));
    }

    // Faldones del chaleco y la faja que cuelga.
    private void BuildFlaps()
    {
        Flap(0f, 27f, 1.02f, 0.84f, 0.022f, 0.42f, _flap, _lining.Uv(0.25f, 0.5f), 0.07f);
        Flap(112f, 30f, 1.03f, 0.84f, 0.018f, 0.42f, _flap, _lining.Uv(0.25f, 0.5f), 0.05f);
        Flap(-112f, 30f, 1.03f, 0.84f, 0.018f, 0.42f, _flap, _lining.Uv(0.25f, 0.5f), 0.05f);
        Flap(-46f, 15f, 1.02f, 0.70f, 0.03f, 0.16f, _scarf, _lining.Uv(0.75f, 0.5f), 0.045f);
    }

    private void Flap(float centerDeg, float halfDeg, float top, float hem, float offset, float flare, ModelRegion region, Vector2 lining, float point)
    {
        Kit.BeginPiece(30f);
        const int columns = 8, rows = 5;
        var rings = new ModelPoint[rows + 1][];
        for (int r = 0; r <= rows; r++)
        {
            float t = r / (float)rows;
            rings[r] = new ModelPoint[columns + 1];
            for (int i = 0; i <= columns; i++)
            {
                float u = i / (float)columns;
                float s = u * 2f - 1f;
                float deg = centerDeg + s * halfDeg * Mathf.Lerp(1f, 0.9f, t);
                float y = Mathf.Lerp(top, hem + point * Mathf.Abs(s), t);

                Vector3 d = TorsoDirection(deg).normalized;
                Vector3 p = TorsoPoint(Mathf.Max(y, 0.97f), deg, offset);
                p += d * (Mathf.Max(0f, 0.97f - y) * flare);
                p.y = y;
                rings[r][i] = new ModelPoint(p, region.Uv(u, 1f - t), FlapSkin(p, t));
            }
        }
        Kit.Sheet(rings, lining);
    }

    // La cola no tiene huesos propios. La base sigue a la cadera, y la punta toma un poco de la columna
    // y de una pierna: con eso acompaña un poco el paso, sin scripts.
    private ModelSkin TailSkin(float t)
    {
        ModelSkin skin = Hips.To(Spine, 0.22f * Smooth(0.15f, 1f, t));
        return skin.To(Kit.Bone("B-thigh.R"), 0.10f * Smooth(0.25f, 1f, t));
    }

    private void BuildTail()
    {
        Kit.BeginPiece(58f);
        var a0 = new Vector3(0f, 0.985f, -0.085f);
        var a1 = new Vector3(0f, 0.86f, -0.22f);
        var a2 = new Vector3(0.03f, 0.60f, -0.33f);
        var b0 = new Vector3(0.09f, 0.60f, -0.50f);
        var b1 = new Vector3(0.15f, 0.60f, -0.67f);
        var b2 = new Vector3(0.20f, 0.86f, -0.68f);
        var b3 = new Vector3(0.12f, 0.95f, -0.60f);

        const int steps = 24;
        var path = new Vector3[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            path[i] = t < 0.5f ? Bezier(a0, a1, a2, b0, t * 2f) : Bezier(b0, b1, b2, b3, t * 2f - 1f);
        }

        Kit.Sweep(path, p => Vector3.right,
            t =>
            {
                float radius = Mathf.Lerp(0.04f, 0.026f, t) * (0.95f + 0.2f * Bell(t, 0.88f, 0.1f));
                float end = Mathf.Sqrt(Mathf.Clamp01((1f - t) / 0.08f));
                return Vector2.one * Mathf.Max(0.002f, radius * end);
            },
            7, (t, a) => _tail.Uv(a, t), (p, t) => TailSkin(t), capStart: true);
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
        Atlas.Paint(_fur, PaintFur);
        Atlas.Paint(_tail, PaintTail);
        Atlas.Paint(_hood, PaintHood);
        Atlas.Paint(_cape, PaintCape);
        Atlas.Paint(_paw, PaintPaw);
        Atlas.Paint(_ear, PaintEar);
        Atlas.Paint(_scarf, PaintScarf);
        Atlas.Paint(_belt, PaintBelt);
        Atlas.Paint(_flap, PaintFlap);
        Atlas.Paint(_brass, (u, v) => Color.Lerp(BrassDark, Brass * 1.15f, v * 0.7f + 0.3f * u));
        Atlas.Paint(_claw, (u, v) => Color.Lerp(new Color(0.05f, 0.05f, 0.06f), new Color(0.34f, 0.32f, 0.30f), v));
        Atlas.Paint(_lining, (u, v) => u < 0.5f ? HoodDark : LeatherDark);
        Atlas.Paint(_pad, (u, v) => Color.Lerp(NoseTone * 0.72f, NoseTone * 0.95f, v));
        Atlas.Paint(_glove, (u, v) => Grain(Color.Lerp(LeatherDark, Leather, v), u, v, 30f, 0.16f));
    }

    // El pelo negro: casi negro, con un brillo azulado en hebras para que se lea la forma.
    private static Color BlackFur(float u, float v)
    {
        float strands = Mathf.PerlinNoise(u * 160f, v * 42f);
        float soft = Mathf.PerlinNoise(u * 12f + 2f, v * 12f + 5f);
        return Color.Lerp(FurBlack, FurSheen, 0.15f + 0.55f * Smooth(0.45f, 0.82f, strands) * (0.5f + 0.6f * soft));
    }

    private static Color WhiteFur(float u, float v)
    {
        float strands = Mathf.PerlinNoise(u * 150f, v * 40f);
        return Color.Lerp(FurShade, FurWhite, 0.62f + 0.38f * Smooth(0.25f, 0.65f, strands));
    }

    // Arrugas de la tela. Un solo Perlin deja ver su cuadrícula, y en tela lisa parece un tartán.
    private static float Wrinkles(float u, float v)
    {
        float a = Mathf.PerlinNoise(u * 3.3f + v * 4.1f + 0.37f, v * 13.7f - u * 2.3f + 0.71f);
        float b = Mathf.PerlinNoise(u * 7.9f - v * 6.3f + 5.13f, v * 21.3f + u * 5.1f + 2.29f);
        return 0.72f + 0.56f * (a * 0.6f + b * 0.4f);
    }

    private static Color PaintHead(float u, float v)
    {
        float x = Mathf.Lerp(FaceLeft, FaceRight, u);
        float y = Mathf.Lerp(FaceBottom, FaceTop, v);
        float ax = Mathf.Abs(x);

        // El antifaz negro y lo blanco: hocico, mentón y la raya que sube por la nariz
        float rag = (Mathf.PerlinNoise(u * 60f, v * 60f) - 0.5f) * 0.005f;
        float muzzle = Mathf.Sqrt(Sq(x / 0.058f) + Sq((y - 1.622f) / 0.05f));
        float blazeWidth = 0.0095f * Smooth(1.75f, 1.71f, y) + Mathf.Max(0f, 1.676f - y) * 0.62f;
        float blaze = (blazeWidth - ax) / 0.004f;
        float chin = Smooth(1.606f, 1.594f, y) * Smooth(0.066f, 0.054f, ax);
        float white = Mathf.Max(Smooth(1.04f, 0.96f, muzzle + rag * 12f), Mathf.Max(Smooth(-0.6f, 0.6f, blaze + rag * 110f) * Smooth(1.752f, 1.74f, y), chin));

        Color c = Over(BlackFur(u, v), WhiteFur(u, v), white);

        // Sombras del blanco: debajo del hocico y al lado de la nariz
        c = Over(c, FurShade, 0.3f * white * Bell(y, 1.606f, 0.006f) * Bell(ax, 0f, 0.04f));
        c = Over(c, FurShade, 0.3f * white * Bell(ax, 0.017f, 0.006f) * Bell(y, 1.655f, 0.014f));

        // Lunares de los bigotes
        foreach (float row in new[] { 1.633f, 1.622f, 1.611f })
        {
            float dots = Mathf.Abs(Mathf.Repeat(ax, 0.0115f) - 0.0058f);
            float spot = Smooth(0.0024f, 0.0014f, Mathf.Sqrt(dots * dots + Sq(y - row - ax * 0.05f)));
            c = Over(c, new Color(0.2f, 0.2f, 0.24f), 0.7f * spot * Smooth(0.016f, 0.021f, ax) * Smooth(0.046f, 0.04f, ax));
        }

        // Boca: la raya de la nariz y las dos curvas
        float line = Smooth(0.0015f, 0.0007f, ax) * Smooth(1.616f, 1.618f, y) * Smooth(1.637f, 1.635f, y);
        float curve = 1.6165f + 18f * Sq(ax - 0.0125f) - 0.0006f;
        float mouth = Smooth(0.0016f, 0.0007f, Mathf.Abs(y - curve)) * Smooth(0.034f, 0.03f, ax);
        c = Over(c, new Color(0.12f, 0.1f, 0.13f), Mathf.Max(line, mouth));

        // Nariz rosa
        float nose = 1f - Mathf.Abs(x) / (0.0115f * Mathf.Clamp01((y - 1.6345f) / 0.0095f + 0.02f));
        float noseMask = Smooth(0f, 0.25f, nose) * Smooth(1.6345f, 1.6355f, y) * Smooth(1.6455f, 1.6445f, y);
        Color noseColor = Color.Lerp(NoseTone * 0.75f, NoseTone * 1.08f, Mathf.InverseLerp(1.635f, 1.645f, y));
        c = Over(c, noseColor, noseMask);

        // Ojos grandes, alzados hacia afuera
        float ex = (ax - 0.0425f) / 0.0245f;
        float ey = (y - 1.6815f - (ax - 0.0425f) * 0.3f) / 0.0138f;
        float open = 1f - ex * ex;
        if (open > 0f)
        {
            float lid = Mathf.Sqrt(open);
            float upper = lid * Mathf.Lerp(1f, 0.82f, Smooth(-0.2f, -1f, ex));
            float eye = Smooth(1.04f, 0.94f, ey > 0f ? ey / Mathf.Max(0.01f, upper) : -ey / Mathf.Max(0.01f, lid));

            float ix = (ax - 0.041f) / 0.0215f;
            float iy = (y - 1.6815f) / 0.0215f;
            float iris = Mathf.Sqrt(ix * ix + iy * iy);
            Color ball = Color.Lerp(new Color(0.98f, 0.90f, 0.24f), new Color(0.52f, 0.74f, 0.12f), Smooth(0.1f, 0.85f, iris));
            float slit = Sq(ix / 0.2f) + Sq(iy / 0.6f);
            ball = Over(ball, new Color(0.01f, 0.01f, 0.02f), Smooth(1.12f, 0.88f, slit));
            ball = Over(ball, Color.white, Smooth(0.14f, 0.09f, Vector2.Distance(new Vector2(ix, iy), new Vector2(-0.28f, 0.26f))));
            ball = Over(ball, new Color(0.03f, 0.06f, 0.04f), 0.5f * Smooth(0.25f, 0.95f, ey));   // el párpado le da sombra
            c = Over(c, ball, eye);
        }

        // Una línea clara sobre el párpado: sin ella, en el pelo negro no se lee la forma del ojo
        float lidY = 1.6815f + (ax - 0.0425f) * 0.3f + 0.0138f * Mathf.Sqrt(Mathf.Max(0f, open)) + 0.0024f;
        float rim = Smooth(0.0016f, 0.0006f, Mathf.Abs(y - lidY)) * Smooth(1f, 0.82f, Mathf.Abs(ex));
        return Over(c, FurSheen * 2.2f, 0.75f * rim);
    }

    private static Color PaintTorso(float u, float v)
    {
        float deg = (u - 0.5f) * 360f;
        float abs = Mathf.Abs(deg);
        float y = Mathf.Lerp(TorsoBottom, TorsoTop, v);

        // El traje oscuro, y al cuello el pelo
        Color c = Suit * Wrinkles(u * 2f, v);
        c = Over(c, BlackFur(u * 3f, v), Smooth(1.45f, 1.48f, y));

        // El pecho blanco: del mentón hasta el esternón
        float rag = (Mathf.PerlinNoise(u * 60f, v * 40f) - 0.5f) * 8f;
        float width = Mathf.Lerp(10f, 70f, Smooth(1.335f, 1.5f, y));
        float bib = Smooth(width + 2f, width - 2f, abs + rag) * Smooth(1.328f, 1.345f, y);
        c = Over(c, WhiteFur(u * 3f, v), bib);
        c = Over(c, FurShade, 0.4f * bib * Smooth(1.47f, 1.4f, y) * Smooth(10f, 0f, abs));

        float bottom = VestBottom(deg), top = VestTop(deg);
        float inside = Smooth(bottom - 0.002f, bottom + 0.002f, y) * Smooth(top + 0.002f, top - 0.002f, y);
        if (inside <= 0f) return c;

        // El chaleco, por paños cosidos
        Color leather = Grain(Leather, u * 3f, v, 40f, 0.16f);
        leather *= 1f + (Mathf.PerlinNoise(u * 12f, v * 9f) - 0.5f) * 0.4f;

        float panel = Mathf.Repeat(abs + 30f, 60f) - 30f;
        float seam = Mathf.Abs(panel);
        leather = Over(leather, LeatherDark, Smooth(28.4f, 29.6f, seam));
        float dash = Smooth(0.3f, 0.2f, Mathf.Abs(Mathf.Repeat(y * 90f, 1f) - 0.5f));
        leather = Over(leather, LeatherLight * 1.2f, Smooth(0.7f, 0.4f, Mathf.Abs(seam - 26f)) * dash);

        // Dos correas con hebilla a cada lado
        foreach (float strapY in new[] { 1.17f, 1.24f })
        {
            float strap = Mathf.Abs(y - strapY);
            float reach = Smooth(26f, 30f, abs) * Smooth(74f, 70f, abs);
            Color strapColor = Over(Grain(LeatherDark, u * 3f, v, 60f, 0.5f), Leather, Smooth(0.0045f, 0.002f, strap));
            leather = Over(leather, strapColor, Smooth(0.0125f, 0.0105f, strap) * reach);
            float buckle = Smooth(4.6f, 3.8f, Mathf.Abs(abs - 34f)) * Smooth(0.0165f, 0.0145f, strap);
            float hole = Smooth(2.6f, 1.8f, Mathf.Abs(abs - 34f)) * Smooth(0.0085f, 0.0065f, strap);
            leather = Over(leather, Brass, buckle * (1f - hole));
        }

        // Ribetes de bronce
        float edge = Mathf.Min(y - bottom, top - y);
        leather = Over(leather, LeatherDark, Smooth(0.016f, 0.012f, edge));
        leather = Over(leather, Brass, Smooth(0.0085f, 0.0065f, edge));
        leather = Over(leather, BrassDark, Smooth(0.0025f, 0.001f, edge));

        return Over(c, leather, inside);
    }

    private static Color PaintArm(float u, float v)
    {
        float d = Mathf.Lerp(-0.05f, 0.446f, v);
        float deg = (u - 0.5f) * 360f;
        float abs = Mathf.Abs(deg);

        Color c = Suit * Wrinkles(u, v);

        // Una correa en el brazo
        float strap = Mathf.Abs(d - 0.115f);
        c = Over(c, Over(Grain(Leather, u * 3f, v, 50f, 0.4f), LeatherDark, Smooth(0.0075f, 0.0105f, strap)), Smooth(0.0135f, 0.0115f, strap));

        // Brazal con cordones
        float bracer = Smooth(BracerStart - 0.002f, BracerStart + 0.002f, d) * Smooth(BracerEnd + 0.002f, BracerEnd - 0.002f, d);
        if (bracer > 0f)
        {
            Color leather = Grain(Leather, u * 3f, v, 40f, 0.16f);
            leather = Over(leather, LeatherDark, 0.7f * Smooth(120f, 160f, abs));

            float edge = Mathf.Min(d - BracerStart, BracerEnd - d);
            leather = Over(leather, LeatherLight, Smooth(0.0075f, 0.0045f, edge));

            float zig = Mathf.Abs(Mathf.Repeat(d / 0.026f, 1f) - 0.5f) * 2f;
            float lace = Smooth(3.2f, 2.2f, Mathf.Abs(abs - 150f + zig * -16f)) * Smooth(0.285f, 0.29f, d) * Smooth(0.402f, 0.398f, d);
            leather = Over(leather, Brass * 0.8f, lace);

            float stud = Mathf.Abs(Mathf.Repeat(deg, 60f) - 30f) * 0.0009f;
            float dot = Smooth(0.0052f, 0.0036f, Mathf.Sqrt(stud * stud + Sq(d - 0.30f)));
            leather = Over(leather, Brass, dot);
            c = Over(c, leather, bracer);
        }

        // Muñeca blanca
        return Over(c, WhiteFur(u * 2f, v), Smooth(BracerEnd - 0.002f, BracerEnd + 0.002f, d));
    }

    private static Color PaintLeg(float u, float v)
    {
        float d = Mathf.Lerp(-0.07f, 0.82f, v);
        float deg = (u - 0.5f) * 360f;
        float abs = Mathf.Abs(deg);

        // Pantalón, con pliegues y un parche en la rodilla
        Color c = Suit * Wrinkles(u, v);
        c = Over(c, Suit * 0.45f, 0.55f * Smooth(0.16f, 0f, d));

        float patch = Mathf.Max(Mathf.Abs(d - 0.41f) / 0.042f, abs / 44f);
        c = Over(c, Grain(LeatherDark, u * 2f, v, 60f, 0.6f), Smooth(1.02f, 0.97f, patch));
        c = Over(c, LeatherLight, Smooth(0.03f, 0.015f, Mathf.Abs(patch - 0.92f)) * 0.8f);

        // Bota con doblez
        float boot = Smooth(BootTop - 0.002f, BootTop + 0.002f, d);
        if (boot > 0f)
        {
            Color leather = Grain(Leather, u * 2f, v, 40f, 0.16f);
            leather *= 1f + (Mathf.PerlinNoise(u * 6f, v * 34f) - 0.5f) * 0.42f;
            leather = Over(leather, LeatherDark, 0.55f * Smooth(110f, 170f, abs));

            // El doblez, más claro, y los cordones del frente
            float cuff = Smooth(BootCuffEnd + 0.002f, BootCuffEnd - 0.002f, d);
            leather = Over(leather, LeatherLight, 0.75f * cuff);
            leather = Over(leather, LeatherDark, Smooth(0.0065f, 0.002f, Mathf.Abs(d - BootCuffEnd)));

            float zig = Mathf.Abs(Mathf.Repeat(d / 0.03f, 1f) - 0.5f) * 2f;
            float lace = Smooth(3.4f, 2.4f, Mathf.Abs(abs - zig * 15f)) * Smooth(0.52f, 0.525f, d) * Smooth(0.72f, 0.715f, d);
            leather = Over(leather, new Color(0.50f, 0.37f, 0.20f), lace);
            c = Over(c, leather, boot);
        }

        // Tobillo blanco
        return Over(c, WhiteFur(u * 2f, v), Smooth(BootEnd - 0.002f, BootEnd + 0.002f, d));
    }

    // La mitad izquierda de la franja es pelo negro y la derecha pelo blanco.
    private static Color PaintFur(float u, float v)
    {
        Color c = u < 0.5f ? BlackFur(u, v * 0.3f) : WhiteFur(u, v * 0.3f);
        return Over(c, u < 0.5f ? FurBlack : FurShade, Smooth(0.5f, 0.05f, v) * 0.7f);
    }

    private static Color PaintTail(float u, float v)
    {
        float tip = Smooth(0.875f, 0.895f, v + (Mathf.PerlinNoise(u * 16f, 3f) - 0.5f) * 0.05f);
        return Over(BlackFur(u * 2f, v * 4f), WhiteFur(u * 2f, v * 4f), tip);
    }

    private static Color PaintHood(float u, float v)
    {
        Color c = Grain(HoodTone, u, v, 60f, 0.14f);
        c *= 0.8f + 0.5f * Mathf.PerlinNoise(u * 12f, v * 8f);
        c = Over(c, HoodDark, 0.6f * Smooth(0.5f, 1f, v));

        // Borde con ribete y una costura al centro
        c = Over(c, Brass, Smooth(0.0225f, 0.018f, Mathf.Abs(v - 0.075f)) * 0.95f);
        c = Over(c, BrassDark, Smooth(0.01f, 0.004f, Mathf.Abs(v - 0.1f)));
        return Over(c, HoodDark, Smooth(0.009f, 0.003f, Mathf.Abs(u - 0.5f)));
    }

    private static Color PaintCape(float u, float v)
    {
        Color c = Grain(HoodTone, u, v, 60f, 0.14f);
        c *= 0.75f + 0.7f * Mathf.PerlinNoise(u * 15f, v * 1.2f + 4f);
        c = Over(c, HoodDark, 0.55f * Smooth(0.25f, 0f, v));

        // Los pliegues, con la misma onda de la malla (BuildCape)
        float s = u * 2f - 1f;
        c *= 1f + Mathf.Cos(s * 8.5f + 1.3f) * 0.3f * Smooth(0.95f, 0.35f, v);
        return c;
    }

    // v bajo = almohadillas de abajo; el resto, pelo blanco con su sombra.
    private static Color PaintPaw(float u, float v)
    {
        if (v < 0.2f) return Color.Lerp(NoseTone * 0.7f, NoseTone * 0.9f, Mathf.PerlinNoise(u * 12f, v * 12f));
        return Color.Lerp(FurShade, FurWhite, Smooth(0.3f, 0.7f, v));
    }

    private static Color PaintEar(float u, float v)
    {
        float angle = u * Mathf.PI * 2f;
        float front = Mathf.Sin(angle);
        Color c = BlackFur(u * 3f, v);

        // Adentro: rosa oscuro con el centro más claro
        float inner = Smooth(0.15f, 0.42f, front) * Smooth(0.92f, 0.76f, v);
        Color skin = Color.Lerp(new Color(0.24f, 0.09f, 0.13f), NoseTone * 0.72f, Smooth(0.55f, 1f, front) * Smooth(0.85f, 0.3f, v));
        c = Over(c, skin, inner);

        // Pelo blanco que nace de los bordes y sube por adentro
        float hairs = Smooth(0.36f, 0.56f, Mathf.PerlinNoise(u * 46f, v * 2.6f));
        float rim = Smooth(1f, 0.55f, front);
        return Over(c, WhiteFur(u, v), hairs * rim * inner * Smooth(0.75f, 0.15f, v));
    }

    private static Color PaintScarf(float u, float v)
    {
        Color c = Grain(ScarfTone, u, v, 60f, 0.12f);
        c *= 0.82f + 0.4f * Mathf.PerlinNoise(u * 16f, v * 3f);
        return Over(c, ScarfTone * 0.45f, 0.55f * Smooth(0.45f, 0.95f, v));
    }

    private static Color PaintBelt(float u, float v)
    {
        Color c = Grain(LeatherDark * 1.4f, u * 6f, v, 40f, 0.45f);
        float edge = Mathf.Abs(v - 0.5f) * 2f;
        c = Over(c, LeatherDark * 0.7f, Smooth(0.75f, 1f, edge));
        float stitch = Smooth(0.08f, 0.04f, Mathf.Abs(edge - 0.68f)) * Smooth(0.28f, 0.2f, Mathf.Abs(Mathf.Repeat(u * 150f, 1f) - 0.5f));
        return Over(c, new Color(0.44f, 0.31f, 0.17f), stitch);
    }

    private static Color PaintFlap(float u, float v)
    {
        Color c = Grain(Leather, u, v, 40f, 0.16f);
        float side = Mathf.Min(u, 1f - u);
        float edge = Mathf.Min(side, v * 0.9f);
        c = Over(c, LeatherDark, Smooth(0.09f, 0.05f, edge));
        return Over(c, Brass, Smooth(0.04f, 0.028f, edge) * 0.9f);
    }
}
