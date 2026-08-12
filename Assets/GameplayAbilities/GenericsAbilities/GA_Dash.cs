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
// tag → el dueño puede volver a usarla.
//
// COOLDOWN = RECARGA POR CARGA: el mismo cooldown de la habilidad
// (ResolveCooldownDuration → CooldownDuration del GA, o AtkSpeed, o el Duration
// del CooldownEffect) es también lo que tarda en volver CADA carga. Un solo
// valor define ambos: gastás una carga y esa carga vuelve en un cooldown. Al
// recuperar una carga se limpia el tag para que el dueño pueda usarla al
// instante (sin esperar a que el GE expire por su cuenta). Si un enemigo
// golpeado muere en una ventana corta, se reembolsa una carga.
// ============================================================
[CreateAssetMenu(fileName = "GA_Dash", menuName = "GAS/Generics/Dash")]
public class GA_Dash : GameplayAbility
{
    [Header("Movimiento del Dash")]
    [Tooltip("Distancia objetivo del dash (se recorta si hay una pared antes).")]
    public float DashDistance = 8f;
    [Tooltip("Velocidad del impulso. El frenado/inercia lo da la atenuación de PlayerController.")]
    public float DashSpeed = 30f;
    [Tooltip("Esquiva direccional (estilo shooter): el dash va hacia donde APUNTA el " +
             "movimiento (WASD/stick) y el cuerpo NO gira — sigue mirando a la mira, así " +
             "podés esquivar de costado o hacia atrás sin dejar de encarar al enemigo. " +
             "Si el jugador está quieto, cae a la dirección de la mira (dash hacia adelante). " +
             "Desactivado = comportamiento clásico: dashea hacia la mira y encara esa dirección.")]
    public bool DirectionalDodge = true;

    [Tooltip("Solo con Directional Dodge: qué tan 'hacia adelante' (alineado con la mira) " +
             "debe ir el dash para tomar el ÁNGULO VERTICAL de la mira y elevarte al apuntar " +
             "arriba. 1 = solo adelante exacto; 0.5 ≈ incluye diagonales hacia adelante. Los " +
             "costados y hacia atrás siempre quedan horizontales (no elevan).")]
    [Range(0f, 1f)]
    public float ForwardDashElevationDot = 0.5f;

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

    [Header("Reinicio por Muerte")]
    [Tooltip("Si un enemigo golpeado por el dash muere dentro de esta ventana (seg), se recupera una carga.")]
    public float KillResetWindow = 2f;

    // Valida (con el cooldown estándar, que se sincroniza al dueño), consume una
    // carga, resuelve el daño del trayecto y le pide al dueño que se impulse.
    public override void Activate()
    {
        if (!IsServer) return;

        // Gate estándar: incluye el tag de cooldown (que el dueño también ve por
        // NetTags, así su predicción coincide con el servidor) y las cargas.
        if (!CanActivate()) return;

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();

        // Gasta la carga, cobra el costo, aplica el cooldown solo si era la última, y
        // reproduce la VisualsSequence. Todo eso vive en la clase base.
        CommitAbility();

        // Dirección del dash.
        Vector3 origin = OwnerASC.transform.position;

        // Dirección de la mira en 3D (incluye ángulo vertical): se usa para el
        // fallback estático y para el dash hacia adelante que sí se eleva.
        Vector3 aimPoint = pc != null ? pc.GetAimPoint() : origin + OwnerASC.transform.forward * DashDistance;
        Vector3 aimDir = aimPoint - origin;
        if (aimDir.sqrMagnitude < 0.0001f) aimDir = OwnerASC.transform.forward;
        aimDir.Normalize();

        Vector3 dir;

        // Esquiva direccional: hacia donde se MUEVE el jugador (WASD/stick en
        // espacio de mundo, plano horizontal). El cuerpo NO gira hacia el dash
        // (faceVelocity=false más abajo): sigue encarando a la mira, que
        // RequestAbility ya orientó — así esquivar de costado/atrás no te da vuelta.
        Vector3 moveDir = (DirectionalDodge && pc != null) ? pc.GetMoveDirection() : Vector3.zero;
        if (moveDir.sqrMagnitude > 0.0001f)
        {
            // Por defecto la esquiva es horizontal (de costado o hacia atrás NO
            // eleva). Excepción: si el dash es hacia ADELANTE —el movimiento está
            // alineado con la mira— tomamos el ángulo vertical de la mira para
            // poder elevarte apuntando hacia arriba.
            Vector3 aimFlat = new Vector3(aimDir.x, 0f, aimDir.z);
            bool forward = aimFlat.sqrMagnitude > 0.0001f &&
                           Vector3.Dot(moveDir, aimFlat.normalized) >= ForwardDashElevationDot;
            dir = forward ? aimDir : moveDir;
        }
        else
        {
            // Quieto: hacia la mira en 3D (mira arriba/abajo sí importa), como antes.
            dir = aimDir;
        }

        // El cuerpo solo encara la dirección del dash en el modo clásico. En la
        // esquiva direccional se mantiene mirando a la mira.
        bool faceDashDir = !DirectionalDodge;

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
            netAsc.ServerStartDash(dir * DashSpeed, duration, ExcludePlayerLayer.value, faceDashDir);
        else if (pc != null)
            pc.ApplyDashVelocity(dir * DashSpeed, faceDashDir); // fallback sin red (no gestiona el fin del impulso)

        if (pc != null) pc.PlayAnimation(this);

        // La recarga ya la arrancó CommitAbility; acá solo queda vigilar las muertes
        // para el reembolso.
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
        var candidates = new List<AbilitySystemComponent>();

        foreach (var c in cols)
        {
            AbilitySystemComponent asc = c.GetComponentInParent<AbilitySystemComponent>();
            if (asc == null || ReferenceEquals(asc, OwnerASC) || !IsEnemy(asc) || !seen.Add(asc)) continue;
            candidates.Add(asc);
        }

        // Golpeamos en el ORDEN REAL en que el dash los atraviesa (proyectando su
        // posición sobre la dirección del dash). El daño le llega a todos igual,
        // pero el orden importa para los efectos de un solo uso que se consumen en
        // el primer golpe: así el crítico asegurado de Emboscada sombría le cae al
        // PRIMER enemigo atravesado, y no a uno al azar (Physics.OverlapCapsule
        // devuelve los colliders sin ningún orden garantizado).
        if (candidates.Count > 1)
        {
            candidates.Sort((a, b) =>
                Vector3.Dot(a.transform.position - origin, dir)
                .CompareTo(Vector3.Dot(b.transform.position - origin, dir)));
        }

        foreach (var asc in candidates)
        {
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

    // Vigila a los enemigos golpeados por este dash: si alguno muere dentro de
    // KillResetWindow, reembolsa una carga (RefundCharge, en la clase base, ya limpia
    // el tag de cooldown para que quede usable al instante) — el "reinicio" del doc.
    private IEnumerator WatchForKills(List<AbilitySystemComponent> enemies)
    {
        float elapsed = 0f;
        while (elapsed < KillResetWindow)
        {
            foreach (var e in enemies)
            {
                if (e != null && e.HasTag(EGameplayTag.State_Dead))
                {
                    RefundCharge();
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
