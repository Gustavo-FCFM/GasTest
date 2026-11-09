using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// ============================================================
// ClassModelKit
//
// La caja de herramientas para generar por código los modelos de relleno de las clases
// (6 de noviembre de 2026, para el showcase; el arte de verdad los reemplaza en el campo "Modelo"
// de la clase). Un modelo sale de tres cosas:
//
//   · ClassModelKit: la malla. Se arma en la pose T del esqueleto del jugador, así que los huesos,
//     las animaciones, los sockets y el ragdoll son los de siempre (ver ClassModelSwap).
//   · ModelAtlas: UNA textura para todo el personaje, pintada con funciones (cada parte tiene su
//     rectángulo, ModelRegion).
//   · Una receta por clase (BarbarianModel) que dice qué forma tiene y de qué color es.
//
// Las piezas se sombrean con facetas o suaves según su ángulo de quiebre (BeginPiece).
// ============================================================

// Cómo refleja la luz el material del personaje.
public enum ModelFinish
{
    Matte,      // sin brillo: cuero, tela, piel
    Polished,   // el alfa de la textura dice qué tan pulido es cada lugar: la armadura brilla y la tela no
}

// A qué huesos sigue un vértice, hasta cuatro.
public readonly struct ModelSkin
{
    public readonly int[] Bones;
    public readonly float[] Weights;

    public ModelSkin(int bone)
    {
        Bones = new[] { bone };
        Weights = new[] { 1f };
    }

    private ModelSkin(int[] bones, float[] weights)
    {
        Bones = bones;
        Weights = weights;
    }

    // t = 0 es este, t = 1 es el otro.
    public ModelSkin To(ModelSkin other, float t)
    {
        t = Mathf.Clamp01(t);
        if (t <= 0f) return this;
        if (t >= 1f) return other;

        var sum = new Dictionary<int, float>();
        for (int i = 0; i < Bones.Length; i++) Add(sum, Bones[i], Weights[i] * (1f - t));
        for (int i = 0; i < other.Bones.Length; i++) Add(sum, other.Bones[i], other.Weights[i] * t);

        KeyValuePair<int, float>[] top = sum.OrderByDescending(p => p.Value).Take(4).ToArray();
        float total = top.Sum(p => p.Value);
        return new ModelSkin(top.Select(p => p.Key).ToArray(), top.Select(p => p.Value / total).ToArray());
    }

    private static void Add(Dictionary<int, float> sum, int bone, float weight)
    {
        sum.TryGetValue(bone, out float value);
        sum[bone] = value + weight;
    }

    public BoneWeight ToBoneWeight()
    {
        var w = new BoneWeight();
        if (Bones.Length > 0) { w.boneIndex0 = Bones[0]; w.weight0 = Weights[0]; }
        if (Bones.Length > 1) { w.boneIndex1 = Bones[1]; w.weight1 = Weights[1]; }
        if (Bones.Length > 2) { w.boneIndex2 = Bones[2]; w.weight2 = Weights[2]; }
        if (Bones.Length > 3) { w.boneIndex3 = Bones[3]; w.weight3 = Weights[3]; }
        return w;
    }
}

public struct ModelPoint
{
    public Vector3 Pos;
    public Vector2 Uv;
    public ModelSkin Skin;

    public ModelPoint(Vector3 pos, Vector2 uv, ModelSkin skin)
    {
        Pos = pos;
        Uv = uv;
        Skin = skin;
    }
}

// Una curva por puntos (x, y, x, y…), suave entre ellos. Son las tablas de medidas del cuerpo.
public sealed class ModelKeys
{
    private readonly float[] _x;
    private readonly float[] _y;

    public ModelKeys(params float[] pairs)
    {
        _x = new float[pairs.Length / 2];
        _y = new float[pairs.Length / 2];
        for (int i = 0; i < _x.Length; i++)
        {
            _x[i] = pairs[i * 2];
            _y[i] = pairs[i * 2 + 1];
        }
    }

    public float At(float x)
    {
        if (x <= _x[0]) return _y[0];
        int last = _x.Length - 1;
        if (x >= _x[last]) return _y[last];

        int i = 0;
        while (x > _x[i + 1]) i++;

        float h = _x[i + 1] - _x[i];
        float t = (x - _x[i]) / h;
        float m0 = Slope(i) * h;
        float m1 = Slope(i + 1) * h;
        float t2 = t * t;
        float t3 = t2 * t;
        return (2f * t3 - 3f * t2 + 1f) * _y[i] + (t3 - 2f * t2 + t) * m0
             + (-2f * t3 + 3f * t2) * _y[i + 1] + (t3 - t2) * m1;
    }

    private float Slope(int i)
    {
        int a = Mathf.Max(i - 1, 0);
        int b = Mathf.Min(i + 1, _x.Length - 1);
        return (_y[b] - _y[a]) / (_x[b] - _x[a]);
    }
}

// Un rectángulo de la textura. u y v van de 0 a 1 adentro de él.
public sealed class ModelRegion
{
    public readonly int X, Y, Width, Height;
    private readonly int _size;

    public ModelRegion(int x, int y, int width, int height, int atlasSize)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
        _size = atlasSize;
    }

    public Vector2 Uv(float u, float v)
    {
        float px = X + 1.5f + Mathf.Clamp01(u) * (Width - 3f);
        float py = Y + 1.5f + Mathf.Clamp01(v) * (Height - 3f);
        return new Vector2(px / _size, py / _size);
    }
}

public sealed class ModelAtlas
{
    public readonly int Size;
    private readonly Color32[] _pixels;
    private Color32[] _glow;   // lo que brilla con luz propia; null = nada (casi todos los modelos)

    // Cuánto brilla lo pintado con PaintGlow (×). El juego no tiene bloom: con más de ~1.6 el color se vuelve blanco.
    public float GlowIntensity = 1.3f;

    public ModelAtlas(int size)
    {
        Size = size;
        _pixels = new Color32[size * size];
    }

    public bool HasGlow => _glow != null;

    public ModelRegion Region(int x, int y, int width, int height) => new ModelRegion(x, y, width, height, Size);

    public void Paint(ModelRegion r, Func<float, float, Color> paint) => Fill(_pixels, r, paint);

    // La segunda textura (los ojos de la Paladina): negro donde no brilla. Usa las mismas regiones.
    public void PaintGlow(ModelRegion r, Func<float, float, Color> paint)
    {
        if (_glow == null) _glow = new Color32[Size * Size];
        Fill(_glow, r, paint);
    }

    private void Fill(Color32[] pixels, ModelRegion r, Func<float, float, Color> paint)
    {
        for (int y = 0; y < r.Height; y++)
        {
            float v = (y + 0.5f) / r.Height;
            for (int x = 0; x < r.Width; x++)
                pixels[(r.Y + y) * Size + r.X + x] = paint((x + 0.5f) / r.Width, v);
        }
    }

    public byte[] EncodePng() => Encode(_pixels);

    public byte[] EncodeGlowPng() => Encode(_glow);

    private byte[] Encode(Color32[] pixels)
    {
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply();
        byte[] png = texture.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(texture);
        return png;
    }
}

public sealed class ClassModelKit
{
    private const string PlayerShortcut = "Assets/Prefabs/Player/Player.prefab";

    private struct Vertex
    {
        public Vector3 Pos;
        public Vector2 Uv;
        public ModelSkin Skin;
    }

    private struct Face
    {
        public int A, B, C, Piece;
    }

    private readonly List<Vertex> _vertices = new List<Vertex>();
    private readonly List<Face> _faces = new List<Face>();
    private readonly List<float> _pieceCrease = new List<float>();

    private readonly SkinnedMeshRenderer _playerBody;
    private readonly string[] _boneNames;
    private readonly Matrix4x4[] _bindPoses;
    private readonly Dictionary<string, int> _boneIndex = new Dictionary<string, int>();

    public ClassModelKit()
    {
        _playerBody = FindPlayerBody();
        if (_playerBody == null) throw new InvalidOperationException("No encontré el cuerpo del prefab del jugador.");

        _boneNames = _playerBody.bones.Select(b => b.name).ToArray();
        _bindPoses = _playerBody.sharedMesh.bindposes;
        for (int i = 0; i < _boneNames.Length; i++) _boneIndex[_boneNames[i]] = i;
        BeginPiece(180f);
    }

    private static SkinnedMeshRenderer FindPlayerBody()
    {
        var shortcut = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerShortcut);
        if (shortcut != null && shortcut.GetComponent<PlayerController>() != null)
            return shortcut.GetComponentInChildren<SkinnedMeshRenderer>(true);

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab != null && prefab.GetComponent<PlayerController>() != null)
                return prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
        }
        return null;
    }

    // =========================================================
    // ESQUELETO
    // =========================================================

    public ModelSkin Bone(string name) => new ModelSkin(_boneIndex[name]);

    // Dónde está ese hueso en la pose T.
    public Vector3 Pos(string name)
    {
        Matrix4x4 m = _bindPoses[_boneIndex[name]].inverse;
        return m.GetColumn(3);
    }

    // =========================================================
    // MALLA
    // =========================================================

    // Lo que venga va suave entre caras con menos de este ángulo; arriba de él, con arista.
    public void BeginPiece(float creaseDegrees) => _pieceCrease.Add(creaseDegrees);

    private int Add(ModelPoint p)
    {
        _vertices.Add(new Vertex { Pos = p.Pos, Uv = p.Uv, Skin = p.Skin });
        return _vertices.Count - 1;
    }

    private void Triangle(int a, int b, int c)
    {
        _faces.Add(new Face { A = a, B = b, C = c, Piece = _pieceCrease.Count - 1 });
    }

    // Une anillos del mismo largo. wrap cierra el último punto de cada anillo con el primero (si la
    // costura de la textura no pide puntos repetidos). Una forma cerrada sale siempre hacia afuera.
    public void Loft(ModelPoint[][] rings, bool wrap = false, bool capStart = false, bool capEnd = false)
    {
        int firstFace = _faces.Count;
        int columns = rings[0].Length;
        var ids = new int[rings.Length][];

        for (int r = 0; r < rings.Length; r++)
        {
            ids[r] = new int[columns];
            for (int i = 0; i < columns; i++) ids[r][i] = Add(rings[r][i]);
        }

        int spans = wrap ? columns : columns - 1;
        for (int r = 0; r < rings.Length - 1; r++)
            for (int i = 0; i < spans; i++)
            {
                int next = (i + 1) % columns;
                Triangle(ids[r][i], ids[r + 1][i], ids[r + 1][next]);
                Triangle(ids[r][i], ids[r + 1][next], ids[r][next]);
            }

        if (capStart) Cap(rings[0], ids[0], wrap, false);
        if (capEnd) Cap(rings[rings.Length - 1], ids[rings.Length - 1], wrap, true);

        FaceOutward(firstFace);
    }

    // Una hoja que rodea al personaje (capa, falda): por afuera lo que traiga cada punto, por adentro
    // el color 'lining'. Son dos caras de verdad porque URP no da vuelta la luz en un material de doble cara.
    public void Sheet(ModelPoint[][] rings, Vector2 lining) => Sheet(rings, lining, null);

    // Lo mismo para una pieza que no rodea al personaje (una hombrera): 'inside' queda del lado del forro.
    public void Sheet(ModelPoint[][] rings, Vector2 lining, Vector3? inside)
    {
        int columns = rings[0].Length;
        var outer = new int[rings.Length][];
        var inner = new int[rings.Length][];
        
        for (int r = 0; r < rings.Length; r++)
        {
            outer[r] = new int[columns];
            inner[r] = new int[columns];
            for (int i = 0; i < columns; i++)
            {
                outer[r][i] = Add(rings[r][i]);
                inner[r][i] = Add(new ModelPoint(rings[r][i].Pos, lining, rings[r][i].Skin));
            }
        }

        // Si las caras en este orden miran hacia el eje del personaje, va al revés.
        float away = 0f;
        for (int r = 0; r < rings.Length - 1; r++)
            for (int i = 0; i < columns - 1; i++)
            {
                Vector3 a = rings[r][i].Pos, b = rings[r + 1][i].Pos, c = rings[r + 1][i + 1].Pos;
                Vector3 middle = (a + b + c) / 3f;
                if (inside.HasValue) middle -= inside.Value;
                else middle.y = 0f;
                away += Vector3.Dot(Vector3.Cross(b - a, c - a), middle);
            }
        bool flip = away < 0f;

        for (int r = 0; r < rings.Length - 1; r++)
            for (int i = 0; i < columns - 1; i++)
            {
                Face2(outer[r][i], outer[r + 1][i], outer[r + 1][i + 1], outer[r][i + 1], flip);
                Face2(inner[r][i], inner[r + 1][i], inner[r + 1][i + 1], inner[r][i + 1], !flip);
            }
    }

    private void Face2(int a, int b, int c, int d, bool flip)
    {
        if (flip)
        {
            Triangle(a, c, b);
            Triangle(a, d, c);
        }
        else
        {
            Triangle(a, b, c);
            Triangle(a, c, d);
        }
    }

    private void Cap(ModelPoint[] ring, int[] ids, bool wrap, bool reverse)
    {
        Vector3 center = Vector3.zero;
        foreach (ModelPoint p in ring) center += p.Pos;
        center /= ring.Length;

        int middle = Add(new ModelPoint(center, ring[0].Uv, ring[0].Skin));
        int spans = wrap ? ids.Length : ids.Length - 1;
        for (int i = 0; i < spans; i++)
        {
            int next = (i + 1) % ids.Length;
            if (reverse) Triangle(middle, ids[next], ids[i]);
            else Triangle(middle, ids[i], ids[next]);
        }
    }

    // El volumen encerrado sale positivo si las caras miran afuera.
    private void FaceOutward(int firstFace)
    {
        Vector3 center = Vector3.zero;
        int count = 0;
        for (int f = firstFace; f < _faces.Count; f++)
        {
            center += _vertices[_faces[f].A].Pos;
            count++;
        }
        if (count == 0) return;
        center /= count;

        float volume = 0f;
        for (int f = firstFace; f < _faces.Count; f++)
        {
            Vector3 a = _vertices[_faces[f].A].Pos - center;
            Vector3 b = _vertices[_faces[f].B].Pos - center;
            Vector3 c = _vertices[_faces[f].C].Pos - center;
            volume += Vector3.Dot(a, Vector3.Cross(b - a, c - a));
        }
        if (volume >= 0f) return;

        for (int f = firstFace; f < _faces.Count; f++)
        {
            Face face = _faces[f];
            (face.B, face.C) = (face.C, face.B);
            _faces[f] = face;
        }
    }

    // Un tubo de sección ovalada a lo largo de un camino: mechones, dedos, trenzas, correas.
    // outward dice hacia dónde mira la cara ancha en cada punto; size da (ancho, grosor) para t de 0 a 1.
    public void Sweep(Vector3[] path, Func<Vector3, Vector3> outward, Func<float, Vector2> size, int sides,
                      Func<float, float, Vector2> uv, Func<Vector3, float, ModelSkin> skin, bool capStart = false, bool capEnd = true)
    {
        var rings = new ModelPoint[path.Length][];
        for (int i = 0; i < path.Length; i++)
        {
            float t = i / (float)(path.Length - 1);
            Vector3 tangent = (path[Mathf.Min(i + 1, path.Length - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
            Vector3 up = Vector3.ProjectOnPlane(outward(path[i]), tangent).normalized;
            if (up.sqrMagnitude < 0.5f) up = Vector3.Cross(tangent, Vector3.right).normalized;
            Vector3 side = Vector3.Cross(up, tangent);
            Vector2 s = size(t);

            rings[i] = new ModelPoint[sides + 1];
            for (int j = 0; j <= sides; j++)
            {
                float a01 = j / (float)sides;
                float a = a01 * Mathf.PI * 2f;
                Vector3 p = path[i] + side * (Mathf.Cos(a) * s.x) + up * (Mathf.Sin(a) * s.y);
                rings[i][j] = new ModelPoint(p, uv(t, a01), skin(path[i], t));
            }
        }
        Loft(rings, false, capStart, capEnd);
    }

    // Una mata de pelaje: una punta aplanada que nace en 'root', sale hacia 'direction' y se cae hacia
    // -normal. u es la franja de la textura que le toca; v va de la raíz (0) a la punta (1).
    public void Tuft(Vector3 root, Vector3 direction, Vector3 normal, float length, float width, float thick,
                     float droop, ModelRegion region, float u, ModelSkin skin)
    {
        Vector3 side = Vector3.Cross(normal, direction).normalized;
        float[] stops = { 0f, 0.5f };
        var rings = new ModelPoint[stops.Length + 1][];

        for (int i = 0; i < stops.Length; i++)
        {
            float t = stops[i];
            Vector3 c = root + direction * (length * t) - normal * (droop * length * t * t);
            float w = width * (i == 0 ? 0.7f : 1f);
            float h = thick * (1f - t * 0.6f);

            rings[i] = new[]
            {
                new ModelPoint(c + side * w,   region.Uv(u + 0.09f, t), skin),
                new ModelPoint(c + normal * h, region.Uv(u, t),         skin),
                new ModelPoint(c - side * w,   region.Uv(u - 0.09f, t), skin),
                new ModelPoint(c - normal * h, region.Uv(u, t),         skin),
            };
        }

        Vector3 tip = root + direction * length - normal * (droop * length);
        var tipPoint = new ModelPoint(tip, region.Uv(u, 1f), skin);
        rings[stops.Length] = new[] { tipPoint, tipPoint, tipPoint, tipPoint };
        Loft(rings, wrap: true);
    }

    // Un bulto redondeado: los nudos de una trenza.
    public void Lump(Vector3 center, Vector3 axis, Vector3 front, float length, float width, float depth, int sides,
                     ModelRegion region, float u, ModelSkin skin)
    {
        Vector3 side = Vector3.Cross(axis, front).normalized;
        float[] rise = { -1f, -0.6f, 0f, 0.6f, 1f };
        float[] round = { 0f, 0.8f, 1f, 0.8f, 0f };

        var rings = new ModelPoint[rise.Length][];
        for (int r = 0; r < rise.Length; r++)
        {
            rings[r] = new ModelPoint[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                Vector3 p = center + axis * (rise[r] * length) + side * (Mathf.Cos(a) * width * round[r]) + front * (Mathf.Sin(a) * depth * round[r]);
                rings[r][i] = new ModelPoint(p, region.Uv(u + 0.05f * Mathf.Cos(a), 0.3f + 0.25f * (rise[r] + 1f)), skin);
            }
        }
        Loft(rings, wrap: true);
    }

    // Un rombo alargado (una gema, una cuenta, una tachuela).
    public void Gem(Vector3 center, Vector3 up, Vector3 front, float height, float width, float depth, int sides,
                    ModelRegion region, ModelSkin skin)
    {
        Vector3 side = Vector3.Cross(up, front).normalized;
        var ring = new ModelPoint[sides];
        for (int i = 0; i < sides; i++)
        {
            float a = i / (float)sides * Mathf.PI * 2f;
            Vector3 p = center + side * (Mathf.Cos(a) * width) + front * (Mathf.Sin(a) * depth);
            ring[i] = new ModelPoint(p, region.Uv(0.5f + 0.4f * Mathf.Cos(a), 0.5f), skin);
        }

        var top = new ModelPoint(center + up * height, region.Uv(0.5f, 0.95f), skin);
        var bottom = new ModelPoint(center - up * height, region.Uv(0.5f, 0.05f), skin);
        var topRing = new ModelPoint[sides];
        var bottomRing = new ModelPoint[sides];
        for (int i = 0; i < sides; i++)
        {
            topRing[i] = top;
            bottomRing[i] = bottom;
        }
        Loft(new[] { bottomRing, ring, topRing }, wrap: true);
    }

    // Un disco abombado apoyado sobre una superficie: tachones y escudos del cinturón.
    public void Disc(Vector3 center, Vector3 normal, Vector3 up, float radiusX, float radiusY, float height, int sides,
                     ModelRegion region, ModelSkin skin)
    {
        Vector3 side = Vector3.Cross(normal, up).normalized;
        up = Vector3.Cross(side, normal).normalized;
        float[] radius = { 1f, 1f, 0.82f, 0.74f, 0.42f, 0f };
        float[] lift = { -0.2f, 0.25f, 0.3f, 0.55f, 1f, 1.1f };
        float[] shade = { 0.1f, 0.85f, 0.4f, 0.9f, 0.6f, 1f };

        var rings = new ModelPoint[radius.Length][];
        for (int r = 0; r < radius.Length; r++)
        {
            rings[r] = new ModelPoint[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                Vector3 p = center + side * (Mathf.Cos(a) * radiusX * radius[r]) + up * (Mathf.Sin(a) * radiusY * radius[r])
                          + normal * (height * lift[r]);
                rings[r][i] = new ModelPoint(p, region.Uv(0.5f, shade[r]), skin);
            }
        }
        Loft(rings, wrap: true);
    }

    public int TriangleCount => _faces.Count;

    // =========================================================
    // ARMAR LA MALLA
    // =========================================================

    private void FillMesh(Mesh mesh, string name)
    {
        var faceNormals = new Vector3[_faces.Count];
        var byPosition = new Dictionary<(int, int, int, int), List<int>>();

        for (int f = 0; f < _faces.Count; f++)
        {
            Face face = _faces[f];
            Vector3 a = _vertices[face.A].Pos, b = _vertices[face.B].Pos, c = _vertices[face.C].Pos;
            faceNormals[f] = Vector3.Cross(b - a, c - a);

            foreach (int v in new[] { face.A, face.B, face.C })
            {
                (int, int, int, int) key = PositionKey(_vertices[v].Pos, face.Piece);
                if (!byPosition.TryGetValue(key, out List<int> list)) byPosition[key] = list = new List<int>();
                list.Add(f);
            }
        }

        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var weights = new List<BoneWeight>();
        var triangles = new List<int>();
        var unique = new Dictionary<(int, int, int, int), int>();

        for (int f = 0; f < _faces.Count; f++)
        {
            Face face = _faces[f];
            float limit = Mathf.Cos(_pieceCrease[face.Piece] * Mathf.Deg2Rad);
            Vector3 own = Unit(faceNormals[f]);
            if (own == Vector3.zero) continue;

            foreach (int v in new[] { face.A, face.B, face.C })
            {
                Vector3 sum = Vector3.zero;
                foreach (int other in byPosition[PositionKey(_vertices[v].Pos, face.Piece)])
                    if (Vector3.Dot(Unit(faceNormals[other]), own) >= limit) sum += faceNormals[other];
                Vector3 normal = Unit(sum);
                if (normal == Vector3.zero) normal = own;

                var key = (v, Mathf.RoundToInt(normal.x * 50f), Mathf.RoundToInt(normal.y * 50f), Mathf.RoundToInt(normal.z * 50f));
                if (!unique.TryGetValue(key, out int index))
                {
                    index = positions.Count;
                    unique[key] = index;
                    positions.Add(_vertices[v].Pos);
                    normals.Add(normal);
                    uvs.Add(_vertices[v].Uv);
                    weights.Add(_vertices[v].Skin.ToBoneWeight());
                }
                triangles.Add(index);
            }
        }

        mesh.Clear(false);   // false: también olvida los canales de antes, si no quedan tangentes con basura
        mesh.name = name;
        mesh.indexFormat = positions.Count > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(positions);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.boneWeights = weights.ToArray();
        mesh.bindposes = _bindPoses;
        mesh.RecalculateBounds();
    }

    // .normalized devuelve cero por debajo de 0.00001, y las caras de las puntas del pelo son más chicas
    // que eso. Una normal en cero da píxeles inválidos, y el bloom los vuelve una bola blanca.
    private static Vector3 Unit(Vector3 v)
    {
        float length = v.magnitude;
        return length > 1e-9f ? v / length : Vector3.zero;
    }

    private static (int, int, int, int) PositionKey(Vector3 p, int piece) =>
        (piece, Mathf.RoundToInt(p.x * 5000f), Mathf.RoundToInt(p.y * 5000f), Mathf.RoundToInt(p.z * 5000f));

    // Guarda la textura, el material, la malla y el prefab. Lo que ya existía se pisa conservando su
    // GUID, así lo que apunta al prefab (el campo "Modelo" de la clase) sigue apuntando.
    public GameObject Save(string name, string artFolder, string prefabFolder, ModelAtlas atlas, ModelFinish finish = ModelFinish.Matte)
    {
        CreateFolder(artFolder);
        CreateFolder(prefabFolder);

        Texture2D texture = SaveTexture($"{artFolder}/{name}_Atlas.png", atlas.EncodePng(), atlas.Size, finish == ModelFinish.Polished);
        Texture2D glow = atlas.HasGlow ? SaveTexture($"{artFolder}/{name}_Glow.png", atlas.EncodeGlowPng(), GlowSize, false) : null;
        Material material = SaveMaterial($"{artFolder}/Mat_{name}.mat", texture, finish, glow, atlas.GlowIntensity);
        Mesh mesh = SaveMesh($"{artFolder}/{name}_Mesh.asset", $"{name}_Mesh");
        GameObject prefab = SavePrefab(name, $"{prefabFolder}/Model_{name}.prefab", mesh, material);

        AssetDatabase.SaveAssets();
        Debug.Log($"[ClassModelKit] {name}: {mesh.vertexCount} vértices, {mesh.triangles.Length / 3} triángulos.");
        return prefab;
    }

    private static void CreateFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        CreateFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // Lo que brilla es chico y difuso: alcanza con una textura más chica (Unity la achica al importarla).
    private const int GlowSize = 512;

    private static Texture2D SaveTexture(string path, byte[] png, int maxSize, bool alpha)
    {
        File.WriteAllBytes(path, png);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.maxTextureSize = maxSize;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.anisoLevel = 4;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        importer.alphaIsTransparency = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Material SaveMaterial(string path, Texture2D texture, ModelFinish finish, Texture2D glow, float glowIntensity)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", 0.12f);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Cull", 2f);

        if (finish == ModelFinish.Polished)
        {
            // Flujo especular, con la suavidad en el alfa de la textura: la luz corre por el metal sin
            // oscurecerlo, porque el color del acero ya está pintado.
            material.SetFloat("_WorkflowMode", 0f);
            material.EnableKeyword("_SPECULAR_SETUP");
            material.SetColor("_SpecColor", new Color(0.18f, 0.18f, 0.2f));
            material.SetFloat("_SmoothnessTextureChannel", 1f);
            material.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            material.SetFloat("_Smoothness", 0.72f);
            material.SetFloat("_SpecularHighlights", 1f);
            material.SetFloat("_EnvironmentReflections", 1f);
            material.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        }
        else
        {
            material.SetFloat("_SpecularHighlights", 0f);       // el brillo lo lleva la pintura
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        }

        if (glow != null)
        {
            // Luz propia: se ve igual de brillante en la sombra
            material.SetTexture("_EmissionMap", glow);
            material.SetColor("_EmissionColor", new Color(glowIntensity, glowIntensity, glowIntensity, 1f));
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else if (material.IsKeywordEnabled("_EMISSION"))
        {
            material.DisableKeyword("_EMISSION");
            material.SetTexture("_EmissionMap", null);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private Mesh SaveMesh(string path, string name)
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null)
        {
            FillMesh(mesh, name);
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        mesh = new Mesh();
        FillMesh(mesh, name);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    // El prefab es una copia del esqueleto del Dummy con la malla nueva: ClassModelSwap lee de acá los
    // nombres de los huesos, y sirve para verlo en el Inspector.
    private GameObject SavePrefab(string name, string path, Mesh mesh, Material material)
    {
        Animator animator = _playerBody.GetComponentInParent<Animator>(true);
        string modelPath = AssetDatabase.GetAssetPath(animator.avatar);

        var copy = (GameObject)UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
        copy.name = $"Model_{name}";

        foreach (SkinnedMeshRenderer old in copy.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            UnityEngine.Object.DestroyImmediate(old.gameObject);

        var bones = new Dictionary<string, Transform>();
        foreach (Transform t in copy.GetComponentsInChildren<Transform>(true)) bones[t.name] = t;

        var body = new GameObject($"{name}_Body");
        body.transform.SetParent(copy.transform, false);

        var renderer = body.AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh = mesh;
        renderer.sharedMaterial = material;
        renderer.bones = _boneNames.Select(n => bones[n]).ToArray();
        renderer.rootBone = bones[_playerBody.rootBone.name];
        renderer.localBounds = new Bounds(Vector3.zero, new Vector3(2.2f, 2.4f, 1.4f));

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(copy, path);
        UnityEngine.Object.DestroyImmediate(copy);
        return prefab;
    }
}
