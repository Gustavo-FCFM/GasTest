using System.Collections.Generic;
using UnityEngine;

// ============================================================
// Summon_Zone  (la zona de una invocación — 19 de octubre de 2026)
//
// "A los que estén dentro de este radio, cada tanto, aplicarles estos efectos." Estaba repetida
// en el aura del tótem, en la zona de espinas de la trampa y en las áreas continuas; ahora es
// una pieza que se pone al lado de un Entity_Summon (o de una subclase) en el prefab:
//
//   · A quién: aliados (y el dueño) o enemigos; si se quiere, solo jugadores (sin NPCs).
//   · Autor de los efectos: el DUEÑO (escalan con sus stats y la baja es suya: el daño) o la
//     INVOCACIÓN (necesita su propio ASC: el empuje sale desde ella; el aura del tótem).
//   · Potenciada: si el dueño tiene un tag (el Frenesí del Chamán), otros efectos.
//   · Empieza prendida al aparecer, o la prende la invocación cuando quiere (Begin; la trampa
//     de espinas al explotar). Dura lo que la invocación, o sus propios segundos.
//   · El VFX de la zona sale en todas las pantallas mientras está prendida, del tamaño del radio
//     (con VFX_AreaVisual calza exacto). El "prendida" viaja en un SyncVar de la invocación.
//   · Forma: un círculo (el radio, a cualquier altura) o una CAJA apoyada en el piso, girada con
//     la invocación (el Muro de fuego del Mago: 10 m de lado a lado, 2 de largo y 2 de alto; el
//     que salta más alto no se quema). Con caja, el VFX va como hijo del prefab (las llamas de
//     cada casilla) y el de acá se deja vacío.
//
// Una zona por invocación. Los efectos se aplican solo en el servidor.
// ============================================================
[RequireComponent(typeof(Entity_Summon))]
public class Summon_Zone : MonoBehaviour
{
    public enum EShape
    {
        [InspectorName("Círculo (el radio)")]           Sphere,
        [InspectorName("Caja (ancho × alto × largo)")]  Box,
    }

    public enum ETargets
    {
        [InspectorName("Aliados (y el dueño)")] Allies,
        [InspectorName("Enemigos")]             Enemies,
    }

    public enum EAuthor
    {
        [InspectorName("El dueño")]       Owner,
        [InspectorName("La invocación")]  Summon,
    }

    [Header("Zona")]
    [Tooltip("Se prende sola al aparecer la invocación. Apagado = la prende la invocación cuando " +
             "quiere (la trampa de espinas, al explotar).")]
    [Label("Empieza prendida")]
    public bool StartActive = true;

    [Tooltip("Cuánto dura prendida. 0 = lo que dure la invocación.")]
    [Label("Dura (s; 0 = lo que la invocación)")]
    public float Seconds = 0f;

    [Tooltip("Círculo: todo lo que esté a 'Radio' metros. Caja: un rectángulo apoyado en el piso, " +
             "girado como la invocación (de lado a lado × alto × hacia adelante).")]
    [Label("Forma")]
    public EShape Shape = EShape.Sphere;

    [Tooltip("Solo con forma Círculo.")]
    [Label("Radio (m)")]
    public float Radius = 5f;

    [Tooltip("Solo con forma Caja: de lado a lado (el eje X de la invocación).")]
    [Label("Caja: ancho de lado a lado (m)")]
    public float BoxWidth = 10f;

    [Tooltip("Solo con forma Caja: desde el piso hacia arriba. El que salta más alto no la toca.")]
    [Label("Caja: alto (m)")]
    public float BoxHeight = 2f;

    [Tooltip("Solo con forma Caja: hacia adelante (el eje Z de la invocación).")]
    [Label("Caja: largo hacia adelante (m)")]
    public float BoxDepth = 2f;

    [Tooltip("Cada cuánto aplica los efectos. El primero, en el acto.")]
    [Label("Cada cuánto (s)")]
    public float TickSeconds = 0.5f;

    [Label("A quién")]
    public ETargets Targets = ETargets.Allies;

    [Tooltip("Solo jugadores y bots: los NPCs no la sienten (las trampas).")]
    [Label("Solo jugadores (sin NPCs)")]
    public bool PlayersOnly = false;

    [Tooltip("Quién figura como autor de los efectos.\n\n" +
             "· El dueño: escalan con sus stats, la baja es suya y le carga la definitiva (el daño).\n" +
             "· La invocación: necesita su propio AbilitySystemComponent. Un empuje sale desde ella.")]
    [Label("Autor de los efectos")]
    public EAuthor Author = EAuthor.Owner;

    [Tooltip("Lo que recibe cada uno dentro del radio, en cada vez.")]
    [Label("Efectos")]
    public List<GameplayEffect> Effects = new List<GameplayEffect>();

    [Header("Potenciada")]
    [Tooltip("Si el DUEÑO tiene este tag, se aplican los efectos potenciados en vez de los normales " +
             "(el tótem con el Chamán enfurecido). None = nunca.")]
    [Label("Si el dueño tiene")]
    public EGameplayTag EmpowerTag = EGameplayTag.None;

    [Label("Efectos potenciados")]
    public List<GameplayEffect> EmpoweredEffects = new List<GameplayEffect>();

    [Header("Visual")]
    [Tooltip("En todas las pantallas mientras la zona está prendida, del tamaño del radio.")]
    [Label("VFX de la zona")]
    public GameObject VFX;

    [Tooltip("Si el VFX no trae VFX_AreaVisual (con él calza solo con el radio): su tamaño por " +
             "cada metro de radio.")]
    [Label("VFX: escala (×)")]
    public float VfxScale = 1f;

    [Label("Capa de personajes")]
    public LayerMask CharacterLayer = 1 << 7;

    private Entity_Summon _summon;
    private GameObject    _vfx;
    private float _vfxGrowth;   // con cuánto crecimiento se escaló el VFX
    private float _endsAt = float.MaxValue, _nextTick;

    // El radio de ahora: el del prefab más lo que creció (Entity_Summon.GrowZone: la Tormenta de la
    // Evocación crece con cada Descarga que le pega). En todas las pantallas.
    public float CurrentRadius => Radius + (_summon != null ? _summon.ZoneGrowth : 0f);

    private static readonly Collider[] s_hits = new Collider[64];
    private readonly HashSet<AbilitySystemComponent> _seen = new HashSet<AbilitySystemComponent>();

    private void Awake() => _summon = GetComponent<Entity_Summon>();

    // La prende (servidor). La llama la invocación: sola al aparecer si "Empieza prendida", o
    // cuando decide.
    public void Begin()
    {
        if (_summon == null || !_summon.IsServerInitialized) return;
        _endsAt   = Seconds > 0f ? Time.time + Seconds : float.MaxValue;
        _nextTick = Time.time;
        _summon.SetZoneFlag(true);
    }

    private void Update()
    {
        if (_summon == null) return;
        bool on = _summon.ZoneActive;

        // Todas las pantallas: el VFX sigue al "prendida" sincronizado.
        if (on && _vfx == null && VFX != null)
        {
            _vfx = Instantiate(VFX, transform.position, transform.rotation, transform);
            _vfxGrowth = _summon.ZoneGrowth;
            VFX_AreaVisual.Configure(_vfx, CurrentRadius, VfxScale, 0f);
        }
        else if (!on && _vfx != null)
        {
            Destroy(_vfx);
            _vfx = null;
        }
        else if (_vfx != null && !Mathf.Approximately(_vfxGrowth, _summon.ZoneGrowth))
        {
            // La zona creció: el VFX crece con ella (solo el tamaño; sigue vivo).
            _vfxGrowth = _summon.ZoneGrowth;
            VFX_AreaVisual area = _vfx.GetComponent<VFX_AreaVisual>();
            if (area != null) area.SetRadius(CurrentRadius);
            else              _vfx.transform.localScale = Vector3.one * (CurrentRadius * VfxScale);
        }

        // Servidor: los efectos.
        if (!on || !_summon.IsServerInitialized) return;
        if (Time.time >= _endsAt) { _endsAt = float.MaxValue; _summon.NotifyZoneEnded(); return; }
        if (Time.time < _nextTick) return;
        _nextTick = Time.time + Mathf.Max(0.05f, TickSeconds);
        Apply();
    }

    private void Apply()
    {
        AbilitySystemComponent owner  = _summon.Summoner;
        AbilitySystemComponent author = Author == EAuthor.Owner ? owner : _summon.OwnASC;
        if (author == null) author = Author == EAuthor.Owner ? _summon.OwnASC : owner;
        if (author == null) return;

        List<GameplayEffect> effects = EmpowerTag != EGameplayTag.None && owner != null && owner.HasTag(EmpowerTag) &&
                                       EmpoweredEffects != null && EmpoweredEffects.Count > 0
                                       ? EmpoweredEffects : Effects;
        if (effects == null || effects.Count == 0) return;

        _seen.Clear();
        int count = Shape == EShape.Box
            ? Physics.OverlapBoxNonAlloc(BoxCenter, BoxHalfExtents, s_hits, transform.rotation, CharacterLayer,
                                         QueryTriggerInteraction.Collide)
            : Physics.OverlapSphereNonAlloc(transform.position, CurrentRadius, s_hits, CharacterLayer,
                                            QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            AbilitySystemComponent target = s_hits[i] != null ? s_hits[i].GetComponentInParent<AbilitySystemComponent>() : null;
            if (target == null || !_seen.Add(target) || target.HasTag(EGameplayTag.State_Dead)) continue;

            Entity_Summon other = target.GetComponent<Entity_Summon>();
            if (other != null && !other.ReceivesZones) continue;

            bool fits = Targets == ETargets.Allies ? _summon.IsAlly(target) : _summon.IsEnemy(target);
            if (!fits) continue;
            if (PlayersOnly && target.GetComponent<PlayerController>() == null) continue;

            foreach (GameplayEffect e in effects)
                if (e != null) target.ApplyGameplayEffect(e, author);
        }
    }

    // La caja: apoyada en el piso (el pivote de la invocación) y girada con ella.
    private Vector3 BoxCenter      => transform.position + transform.up * (BoxHeight * 0.5f);
    private Vector3 BoxHalfExtents => new Vector3(BoxWidth * 0.5f, BoxHeight * 0.5f, BoxDepth * 0.5f);

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Targets == ETargets.Allies ? new Color(0f, 1f, 0.5f, 0.3f) : new Color(1f, 0.3f, 0.2f, 0.3f);
        if (Shape == EShape.Box)
        {
            Gizmos.matrix = Matrix4x4.TRS(BoxCenter, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, BoxHalfExtents * 2f);
            Gizmos.matrix = Matrix4x4.identity;
            return;
        }
        Gizmos.DrawWireSphere(transform.position, Radius);
    }
}
