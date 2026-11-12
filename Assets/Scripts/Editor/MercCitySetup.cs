using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// ============================================================
// MercCitySetup
//
// La CIUDAD de afuera del Cementerio (12 de noviembre de 2026, pedido de Gustavo): para que quien salta o vuela alto no
// vea un vacío negro alrededor del mapa. Es solo decorado: sin colliders, sin sombras, fuera del alcance del juego (el
// techo invisible y las paredes de MercArenaBounds no dejan llegar).
//
//   1. Mide el borde del mapa en la escena abierta (rayos hacia abajo cada medio grado, en la cuña de Sector_Team1).
//   2. Arma en esa cuña de 120°: el suelo (desde el borde hasta 320 m), filas de casas alrededor (paredes de yeso o
//      ladrillo, techo a dos aguas, chimeneas, ventanas: algunas encendidas), una torre de vez en cuando, una iglesia
//      con campanario y una torre del reloj. Delante de cada base queda una plaza (las casas empiezan más lejos).
//   3. Junta todo en pocas mallas, una por material (Art/Models/City), y las pone en Graveyard_Sector ▸ City: así sale
//      en los tres sectores, como las antorchas.
//
// Siempre sale igual (semilla fija). Correrla de nuevo no toca la ciudad que ya está; Rebuild() la rehace.
// Lo lejano lo tapa la niebla de la noche: más allá de ~160 m todo es silueta.
// ============================================================
public static class MercCitySetup
{
    private const string SectorPath = "Assets/Prefabs/Map/Graveyard_Sector.prefab";
    private const string ModelDir = "Assets/Art/Models/City";
    private const string MatDir = "Assets/Art/Materials/City";
    private const string TexDir = "Assets/Art/Materials/Ramiro Materials/10 Miscellaneous Handpainted Texture Pack by RamiroME/Textures/";

    private const float WedgeHalf = 60f;      // ° a cada lado de la base del sector
    private const float FirstRow = 74f;       // m: radio de la primera fila de casas
    private const float RowStep = 15f;        // m entre filas (la casa más honda mide 9: queda una calle de 6)
    private const int Rows = 9;
    private const float ClearFromEdge = 8f;   // m de calle entre el borde del mapa y la primera casa
    private const float GroundOuter = 320f;
    private const float GroundY = -0.05f;
    private const float Floor = 3f;           // m por piso

    // Las piezas: una malla y un material cada una.
    private enum EPart { PlasterOchre, PlasterGray, Brick, Stone, RoofRed, RoofSlate, Ground, WindowLit, WindowDark }

    [MenuItem("Mercenarios/Mapa/Armar la ciudad de afuera del Cementerio")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Rebuild() => Run(true);

    public static string Run(bool rebuild = false)
    {
        var log = new StringBuilder("[Ciudad] ");
        Transform sector = null;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t.name == "Sector_Team1") { sector = t; break; }
        if (sector == null) return log.Append("¡Ojo! Abre la escena del Cementerio: no encontré Sector_Team1.").ToString();

        GameObject root = PrefabUtility.LoadPrefabContents(SectorPath);
        try
        {
            Transform old = root.transform.Find("City");
            if (old != null && !rebuild) return log.Append("El sector ya tenía la ciudad (Rebuild() la rehace).").ToString();

            Physics.SyncTransforms();
            float[] edge = MeasureEdge();

            var bufs = new Dictionary<EPart, Buf>();
            foreach (EPart p in System.Enum.GetValues(typeof(EPart))) bufs[p] = new Buf(TileOf(p));

            var city = new CityBuilder(bufs, edge, new System.Random(1234));
            city.Ground();
            int houses = city.Houses();
            city.Landmarks();

            // Del mundo (Sector_Team1 en la escena) al espacio del prefab del sector.
            Matrix4x4 toLocal = sector.worldToLocalMatrix;
            if (old != null) Object.DestroyImmediate(old.gameObject);
            Transform group = new GameObject("City").transform;
            group.SetParent(root.transform, false);

            if (!AssetDatabase.IsValidFolder(ModelDir)) AssetDatabase.CreateFolder("Assets/Art/Models", "City");
            int verts = 0;
            foreach (var kv in bufs)
            {
                if (kv.Value.V.Count == 0) continue;
                Mesh mesh = SaveMesh(kv.Key, kv.Value, toLocal);
                verts += mesh.vertexCount;

                GameObject go = new GameObject("City_" + kv.Key);
                go.transform.SetParent(group, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = MaterialOf(kv.Key);
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            PrefabUtility.SaveAsPrefabAsset(root, SectorPath);
            AssetDatabase.SaveAssets();
            return log.Append($"Lista: {houses} casas por sector (×3), iglesia, torre del reloj y suelo hasta {GroundOuter} m; " +
                              $"{verts} vértices por sector. En {SectorPath} ▸ City.").ToString();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // =========================================================
    // EL BORDE DEL MAPA
    // =========================================================

    // Radio del borde del mapa cada medio grado, de −60° a 60° (0° = hacia la base de Sector_Team1, el +X del mundo).
    private static float[] MeasureEdge()
    {
        int n = Mathf.RoundToInt(WedgeHalf * 2f / 0.5f) + 1;
        var edge = new float[n];
        int mask = ~(1 << 2);   // sin Ignore Raycast
        for (int i = 0; i < n; i++)
        {
            float phi = (-WedgeHalf + i * 0.5f) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(phi), 0f, Mathf.Sin(phi));
            float found = 40f;
            for (float r = 40f; r <= 140f; r += 0.25f)
                if (Physics.Raycast(dir * r + Vector3.up * 40f, Vector3.down, 60f, mask, QueryTriggerInteraction.Ignore))
                    found = r;
            edge[i] = found;
        }
        return edge;
    }

    // =========================================================
    // MALLAS Y MATERIALES
    // =========================================================

    private static Vector2 TileOf(EPart p)
    {
        switch (p)
        {
            case EPart.Brick:
            case EPart.Stone:     return new Vector2(3f, 1.5f);
            case EPart.RoofRed:
            case EPart.RoofSlate: return new Vector2(2.5f, 2.5f);
            case EPart.Ground:    return new Vector2(5f, 5f);
            default:              return new Vector2(4f, 4f);
        }
    }

    private static Mesh SaveMesh(EPart part, Buf buf, Matrix4x4 toLocal)
    {
        string path = $"{ModelDir}/City_{part}.asset";
        var verts = new List<Vector3>(buf.V.Count);
        foreach (Vector3 v in buf.V) verts.Add(toLocal.MultiplyPoint3x4(v));

        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool isNew = mesh == null;
        if (isNew) mesh = new Mesh();
        mesh.Clear();
        mesh.name = "City_" + part;
        mesh.indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(verts);
        mesh.SetUVs(0, buf.U);
        mesh.SetTriangles(buf.T, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        if (isNew) AssetDatabase.CreateAsset(mesh, path);
        else EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static Material MaterialOf(EPart part)
    {
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/Art/Materials", "City");
        string path = $"{MatDir}/Mat_City{part}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        bool unlit = part == EPart.WindowLit || part == EPart.WindowDark;
        mat = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
        switch (part)
        {
            case EPart.PlasterOchre: Textured(mat, "Wallpaper_02", new Color(0.85f, 0.80f, 0.72f)); break;
            case EPart.PlasterGray:  Textured(mat, "Wallpaper_04", new Color(0.85f, 0.85f, 0.88f)); break;
            case EPart.Brick:        Textured(mat, "BrickTile_01", new Color(0.75f, 0.70f, 0.68f)); break;
            case EPart.Stone:        Textured(mat, "BrickTile_03", new Color(0.80f, 0.80f, 0.80f)); break;
            case EPart.RoofRed:      Textured(mat, "WoodTrunk_04", new Color(0.80f, 0.70f, 0.68f)); break;
            case EPart.RoofSlate:    Textured(mat, "WoodTrunk_02", new Color(0.70f, 0.72f, 0.78f)); break;
            case EPart.Ground:       Textured(mat, "Ground_02",    new Color(0.75f, 0.75f, 0.75f)); break;
            case EPart.WindowLit:    mat.SetColor("_BaseColor", new Color(1.00f, 0.70f, 0.36f)); break;
            case EPart.WindowDark:   mat.SetColor("_BaseColor", new Color(0.05f, 0.06f, 0.09f)); break;
        }
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.08f);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static void Textured(Material mat, string texture, Color tint)
    {
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + texture + ".png"));
        mat.SetColor("_BaseColor", tint);
    }

    // Una malla a medio armar: vértices, UV (en metros / tamaño de la textura) y triángulos.
    private class Buf
    {
        public readonly List<Vector3> V = new List<Vector3>();
        public readonly List<Vector2> U = new List<Vector2>();
        public readonly List<int> T = new List<int>();
        public readonly Vector2 Tile;
        public Buf(Vector2 tile) { Tile = tile; }

        // a = abajo izquierda, b = arriba izquierda, c = arriba derecha, d = abajo derecha, vistos desde afuera.
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = V.Count;
            V.Add(a); V.Add(b); V.Add(c); V.Add(d);
            U.Add(Scale(ua)); U.Add(Scale(ub)); U.Add(Scale(uc)); U.Add(Scale(ud));
            T.Add(i); T.Add(i + 1); T.Add(i + 2);
            T.Add(i); T.Add(i + 2); T.Add(i + 3);
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc)
        {
            int i = V.Count;
            V.Add(a); V.Add(b); V.Add(c);
            U.Add(Scale(ua)); U.Add(Scale(ub)); U.Add(Scale(uc));
            T.Add(i); T.Add(i + 1); T.Add(i + 2);
        }

        private Vector2 Scale(Vector2 meters) => new Vector2(meters.x / Tile.x, meters.y / Tile.y);
    }

    // =========================================================
    // LA CIUDAD
    // =========================================================

    private class CityBuilder
    {
        private readonly Dictionary<EPart, Buf> _b;
        private readonly float[] _edge;
        private readonly System.Random _rng;
        private readonly List<(Vector3 c, float r)> _reserved = new List<(Vector3, float)>();

        public CityBuilder(Dictionary<EPart, Buf> bufs, float[] edge, System.Random rng)
        {
            _b = bufs; _edge = edge; _rng = rng;
            // Lugar para la iglesia y la torre del reloj: ahí no van casas.
            _reserved.Add((Polar(34f, 120f), 22f));
            _reserved.Add((Polar(-28f, 148f), 9f));
        }

        private float Rand(float a, float b) => a + (float)_rng.NextDouble() * (b - a);
        private bool Chance(float p) => _rng.NextDouble() < p;

        private static Vector3 Polar(float deg, float r)
        {
            float a = deg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
        }

        // El borde más lejano entre dos ángulos (con un margen de 3°).
        private float EdgeBetween(float fromDeg, float toDeg)
        {
            int i0 = Mathf.Clamp(Mathf.FloorToInt((fromDeg - 3f + WedgeHalf) / 0.5f), 0, _edge.Length - 1);
            int i1 = Mathf.Clamp(Mathf.CeilToInt((toDeg + 3f + WedgeHalf) / 0.5f), 0, _edge.Length - 1);
            float max = 0f;
            for (int i = i0; i <= i1; i++) max = Mathf.Max(max, _edge[i]);
            return max;
        }

        // --- el suelo: del borde del mapa hasta GroundOuter, en anillos ---
        public void Ground()
        {
            float[] rings = { 0f, 100f, 160f, 230f, GroundOuter };
            for (int i = 0; i < _edge.Length - 1; i++)
            {
                float a0 = (-WedgeHalf + i * 0.5f) * Mathf.Deg2Rad, a1 = (-WedgeHalf + (i + 1) * 0.5f) * Mathf.Deg2Rad;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                float in0 = _edge[i] + 0.2f, in1 = _edge[i + 1] + 0.2f;
                for (int k = 0; k < rings.Length - 1; k++)
                {
                    float r0a = Mathf.Max(rings[k], in0), r0b = Mathf.Max(rings[k], in1);
                    float r1 = rings[k + 1];
                    if (r1 <= r0a && r1 <= r0b) continue;
                    Vector3 p00 = d0 * r0a, p01 = d1 * r0b, p10 = d0 * Mathf.Max(r1, r0a), p11 = d1 * Mathf.Max(r1, r0b);
                    p00.y = p01.y = p10.y = p11.y = GroundY;
                    AddUp(EPart.Ground, p00, p10, p11, p01);
                }
            }
        }

        private static Vector2 XZ(Vector3 p) => new Vector2(p.x, p.z);

        // --- las filas de casas ---
        public int Houses()
        {
            int count = 0;
            for (int row = 0; row < Rows; row++)
            {
                float R = FirstRow + row * RowStep;
                float s = Rand(0f, 3f);                        // arco recorrido desde −60°
                float arc = WedgeHalf * 2f * Mathf.Deg2Rad * R;
                while (true)
                {
                    float w = Rand(5.5f, 10.5f);
                    if (s + w > arc - 1f) break;
                    float mid = -WedgeHalf + (s + w * 0.5f) / R * Mathf.Rad2Deg;
                    float half = w * 0.5f / R * Mathf.Rad2Deg;
                    float d = Rand(6.5f, 9f);
                    bool fits = R - d * 0.5f >= EdgeBetween(mid - half, mid + half) + ClearFromEdge && !Reserved(Polar(mid, R), Mathf.Max(w, d) * 0.6f);
                    if (fits)
                    {
                        if (Chance(0.05f) && row > 1) Tower(mid, R, Rand(4.5f, 5.5f), Rand(12f, 17f));
                        else House(mid, R, w, d);
                        count++;
                    }
                    // Lo que sigue: pegada, un callejón o una calle que cruza.
                    s += w + (Chance(0.12f) ? Rand(6f, 8f) : Chance(0.3f) ? Rand(1.2f, 2.5f) : 0.15f);
                }
            }
            return count;
        }

        private bool Reserved(Vector3 p, float r)
        {
            foreach (var (c, rr) in _reserved) if ((p - c).sqrMagnitude < (r + rr) * (r + rr)) return true;
            return false;
        }

        private void House(float deg, float R, float w, float d)
        {
            Vector3 c = Polar(deg, R);
            Vector3 radial = c.normalized;
            Vector3 tangent = new Vector3(-radial.z, 0f, radial.x);

            int floors = Chance(0.25f) ? 1 : Chance(0.67f) ? 2 : 3;
            float h = floors * Floor + 0.4f;
            EPart wall = Chance(0.45f) ? EPart.PlasterOchre : Chance(0.64f) ? EPart.PlasterGray : EPart.Brick;
            EPart roof = Chance(0.65f) ? EPart.RoofRed : EPart.RoofSlate;

            // La mayoría con el caballete a lo largo de la calle; algunas con el triángulo hacia la calle.
            bool gableToStreet = Chance(0.25f);
            Vector3 along = gableToStreet ? radial : tangent;
            Vector3 across = gableToStreet ? tangent : radial;
            float ha = (gableToStreet ? d : w) * 0.5f, hb = (gableToStreet ? w : d) * 0.5f;
            float roofH = hb * Rand(0.7f, 1.05f);

            Walls(wall, c, along, across, ha, hb, -1f, h);
            GableRoof(roof, wall, c, along, across, ha, hb, h, roofH, 0.35f);
            Windows(c, along, across, ha, hb, h, floors, door: true);
            if (Chance(0.45f))
            {
                Vector3 ch = c + along * (ha * Rand(-0.6f, 0.6f)) + across * (hb * 0.35f * (Chance(0.5f) ? 1f : -1f));
                Box(EPart.Brick, ch, along, across, 0.35f, 0.35f, h, h + roofH + 0.9f, top: true);
            }
        }

        private void Tower(float deg, float R, float size, float h)
        {
            Vector3 c = Polar(deg, R);
            Vector3 radial = c.normalized, tangent = new Vector3(-radial.z, 0f, radial.x);
            float hs = size * 0.5f;
            Walls(EPart.Stone, c, tangent, radial, hs, hs, -1f, h);
            PyramidRoof(Chance(0.5f) ? EPart.RoofSlate : EPart.RoofRed, c, tangent, radial, hs + 0.3f, h, size * 0.9f);
            SlitWindows(c, tangent, radial, hs, h);
        }

        // --- la iglesia (nave + campanario con aguja) y la torre del reloj ---
        public void Landmarks()
        {
            Vector3 c = Polar(34f, 120f);
            Vector3 radial = c.normalized, tangent = new Vector3(-radial.z, 0f, radial.x);
            Walls(EPart.Stone, c, radial, tangent, 12f, 5.5f, -1f, 11f);
            GableRoof(EPart.RoofSlate, EPart.Stone, c, radial, tangent, 12f, 5.5f, 11f, 6f, 0.4f);
            for (int i = -2; i <= 2; i++)   // ventanales altos a los costados
                foreach (float side in new[] { -1f, 1f })
                    Window(c + radial * (i * 4.5f) + tangent * (5.5f * side), tangent * side, 1.2f, 4.5f, 6f, Chance(0.6f));

            Vector3 t = c - radial * 15.5f;   // el campanario, del lado del cementerio
            Walls(EPart.Stone, t, radial, tangent, 3.5f, 3.5f, -1f, 26f);
            PyramidRoof(EPart.RoofSlate, t, radial, tangent, 3.8f, 26f, 14f);
            SlitWindows(t, radial, tangent, 3.5f, 26f);
            Window(t - radial * 3.5f, -radial, 1.6f, 3f, 1.5f, false);   // la puerta

            Vector3 k = Polar(-28f, 148f);   // la torre del reloj
            Vector3 kr = k.normalized, kt = new Vector3(-kr.z, 0f, kr.x);
            Walls(EPart.Brick, k, kt, kr, 3f, 3f, -1f, 22f);
            PyramidRoof(EPart.RoofRed, k, kt, kr, 3.4f, 22f, 6f);
            Window(k - kr * 3f, -kr, 2.2f, 2.2f, 18.5f, true);   // la esfera del reloj, encendida
            SlitWindows(k, kt, kr, 3f, 16f);
        }

        // =========================================================
        // PIEZAS
        // =========================================================

        // Las cuatro paredes de una caja (sin techo), de y0 a y1.
        private void Walls(EPart part, Vector3 c, Vector3 a, Vector3 b, float ha, float hb, float y0, float y1)
        {
            foreach (var (n, half, depth) in Faces(a, b, ha, hb))
            {
                Vector3 right = -Vector3.Cross(Vector3.up, n);
                Vector3 fc = c + n * depth;
                Vector3 p0 = fc - right * half, p1 = fc + right * half;
                float u0 = Vector3.Dot(p0, right);
                _b[part].Quad(At(p0, y0), At(p0, y1), At(p1, y1), At(p1, y0),
                              new Vector2(u0, y0), new Vector2(u0, y1), new Vector2(u0 + half * 2f, y1), new Vector2(u0 + half * 2f, y0));
            }
        }

        // Caja cerrada arriba (chimeneas).
        private void Box(EPart part, Vector3 c, Vector3 a, Vector3 b, float ha, float hb, float y0, float y1, bool top)
        {
            Walls(part, c, a, b, ha, hb, y0, y1);
            if (!top) return;
            Vector3 p00 = At(c - a * ha - b * hb, y1), p01 = At(c - a * ha + b * hb, y1);
            Vector3 p11 = At(c + a * ha + b * hb, y1), p10 = At(c + a * ha - b * hb, y1);
            AddUp(part, p00, p01, p11, p10);
        }

        // Una cara horizontal que mira hacia arriba, sin importar el orden en que vengan las esquinas.
        private void AddUp(EPart part, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            bool up = Vector3.Cross(p1 - p0, p2 - p0).y > 0f;
            if (up) _b[part].Quad(p0, p1, p2, p3, XZ(p0), XZ(p1), XZ(p2), XZ(p3));
            else _b[part].Quad(p3, p2, p1, p0, XZ(p3), XZ(p2), XZ(p1), XZ(p0));
        }

        // Techo a dos aguas: el caballete a lo largo de a; los triángulos de las puntas van con la pared.
        private void GableRoof(EPart roof, EPart wall, Vector3 c, Vector3 a, Vector3 b, float ha, float hb, float h, float rise, float over)
        {
            float top = h + rise;
            float eave = h - over * rise / hb;
            float slope = Mathf.Sqrt((hb + over) * (hb + over) + (top - eave) * (top - eave));
            foreach (float side in new[] { 1f, -1f })
            {
                Vector3 n = b * side;
                Vector3 right = -Vector3.Cross(Vector3.up, n);
                Vector3 e = c + n * (hb + over);
                float L = ha + over;
                Vector3 ea = At(e - right * L, eave), eb = At(e + right * L, eave);
                Vector3 ra = At(c - right * L, top), rb = At(c + right * L, top);
                float u0 = Vector3.Dot(ea, right);
                _b[roof].Quad(ea, ra, rb, eb, new Vector2(u0, 0f), new Vector2(u0, slope), new Vector2(u0 + 2f * L, slope), new Vector2(u0 + 2f * L, 0f));
                // Y por debajo del alero (si no, desde abajo se ve a través).
                _b[roof].Quad(eb, rb, ra, ea, new Vector2(0f, 0f), new Vector2(0f, slope), new Vector2(2f * L, slope), new Vector2(2f * L, 0f));
            }
            foreach (float side in new[] { 1f, -1f })
            {
                Vector3 n = a * side;
                Vector3 right = -Vector3.Cross(Vector3.up, n);
                Vector3 fc = c + n * ha;
                Vector3 l = At(fc - right * hb, h), r = At(fc + right * hb, h), apex = At(fc, top);
                float u0 = Vector3.Dot(l, right);
                _b[wall].Tri(l, apex, r, new Vector2(u0, h), new Vector2(u0 + hb, top), new Vector2(u0 + 2f * hb, h));
            }
        }

        // Techo de cuatro caras que terminan en punta (torres, el campanario, la aguja).
        private void PyramidRoof(EPart roof, Vector3 c, Vector3 a, Vector3 b, float half, float h, float rise)
        {
            Vector3 apex = At(c, h + rise);
            float slope = Mathf.Sqrt(half * half + rise * rise);
            foreach (var (n, _, _) in Faces(a, b, half, half))
            {
                Vector3 right = -Vector3.Cross(Vector3.up, n);
                Vector3 fc = c + n * half;
                Vector3 l = At(fc - right * half, h), r = At(fc + right * half, h);
                _b[roof].Tri(l, apex, r, new Vector2(0f, 0f), new Vector2(half, slope), new Vector2(2f * half, 0f));
                _b[roof].Tri(r, apex, l, new Vector2(0f, 0f), new Vector2(half, slope), new Vector2(2f * half, 0f));   // por debajo
            }
        }

        // Ventanas en las caras que se ven desde el cementerio (no en la de atrás), y la puerta.
        private void Windows(Vector3 c, Vector3 a, Vector3 b, float ha, float hb, float h, int floors, bool door)
        {
            Vector3 toCenter = -c.normalized;
            foreach (var (n, half, depth) in Faces(a, b, ha, hb))
            {
                float facing = Vector3.Dot(n, toCenter);
                if (facing < -0.3f) continue;   // la de atrás no se ve
                bool front = facing > 0.7f;
                Vector3 right = -Vector3.Cross(Vector3.up, n);
                Vector3 fc = c + n * depth;
                int cols = Mathf.Max(1, Mathf.FloorToInt(half * 2f / 2.6f));
                for (int f = 0; f < floors; f++)
                    for (int i = 0; i < cols; i++)
                    {
                        float x = -half + (i + 0.5f) * (half * 2f / cols);
                        bool isDoor = door && front && f == 0 && i == cols / 2;
                        if (isDoor) { Window(fc + right * x, n, 1.3f, 2.3f, 0f, Chance(0.15f)); continue; }
                        if (!front && Chance(0.35f)) continue;
                        Window(fc + right * x, n, 0.9f, 1.25f, f * Floor + 1.05f, Chance(0.28f));
                    }
            }
        }

        private void SlitWindows(Vector3 c, Vector3 a, Vector3 b, float half, float h)
        {
            foreach (var (n, _, _) in Faces(a, b, half, half))
                for (float y = 4f; y < h - 3f; y += 4.5f)
                    Window(c + n * half, n, 0.5f, 1.6f, y, Chance(0.3f));
        }

        // Un rectángulo pegado a la pared (5 cm afuera), del piso y0 hacia arriba.
        private void Window(Vector3 wallPoint, Vector3 n, float w, float h, float y0, bool lit)
        {
            Vector3 right = -Vector3.Cross(Vector3.up, n);
            Vector3 p = wallPoint + n * 0.05f;
            Vector3 l = p - right * (w * 0.5f), r = p + right * (w * 0.5f);
            _b[lit ? EPart.WindowLit : EPart.WindowDark].Quad(At(l, y0), At(l, y0 + h), At(r, y0 + h), At(r, y0),
                Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
        }

        // Las cuatro caras de una caja: (normal hacia afuera, media anchura de la cara, distancia del centro).
        private static IEnumerable<(Vector3 n, float half, float depth)> Faces(Vector3 a, Vector3 b, float ha, float hb)
        {
            yield return (a, hb, ha);
            yield return (-a, hb, ha);
            yield return (b, ha, hb);
            yield return (-b, ha, hb);
        }

        private static Vector3 At(Vector3 p, float y) => new Vector3(p.x, y, p.z);
    }
}
