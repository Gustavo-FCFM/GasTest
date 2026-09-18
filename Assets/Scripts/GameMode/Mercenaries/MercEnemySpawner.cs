using System.Collections.Generic;
using FishNet;
using UnityEngine;

// ============================================================
// MercEnemySpawner
//
// Un "campamento" de NPCs: mantiene una cantidad fija de enemigos vivos alrededor de
// este punto y los repone unos segundos después de que los maten.
//
// QUÉ APARECE Y CUÁNDO — la tabla de abajo:
// cada renglón dice un TIPO de enemigo, con qué peso puede salir, y a partir de qué
// minuto de partida está habilitado. Cada vez que toca reponer, el campamento sortea
// entre los tipos que ya se desbloquearon.
//
// Así se arma la curva de la partida sin escribir una línea de código: fantasmas desde
// el arranque, magos a partir del minuto 2, jefes a partir del 5. La partida se pone
// fea sola a medida que los equipos suben de nivel — que es exactamente lo que uno
// quiere de un modo de 15 minutos.
//
// No es un NetworkBehaviour a propósito: no tiene estado que mostrarle a nadie. Corre
// SOLO en el servidor y lo único que hace es pedirle a FishNet que spawnee el prefab
// (que sí es de red). Así se pueden repartir veinte campamentos sin llenar la escena
// de NetworkObjects.
// ============================================================
public class MercEnemySpawner : MonoBehaviour
{
    // Un renglón de la tabla: qué tipo, con cuánta probabilidad, y desde cuándo.
    [System.Serializable]
    public class SpawnRule
    {
        [Tooltip("Qué enemigo. El prefab lo resuelve MercEnemyCatalog.")]
        public EMercEnemyType Type = EMercEnemyType.Ghost;

        [Tooltip("Peso del sorteo. NO tiene que sumar 100 entre todos: el porcentaje real " +
                 "se calcula sobre los tipos que estén desbloqueados en ese momento. Poner " +
                 "70 y 30 es lo mismo que poner 7 y 3.")]
        [Range(0f, 100f)] public float Weight = 100f;

        [Tooltip("Minuto de partida a partir del cual PUEDE aparecer. En 0 aparece desde el " +
                 "arranque. El reloj cuenta desde que termina la preparación.")]
        public float UnlockMinute = 0f;

        [Tooltip("Cuántos de ESTE tipo puede haber vivos a la vez en este campamento. " +
                 "En 0 no hay límite. Sirve para los jefes: sin esto, un campamento de tres " +
                 "puede sacarte tres jefes juntos.")]
        public int MaxAlive = 0;
    }

    [Header("Qué aparece")]
    [Tooltip("La tabla de aparición. Si la dejás vacía, el campamento usa el prefab de respaldo.")]
    public List<SpawnRule> SpawnTable = new List<SpawnRule>
    {
        new SpawnRule { Type = EMercEnemyType.Ghost, Weight = 100f, UnlockMinute = 0f },
    };

    [Tooltip("Prefab que se usa si la tabla está vacía o si el catálogo no resuelve ningún " +
             "tipo. Es la red de seguridad: un campamento mal configurado saca esto en vez " +
             "de quedarse vacío toda la partida.")]
    public GameObject EnemyPrefab;

    [Header("Cuántos")]
    [Tooltip("Cuántos mantiene vivos a la vez, contando todos los tipos.")]
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

    // Enemigos vivos de este campamento, con el tipo con el que salieron (solo servidor).
    private readonly List<MercEnemyAI> _alive = new List<MercEnemyAI>();
    private readonly List<EMercEnemyType> _aliveTypes = new List<EMercEnemyType>();

    private float _nextSpawnTime;

    private void Update()
    {
        if (!InstanceFinder.IsServerStarted) return;

        MercenariesGameMode gm = MercenariesGameMode.Instance;
        if (gm != null)
        {
            if (gm.State == EMatchState.Ended) return;
            if (gm.State == EMatchState.Warmup && !PopulateDuringWarmup) return;
        }

        // Limpiar los que ya no existen (murieron y se despawnearon).
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            if (_alive[i] != null) continue;
            _alive.RemoveAt(i);
            _aliveTypes.RemoveAt(i);
        }

        if (_alive.Count >= Count) return;
        if (Time.time < _nextSpawnTime) return;

        SpawnOne(gm);
    }

    // =========================================================
    // SORTEO
    // =========================================================

    // Minuto de partida actual. En la preparación es 0, así que solo salen los tipos
    // desbloqueados desde el arranque.
    private float CurrentMatchMinutes(MercenariesGameMode gm)
        => gm != null ? gm.MatchElapsedSeconds / 60f : 0f;

    // Elige un tipo entre los que YA se desbloquearon, con probabilidad proporcional a su
    // peso. Devuelve false si no hay ninguno disponible (entonces se usa el respaldo).
    private bool TryPickType(MercenariesGameMode gm, out EMercEnemyType picked)
    {
        picked = EMercEnemyType.Ghost;

        MercEnemyCatalog catalog = MercEnemyCatalog.Instance;
        if (catalog == null || SpawnTable == null || SpawnTable.Count == 0) return false;

        float minutes = CurrentMatchMinutes(gm);

        // Primero el total de pesos de lo que puede salir AHORA. Se recorre dos veces en
        // vez de armar una lista temporal: son cuatro o cinco renglones y esto corre cada
        // vez que muere un bicho, no por frame.
        float total = 0f;
        foreach (SpawnRule rule in SpawnTable)
            if (IsAvailable(rule, minutes, catalog)) total += rule.Weight;

        if (total <= 0f) return false;

        float roll = Random.value * total;
        foreach (SpawnRule rule in SpawnTable)
        {
            if (!IsAvailable(rule, minutes, catalog)) continue;

            roll -= rule.Weight;
            if (roll > 0f) continue;

            picked = rule.Type;
            return true;
        }

        return false;
    }

    private bool IsAvailable(SpawnRule rule, float minutes, MercEnemyCatalog catalog)
    {
        if (rule == null || rule.Weight <= 0f) return false;
        if (minutes < rule.UnlockMinute) return false;
        if (!catalog.Has(rule.Type)) return false;
        if (rule.MaxAlive > 0 && CountAliveOfType(rule.Type) >= rule.MaxAlive) return false;
        return true;
    }

    private int CountAliveOfType(EMercEnemyType type)
    {
        int n = 0;
        for (int i = 0; i < _aliveTypes.Count; i++)
            if (_aliveTypes[i] == type) n++;
        return n;
    }

    // =========================================================
    // APARICIÓN
    // =========================================================

    private void SpawnOne(MercenariesGameMode gm)
    {
        GameObject prefab;
        EMercEnemyType type = EMercEnemyType.Ghost;

        if (TryPickType(gm, out type))
            prefab = MercEnemyCatalog.Instance.GetPrefab(type);
        else
            prefab = EnemyPrefab;   // red de seguridad

        if (prefab == null) return;

        Vector3 position = ResolveSpawnPosition();

        GameObject go = Instantiate(prefab, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        InstanceFinder.ServerManager.Spawn(go);

        MercEnemyAI ai = go.GetComponent<MercEnemyAI>();
        if (ai != null) ai.Spawner = this;

        _alive.Add(ai);
        _aliveTypes.Add(type);
        _nextSpawnTime = Time.time + SpawnInterval;
    }

    // Punto al azar dentro del radio, pegado al NavMesh. Si el punto elegido no cae sobre
    // el NavMesh (una roca, un borde), se usa el centro del campamento: es preferible que
    // aparezcan encimados un instante a que aparezcan flotando o adentro de una pared,
    // donde el NavMeshAgent no arranca nunca.
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
        int index = _alive.IndexOf(enemy);
        if (index >= 0)
        {
            _alive.RemoveAt(index);
            _aliveTypes.RemoveAt(index);
        }

        _nextSpawnTime = Time.time + RespawnSeconds;
    }

    // =========================================================
    // AYUDA PARA EL EDITOR
    // =========================================================

    // Qué porcentaje REAL tiene cada tipo en un minuto dado. Lo usa el inspector para
    // mostrar la mezcla, porque los pesos sueltos no se leen: "70 y 30" y "7 y 3" son lo
    // mismo, y con tres renglones desbloqueándose en distintos momentos ya no se saca de
    // cabeza.
    public void GetMixAt(float minutes, List<EMercEnemyType> types, List<float> percents)
    {
        types.Clear();
        percents.Clear();

        MercEnemyCatalog catalog = MercEnemyCatalog.Instance;
        if (catalog == null || SpawnTable == null) return;

        float total = 0f;
        foreach (SpawnRule rule in SpawnTable)
            if (rule != null && rule.Weight > 0f && minutes >= rule.UnlockMinute && catalog.Has(rule.Type))
                total += rule.Weight;

        if (total <= 0f) return;

        foreach (SpawnRule rule in SpawnTable)
        {
            if (rule == null || rule.Weight <= 0f) continue;
            if (minutes < rule.UnlockMinute || !catalog.Has(rule.Type)) continue;

            types.Add(rule.Type);
            percents.Add(rule.Weight / total * 100f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, SpawnRadius);
        Gizmos.DrawIcon(transform.position + Vector3.up * 2f, "sv_icon_dot6_pix16_gizmo", true);
    }
}
