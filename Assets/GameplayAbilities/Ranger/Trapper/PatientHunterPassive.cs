using System.Collections.Generic;
using UnityEngine;

// ============================================================
// PatientHunterPassive  (Cazador paciente — pasiva del Trampero, 15 de octubre de 2026)
//
// "Tras un tiempo sin recibir daño se vuelve invisible, aumenta su velocidad de movimiento y
// regenera vida lentamente. El primer ataque al salir de invisibilidad es un crítico asegurado."
//
// Mira cuándo fue la última vez que el Trampero pegó o le pegaron (OnDealtDamage / OnTookDamage).
// Pasados "Sin pelear durante" segundos le aplica los efectos del sigilo: GEs largos que llevan
// TODOS Status_Invisible (el sigilo con la velocidad y Status_GuaranteedCrit; la curación por
// segundo, aparte, porque un efecto periódico aplica sus cambios en cada tick). No hace falta
// sacárselos: la invisibilidad se rompe sola al atacar o al recibir daño
// (AbilitySystemComponent.BreakInvisibility saca todo efecto con Status_Invisible), y el crítico
// asegurado se gasta en ese mismo golpe.
//
// Vive en TrapperBehaviours. Solo en el servidor.
// ============================================================
public class PatientHunterPassive : MonoBehaviour
{
    [Tooltip("Segundos sin pegar ni recibir daño para volverse invisible.")]
    [Label("Sin pelear durante (s)")]
    public float OutOfCombatSeconds = 4f;

    [Tooltip("Los efectos del sigilo, largos: TODOS con Status_Invisible, así se cortan juntos al pelear " +
             "(el sigilo con la velocidad y el crítico asegurado, y la curación por segundo).")]
    [Label("Efectos del sigilo")]
    public List<GameplayEffect> StealthEffects = new List<GameplayEffect>();

    private AbilitySystemComponent        _asc;
    private NetworkAbilitySystemComponent _netAsc;
    private float _lastCombat;

    private bool IsServer => _netAsc == null || _netAsc.IsServerInitialized;

    private void Awake()
    {
        _asc    = GetComponentInParent<AbilitySystemComponent>();
        _netAsc = _asc != null ? _asc.GetComponent<NetworkAbilitySystemComponent>() : null;
    }

    private void OnEnable()
    {
        _lastCombat = Time.time;
        if (_asc == null) return;
        _asc.OnDealtDamage += HandleDealtDamage;
        _asc.OnTookDamage  += HandleTookDamage;
    }

    private void OnDisable()
    {
        if (_asc == null) return;
        _asc.OnDealtDamage -= HandleDealtDamage;
        _asc.OnTookDamage  -= HandleTookDamage;
    }

    private void HandleDealtDamage(AbilitySystemComponent victim, float damage) => _lastCombat = Time.time;
    private void HandleTookDamage(AbilitySystemComponent attacker)             => _lastCombat = Time.time;

    private void Update()
    {
        if (!IsServer || _asc == null || StealthEffects.Count == 0) return;

        if (_asc.HasTag(EGameplayTag.State_Dead)) { _lastCombat = Time.time; return; }
        if (_asc.HasTag(EGameplayTag.Status_Invisible)) return;
        if (Time.time - _lastCombat < OutOfCombatSeconds) return;

        foreach (GameplayEffect e in StealthEffects) if (e != null) _asc.ApplyGameplayEffect(e, _asc);
    }
}
