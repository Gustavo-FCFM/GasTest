using System.Collections.Generic;
using UnityEngine;

// ============================================================
// HunterPreyPassive  (Cazador — subclase del Explorador, 15 de octubre de 2026)
//
// Lleva las PRESAS del Cazador y lo que le dan, según el ROL del marcado (pedido de Gustavo):
//
//   · Tanque  → sus golpes le hacen además un % de su vida MÁXIMA (Tanque: daño extra).
//   · Daño    → sus golpes le hacen además un % de su vida FALTANTE (Daño: daño extra).
//
// SOLO JUGADORES (y bots), nunca NPCs (pedido de Gustavo, 15 de octubre): ni la Presa, ni la
// cacería al matarla, ni el Depredador definitivo marcan o benefician contra un monstruo.
//   · Soporte → recibe menos curación mientras dure la marca (SupportEffect).
//
// La marca la pone la habilidad Presa (GA_Prey, la E) con MarkPrey. Si el Cazador MATA a su
// presa mientras está marcada, durante HuntSeconds ese mismo beneficio vale contra TODOS los
// enemigos (y recibe HuntEffect, que se ve en su barra).
//
// DEPREDADOR DEFINITIVO (la R): mientras el Cazador tiene Status_ApexPredator (GE_ApexPredator),
// todos los enemigos son su presa (cada uno según su rol), llevan la Marca del cazador y su
// equipo los ve a través de las paredes. Cada baja alarga la definitiva ApexSecondsPerKill.
//
// El daño extra va por el pipeline de daño (IDamageModifier: el ASC le pregunta en cada golpe
// del dueño). Los efectos se aplican en el Update y no en los avisos: el aviso de una baja o
// de un golpe llega desde adentro del recorrido de efectos de la víctima, y aplicarle otro ahí
// rompería esa iteración (igual que el Punto débil y la Marca del cazador).
//
// Vive en el PassiveBehaviorsPrefab del Cazador (HunterBehaviours). Solo en el servidor.
// ============================================================
public class HunterPreyPassive : MonoBehaviour, IDamageModifier
{
    [Header("La marca")]
    [Tooltip("El GE que lleva el marcado (Status_Prey, el ícono de presa). Su duración la pone la " +
             "habilidad que marca.")]
    [Label("Marca de presa (GE)")]
    public GameplayEffect PreyEffect;

    [Tooltip("Lo que recibe una presa de rol Soporte: menos curación recibida.")]
    [Label("Soporte: menos curación (GE)")]
    public GameplayEffect SupportEffect;

    [Header("Beneficio según el rol")]
    [Tooltip("Contra una presa Tanque, cada golpe le hace además esta parte de su vida MÁXIMA. " +
             "0.02 = el 2 %.")]
    [Label("Tanque: daño extra (0 a 1 de su vida máx.)")]
    public float TankBonusOfMaxHealth = 0.02f;

    [Tooltip("Contra una presa Daño (o un NPC), cada golpe le hace además esta parte de la vida " +
             "que le FALTA. 0.03 = el 3 %.")]
    [Label("Daño: daño extra (0 a 1 de su vida faltante)")]
    public float DamageBonusOfMissingHealth = 0.03f;

    [Header("Cazar a la presa")]
    [Tooltip("Si mata a su presa mientras está marcada, el beneficio de su rol vale contra todos " +
             "los enemigos durante estos segundos.")]
    [Label("Al cazarla: contra todos (s)")]
    public float HuntSeconds = 6f;

    [Tooltip("Lo que recibe el Cazador al cazarla (solo para que se vea en su barra).")]
    [Label("Al cazarla: efecto (GE)")]
    public GameplayEffect HuntEffect;

    [Header("Depredador definitivo")]
    [Tooltip("El GE de la definitiva (GE_ApexPredator, con Status_ApexPredator). Con él se lee " +
             "cuánto le queda y se alarga con cada baja.")]
    [Label("Definitiva (GE)")]
    public GameplayEffect ApexEffect;

    [Tooltip("Segundos que suma cada baja mientras dura la definitiva.")]
    [Label("Definitiva: cada baja suma (s)")]
    public float ApexSecondsPerKill = 3f;

    private AbilitySystemComponent        _asc;
    private NetworkAbilitySystemComponent _netAsc;
    private HuntersMarkPassive            _huntersMark;

    private struct Prey { public EClassRole Role; public float Until; }
    private readonly Dictionary<AbilitySystemComponent, Prey> _prey = new Dictionary<AbilitySystemComponent, Prey>();

    private EClassRole _huntRole;
    private float      _huntUntil;

    private readonly List<AbilitySystemComponent>    _killed     = new List<AbilitySystemComponent>();
    private readonly HashSet<AbilitySystemComponent> _cutHealing = new HashSet<AbilitySystemComponent>();
    private float _nextApexScan;

    private bool IsServer => _netAsc == null || _netAsc.IsServerInitialized;

    private void Awake()
    {
        _asc    = GetComponentInParent<AbilitySystemComponent>();
        _netAsc = _asc != null ? _asc.GetComponent<NetworkAbilitySystemComponent>() : null;
        _huntersMark = _asc != null ? _asc.GetComponentInChildren<HuntersMarkPassive>(true) : null;
    }

    private void OnEnable()
    {
        if (_asc == null) return;
        _asc.RegisterDamageModifier(this);
        _asc.OnKilledTarget += HandleKilledTarget;
    }

    private void OnDisable()
    {
        if (_asc == null) return;
        _asc.UnregisterDamageModifier(this);
        _asc.OnKilledTarget -= HandleKilledTarget;
        _prey.Clear();
    }

    // =========================================================
    // MARCAR (lo llama GA_Prey en el servidor, y la definitiva)
    // =========================================================

    public void MarkPrey(AbilitySystemComponent target, float seconds)
    {
        if (!IsServer || target == null || seconds <= 0f || !IsPlayer(target)) return;

        EClassRole role = RoleOf(target);
        _prey[target] = new Prey { Role = role, Until = Time.time + seconds };

        if (PreyEffect != null) target.ApplyGameplayEffect(PreyEffect, _asc, seconds);
        if (role == EClassRole.Support && SupportEffect != null) target.ApplyGameplayEffect(SupportEffect, _asc, seconds);
    }

    // Un jugador o un bot (los dos tienen PlayerController); un NPC no.
    public static bool IsPlayer(AbilitySystemComponent asc) => asc != null && asc.GetComponent<PlayerController>() != null;

    // El rol del marcado: el de su clase (una clase base se ve con el de su rama; sin rol, Daño).
    private static EClassRole RoleOf(AbilitySystemComponent target)
    {
        EClassRole role = target.CurrentClass != null ? target.CurrentClass.DisplayRole : EClassRole.None;
        return role == EClassRole.None ? EClassRole.Damage : role;
    }

    // =========================================================
    // EL DAÑO EXTRA (en cada golpe del Cazador)
    // =========================================================

    public void ModifyOutgoingDamage(ref DamageContext ctx)
    {
        if (ctx.IsPeriodicTick || ctx.Magnitude >= 0f || ctx.Target == null) return;
        if (!TryGetRole(ctx.Target, out EClassRole role)) return;

        switch (role)
        {
            case EClassRole.Tank:
                ctx.Magnitude -= ctx.Target.GetAttributeValue(EAttributeType.MaxHealth) * TankBonusOfMaxHealth;
                break;
            case EClassRole.Damage:
                float missing = ctx.Target.GetAttributeValue(EAttributeType.MaxHealth) -
                                ctx.Target.GetAttributeValue(EAttributeType.Health);
                ctx.Magnitude -= Mathf.Max(0f, missing) * DamageBonusOfMissingHealth;
                break;
            case EClassRole.Support:
                // Cazando (contra todos): el golpeado recibe menos curación. Se aplica en el Update.
                if (!IsPrey(ctx.Target)) _cutHealing.Add(ctx.Target);
                break;
        }
    }

    // ¿Qué beneficio vale contra este enemigo? El de su marca, o el de la cacería si está activa.
    private bool TryGetRole(AbilitySystemComponent target, out EClassRole role)
    {
        if (_prey.TryGetValue(target, out Prey p) && p.Until > Time.time) { role = p.Role; return true; }
        if (_huntUntil > Time.time && _asc != null && _asc.IsEnemyOf(target) && IsPlayer(target)) { role = _huntRole; return true; }
        role = EClassRole.None;
        return false;
    }

    private bool IsPrey(AbilitySystemComponent target)
        => _prey.TryGetValue(target, out Prey p) && p.Until > Time.time;

    // =========================================================
    // BAJAS Y DEFINITIVA
    // =========================================================

    private void HandleKilledTarget(AbilitySystemComponent victim)
    {
        if (IsServer && victim != null) _killed.Add(victim);
    }

    private void Update()
    {
        if (!IsServer || _asc == null) return;

        bool apex = _asc.HasTag(EGameplayTag.Status_ApexPredator);

        // Bajas: cazar a la presa, y alargar la definitiva.
        if (_killed.Count > 0)
        {
            foreach (AbilitySystemComponent victim in _killed)
            {
                if (victim != null && _prey.TryGetValue(victim, out Prey p) && p.Until > Time.time)
                {
                    _huntRole  = p.Role;
                    _huntUntil = Time.time + HuntSeconds;
                    if (HuntEffect != null) _asc.ApplyGameplayEffect(HuntEffect, _asc, HuntSeconds);
                }
                if (apex) ExtendApex();
            }
            _killed.Clear();
            _nextApexScan = 0f;   // re-marcar a todos con el tiempo nuevo
        }

        // Cazando contra todos a un Soporte: menos curación.
        if (_cutHealing.Count > 0)
        {
            float left = _huntUntil - Time.time;
            if (SupportEffect != null && left > 0f)
                foreach (AbilitySystemComponent t in _cutHealing)
                    if (t != null && !t.HasTag(EGameplayTag.State_Dead)) t.ApplyGameplayEffect(SupportEffect, _asc, left);
            _cutHealing.Clear();
        }

        if (apex && Time.time >= _nextApexScan)
        {
            _nextApexScan = Time.time + 0.5f;
            MarkEveryone(ApexRemaining());
        }
    }

    // Lo que le queda a la definitiva (del GE activo en el Cazador).
    private float ApexRemaining()
    {
        foreach (ActiveGameplayEffect e in _asc.GetActiveEffects())
            if (e != null && e.Definition == ApexEffect) return Mathf.Max(0f, e.DurationRemaining);
        return 0f;
    }

    // Una baja durante la definitiva la alarga: se vuelve a aplicar con lo que quedaba más lo que suma.
    private void ExtendApex()
    {
        if (ApexEffect == null) return;
        _asc.ApplyGameplayEffect(ApexEffect, _asc, ApexRemaining() + ApexSecondsPerKill);
    }

    // Todos los enemigos (jugadores y bots, no NPCs) son presa hasta que termine la definitiva, con la
    // Marca del cazador y revelados a su equipo. Se repasa cada medio segundo: así entran los que
    // reaparecen, y una baja que alarga la definitiva alarga también las marcas.
    private void MarkEveryone(float seconds)
    {
        if (seconds <= 0.05f) return;
        float until = Time.time + seconds;

        foreach (AbilitySystemComponent other in FindObjectsByType<AbilitySystemComponent>(FindObjectsSortMode.None))
        {
            if (other == null || other == _asc || other.HasTag(EGameplayTag.State_Dead)) continue;
            if (!_asc.IsEnemyOf(other)) continue;
            if (!IsPlayer(other)) continue;   // solo jugadores y bots

            // Ya marcado hasta el final: nada que hacer.
            if (_prey.TryGetValue(other, out Prey p) && p.Until >= until - 0.25f) continue;

            MarkPrey(other, seconds);
            if (_huntersMark != null && _huntersMark.MarkEffect != null)
                other.ApplyGameplayEffect(_huntersMark.MarkEffect, _asc, seconds);

            NetworkAbilitySystemComponent otherNet = other.GetComponent<NetworkAbilitySystemComponent>();
            if (otherNet != null && otherNet.IsServerInitialized) otherNet.ServerRevealToTeam(_asc.TeamID, seconds);
        }
    }
}
