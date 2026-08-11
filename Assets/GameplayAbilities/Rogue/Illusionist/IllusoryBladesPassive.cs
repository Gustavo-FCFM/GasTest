using UnityEngine;
using FishNet;
using FishNet.Object;

// ============================================================
// IllusoryBladesPassive  (Cuchillas ilusorias — pasiva del Ilusionista)
//
// Cada vez que el jugador CONECTA un golpe de daño, lanza una cuchilla lenta
// hacia el enemigo más cercano, prefiriendo uno DISTINTO al que golpeó; si no hay
// otro enemigo cerca, la cuchilla cae sobre la misma víctima. La cuchilla no hace
// daño, solo genera una Herida al impactar (que suma para la explosión de las
// heridas — el GE apilable "Mortal Wounds").
//
// Escucha el evento AbilitySystemComponent.OnDealtDamage, que se dispara con
// autoridad de servidor (solo en golpes directos, no ticks de DoT). Así que la
// invocación de la cuchilla corre en el servidor y se replica como cualquier
// NetworkObject. Como este componente vive en el PassiveBehaviorsPrefab del
// Ilusionista, su sola presencia = la pasiva activa (no hace falta ningún tag).
//
// SETUP: este componente vive en el PassiveBehaviorsPrefab de la clase Ilusionista
// (un prefab que se instancia como hijo del jugador al equipar la clase — ver
// CharacterClassDefinition.PassiveBehaviorsPrefab y PlayerController.SetupPassiveBehaviors),
// no directo en el prefab del Player. Por eso busca el ASC en el PADRE. Asigná el
// prefab de la cuchilla (Entity_IllusoryBlade) y el GE de Herida (Mortal Wounds).
// ============================================================
public class IllusoryBladesPassive : MonoBehaviour
{
    [Header("Cuchilla")]
    [Tooltip("Prefab con Entity_IllusoryBlade + NetworkObject + NetworkTransform.")]
    public GameObject BladePrefab;
    [Tooltip("Herida que la cuchilla aplica al impactar (normalmente el mismo GE_Heridas).")]
    public GameplayEffect WoundEffect;

    [Header("Búsqueda de Objetivo")]
    [Tooltip("Alcance para buscar al enemigo más cercano (distinto al golpeado).")]
    public float SearchRadius = 15f;
    public LayerMask CharacterLayer;

    private AbilitySystemComponent _asc;

    // El ASC está en el jugador (este componente vive en un hijo).
    private void Awake() => _asc = GetComponentInParent<AbilitySystemComponent>();

    private void OnEnable()  { if (_asc != null) _asc.OnDealtDamage += HandleDealtDamage; }
    private void OnDisable() { if (_asc != null) _asc.OnDealtDamage -= HandleDealtDamage; }

    // Al conectar un golpe: lanzo una cuchilla al enemigo más cercano, prefiriendo
    // uno DISTINTO a la víctima; si no hay otro cerca, cae sobre la misma víctima.
    // OnDealtDamage ya es server-side; igual chequeamos que haya servidor antes de
    // spawnear en red.
    //
    // 'damageDealt' (cuánto entró de verdad) no se usa acá: la cuchilla sale igual
    // sin importar el tamaño del golpe. El parámetro está en el evento para las
    // pasivas que sí escalan con el daño (ver PaladinAuraPassive).
    private void HandleDealtDamage(AbilitySystemComponent victim, float damageDealt)
    {
        if (BladePrefab == null) return;
        if (!InstanceFinder.IsServerStarted) return;

        AbilitySystemComponent target = FindBladeTarget(victim);
        if (target == null) return; // no hay ningún enemigo válido (ni siquiera la víctima)

        Vector3 spawnPos = _asc.transform.position + Vector3.up * 1.2f;
        GameObject bladeObj = Instantiate(BladePrefab, spawnPos, Quaternion.identity);

        NetworkObject nob = bladeObj.GetComponent<NetworkObject>();
        if (nob != null) InstanceFinder.ServerManager.Spawn(nob);
        else Debug.LogWarning("[IllusoryBladesPassive] El BladePrefab no tiene NetworkObject — no se replicará.");

        Entity_IllusoryBlade blade = bladeObj.GetComponent<Entity_IllusoryBlade>();
        if (blade != null) blade.ServerInit(target, _asc, WoundEffect);
    }

    // Objetivo de la cuchilla: el enemigo vivo más cercano dentro de SearchRadius,
    // PREFIRIENDO uno distinto de la víctima recién golpeada. Si no hay ningún otro
    // enemigo cerca, cae sobre la MISMA víctima (así en 1v1 la cuchilla igual sale).
    // Devuelve null solo si no hay ningún enemigo válido (ni la víctima).
    private AbilitySystemComponent FindBladeTarget(AbilitySystemComponent victim)
    {
        Vector3 origin = _asc.transform.position;
        Collider[] cols = Physics.OverlapSphere(origin, SearchRadius, CharacterLayer);
        AbilitySystemComponent best = null;
        float bestDist = float.MaxValue;

        foreach (var c in cols)
        {
            AbilitySystemComponent asc = c.GetComponentInParent<AbilitySystemComponent>();
            // Excluye solo a uno mismo y a la víctima: buscamos "otro" enemigo.
            if (asc == null || ReferenceEquals(asc, _asc) || ReferenceEquals(asc, victim)) continue;
            if (!_asc.IsEnemyOf(asc) || asc.HasTag(EGameplayTag.State_Dead)) continue;

            float d = (asc.transform.position - origin).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = asc; }
        }

        // Sin otro enemigo cerca → fallback a la misma víctima (si sigue viva y es
        // un enemigo válido: podría haber muerto por este mismo golpe).
        if (best == null && victim != null && _asc.IsEnemyOf(victim) && !victim.HasTag(EGameplayTag.State_Dead))
            best = victim;

        return best;
    }
}
