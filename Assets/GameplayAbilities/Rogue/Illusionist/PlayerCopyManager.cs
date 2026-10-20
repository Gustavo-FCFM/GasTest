using UnityEngine;
using FishNet;

// ============================================================
// PlayerCopyManager  (crea las copias del Ilusionista)
//
// Desde el 20 de octubre de 2026 las copias son invocaciones (Entity_Summon) y la base resuelve
// lo que antes se hacía acá a mano:
//   - Copia exacta (SpawnCopy): límite de MaxCopies, se va la MÁS VIEJA.
//   - Fiesta de copias (SpawnPartyCopy): SIN límite (no cuentan para el de la Copia exacta).
//   - Si el Ilusionista muere, se va del juego o cambia de clase, desaparecen todas (el prefab
//     de la copia dice "Si el dueño muere: desaparece").
// Este componente solo guarda el prefab y el máximo, y las crea.
//
// Vive en el PassiveBehaviorsPrefab del Ilusionista (hijo del jugador). Los GA lo encuentran
// con GetComponentInChildren. Crea solo en el servidor.
// ============================================================
public class PlayerCopyManager : MonoBehaviour
{
    [Tooltip("Prefab de la copia (Entity_PlayerCopy + NetworkObject + NetworkTransform + ASC + collider).")]
    [Label("Prefab de la copia")]
    public GameObject CopyPrefab;
    [Tooltip("Máximo de copias de Copia exacta a la vez. Al invocar una más, se va la más vieja. No aplica a Fiesta.")]
    [Label("Máximo de copias")]
    public int MaxCopies = 4;

    private AbilitySystemComponent _asc;

    private void Awake() => _asc = GetComponentInParent<AbilitySystemComponent>();

    // Copia exacta, con límite. sourceClassIndex = clase del jugador COPIADO (para su arma y
    // su animación); source = ese jugador (su barra de vida). La llama GA_ExactCopy.
    public void SpawnCopy(Vector3 spawnPos, Vector3 target, float speed, int sourceClassIndex,
                          AbilitySystemComponent source = null)
        => Spawn(spawnPos, target, speed, sourceClassIndex, source, Mathf.Max(1, MaxCopies), counts: true);

    // Fiesta de copias: sin límite (se van al explotar o con el Ilusionista). La llama
    // GA_CopyParty, una por aliado en rango.
    public void SpawnPartyCopy(Vector3 spawnPos, Vector3 target, float speed, int sourceClassIndex,
                               AbilitySystemComponent source = null)
        => Spawn(spawnPos, target, speed, sourceClassIndex, source, -1, counts: false);

    private void Spawn(Vector3 spawnPos, Vector3 target, float speed, int sourceClassIndex,
                       AbilitySystemComponent source, int max, bool counts)
    {
        if (CopyPrefab == null || _asc == null || !InstanceFinder.IsServerStarted) return;

        if (Entity_Summon.Spawn(CopyPrefab, _asc, spawnPos, Quaternion.identity, max, counts) is Entity_PlayerCopy copy)
            copy.Begin(target, speed, sourceClassIndex, source);
    }
}
