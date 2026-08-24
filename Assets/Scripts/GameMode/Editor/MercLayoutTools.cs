using System.Collections.Generic;
using System.Text;
using FishNet.Managing.Object;
using FishNet.Object;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// ============================================================
// MercLayoutTools
//
// Herramientas para trabajar la arena A MANO sin perder la simetría, y para
// verificar que no falte nada antes de jugar. A diferencia de MercSetupTools (que
// GENERA cosas), estas operan sobre la escena que ya armaste: no borran nada y todo
// se puede deshacer con Ctrl+Z.
//
//   4 · Acomodar las bases   → las tres a 120° exactos, mirando al centro.
//   5 · Espejar la selección → decorás un tercio de la arena y el resto se copia solo.
//   6 · Revisar el montaje   → informe de qué está bien y qué falta.
//
// La #5 es la importante para decorar: ponés torres, tiendas, rocas y estandartes en
// UN sector, apretás el botón, y aparecen idénticos en los otros dos. Es la forma de
// que un mapa hecho a mano siga siendo justo para los tres equipos.
// ============================================================
public static class MercLayoutTools
{
    // =========================================================
    // 4 · ACOMODAR LAS BASES
    // =========================================================

    [MenuItem("Mercenarios/4 · Acomodar las bases en simetría de 3", false, 20)]
    public static void AlignBases()
    {
        var bases = new List<MercTeamBase>(Object.FindObjectsByType<MercTeamBase>(FindObjectsSortMode.None));
        bases.Sort((a, b) => a.TeamID.CompareTo(b.TeamID));

        if (bases.Count != MercenariesGameMode.TeamCount)
        {
            Debug.LogError($"[Mercenarios] Encontré {bases.Count} bases y esperaba {MercenariesGameMode.TeamCount}. " +
                           "Revisá que cada base tenga su MercTeamBase y un TeamID distinto (1, 2 y 3).");
            return;
        }

        Vector3 center = ResolveArenaCenter();

        // La base del EQUIPO 1 manda: se respeta dónde la pusiste (distancia y ángulo)
        // y las otras dos se acomodan a 120° y 240° de esa. Así el ajuste conserva tu
        // intención en vez de imponer una posición mía.
        MercTeamBase reference = bases[0];
        Vector3 refDir = Flat(reference.transform.position - center);

        if (refDir.sqrMagnitude < 0.01f)
        {
            Debug.LogError("[Mercenarios] La base del equipo 1 está justo en el centro de la arena — " +
                           "movela para afuera antes de acomodar.");
            return;
        }

        float radius     = refDir.magnitude;
        float baseAngle  = Mathf.Atan2(refDir.x, refDir.z) * Mathf.Rad2Deg;
        float height     = reference.transform.position.y;

        for (int i = 0; i < bases.Count; i++)
        {
            MercTeamBase b = bases[i];
            Undo.RecordObject(b.transform, "Acomodar bases");

            float angle  = baseAngle + i * (360f / MercenariesGameMode.TeamCount);
            Vector3 dir  = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 pos  = center + dir * radius;
            pos.y        = height;

            b.transform.position = pos;
            // Mirando al centro: el +Z de la sala apunta a la puerta, que es como la
            // arma el generador y como la esperan la entrega y los spawns.
            b.transform.rotation = Quaternion.LookRotation(Flat(center - pos).normalized, Vector3.up);

            EditorUtility.SetDirty(b.transform);
        }

        Debug.Log($"[Mercenarios] Bases acomodadas: radio {radius:F1} m desde {center}, " +
                  $"a {baseAngle:F0}°, {baseAngle + 120f:F0}° y {baseAngle + 240f:F0}°. " +
                  "Todo lo que cuelga de cada base (entrega, spawns, sala segura) se movió con ella.");
    }

    // =========================================================
    // 5 · ESPEJAR LA SELECCIÓN
    // =========================================================

    [MenuItem("Mercenarios/5 · Espejar la selección a los otros 2 sectores", false, 21)]
    public static void MirrorSelection()
    {
        Transform[] selection = Selection.GetTransforms(SelectionMode.TopLevel | SelectionMode.Editable);
        if (selection.Length == 0)
        {
            Debug.LogWarning("[Mercenarios] Seleccioná primero los objetos que querés repetir en los otros dos sectores.");
            return;
        }

        Vector3 center = ResolveArenaCenter();
        int created = 0;

        foreach (Transform original in selection)
        {
            // No copiamos las bases: para eso está el acomodador, y duplicarlas rompería
            // los TeamID (quedarían dos equipos 1).
            if (original.GetComponentInChildren<MercTeamBase>() != null)
            {
                Debug.LogWarning($"[Mercenarios] Saltée '{original.name}': las bases no se espejan " +
                                 "(usá '4 · Acomodar las bases').");
                continue;
            }

            for (int k = 1; k <= 2; k++)
            {
                GameObject copy = Duplicate(original.gameObject);
                if (copy == null) continue;

                copy.name = $"{original.name} (Sector {k + 1})";
                copy.transform.SetPositionAndRotation(original.position, original.rotation);
                copy.transform.RotateAround(center, Vector3.up, 120f * k);

                Undo.RegisterCreatedObjectUndo(copy, "Espejar a los sectores");
                created++;
            }
        }

        Debug.Log($"[Mercenarios] {created} copias creadas girando {selection.Length} objeto(s) " +
                  $"120° y 240° alrededor de {center}. Ctrl+Z deshace todo de una.");
    }

    // Copia un objeto conservando el vínculo con su prefab cuando lo tiene: si no, cada
    // adorno espejado quedaría suelto y cambiar el prefab original no los actualizaría.
    private static GameObject Duplicate(GameObject original)
    {
        if (PrefabUtility.IsAnyPrefabInstanceRoot(original))
        {
            Object source = PrefabUtility.GetCorrespondingObjectFromSource(original);
            if (source != null)
            {
                GameObject copy = (GameObject)PrefabUtility.InstantiatePrefab(source, original.transform.parent);
                copy.transform.localScale = original.transform.localScale;
                return copy;
            }
        }

        return Object.Instantiate(original, original.transform.parent);
    }

    // =========================================================
    // 6 · REVISAR EL MONTAJE
    // =========================================================

    [MenuItem("Mercenarios/6 · Revisar el montaje", false, 22)]
    public static void ValidateSetup()
    {
        var report = new StringBuilder();
        int problems = 0;

        report.AppendLine("═══ REVISIÓN DEL MODO MERCENARIOS ═══");

        // --- modo de juego ---
        MercenariesGameMode gm = Object.FindFirstObjectByType<MercenariesGameMode>();
        if (gm == null)
        {
            Line(report, false, "No hay MercenariesGameMode en la escena.");
            Debug.LogError(report.ToString());
            return;
        }

        Line(report, true, "MercenariesGameMode encontrado.");

        NetworkObject nob = gm.GetComponent<NetworkObject>();
        problems += Line(report, nob != null,
            nob != null ? "Está sobre un NetworkObject."
                        : "NO tiene NetworkObject en el mismo GameObject: nada del modo va a correr en red.");

        // --- objetivo ---
        problems += Line(report, gm.ObjectiveSpawnPoint != null,
            gm.ObjectiveSpawnPoint != null
                ? $"Punto de aparición del Objetivo: {gm.ObjectiveSpawnPoint.name}."
                : "FALTA el punto de aparición del Objetivo (ObjectiveSpawnPoint).");

        if (gm.ObjectivePrefab == null)
        {
            // No alcanza con avisar: el prefab casi siempre EXISTE y lo único que pasó
            // es que el campo quedó vacío (lo renombraste, lo moviste, regeneraste la
            // escena). Lo buscamos por componente y ofrecemos enchufarlo.
            GameObject found = MercSetupTools.FindObjectivePrefab();

            if (found != null && EditorUtility.DisplayDialog("Falta el prefab del Objetivo",
                    $"El campo Objective Prefab está vacío, pero encontré '{found.name}' en el proyecto.\n\n" +
                    "¿Lo asigno?", "Asignar", "No"))
            {
                Undo.RecordObject(gm, "Asignar prefab del Objetivo");
                gm.ObjectivePrefab = found;
                EditorUtility.SetDirty(gm);
                Line(report, true, $"Prefab del Objetivo asignado automáticamente: {found.name}.");
            }
            else
            {
                problems += Line(report, false, found != null
                    ? $"El campo Objective Prefab está vacío (hay uno sin asignar: '{found.name}')."
                    : "FALTA el prefab del Objetivo (ObjectivePrefab). Sin eso nunca aparece.");
            }
        }

        // Con el prefab ya en la mano (asignado antes o recién ahora), se revisa que sirva.
        if (gm.ObjectivePrefab != null)
        {
            Line(report, true, $"Prefab del Objetivo: {gm.ObjectivePrefab.name}.");

            problems += Line(report, gm.ObjectivePrefab.GetComponent<MercObjective>() != null,
                "El prefab del Objetivo tiene MercObjective.");

            NetworkObject objNob = gm.ObjectivePrefab.GetComponent<NetworkObject>();
            problems += Line(report, objNob != null, "El prefab del Objetivo tiene NetworkObject.");

            if (objNob != null)
            {
                bool registered = IsRegisteredAsSpawnable(objNob);
                problems += Line(report, registered, registered
                    ? "El prefab del Objetivo está registrado en los prefabs de red de FishNet."
                    : "El prefab del Objetivo NO está en la lista de prefabs de red de FishNet — " +
                      "el servidor no lo va a poder spawnear (ESTA suele ser la causa de que 'no aparezca').");

                if (!registered && EditorUtility.DisplayDialog("Prefab sin registrar",
                        "El Objetivo no está en la lista de prefabs de red de FishNet, así que el servidor " +
                        "no lo puede crear.\n\n¿Refresco la lista ahora?", "Refrescar", "Después"))
                {
                    EditorApplication.ExecuteMenuItem("Tools/Fish-Networking/Utility/Refresh Default Prefabs");
                }
            }
        }

        // Cuándo aparece, en tiempo de reloj. Es la otra causa clásica de "no aparece":
        // que no se haya esperado lo suficiente.
        report.AppendLine($"   · El primer Objetivo sale a los {gm.WarmupSeconds + gm.FirstObjectiveDelay:F0} s " +
                          $"de empezar ({gm.WarmupSeconds:F0} de preparación + {gm.FirstObjectiveDelay:F0} de espera).");

        // --- bases ---
        problems += ValidateBases(report, gm);

        // --- jugador ---
        NetworkGameManager manager = Object.FindFirstObjectByType<NetworkGameManager>();
        problems += Line(report, manager != null && manager.PlayerPrefab != null,
            manager == null ? "No hay NetworkGameManager en la escena."
                            : manager.PlayerPrefab != null ? "El NetworkGameManager tiene el prefab del jugador."
                                                           : "El NetworkGameManager NO tiene prefab de jugador.");

        if (manager != null && manager.PlayerPrefab != null)
        {
            AbilitySystemComponent asc = manager.PlayerPrefab.GetComponent<AbilitySystemComponent>();
            if (asc != null)
                problems += Line(report, asc.MaxLevel == gm.MaxTeamLevel,
                    asc.MaxLevel == gm.MaxTeamLevel
                        ? $"Nivel máximo coincide (jugador y modo en {asc.MaxLevel})."
                        : $"El nivel máximo NO coincide: el jugador tiene {asc.MaxLevel} y el modo {gm.MaxTeamLevel}.");
        }

        // --- enemigos ---
        var spawners = Object.FindObjectsByType<MercEnemySpawner>(FindObjectsSortMode.None);
        int withoutPrefab = 0, totalEnemies = 0;
        foreach (MercEnemySpawner s in spawners)
        {
            if (s.EnemyPrefab == null) withoutPrefab++;
            else totalEnemies += s.Count;
        }

        problems += Line(report, spawners.Length > 0,
            spawners.Length > 0 ? $"{spawners.Length} campamentos, {totalEnemies} enemigos vivos a la vez."
                                : "No hay ningún campamento de enemigos en la escena.");
        if (withoutPrefab > 0)
            problems += Line(report, false, $"{withoutPrefab} campamentos SIN prefab de enemigo asignado.");

        // --- navmesh ---
        bool hasNavMesh = NavMesh.CalculateTriangulation().vertices.Length > 0;
        problems += Line(report, hasNavMesh, hasNavMesh
            ? "Hay NavMesh horneado (los NPCs pueden caminar)."
            : "NO hay NavMesh en la escena: los fantasmas se quedan clavados donde aparezcan.");

        // --- límites y rejas ---
        var bounds = Object.FindFirstObjectByType<MercArenaBounds>();
        Line(report, bounds != null, bounds != null
            ? "Techo y paredes invisibles configurados (MercArenaBounds)."
            : "Sin MercArenaBounds: un jugador con dash o salto se puede escapar del escenario.");

        var gates = Object.FindObjectsByType<MercGate>(FindObjectsSortMode.None);
        Line(report, gates.Length > 0, gates.Length > 0
            ? $"{gates.Length} rejas de preparación."
            : "Sin rejas (MercGate): durante la preparación nadie está encerrado en su base.");

        // --- HUD ---
        bool hud = Object.FindFirstObjectByType<UI_MercenariesHUD>() != null;
        problems += Line(report, hud, hud ? "HUD de partida en la escena."
                                          : "FALTA el HUD (UI_MercenariesHUD): no se va a ver el marcador.");

        report.AppendLine();
        report.AppendLine(problems == 0
            ? "RESULTADO: todo en orden."
            : $"RESULTADO: {problems} cosa(s) para arreglar (las marcadas con ✗).");

        if (problems == 0) Debug.Log(report.ToString());
        else               Debug.LogWarning(report.ToString());
    }

    private static int ValidateBases(StringBuilder report, MercenariesGameMode gm)
    {
        int problems = 0;

        var bases = new List<MercTeamBase>(Object.FindObjectsByType<MercTeamBase>(FindObjectsSortMode.None));
        bases.Sort((a, b) => a.TeamID.CompareTo(b.TeamID));

        problems += Line(report, bases.Count == MercenariesGameMode.TeamCount,
            $"Bases en la escena: {bases.Count} (esperadas {MercenariesGameMode.TeamCount}).");

        var seenTeams = new HashSet<int>();
        foreach (MercTeamBase b in bases)
        {
            if (!seenTeams.Add(b.TeamID))
                problems += Line(report, false, $"Hay más de una base con TeamID {b.TeamID}.");

            if (b.DeliveryPoint == null)
                problems += Line(report, false, $"La base del equipo {b.TeamID} no tiene punto de entrega.");
            if (b.SafeRoomCenter == null)
                Line(report, true, $"La base del equipo {b.TeamID} usa su propio transform como centro de la sala segura.");
            if (b.SpawnPoints == null || b.SpawnPoints.Length == 0)
                problems += Line(report, false, $"La base del equipo {b.TeamID} no tiene puntos de aparición.");
        }

        // ¿Están asignadas en el modo de juego? Es distinto de que existan en la escena.
        int assigned = 0;
        if (gm.TeamBases != null)
            foreach (MercTeamBase b in gm.TeamBases) if (b != null) assigned++;

        problems += Line(report, assigned == MercenariesGameMode.TeamCount,
            assigned == MercenariesGameMode.TeamCount
                ? "Las tres bases están asignadas en el modo de juego."
                : $"El modo de juego solo tiene {assigned} bases asignadas en su lista TeamBases.");

        // --- simetría ---
        if (bases.Count == MercenariesGameMode.TeamCount)
        {
            Vector3 center = ResolveArenaCenter();
            float minR = float.MaxValue, maxR = 0f;
            var angles = new List<float>();

            foreach (MercTeamBase b in bases)
            {
                Vector3 d = Flat(b.transform.position - center);
                minR = Mathf.Min(minR, d.magnitude);
                maxR = Mathf.Max(maxR, d.magnitude);
                angles.Add(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
            }

            float radiusSpread = maxR - minR;
            problems += Line(report, radiusSpread < 1.5f,
                $"Distancia de las bases al centro: entre {minR:F1} y {maxR:F1} m " +
                (radiusSpread < 1.5f ? "(parejas)." : "— hay hasta " + radiusSpread.ToString("F1") + " m de diferencia."));

            float worstAngle = 0f;
            for (int i = 0; i < angles.Count; i++)
            {
                float expected = angles[0] + i * 120f;
                worstAngle = Mathf.Max(worstAngle, Mathf.Abs(Mathf.DeltaAngle(angles[i], expected)));
            }

            problems += Line(report, worstAngle < 3f,
                worstAngle < 3f ? "Las bases están a 120° entre sí."
                                : $"Las bases están hasta {worstAngle:F0}° fuera de los 120°. " +
                                  "Corré '4 · Acomodar las bases en simetría de 3'.");
        }

        return problems;
    }

    // =========================================================
    // AYUDANTES
    // =========================================================

    // El centro del mapa. Se prefiere el punto donde aparece el Objetivo porque ESE es
    // el centro por definición del modo; si no está, se promedian las bases.
    private static Vector3 ResolveArenaCenter()
    {
        MercenariesGameMode gm = Object.FindFirstObjectByType<MercenariesGameMode>();
        if (gm != null && gm.ObjectiveSpawnPoint != null)
        {
            Vector3 c = gm.ObjectiveSpawnPoint.position;
            c.y = 0f;
            return c;
        }

        var bases = Object.FindObjectsByType<MercTeamBase>(FindObjectsSortMode.None);
        if (bases.Length > 0)
        {
            Vector3 sum = Vector3.zero;
            foreach (MercTeamBase b in bases) sum += b.transform.position;
            Vector3 c = sum / bases.Length;
            c.y = 0f;
            return c;
        }

        GameObject arena = GameObject.Find(MercArenaBuilder.ArenaRootName);
        return arena != null ? arena.transform.position : Vector3.zero;
    }

    // ¿Está este NetworkObject en la lista de prefabs spawneables de FishNet? Si no
    // está, ServerManager.Spawn no lo puede crear en los clientes.
    private static bool IsRegisteredAsSpawnable(NetworkObject prefab)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:DefaultPrefabObjects"))
        {
            var collection = AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (collection == null) continue;

            int count = collection.GetObjectCount();
            for (int i = 0; i < count; i++)
            {
                NetworkObject entry = collection.GetObject(true, i);
                if (entry == prefab) return true;
            }
        }
        return false;
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    // Escribe una línea del informe y devuelve 1 si es un problema (para el conteo).
    private static int Line(StringBuilder report, bool ok, string message)
    {
        report.AppendLine($"{(ok ? " ✓" : " ✗")} {message}");
        return ok ? 0 : 1;
    }
}
