using System;
using UnityEngine;

// ============================================================
// ClassModelRecipe
//
// Lo que comparten las recetas de los modelos de clase (BarbarianModel, FighterModel): los huesos, las
// cuentas, los dedos, los mechones y los pinceles de la textura. Cada receta pone sus medidas y su ropa.
// Una receta nueva sale de copiar una que se parezca: ver ClassModelKit para qué arma cada pieza.
//
// Medidas en metros, en la pose T del esqueleto del jugador. +X es la derecha del personaje, +Z su
// frente. Los ángulos del cuerpo (deg): 0 al frente, 90 a su derecha, -90 a su izquierda, 180 atrás.
// ============================================================
public abstract class ClassModelRecipe
{
    protected static readonly Vector3 HeadCenter = new Vector3(0f, 1.685f, 0.02f);

    protected readonly ClassModelKit Kit = new ClassModelKit();
    protected readonly ModelAtlas Atlas = new ModelAtlas(2048);
    protected readonly ModelSkin Hips, Spine, Chest, Neck, HeadBone;

    private readonly System.Random _random;

    protected ClassModelRecipe(int seed)
    {
        _random = new System.Random(seed);
        Hips = Kit.Bone("B-hips");
        Spine = Kit.Bone("B-spine");
        Chest = Kit.Bone("B-chest");
        Neck = Kit.Bone("B-neck");
        HeadBone = Kit.Bone("B-head");
    }

    // El rectángulo de la textura que usan los mechones.
    protected abstract ModelRegion HairRegion { get; }

    // =========================================================
    // CUENTAS
    // =========================================================

    protected float Range(float from, float to) => from + (float)_random.NextDouble() * (to - from);

    protected static float Smooth(float from, float to, float x)
    {
        float t = Mathf.Clamp01((x - from) / (to - from));
        return t * t * (3f - 2f * t);
    }

    protected static float Bell(float x, float center, float width)
    {
        float d = (x - center) / width;
        return Mathf.Exp(-d * d);
    }

    protected static float Sq(float x) => x * x;

    protected static float Wrap(float deg)
    {
        while (deg > 180f) deg -= 360f;
        while (deg < -180f) deg += 360f;
        return deg;
    }

    protected static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        float m = 1f - t;
        return m * m * m * a + 3f * m * m * t * b + 3f * m * t * t * c + t * t * t * d;
    }

    // =========================================================
    // ESQUELETO
    // =========================================================

    protected static string SideName(float side) => side > 0f ? ".R" : ".L";

    protected Vector3[] ArmChain(string side) => new[]
    {
        Kit.Pos("B-upperArm" + side), Kit.Pos("B-forearm" + side), Kit.Pos("B-hand" + side),
    };

    protected Vector3[] LegChain(string side) => new[]
    {
        Kit.Pos("B-thigh" + side), Kit.Pos("B-shin" + side), Kit.Pos("B-foot" + side),
    };

    // La posición y la dirección a esa distancia por un camino de huesos.
    protected static void AlongChain(Vector3[] chain, float distance, out Vector3 point, out Vector3 tangent)
    {
        float left = distance;
        for (int i = 0; i < chain.Length - 1; i++)
        {
            Vector3 segment = chain[i + 1] - chain[i];
            float length = segment.magnitude;
            bool last = i == chain.Length - 2;
            if (left <= length || last)
            {
                tangent = segment / length;
                point = chain[i] + tangent * left;
                return;
            }
            left -= length;
        }
        tangent = Vector3.up;
        point = chain[0];
    }

    // =========================================================
    // MANOS
    // =========================================================

    protected void BuildFinger(string finger, string side, float radius, int sides, Func<float, float, Vector2> uv)
    {
        Vector3 a = Kit.Pos(finger + "01" + side);
        Vector3 b = Kit.Pos(finger + "02" + side);
        Vector3 c = Kit.Pos(finger + "03" + side);
        Vector3 tip = c + (c - b) * 0.85f;

        // Nace adentro de la palma: el pulgar está lejos del borde y sin esto queda flotando.
        Vector3 wrist = Kit.Pos("B-hand" + side);
        Vector3 root = finger == "B-thumb" ? Vector3.Lerp(wrist, a, 0.35f) : a + (wrist - a).normalized * 0.02f;

        ModelSkin hand = Kit.Bone("B-hand" + side);
        ModelSkin first = Kit.Bone(finger + "01" + side);
        ModelSkin second = Kit.Bone(finger + "02" + side);
        ModelSkin third = Kit.Bone(finger + "03" + side);

        Kit.Sweep(new[] { root, a, b, c, tip }, p => Vector3.up, t => Vector2.one * (radius * (1f - 0.22f * t)), sides, uv,
            (p, t) => t < 0.1f ? hand : t < 0.4f ? first : t < 0.6f ? first.To(second, 0.5f) : t < 0.9f ? second.To(third, 0.5f) : third);
    }

    // =========================================================
    // PELO
    // =========================================================

    // Arriba sigue a la cabeza; lo que cae sobre la espalda, al pecho y a la columna.
    protected ModelSkin HairSkin(Vector3 p)
    {
        ModelSkin skin = HeadBone.To(Neck, Smooth(1.60f, 1.54f, p.y));
        skin = skin.To(Chest, Smooth(1.54f, 1.44f, p.y));
        return skin.To(Spine, Smooth(1.26f, 1.1f, p.y));
    }

    protected static Vector3 Outward(Vector3 p)
    {
        var axis = new Vector3(0f, p.y, 0f);
        if (p.y > 1.56f) axis = HeadCenter;
        return (p - axis).normalized;
    }

    // Un mechón: una curva de la raíz a la punta, con una onda suave y sección aplanada.
    protected void Lock(Vector3 root, Vector3 control1, Vector3 control2, Vector3 tip, float width, float thick, int segments = 9, int sides = 5)
    {
        float u = Range(0.12f, 0.88f);
        float phase = Range(0f, 6.28f);
        float wave = Range(0.008f, 0.02f);

        var path = new Vector3[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 p = Bezier(root, control1, control2, tip, t);
            p.x += Mathf.Sin(t * 7f + phase) * wave * t;
            path[i] = p;
        }

        Kit.BeginPiece(28f);
        Kit.Sweep(path, Outward,
            t =>
            {
                float shape = Mathf.Min(1f, 0.32f + t * 2.6f) * (1f - Mathf.Pow(t, 2.6f));
                return new Vector2(Mathf.Max(0.0015f, width * shape), Mathf.Max(0.001f, thick * shape));
            },
            sides,
            (t, a) => HairRegion.Uv(u + 0.11f * Mathf.Abs(a * 2f - 1f) - 0.05f, 0.2f + t * 0.78f),
            (p, t) => HairSkin(p));
    }

    // =========================================================
    // PINCELES
    // =========================================================

    protected static Color Over(Color under, Color color, float amount) => Color.Lerp(under, color, Mathf.Clamp01(amount));

    protected static Color Grain(Color c, float u, float v, float scale, float strength)
    {
        float n = Mathf.PerlinNoise(u * scale + 3.1f, v * scale + 7.7f) - 0.5f;
        return c * (1f + n * strength);
    }
}
