using UnityEngine;

// ============================================================
// DefensiveTacticsPassive  (Tácticas defensivas — pasiva del Cazador, 15 de octubre de 2026)
//
// "Cada impacto recibido da una acumulación de velocidad de movimiento y resistencia al daño
// (máx. 3). Con el máximo, su siguiente Retirada lo vuelve invisible por un corto periodo."
//
//   · Cada golpe directo que recibe (OnTookDamage) le aplica StackEffect: un GE que se acumula
//     (Status_DefensiveTactics, velocidad y resistencia, con su tope y su duración).
//   · Con StacksToVanish acumulaciones recibe ReadyEffect (Status_TacticsReady): la Retirada del
//     Cazador tiene en su lista un "al activarse, el lanzador, solo si tiene Status_TacticsReady"
//     que lo vuelve invisible (GE_HunterVanish). Si las acumulaciones se vencen antes, se le saca.
//   · Cuando se vuelve invisible con eso listo, gasta las acumulaciones y el "listo".
//
// Se aplica en el Update y no en el aviso: el golpe llega desde adentro del recorrido de
// efectos del propio Cazador. Vive en HunterBehaviours. Solo en el servidor.
// ============================================================
public class DefensiveTacticsPassive : MonoBehaviour
{
    [Tooltip("Lo que da cada golpe recibido: se acumula (velocidad de movimiento y resistencia), con " +
             "su tope de acumulaciones y su duración. Da Status_DefensiveTactics por acumulación.")]
    [Label("Acumulación (GE)")]
    public GameplayEffect StackEffect;

    [Tooltip("Lo que recibe al llegar al máximo (Status_TacticsReady): la próxima Retirada lo vuelve " +
             "invisible.")]
    [Label("Listo para desaparecer (GE)")]
    public GameplayEffect ReadyEffect;

    [Tooltip("Cuántas acumulaciones hacen falta para que la Retirada lo vuelva invisible.")]
    [Label("Acumulaciones para desaparecer")]
    public int StacksToVanish = 3;

    private AbilitySystemComponent        _asc;
    private NetworkAbilitySystemComponent _netAsc;
    private int _pendingHits;

    private bool IsServer => _netAsc == null || _netAsc.IsServerInitialized;

    private void Awake()
    {
        _asc    = GetComponentInParent<AbilitySystemComponent>();
        _netAsc = _asc != null ? _asc.GetComponent<NetworkAbilitySystemComponent>() : null;
    }

    private void OnEnable()  { if (_asc != null) _asc.OnTookDamage += HandleTookDamage; }
    private void OnDisable() { if (_asc != null) _asc.OnTookDamage -= HandleTookDamage; }

    private void HandleTookDamage(AbilitySystemComponent attacker)
    {
        if (IsServer && attacker != null && attacker != _asc) _pendingHits++;
    }

    private void Update()
    {
        if (!IsServer || _asc == null) return;
        if (_asc.HasTag(EGameplayTag.State_Dead)) { _pendingHits = 0; return; }

        // Se volvió invisible con todo listo (la Retirada): gasta todo.
        bool ready = _asc.HasTag(EGameplayTag.Status_TacticsReady);
        if (ready && _asc.HasTag(EGameplayTag.Status_Invisible))
        {
            _asc.RemoveEffectsWithTag(EGameplayTag.Status_DefensiveTactics);
            if (ReadyEffect != null) _asc.RemoveEffectsByDefinition(ReadyEffect);
            _pendingHits = 0;
            return;
        }

        for (; _pendingHits > 0; _pendingHits--)
            if (StackEffect != null) _asc.ApplyGameplayEffect(StackEffect, _asc);

        int stacks = _asc.GetTagCount(EGameplayTag.Status_DefensiveTactics);
        if (stacks >= StacksToVanish && !ready && ReadyEffect != null) _asc.ApplyGameplayEffect(ReadyEffect, _asc);
        else if (stacks < StacksToVanish && ready && ReadyEffect != null) _asc.RemoveEffectsByDefinition(ReadyEffect);
    }
}
