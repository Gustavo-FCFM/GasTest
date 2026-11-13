using System.Collections.Generic;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

// ============================================================
// MercCatacombsSetup
//
// Herramienta de UNA VEZ (13 de noviembre de 2026, pedido de Gustavo: "que abajo sea más amplio y laberíntico, no solo
// pasillos"). Arma en el prefab del sector (Graveyard_Sector, así sale en los tres) la ampliación de las catacumbas
// que se eligió sobre la propuesta en 2D:
//   · EL ANILLO: un pasillo curvo de 5 m a 40–45 m del centro; las tres copias se unen y dan la vuelta entera, así
//     abajo hay rutas para flanquear a los otros equipos. Cruza el pasillo de la fosa.
//   · DOS OSARIOS por sector (salas con columnas) a 23–35 m: el A entre el pasillo de la fosa y el de la torre, el B
//     entre la fosa y la torre vecina. Cada uno con un pasaje desde la cripta, otro hasta el anillo, una puerta
//     lateral (A al pasillo de la fosa, B al de la torre vecina) y una RAMPA que sube a la superficie (la boca está
//     donde arriba solo había un sarcófago: Tomb_0 y Tomb_1, que se mueven al lugar libre más cercano). Barandas
//     alrededor de la boca, como las de la torre. Un campamento en cada osario.
//   · DOS CÁMARAS SIN SALIDA afuera del anillo (para emboscadas; más adelante, objetos rompibles).
//   · Antorchas en lo nuevo (Catacombs ▸ Torches).
// Corta lo que estorba de lo de antes: cuatro muros de la cripta (donde entran los pasajes) y los muros de los pasillos
// de la fosa y de la torre donde llegan el anillo y las puertas. Abre las bocas en el piso de la superficie
// (Graveyard_Meshes: se recortan los triángulos y se cierran los bordes).
// También pone la ciudad fuera del NavMesh (NavMeshModifier: ignorar). El NavMesh de la escena se rehace aparte
// (BakeNavMesh), porque está en la escena.
//
// Coordenadas: las del sector. θ en grados desde +Z (la base) hacia +X; Pt(θ, r, t) = r hacia θ + t de costado.
// Si ya está (Catacombs), no hace nada. Después se borra este archivo; lo armado se edita a mano.
// ============================================================
public static class MercCatacombsSetup
{
    private const string SectorPath = "Assets/Prefabs/Map/Graveyard_Sector.prefab";
    private const string TorchPath = "Assets/Prefabs/Map/Torch.prefab";
    private const string RingMeshPath = "Assets/Art/Models/Graveyard/Catacomb_RingFloor.asset";

    private const float FloorTop = -6.02f;          // 2 cm bajo el piso de antes: donde se pisan, no titilan
    private const float WallY = -3.5f, WallH = 6f, WallT = 0.5f;
    private const float SurfaceTop = 0f, SurfaceBottom = -0.5f;

    // El anillo
    private const float RingIn = 40f, RingOut = 45f;
    // Los osarios: centro radial 29, de 23 a 35
    private const float RoomR = 29f, RoomHalfR = 6f;
    private const float DoorW = 3.5f, SpokeW = 4.5f, RampW = 3f, RampLen = 11f;

    // A, 1 m más ancho que la propuesta: la rampa de los nichos (Ramp_Headstones) pasa por arriba hasta t 3.5.
    // B sube hacia ADENTRO: hacia afuera desembocaba en un muro de nichos (Niches_R1_00/01, de 4 m).
    private struct Room { public string Name; public float Theta, HalfT; public bool RampInward; }
    private static readonly Room RoomA = new Room { Name = "Osario_A", Theta = 16.5f,  HalfT = 7f, RampInward = true };
    private static readonly Room RoomB = new Room { Name = "Osario_B", Theta = -38.5f, HalfT = 5f, RampInward = true };

    private static Material _floorMat, _wallMat, _rampMat, _railMat;
    private static Mesh _wedge;
    private static StringBuilder _log;

    [MenuItem("Mercenarios/Mapa/Una vez: ampliar las catacumbas (anillo y osarios)")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        _log = new StringBuilder("[Catacumbas] ");
        GameObject root = PrefabUtility.LoadPrefabContents(SectorPath);
        try
        {
            if (root.transform.Find("Catacombs") != null) return "[Catacumbas] Ya estaban ampliadas.";

            Transform fosa = root.transform.Find("Catacomb_Fosa");
            Transform tower = root.transform.Find("Catacomb_Tower");
            _floorMat = fosa.Find("Floor").GetComponent<Renderer>().sharedMaterial;
            _wallMat  = fosa.Find("Wall_L").GetComponent<Renderer>().sharedMaterial;
            _rampMat  = tower.Find("Ramp_ToTower").GetComponent<Renderer>().sharedMaterial;
            _railMat  = tower.Find("Rail_L").GetComponent<Renderer>().sharedMaterial;
            _wedge    = tower.Find("Ramp_ToTower").GetComponent<MeshFilter>().sharedMesh;

            Transform group = new GameObject("Catacombs").transform;
            group.SetParent(root.transform, false);

            // --- 1. Cortar lo de antes ---
            DeleteCryptWalls(root.transform.Find("Crypt"), new[] { 11.3f, 21.7f, -43.9f, -32.7f });

            Transform fosaWallL = fosa.Find("Wall_L"), fosaWallR = fosa.Find("Wall_R");
            var fosaR = new List<Transform> { fosaWallR };
            CutByRadius(fosaWallL, RingIn - WallT, RingOut + WallT);
            CutByRadius(fosaR, RingIn - WallT, RingOut + WallT);

            // --- 2. El anillo ---
            float fosaTheta = ThetaOf(fosa.Find("Floor").localPosition);
            var innerGaps = new List<Vector2> { Gap(RoomA.Theta, SpokeW * 0.5f + WallT, RingIn), Gap(RoomB.Theta, SpokeW * 0.5f + WallT, RingIn), Gap(fosaTheta, 3.25f, RingIn) };
            var outerGaps = new List<Vector2> { Gap(fosaTheta, 3.25f, RingOut), Gap(2f, 2.5f, RingOut), Gap(38f, 2.5f, RingOut) };
            BuildRing(group, innerGaps, outerGaps);

            // --- 3. Los osarios, sus pasajes y sus puertas ---
            foreach (Room room in new[] { RoomA, RoomB }) BuildRoom(group, room);

            // Puerta del A al pasillo de la fosa (muro derecho de la fosa)
            BuildConnector(group, "Pasaje_A_Fosa", Pt(RoomA.Theta, RoomR, -(RoomA.HalfT + WallT * 0.5f)), -Tan(RoomA.Theta),
                           fosaR, Quaternion.identity);
            // Puerta del B al pasillo de la torre VECINA: es este mismo muro, girado -120° (la copia de al lado)
            var towerR = new List<Transform> { tower.Find("Wall_R") };
            BuildConnector(group, "Pasaje_B_Torre", Pt(RoomB.Theta, RoomR, -(RoomB.HalfT + WallT * 0.5f)), -Tan(RoomB.Theta),
                           towerR, Quaternion.Euler(0f, -120f, 0f));

            // --- 4. Cámaras sin salida, afuera del anillo ---
            BuildChamber(group, "Camara_Ciega_1", 2f);
            BuildChamber(group, "Camara_Ciega_2", 38f);

            // --- 5. Las bocas en la superficie, las barandas y los sarcófagos ---
            Transform surface = root.transform.Find("Floors/Surface");
            Mesh surfaceMesh = surface.GetComponent<MeshFilter>().sharedMesh;
            var holes = new List<Vector2[]>();
            foreach (Room room in new[] { RoomA, RoomB })
            {
                Vector2[] hole = HoleOf(room);
                holes.Add(hole);
                BuildRails(group, room);
            }
            CutHoles(surfaceMesh, holes);
            RemoveSidesInHoles(surfaceMesh, holes);
            MeshCollider surfaceCollider = surface.GetComponent<MeshCollider>();
            if (surfaceCollider != null) { surfaceCollider.sharedMesh = null; surfaceCollider.sharedMesh = surfaceMesh; }
            RemoveSurfaceTorchesInHoles(root.transform, holes);
            foreach (string half in new[] { "Half_L", "Half_R" })
            {
                Transform h = root.transform.Find(half);
                if (h == null) continue;
                foreach (Transform tomb in h)
                    if (tomb.name.StartsWith("Tomb") && !FootprintFree(tomb.localPosition, TombHalf(tomb), tomb.localRotation, null, holes))
                        MoveTomb(root.transform, tomb, holes);
            }

            // --- 6. Antorchas y campamentos ---
            BuildTorches(group);
            BuildCamps(root.transform);

            // --- 7. La ciudad fuera del NavMesh ---
            Transform city = root.transform.Find("City");
            if (city != null && city.GetComponent<NavMeshModifier>() == null)
            {
                city.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
                _log.Append("La ciudad queda fuera del NavMesh. ");
            }

            PrefabUtility.SaveAsPrefabAsset(root, SectorPath);
            AssetDatabase.SaveAssets();
            _log.Append("Listo en Graveyard_Sector ▸ Catacombs. Falta rehacer el NavMesh (BakeNavMesh) y guardar la escena.");
            return _log.ToString();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Rehace el NavMesh de la escena abierta (el NavMeshSurface de GRAVEYARD). Deja la escena con cambios: Ctrl+S.
    public static string BakeNavMesh()
    {
        var surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
        if (surfaces.Length == 0) return "[Catacumbas] No hay NavMeshSurface en la escena abierta.";
        Unity.AI.Navigation.Editor.NavMeshAssetManager.instance.StartBakingSurfaces(surfaces);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(surfaces[0].gameObject.scene);
        return $"[Catacumbas] Horneando el NavMesh ({surfaces.Length} superficie/s). Después: guardar la escena (Ctrl+S).";
    }

    // =========================================================
    // GEOMETRÍA BÁSICA
    // =========================================================

    private static Vector3 Dir(float theta) { float a = theta * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)); }
    private static Vector3 Tan(float theta) { float a = theta * Mathf.Deg2Rad; return new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a)); }
    private static Vector3 Pt(float theta, float r, float t) => Dir(theta) * r + Tan(theta) * t;
    private static float ThetaOf(Vector3 p) => Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg;
    private static float Yaw(Vector3 forward) => Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
    private static Vector2 Gap(float theta, float halfWidth, float r) { float h = halfWidth / r * Mathf.Rad2Deg; return new Vector2(theta - h, theta + h); }

    // Una caja (cubo escalado con su collider) en el plano del sector.
    private static GameObject Box(Transform parent, string name, Vector3 center, float yaw, Vector3 size, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    private static void Floor(Transform parent, string name, Vector3 a, Vector3 b, float width)
    {
        Vector3 d = b - a; d.y = 0f;
        Vector3 c = (a + b) * 0.5f; c.y = FloorTop - 0.25f;
        Box(parent, name, c, Yaw(d), new Vector3(width, 0.5f, d.magnitude), _floorMat);
    }

    private static void Wall(Transform parent, string name, Vector3 a, Vector3 b)
    {
        Vector3 d = b - a; d.y = 0f;
        if (d.magnitude < 0.05f) return;
        Vector3 c = (a + b) * 0.5f; c.y = WallY;
        Box(parent, name, c, Yaw(d), new Vector3(WallT, WallH, d.magnitude), _wallMat);
    }

    // Un muro recto de a hasta b, con huecos (en metros desde a).
    private static void WallWithGaps(Transform parent, string name, Vector3 a, Vector3 b, params Vector2[] gaps)
    {
        Vector3 d = b - a; d.y = 0f;
        float len = d.magnitude; Vector3 u = d / len;
        var cuts = new List<Vector2>(gaps);
        cuts.Sort((x, y) => x.x.CompareTo(y.x));
        float s = 0f; int i = 0;
        foreach (Vector2 g in cuts)
        {
            if (g.x > s) Wall(parent, $"{name}_{i++}", a + u * s, a + u * Mathf.Min(g.x, len));
            s = Mathf.Max(s, g.y);
        }
        if (s < len) Wall(parent, $"{name}_{i}", a + u * s, b);
    }

    // =========================================================
    // CORTAR LO DE ANTES
    // =========================================================

    private static void DeleteCryptWalls(Transform crypt, float[] thetas)
    {
        var doomed = new List<GameObject>();
        foreach (Transform w in crypt)
        {
            if (!w.name.StartsWith("Wall_")) continue;
            float th = ThetaOf(w.localPosition);
            foreach (float t in thetas) if (Mathf.Abs(Mathf.DeltaAngle(th, t)) < 2.5f) doomed.Add(w.gameObject);
        }
        foreach (GameObject g in doomed) Object.DestroyImmediate(g);
        _log.Append($"{doomed.Count} muros de la cripta quitados. ");
    }

    // Un muro (cubo escalado, largo en su eje Z) sin el tramo [s0, s1] (metros desde su centro, a lo largo).
    private static List<Transform> CutWall(Transform wall, float s0, float s1)
    {
        var result = new List<Transform>();
        float half = wall.localScale.z * 0.5f;
        s0 = Mathf.Max(s0, -half); s1 = Mathf.Min(s1, half);
        if (s1 <= s0) { result.Add(wall); return result; }
        Vector3 f = wall.localRotation * Vector3.forward;
        foreach (Vector2 piece in new[] { new Vector2(-half, s0), new Vector2(s1, half) })
        {
            float len = piece.y - piece.x;
            if (len < 0.05f) continue;
            GameObject copy = Object.Instantiate(wall.gameObject, wall.parent);
            copy.name = wall.name + (piece.x < 0f && s0 > -half ? "_a" : "_b");
            copy.transform.localPosition = wall.localPosition + f * ((piece.x + piece.y) * 0.5f);
            copy.transform.localScale = new Vector3(wall.localScale.x, wall.localScale.y, len);
            result.Add(copy.transform);
        }
        Object.DestroyImmediate(wall.gameObject);
        return result;
    }

    // Corta un muro donde pasa entre dos radios (el anillo cruzando un pasillo).
    private static void CutByRadius(Transform wall, float r0, float r1) => CutByRadius(new List<Transform> { wall }, r0, r1);

    private static void CutByRadius(List<Transform> pieces, float r0, float r1)
    {
        Transform wall = pieces[0];
        string wallName = wall.name;
        Vector3 f = wall.localRotation * Vector3.forward; Vector3 p = wall.localPosition; p.y = 0f;
        float sa = SolveRadius(p, f, r0), sb = SolveRadius(p, f, r1);
        List<Transform> rest = CutWall(wall, Mathf.Min(sa, sb), Mathf.Max(sa, sb));
        pieces.Clear(); pieces.AddRange(rest);
        _log.Append($"{wallName} cortado por el anillo. ");
    }

    // s tal que |p + f·s| = r (la solución hacia afuera).
    private static float SolveRadius(Vector3 p, Vector3 f, float r)
    {
        float b = Vector3.Dot(p, f), c = p.sqrMagnitude - r * r;
        return -b + Mathf.Sqrt(Mathf.Max(0f, b * b - c));
    }

    // =========================================================
    // EL ANILLO
    // =========================================================

    private static void BuildRing(Transform group, List<Vector2> innerGaps, List<Vector2> outerGaps)
    {
        Transform ring = new GameObject("Ring").transform;
        ring.SetParent(group, false);

        // El piso: una malla (una banda curva), de -60° a 60°.
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(RingMeshPath);
        bool isNew = mesh == null;
        if (isNew) mesh = new Mesh { name = "Catacomb_RingFloor" };
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        const int steps = 60;
        for (int i = 0; i <= steps; i++)
        {
            float th = -60f + 120f * i / steps;
            foreach (float y in new[] { FloorTop, FloorTop - 0.5f })
            {
                Vector3 a = Dir(th) * (RingIn - WallT), b = Dir(th) * (RingOut + WallT);
                a.y = b.y = y;
                v.Add(a); v.Add(b);
                uv.Add(new Vector2(a.x, a.z) / 4f); uv.Add(new Vector2(b.x, b.z) / 4f);
            }
        }
        for (int i = 0; i < steps; i++)
        {
            int k = i * 4, n = k + 4;
            tri.AddRange(new[] { k, n, n + 1, k, n + 1, k + 1 });                 // arriba
            tri.AddRange(new[] { k + 2, n + 3, n + 2, k + 2, k + 3, n + 3 });     // abajo
        }
        mesh.Clear();
        mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        // Que la cara de arriba mire para arriba (según cómo quedó el giro).
        if (mesh.normals[0].y < 0f) { tri.Reverse(); mesh.SetTriangles(tri, 0); mesh.RecalculateNormals(); }
        if (isNew) AssetDatabase.CreateAsset(mesh, RingMeshPath); else EditorUtility.SetDirty(mesh);

        GameObject floor = new GameObject("Floor", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
        floor.transform.SetParent(ring, false);
        floor.GetComponent<MeshFilter>().sharedMesh = mesh;
        floor.GetComponent<MeshRenderer>().sharedMaterial = _floorMat;
        floor.GetComponent<MeshCollider>().sharedMesh = mesh;

        RingWall(ring, "Wall_In", RingIn - WallT * 0.5f, innerGaps);
        RingWall(ring, "Wall_Out", RingOut + WallT * 0.5f, outerGaps);
    }

    // Un muro curvo hecho de tramos rectos (cada ~3°), sin los huecos (en grados).
    private static void RingWall(Transform parent, string name, float r, List<Vector2> gaps)
    {
        var solid = new List<Vector2> { new Vector2(-60f, 60f) };
        foreach (Vector2 g in gaps)
        {
            var next = new List<Vector2>();
            foreach (Vector2 s in solid)
            {
                if (g.y <= s.x || g.x >= s.y) { next.Add(s); continue; }
                if (g.x > s.x) next.Add(new Vector2(s.x, g.x));
                if (g.y < s.y) next.Add(new Vector2(g.y, s.y));
            }
            solid = next;
        }
        int k = 0;
        foreach (Vector2 s in solid)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt((s.y - s.x) / 3f));
            for (int i = 0; i < n; i++)
            {
                float a = s.x + (s.y - s.x) * i / n, b = s.x + (s.y - s.x) * (i + 1) / n;
                Vector3 pa = Dir(a) * r, pb = Dir(b) * r;
                Vector3 d = pb - pa;
                Vector3 c = (pa + pb) * 0.5f; c.y = WallY;
                Box(parent, $"{name}_{k++}", c, Yaw(d), new Vector3(WallT, WallH, d.magnitude + 0.06f), _wallMat);
            }
        }
    }

    // =========================================================
    // LOS OSARIOS
    // =========================================================

    private static void BuildRoom(Transform group, Room room)
    {
        Transform g = new GameObject(room.Name).transform;
        g.SetParent(group, false);
        float th = room.Theta, ht = room.HalfT;
        float rIn = RoomR - RoomHalfR, rOut = RoomR + RoomHalfR;
        Vector3 radial = Dir(th);

        // Piso
        Vector3 c = Pt(th, RoomR, 0f); c.y = FloorTop - 0.25f;
        Box(g, "Floor", c, th, new Vector3(ht * 2f + WallT * 2f, 0.5f, RoomHalfR * 2f + WallT * 2f), _floorMat);

        // Muros adentro y afuera (con la puerta del pasaje al centro) y a los costados (el -θ con su puerta)
        float halfSpoke = SpokeW * 0.5f;
        float sideLen = ht * 2f + WallT * 2f;
        Vector2 spokeGap = new Vector2(sideLen * 0.5f - halfSpoke, sideLen * 0.5f + halfSpoke);
        WallWithGaps(g, "Wall_In", Pt(th, rIn - WallT * 0.5f, -ht - WallT), Pt(th, rIn - WallT * 0.5f, ht + WallT), spokeGap);
        WallWithGaps(g, "Wall_Out", Pt(th, rOut + WallT * 0.5f, -ht - WallT), Pt(th, rOut + WallT * 0.5f, ht + WallT), spokeGap);
        float doorFrom = (RoomR - rIn) - DoorW * 0.5f, doorTo = (RoomR - rIn) + DoorW * 0.5f;
        WallWithGaps(g, "Wall_Side_Door", Pt(th, rIn, -ht - WallT * 0.5f), Pt(th, rOut, -ht - WallT * 0.5f), new Vector2(doorFrom, doorTo));
        Wall(g, "Wall_Side", Pt(th, rIn, ht + WallT * 0.5f), Pt(th, rOut, ht + WallT * 0.5f));

        // Columnas en la parte que no es rampa
        float usableFrom = -ht, usableTo = ht - RampW;
        foreach (float t in new[] { Mathf.Lerp(usableFrom, usableTo, 0.25f), Mathf.Lerp(usableFrom, usableTo, 0.75f) })
            foreach (float r in new[] { RoomR - 2.5f, RoomR + 2.5f })
            {
                Vector3 p = Pt(th, r, t); p.y = WallY + 0.2f;
                Box(g, "Pillar", p, th, new Vector3(1.2f, 5.6f, 1.2f), _wallMat);
            }

        // La rampa, pegada al costado +θ. Sube hacia adentro (A) o hacia afuera (B).
        float tRamp = ht - RampW * 0.5f;
        float rLow = room.RampInward ? rOut - 0.25f : rIn + 0.25f;
        Vector3 forward = room.RampInward ? -radial : radial;
        Vector3 low = Pt(th, rLow, tRamp); low.y = -6f;
        GameObject ramp = new GameObject("Ramp", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
        ramp.transform.SetParent(g, false);
        ramp.transform.localPosition = low;
        ramp.transform.localRotation = Quaternion.Euler(0f, Yaw(forward), 0f);
        ramp.transform.localScale = new Vector3(RampW, 6f, RampLen);
        ramp.GetComponent<MeshFilter>().sharedMesh = _wedge;
        ramp.GetComponent<MeshRenderer>().sharedMaterial = _rampMat;
        ramp.GetComponent<MeshCollider>().sharedMesh = _wedge;

        // Los pasajes: de la cripta al osario y del osario al anillo
        Floor(g, "Spoke_In_Floor", Pt(th, 13.6f, 0f), Pt(th, rIn - WallT, 0f), SpokeW + WallT * 2f);
        Wall(g, "Spoke_In_Wall_L", Pt(th, 13.9f, -halfSpoke - WallT * 0.5f), Pt(th, rIn - WallT, -halfSpoke - WallT * 0.5f));
        Wall(g, "Spoke_In_Wall_R", Pt(th, 13.9f, halfSpoke + WallT * 0.5f), Pt(th, rIn - WallT, halfSpoke + WallT * 0.5f));
        Floor(g, "Spoke_Out_Floor", Pt(th, rOut + WallT, 0f), Pt(th, RingIn - WallT, 0f), SpokeW + WallT * 2f);
        Wall(g, "Spoke_Out_Wall_L", Pt(th, rOut + WallT, -halfSpoke - WallT * 0.5f), Pt(th, RingIn - WallT, -halfSpoke - WallT * 0.5f));
        Wall(g, "Spoke_Out_Wall_R", Pt(th, rOut + WallT, halfSpoke + WallT * 0.5f), Pt(th, RingIn - WallT, halfSpoke + WallT * 0.5f));
        _log.Append($"{room.Name} (θ {th}°). ");
    }

    // La boca de la rampa en la superficie: los 7 m de arriba de la rampa (rectángulo, en sentido antihorario).
    private static Vector2[] HoleOf(Room room)
    {
        float th = room.Theta, ht = room.HalfT;
        float rIn = RoomR - RoomHalfR, rOut = RoomR + RoomHalfR;
        float t0 = ht - RampW - 0.25f, t1 = ht;
        float r0, r1;
        if (room.RampInward) { r0 = rIn + 0.75f; r1 = r0 + 7f; }
        else { r1 = rOut - 0.75f; r0 = r1 - 7f; }
        var pts = new[] { Pt(th, r0, t0), Pt(th, r0, t1), Pt(th, r1, t1), Pt(th, r1, t0) };
        var poly = new Vector2[4];
        for (int i = 0; i < 4; i++) poly[i] = new Vector2(pts[i].x, pts[i].z);
        if (SignedArea(poly) < 0f) System.Array.Reverse(poly);
        return poly;
    }

    private static void BuildRails(Transform group, Room room)
    {
        Transform g = group.Find(room.Name);
        float th = room.Theta, ht = room.HalfT;
        float rIn = RoomR - RoomHalfR, rOut = RoomR + RoomHalfR;
        float t0 = ht - RampW - 0.25f - 0.15f, t1 = ht + 0.15f;
        float r0, r1, rEnd;
        if (room.RampInward) { r0 = rIn + 0.75f; r1 = r0 + 7f; rEnd = r1 + 0.15f; }
        else { r1 = rOut - 0.75f; r0 = r1 - 7f; rEnd = r0 - 0.15f; }
        // Los dos largos (a los costados de la boca) y el del fondo (donde la rampa ya está honda). El lado de arriba,
        // abierto: por ahí se baja.
        foreach (float t in new[] { t0, t1 })
        {
            Vector3 a = Pt(th, r0, t), b = Pt(th, r1, t), c = (a + b) * 0.5f; c.y = 0.55f;
            Box(g, "Rail", c, Yaw(b - a), new Vector3(0.3f, 1.1f, (b - a).magnitude + 0.3f), _railMat);
        }
        Vector3 e0 = Pt(th, rEnd, t0), e1 = Pt(th, rEnd, t1), ec = (e0 + e1) * 0.5f; ec.y = 0.55f;
        Box(g, "Rail_End", ec, Yaw(e1 - e0), new Vector3(0.3f, 1.1f, (e1 - e0).magnitude + 0.3f), _railMat);
    }

    // =========================================================
    // PUERTAS LATERALES Y CÁMARAS
    // =========================================================

    // De la puerta de un osario (door, en el muro) en la dirección dir hasta el muro de un pasillo. El muro puede ser el
    // de una copia vecina (view = cómo se ve esa copia desde este sector): el corte se hace en el original.
    private static void BuildConnector(Transform group, string name, Vector3 door, Vector3 dir, List<Transform> wallPieces,
                                       Quaternion view)
    {
        Transform g = new GameObject(name).transform;
        g.SetParent(group, false);
        dir.y = 0f; dir.Normalize();
        door.y = 0f;

        // El muro, visto desde este sector.
        Transform wall = wallPieces[0];
        Vector3 wp = view * wall.localPosition; wp.y = 0f;
        Vector3 wf = view * (wall.localRotation * Vector3.forward); wf.y = 0f; wf.Normalize();
        // door + dir·s = wp + wf·u
        float denom = dir.x * wf.z - dir.z * wf.x;
        if (Mathf.Abs(denom) < 1e-4f) { _log.Append($"¡Ojo! {name}: el pasaje no llega al muro. "); return; }
        Vector3 diff = wp - door;
        float s = (diff.x * wf.z - diff.z * wf.x) / denom;
        Vector3 hit = door + dir * s;

        Floor(g, "Floor", door + dir * 0.25f, door + dir * (s + 0.6f), DoorW + WallT * 2f);
        Vector3 side = new Vector3(dir.z, 0f, -dir.x);
        foreach (float sign in new[] { -1f, 1f })
        {
            Vector3 off = side * sign * (DoorW * 0.5f + WallT * 0.5f);
            Wall(g, sign < 0 ? "Wall_L" : "Wall_R", door + off + dir * 0.25f, door + off + dir * Mathf.Max(0.3f, s));
        }

        // Cortar el muro del pasillo donde llega (más ancho si llega en diagonal).
        float sin = Mathf.Abs(Vector3.Cross(dir, wf).y);
        float halfCut = (DoorW * 0.5f) / Mathf.Max(0.3f, sin);
        Vector3 hitOriginal = Quaternion.Inverse(view) * hit;
        foreach (Transform piece in new List<Transform>(wallPieces))
        {
            Vector3 f = piece.localRotation * Vector3.forward; Vector3 p = piece.localPosition; p.y = 0f;
            float along = Vector3.Dot(hitOriginal - p, f);
            if (Mathf.Abs(along) > piece.localScale.z * 0.5f) continue;
            string pieceName = piece.name;
            List<Transform> rest = CutWall(piece, along - halfCut, along + halfCut);
            wallPieces.Remove(piece);
            wallPieces.AddRange(rest);
            _log.Append($"{name}: {s:0.0} m hasta {pieceName}, cortado. ");
            return;
        }
        _log.Append($"¡Ojo! {name}: no encontré el tramo del muro donde llega. ");
    }

    private static void BuildChamber(Transform group, string name, float th)
    {
        Transform g = new GameObject(name).transform;
        g.SetParent(group, false);
        const float r0 = RingOut + WallT, r1 = RingOut + 6.5f, ht = 3f, door = 2f;
        Vector3 c = Pt(th, (r0 + r1 + WallT) * 0.5f, 0f); c.y = FloorTop - 0.25f;
        Box(g, "Floor", c, th, new Vector3(ht * 2f + WallT * 2f, 0.5f, r1 - r0 + WallT), _floorMat);
        Wall(g, "Wall_End", Pt(th, r1 + WallT * 0.5f, -ht - WallT), Pt(th, r1 + WallT * 0.5f, ht + WallT));
        Wall(g, "Wall_L", Pt(th, r0 - WallT, -ht - WallT * 0.5f), Pt(th, r1, -ht - WallT * 0.5f));
        Wall(g, "Wall_R", Pt(th, r0 - WallT, ht + WallT * 0.5f), Pt(th, r1, ht + WallT * 0.5f));
        // Lo que queda del muro del anillo a los costados de la puerta lo cubre el propio anillo (hueco de ±2.5 m).
        Wall(g, "Wall_Door_L", Pt(th, r0 - WallT * 0.5f, -ht - WallT), Pt(th, r0 - WallT * 0.5f, -door));
        Wall(g, "Wall_Door_R", Pt(th, r0 - WallT * 0.5f, door), Pt(th, r0 - WallT * 0.5f, ht + WallT));
    }

    // =========================================================
    // LAS BOCAS EN EL PISO DE LA SUPERFICIE
    // =========================================================

    private struct V { public Vector3 P; public Vector2 UV; }

    // Saca de la malla de la superficie los rectángulos 'holes' (en x/z, antihorarios) y cierra sus bordes.
    private static void CutHoles(Mesh mesh, List<Vector2[]> holes)
    {
        Vector3[] pos = mesh.vertices; Vector2[] uvs = mesh.uv; int[] tris = mesh.triangles;
        bool hasUV = uvs != null && uvs.Length == pos.Length;
        var outV = new List<Vector3>(); var outUV = new List<Vector2>(); var outT = new List<int>();
        int clipped = 0, vertical = 0;

        for (int i = 0; i < tris.Length; i += 3)
        {
            var tri = new List<V>(3);
            for (int k = 0; k < 3; k++) tri.Add(new V { P = pos[tris[i + k]], UV = hasUV ? uvs[tris[i + k]] : Vector2.zero });
            bool flat = Mathf.Abs(tri[0].P.y - tri[1].P.y) < 1e-3f && Mathf.Abs(tri[0].P.y - tri[2].P.y) < 1e-3f;
            float up = Vector3.Cross(tri[1].P - tri[0].P, tri[2].P - tri[0].P).y;

            var pieces = new List<List<V>> { tri };
            if (flat)
                foreach (Vector2[] hole in holes)
                {
                    var next = new List<List<V>>();
                    foreach (List<V> poly in pieces)
                    {
                        if (!Overlaps(poly, hole)) { next.Add(poly); continue; }
                        clipped++;
                        // poly menos el rectángulo: la parte afuera del lado i, adentro de los lados anteriores.
                        for (int e = 0; e < hole.Length; e++)
                        {
                            List<V> piece = poly;
                            for (int j = 0; j < e && piece.Count >= 3; j++) piece = Clip(piece, hole[j], hole[(j + 1) % hole.Length], true);
                            if (piece.Count >= 3) piece = Clip(piece, hole[e], hole[(e + 1) % hole.Length], false);
                            if (piece.Count >= 3) next.Add(piece);
                        }
                    }
                    pieces = next;
                }
            else if (TouchesAny(tri, holes)) vertical++;

            foreach (List<V> poly in pieces)
                for (int k = 1; k + 1 < poly.Count; k++)
                {
                    V a = poly[0], b = poly[k], c = poly[k + 1];
                    float s = Vector3.Cross(b.P - a.P, c.P - a.P).y;
                    if (flat && Mathf.Abs(s) < 1e-6f) continue;
                    if (flat && Mathf.Sign(s) != Mathf.Sign(up)) { V t = b; b = c; c = t; }
                    int n = outV.Count;
                    outV.Add(a.P); outV.Add(b.P); outV.Add(c.P);
                    outUV.Add(a.UV); outUV.Add(b.UV); outUV.Add(c.UV);
                    outT.Add(n); outT.Add(n + 1); outT.Add(n + 2);
                }
        }

        // Los bordes de cada boca: una cara vertical por lado, mirando hacia adentro de la boca.
        foreach (Vector2[] hole in holes)
            for (int e = 0; e < hole.Length; e++)
            {
                Vector2 a2 = hole[e], b2 = hole[(e + 1) % hole.Length];
                Vector3 a = new Vector3(a2.x, SurfaceBottom, a2.y), b = new Vector3(b2.x, SurfaceBottom, b2.y);
                Vector3 at = new Vector3(a2.x, SurfaceTop, a2.y), bt = new Vector3(b2.x, SurfaceTop, b2.y);
                Vector3 inward = new Vector3(-(b2.y - a2.y), 0f, b2.x - a2.x);   // izquierda del lado = adentro (antihorario)
                int n = outV.Count;
                outV.AddRange(new[] { a, at, bt, b });
                float len = (b2 - a2).magnitude;
                outUV.AddRange(new[] { new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(len, 0.5f), new Vector2(len, 0f) });
                bool facesIn = Vector3.Dot(Vector3.Cross(at - a, bt - a), inward) > 0f;
                if (facesIn) outT.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
                else outT.AddRange(new[] { n, n + 2, n + 1, n, n + 3, n + 2 });
            }

        mesh.Clear();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(outV);
        if (hasUV) mesh.SetUVs(0, outUV);
        mesh.SetTriangles(outT, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        _log.Append($"Superficie: {holes.Count} bocas ({clipped} recortes{(vertical > 0 ? $"; ¡ojo! {vertical} caras de costado tocan una boca" : "")}). ");
    }

    // Las caras de costado del piso (las que separan sus polígonos, normalmente escondidas dentro de la losa) que
    // quedan DENTRO de una boca: se ven y estorban al subir. Se quitan las que tienen el centro adentro.
    private static void RemoveSidesInHoles(Mesh mesh, List<Vector2[]> holes)
    {
        {
            Vector3[] pos = mesh.vertices; Vector2[] uvs = mesh.uv; int[] tris = mesh.triangles;
            var keep = new List<int>(); int removed = 0;
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = pos[tris[i]], b = pos[tris[i + 1]], c = pos[tris[i + 2]];
                bool flat = Mathf.Abs(a.y - b.y) < 1e-3f && Mathf.Abs(a.y - c.y) < 1e-3f;
                // Cualquier punto de sus lados (cada 10 %) adentro de una boca (1 cm hacia adentro del borde): fuera.
                bool inside = false;
                if (!flat)
                    foreach (Vector3[] edge in new[] { new[] { a, b }, new[] { b, c }, new[] { c, a } })
                        for (int k = 0; k <= 10 && !inside; k++)
                        {
                            Vector3 m = Vector3.Lerp(edge[0], edge[1], k / 10f);
                            foreach (Vector2[] h in holes)
                            {
                                bool inH = true;
                                for (int e = 0; e < h.Length; e++)
                                    if (Side(h[e], h[(e + 1) % h.Length], m) / (h[(e + 1) % h.Length] - h[e]).magnitude < 0.01f) { inH = false; break; }
                                if (inH) { inside = true; break; }
                            }
                        }
                if (inside) { removed++; continue; }
                keep.Add(tris[i]); keep.Add(tris[i + 1]); keep.Add(tris[i + 2]);
            }
            mesh.SetTriangles(keep, 0);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            _log.Append($"{removed} caras de costado quitadas de las bocas. ");
        }
    }

    private static List<V> Clip(List<V> poly, Vector2 a, Vector2 b, bool keepLeft)
    {
        var result = new List<V>();
        for (int i = 0; i < poly.Count; i++)
        {
            V cur = poly[i], nxt = poly[(i + 1) % poly.Count];
            float dc = Side(a, b, cur.P), dn = Side(a, b, nxt.P);
            bool inC = keepLeft ? dc >= 0f : dc <= 0f, inN = keepLeft ? dn >= 0f : dn <= 0f;
            if (inC) result.Add(cur);
            if (inC != inN)
            {
                float t = dc / (dc - dn);
                result.Add(new V { P = Vector3.Lerp(cur.P, nxt.P, t), UV = Vector2.Lerp(cur.UV, nxt.UV, t) });
            }
        }
        return result;
    }

    private static float Side(Vector2 a, Vector2 b, Vector3 p) => (b.x - a.x) * (p.z - a.y) - (b.y - a.y) * (p.x - a.x);

    private static bool Overlaps(List<V> poly, Vector2[] hole)
    {
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (V v in poly) { minX = Mathf.Min(minX, v.P.x); maxX = Mathf.Max(maxX, v.P.x); minZ = Mathf.Min(minZ, v.P.z); maxZ = Mathf.Max(maxZ, v.P.z); }
        float hx0 = float.MaxValue, hx1 = float.MinValue, hz0 = float.MaxValue, hz1 = float.MinValue;
        foreach (Vector2 h in hole) { hx0 = Mathf.Min(hx0, h.x); hx1 = Mathf.Max(hx1, h.x); hz0 = Mathf.Min(hz0, h.y); hz1 = Mathf.Max(hz1, h.y); }
        if (maxX < hx0 || minX > hx1 || maxZ < hz0 || minZ > hz1) return false;
        // ¿Todo el polígono queda afuera de algún lado del rectángulo? Entonces no se tocan.
        for (int e = 0; e < hole.Length; e++)
        {
            bool allOut = true;
            foreach (V v in poly) if (Side(hole[e], hole[(e + 1) % hole.Length], v.P) > 1e-5f) { allOut = false; break; }
            if (allOut) return false;
        }
        return true;
    }

    private static bool TouchesAny(List<V> tri, List<Vector2[]> holes)
    {
        foreach (Vector2[] h in holes) if (Overlaps(tri, h)) return true;
        return false;
    }

    private static float SignedArea(Vector2[] p)
    {
        float a = 0f;
        for (int i = 0; i < p.Length; i++) { Vector2 u = p[i], w = p[(i + 1) % p.Length]; a += u.x * w.y - w.x * u.y; }
        return a * 0.5f;
    }

    // =========================================================
    // SARCÓFAGOS, ANTORCHAS Y CAMPAMENTOS
    // =========================================================

    // El sarcófago que estaba sobre la boca se va al lugar libre más cercano (sin pisar otra cosa ni las bocas).
    private static Vector3 TombHalf(Transform tomb) => new Vector3(tomb.localScale.x * 0.5f + 0.4f, 0.5f, tomb.localScale.z * 0.5f + 0.4f);

    private static void MoveTomb(Transform root, Transform tomb, List<Vector2[]> holes)
    {
        Vector3 start = tomb.localPosition;
        Vector3 half = TombHalf(tomb);

        // Lo que hay arriba en este prefab: la caja de la MALLA de cada objeto (los muros de nichos son mallas huecas
        // y su collider no los representa bien), para no encimarse.
        var blockers = new List<(Vector3 c, Vector3 h, Quaternion r)>();
        foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null || mf.transform == tomb || mf.transform.IsChildOf(tomb)) continue;
            string top = Top(root, mf.transform);
            if (top == "Floors" || top == "City") continue;
            Bounds b = mf.sharedMesh.bounds;
            Vector3 c = root.InverseTransformPoint(mf.transform.TransformPoint(b.center));
            Vector3 sz = Vector3.Scale(b.extents, mf.transform.lossyScale);
            sz = new Vector3(Mathf.Abs(sz.x), Mathf.Abs(sz.y), Mathf.Abs(sz.z));
            if (c.y + sz.y < 0.05f || c.y - sz.y > 3f) continue;   // bajo tierra o muy alto
            blockers.Add((c, sz, mf.transform.rotation));
        }

        for (float radius = 1.5f; radius <= 9f; radius += 0.75f)
            for (int a = 0; a < 16; a++)
            {
                float ang = a * 22.5f * Mathf.Deg2Rad;
                Vector3 p = start + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radius;
                if (FootprintFree(p, half, tomb.localRotation, blockers, holes))
                {
                    tomb.localPosition = p;
                    _log.Append($"{tomb.name} movido {radius:0.0} m. ");
                    return;
                }
            }
        _log.Append($"¡Ojo! No encontré lugar libre para {tomb.name}: muévelo a mano. ");
    }

    private static string Top(Transform root, Transform t)
    {
        while (t.parent != null && t.parent != root) t = t.parent;
        return t.name;
    }

    // Una antorcha de la superficie que quedó adentro de una boca (con medio metro de margen) se quita.
    private static void RemoveSurfaceTorchesInHoles(Transform root, List<Vector2[]> holes)
    {
        Transform torches = root.Find("Torches");
        if (torches == null) return;
        var doomed = new List<GameObject>();
        foreach (Transform t in torches)
            foreach (Vector2[] h in holes)
            {
                bool inside = true;
                for (int e = 0; e < h.Length; e++)
                    if (Side(h[e], h[(e + 1) % h.Length], t.localPosition) / (h[(e + 1) % h.Length] - h[e]).magnitude < -0.5f) { inside = false; break; }
                if (inside) { doomed.Add(t.gameObject); break; }
            }
        foreach (GameObject g in doomed) Object.DestroyImmediate(g);
        if (doomed.Count > 0) _log.Append($"{doomed.Count} antorcha(s) de la superficie quitada(s) de las bocas. ");
    }

    private static bool FootprintFree(Vector3 p, Vector3 half, Quaternion rot, List<(Vector3 c, Vector3 h, Quaternion r)> blockers, List<Vector2[]> holes)
    {
        // Las 4 esquinas y el centro no pueden caer en una boca (con margen).
        var probes = new List<Vector3> { p };
        foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f }) probes.Add(p + rot * new Vector3(sx * half.x, 0f, sz * half.z));
        foreach (Vector3 q in probes)
            foreach (Vector2[] h in holes)
            {
                bool inside = true;
                for (int e = 0; e < h.Length; e++) if (Side(h[e], h[(e + 1) % h.Length], q) / (h[(e + 1) % h.Length] - h[e]).magnitude < -1.2f) { inside = false; break; }
                if (inside) return false;
            }
        // Que no se superponga con ninguna caja de las de arriba (prueba por separación de ejes en el plano).
        if (blockers == null) return true;
        foreach (var b in blockers)
            if (OverlapOBB2D(p, half, rot, b.c, b.h, b.r)) return false;
        return true;
    }

    private static bool OverlapOBB2D(Vector3 ca, Vector3 ha, Quaternion ra, Vector3 cb, Vector3 hb, Quaternion rb)
    {
        Vector3[] axes = { ra * Vector3.right, ra * Vector3.forward, rb * Vector3.right, rb * Vector3.forward };
        Vector3 d = cb - ca; d.y = 0f;
        foreach (Vector3 ax0 in axes)
        {
            Vector3 ax = new Vector3(ax0.x, 0f, ax0.z).normalized;
            float projA = Mathf.Abs(Vector3.Dot(ra * Vector3.right, ax)) * ha.x + Mathf.Abs(Vector3.Dot(ra * Vector3.forward, ax)) * ha.z;
            float projB = Mathf.Abs(Vector3.Dot(rb * Vector3.right, ax)) * hb.x + Mathf.Abs(Vector3.Dot(rb * Vector3.forward, ax)) * hb.z;
            if (Mathf.Abs(Vector3.Dot(d, ax)) > projA + projB) return false;
        }
        return true;
    }

    private static void BuildTorches(Transform group)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TorchPath);
        if (prefab == null) { _log.Append("¡Ojo! Sin prefab Torch. "); return; }
        Transform g = new GameObject("Torches").transform;
        g.SetParent(group, false);
        var spots = new List<Vector3>();
        foreach (float th in new[] { -50f, -28f, -5f, 28f, 50f }) spots.Add(Pt(th, RingOut - 0.8f, 0f));    // el anillo, del lado de afuera
        foreach (Room room in new[] { RoomA, RoomB })
        {
            spots.Add(Pt(room.Theta, RoomR - RoomHalfR + 0.8f, -room.HalfT + 0.8f));
            spots.Add(Pt(room.Theta, RoomR + RoomHalfR - 0.8f, -room.HalfT + 0.8f));
        }
        spots.Add(Pt(2f, RingOut + 5.7f, 0f));
        spots.Add(Pt(38f, RingOut + 5.7f, 0f));
        int i = 0;
        foreach (Vector3 s in spots)
        {
            var t = (GameObject)PrefabUtility.InstantiatePrefab(prefab, g);
            t.name = "Torch_" + i++;
            t.transform.localPosition = new Vector3(s.x, FloorTop, s.z);
        }
        _log.Append($"{spots.Count} antorchas. ");
    }

    private static void BuildCamps(Transform root)
    {
        Transform camps = root.Find("Camps");
        Transform source = camps != null ? camps.Find("Camp_Catacomb_Fosa") : null;
        if (source == null) { _log.Append("¡Ojo! Sin Camp_Catacomb_Fosa para copiar: los campamentos de los osarios van a mano. "); return; }
        foreach (Room room in new[] { RoomA, RoomB })
        {
            GameObject camp = Object.Instantiate(source.gameObject, camps);
            camp.name = "Camp_" + room.Name;
            float t = (-room.HalfT + (room.HalfT - RampW)) * 0.5f;   // al medio de la parte sin rampa
            camp.transform.localPosition = new Vector3(Pt(room.Theta, RoomR, t).x, -6f, Pt(room.Theta, RoomR, t).z);
            MercEnemySpawner spawner = camp.GetComponent<MercEnemySpawner>();
            if (spawner != null) spawner.SpawnRadius = Mathf.Min(spawner.SpawnRadius, 2.5f);
        }
        _log.Append("Campamentos en los dos osarios (copias del de la fosa). ");
    }
}
