using System.Collections.Generic;
using UnityEngine;

// ============================================================
// DefensiveDancePassive  (Danza defensiva — pasiva del Filo danzante, 23 de octubre de 2026)
//
// "+3 % de resistencia por punto arcano en los enemigos a 8 m o menos, tope 30 %." Cada tanto suma
// los puntos arcanos (Status_ArcanePoint) de los enemigos vivos a su alcance y deja al Mago con
// tantas acumulaciones de GE_DefensiveDance como haga falta (cada una: +3 % de resistencia y el
// tag Status_DefensiveDance, con el que se cuentan): pone las que faltan y saca las que sobran. Se
// ve como un buff con su número en la barra.
//
// Vive en el PassiveBehaviorsPrefab del Filo danzante (BladeBehaviours, junto con lo del Mago base)
// y corre en el SERVIDOR. Como vive en un hijo del jugador, busca el ASC en el PADRE.
// ============================================================
public class DefensiveDancePassive : MonoBehaviour
{
    [Tooltip("Una acumulación: +3 % de resistencia y el tag de abajo. Se acumula hasta el tope " +
             "(StackingPolicy Stack, MaxStacks 10) y dura mucho: las pone y las saca esta pasiva.")]
    [Label("Efecto por acumulación")]
    public GameplayEffect DanceEffect;

    [Tooltip("El tag que da cada acumulación (con el que se cuentan y se sacan de a una).")]
    [Label("Tag de las acumulaciones")]
    public EGameplayTag DanceTag = EGameplayTag.Status_DefensiveDance;

    [Tooltip("El tag de los puntos de los enemigos (los puntos arcanos).")]
    [Label("Tag de los puntos")]
    public EGameplayTag PointTag = EGameplayTag.Status_ArcanePoint;

    [Tooltip("Hasta dónde mira enemigos.")]
    [Label("Alcance (m)")]
    public float Range = 8f;

    [Tooltip("Acumulaciones por punto (1 = +3 % por punto).")]
    [Label("Acumulaciones por punto")]
    public int StacksPerPoint = 1;

    [Tooltip("Tope de acumulaciones (10 × 3 % = 30 %).")]
    [Label("Tope de acumulaciones")]
    public int MaxStacks = 10;

    [Tooltip("Cada cuánto recuenta.")]
    [Label("Recontar cada (s)")]
    public float Interval = 0.25f;

    [Tooltip("Capa de los personajes, donde buscar enemigos.")]
    [Label("Capa de personajes")]
    public LayerMask CharacterLayer = 1 << 7;

    private AbilitySystemComponent        _asc;
    private NetworkAbilitySystemComponent _netAsc;
    private float _timer;

    private readonly HashSet<AbilitySystemComponent> _seen = new HashSet<AbilitySystemComponent>();

    private bool IsServer => _netAsc == null || _netAsc.IsServerInitialized;

    private void Awake()
    {
        _asc    = GetComponentInParent<AbilitySystemComponent>();
        _netAsc = _asc != null ? _asc.GetComponent<NetworkAbilitySystemComponent>() : null;
    }

    private void OnDisable()
    {
        if (_asc != null && IsServer && DanceTag != EGameplayTag.None) _asc.RemoveEffectsWithTag(DanceTag);
    }

    private void Update()
    {
        if (!IsServer || _asc == null || DanceEffect == null) return;

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = Mathf.Max(0.05f, Interval);

        int wanted = _asc.HasTag(EGameplayTag.State_Dead) ? 0 : Mathf.Min(MaxStacks, PointsNearby() * StacksPerPoint);
        int current = _asc.GetTagCount(DanceTag);

        for (int i = current; i < wanted; i++) _asc.ApplyGameplayEffect(DanceEffect, _asc);
        for (int i = wanted; i < current; i++) _asc.RemoveOneEffectWithTag(DanceTag);
    }

    // Los puntos arcanos de los enemigos vivos a su alcance, sumados.
    private int PointsNearby()
    {
        _seen.Clear();
        int points = 0;
        foreach (Collider col in Physics.OverlapSphere(_asc.transform.position, Range, CharacterLayer,
                                                       QueryTriggerInteraction.Collide))
        {
            AbilitySystemComponent other = col.GetComponentInParent<AbilitySystemComponent>();
            if (other == null || other == _asc || !_seen.Add(other)) continue;
            if (other.HasTag(EGameplayTag.State_Dead) || !_asc.IsEnemyOf(other)) continue;
            points += other.GetTagCount(PointTag);
        }
        return points;
    }
}
