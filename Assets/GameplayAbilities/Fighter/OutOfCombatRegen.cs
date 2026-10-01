using UnityEngine;

// ============================================================
// OutOfCombatRegen  (Segundo aliento, parte 1 — pasiva del Guerrero)
//
// "Regenera vida fuera de combate."
//
// Fuera de combate = pasaron OutOfCombatDelay segundos sin que le BAJE la vida. Desde
// ahí cura cada TickInterval, hasta llenarse o hasta que le vuelvan a pegar.
//
// CUÁNTO CURA (Mode):
//   · PercentOfMissing (el de Sett del LoL): un % de la vida que le FALTA. Con 5 %: a
//     un 10 % de vida cura 4.5 % de la máxima por tick; a media vida, 2.5 %; casi lleno,
//     casi nada. Mucho cuando está al borde, normalito el resto del tiempo.
//   · PercentOfMax: un % fijo de la vida máxima.
//   · Flat: puntos fijos.
//
// Se detecta "le pegaron" mirando si la vida BAJÓ, no por un evento de daño: así cuenta
// todo (golpes, quemaduras, caídas, zonas). Su propia curación sube la vida, no la baja.
// La curación pasa por HealingReceived (el Faro de esperanza la potencia) y saca su
// número verde.
//
// Vive en el PassiveBehaviorsPrefab de la clase. Solo actúa en el servidor.
// ============================================================
public class OutOfCombatRegen : MonoBehaviour
{
    public enum ERegenMode { PercentOfMissing, PercentOfMax, Flat }

    [Tooltip("Segundos sin que le baje la vida para contar como fuera de combate.")]
    public float OutOfCombatDelay = 3f;

    [Tooltip("Cada cuánto cura, ya fuera de combate.")]
    public float TickInterval = 1f;

    [Tooltip("Cómo se calcula cada curación (ver la cabecera del script).")]
    public ERegenMode Mode = ERegenMode.PercentOfMissing;

    [Tooltip("PercentOfMissing / PercentOfMax: fracción (0.05 = 5 %). Flat: puntos de vida.")]
    public float Amount = 0.05f;

    [Tooltip("Curación mínima por tick, para que casi lleno no salgan números de 0.")]
    public float MinPerTick = 1f;

    private AbilitySystemComponent        _asc;
    private NetworkAbilitySystemComponent _netAsc;

    private float _lastHealth  = -1f;
    private float _lastHurtAt  = -999f;
    private float _nextTickAt;

    private bool IsServer => _netAsc == null || _netAsc.IsServerInitialized;

    private void Awake()
    {
        _asc    = GetComponentInParent<AbilitySystemComponent>();
        _netAsc = _asc != null ? _asc.GetComponent<NetworkAbilitySystemComponent>() : null;
    }

    private void Update()
    {
        if (_asc == null || !IsServer) return;

        float health = _asc.GetAttributeValue(EAttributeType.Health);
        float max    = _asc.GetAttributeValue(EAttributeType.MaxHealth);

        // ¿Le bajó la vida desde el frame pasado? Entonces está en combate.
        if (_lastHealth >= 0f && health < _lastHealth - 0.01f) _lastHurtAt = Time.time;
        _lastHealth = health;

        if (_asc.HasTag(EGameplayTag.State_Dead) || max <= 0f || health >= max) return;
        if (Time.time - _lastHurtAt < OutOfCombatDelay) return;
        if (Time.time < _nextTickAt) return;
        _nextTickAt = Time.time + Mathf.Max(0.1f, TickInterval);

        float amount = Mode switch
        {
            ERegenMode.PercentOfMissing => (max - health) * Amount,
            ERegenMode.PercentOfMax     => max * Amount,
            _                           => Amount,
        };

        _lastHealth = SelfHeal.Apply(_asc, Mathf.Max(MinPerTick, amount));
    }
}

// Curación propia escrita directo (la de las pasivas del Guerrero): respeta
// HealingReceived, no pasa de la máxima y saca el número verde. Devuelve la vida nueva.
public static class SelfHeal
{
    public static float Apply(AbilitySystemComponent asc, float amount)
    {
        float health = asc.GetAttributeValue(EAttributeType.Health);
        float max    = asc.GetAttributeValue(EAttributeType.MaxHealth);

        amount *= Mathf.Max(0f, 1f + asc.GetAttributeValue(EAttributeType.HealingReceived));
        float healed = Mathf.Min(amount, max - health);
        if (healed <= 0f) return health;

        asc.SetCurrentAttributeValue(EAttributeType.Health, health + healed);
        asc.ShowCombatNumber(healed, ECombatNumber.Heal);
        return health + healed;
    }
}
