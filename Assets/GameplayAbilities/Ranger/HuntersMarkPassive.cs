using UnityEngine;
using System.Collections.Generic;

// ============================================================
// HuntersMarkPassive  (Marca del cazador — pasiva del Explorador, 13 de octubre de 2026)
//
// "Cada impacto marca la posición del enemigo, volviéndolo visible para el jugador y sus
// aliados incluso a través de paredes, y haciéndole daño adicional por un corto periodo."
//
// Escucha OnDealtDamage del dueño (lo dispara el pipeline de daño cuando su golpe le baja
// la vida a alguien) y al golpeado:
//   · le aplica MarkEffect: un GE con Vulnerability y duración (el daño adicional, y el
//     ícono para que el marcado sepa que lo marcaron). Con Refresh, pegarle seguido lo
//     mantiene marcado.
//   · lo REVELA al equipo del dueño por lo mismo que dura la marca: el Explorador y sus
//     aliados ven su silueta a través de las paredes (TeamReveal). El equipo del marcado y
//     un tercer equipo no.
//
// Se aplica en el Update y no en el aviso mismo, como el Punto débil: el aviso llega desde
// adentro del recorrido de efectos del golpeado, y agregarle uno ahí rompería esa iteración.
//
// Vive en el PassiveBehaviorsPrefab del Explorador (y de sus subclases). Solo en el servidor.
// ============================================================
public class HuntersMarkPassive : MonoBehaviour
{
    [Tooltip("La marca (GE_HuntersMark): Vulnerability con duración y Refresh.")]
    [Label("Efecto de la marca")]
    public GameplayEffect MarkEffect;

    [Tooltip("Segundos que su equipo lo ve a través de las paredes. 0 = lo que dure la marca.")]
    [Label("Revelado (s)")]
    public float RevealSeconds = 0f;

    private AbilitySystemComponent        _asc;
    private NetworkAbilitySystemComponent _netAsc;

    private readonly HashSet<AbilitySystemComponent> _pending = new HashSet<AbilitySystemComponent>();

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
        _pending.Add(victim);
    }

    private void Update()
    {
        if (_pending.Count == 0) return;

        float reveal = RevealSeconds > 0f ? RevealSeconds : (MarkEffect != null ? MarkEffect.Duration : 0f);

        foreach (AbilitySystemComponent victim in _pending)
        {
            if (victim == null || victim.HasTag(EGameplayTag.State_Dead)) continue;
            if (MarkEffect != null) victim.ApplyGameplayEffect(MarkEffect, _asc);

            NetworkAbilitySystemComponent victimNet = victim.GetComponent<NetworkAbilitySystemComponent>();
            if (victimNet != null && victimNet.IsServerInitialized && _asc != null)
                victimNet.ServerRevealToTeam(_asc.TeamID, reveal);
        }
        _pending.Clear();
    }
}
