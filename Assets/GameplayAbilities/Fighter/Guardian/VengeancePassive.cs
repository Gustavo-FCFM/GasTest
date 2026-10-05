using UnityEngine;

// ============================================================
// VengeancePassive  (pasiva extra del Guardián — Venganza)
//
// Cuanta menos vida tiene, más resistente es: resistencia al daño Y al control que crece
// con la vida que le FALTA. Con la vida llena, nada; a la mitad de la vida llega al
// máximo (30 %) y ahí se queda aunque siga bajando.
//
// Se reparte en acumulaciones de StackEffect (cada una suma su parte a Resistance y a
// CCResistance), así el valor viaja solo y se ve en la barra de efectos con su número:
// 10 acumulaciones de 3 % = 30 %. Solo se tocan cuando cambia la cantidad.
//
// Va en el prefab de pasivas del Guardián. Corre en el SERVIDOR.
// ============================================================
public class VengeancePassive : MonoBehaviour
{
    [Tooltip("El efecto de UNA acumulación (Stack, MaxStacks = Max Stacks, con duración larga).")]
    public GameplayEffect StackEffect;

    [Tooltip("Acumulaciones con el máximo de Venganza (10 × 3 % = 30 %).")]
    public int MaxStacks = 10;

    [Tooltip("Fracción de la vida a la que se llega al máximo (0.5 = a la mitad).")]
    [Range(0.05f, 1f)]
    public float FullAtHealthFraction = 0.5f;

    [Tooltip("Cada cuánto se revisa la vida.")]
    public float TickInterval = 0.2f;

    private AbilitySystemComponent        _asc;
    private NetworkAbilitySystemComponent _netAsc;
    private float _timer;
    private int   _applied;

    private bool IsServer => _netAsc == null || _netAsc.IsServerInitialized;

    private void Awake()
    {
        _asc    = GetComponentInParent<AbilitySystemComponent>();
        _netAsc = _asc != null ? _asc.GetComponent<NetworkAbilitySystemComponent>() : null;
    }

    // Al cambiar de clase el prefab se destruye: no se lleva la Venganza puesta.
    private void OnDisable()
    {
        if (_asc != null && StackEffect != null && IsServer) _asc.RemoveEffectsByDefinition(StackEffect);
        _applied = 0;
    }

    private void Update()
    {
        if (!IsServer || _asc == null || StackEffect == null) return;

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = Mathf.Max(0.05f, TickInterval);

        int wanted = WantedStacks();

        // Lo que hay de verdad (al morir se limpian los buffs: hay que volver a ponerlos).
        int current = CountStacks();
        if (current == wanted && _applied == wanted) return;

        _asc.RemoveEffectsByDefinition(StackEffect);
        for (int i = 0; i < wanted; i++) _asc.ApplyGameplayEffect(StackEffect, _asc);
        _applied = wanted;
    }

    private int WantedStacks()
    {
        if (_asc.HasTag(EGameplayTag.State_Dead)) return 0;

        float max = _asc.GetAttributeValue(EAttributeType.MaxHealth);
        if (max <= 0f) return 0;

        float missing = 1f - Mathf.Clamp01(_asc.GetAttributeValue(EAttributeType.Health) / max);
        float t = Mathf.Clamp01(missing / Mathf.Max(0.01f, 1f - FullAtHealthFraction));
        return Mathf.FloorToInt(t * MaxStacks + 0.0001f);
    }

    private int CountStacks()
    {
        int n = 0;
        foreach (var e in _asc.GetActiveEffects())
            if (e.Definition == StackEffect) n++;
        return n;
    }
}
