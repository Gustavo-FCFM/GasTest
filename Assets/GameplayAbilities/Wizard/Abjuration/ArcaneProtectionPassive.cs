using System.Collections.Generic;
using UnityEngine;

// ============================================================
// ArcaneProtectionPassive  (Protección arcana y las proyecciones — la Abjuración, 22 de octubre de 2026)
//
// Vive en el PassiveBehaviorsPrefab de la Abjuración (AbjurationBehaviours, junto con lo del Mago
// base) y corre en el SERVIDOR. Lleva tres cosas:
//
//   · LAS PROYECCIONES. La E (GA_ArcaneProjection) proyecta al Mago sobre UN aliado: le pone el
//     efecto de la proyección (tag, ícono y VFX, refrescado acá) y un escudo inicial del 20 % de la
//     vida máxima del Mago. Dura hasta que muere el Mago, muere el aliado o el aliado se queda sin
//     ese escudo; relanzarla sobre otro la mueve. La R (Protección máxima: su tag en el Mago, 8 s)
//     proyecta además a TODOS los aliados que estén a 25 m en algún momento de esos 8 s, cada uno
//     con su escudo inicial (una vez por R); al terminar, esas se van (salvo la de la E).
//   · LAS COPIAS (IAbilityEchoSource). Cada clic izquierdo del Mago sale además desde cada aliado
//     proyectado a 25 m o menos, hacia donde apunta el Mago, con el 50 % del daño (ver AbilityEcho).
//   · LA PROTECCIÓN ARCANA. Al hacer daño, el Mago y sus proyectados ganan escudo: el 25 % del daño
//     hecho, hasta la mitad de la vida máxima del Mago. Si deja de hacer daño, a los 3 s ese extra
//     empieza a bajar (10 % del tope por segundo) hasta 0; cada golpe reinicia la cuenta. El escudo
//     inicial de la proyección NO baja solo.
//   · LO QUE SE VE. Quien tiene escudo nuestro (el Mago y sus proyectados) lleva el efecto del escudo
//     (GE_ArcaneShield: su ícono y la burbuja en su TargetVFX), y el proyectado además el de la
//     proyección. Los VFX nacen de esos GE, no de un componente aparte.
//
// EL ESCUDO es el de siempre (el atributo Shield, un solo pozo): acá se lleva la cuenta de cuánto
// puso cada parte. Si el escudo real baja (golpes), se descuenta primero lo extra y después lo de
// la proyección; si otro escudo (un Paladín) se suma, no se toca. Al terminar una proyección se le
// quita lo que le quedaba de lo nuestro.
//
// El escudo que les da a los aliados cuenta como "curar a un aliado" para la definitiva del rol
// Soporte (NotifyHealedAlly).
// ============================================================
public class ArcaneProtectionPassive : MonoBehaviour, IAbilityEchoSource
{
    [Header("Protección arcana (la pasiva)")]
    [Tooltip("Qué parte del daño hecho se vuelve escudo, para el Mago y para cada proyectado. " +
             "0.25 = un golpe de 40 da 10 de escudo a cada uno.")]
    [Range(0f, 2f)]
    [Label("Escudo por daño hecho (0 a 1)")]
    public float ShieldPerDamage = 0.25f;

    [Tooltip("Tope del escudo que da la pasiva a cada uno, como fracción de la vida MÁXIMA del Mago " +
             "(0.5 = con 140 de vida, 70). En los proyectados cuenta también su escudo inicial.")]
    [Label("Tope del escudo (× vida máx. del Mago)")]
    public float ShieldCap = 0.5f;

    [Tooltip("Segundos sin hacer daño hasta que lo extra empieza a bajar. Cada golpe reinicia la cuenta.")]
    [Label("Empieza a bajar a los (s)")]
    public float DecayDelay = 3f;

    [Tooltip("Cuánto baja por segundo, como fracción del tope (0.1 = el tope entero en 10 s).")]
    [Label("Baja por segundo (× el tope)")]
    public float DecayPerSecond = 0.1f;

    [Header("Proyección arcana (la E)")]
    [Tooltip("Escudo con el que arranca cada proyección, como fracción de la vida MÁXIMA del Mago " +
             "(0.2 = con 140 de vida, 28). No baja solo: la proyección se acaba cuando se gasta.")]
    [Label("Escudo inicial (× vida máx. del Mago)")]
    public float ProjectionShield = 0.2f;

    [Tooltip("El efecto que lleva el aliado mientras está proyectado: el tag Status_ArcaneProjection, " +
             "el ícono y el VFX. Se refresca solo mientras dura (Refresh, ~1 s de duración).")]
    [Label("Efecto de la proyección (en el aliado)")]
    public GameplayEffect ProjectionEffect;

    [Tooltip("El efecto que lleva quien tiene escudo de la Abjuración (el Mago y sus proyectados): " +
             "su ícono y su VFX (la burbuja) dicen que tiene escudo. Se refresca mientras le quede " +
             "(Refresh, ~1 s de duración) y se quita cuando se le acaba.")]
    [Label("Efecto del escudo (la burbuja)")]
    public GameplayEffect ShieldEffect;

    [Tooltip("Cada cuánto se les refrescan esos efectos.")]
    [Label("Refrescar los efectos cada (s)")]
    public float RefreshInterval = 0.5f;

    [Tooltip("Las copias del clic izquierdo salen solo de los proyectados a esta distancia del Mago o menos.")]
    [Label("Alcance de las copias (m)")]
    public float EchoRange = 25f;

    [Tooltip("Qué parte del daño hacen las copias (0.5 = la mitad; también la quemadura).")]
    [Range(0f, 1f)]
    [Label("Daño de las copias (0 a 1)")]
    public float EchoDamage = 0.5f;

    [Header("Protección máxima (la R)")]
    [Tooltip("Mientras el Mago tenga este tag (lo da GE_MaximumProtection), proyecta a todos los aliados cerca.")]
    [Label("Tag de la Protección máxima")]
    public EGameplayTag MaximumProtectionTag = EGameplayTag.Status_MaximumProtection;

    [Tooltip("Hasta dónde proyecta la R.")]
    [Label("Alcance de la R (m)")]
    public float MaximumProtectionRange = 25f;

    [Tooltip("Capa de los personajes, donde buscar aliados.")]
    [Label("Capa de personajes")]
    public LayerMask CharacterLayer = 1 << 7;

    [Tooltip("Cada cuánto busca aliados nuevos mientras dura la R.")]
    [Label("Buscar aliados cada (s)")]
    public float ScanInterval = 0.25f;

    // Una proyección: de dónde viene y cuánto escudo nuestro le queda de cada parte.
    private class Projection
    {
        public bool  FromE;    // la de la E: dura hasta que se acabe
        public bool  FromR;    // de la Protección máxima: se va al terminar (salvo FromE)
        public float Base;     // lo que queda del escudo inicial (no baja solo)
        public float Extra;    // lo que dio la Protección arcana (baja solo)
        public float Total => Base + Extra;
    }

    private AbilitySystemComponent        _asc;
    private NetworkAbilitySystemComponent _netAsc;

    private readonly Dictionary<AbilitySystemComponent, Projection> _projections =
        new Dictionary<AbilitySystemComponent, Projection>();
    private readonly HashSet<AbilitySystemComponent> _ultTouched = new HashSet<AbilitySystemComponent>();
    private readonly List<AbilitySystemComponent>    _ending     = new List<AbilitySystemComponent>();

    // Quiénes llevan ahora el efecto del escudo (para quitárselo apenas se les acaba).
    private readonly HashSet<AbilitySystemComponent> _shielded = new HashSet<AbilitySystemComponent>();
    private readonly List<AbilitySystemComponent>    _unshield = new List<AbilitySystemComponent>();

    // Menos que esto de escudo nuestro no se muestra (un resto de 0.2 no prende la burbuja).
    private const float VisibleShield = 0.5f;

    private float _selfExtra;            // el escudo de la pasiva sobre el propio Mago
    private float _lastGainAt = -999f;   // el último golpe (la cuenta del desgaste)
    private float _scanTimer;
    private float _refreshTimer;

    private bool  IsServer      => _netAsc == null || _netAsc.IsServerInitialized;
    private float MageMaxHealth => _asc != null ? _asc.GetAttributeValue(EAttributeType.MaxHealth) : 0f;
    private float Cap           => ShieldCap * MageMaxHealth;

    private void Awake()
    {
        _asc    = GetComponentInParent<AbilitySystemComponent>();
        _netAsc = _asc != null ? _asc.GetComponent<NetworkAbilitySystemComponent>() : null;
    }

    private void OnEnable()
    {
        if (_asc == null) return;
        _asc.RegisterEchoSource(this);
        _asc.OnDealtDamage += HandleDealtDamage;
    }

    private void OnDisable()
    {
        if (_asc == null) return;
        _asc.UnregisterEchoSource(this);
        _asc.OnDealtDamage -= HandleDealtDamage;
        if (!IsServer) return;
        EndAll();
        HideShield(_asc);
    }

    // =========================================================
    // PROYECTAR
    // =========================================================

    // La E (GA_ArcaneProjection) eligió a este aliado. La de la E anterior se mueve acá.
    public void ProjectOnto(AbilitySystemComponent ally)
    {
        if (!IsServer || _asc == null || ally == null || ally == _asc) return;

        foreach (var pair in _projections)
        {
            if (pair.Key == ally || !pair.Value.FromE) continue;
            pair.Value.FromE = false;
            if (!pair.Value.FromR) _ending.Add(pair.Key);
        }
        EndQueued();

        Project(ally, fromE: true, fromR: false);
    }

    // El escudo inicial va al proyectarlo por primera vez y al relanzarle la E (se repone hasta el
    // inicial); la R que lo vuelve a ver no le da más.
    private void Project(AbilitySystemComponent ally, bool fromE, bool fromR)
    {
        bool isNew = !_projections.TryGetValue(ally, out Projection p);
        if (isNew)
        {
            p = new Projection();
            _projections[ally] = p;
            if (ProjectionEffect != null) ally.ApplyGameplayEffect(ProjectionEffect, _asc);
        }
        p.FromE |= fromE;
        p.FromR |= fromR;

        if (!isNew && !fromE) return;
        float wanted = ProjectionShield * MageMaxHealth;
        if (p.Base < wanted) GiveShield(ally, wanted - p.Base, ref p.Base);
    }

    private void EndProjection(AbilitySystemComponent ally)
    {
        if (!_projections.TryGetValue(ally, out Projection p)) return;
        _projections.Remove(ally);
        if (ally == null) return;

        if (ProjectionEffect != null) ally.RemoveEffectsByDefinition(ProjectionEffect);
        HideShield(ally);
        TakeShield(ally, p.Total);
    }

    private void EndQueued()
    {
        foreach (AbilitySystemComponent ally in _ending) EndProjection(ally);
        _ending.Clear();
    }

    private void EndAll()
    {
        _ending.AddRange(_projections.Keys);
        EndQueued();
        _ultTouched.Clear();
    }

    // =========================================================
    // CADA FRAME (servidor)
    // =========================================================

    private void Update()
    {
        if (!IsServer || _asc == null) return;

        if (_asc.HasTag(EGameplayTag.State_Dead))
        {
            if (_projections.Count > 0) EndAll();
            _selfExtra = 0f;
            if (_shielded.Contains(_asc)) HideShield(_asc);
            return;
        }

        Reconcile();
        UpdateMaximumProtection();
        if (Time.time - _lastGainAt >= DecayDelay) Decay(DecayPerSecond * Cap * Time.deltaTime);
        UpdateVisibleEffects();
    }

    // Los efectos visibles (ícono y VFX): el de la proyección en los proyectados y el del escudo en
    // quien tenga escudo nuestro. Se refrescan cada tanto (si esto deja de correr, se caen solos) y
    // el del escudo se quita apenas se acaba.
    private void UpdateVisibleEffects()
    {
        foreach (AbilitySystemComponent who in _shielded)
            if (who == null || OurShieldOn(who) <= VisibleShield) _unshield.Add(who);
        foreach (AbilitySystemComponent who in _unshield) HideShield(who);
        _unshield.Clear();

        _refreshTimer -= Time.deltaTime;
        if (_refreshTimer > 0f) return;
        _refreshTimer = Mathf.Max(0.1f, RefreshInterval);

        foreach (var pair in _projections)
        {
            if (pair.Key == null) continue;
            if (ProjectionEffect != null) pair.Key.ApplyGameplayEffect(ProjectionEffect, _asc);
            if (pair.Value.Total > VisibleShield) ShowShield(pair.Key);
        }
        if (_selfExtra > VisibleShield) ShowShield(_asc);
    }

    // Cuánto escudo nuestro tiene (el Mago: el de la pasiva; un proyectado: el inicial + el extra).
    private float OurShieldOn(AbilitySystemComponent who)
        => who == _asc ? _selfExtra : _projections.TryGetValue(who, out Projection p) ? p.Total : 0f;

    private void ShowShield(AbilitySystemComponent who)
    {
        if (ShieldEffect == null || who == null) return;
        who.ApplyGameplayEffect(ShieldEffect, _asc);
        _shielded.Add(who);
    }

    private void HideShield(AbilitySystemComponent who)
    {
        _shielded.Remove(who);
        if (ShieldEffect != null && who != null) who.RemoveEffectsByDefinition(ShieldEffect);
    }

    // Lo que se gastó con golpes: el escudo real quedó por debajo de lo anotado. Primero se va lo
    // extra, después lo inicial; sin nada nuestro, la proyección se acaba.
    private void Reconcile()
    {
        _selfExtra = Mathf.Min(_selfExtra, _asc.GetAttributeValue(EAttributeType.Shield));

        foreach (var pair in _projections)
        {
            AbilitySystemComponent ally = pair.Key;
            Projection p = pair.Value;
            if (ally == null || ally.HasTag(EGameplayTag.State_Dead)) { _ending.Add(ally); continue; }

            float actual = ally.GetAttributeValue(EAttributeType.Shield);
            if (actual < p.Total)
            {
                float lost      = p.Total - actual;
                float fromExtra = Mathf.Min(p.Extra, lost);
                p.Extra -= fromExtra;
                p.Base   = Mathf.Max(0f, p.Base - (lost - fromExtra));
            }
            if (p.Total <= 0.01f) _ending.Add(ally);
        }
        EndQueued();
    }

    // La R: mientras dura, todo aliado que aparezca a su alcance queda proyectado (una vez por R).
    // Al terminar, se van las que eran solo de la R.
    private void UpdateMaximumProtection()
    {
        bool active = MaximumProtectionTag != EGameplayTag.None && _asc.HasTag(MaximumProtectionTag);

        if (active)
        {
            _scanTimer -= Time.deltaTime;
            if (_scanTimer > 0f) return;
            _scanTimer = Mathf.Max(0.05f, ScanInterval);

            foreach (Collider col in Physics.OverlapSphere(_asc.transform.position, MaximumProtectionRange,
                                                           CharacterLayer, QueryTriggerInteraction.Collide))
            {
                AbilitySystemComponent other = col.GetComponentInParent<AbilitySystemComponent>();
                if (other == null || other == _asc || _ultTouched.Contains(other)) continue;
                if (other.HasTag(EGameplayTag.State_Dead) || !_asc.IsAllyOf(other, includeSelf: false)) continue;
                if (other.GetComponent<PlayerController>() == null) continue;   // jugadores y bots, no tótems ni invocaciones

                _ultTouched.Add(other);
                Project(other, fromE: false, fromR: true);
            }
            return;
        }

        if (_ultTouched.Count == 0) return;
        _ultTouched.Clear();
        _scanTimer = 0f;
        foreach (var pair in _projections)
        {
            if (!pair.Value.FromR) continue;
            pair.Value.FromR = false;
            if (!pair.Value.FromE) _ending.Add(pair.Key);
        }
        EndQueued();
    }

    // El desgaste de lo extra (el Mago y cada proyectado), sin tocar el escudo inicial.
    private void Decay(float amount)
    {
        if (amount <= 0f) return;

        if (_selfExtra > 0f)
        {
            float d = Mathf.Min(_selfExtra, amount);
            _selfExtra -= d;
            TakeShield(_asc, d);
        }

        foreach (var pair in _projections)
        {
            Projection p = pair.Value;
            if (pair.Key == null || p.Extra <= 0f) continue;
            float d = Mathf.Min(p.Extra, amount);
            p.Extra -= d;
            TakeShield(pair.Key, d);
        }
    }

    // =========================================================
    // LA PROTECCIÓN ARCANA: escudo al hacer daño
    // =========================================================

    private void HandleDealtDamage(AbilitySystemComponent victim, float damage)
    {
        if (!IsServer || _asc == null || damage <= 0f || ShieldPerDamage <= 0f) return;
        if (_asc.HasTag(EGameplayTag.State_Dead)) return;

        _lastGainAt = Time.time;
        float gain = damage * ShieldPerDamage;
        float cap  = Cap;

        float self = Mathf.Min(gain, cap - _selfExtra);
        if (self > 0f) GiveShield(_asc, self, ref _selfExtra);

        foreach (var pair in _projections)
        {
            Projection p = pair.Value;
            if (pair.Key == null) continue;
            float add = Mathf.Min(gain, cap - p.Total);
            if (add > 0f) GiveShield(pair.Key, add, ref p.Extra);
        }
    }

    // =========================================================
    // LAS COPIAS (IAbilityEchoSource)
    // =========================================================

    // Solo el clic izquierdo, y desde los proyectados vivos a su alcance.
    public void CollectEchoes(GameplayAbility ability, List<AbilityEcho> echoes)
    {
        if (ability == null || !ability.CountsAsBasicAttack || _asc == null) return;
        if (_asc.HasTag(EGameplayTag.State_Dead) || EchoDamage <= 0f) return;

        float range2 = EchoRange * EchoRange;
        foreach (AbilitySystemComponent ally in _projections.Keys)
        {
            if (ally == null || ally.HasTag(EGameplayTag.State_Dead)) continue;
            if ((ally.transform.position - _asc.transform.position).sqrMagnitude > range2) continue;
            echoes.Add(new AbilityEcho { From = ally, DamageScale = EchoDamage });
        }
    }

    // =========================================================
    // AYUDAS
    // =========================================================

    // Suma escudo y lo anota en 'tracked'. Al de un aliado le cuenta como curarlo (la definitiva
    // del Soporte).
    private void GiveShield(AbilitySystemComponent to, float amount, ref float tracked)
    {
        if (to == null || amount <= 0f) return;
        to.SetCurrentAttributeValue(EAttributeType.Shield, to.GetAttributeValue(EAttributeType.Shield) + amount);
        tracked += amount;
        if (to != _asc) _asc.NotifyHealedAlly(to, amount);

        // La burbuja aparece en el acto (después la mantiene el refresco).
        if (!_shielded.Contains(to) && tracked > VisibleShield) ShowShield(to);
    }

    // Saca hasta 'amount' de escudo (lo que tenga, si es menos).
    private static void TakeShield(AbilitySystemComponent from, float amount)
    {
        if (from == null || amount <= 0f) return;
        float current = from.GetAttributeValue(EAttributeType.Shield);
        from.SetCurrentAttributeValue(EAttributeType.Shield, Mathf.Max(0f, current - amount));
    }
}
