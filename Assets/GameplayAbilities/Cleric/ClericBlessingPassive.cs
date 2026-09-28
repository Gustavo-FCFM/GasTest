using UnityEngine;

// ============================================================
// ClericBlessingPassive  (Bendición — pasiva del Clérigo)
//
// "Los aliados a los que cura obtienen resistencia al daño durante un tiempo."
//
// No hace falta tocar cada curación del Clérigo (el arco, la curación apuntada, y las
// de las subclases que vengan): todas pasan por el pipeline de efectos, que ya avisa
// OnHealedAlly cuando ESTE personaje le sube la vida a un ALIADO (nunca a sí mismo, y
// sin contar lo que se pasaba del máximo). Esta pasiva escucha ese aviso y le aplica
// BlessingEffect al curado. Una curación que escriba la vida directo tiene que avisar
// con NotifyHealedAlly —como el aura del Paladín— y queda cubierta igual.
//
// Vive en el PassiveBehaviorsPrefab de la clase: se crea al equiparla y se destruye al
// cambiar. Solo actúa con autoridad de servidor.
// ============================================================
public class ClericBlessingPassive : MonoBehaviour
{
    [Tooltip("Lo que recibe el aliado curado: la resistencia con duración (GE_Blessing). " +
             "Con política Refresh, curar seguido lo mantiene, no lo apila.")]
    public GameplayEffect BlessingEffect;

    [Tooltip("Curación mínima para bendecir: el goteo de 1 punto de un efecto con el tiempo " +
             "no tiene por qué renovarla en cada tick. 0 = cualquier curación.")]
    public float MinHealToBless = 1f;

    private AbilitySystemComponent        _asc;
    private NetworkAbilitySystemComponent _netAsc;

    private bool IsServer => _netAsc == null || _netAsc.IsServerInitialized;

    private void Awake()
    {
        // El ASC vive en el jugador; este componente es un hijo suyo.
        _asc    = GetComponentInParent<AbilitySystemComponent>();
        _netAsc = _asc != null ? _asc.GetComponent<NetworkAbilitySystemComponent>() : null;
    }

    private void OnEnable()
    {
        if (_asc != null) _asc.OnHealedAlly += HandleHealedAlly;
    }

    private void OnDisable()
    {
        if (_asc != null) _asc.OnHealedAlly -= HandleHealedAlly;
    }

    // Se anota y se aplica en el Update, no en el aviso mismo: una curación con el
    // tiempo avisa DESDE adentro del recorrido de los efectos activos del curado, y
    // agregarle un efecto ahí rompería esa iteración (la misma razón por la que el ASC
    // no rompe la invisibilidad en los ticks).
    private readonly System.Collections.Generic.HashSet<AbilitySystemComponent> _pending =
        new System.Collections.Generic.HashSet<AbilitySystemComponent>();

    private void HandleHealedAlly(AbilitySystemComponent ally, float healed)
    {
        if (!IsServer || ally == null || BlessingEffect == null) return;
        if (healed < MinHealToBless) return;

        _pending.Add(ally);
    }

    private void Update()
    {
        if (_pending.Count == 0) return;

        foreach (AbilitySystemComponent ally in _pending)
            if (ally != null && !ally.HasTag(EGameplayTag.State_Dead))
                ally.ApplyGameplayEffect(BlessingEffect, _asc);

        _pending.Clear();
    }
}
