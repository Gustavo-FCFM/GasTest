using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.AI.Navigation;

// ============================================================
// MercArenaBuilder
//
// Construye la ARENA del modo Mercenarios: la geometría, las tres bases, las
// pasarelas y los campamentos. Lo llama MercSetupTools (los menús); acá vive todo
// lo que tiene que ver con la FORMA del escenario, separado de la plomería de la
// escena para poder iterar el diseño sin tocar nada más.
//
// ─────────────────────────────────────────────────────────────
// LA IDEA DEL MAPA
//
// Un coliseo redondo con SIMETRÍA DE 3: todo lo que existe para un equipo existe
// igual para los otros dos, girado 120°. Eso es lo que hace que un 3c3c3 sea justo —
// nadie tiene mejor camino que nadie, y es la misma regla que usan los mapas de tres
// jugadores de StarCraft.
//
// TRES ALTURAS, y esa es la gracia:
//
//   Nivel 0 · la arena de arena.    Donde caés si te tirás de cualquier lado.
//   Nivel 1 · los tablados (2,5 m). Tres plataformas de madera, una entre cada par
//             de bases. Terreno neutral y disputado.
//   Nivel 2 · la meseta (5 m).      El centro, donde aparece el Objetivo. Quien la
//             tiene, ve todo.
//
// DOS FORMAS DE LLEGAR AL OBJETIVO, y ahí está el juego:
//
//   · EL CARRIL (directo, abajo): salís de tu base, cruzás tu camino de piedra, subís
//     la rampa ancha y estás arriba. Rápido y a la vista de todos.
//   · LA VUELTA ALTA (flanco): rampa lateral a un tablado, y del tablado un puente a
//     la meseta. Más largo, pero llegás por arriba y por el costado.
//
// Cada tablado toca a DOS bases (una rampa para cada una), así que también es la
// forma natural de ir a molestar al vecino sin pasar por el medio.
//
// Bajar el Objetivo desde la meseta es a propósito lo más tenso de la partida: estás
// lento, no tenés definitiva, y tenés que elegir rampa con dos equipos mirándote.
// ============================================================
public static class MercArenaBuilder
{
    public const string ArenaRootName = "ARENA_MERCENARIES";

    // --- medidas generales (metros) ---
    private const float ArenaRadius   = 42f;   // hasta el muro
    private const int   WallSegments  = 36;    // el muro es un anillo de bloques
    private const float WallHeight    = 8f;

    // --- meseta central (nivel 2) ---
    private const float PlateauRadius = 9f;
    private const float PlateauHeight = 5f;

    // --- tablados laterales (nivel 1) ---
    private const float DeckDistance  = 20f;   // del centro
    private const float DeckRadius    = 6f;
    private const float DeckHeight    = 2.5f;

    // --- bases ---
    private const float BaseDistance  = 33f;   // del centro al centro de la sala
    private const float RoomHalfSize  = 6f;    // sala de 12 x 12
    private const float RoomWallHeight = 4f;
    private const float RoomWallThick  = 0.5f;
    private const float DoorHalfWidth  = 2.5f;

    // --- carriles ---
    private const float LaneWidth     = 9f;
    private const float LaneInnerZ    = 18f;   // donde arranca la rampa a la meseta
    private const float LaneOuterZ    = 27f;   // la puerta de la base
    private const float DeliveryZ     = 23f;   // plataforma de entrega, afuera de la puerta

    // Materiales de la tanda actual (se arman una vez por construcción).
    private class Palette
    {
        public Material Sand, Wood, Stone, Road, Gold;
        public Material[] Team = new Material[3];
    }

    // ============================================================
    // ENTRADA
    // ============================================================

    // Construye la arena entera y devuelve las tres bases y el punto donde aparece
    // el Objetivo, para que quien llame los cablee en el modo de juego.
    public static GameObject Build(out MercTeamBase[] bases, out Transform objectivePoint)
    {
        Palette p = BuildPalette();

        GameObject root = new GameObject(ArenaRootName);

        BuildFloorAndWall(root.transform, p);
        objectivePoint = BuildPlateau(root.transform, p);

        Transform camps = new GameObject("EnemyCamps").transform;
        camps.SetParent(root.transform, false);

        GameObject enemyPrefab = MercSetupTools.FindEnemyPrefab();
        if (enemyPrefab == null)
            Debug.LogWarning("[Mercenarios] Todavía no existe el prefab del enemigo — corré " +
                             "'1 · Crear prefabs de la demo' y asignalo en los campamentos.");

        bases = new MercTeamBase[MercenariesGameMode.TeamCount];
        for (int i = 0; i < MercenariesGameMode.TeamCount; i++)
            bases[i] = BuildSector(root.transform, i + 1, p, camps, enemyPrefab);

        BakeNavMesh(root);
        return root;
    }

    // ============================================================
    // PISO, MURO Y COLUMNAS
    // ============================================================

    private static void BuildFloorAndWall(Transform parent, Palette p)
    {
        Transform group = new GameObject("Ground").transform;
        group.SetParent(parent, false);

        // El piso es un cilindro y no un cubo: la arena es REDONDA, y desde adentro
        // eso se nota muchísimo — es lo que la hace leer como coliseo y no como caja.
        Cylinder(group, "ArenaFloor", new Vector3(0f, -0.25f, 0f),
                 ArenaRadius * 2f, 0.5f, p.Sand);

        // Muro perimetral, hecho de bloques en anillo. Cada bloque mira al centro.
        Transform wall = new GameObject("Wall").transform;
        wall.SetParent(group, false);

        float segmentWidth = (2f * Mathf.PI * ArenaRadius / WallSegments) * 1.12f; // 12% de solape
        for (int i = 0; i < WallSegments; i++)
        {
            float a = i * (360f / WallSegments);
            Quaternion rot = Quaternion.Euler(0f, a, 0f);
            Vector3 pos = rot * Vector3.forward * ArenaRadius;

            GameObject block = Box(wall, $"Wall_{i:00}", pos + Vector3.up * (WallHeight * 0.5f),
                                   new Vector3(segmentWidth, WallHeight, 1.4f), p.Stone);
            block.transform.localRotation = rot;
        }

        // Columnas contra el muro: puro ritmo visual, pero es lo que le da escala al
        // lugar cuando estás parado adentro.
        Transform pillars = new GameObject("Pillars").transform;
        pillars.SetParent(group, false);
        for (int i = 0; i < 12; i++)
        {
            float a = 15f + i * 30f;
            Vector3 pos = Quaternion.Euler(0f, a, 0f) * Vector3.forward * (ArenaRadius - 2.5f);
            Cylinder(pillars, $"Pillar_{i:00}", pos + Vector3.up * 5f, 2.2f, 10f, p.Stone);
        }
    }

    // ============================================================
    // MESETA CENTRAL (nivel 2)
    // ============================================================

    private static Transform BuildPlateau(Transform parent, Palette p)
    {
        Transform group = new GameObject("CentralPlateau").transform;
        group.SetParent(parent, false);

        Cylinder(group, "Plateau", new Vector3(0f, PlateauHeight * 0.5f, 0f),
                 PlateauRadius * 2f, PlateauHeight, p.Stone);

        // Un borde dorado apenas saliente: marca el nivel de arriba desde lejos y le
        // da al Objetivo un "escenario" en vez de una torta de piedra pelada.
        Cylinder(group, "PlateauRim", new Vector3(0f, PlateauHeight - 0.15f, 0f),
                 PlateauRadius * 2f + 1.2f, 0.3f, p.Gold);

        Transform point = new GameObject("ObjectiveSpawnPoint").transform;
        point.SetParent(group, false);
        point.localPosition = new Vector3(0f, PlateauHeight + 0.4f, 0f);
        return point;
    }

    // ============================================================
    // UN SECTOR = UN EQUIPO
    //
    // Todo se escribe UNA vez en el marco del sector (origen en el centro de la
    // arena, +Z local apuntando a la base de este equipo) y se instancia tres veces
    // girado 120°. Esa es toda la simetría: no hay tres copias del código, hay un
    // sector y tres rotaciones.
    // ============================================================

    private static MercTeamBase BuildSector(Transform parent, int team, Palette p,
                                            Transform campParent, GameObject enemyPrefab)
    {
        float angle = 90f + (team - 1) * 120f;

        Transform sector = new GameObject($"Sector_Team{team}").transform;
        sector.SetParent(parent, false);
        sector.localRotation = Quaternion.Euler(0f, angle, 0f);

        Material teamMat = p.Team[team - 1];

        MercTeamBase teamBase = BuildBase(sector, team, p, teamMat);
        BuildLane(sector, p, teamMat);
        BuildDeck(sector, p, campParent, enemyPrefab, team);

        // --- campamentos del sector ---
        // Los dos que importan están en el CARRIL y arriba en la meseta: el diseño
        // pide que el escenario esté plagado sobre todo donde aparece el Objetivo.
        CreateCamp(campParent, $"Camp_Lane_{team}", sector.TransformPoint(new Vector3(0f, 0f, 21f)),
                   2, 3.5f, enemyPrefab);
        CreateCamp(campParent, $"Camp_Plateau_{team}", sector.TransformPoint(new Vector3(0f, PlateauHeight, 5.5f)),
                   2, 2.5f, enemyPrefab);

        return teamBase;
    }

    // La sala segura: 12 x 12, con UNA sola puerta, mirando al centro.
    private static MercTeamBase BuildBase(Transform sector, int team, Palette p, Material teamMat)
    {
        Transform baseRoot = new GameObject($"Base_Team{team}").transform;
        baseRoot.SetParent(sector, false);
        baseRoot.localPosition = new Vector3(0f, 0f, BaseDistance);
        // Girada 180°: dentro de la sala, +Z local apunta al centro de la arena, así
        // "la puerta va adelante" se escribe una sola vez.
        baseRoot.localRotation = Quaternion.Euler(0f, 180f, 0f);

        float span = RoomHalfSize * 2f;

        Box(baseRoot, "BaseFloor", new Vector3(0f, 0.06f, 0f),
            new Vector3(span, 0.12f, span), teamMat);

        Box(baseRoot, "Wall_Back", new Vector3(0f, RoomWallHeight * 0.5f, -RoomHalfSize),
            new Vector3(span, RoomWallHeight, RoomWallThick), p.Stone);
        Box(baseRoot, "Wall_Left", new Vector3(-RoomHalfSize, RoomWallHeight * 0.5f, 0f),
            new Vector3(RoomWallThick, RoomWallHeight, span), p.Stone);
        Box(baseRoot, "Wall_Right", new Vector3(RoomHalfSize, RoomWallHeight * 0.5f, 0f),
            new Vector3(RoomWallThick, RoomWallHeight, span), p.Stone);

        float segment = RoomHalfSize - DoorHalfWidth;
        Box(baseRoot, "Wall_Front_A", new Vector3(-(DoorHalfWidth + segment * 0.5f), RoomWallHeight * 0.5f, RoomHalfSize),
            new Vector3(segment, RoomWallHeight, RoomWallThick), p.Stone);
        Box(baseRoot, "Wall_Front_B", new Vector3(DoorHalfWidth + segment * 0.5f, RoomWallHeight * 0.5f, RoomHalfSize),
            new Vector3(segment, RoomWallHeight, RoomWallThick), p.Stone);

        // Entrega: AFUERA de la puerta. Si estuviera adentro de la sala segura, pisar
        // la base sería punto asegurado (ahí sos invulnerable) y el último tramo —el
        // más emocionante— no se podría disputar.
        float deliveryLocalZ = BaseDistance - DeliveryZ;
        Cylinder(baseRoot, "DeliveryPad", new Vector3(0f, 0.08f, deliveryLocalZ),
                 7.5f, 0.16f, teamMat);

        Transform delivery = new GameObject("DeliveryPoint").transform;
        delivery.SetParent(baseRoot, false);
        delivery.localPosition = new Vector3(0f, 0.3f, deliveryLocalZ);

        Transform safeCenter = new GameObject("SafeRoomCenter").transform;
        safeCenter.SetParent(baseRoot, false);
        safeCenter.localPosition = new Vector3(0f, 2f, 0f);

        var spawns = new Transform[3];
        for (int s = 0; s < 3; s++)
        {
            Transform sp = new GameObject($"SpawnPoint_{s + 1}").transform;
            sp.SetParent(baseRoot, false);
            sp.localPosition = new Vector3((s - 1) * 3.2f, 0.15f, -3.5f);
            spawns[s] = sp;
        }

        MercTeamBase teamBase = baseRoot.gameObject.AddComponent<MercTeamBase>();
        teamBase.TeamID         = team;
        teamBase.SafeRoomCenter = safeCenter;
        teamBase.SafeRoomSize   = new Vector3(span, 6f, span);
        teamBase.DeliveryPoint  = delivery;
        teamBase.DeliveryRadius = 3.5f;
        teamBase.SpawnPoints    = spawns;
        teamBase.CharacterLayer = 1 << 7;
        return teamBase;
    }

    // El CARRIL: el camino de piedra que sale de tu puerta y la rampa ancha que sube
    // a la meseta. Es la ruta corta, y está a la vista de todo el mundo.
    private static void BuildLane(Transform sector, Palette p, Material teamMat)
    {
        Transform lane = new GameObject("Lane").transform;
        lane.SetParent(sector, false);

        float laneLength = LaneOuterZ - LaneInnerZ;
        float laneCenter = (LaneOuterZ + LaneInnerZ) * 0.5f;

        // Camino de piedra a ras del piso: sin esto la arena es un desierto sin
        // lectura y nadie sabe por dónde "se va" a ningún lado.
        Box(lane, "LaneRoad", new Vector3(0f, 0.03f, laneCenter),
            new Vector3(LaneWidth, 0.06f, laneLength), p.Road);

        // Una banda del color del equipo en la boca del carril: desde el centro se ve
        // de quién es cada camino.
        Box(lane, "TeamStripe", new Vector3(0f, 0.05f, LaneOuterZ - 0.8f),
            new Vector3(LaneWidth, 0.08f, 1.6f), teamMat);

        // Rampa ancha del carril a la meseta.
        Ramp(lane, "LaneRamp",
             new Vector3(0f, 0f, LaneInnerZ), new Vector3(0f, PlateauHeight, PlateauRadius),
             7f, p.Stone, rails: false);

        // Coberturas: pegadas al carril, en pares espejados. Un tiroteo sin dónde
        // cubrirse es un tiroteo de quien dispara primero.
        Box(lane, "Cover_Left", new Vector3(-5.2f, 0.7f, 20.5f), new Vector3(3f, 1.4f, 1f), p.Stone);
        Box(lane, "Cover_Right", new Vector3( 5.2f, 0.7f, 20.5f), new Vector3(3f, 1.4f, 1f), p.Stone);
        Box(lane, "Cover_Ramp_Left", new Vector3(-6f, 0.7f, 15f), new Vector3(1f, 1.4f, 3.5f), p.Stone);
        Box(lane, "Cover_Ramp_Right", new Vector3( 6f, 0.7f, 15f), new Vector3(1f, 1.4f, 3.5f), p.Stone);

        // Bloques arriba, al borde de la meseta: sirven para pelear el alto y para
        // que el que sube por la rampa tenga dónde meterse.
        Box(lane, "Parapet_Left", new Vector3(-4.5f, PlateauHeight + 0.8f, 4.5f), new Vector3(2.4f, 1.6f, 0.9f), p.Stone);
        Box(lane, "Parapet_Right", new Vector3( 4.5f, PlateauHeight + 0.8f, 4.5f), new Vector3(2.4f, 1.6f, 0.9f), p.Stone);
    }

    // El TABLADO: plataforma de madera a media altura, entre dos bases. Tiene una
    // rampa hacia cada una de esas dos bases y un puente a la meseta.
    private static void BuildDeck(Transform sector, Palette p, Transform campParent,
                                  GameObject enemyPrefab, int team)
    {
        Transform deck = new GameObject("Deck").transform;
        deck.SetParent(sector, false);
        // A 60° de la base de este sector: o sea, justo en el medio entre dos bases.
        deck.localRotation = Quaternion.Euler(0f, 60f, 0f);
        deck.localPosition = Quaternion.Euler(0f, 60f, 0f) * Vector3.forward * DeckDistance;

        Cylinder(deck, "DeckPlatform", new Vector3(0f, DeckHeight * 0.5f, 0f),
                 DeckRadius * 2f, DeckHeight, p.Wood);

        // Puente al centro: sube del tablado (2,5 m) al borde de la meseta (5 m).
        // Hacia el centro es -Z local del tablado.
        Ramp(deck, "BridgeToPlateau",
             new Vector3(0f, DeckHeight, -DeckRadius + 0.2f),
             new Vector3(0f, PlateauHeight, -(DeckDistance - PlateauRadius)),
             4.5f, p.Wood, rails: true);

        // Las dos rampas laterales, una hacia cada base vecina.
        Ramp(deck, "SideRamp_A",
             new Vector3(-DeckRadius + 0.2f, DeckHeight, 0f), new Vector3(-DeckRadius - 7f, 0f, 0f),
             4f, p.Wood, rails: true);
        Ramp(deck, "SideRamp_B",
             new Vector3(DeckRadius - 0.2f, DeckHeight, 0f), new Vector3(DeckRadius + 7f, 0f, 0f),
             4f, p.Wood, rails: true);

        // Baranda del lado de afuera, para que el tablado se lea como plataforma y no
        // como una moneda apoyada en el aire (y para no caerse de espaldas peleando).
        for (int i = 0; i < 5; i++)
        {
            float a = -40f + i * 20f;
            Vector3 pos = Quaternion.Euler(0f, a, 0f) * Vector3.forward * (DeckRadius - 0.3f);
            GameObject rail = Box(deck, $"Railing_{i}", pos + Vector3.up * (DeckHeight + 0.5f),
                                  new Vector3(3.2f, 1f, 0.25f), p.Wood);
            rail.transform.localRotation = Quaternion.Euler(0f, a, 0f);
        }

        CreateCamp(campParent, $"Camp_Deck_{team}", deck.TransformPoint(new Vector3(0f, DeckHeight, 0f)),
                   2, 3f, enemyPrefab);
    }

    // ============================================================
    // CAMPAMENTOS
    // ============================================================

    private static void CreateCamp(Transform parent, string name, Vector3 worldPos,
                                   int count, float radius, GameObject prefab)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = worldPos;

        MercEnemySpawner spawner = go.AddComponent<MercEnemySpawner>();
        spawner.EnemyPrefab    = prefab;
        spawner.Count          = count;
        spawner.SpawnRadius    = radius;
        spawner.RespawnSeconds = 20f;

        // La curva de dificultad de la partida, puesta de fábrica: fantasmas desde el
        // arranque, magos desde el minuto 2, un jefe (uno solo) desde el 5. A medida que
        // los equipos suben de nivel, el mapa se pone más feo solo.
        spawner.SpawnTable = new List<MercEnemySpawner.SpawnRule>
        {
            new MercEnemySpawner.SpawnRule { Type = EMercEnemyType.Ghost, Weight = 70f, UnlockMinute = 0f },
            new MercEnemySpawner.SpawnRule { Type = EMercEnemyType.Mage,  Weight = 30f, UnlockMinute = 2f },
            new MercEnemySpawner.SpawnRule { Type = EMercEnemyType.Boss,  Weight = 10f, UnlockMinute = 5f, MaxAlive = 1 },
        };
    }

    // ============================================================
    // NAVMESH
    // ============================================================

    private static void BakeNavMesh(GameObject root)
    {
        NavMeshSurface surface = root.GetComponent<NavMeshSurface>();
        if (surface == null) surface = root.AddComponent<NavMeshSurface>();

        surface.collectObjects = CollectObjects.Children;
        surface.BuildNavMesh();

        Debug.Log("[Mercenarios] NavMesh horneado. Las rampas quedan por debajo de los 30° " +
                  "para que los NPCs suban a los tablados y a la meseta.");
    }

    // ============================================================
    // PRIMITIVAS
    // ============================================================

    private static GameObject Box(Transform parent, string name, Vector3 localPos,
                                  Vector3 size, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale    = size;
        Paint(go, mat);
        return go;
    }

    // OJO con la escala del cilindro de Unity: la malla mide 2 de alto y 1 de
    // diámetro, así que la Y va a la MITAD de la altura que se quiere.
    private static GameObject Cylinder(Transform parent, string name, Vector3 localPos,
                                       float diameter, float height, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale    = new Vector3(diameter, height * 0.5f, diameter);
        Paint(go, mat);
        return go;
    }

    // Una rampa/puente entre dos puntos. Se le pasan los puntos que tiene que unir y
    // el resto lo calcula: dirección, largo e inclinación. La losa se baja media
    // altura para que la CARA DE ARRIBA pase exactamente por 'from' y 'to' — si no,
    // cada rampa quedaría medio escalón por encima de lo que une.
    private static void Ramp(Transform parent, string name, Vector3 from, Vector3 to,
                             float width, Material mat, bool rails)
    {
        Vector3 dir = to - from;
        if (dir.sqrMagnitude < 0.001f) return;

        const float thickness = 0.4f;
        Quaternion rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        Vector3 mid    = (from + to) * 0.5f;
        Vector3 pos    = mid - (rot * Vector3.up) * (thickness * 0.5f);

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale    = new Vector3(width, thickness, dir.magnitude);
        Paint(go, mat);

        if (!rails) return;

        // Barandas: dos losas finas siguiendo la misma pendiente, corridas a los
        // costados. Se generan con la misma cuenta en vez de colgarlas del hijo,
        // porque la rampa tiene escala no uniforme y deformaría cualquier hijo.
        Vector3 side = Vector3.Cross(Vector3.up, dir.normalized).normalized * (width * 0.5f - 0.15f);
        Vector3 up   = Vector3.up * 0.55f;

        RampSlab(parent, name + "_RailA", from + side + up, to + side + up, 0.25f, 0.9f, mat);
        RampSlab(parent, name + "_RailB", from - side + up, to - side + up, 0.25f, 0.9f, mat);
    }

    private static void RampSlab(Transform parent, string name, Vector3 from, Vector3 to,
                                 float width, float thickness, Material mat)
    {
        Vector3 dir = to - from;
        Quaternion rot = Quaternion.LookRotation(dir.normalized, Vector3.up);

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = (from + to) * 0.5f;
        go.transform.localRotation = rot;
        go.transform.localScale    = new Vector3(width, thickness, dir.magnitude);
        Paint(go, mat);
    }

    private static void Paint(GameObject go, Material mat)
    {
        if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        go.isStatic = true;
    }

    // ============================================================
    // PALETA
    //
    // Sacada de los mapas de referencia: arena clara, madera para todo lo que está
    // en alto, piedra para lo que no se mueve. Que el nivel 1 sea SIEMPRE madera y
    // el nivel 2 SIEMPRE piedra no es decoración: es cómo se lee la altura de un
    // vistazo cuando estás corriendo.
    // ============================================================

    private static Palette BuildPalette()
    {
        var p = new Palette
        {
            Sand  = MercSetupTools.GetOrCreateMaterial("Mat_ArenaSand",   new Color(0.82f, 0.68f, 0.46f)),
            Wood  = MercSetupTools.GetOrCreateMaterial("Mat_ArenaWood", new Color(0.42f, 0.28f, 0.16f)),
            Stone = MercSetupTools.GetOrCreateMaterial("Mat_ArenaStone", new Color(0.56f, 0.53f, 0.47f)),
            Road  = MercSetupTools.GetOrCreateMaterial("Mat_ArenaRoad", new Color(0.44f, 0.40f, 0.35f)),
            Gold  = MercSetupTools.GetOrCreateMaterial("Mat_ArenaGold",    new Color(0.78f, 0.62f, 0.24f),
                                                       new Color(0.35f, 0.26f, 0.06f)),
        };

        for (int i = 0; i < 3; i++)
        {
            Color c = DefaultTeamColor(i + 1);
            p.Team[i] = MercSetupTools.GetOrCreateMaterial($"Mat_Team{i + 1}", c, c * 0.35f);
        }
        return p;
    }

    public static Color DefaultTeamColor(int team)
    {
        switch (team)
        {
            case 1:  return new Color(0.90f, 0.25f, 0.25f);
            case 2:  return new Color(0.30f, 0.85f, 0.35f);
            default: return new Color(0.30f, 0.60f, 1.00f);
        }
    }
}
