using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ============================================================
// GA_Dash  (Dash siniestro / genérico)
//
// Impulsa al jugador con gran velocidad hacia donde apunta, una distancia
// corta, frenando con inercia al final (para seguir desplazándose un poco).
// Durante el impulso atraviesa a otros jugadores (se excluye su capa de la
// colisión del CharacterController) pero sigue chocando con las paredes, y
// hace daño a todos los enemigos por los que pasa.
//
// Reparto de responsabilidades (igual que el salto):
//  - El DAÑO a lo largo del trayecto lo resuelve el servidor acá mismo, con
//    un barrido geométrico del recorrido (recortado por paredes) al activar.
//  - El MOVIMIENTO del CharacterController lo ejecuta el proceso dueño, vía
//    NetworkAbilitySystemComponent.ServerStartDash (el CC es client-authoritative).
//
// CARGAS Y RED: la habilidad puede tener varias cargas (MaxCharges). El estado
// de cargas se lleva SOLO en el servidor (Activate() es server-only, así que la
// instancia del dueño nunca lo tocaría). Para que la predicción del dueño sea
// correcta, el bloqueo se hace con el TAG de cooldown (que SÍ se sincroniza al
// dueño vía NetworkASC), no con un contador local: por eso solo aplicamos el
// CooldownEffect cuando se agota la ÚLTIMA carga. Con cargas disponibles no hay
// tag → el dueño puede volver a usarla. La recarga devuelve 1 carga cada
// RechargeTime, que además es la duración del cooldown que aplica al agotar la
// última carga (una sola fuente de verdad). Si un enemigo golpeado muere en una
// ventana corta, se reembolsa una carga.
// ============================================================
[CreateAssetMenu(fileName = "GA_Dash", menuName = "GAS/Generics/Dash")]
public class GA_Dash : GameplayAbility, IChargedAbility
{
    // Máximo de cargas, para que el HUD sepa el valor "lleno" (ver IChargedAbility).
    public int MaxChargeCount => MaxCharges;

    [Header("Movimiento del Dash")]
    [Tooltip("Distancia objetivo del dash (se recorta si hay una pared antes).")]
    public float DashDistance = 8f;
    [Tooltip("Velocidad del impulso. El frenado/inercia lo da la atenuación de PlayerController.")]
    public float DashSpeed = 30f;

    [Header("Colisión")]
    [Tooltip("Capas que FRENAN el dash (paredes/entorno). Se usa para recortar la distancia.")]
    public LayerMask WallLayer;
    [Tooltip("Capa de JUGADORES a atravesar durante el dash (se excluye de la colisión del CC).")]
    public LayerMask ExcludePlayerLayer;

    [Header("Daño en el Trayecto")]
    public GameplayEffect DamageEffect;
    // Efectos EXTRA que se aplican a cada enemigo atravesado, además del daño. Opcional.
    public List<GameplayEffect> AdditionalEffects;
    [Tooltip("Radio del barrido de daño a lo largo del recorrido.")]
    public float HitRadius = 1.2f;
    public GameObject HitVFX;

    [Header("Cargas")]
    public int   MaxCharges   = 2;
    [Tooltip("Segundos para recuperar 1 carga. También es la duración del cooldown que ve la UI " +
             "al quedarte sin cargas — una sola fuente, no hace falta tocar el Duration del GE.")]
    public float RechargeTime = 6f;

    [Header("Reinicio por Muerte")]
    [Tooltip("Si un enemigo golpeado por el dash muere dentro de esta ventana (seg), se recupera una carga.")]
    public float KillResetWindow = 2f;

    // Cargas disponibles en el SERVIDOR. -1 = sin inicializar (se toma como lleno).
    // NonSerialized: estado de runtime por instancia otorgada, no se guarda en el asset.
    [System.NonSerialized] private int  _charges = -1;
    [System.NonSerialized] private bool _recharging;

    // Valida (con el cooldown estándar, que se sincroniza al dueño), consume una
    // carga, resuelve el daño del trayecto y le pide al dueño que se impulse.
    public override void Activate()
    {
        if (!IsServer) return;
        if (_charges < 0) _charges = MaxCharges;

        // Gate estándar: incluye el tag de cooldown, que el dueño también ve
        // (NetTags), así su predicción coincide con la decisión del servidor.
        if (!CanActivate()) return;

        // Red de seguridad: si el tag ya expiró pero todavía no se recargó una
        // carga (RechargeTime ≠ Duration del cooldown), no dashamos y liberamos
        // al dueño para que no quede trabado en "atacando".
        if (_charges <= 0) { EndAbility(); return; }

        _charges--;
        ReportCharges();

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();

        // Costo (siempre). El cooldown se aplica SOLO al agotar la última carga
        // (ver nota de cabecera). Reproducimos también la VisualsSequence, como
        // haría CommitAbility.
        if (CostEffect != null) OwnerASC.ApplyGameplayEffect(CostEffect, this);

        if (_charges <= 0 && CooldownEffect != null)
        {
            // El cooldown que ve la UI al quedarte sin cargas = el tiempo de
            // recarga de una carga. Una sola fuente de verdad (RechargeTime): no
            // hay que igualar el Duration del GE ni configurar CooldownDuration.
            OwnerASC.ApplyGameplayEffect(CooldownEffect, this, RechargeTime);
        }

        if (VisualsSequence != null && VisualsSequence.Count > 0)
        {
            if (netAsc != null) netAsc.ServerPlayAbilityVisualsSequence(this);
            else OwnerASC.StartAbilityCoroutine(PlayVisualsSequence());
        }

        // Dirección: hacia la mira, aplanada al plano horizontal.
        Vector3 origin   = OwnerASC.transform.position;
        Vector3 aimPoint = pc != null ? pc.GetAimPoint() : origin + OwnerASC.transform.forward * DashDistance;
        Vector3 dir = aimPoint - origin; dir.y = 0;
        if (dir.sqrMagnitude < 0.0001f) dir = OwnerASC.transform.forward;
        dir.Normalize();

        // Recorte por paredes: no aplicamos daño (ni impulsamos) más allá de un
        // muro. El movimiento real del CC también choca con las paredes, así que
        // ambos se detienen aproximadamente en el mismo punto.
        float dist = DashDistance;
        Vector3 castOrigin = origin + Vector3.up * 0.5f;
        if (Physics.SphereCast(castOrigin, HitRadius * 0.9f, dir, out RaycastHit wallHit, DashDistance, WallLayer, QueryTriggerInteraction.Ignore))
            dist = Mathf.Max(0f, wallHit.distance);

        // Daño a lo largo del recorrido (server-authoritative).
        List<AbilitySystemComponent> hitEnemies = ResolvePathDamage(origin, dir, dist);

        // Impulso en el proceso dueño (atraviesa jugadores, frena con inercia).
        float duration = Mathf.Clamp(dist / Mathf.Max(1f, DashSpeed), 0.1f, 0.6f);
        if (netAsc != null)
            netAsc.ServerStartDash(dir * DashSpeed, duration, ExcludePlayerLayer.value);
        else if (pc != null)
            pc.ApplyAbilityVelocity(dir * DashSpeed, 0f); // fallback sin red (no gestiona el fin del impulso)

        if (pc != null) pc.PlayAnimation(AnimationTriggerName, AnimationID);

        // Recarga de cargas y vigilancia de muertes para el reembolso.
        StartRecharge();
        if (hitEnemies.Count > 0)
            OwnerASC.StartAbilityCoroutine(WatchForKills(hitEnemies));

        // NO llamamos EndAbility() en el caso normal: el fin del dash (restaurar
        // colisión, soltar el impulso y liberar isAttacking) lo maneja el dueño en
        // NetworkAbilitySystemComponent.DashRoutine cuando termina la duración.
    }

    // Aplica DamageEffect a todos los enemigos dentro de una cápsula que recorre
    // el trayecto del dash (desde el origen hasta la distancia recortada) y
    // devuelve la lista de golpeados (para el reembolso por muerte).
    private List<AbilitySystemComponent> ResolvePathDamage(Vector3 origin, Vector3 dir, float dist)
    {
        var hitList = new List<AbilitySystemComponent>();
        if (dist <= 0f) return hitList;

        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
        Vector3 p0 = origin + Vector3.up * 0.5f;
        Vector3 p1 = p0 + dir * dist;

        Collider[] cols = Physics.OverlapCapsule(p0, p1, HitRadius, TargetLayer);
        var seen = new HashSet<AbilitySystemComponent>();

        foreach (var c in cols)
        {
            AbilitySystemComponent asc = c.GetComponentInParent<AbilitySystemComponent>();
            if (asc == null || ReferenceEquals(asc, OwnerASC) || !IsEnemy(asc) || seen.Contains(asc)) continue;
            seen.Add(asc);

            if (DamageEffect != null) asc.ApplyGameplayEffect(DamageEffect, OwnerASC);
            ApplyEffectsTo(AdditionalEffects, asc);
            ChargeUltimate();

            Vector3 hitPos = asc.transform.position + Vector3.up;
            if (netAsc != null) netAsc.ServerPlayAbilityVFX(this, hitPos);
            else PlayImpactVFX(hitPos);

            hitList.Add(asc);
        }
        return hitList;
    }

    // Reporta las cargas actuales a la capa de red para que la UI del dueño las
    // muestre (ver NetworkAbilitySystemComponent.ServerReportCharges).
    private void ReportCharges()
    {
        if (OwnerASC == null) return;
        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
        if (netAsc != null) netAsc.ServerReportCharges(this, _charges);
    }

    // Arranca la corutina de recarga si no está corriendo ya.
    private void StartRecharge()
    {
        if (_recharging || OwnerASC == null) return;
        OwnerASC.StartAbilityCoroutine(RechargeRoutine());
    }

    // Devuelve 1 carga cada RechargeTime hasta llenar MaxCharges.
    private IEnumerator RechargeRoutine()
    {
        _recharging = true;
        while (_charges < MaxCharges)
        {
            yield return new WaitForSeconds(RechargeTime);
            if (_charges < MaxCharges) { _charges++; ReportCharges(); }
        }
        _recharging = false;
    }

    // Vigila a los enemigos golpeados por este dash: si alguno muere dentro de
    // KillResetWindow, reembolsa una carga y limpia el tag de cooldown (para que
    // el dueño pueda volver a usar la habilidad de inmediato) — el "reinicio" del doc.
    private IEnumerator WatchForKills(List<AbilitySystemComponent> enemies)
    {
        float elapsed = 0f;
        while (elapsed < KillResetWindow)
        {
            foreach (var e in enemies)
            {
                if (e != null && e.HasTag(EGameplayTag.State_Dead))
                {
                    if (_charges < MaxCharges) _charges++;
                    if (CooldownEffect != null && CooldownEffect.GrantedTags.Count > 0)
                        OwnerASC.ReduceCooldownByTag(CooldownEffect.GrantedTags[0], 99999f);
                    ReportCharges();
                    StartRecharge();
                    yield break;
                }
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    // Instancia HitVFX en la posición de impacto. La llama cada peer con su
    // propia copia (ver NetworkAbilitySystemComponent.ServerPlayAbilityVFX).
    public override void PlayImpactVFX(Vector3 position)
    {
        if (HitVFX == null) return;
        GameObject vfx = Instantiate(HitVFX, position, Quaternion.identity);
        Destroy(vfx, 1.5f);
    }

    // Vista previa del trayecto del dash en el Editor (línea hacia adelante con
    // el radio de golpe).
    public override void DrawGizmos(Transform origin)
    {
        if (origin == null) return;

        Vector3 p0 = origin.position + Vector3.up * 0.5f;
        Vector3 p1 = p0 + origin.forward * DashDistance;

        Gizmos.color = new Color(0.2f, 0.9f, 0.9f, 0.9f);
        Gizmos.DrawLine(p0, p1);
        Gizmos.DrawWireSphere(p0, HitRadius);
        Gizmos.DrawWireSphere(p1, HitRadius);
    }
}
