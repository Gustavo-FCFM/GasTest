using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ============================================================
// GA_RushAttack  (genérico — embestida larga que se puede guiar y cortar)
//
// Como la Carga de Reinhardt (Overwatch): sale disparado hacia donde mira, a velocidad
// CONSTANTE y una distancia larga; mientras dura, izquierda/derecha la tuercen un poco
// (TurnRate) y volver a apretar el botón la corta en el acto (por si apuntaste a un
// precipicio). Choca con las paredes, y ahí termina. La usa la Patada voladora del Monje.
//
// DIFERENCIA CON GA_Dash: el dash resuelve el daño de TODO el trayecto al activar (es
// corto y rápido, no hay tiempo de cambiar nada). Acá el trayecto no se conoce de
// antemano (se tuerce y se puede cortar), así que el servidor mira cada frame quién
// está cerca del dueño y le pega al llegar. Con StopAtFirstEnemy, el primero que toca
// recibe el golpe (y los FirstHitEffects: el aturdido de la Patada del dragón) y la
// embestida termina ahí; sin eso atraviesa y le pega a todos una vez.
//
// RED: el movimiento lo hace el dueño (el transform es suyo): NetworkASC.ServerStartRush
// le manda la velocidad, la duración y el giro; él avisa si la cortó o chocó con una
// pared (ServerRequestStopRush), y el servidor le avisa si frenó en un enemigo.
// Los bots no la usan (MovesThroughOwner).
//
// ANIMACIÓN: AnimationClip suelto al salir, como cualquier habilidad. Si además tiene
// RushLoopClip, esa pose se sostiene mientras dura (ranuras del canalizado: no hay
// estados nuevos que crear en el Animator).
// ============================================================
[CreateAssetMenu(fileName = "GA_RushAttack", menuName = "GAS/Generics/Rush Attack")]
public class GA_RushAttack : GameplayAbility, IChanneledAbility
{
    [Header("Movimiento")]
    [Tooltip("Metros que recorre si nada la corta.")]
    public float Distance = 14f;

    [Tooltip("Metros por segundo, constantes. Más bajo = más larga y más fácil de guiar.")]
    public float Speed = 10f;

    [Tooltip("Grados por segundo que la tuercen izquierda/derecha mientras dura. 0 = recta.")]
    public float TurnRate = 70f;

    [Tooltip("Volver a apretar el botón la corta en el acto.")]
    public bool Cancelable = true;

    [Tooltip("Capas que atraviesa mientras dura (los jugadores), sin dejar de chocar con las paredes.")]
    public LayerMask ExcludePlayerLayer;

    [Header("Golpe")]
    public GameplayEffect DamageEffect;

    [Tooltip("Efectos extra a cada enemigo golpeado.")]
    public List<GameplayEffect> AdditionalEffects = new List<GameplayEffect>();

    [Tooltip("Efectos solo al PRIMER enemigo golpeado (el aturdido de la Patada del dragón).")]
    public List<GameplayEffect> FirstHitEffects = new List<GameplayEffect>();

    [Tooltip("Radio alrededor del dueño en el que un enemigo cuenta como alcanzado.")]
    public float HitRadius = 1.2f;

    [Tooltip("Termina en el primer enemigo que toca. Apagado = lo atraviesa y sigue (cada enemigo, una vez).")]
    public bool StopAtFirstEnemy = true;

    [Tooltip("VFX de impacto en cada enemigo golpeado (en todas las pantallas).")]
    public GameObject HitVFX;

    [Header("Animación")]
    [Tooltip("OPCIONAL: pose EN BUCLE mientras dura (la patada extendida). Vacío = solo el AnimationClip suelto.")]
    public AnimationClip RushLoopClip;

    public AnimationClip ChannelStartClip => null;
    public AnimationClip ChannelLoopClip  => RushLoopClip;
    public AnimationClip ChannelEndClip   => null;
    public float         SpinSpeed        => 0f;

    public override bool MovesThroughOwner => true;

    // Lo que el servidor sigue mirando después de la duración: la posición del dueño le
    // llega con un poco de atraso, y sin esto el último tramo nunca golpearía.
    private const float LatencyGrace = 0.2f;

    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
        if (netAsc == null)
        {
            EndAbility();
            return;
        }

        CommitAbility();

        // Hacia donde mira, en el plano: una embestida no sube ni baja.
        Vector3 origin = OwnerASC.transform.position;
        Vector3 dir = pc != null ? pc.GetAimPoint() - origin : OwnerASC.transform.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = OwnerASC.transform.forward;
        dir.Normalize();

        float speed    = Mathf.Max(1f, Speed);
        float duration = Mathf.Max(0.1f, Distance / speed);

        netAsc.ServerStartRush(dir * speed, duration, TurnRate, ExcludePlayerLayer.value, Cancelable);

        if (RushLoopClip != null) netAsc.ServerPlayChannelAnimation(this, true);
        if (pc != null) pc.PlayAnimation(this);

        OwnerASC.StartAbilityCoroutine(RushRoutine(netAsc, duration));
    }

    // Servidor: cada frame, quién está cerca del dueño. Termina al cumplirse el tiempo, si
    // el dueño la cortó (o chocó), si lo aturden, lo enraízan o muere, o en el primer enemigo.
    private IEnumerator RushRoutine(NetworkAbilitySystemComponent netAsc, float duration)
    {
        var hit = new HashSet<AbilitySystemComponent>();
        float elapsed = 0f;

        while (elapsed < duration + LatencyGrace)
        {
            if (OwnerASC == null) yield break;
            if (OwnerASC.HasTag(EGameplayTag.State_Dead) || OwnerASC.HasTag(EGameplayTag.State_Stunned) ||
                OwnerASC.HasTag(EGameplayTag.State_Rooted))
                break;
            if (netAsc.ConsumeRushStopRequest()) break;

            if (HitAround(hit) && StopAtFirstEnemy) break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Si la terminó el servidor (enemigo, aturdido) el dueño todavía se está moviendo.
        netAsc.ServerStopRush();
        if (RushLoopClip != null) netAsc.ServerPlayChannelAnimation(this, false);
        EndAbility();
    }

    // Golpea a los enemigos nuevos alrededor del dueño. true = golpeó a alguien.
    private bool HitAround(HashSet<AbilitySystemComponent> hit)
    {
        Vector3 center = OwnerASC.transform.position + Vector3.up;
        Collider[] cols = Physics.OverlapSphere(center, HitRadius, TargetLayer);

        // El más cercano primero: con StopAtFirstEnemy es a ese al que le pega.
        System.Array.Sort(cols, (a, b) =>
            (a.transform.position - center).sqrMagnitude.CompareTo((b.transform.position - center).sqrMagnitude));

        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
        bool any = false;

        foreach (Collider c in cols)
        {
            AbilitySystemComponent asc = c.GetComponentInParent<AbilitySystemComponent>();
            if (asc == null || ReferenceEquals(asc, OwnerASC) || !IsEnemy(asc) || hit.Contains(asc)) continue;
            if (asc.HasTag(EGameplayTag.State_Dead)) continue;

            bool first = hit.Count == 0;
            hit.Add(asc);
            any = true;

            if (DamageEffect != null) asc.ApplyGameplayEffect(DamageEffect, OwnerASC);
            ApplyEffectsTo(AdditionalEffects, asc);
            if (first) ApplyEffectsTo(FirstHitEffects, asc);
            ChargeUltimate();

            Vector3 hitPos = asc.transform.position + Vector3.up;
            if (netAsc != null) netAsc.ServerPlayAbilityVFX(this, hitPos);
            else PlayImpactVFX(hitPos);

            if (StopAtFirstEnemy) break;
        }
        return any;
    }

    public override void PlayImpactVFX(Vector3 position)
    {
        if (HitVFX == null) return;
        GameObject vfx = Instantiate(HitVFX, position, Quaternion.identity);
        Destroy(vfx, 1.5f);
    }

    public override void DrawGizmos(Transform origin)
    {
        if (origin == null) return;
        Vector3 p0 = origin.position + Vector3.up;
        Vector3 p1 = p0 + origin.forward * Distance;
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.9f);
        Gizmos.DrawLine(p0, p1);
        Gizmos.DrawWireSphere(p0, HitRadius);
        Gizmos.DrawWireSphere(p1, HitRadius);
    }
}
