using UnityEngine;
using System.Collections.Generic;
using FishNet;
using FishNet.Object;

// ============================================================
// PlayerCopyManager  (gestor de las copias del Ilusionista)
//
// Lleva el registro de las copias activas de ESTE Ilusionista y sus reglas. Hay DOS
// pools independientes:
//   - Copia exacta (SpawnCopy): límite de MaxCopies, se elimina la MÁS VIEJA (FIFO).
//   - Fiesta de copias (SpawnPartyCopy): SIN límite (una por aliado en rango).
// Ambos: sin límite de tiempo, y si el Ilusionista muere TODAS desaparecen (OnDeath).
//
// Vive en el PassiveBehaviorsPrefab del Ilusionista (hijo del jugador), así que
// corre en todos los peers; el spawn/despawn en red lo hace solo el servidor. Los
// GA lo encuentran con GetComponentInChildren.
// ============================================================
public class PlayerCopyManager : MonoBehaviour
{
    [Tooltip("Prefab de la copia (Entity_PlayerCopy + NetworkObject + NetworkTransform + ASC + collider).")]
    public GameObject CopyPrefab;
    [Tooltip("Máximo de copias de Copia exacta a la vez. Al invocar una más, se elimina la más vieja (FIFO). No aplica a Fiesta.")]
    public int MaxCopies = 4;

    private AbilitySystemComponent _asc;
    // Copia exacta: FIFO (las nuevas al final, la más vieja en el índice 0).
    private readonly List<Entity_PlayerCopy> _copies = new List<Entity_PlayerCopy>();
    // Fiesta de copias: sin límite.
    private readonly List<Entity_PlayerCopy> _partyCopies = new List<Entity_PlayerCopy>();

    private void Awake() => _asc = GetComponentInParent<AbilitySystemComponent>();

    private void OnEnable()  { if (_asc != null) _asc.OnDeath += DespawnAll; }
    private void OnDisable()
    {
        if (_asc != null) _asc.OnDeath -= DespawnAll;
        DespawnAll(); // cambio de clase / destrucción: no dejar copias huérfanas
    }

    // Copia exacta: una copia con límite FIFO. sourceClassIndex = clase del jugador
    // COPIADO (para su arma/anim). La llama GA_ExactCopy.
    public void SpawnCopy(Vector3 spawnPos, Vector3 target, float speed, int sourceClassIndex)
    {
        _copies.RemoveAll(c => c == null);

        // Límite FIFO: sacar las más viejas hasta dejar lugar para la nueva.
        int max = Mathf.Max(1, MaxCopies);
        while (_copies.Count >= max)
        {
            Entity_PlayerCopy oldest = _copies[0];
            _copies.RemoveAt(0);
            if (oldest != null) oldest.Dissipate();
        }

        Entity_PlayerCopy copy = InstantiateCopy(spawnPos, target, speed, sourceClassIndex);
        if (copy != null) _copies.Add(copy);
    }

    // Fiesta de copias: una copia SIN límite (se limpia solo por explosión o muerte
    // del Ilusionista). La llama GA_CopyParty (una por aliado en rango).
    public void SpawnPartyCopy(Vector3 spawnPos, Vector3 target, float speed, int sourceClassIndex)
    {
        _partyCopies.RemoveAll(c => c == null);
        Entity_PlayerCopy copy = InstantiateCopy(spawnPos, target, speed, sourceClassIndex);
        if (copy != null) _partyCopies.Add(copy);
    }

    // Instancia + spawnea + inicializa una copia en el servidor. Devuelve null si no
    // se puede (sin prefab, sin servidor, prefab sin NetworkObject).
    private Entity_PlayerCopy InstantiateCopy(Vector3 spawnPos, Vector3 target, float speed, int sourceClassIndex)
    {
        if (CopyPrefab == null || _asc == null || !InstanceFinder.IsServerStarted) return null;

        GameObject obj = Instantiate(CopyPrefab, spawnPos, Quaternion.identity);

        NetworkObject nob = obj.GetComponent<NetworkObject>();
        if (nob == null)
        {
            Debug.LogWarning("[PlayerCopyManager] El CopyPrefab no tiene NetworkObject — no se replicará.");
            Destroy(obj);
            return null;
        }
        InstanceFinder.ServerManager.Spawn(nob);

        Entity_PlayerCopy copy = obj.GetComponent<Entity_PlayerCopy>();
        if (copy != null) copy.ServerInit(_asc, target, speed, sourceClassIndex);
        return copy;
    }

    // Despawnea TODAS las copias (ambos pools). Muerte del Ilusionista o cambio de clase.
    private void DespawnAll()
    {
        // Durante el APAGADO de la red no despawneamos a mano: FishNet ya limpia sus
        // NetworkObjects, y llamar a Despawn en pleno teardown (con el NetworkManager ya
        // null) revienta adentro de FishNet — MatchCondition.RemoveFromMatchWithoutRebuild
        // con NetworkManager null → ArgumentNullException. Acá solo vaciamos las listas.
        if (!InstanceFinder.IsServerStarted)
        {
            _copies.Clear();
            _partyCopies.Clear();
            return;
        }

        DissipateAll(_copies);
        DissipateAll(_partyCopies);
    }

    private static void DissipateAll(List<Entity_PlayerCopy> list)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null) list[i].Dissipate();
        list.Clear();
    }
}
