using UnityEngine;
using System.Collections.Generic;
using FishNet;
using FishNet.Object;

// ============================================================
// PlayerCopyManager  (gestor de las Copias exactas del Ilusionista)
//
// Lleva el registro de las copias activas de ESTE Ilusionista y les aplica las
// reglas de la habilidad Copia exacta:
//   - Límite de MaxCopies activas. Al invocar una más, se elimina la MÁS VIEJA
//     (FIFO), sin límite de tiempo para las demás.
//   - Si el Ilusionista muere, todas sus copias desaparecen (OnDeath).
//
// Vive en el PassiveBehaviorsPrefab del Ilusionista (hijo del jugador), así que
// corre en todos los peers; el spawn/despawn en red lo hace solo el servidor. El
// GA_ExactCopy lo encuentra con GetComponentInChildren y le pide SpawnCopy.
// ============================================================
public class PlayerCopyManager : MonoBehaviour
{
    [Tooltip("Prefab de la copia (Entity_PlayerCopy + NetworkObject + NetworkTransform + ASC + collider).")]
    public GameObject CopyPrefab;
    [Tooltip("Máximo de copias activas a la vez. Al invocar una más, se elimina la más vieja (FIFO).")]
    public int MaxCopies = 4;

    private AbilitySystemComponent _asc;
    // Registro FIFO: las nuevas van al final, la más vieja está en el índice 0.
    private readonly List<Entity_PlayerCopy> _copies = new List<Entity_PlayerCopy>();

    private void Awake() => _asc = GetComponentInParent<AbilitySystemComponent>();

    private void OnEnable()  { if (_asc != null) _asc.OnDeath += DespawnAll; }
    private void OnDisable()
    {
        if (_asc != null) _asc.OnDeath -= DespawnAll;
        DespawnAll(); // cambio de clase / destrucción: no dejar copias huérfanas
    }

    // Invoca una copia caminando desde spawnPos hacia target a 'speed'. Server-side.
    // Respeta el límite FIFO. La llama GA_ExactCopy.
    public void SpawnCopy(Vector3 spawnPos, Vector3 target, float speed)
    {
        if (CopyPrefab == null || _asc == null || !InstanceFinder.IsServerStarted) return;

        PruneDead();

        // Límite FIFO: sacar las más viejas hasta dejar lugar para la nueva.
        int max = Mathf.Max(1, MaxCopies);
        while (_copies.Count >= max)
        {
            Entity_PlayerCopy oldest = _copies[0];
            _copies.RemoveAt(0);
            if (oldest != null) oldest.Dissipate();
        }

        GameObject obj = Instantiate(CopyPrefab, spawnPos, Quaternion.identity);

        NetworkObject nob = obj.GetComponent<NetworkObject>();
        if (nob == null)
        {
            Debug.LogWarning("[PlayerCopyManager] El CopyPrefab no tiene NetworkObject — no se replicará.");
            Destroy(obj);
            return;
        }
        InstanceFinder.ServerManager.Spawn(nob);

        Entity_PlayerCopy copy = obj.GetComponent<Entity_PlayerCopy>();
        if (copy != null)
        {
            copy.ServerInit(_asc, target, speed);
            _copies.Add(copy);
        }
    }

    // Quita del registro las copias ya despawneadas (explotaron por su cuenta).
    private void PruneDead() => _copies.RemoveAll(c => c == null);

    // Despawnea todas las copias activas (muerte del Ilusionista o cambio de clase).
    private void DespawnAll()
    {
        for (int i = 0; i < _copies.Count; i++)
            if (_copies[i] != null) _copies[i].Dissipate();
        _copies.Clear();
    }
}
