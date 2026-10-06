using UnityEngine;

// ============================================================
// MartialArtsPassive  (Artes marciales — pasiva del Monje)
//
// "Cada golpe que impacta incrementa su velocidad de ataque, hasta un límite."
//
// Escucha OnDealtDamage del dueño (golpes directos, no ticks) y se aplica StackEffect a
// sí mismo: un GE con Stack, MaxStacks y duración (GE_MartialArts: 5 % de velocidad de
// ataque por acumulación, hasta 4, 8 s). El reloj de las acumulaciones es compartido
// (ver ApplyGameplayEffect): seguir pegando las mantiene vivas a todas.
//
// UNA acumulación por ATAQUE, no por enemigo: un puñetazo que alcanza a tres suma una
// sola. Se resuelve en el Update (igual que el Punto débil) para no agregar un efecto
// desde adentro del recorrido de efectos del golpeado.
//
// La Ráfaga de golpes (el Disparo con Ki) se pone las cuatro de una antes de pegar: es
// un GA_SelfBuff con este mismo GE cuatro veces.
//
// Vive en el PassiveBehaviorsPrefab del Monje (MonkBehaviours). Solo actúa en el servidor.
// ============================================================
public class MartialArtsPassive : MonoBehaviour
{
    [Tooltip("El GE que se aplica a sí mismo por cada ataque que pega (GE_MartialArts: Stack, " +
             "MaxStacks 4, 8 s, AtkSpeed × 0.95).")]
    public GameplayEffect StackEffect;

    private AbilitySystemComponent        _asc;
    private NetworkAbilitySystemComponent _netAsc;
    private bool _pending;

    private bool IsServer => _netAsc == null || _netAsc.IsServerInitialized;

    private void Awake()
    {
        _asc    = GetComponentInParent<AbilitySystemComponent>();
        _netAsc = _asc != null ? _asc.GetComponent<NetworkAbilitySystemComponent>() : null;
    }

    private void OnEnable()  { if (_asc != null) _asc.OnDealtDamage += HandleDealtDamage; }
    private void OnDisable() { if (_asc != null) _asc.OnDealtDamage -= HandleDealtDamage; }

    private void HandleDealtDamage(AbilitySystemComponent victim, float damage)
    {
        if (!IsServer || victim == null || damage <= 0f || ReferenceEquals(victim, _asc)) return;
        _pending = true;
    }

    private void Update()
    {
        if (!_pending) return;
        _pending = false;

        if (StackEffect != null && _asc != null && !_asc.HasTag(EGameplayTag.State_Dead))
            _asc.ApplyGameplayEffect(StackEffect, _asc);
    }
}
