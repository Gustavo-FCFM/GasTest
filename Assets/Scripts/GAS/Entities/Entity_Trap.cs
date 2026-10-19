using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

// ============================================================
// Entity_Trap  (las trampas y las bombas del Trampero — 15 de octubre de 2026)
//
// UNA pieza para todas: cada trampa es este script con otros números y efectos en su prefab.
// Desde el 19 de octubre de 2026 es una invocación (Entity_Summon): la base resuelve el equipo
// del Trampero, que la vean solo los suyos ("La ven: solo su equipo"), "Dura", el límite de
// trampas por Trampero (la más vieja se va; las bombas no cuentan) y desaparecer si el Trampero
// se va o cambia de clase. Y la zona de espinas es una Summon_Zone. Acá queda lo propio:
//
//   1. La pone una habilidad (GA_PlaceTrap con la rueda, GA_BombBarrage la definitiva) con
//      Entity_Summon.Spawn. Puede estar en el piso o en una pared (su "arriba" sale de la
//      superficie).
//   2. Tarda "Se arma en" segundos; desde ahí salta cuando un JUGADOR o bot enemigo (nunca un
//      NPC, pedido de Gustavo) entra a "Salta a".
//   3. Al saltar: "Aviso antes de explotar" (la de frío se escarcha a la vista de todos) y
//      después EXPLOTA (los efectos, una vez, en su radio) o prende su ZONA (la de espinas, que
//      golpea cada tanto mientras dura: su Summon_Zone).
//   4. Si no la pisa nadie, se va a los "Dura" segundos; con "Explota al vencerse" explota ahí
//      (las bombas de la definitiva).
//
// EL DAÑO ES DEL TRAMPERO: los "Efectos" se aplican con él como autor (escalan con su ataque, la
// baja es suya y le carga la definitiva). Los "Efectos desde la trampa" se aplican con la propia
// trampa como autor: el EMPUJE sale desde ella (por eso lleva un AbilitySystemComponent).
//
// PREFABS: Trap_Base (todo lo común) y una VARIANTE por trampa (Trap_Spikes, Trap_Cold,
// Trap_Explosive, Trap_Bomb) con su modelo y sus números: lo que se cambie en la base cambia
// en todas.
// ============================================================
[RequireComponent(typeof(AbilitySystemComponent))]
public class Entity_Trap : Entity_Summon
{
    [Header("Armado")]
    [Tooltip("Segundos desde que se pone hasta que puede saltar: así no explota en el acto si la " +
             "ponés al lado de alguien.")]
    [Label("Se arma en (s)")]
    public float ArmSeconds = 1.5f;

    [Tooltip("Salta cuando un jugador o bot enemigo entra a esta distancia.")]
    [Label("Salta a (m)")]
    public float TriggerRadius = 2f;

    [Tooltip("Al vencerse (\"Dura\"), explota igual (las bombas de la definitiva). Apagado = desaparece sin más.")]
    [Label("Explota al vencerse")]
    public bool ExplodeOnExpire;

    [Header("Al saltar")]
    [Tooltip("Segundos entre que salta y que explota (la de frío: el aviso de escarcha). 0 = en el acto.")]
    [Label("Aviso antes de explotar (s)")]
    public float DetonateDelay = 0f;

    [Tooltip("VFX del aviso, en todas las pantallas (opcional).")]
    [Label("VFX del aviso")]
    public GameObject WarningVFX;

    [Tooltip("Radio en el que alcanza a los jugadores al explotar.")]
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

    [Header("Referencias")]
    [Label("Capa de personajes")]
    public LayerMask CharacterLayer = 1 << 7;

    // --- servidor ---
    private float _armedAt, _detonateAt;
    private bool  _triggered, _detonated;
    private Summon_Zone _zone;
    private readonly Collider[] _hits = new Collider[32];
    private readonly HashSet<AbilitySystemComponent> _seen = new HashSet<AbilitySystemComponent>();
    private readonly List<AbilitySystemComponent> _targets = new List<AbilitySystemComponent>();

    protected override void Awake()
    {
        base.Awake();
        _zone = GetComponent<Summon_Zone>();
    }

    protected override void OnSummoned()
    {
        _armedAt = Time.time + ArmSeconds;
    }

    // =========================================================
    // SERVIDOR
    // =========================================================

    protected override void Update()
    {
        base.Update();
        if (!IsLiveOnServer || _detonated) return;

        if (!_triggered)
        {
            if (Time.time >= _armedAt && EnemyPlayerNear()) Trigger();
            return;
        }

        if (Time.time >= _detonateAt) Detonate();
    }

    // "Dura" se cumplió sin que nadie la pisara: explota (las bombas) o se va. Si ya saltó, lo
    // que sigue (el aviso, la zona) termina solo.
    protected override void OnExpired()
    {
        if (_triggered || _detonated) return;
        if (ExplodeOnExpire) Detonate();
        else ServerDespawn();
    }

    // La zona de espinas se terminó: la trampa ya no tiene nada que hacer.
    protected override void OnZoneEnded() => ServerDespawn();

    private bool EnemyPlayerNear()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, TriggerRadius, _hits, CharacterLayer, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
            if (IsTarget(_hits[i] != null ? _hits[i].GetComponentInParent<AbilitySystemComponent>() : null)) return true;
        return false;
    }

    // Solo jugadores y bots enemigos, vivos (nunca NPCs).
    private bool IsTarget(AbilitySystemComponent other)
        => other != null && other.GetComponent<PlayerController>() != null &&
           !other.HasTag(EGameplayTag.State_Dead) && IsEnemy(other);

    private void Trigger()
    {
        _triggered  = true;
        _detonateAt = Time.time + DetonateDelay;
        if (DetonateDelay > 0f)
        {
            SetRevealed(true);   // el aviso lo ven todos
            ObserversWarning();
        }
    }

    private void Detonate()
    {
        _detonated = true;
        SetModelHidden(true);
        ObserversExplosion();

        // La de espinas: su zona golpea cada tanto y, al terminar, la trampa se va (OnZoneEnded).
        if (_zone != null)
        {
            _zone.Begin();
            return;
        }

        AbilitySystemComponent owner = Summoner;
        foreach (AbilitySystemComponent target in TargetsInRadius())
        {
            if (owner != null)  foreach (GameplayEffect e in Effects)            if (e != null) target.ApplyGameplayEffect(e, owner);
            if (OwnASC != null) foreach (GameplayEffect e in TrapSourcedEffects) if (e != null) target.ApplyGameplayEffect(e, OwnASC);
        }
        Invoke(nameof(ServerDespawn), 0.5f);   // que alcance a verse la explosión
    }

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

    // =========================================================
    // LO QUE VEN TODOS
    // =========================================================

    [ObserversRpc(RunLocally = true)]
    private void ObserversWarning()
    {
        if (WarningVFX != null) Destroy(Instantiate(WarningVFX, transform.position, transform.rotation), Mathf.Max(1f, DetonateDelay + 0.5f));
    }

    [ObserversRpc(RunLocally = true)]
    private void ObserversExplosion()
    {
        if (ExplosionVFX == null) return;
        // Con la rotación de la trampa: en una pared, las espinas salen de la pared. Con zona, el
        // VFX dura lo que ella.
        float zoneSeconds = _zone != null ? _zone.Seconds : 0f;
        GameObject vfx = Instantiate(ExplosionVFX, transform.position, transform.rotation);
        VFX_AreaVisual.Configure(vfx, _zone != null ? _zone.Radius : EffectRadius, VfxScalePerMeter, zoneSeconds);
        Destroy(vfx, Mathf.Max(2f, zoneSeconds + 1f));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, TriggerRadius);
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, EffectRadius);
    }

    // =========================================================
    // DATOS VIEJOS: la zona de espinas vivía acá. "Mercenarios ▸ Una vez ▸ Pasar las trampas a la
    // base de invocaciones" los copia a una Summon_Zone. Después de correrla no se usan.
    // =========================================================
#pragma warning disable CS0414   // solo los lee la herramienta, por SerializedObject
    [SerializeField, HideInInspector] private float ZoneSeconds = 0f;
    [SerializeField, HideInInspector] private float ZoneTickSeconds = 1f;
    [SerializeField, HideInInspector] private List<GameplayEffect> ZoneEffects = new List<GameplayEffect>();
#pragma warning restore CS0414
}
