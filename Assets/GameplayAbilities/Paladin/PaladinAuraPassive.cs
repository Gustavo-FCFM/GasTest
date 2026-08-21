using UnityEngine;
using System.Collections.Generic;

// ============================================================
// PaladinAuraPassive  (Aura de protección — pasiva del Paladín)
//
// El Paladín emite un aura permanente que:
//   · le da RESISTENCIA a los aliados dentro del alcance (a él incluido), y
//   · los CURA un porcentaje del daño cada vez que él golpea a un enemigo, y
//     también cada vez que su escudo frena daño.
//
// PENSADO PARA REUSARSE EN LAS SUBCLASES, que es el requisito central: el aura de
// protección nunca se va, y al elegir subclase se le SUMA otra aura más grande
// (Devoción, Venganza, Conquista). Por eso el componente no tiene UN aura sino una
// LISTA de anillos, cada uno con su radio, su afiliación y sus efectos:
//
//   Behaviors_Paladin              → 1 anillo  (Protección, radio chico)
//   Behaviors_Paladin_Devocion     → 2 anillos (Protección MÁS GRANDE + Devoción)
//   Behaviors_Paladin_Venganza     → 2 anillos (Protección MÁS GRANDE + Venganza,
//                                               esta última apuntando a ENEMIGOS)
//   Behaviors_Paladin_Conquista    → 2 anillos (Protección MÁS GRANDE + Conquista)
//
// O sea: mismo script en los cuatro prefabs, cambian los datos. No hay una clase
// por subclase ni herencia.
//
// SETUP: va en el PassiveBehaviorsPrefab de la clase (PlayerController lo instancia
// como hijo del jugador al equiparla). Busca el ASC en el PADRE. La lógica corre
// solo en el SERVIDOR: los GEs que aplica y las curaciones se sincronizan solas por
// los canales normales del NetworkASC.
//
// TRAMPA A EVITAR AL CONFIGURAR LOS GEs DEL AURA: tienen que ser
// StackingPolicy = Refresh y Duration ≈ 2 × TickInterval. Con el default (Stack +
// MaxStacks 0) cada tick agrega una acumulación y en un minuto el aliado tendría
// cien capas de Resistencia. Con Refresh, el efecto se renueva mientras estés
// dentro y se cae solo un tick después de salir.
// ============================================================
public class PaladinAuraPassive : MonoBehaviour
{
    // A quién afecta un anillo del aura.
    public enum EAuraTarget { Allies, Enemies }

    // UN anillo del aura. La lista de abajo tiene uno por cada aura que el
    // personaje emite a la vez.
    [System.Serializable]
    public class AuraRing
    {
        [Tooltip("Solo para leerlo cómodo en el inspector (ej. 'Protección', 'Venganza').")]
        public string Name = "Aura";

        [Tooltip("Radio del aura, en metros.")]
        public float Radius = 8f;

        [Tooltip("A quién alcanza este anillo. El aura de protección apunta a Allies; " +
                 "la de Venganza (los enemigos reciben más daño) apunta a Enemies.")]
        public EAuraTarget Targets = EAuraTarget.Allies;

        [Tooltip("Solo con Targets = Allies: si el propio Paladín se cuenta como aliado " +
                 "(sí para el aura de protección — también se beneficia a sí mismo).")]
        public bool IncludeSelf = true;

        [Tooltip("GameplayEffects que se le aplican a cada objetivo válido en cada tick.\n\n" +
                 "IMPORTANTE: StackingPolicy = Refresh y Duration ≈ 2× el intervalo de tick, " +
                 "o se van a apilar sin freno (ver la nota de la cabecera del script).")]
        public List<GameplayEffect> Effects = new List<GameplayEffect>();

        [Tooltip("OPCIONAL: si se pone un tag, el anillo SOLO tickea mientras el Paladín lo tenga.\n\n" +
                 "Es lo que permite que una habilidad temporal agregue efectos a las auras sin " +
                 "duplicar la lógica: el Ángel vengador otorga su tag y con eso se encienden dos " +
                 "anillos extra (velocidad de movimiento sobre el aura de protección, velocidad de " +
                 "ataque sobre la de venganza) que el resto del tiempo están apagados.\n\n" +
                 "None = el anillo está siempre activo, que es el caso normal.")]
        public EGameplayTag RequiredOwnerTag = EGameplayTag.None;
    }

    [Header("Anillos del Aura")]
    [Tooltip("Un anillo por cada aura simultánea. El prefab de la clase base trae solo el de " +
             "protección; los de las subclases traen ese (con más radio) y el suyo propio.")]
    public List<AuraRing> Rings = new List<AuraRing>();

    [Tooltip("Cada cuánto se revisa quién está dentro de cada anillo y se reaplican sus efectos.")]
    public float TickInterval = 0.5f;

    [Tooltip("Capa de los personajes (jugadores y NPCs) donde buscar objetivos.")]
    public LayerMask CharacterLayer;

    [Header("Curación al Golpear")]
    [Tooltip("Fracción del daño infligido que se cura a los aliados en el aura. 0.3 = cada golpe " +
             "de 50 les cura 15 a todos los aliados al alcance. 0 = desactivado.")]
    [Range(0f, 2f)]
    public float HealPercentOfDamage = 0.3f;

    [Header("Curación al Bloquear")]
    [Tooltip("Lo mismo pero cuando el ESCUDO frena daño ('cada vez que evite daño, el jugador " +
             "sanará a los aliados que estén en su aura'). 0 = desactivado.")]
    [Range(0f, 2f)]
    public float HealPercentOfBlocked = 0.3f;

    [Header("Alcance de la Curación")]
    [Tooltip("Qué anillo define hasta dónde llega la curación (0 = el primero de la lista, " +
             "normalmente el de protección). Si el índice no existe, se usa el primero.")]
    public int HealRingIndex = 0;

    [Tooltip("Efectos extra que se le aplican al aliado curado (un VFX de destello, un buff " +
             "corto...). Opcional — la curación en sí no los necesita.")]
    public List<GameplayEffect> HealExtraEffects;

    private AbilitySystemComponent        _asc;
    private NetworkAbilitySystemComponent _netAsc;
    private Entity_ShieldBarrier          _barrier;
    private float _tickTimer;

    // Buffer reusado por los barridos, para no alocar una lista por tick.
    private readonly List<AbilitySystemComponent> _buffer = new List<AbilitySystemComponent>();

    // True si esta copia tiene autoridad (servidor, o escena sin red). Mismo criterio
    // que GameplayAbility.IsServer: sin capa de red, este proceso ES la autoridad.
    private bool IsServer => _netAsc == null || _netAsc.IsServerInitialized;

    // =========================================================
    // CICLO DE VIDA
    // =========================================================

    private void Awake()
    {
        // El ASC vive en el jugador; este componente es un hijo suyo.
        _asc    = GetComponentInParent<AbilitySystemComponent>();
        _netAsc = _asc != null ? _asc.GetComponent<NetworkAbilitySystemComponent>() : null;

        // La barrera del escudo vive en este mismo prefab de comportamientos, así que
        // la buscamos desde el jugador (incluyendo objetos inactivos: arranca apagada).
        if (_asc != null) _barrier = _asc.GetComponentInChildren<Entity_ShieldBarrier>(true);
    }

    private void OnEnable()
    {
        if (_asc != null)     _asc.OnDealtDamage      += HandleDealtDamage;
        if (_barrier != null) _barrier.OnDamageBlocked += HandleDamageBlocked;
    }

    private void OnDisable()
    {
        if (_asc != null)     _asc.OnDealtDamage      -= HandleDealtDamage;
        if (_barrier != null) _barrier.OnDamageBlocked -= HandleDamageBlocked;
    }

    // =========================================================
    // EL AURA (efectos por tick)
    // =========================================================

    private void Update()
    {
        if (!IsServer || _asc == null) return;

        // Un Paladín muerto no emite su aura. Las duraciones cortas de los GEs hacen
        // que se caiga sola de los aliados en un tick.
        if (_asc.HasTag(EGameplayTag.State_Dead)) return;

        _tickTimer -= Time.deltaTime;
        if (_tickTimer > 0f) return;
        _tickTimer = Mathf.Max(0.05f, TickInterval);

        foreach (var ring in Rings) ApplyRing(ring);
    }

    // Aplica los efectos de un anillo a todos los objetivos válidos dentro de su radio.
    private void ApplyRing(AuraRing ring)
    {
        if (ring == null || ring.Effects == null || ring.Effects.Count == 0) return;
        if (ring.Radius <= 0f) return;

        // Anillo condicional: solo tickea mientras el Paladín tenga el tag. Los efectos
        // que ya repartió se caen solos un tick después, porque duran ~2× el intervalo.
        if (ring.RequiredOwnerTag != EGameplayTag.None && !_asc.HasTag(ring.RequiredOwnerTag)) return;

        CollectTargets(ring.Radius, ring.Targets, ring.IncludeSelf);

        foreach (var target in _buffer)
            foreach (var effect in ring.Effects)
                if (effect != null) target.ApplyGameplayEffect(effect, _asc);
    }

    // Llena _buffer con los personajes válidos dentro de un radio. Descarta muertos:
    // ni tiene sentido buffear un cadáver ni castigarlo.
    private void CollectTargets(float radius, EAuraTarget targets, bool includeSelf)
    {
        _buffer.Clear();

        Collider[] cols = Physics.OverlapSphere(_asc.transform.position, radius, CharacterLayer);
        foreach (var c in cols)
        {
            AbilitySystemComponent other = c.GetComponentInParent<AbilitySystemComponent>();
            if (other == null || _buffer.Contains(other)) continue;
            if (other.HasTag(EGameplayTag.State_Dead)) continue;

            bool valid = targets == EAuraTarget.Allies
                ? _asc.IsAllyOf(other, includeSelf)
                : _asc.IsEnemyOf(other);

            if (valid) _buffer.Add(other);
        }
    }

    // =========================================================
    // CURACIÓN AL GOLPEAR / AL BLOQUEAR
    // =========================================================

    // El Paladín conectó un golpe: cura a los aliados del aura un porcentaje del
    // daño que entró de verdad (ya pasado por defensas y escudo — por eso la
    // cantidad viaja en el evento y no se recalcula acá).
    private void HandleDealtDamage(AbilitySystemComponent victim, float damageDealt)
    {
        if (HealPercentOfDamage <= 0f || damageDealt <= 0f) return;
        HealAura(damageDealt * HealPercentOfDamage);
    }

    // El escudo frenó daño: misma curación, con su propio porcentaje. El punto de
    // impacto no se usa acá (es para el destello, ver ShieldBlockFlash).
    private void HandleDamageBlocked(float damageBlocked, Vector3 hitPoint)
    {
        if (HealPercentOfBlocked <= 0f || damageBlocked <= 0f) return;
        HealAura(damageBlocked * HealPercentOfBlocked);
    }

    // Cura esa cantidad a todos los aliados dentro del anillo de curación.
    //
    // Se escribe la vida directo (como hace el robo de vida del ASC) en vez de
    // aplicar un GameplayEffect: la cantidad es DINÁMICA —un porcentaje de este
    // golpe— y un GE solo sabe magnitudes fijas o escaladas por stats.
    private void HealAura(float amount)
    {
        if (!IsServer || _asc == null || Rings.Count == 0 || amount <= 0f) return;

        int index = (HealRingIndex >= 0 && HealRingIndex < Rings.Count) ? HealRingIndex : 0;
        AuraRing ring = Rings[index];
        if (ring == null || ring.Radius <= 0f) return;

        // La curación siempre va a ALIADOS, aunque el anillo del que sacamos el radio
        // apunte a enemigos (ej. si alguien pone el de Venganza como referencia).
        CollectTargets(ring.Radius, EAuraTarget.Allies, includeSelf: true);

        foreach (var ally in _buffer)
        {
            float current = ally.GetAttributeValue(EAttributeType.Health);
            float max     = ally.GetAttributeValue(EAttributeType.MaxHealth);
            if (max <= 0f) continue;

            ally.SetCurrentAttributeValue(EAttributeType.Health, Mathf.Min(current + amount, max));

            if (HealExtraEffects != null)
                foreach (var effect in HealExtraEffects)
                    if (effect != null) ally.ApplyGameplayEffect(effect, _asc);
        }
    }

    // =========================================================
    // GIZMOS — los anillos reales, en la Scene view
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        Transform origin = transform.parent != null ? transform.parent : transform;

        foreach (var ring in Rings)
        {
            if (ring == null || ring.Radius <= 0f) continue;

            Gizmos.color = ring.Targets == EAuraTarget.Allies
                ? new Color(0.9f, 0.85f, 0.3f, 0.9f)   // dorado: aura de apoyo
                : new Color(0.9f, 0.2f, 0.2f, 0.9f);   // rojo: aura hostil

            Gizmos.DrawWireSphere(origin.position, ring.Radius);
        }
    }
}
