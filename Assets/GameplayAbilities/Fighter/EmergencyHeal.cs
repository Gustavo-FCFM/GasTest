using UnityEngine;
using System.Collections;

// ============================================================
// EmergencyHeal  (Segundo aliento, parte 2 — pasiva del Guerrero)
//
// "Al bajar del 30 % de vida regenera hasta un 80 % de su vida máxima a lo largo de un
// corto periodo."
//
// Cuando la vida baja de HealthThreshold, calcula HealPercentOfMax de la vida máxima EN
// ESE MOMENTO y la reparte en Ticks curaciones iguales a lo largo de Duration segundos.
// No cura de golpe a propósito: mientras se recupera, todavía lo pueden matar.
//
// El primer tick sale en el acto (para que se note que saltó la pasiva); los demás, uno
// cada Duration / Ticks. Se corta si muere. Después espera Cooldown para poder volver a
// saltar.
//
// Vive en el PassiveBehaviorsPrefab de la clase. Solo actúa en el servidor.
// ============================================================
public class EmergencyHeal : MonoBehaviour
{
    [Tooltip("Fracción de la vida por debajo de la cual salta. 0.3 = al bajar del 30 %.")]
    [Range(0.01f, 1f)]
    public float HealthThreshold = 0.3f;

    [Tooltip("Cuánto cura en total, como fracción de la vida máxima del momento en que salta. " +
             "0.8 = un 80 %.")]
    public float HealPercentOfMax = 0.8f;

    [Tooltip("En cuántas curaciones iguales se reparte.")]
    [Min(1)]
    public int Ticks = 5;

    [Tooltip("Segundos en los que se reparten las curaciones.")]
    public float Duration = 5f;

    [Tooltip("Segundos, desde que salta, hasta que puede volver a saltar.")]
    public float Cooldown = 60f;

    private AbilitySystemComponent        _asc;
    private NetworkAbilitySystemComponent _netAsc;

    private bool  _running;
    private float _readyAt;

    private bool IsServer => _netAsc == null || _netAsc.IsServerInitialized;

    private void Awake()
    {
        _asc    = GetComponentInParent<AbilitySystemComponent>();
        _netAsc = _asc != null ? _asc.GetComponent<NetworkAbilitySystemComponent>() : null;
    }

    private void OnDisable()
    {
        _running = false;   // la corrutina muere con el componente
    }

    private void Update()
    {
        if (_asc == null || !IsServer || _running || Time.time < _readyAt) return;
        if (_asc.HasTag(EGameplayTag.State_Dead)) return;

        float health = _asc.GetAttributeValue(EAttributeType.Health);
        float max    = _asc.GetAttributeValue(EAttributeType.MaxHealth);
        if (max <= 0f || health <= 0f || health / max >= HealthThreshold) return;

        _readyAt = Time.time + Cooldown;
        StartCoroutine(HealOverTime(max * HealPercentOfMax));
    }

    private IEnumerator HealOverTime(float total)
    {
        _running = true;

        int   ticks    = Mathf.Max(1, Ticks);
        float perTick  = total / ticks;
        float interval = ticks > 1 ? Duration / ticks : 0f;

        for (int i = 0; i < ticks; i++)
        {
            if (i > 0 && interval > 0f) yield return new WaitForSeconds(interval);
            if (_asc == null || _asc.HasTag(EGameplayTag.State_Dead)) break;

            SelfHeal.Apply(_asc, perTick);
        }

        _running = false;
    }
}
