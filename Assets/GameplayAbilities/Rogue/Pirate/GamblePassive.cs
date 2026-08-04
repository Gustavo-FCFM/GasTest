using UnityEngine;
using System;
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
// VARIAS APUESTAS A LA VEZ: puede haber muchas apuestas abiertas en paralelo, cada
// una con su propio vencimiento. La pasiva mantiene UNA apuesta automática (la de su
// intervalo), y la ultimate (Cañones) abre una por cada enemigo al que le pegue, sin
// pisar la automática ni entre ellas. Cada apuesta ganada cobra sus WinEffects, así
// que rematar a varios marcados de una cobra varias veces (regulalo con la
// StackingPolicy del GE si no querés que se acumule).
//
// El "daño aumentado" es SOLO del Pirata contra sus propios marcados — por eso es un
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

    // UNA apuesta abierta contra un enemigo concreto.
    private class Bet
    {
        public AbilitySystemComponent Target;
        // Cuándo vence ESTA apuesta. Llevamos el tiempo nosotros en vez de preguntar por
        // el tag Status_Gambled del objetivo: los tags del ASC son un CONTEO, así que si
        // otro Pirata también le apostó al mismo enemigo, el tag sigue puesto por la marca
        // del otro y daríamos la nuestra por viva (seguiríamos pegando de más y podríamos
        // cobrar una apuesta ya vencida).
        public float EndTime;
        // Handler suscrito a Target.OnDeath. Lo guardamos porque OnDeath no dice QUIÉN
        // murió: cada apuesta se suscribe con su propio closure para saber cuál se resolvió
        // (y para poder desuscribirse exactamente de esa).
        public Action DeathHandler;
        // True si la abrió el marcado automático de la pasiva (no la ultimate).
        public bool IsAuto;

        public bool IsLive => Target != null && Time.time < EndTime;
    }

    // ASC del Pirata (este componente vive en un hijo suyo).
    private AbilitySystemComponent _asc;

    // Apuestas abiertas ahora mismo (solo server).
    private readonly List<Bet> _bets = new List<Bet>();

    private void Awake() => _asc = GetComponentInParent<AbilitySystemComponent>();

    private void OnEnable()
    {
        if (_asc == null) return;

        _asc.RegisterDamageModifier(this);
        // Para detectar la apuesta PERDIDA: si el Pirata muere, miramos si lo mató
        // justo alguno de los enemigos a los que les había apostado.
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
        // Cambio de clase / destrucción: soltamos las suscripciones para no dejar
        // callbacks colgando sobre enemigos que siguen vivos.
        CloseAllBets();
    }

    // =========================================================
    // DAÑO AUMENTADO CONTRA LOS MARCADOS
    // =========================================================

    // Amplifica el daño del Pirata contra cualquier enemigo con una apuesta suya viva.
    // Magnitude es negativa (es daño), así que multiplicarla por >1 pega más fuerte. No
    // aplica a ticks de DoT, por consistencia con las demás pasivas de daño.
    public void ModifyOutgoingDamage(ref DamageContext ctx)
    {
        if (ctx.IsPeriodicTick || ctx.Target == null) return;
        if (FindLiveBet(ctx.Target) == null) return;

        ctx.Magnitude *= Mathf.Max(1f, DamageMultiplier);
    }

    // =========================================================
    // CICLO DE LA APUESTA
    // =========================================================

    // Cada MarkInterval segundos, si no hay una apuesta AUTOMÁTICA en curso, elige un
    // enemigo cercano al azar y le apuesta. Mira solo la automática: las que reparte la
    // ultimate no deben cortarle el ritmo a la pasiva. Corre en todos los peers pero
    // solo actúa en el servidor (la marca es un GE: aplicarlo es autoridad de servidor).
    private IEnumerator GambleRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.5f, MarkInterval));

        while (true)
        {
            yield return wait;

            if (!InstanceFinder.IsServerStarted) continue;
            if (_asc == null || _asc.HasTag(EGameplayTag.State_Dead)) continue;

            // Cierra las apuestas que vencieron solas (nadie murió).
            PruneExpiredBets();

            if (HasLiveAutoBet()) continue; // la automática sigue en curso

            AbilitySystemComponent target = PickRandomNearbyEnemy();
            if (target != null) ServerMarkTarget(target, isAuto: true);
        }
    }

    // Apuesta por un enemigo concreto, SIN cerrar las demás apuestas abiertas. Público
    // para que la ultimate del Pirata (Cañones) le apueste a todos los que golpea,
    // además del marcado automático de acá.
    public void ServerMarkTarget(AbilitySystemComponent target, bool isAuto = false)
    {
        if (!InstanceFinder.IsServerStarted) return;
        if (_asc == null || target == null || MarkEffect == null) return;
        if (!_asc.IsEnemyOf(target) || target.HasTag(EGameplayTag.State_Dead)) return;
        // Ya le estamos apostando a ESE mismo y la apuesta sigue viva: no la reiniciamos
        // (si no, los impactos repetidos de Cañones la refrescarían para siempre). Si ya
        // venció, seguimos de largo y se le vuelve a apostar normalmente.
        if (FindLiveBet(target) != null) return;

        PruneExpiredBets();

        Bet bet = new Bet
        {
            Target  = target,
            EndTime = Time.time + MarkEffect.Duration,
            IsAuto  = isAuto,
        };
        // Para detectar la apuesta GANADA: nos avisa cuando ESTE marcado muere, y ahí
        // miramos si fue el Pirata quien lo mató.
        bet.DeathHandler = () => HandleMarkedDeath(bet);
        target.OnDeath += bet.DeathHandler;

        _bets.Add(bet);
        target.ApplyGameplayEffect(MarkEffect, _asc);
    }

    // Enemigo vivo AL AZAR dentro de SearchRadius (el diseño pide aleatorio, no el
    // más cercano) que no tenga ya una apuesta viva. Devuelve null si no hay ninguno.
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
            if (FindLiveBet(asc) != null) continue; // ya tiene una apuesta nuestra viva
            candidates.Add(asc);
        }

        if (candidates.Count == 0) return null;
        // UnityEngine.Random explícito: este archivo tiene 'using System', que también
        // trae System.Random y dejaría 'Random' ambiguo.
        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    // =========================================================
    // RESOLUCIÓN
    // =========================================================

    // Murió un enemigo marcado. Si lo mató el Pirata, cobramos ESA apuesta; si lo
    // mató otro, se cierra sin premio. Las demás apuestas siguen abiertas.
    private void HandleMarkedDeath(Bet bet)
    {
        if (!InstanceFinder.IsServerStarted) { CloseBet(bet); return; }

        // Solo cobramos si la apuesta seguía vigente Y lo mató el Pirata. LastAttacker
        // lo anota el ASC en cada golpe que baja vida (server-side).
        bool wonByOwner = bet.IsLive && ReferenceEquals(bet.Target.LastAttacker, _asc);

        if (wonByOwner) ApplyAll(WinEffects, _asc);

        CloseBet(bet);
    }

    // Murió el Pirata. Si el que lo mató es justo un enemigo al que le había apostado,
    // ese enemigo se lleva el premio de consolación.
    private void HandleOwnerDeath()
    {
        if (!InstanceFinder.IsServerStarted) { CloseAllBets(); return; }

        AbilitySystemComponent killer = _asc != null ? _asc.LastAttacker : null;
        if (killer != null && FindLiveBet(killer) != null)
            ApplyAll(LossEffects, killer);

        // Todas las apuestas se cierran al morir: al revivir se empieza de cero.
        CloseAllBets();
    }

    // =========================================================
    // REGISTRO DE APUESTAS
    // =========================================================

    // La apuesta viva contra ese objetivo, o null si no hay.
    private Bet FindLiveBet(AbilitySystemComponent target)
    {
        if (target == null) return null;
        for (int i = 0; i < _bets.Count; i++)
            if (_bets[i].IsLive && ReferenceEquals(_bets[i].Target, target)) return _bets[i];
        return null;
    }

    private bool HasLiveAutoBet()
    {
        for (int i = 0; i < _bets.Count; i++)
            if (_bets[i].IsAuto && _bets[i].IsLive) return true;
        return false;
    }

    // Cierra una apuesta: suelta la suscripción a la muerte de su objetivo y la saca
    // del registro. NO le quitamos el GE de la marca a mano — su propia duración se
    // encarga (y si el marcado murió, ya no importa).
    private void CloseBet(Bet bet)
    {
        if (bet == null) return;
        if (bet.Target != null) bet.Target.OnDeath -= bet.DeathHandler;
        _bets.Remove(bet);
    }

    // Cierra las apuestas ya vencidas (nadie murió a tiempo).
    private void PruneExpiredBets()
    {
        for (int i = _bets.Count - 1; i >= 0; i--)
            if (!_bets[i].IsLive) CloseBet(_bets[i]);
    }

    private void CloseAllBets()
    {
        for (int i = _bets.Count - 1; i >= 0; i--) CloseBet(_bets[i]);
        _bets.Clear();
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
