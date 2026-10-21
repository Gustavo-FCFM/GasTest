using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
// GA_GroundFissure  (genérico — una grieta que avanza por el piso, 21 de octubre de 2026)
//
// El Terremoto del Mago, como el de Spellbreak: desde los pies del lanzador sale una grieta que
// AVANZA por el piso hacia donde mira (se ve venir y se puede esquivar) y le aplica la lista de
// efectos a cada enemigo que pisa, una vez a cada uno.
//
//   · Sigue el terreno: sube escalones chicos ("Escalón que sube") y, si está arriba de algo,
//     baja por el borde al piso de abajo ("Baja hasta"). Una pared más alta, o el vacío, la corta.
//   · Pega PEGADA AL PISO: solo hasta "Alto" sobre el piso (0.5 m): el que salta alto la esquiva.
//   · Sus VFX "en el impacto" salen a lo largo del camino, cada "VFX cada" metros, en todas las
//     pantallas: así se ve la grieta abrirse.
//   · Lanzada VOLANDO: se corta el vuelo, el lanzador cae en picada y la grieta sale recién al
//     tocar el piso (el dueño se mueve solo: NetworkASC.ServerStartDive).
//
// Lo de los puntos arcanos (aturdir 0.5 s por punto y gastarlos) va en su lista de efectos
// ("Con los puntos del objetivo"), no acá. Corre en el servidor.
// ============================================================
[CreateAssetMenu(fileName = "GA_GroundFissure", menuName = "GAS/Generics/Ground Fissure")]
public class GA_GroundFissure : GameplayAbility
{
    [Section(AbilitySection.Shape)]
    [Tooltip("Hasta dónde avanza la grieta.")]
    [Label("Largo (m)")]
    public float Length = 12f;

    [Tooltip("Ancho de lo que pega, de lado a lado.")]
    [Label("Ancho (m)")]
    public float Width = 2f;

    [Tooltip("Hasta qué altura sobre el piso pega: el que salta más alto la esquiva.")]
    [Label("Alto (m)")]
    public float Height = 0.5f;

    [Tooltip("Qué tan rápido avanza.")]
    [Label("Velocidad (m/s)")]
    public float Speed = 20f;

    [Tooltip("Sube escalones de hasta esto; una pared más alta la corta.")]
    [Label("Escalón que sube (m)")]
    public float StepUp = 0.6f;

    [Tooltip("Si está arriba de algo, baja por el borde hasta el piso de abajo si está a esto o " +
             "menos; más alto (o el vacío), se corta.")]
    [Label("Baja hasta (m)")]
    public float MaxDrop = 8f;

    [Section("Grieta")]
    [Tooltip("Cada cuántos metros salen sus VFX 'en el impacto' a lo largo del camino.")]
    [Label("VFX cada (m)")]
    public float VfxSpacing = 1.5f;

    [Section("Desde el aire")]
    [Tooltip("Lanzada volando: se corta el vuelo, cae en picada a esta velocidad y la grieta sale " +
             "al tocar el piso.")]
    [Label("Picada (m/s)")]
    public float DiveSpeed = 30f;

    [Tooltip("Si no lo ve llegar al piso en este tiempo, la grieta sale igual, en el piso de abajo.")]
    [Label("Espera máxima del aterrizaje (s)")]
    public float DiveTimeout = 3f;

    // Ni personajes, ni paredes invisibles, ni UI: el piso y las paredes de verdad.
    private const int GroundMask = ~((1 << 7) | (1 << 2) | (1 << 5));

    public override bool SupportsVisualTiming(EVisualWhen when) => true;

    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;

        CommitAbility();
        if (OwnerASC == null) return;

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        Vector3 dir = FlatAimDirection(pc);
        if (pc != null)
        {
            pc.RotateToAim();
            pc.PlayAnimation(this);
        }

        if (OwnerASC.HasTag(EGameplayTag.Status_Flying))
        {
            OwnerASC.RemoveEffectsWithTag(EGameplayTag.Status_Flying);
            NetworkAbilitySystemComponent net = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
            if (net != null) net.ServerStartDive(DiveSpeed);
            OwnerASC.StartAbilityCoroutine(DiveThenFissure(dir));
        }
        else
        {
            OwnerASC.StartAbilityCoroutine(FissureRoutine(GroundBelow(OwnerASC.transform.position), dir));
        }

        // La grieta sigue sola: el lanzador queda libre.
        EndAbility();
    }

    // Hacia donde mira, en el plano.
    private Vector3 FlatAimDirection(PlayerController pc)
    {
        Vector3 origin = OwnerASC.transform.position;
        Vector3 dir = pc != null ? pc.GetAimPoint(Length) - origin : OwnerASC.transform.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) { dir = OwnerASC.transform.forward; dir.y = 0f; }
        return dir.sqrMagnitude < 0.001f ? Vector3.forward : dir.normalized;
    }

    private static Vector3 GroundBelow(Vector3 position)
    {
        return Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 50f, GroundMask,
                               QueryTriggerInteraction.Ignore) ? hit.point : position;
    }

    // Espera a ver al dueño en el piso (su posición viaja por red) y larga la grieta desde ahí.
    private IEnumerator DiveThenFissure(Vector3 dir)
    {
        yield return null;
        float waited = 0f;
        while (waited < DiveTimeout)
        {
            if (OwnerASC == null || OwnerASC.HasTag(EGameplayTag.State_Dead)) yield break;
            if (Physics.Raycast(OwnerASC.transform.position + Vector3.up * 0.2f, Vector3.down, 0.45f, GroundMask,
                                QueryTriggerInteraction.Ignore)) break;
            waited += Time.deltaTime;
            yield return null;
        }

        if (OwnerASC == null) yield break;
        yield return FissureRoutine(GroundBelow(OwnerASC.transform.position), dir);
    }

    private IEnumerator FissureRoutine(Vector3 start, Vector3 dir)
    {
        var struck = new HashSet<AbilitySystemComponent>();
        Quaternion rotation = Quaternion.LookRotation(dir);
        Vector3 current = start;
        float traveled = 0f;
        float nextVfx  = 0f;

        while (traveled < Length)
        {
            if (OwnerASC == null) yield break;

            float step = Mathf.Min(Mathf.Clamp(Speed * Time.deltaTime, 0.05f, 1f), Length - traveled);

            // Una pared más alta que un escalón la corta.
            if (Physics.Raycast(current + Vector3.up * StepUp, dir, step, GroundMask, QueryTriggerInteraction.Ignore))
                break;

            // El piso del tramo que sigue: sube un escalón o baja por un borde; al vacío, se corta.
            Vector3 probe = current + dir * step + Vector3.up * StepUp;
            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit ground, StepUp + MaxDrop, GroundMask,
                                 QueryTriggerInteraction.Ignore))
                break;

            HitSegment(current, ground.point, rotation, step, struck);

            if (traveled >= nextVfx)
            {
                BroadcastImpactVFX(current);
                nextVfx += Mathf.Max(0.2f, VfxSpacing);
            }

            current   = ground.point;
            traveled += step;
            yield return null;
        }
    }

    // Lo que pisa este tramo: una caja pegada al piso, de "Alto" de altura.
    private void HitSegment(Vector3 from, Vector3 to, Quaternion rotation, float step,
                            HashSet<AbilitySystemComponent> struck)
    {
        Vector3 center = (from + to) * 0.5f + Vector3.up * (Height * 0.5f);
        Vector3 half   = new Vector3(Width * 0.5f, Height * 0.5f, Mathf.Max(step, 0.2f) * 0.5f + 0.1f);

        foreach (Collider col in Physics.OverlapBox(center, half, rotation, TargetLayer, QueryTriggerInteraction.Collide))
        {
            AbilitySystemComponent target = col.GetComponentInParent<AbilitySystemComponent>();
            if (target == null || target == OwnerASC || struck.Contains(target)) continue;
            if (target.HasTag(EGameplayTag.State_Dead) || !IsEnemy(target)) continue;

            struck.Add(target);
            if (!ApplyHitEffects(target, firstHit: struck.Count == 1)) continue;
            BroadcastHitVFX(target, withSound: false);
            if (OwnerASC.CompareTag("Player")) ChargeUltimate();
        }
    }

    // Vista previa: el camino de la grieta, derecho hacia adelante.
    public override void DrawGizmos(Transform origin)
    {
        if (origin == null) return;
        Gizmos.color = new Color(0.65f, 0.45f, 0.2f, 0.5f);
        Gizmos.matrix = Matrix4x4.TRS(origin.position + origin.forward * (Length * 0.5f) + Vector3.up * (Height * 0.5f),
                                      origin.rotation, Vector3.one);
        Gizmos.DrawCube(Vector3.zero, new Vector3(Width, Height, Length));
        Gizmos.matrix = Matrix4x4.identity;
    }
}
