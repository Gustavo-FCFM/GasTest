using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using UnityEngine;

// ============================================================
// MercEnemySpawner
//
// Un "campamento" de NPCs: mantiene una cantidad fija de enemigos vivos alrededor de
// este punto y los repone unos segundos después de que los maten. Se ponen varios en
// el escenario (sobre todo alrededor del centro, donde aparece el Objetivo: ahí es
// donde el diseño quiere que se junte la gente a farmear y a pelearse).
//
// No es un NetworkBehaviour a propósito: no tiene estado que mostrarle a nadie. Corre
// SOLO en el servidor y lo único que hace es pedirle a FishNet que spawnee el prefab
// (que sí es de red). Así se pueden repartir veinte campamentos por la escena sin
// llenarla de NetworkObjects.
//
// Requisito: el prefab del enemigo tiene que estar en la lista de prefabs spawneables
// de FishNet (DefaultPrefabObjects lo hace solo cuando el prefab tiene NetworkObject)
// y el piso necesita NavMesh horneado.
// ============================================================
public class MercEnemySpawner : MonoBehaviour
{
    [Header("Qué aparece")]
    [Tooltip("Prefab del enemigo. Necesita NetworkObject + MercEnemyAI + NavMeshAgent.")]
    public GameObject EnemyPrefab;

    [Tooltip("Cuántos mantiene vivos a la vez.")]
    public int Count = 3;

    [Tooltip("Radio en el que se reparten alrededor de este punto.")]
    public float SpawnRadius = 5f;

    [Header("Reposición")]
    [Tooltip("Segundos entre que uno muere y aparece su reemplazo.")]
    public float RespawnSeconds = 20f;

    [Tooltip("Segundos entre apariciones al poblar el campamento por primera vez.")]
    public float SpawnInterval = 0.5f;

    [Header("Cuándo")]
    [Tooltip("Poblar el campamento ya en la preparación, para que el escenario nunca se vea vacío.")]
    public bool PopulateDuringWarmup = true;

    // Enemigos vivos de este campamento (solo servidor).
    private readonly List<MercEnemyAI> _alive = new List<MercEnemyAI>();

    private float _nextSpawnTime;

    private void Update()
    {
        if (!InstanceFinder.IsServerStarted) return;
        if (EnemyPrefab == null) return;

        MercenariesGameMode gm = MercenariesGameMode.Instance;
        if (gm != null)
        {
            if (gm.State == EMatchState.Ended) return;
            if (gm.State == EMatchState.Warmup && !PopulateDuringWarmup) return;
        }

        // Limpiar los que ya no existen (murieron y se despawnearon).
        for (int i = _alive.Count - 1; i >= 0; i--)
            if (_alive[i] == null) _alive.RemoveAt(i);

        if (_alive.Count >= Count) return;
        if (Time.time < _nextSpawnTime) return;

        SpawnOne();
    }

    private void SpawnOne()
    {
        Vector3 position = ResolveSpawnPosition();

        GameObject go = Instantiate(EnemyPrefab, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        InstanceFinder.ServerManager.Spawn(go);

        MercEnemyAI ai = go.GetComponent<MercEnemyAI>();
        if (ai != null) ai.Spawner = this;

        _alive.Add(ai);
        _nextSpawnTime = Time.time + SpawnInterval;
    }

    // Punto al azar dentro del radio, pegado al NavMesh. Si el punto elegido no cae
    // sobre el NavMesh (una roca, un borde), se usa el centro del campamento: es
    // preferible que aparezcan encimados un instante a que aparezcan flotando o
    // adentro de una pared, donde el NavMeshAgent no arranca nunca.
    private Vector3 ResolveSpawnPosition()
    {
        Vector2 offset = Random.insideUnitCircle * SpawnRadius;
        Vector3 candidate = transform.position + new Vector3(offset.x, 0f, offset.y);

        if (UnityEngine.AI.NavMesh.SamplePosition(candidate, out UnityEngine.AI.NavMeshHit hit, 4f,
                                                  UnityEngine.AI.NavMesh.AllAreas))
            return hit.position;

        return transform.position;
    }

    // La llama MercEnemyAI al morir: arranca el reloj de reposición.
    public void ServerNotifyEnemyDied(MercEnemyAI enemy)
    {
        _alive.Remove(enemy);
        _nextSpawnTime = Time.time + RespawnSeconds;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, SpawnRadius);
        Gizmos.DrawIcon(transform.position + Vector3.up * 2f, "sv_icon_dot6_pix16_gizmo", true);
    }
}
