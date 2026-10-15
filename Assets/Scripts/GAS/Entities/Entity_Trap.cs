using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

// ============================================================
// Entity_Trap  (las trampas y las bombas del Trampero — 15 de octubre de 2026)
//
// UNA pieza para todas: cada trampa es este script con otros números y efectos en su prefab.
//
//   1. La pone una habilidad (GA_PlaceTrap con la rueda, GA_BombBarrage la definitiva) y la
//      prepara con Initialize: su dueño (el Trampero) y su equipo.
//   2. Puede estar en el piso o en una pared (su "arriba" sale de la superficie).
//      Tarda "Se arma en" segundos; desde ahí salta cuando un JUGADOR o bot enemigo (nunca un
//      NPC, pedido de Gustavo) entra a "Salta a".
//   3. Al saltar: "Aviso antes de explotar" (la de frío se escarcha a la vista de todos) y
//      después EXPLOTA (los efectos, una vez, en su radio) o deja una ZONA ("Zona: dura" > 0:
//      la de espinas, que golpea cada tanto mientras dura).
//   4. Si no la pisa nadie, desaparece a los "Dura" segundos; con "Explota al vencerse"
//      explota ahí (las bombas de la definitiva).
//
// INVISIBLE PARA LOS ENEMIGOS: cada pantalla dibuja el modelo solo si la trampa es de su
// equipo (o si ya saltó y se ve el aviso). El equipo viaja en un SyncVar; nada más.
//
// EL DAÑO ES DEL TRAMPERO: los "Efectos" se aplican con él como autor (escalan con su ataque, la
// baja es suya y le carga la definitiva). Los "Efectos desde la trampa" se aplican con la propia
// trampa como autor: el EMPUJE sale desde la trampa (por eso lleva un AbilitySystemComponent,
// como los tótems).
//
// LÍMITE: cada dueño tiene hasta N trampas que "cuentan para el límite"; al pasar el límite se
// saca la más vieja (lo decide quien la pone, ver Initialize). Todo corre en el servidor.
// ============================================================
[RequireComponent(typeof(AbilitySystemComponent))]
public class Entity_Trap : NetworkBehaviour
{
    [Header("Armado")]
    [Tooltip("Segundos desde que se pone hasta que puede saltar: así no explota en el acto si la " +
             "ponés al lado de alguien.")]
    [Label("Se arma en (s)")]
    public float ArmSeconds = 1.5f;

    [Tooltip("Salta cuando un jugador o bot enemigo entra a esta distancia.")]
    [Label("Salta a (m)")]
    public float TriggerRadius = 2f;

    [Tooltip("Segundos que dura puesta si nadie la pisa.")]
    [Label("Dura (s)")]
    public float LifeSeconds = 180f;

    [Tooltip("Al vencerse, explota igual (las bombas de la definitiva). Apagado = desaparece sin más.")]
    [Label("Explota al vencerse")]
    public bool ExplodeOnExpire;

    [Tooltip("Cuenta para el límite de trampas puestas del dueño (las de la definitiva no).")]
    [Label("Cuenta para el límite")]
    public bool CountsTowardLimit = true;

    [Header("Al saltar")]
    [Tooltip("Segundos entre que salta y que explota (la de frío: el aviso de escarcha). 0 = en el acto.")]
    [Label("Aviso antes de explotar (s)")]
    public float DetonateDelay = 0f;

    [Tooltip("VFX del aviso, en todas las pantallas (opcional).")]
    [Label("VFX del aviso")]
    public GameObject WarningVFX;

    [Tooltip("Radio en el que alcanza a los jugadores al explotar (o el de la zona).")]
    [Label("Radio (m)")]
    public float EffectRadius = 3f;

    [Tooltip("Lo que reciben los jugadores enemigos en el radio al explotar, con el Trampero como autor " +
             "(daño, aturdido, ralentización...).")]
    [Label("Efectos (del Trampero)")]
    public List<GameplayEffect> Effects = new List<GameplayEffect>();

    [Tooltip("Lo que reciben con la TRAMPA como autor: el empuje sale desde ella (\"Repeler (lejos)\").")]
    [Label("Efectos desde la trampa")]
    public List<GameplayEffect> TrapSourcedEffects = new List<GameplayEffect>();

    [Tooltip("VFX de la explosión (o del comienzo de la zona), en todas las pantallas.")]
    [Label("VFX al explotar")]
    public GameObject ExplosionVFX;

    [Tooltip("Tamaño del VFX de la explosión por cada metro de radio, si el VFX no trae VFX_AreaVisual " +
             "(con él, calza solo con el radio).")]
    [Label("VFX: escala por metro de radio (×)")]
    public float VfxScalePerMeter = 0.5f;

    [Header("Zona (la de espinas)")]
    [Tooltip("Si es más que 0, en vez de explotar una vez deja una zona en el radio que golpea cada " +
             "tanto durante estos segundos.")]
    [Label("Zona: dura (s)")]
    public float ZoneSeconds = 0f;

    [Tooltip("Cada cuántos segundos golpea la zona (el primero, en el acto).")]
    [Label("Zona: cada cuánto (s)")]
    public float ZoneTickSeconds = 1f;

    [Tooltip("Lo que recibe cada jugador enemigo dentro de la zona en cada golpe, con el Trampero como autor.")]
    [Label("Zona: efectos por golpe")]
    public List<GameplayEffect> ZoneEffects = new List<GameplayEffect>();

    [Header("Referencias")]
    [Label("Capa de personajes")]
    public LayerMask CharacterLayer = 1 << 7;

    [Tooltip("El modelo (el prefab de la bomba): se esconde para los enemigos y al explotar. Vacío = " +
             "todo lo que cuelga de la trampa.")]
    [Label("Modelo")]
    public Transform Visual;

    // --- todas las pantallas ---
    private readonly SyncVar<int>  _team     = new SyncVar<int>();
    private readonly SyncVar<bool> _revealed = new SyncVar<bool>();   // saltó: el aviso lo ven todos
    private readonly SyncVar<bool> _spent    = new SyncVar<bool>();   // explotó: ya no hay modelo
    private Renderer[] _renderers;
    private bool _lastVisible = true;

    // --- servidor ---
    private AbilitySystemComponent _owner;
    private AbilitySystemComponent _asc;
    private float _armedAt, _expireAt, _detonateAt, _zoneEndsAt, _nextZoneTick;
    private bool  _triggered, _detonated;
    private readonly Collider[] _hits = new Collider[32];
    private readonly HashSet<AbilitySystemComponent> _seen = new HashSet<AbilitySystemComponent>();

    // Las trampas que cuentan para el límite, por dueño, en orden (la primera es la más vieja).
    private static readonly Dictionary<AbilitySystemComponent, List<Entity_Trap>> _byOwner =
        new Dictionary<AbilitySystemComponent, List<Entity_Trap>>();

    private void Awake()
    {
        _asc = GetComponent<AbilitySystemComponent>();
        Transform root = Visual != null ? Visual : transform;
        _renderers = root.GetComponentsInChildren<Renderer>(true);
    }

    // Lo llama quien la pone, en el servidor, justo después de spawnearla. maxAlive: cuántas
    // trampas que cuentan para el límite puede tener este dueño (0 = sin límite).
    [Server]
    public void Initialize(AbilitySystemComponent owner, int maxAlive)
    {
        _owner = owner;
        _team.Value = owner != null ? owner.TeamID : 0;
        if (_asc != null) _asc.TeamID = _team.Value;

        _armedAt  = Time.time + ArmSeconds;
        _expireAt = Time.time + LifeSeconds;

        if (!CountsTowardLimit || owner == null) return;
        if (!_byOwner.TryGetValue(owner, out List<Entity_Trap> list))
            _byOwner[owner] = list = new List<Entity_Trap>();
        list.RemoveAll(t => t == null);
        list.Add(this);
        while (maxAlive > 0 && list.Count > maxAlive)
        {
            Entity_Trap oldest = list[0];
            list.RemoveAt(0);
            if (oldest != null) oldest.ServerDespawn();
        }
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        if (_owner != null && _byOwner.TryGetValue(_owner, out List<Entity_Trap> list)) list.Remove(this);
    }

    // =========================================================
    // SERVIDOR
    // =========================================================

    private void Update()
    {
        UpdateVisibility();
        if (!IsServerInitialized || (_detonated && ZoneSeconds <= 0f)) return;

        // Sin dueño (se fue, cambió de clase) la trampa no tiene a quién darle el daño.
        if (_owner == null) { ServerDespawn(); return; }

        if (_detonated) { TickZone(); return; }

        if (!_triggered)
        {
            if (Time.time >= _expireAt)
            {
                if (ExplodeOnExpire) Detonate();
                else ServerDespawn();
                return;
            }
            if (Time.time >= _armedAt && EnemyPlayerNear()) Trigger();
            return;
        }

        if (Time.time >= _detonateAt) Detonate();
    }

    private bool EnemyPlayerNear()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, TriggerRadius, _hits, CharacterLayer, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
            if (IsTarget(_hits[i] != null ? _hits[i].GetComponentInParent<AbilitySystemComponent>() : null)) return true;
        return false;
    }

    // Solo jugadores y bots enemigos, vivos (nunca NPCs).
    private bool IsTarget(AbilitySystemComponent other)
        => other != null && other != _owner && other.GetComponent<PlayerController>() != null &&
           !other.HasTag(EGameplayTag.State_Dead) && _owner.IsEnemyOf(other);

    private void Trigger()
    {
        _triggered  = true;
        _detonateAt = Time.time + DetonateDelay;
        if (DetonateDelay > 0f)
        {
            _revealed.Value = true;   // el aviso lo ven todos
            ObserversWarning();
        }
    }

    private void Detonate()
    {
        _detonated = true;
        _spent.Value = true;
        ObserversExplosion();

        if (ZoneSeconds > 0f)
        {
            _zoneEndsAt   = Time.time + ZoneSeconds;
            _nextZoneTick = Time.time;
            TickZone();
            return;
        }

        foreach (AbilitySystemComponent target in TargetsInRadius())
        {
            foreach (GameplayEffect e in Effects)            if (e != null) target.ApplyGameplayEffect(e, _owner);
            foreach (GameplayEffect e in TrapSourcedEffects) if (e != null) target.ApplyGameplayEffect(e, _asc);
        }
        Invoke(nameof(ServerDespawn), 0.5f);   // que alcance a verse la explosión
    }

    private void TickZone()
    {
        if (Time.time >= _zoneEndsAt) { ServerDespawn(); return; }
        if (Time.time < _nextZoneTick) return;
        _nextZoneTick = Time.time + Mathf.Max(0.1f, ZoneTickSeconds);

        foreach (AbilitySystemComponent target in TargetsInRadius())
            foreach (GameplayEffect e in ZoneEffects) if (e != null) target.ApplyGameplayEffect(e, _owner);
    }

    private readonly List<AbilitySystemComponent> _targets = new List<AbilitySystemComponent>();
    private List<AbilitySystemComponent> TargetsInRadius()
    {
        _targets.Clear();
        _seen.Clear();
        int count = Physics.OverlapSphereNonAlloc(transform.position, EffectRadius, _hits, CharacterLayer, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            AbilitySystemComponent other = _hits[i] != null ? _hits[i].GetComponentInParent<AbilitySystemComponent>() : null;
            if (other != null && _seen.Add(other) && IsTarget(other)) _targets.Add(other);
        }
        return _targets;
    }

    [Server]
    public void ServerDespawn()
    {
        if (IsSpawned) ServerManager.Despawn(gameObject);
    }

    // =========================================================
    // LO QUE VEN TODOS
    // =========================================================

    // El modelo: lo ve su equipo (y nadie una vez que explotó); los enemigos solo durante el aviso.
    private void UpdateVisibility()
    {
        bool visible;
        if (_spent.Value) visible = false;
        else if (_revealed.Value) visible = true;
        else visible = !IsClientInitialized || _team.Value == 0 || MercUIFactory.LocalTeam() == _team.Value;

        if (visible == _lastVisible) return;
        _lastVisible = visible;
        foreach (Renderer r in _renderers) if (r != null) r.enabled = visible;
    }

    [ObserversRpc(RunLocally = true)]
    private void ObserversWarning()
    {
        if (WarningVFX != null) Destroy(Instantiate(WarningVFX, transform.position, transform.rotation), Mathf.Max(1f, DetonateDelay + 0.5f));
    }

    [ObserversRpc(RunLocally = true)]
    private void ObserversExplosion()
    {
        if (ExplosionVFX == null) return;
        // Con la rotación de la trampa: en una pared, las espinas salen de la pared.
        GameObject vfx = Instantiate(ExplosionVFX, transform.position, transform.rotation);
        VFX_AreaVisual.Configure(vfx, EffectRadius, VfxScalePerMeter, ZoneSeconds > 0f ? ZoneSeconds : 0f);
        Destroy(vfx, Mathf.Max(2f, ZoneSeconds + 1f));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, TriggerRadius);
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, EffectRadius);
    }
}
