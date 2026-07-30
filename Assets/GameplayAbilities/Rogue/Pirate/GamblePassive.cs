using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using FishNet;

// ============================================================
// GamblePassive  (Apostar — pasiva extra del Pirata)
//
// Cada tanto, el Pirata "apuesta" por un enemigo al azar que tenga cerca: lo marca
// por un tiempo y le pega MÁS FUERTE mientras la marca dure. Cómo se resuelve la
// apuesta:
//   · GANA  → si el PIRATA mata al marcado antes de que expire: se lleva los
//             WinEffects (buff de daño/velocidades + una curación).
//   · PIERDE → si el marcado mata al Pirata: ese enemigo se lleva los LossEffects
//             (una versión menor de esos beneficios, por poco tiempo).
//   · Si expira sin muertes, no pasa nada: se espera al próximo intervalo.
//
// El "daño aumentado" es SOLO del Pirata contra su propio marcado — por eso es un
// IDamageModifier (igual que el Ataque Furtivo del Pícaro base) y no un sistema de
// vulnerabilidad general: la marca no hace que le peguen más los demás.
//
// QUIÉN MATÓ A QUIÉN: no hace falta nada nuevo. El ASC ya anota en LastAttacker al
// último que le bajó vida (server-side), así que al morir cualquiera de los dos
// alcanza con mirar ese campo para saber si la apuesta se ganó o se perdió.
//
// RED: la lógica es SERVER-ONLY (marcar, aplicar efectos y resolver la apuesta son
// autoridad de servidor). Este componente igual existe en todos los peers —el prefab
// de pasivas se instancia en todos, ver PlayerController.SetupPassiveBehaviors— así
// que todo lo que muta estado va detrás de un guard de IsServerStarted. La marca
// viaja sola a los clientes: es un GameplayEffect normal, su tag se sincroniza por
// NetTags y su icono por el registro de efectos.
//
// SETUP: va en el PassiveBehaviorsPrefab del Pirata (junto a BackstabDamageModifier,
// que hereda del Pícaro base). Como vive en un hijo del jugador, busca el ASC en el
// PADRE. Hay que asignarle el GE de la marca y las listas de efectos de la apuesta.
// ============================================================
public class GamblePassive : MonoBehaviour, IDamageModifier
{
    [Header("Marca")]
    [Tooltip("Cada cuántos segundos busca un nuevo enemigo al que apostarle (solo si no hay una marca activa).")]
    public float MarkInterval = 15f;

    [Tooltip("Radio en el que busca enemigos a los que apostarle.")]
    public float SearchRadius = 20f;

    [Tooltip("Capa de personajes en la que buscar enemigos (Character).")]
    public LayerMask CharacterLayer;

    [Tooltip("GE de la marca que se aplica al enemigo elegido. Debe otorgar Status_Gambled y " +
             "tener Duration > 0 (esa duración ES el tiempo que dura la apuesta). Marcalo como " +
             "Debuff para que salga en rojo en su barra de efectos.")]
    public GameplayEffect MarkEffect;

    [Tooltip("Cuánto más fuerte le pega el Pirata al enemigo marcado (1.25 = +25%). Solo afecta " +
             "al daño del Pirata contra SU marcado, no al de sus aliados.")]
    public float DamageMultiplier = 1.25f;

    [Header("Apuesta ganada (el Pirata mata al marcado)")]
    [Tooltip("Efectos que se aplica el PIRATA al ganar la apuesta. Poné acá el buff con duración " +
             "(daño, velocidad de movimiento y de ataque) y, aparte, un GE INSTANTÁNEO de curación " +
             "(un GE con duración no puede tocar la Vida: es un 'pool', quedaría inerte).")]
    public List<GameplayEffect> WinEffects;

    [Header("Apuesta perdida (el marcado mata al Pirata)")]
    [Tooltip("Efectos que recibe el ENEMIGO que ganó la apuesta (versión menor de los de arriba, " +
             "por poco tiempo).")]
    public List<GameplayEffect> LossEffects;

    // ASC del Pirata (este componente vive en un hijo suyo).
    private AbilitySystemComponent _asc;

    // Enemigo sobre el que hay una apuesta activa ahora mismo (solo server). null = sin apuesta.
    private AbilitySystemComponent _marked;

    // Cuándo vence ESTA apuesta. Llevamos el tiempo nosotros en vez de preguntar por el
    // tag Status_Gambled del objetivo: los tags del ASC son un CONTEO, así que si otro
    // Pirata también le apostó al mismo enemigo, el tag sigue puesto por la marca del
    // otro y daríamos la nuestra por viva (seguiríamos pegando de más y podríamos cobrar
    // una apuesta ya vencida).
    private float _markEndTime;

    // True mientras la apuesta de ESTE Pirata siga vigente.
    private bool HasActiveMark => _marked != null && Time.time < _markEndTime;

    private void Awake() => _asc = GetComponentInParent<AbilitySystemComponent>();

    private void OnEnable()
    {
        if (_asc == null) return;

        _asc.RegisterDamageModifier(this);
        // Para detectar la apuesta PERDIDA: si el Pirata muere, miramos si lo mató
        // justo el enemigo al que le había apostado.
        _asc.OnDeath += HandleOwnerDeath;

        StartCoroutine(GambleRoutine());
    }

    private void OnDisable()
    {
        if (_asc != null)
        {
            _asc.UnregisterDamageModifier(this);
            _asc.OnDeath -= HandleOwnerDeath;
        }
        // Cambio de clase / destrucción: soltamos la suscripción al marcado para no
        // dejar callbacks colgando sobre un enemigo que sigue vivo.
        ClearMark();
    }

    // =========================================================
    // DAÑO AUMENTADO CONTRA EL MARCADO
    // =========================================================

    // Amplifica el daño del Pirata contra SU enemigo marcado. Magnitude es negativa
    // (es daño), así que multiplicarla por >1 pega más fuerte. No aplica a ticks de
    // DoT, por consistencia con las demás pasivas de daño.
    public void ModifyOutgoingDamage(ref DamageContext ctx)
    {
        if (ctx.IsPeriodicTick || ctx.Target == null) return;
        if (!HasActiveMark || !ReferenceEquals(ctx.Target, _marked)) return;

        ctx.Magnitude *= Mathf.Max(1f, DamageMultiplier);
    }

    // =========================================================
    // CICLO DE LA APUESTA
    // =========================================================

    // Cada MarkInterval segundos, si no hay una apuesta en curso, elige un enemigo
    // cercano al azar y le apuesta. Corre en todos los peers pero solo actúa en el
    // servidor (la marca es un GE: aplicarlo es autoridad de servidor).
    private IEnumerator GambleRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.5f, MarkInterval));

        while (true)
        {
            yield return wait;

            if (!InstanceFinder.IsServerStarted) continue;
            if (_asc == null || _asc.HasTag(EGameplayTag.State_Dead)) continue;

            // Si la apuesta anterior venció sola (nadie murió), la damos por cerrada.
            if (!HasActiveMark) ClearMark();
            else continue; // apuesta todavía en curso

            AbilitySystemComponent target = PickRandomNearbyEnemy();
            if (target != null) ServerMarkTarget(target);
        }
    }

    // Apuesta por un enemigo concreto. Público para que la ultimate del Pirata
    // (Cañones) pueda marcar a quien golpee, además del marcado automático de acá.
    // Si ya había una apuesta en curso, la reemplaza.
    public void ServerMarkTarget(AbilitySystemComponent target)
    {
        if (!InstanceFinder.IsServerStarted) return;
        if (_asc == null || target == null || MarkEffect == null) return;
        if (!_asc.IsEnemyOf(target) || target.HasTag(EGameplayTag.State_Dead)) return;
        // Ya le estamos apostando a ESE mismo y la apuesta sigue viva: no la reiniciamos
        // (si no, los impactos repetidos de Cañones la refrescarían para siempre). Si ya
        // venció, seguimos de largo y se le vuelve a apostar normalmente.
        if (HasActiveMark && ReferenceEquals(target, _marked)) return;

        ClearMark();

        _marked      = target;
        _markEndTime = Time.time + MarkEffect.Duration;
        // Para detectar la apuesta GANADA: nos avisa cuando el marcado muere, y ahí
        // miramos si fue el Pirata quien lo mató.
        _marked.OnDeath += HandleMarkedDeath;

        target.ApplyGameplayEffect(MarkEffect, _asc);
    }

    // Enemigo vivo AL AZAR dentro de SearchRadius (el diseño pide aleatorio, no el
    // más cercano). Devuelve null si no hay ninguno.
    private AbilitySystemComponent PickRandomNearbyEnemy()
    {
        Collider[] cols = Physics.OverlapSphere(_asc.transform.position, SearchRadius, CharacterLayer);

        List<AbilitySystemComponent> candidates = new List<AbilitySystemComponent>();
        HashSet<AbilitySystemComponent> seen = new HashSet<AbilitySystemComponent>();

        foreach (Collider c in cols)
        {
            AbilitySystemComponent asc = c.GetComponentInParent<AbilitySystemComponent>();
            if (asc == null || ReferenceEquals(asc, _asc) || !seen.Add(asc)) continue;
            if (!_asc.IsEnemyOf(asc) || asc.HasTag(EGameplayTag.State_Dead)) continue;
            candidates.Add(asc);
        }

        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }

    // =========================================================
    // RESOLUCIÓN
    // =========================================================

    // Murió el enemigo marcado. Si lo mató el Pirata, cobramos la apuesta; si lo
    // mató otro, la apuesta simplemente se cierra sin premio.
    private void HandleMarkedDeath()
    {
        if (!InstanceFinder.IsServerStarted) { ClearMark(); return; }

        // Solo cobramos si la apuesta seguía vigente Y lo mató el Pirata. LastAttacker
        // lo anota el ASC en cada golpe que baja vida (server-side).
        bool wonByOwner = HasActiveMark && ReferenceEquals(_marked.LastAttacker, _asc);

        if (wonByOwner) ApplyAll(WinEffects, _asc);

        ClearMark();
    }

    // Murió el Pirata. Si el que lo mató es justo el enemigo al que le había
    // apostado, ese enemigo se lleva el premio de consolación.
    private void HandleOwnerDeath()
    {
        if (!InstanceFinder.IsServerStarted) { ClearMark(); return; }

        AbilitySystemComponent killer = _asc != null ? _asc.LastAttacker : null;
        if (killer != null && HasActiveMark && ReferenceEquals(killer, _marked))
            ApplyAll(LossEffects, killer);

        // La apuesta se cierra al morir: al revivir se empieza una nueva.
        ClearMark();
    }

    // Cierra la apuesta en curso: suelta la suscripción a la muerte del marcado.
    // NO le quitamos el GE de la marca a mano — su propia duración se encarga (y si
    // el marcado murió, ya no importa).
    private void ClearMark()
    {
        if (_marked != null) _marked.OnDeath -= HandleMarkedDeath;
        _marked      = null;
        _markEndTime = 0f;
    }

    // Aplica una lista de GEs a un objetivo, usando al Pirata como fuente (para
    // escalados y crédito de la muerte). Ignora entradas nulas.
    private void ApplyAll(List<GameplayEffect> effects, AbilitySystemComponent target)
    {
        if (effects == null || target == null) return;
        foreach (GameplayEffect effect in effects)
            if (effect != null) target.ApplyGameplayEffect(effect, _asc);
    }

    // Vista previa del radio de búsqueda en el Editor.
    private void OnDrawGizmosSelected()
    {
        Transform origin = _asc != null ? _asc.transform : transform;
        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.35f);
        Gizmos.DrawWireSphere(origin.position, SearchRadius);
    }
}
