using FishNet.Object;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// ============================================================
// MercHealthPackSetup
//
// Reparte botiquines por la arena: un anillo alrededor de la meseta central (la zona
// por la que se pelea) y otro más afuera, sobre los carriles hacia las bases, para que
// se pueda volver a la pelea sin caminar hasta casa.
//
// Cada uno se apoya en el piso con un rayo y se corrige al NavMesh, así no quedan
// flotando ni dentro de una rampa. Los que caigan dentro de una sala segura se saltean:
// ahí ya te curás solo.
//
// Son un PUNTO DE PARTIDA. Una vez puestos, moverlos a mano en la escena es lo normal:
// el buen sitio para un botiquín se descubre jugando, no calculando.
// ============================================================
public static class MercHealthPackSetup
{
    private const string ParentName = "HealthPacks";

    // Dos anillos: el de adentro rodea la meseta (peleas por el Objetivo), el de afuera
    // queda a mitad de camino de las bases.
    private const int   InnerCount  = 6;
    private const float InnerRadius = 0.45f;   // fracción del radio de la arena
    private const int   OuterCount  = 3;
    private const float OuterRadius = 0.78f;

    [MenuItem("Mercenarios/Instalar botiquines", false, 6)]
    public static void Install()
    {
        Scene scene = EditorSceneManager.GetActiveScene();

        MercenariesGameMode gm = Object.FindFirstObjectByType<MercenariesGameMode>(FindObjectsInactive.Include);
        if (gm == null)
        {
            Debug.LogError("[Botiquines] Esta no es la escena de la arena (no hay MercenariesGameMode).");
            return;
        }

        if (Object.FindFirstObjectByType<HealthPack>(FindObjectsInactive.Include) != null)
        {
            Debug.Log("[Botiquines] Ya hay botiquines en la escena. Si querés rehacerlos, borrá el " +
                      "objeto 'HealthPacks' y volvé a correr esto.");
            return;
        }

        Vector3 center = gm.ObjectiveSpawnPoint != null ? gm.ObjectiveSpawnPoint.position : Vector3.zero;

        MercArenaBounds bounds = Object.FindFirstObjectByType<MercArenaBounds>(FindObjectsInactive.Include);
        float arenaRadius = bounds != null ? bounds.Radius : 43f;

        Transform parent = new GameObject(ParentName).transform;
        Undo.RegisterCreatedObjectUndo(parent.gameObject, "Instalar botiquines");

        int placed = 0;
        placed += PlaceRing(parent, center, arenaRadius * InnerRadius, InnerCount, 0f);
        // El anillo de afuera va girado medio paso para que no queden alineados con los
        // de adentro (y con las tres bases, que están cada 120°).
        placed += PlaceRing(parent, center, arenaRadius * OuterRadius, OuterCount, 60f);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[Botiquines] {placed} botiquines puestos y escena guardada. Movelos a mano donde " +
                  "tengan sentido: el buen sitio se descubre jugando.");
    }

    private static int PlaceRing(Transform parent, Vector3 center, float radius, int count, float offsetDegrees)
    {
        int placed = 0;

        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count * i + offsetDegrees) * Mathf.Deg2Rad;
            Vector3 spot = center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;

            if (!Resolve(ref spot)) continue;
            if (InsideSafeRoom(spot)) continue;

            GameObject go = new GameObject($"HealthPack_{parent.childCount + 1}");
            go.transform.SetParent(parent, false);
            go.transform.position = spot;

            go.AddComponent<SphereCollider>();
            go.AddComponent<NetworkObject>();
            go.AddComponent<HealthPack>();

            placed++;
        }

        return placed;
    }

    // Apoya el punto en el piso (rayo desde arriba) y lo corrige al NavMesh, que es el
    // invariante de "acá se puede caminar" en esta arena.
    private static bool Resolve(ref Vector3 spot)
    {
        if (Physics.Raycast(spot + Vector3.up * 30f, Vector3.down, out RaycastHit hit, 60f,
                            ~0, QueryTriggerInteraction.Ignore))
            spot = hit.point;

        if (NavMesh.SamplePosition(spot, out NavMeshHit nav, 6f, NavMesh.AllAreas))
        {
            spot = nav.position;
            return true;
        }

        Debug.LogWarning($"[Botiquines] Un punto quedó fuera del NavMesh ({spot}) y se saltea.");
        return false;
    }

    private static bool InsideSafeRoom(Vector3 spot)
    {
        foreach (MercTeamBase b in Object.FindObjectsByType<MercTeamBase>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Vector3 d = spot - b.SafeRoomWorldCenter;
            if (Mathf.Abs(d.x) <= b.SafeRoomSize.x * 0.5f + 2f &&
                Mathf.Abs(d.z) <= b.SafeRoomSize.z * 0.5f + 2f) return true;
        }
        return false;
    }
}
