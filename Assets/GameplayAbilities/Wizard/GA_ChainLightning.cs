using System.Collections.Generic;
using UnityEngine;

// ============================================================
// GA_ChainLightning  (Descarga eléctrica — el Mago, 21 de octubre de 2026)
//
// Un rayo instantáneo (GA_HitscanShot) con un rayito azul que dibuja su trayectoria en todas
// las pantallas, pegue o no. Si el golpeado tiene PUNTOS ARCANOS, el daño REBOTA:
//
//   · Ramas (la forma de siempre): cada golpeado salta a tantos enemigos como puntos tenga, y
//     cada uno de esos vuelve a saltar según los suyos. A con 2 → B y C; B con 1 → D.
//   · Cadena: cada golpeado con al menos un punto salta a UNO (A → B → C...).
//
// A quién salta: al que tenga MÁS PUNTOS y, entre empatados, al MÁS CERCANO; a "Salto" metros
// o menos y VISIBLE desde el anterior (sin pared en medio: sus "Capas que frenan"), como si
// apuntara y disparara. Cada enemigo recibe un solo rayo por ataque, con el daño completo (la
// lista de efectos). Con 0 puntos no rebota. Los rebotes no gastan los puntos.
//
// ChainFrom es público: la Tormenta eléctrica (etapa 3) arranca sus cadenas desde ahí.
// ============================================================
[CreateAssetMenu(fileName = "GA_ChainLightning", menuName = "GAS/Specific Abilities/Wizard/Chain Lightning")]
public class GA_ChainLightning : GA_HitscanShot
{
    public enum EChainShape
    {
        [InspectorName("Ramas: salta a tantos como puntos tenga")] Branches,
        [InspectorName("Cadena: salta a uno, si tiene puntos")]   Chain,
    }

    [Section("Rebotes")]
    [Tooltip("El tag que da cada punto del golpeado (los puntos arcanos: Status_ArcanePoint).")]
    [Label("Tag de los puntos")]
    public EGameplayTag PointTag = EGameplayTag.Status_ArcanePoint;

    [Tooltip("Cómo rebota.\n\n" +
             "· Ramas: cada golpeado salta a tantos enemigos como puntos tenga (A con 2 → B y C).\n" +
             "· Cadena: cada golpeado con algún punto salta a uno solo (A → B → C).")]
    [Label("Forma")]
    public EChainShape Shape = EChainShape.Branches;

    [Tooltip("Hasta dónde salta un rebote desde el golpeado.")]
    [Label("Salto (m)")]
    public float JumpRange = 4f;

    [Tooltip("Tope de enemigos por ataque (el primero incluido), por si se junta una multitud.")]
    [Label("Máximo de golpeados")]
    public int MaxTargets = 12;

    [Tooltip("A qué altura sobre los pies salen y llegan los rebotes (y desde dónde se mira si " +
             "hay pared en medio).")]
    [Label("Altura del pecho (m)")]
    public float ChestHeight = 1.1f;

    [Section("Rayo")]
    [Tooltip("El prefab del rayito (con VFX_LightningBeam): uno por tramo, en todas las pantallas.")]
    [Label("Rayo (prefab)")]
    public GameObject BeamPrefab;

    protected override void OnShotResolved(Vector3 origin, Vector3 impactPoint, AbilitySystemComponent victim)
    {
        var from = new List<Vector3> { origin };
        var to   = new List<Vector3> { impactPoint };
        if (victim != null) ChainFrom(new List<AbilitySystemComponent> { victim }, from, to);
        BroadcastBeams(from.ToArray(), to.ToArray());
    }

    // Los rebotes desde estos ya golpeados (que no se vuelven a golpear). Agrega cada tramo a
    // from/to para dibujarlo. Servidor.
    public void ChainFrom(List<AbilitySystemComponent> struck, List<Vector3> from, List<Vector3> to)
    {
        if (OwnerASC == null || struck == null) return;

        var hit   = new HashSet<AbilitySystemComponent>(struck);
        var queue = new Queue<AbilitySystemComponent>(struck);

        while (queue.Count > 0 && hit.Count < MaxTargets)
        {
            AbilitySystemComponent current = queue.Dequeue();
            if (current == null) continue;

            int points = current.GetTagCount(PointTag);
            if (points <= 0) continue;
            int jumps = Shape == EChainShape.Chain ? 1 : points;

            Vector3 chest = Chest(current);
            List<AbilitySystemComponent> candidates = Candidates(chest, hit);
            for (int k = 0; k < candidates.Count && k < jumps && hit.Count < MaxTargets; k++)
            {
                AbilitySystemComponent next = candidates[k];
                hit.Add(next);
                ApplyHitEffects(next, firstHit: false);
                BroadcastHitVFX(next, withSound: false);
                if (OwnerASC.CompareTag("Player")) ChargeUltimate();

                from.Add(chest);
                to.Add(Chest(next));
                queue.Enqueue(next);
            }
        }
    }

    // Los enemigos a tiro de rebote desde 'from', vivos, visibles y sin golpear: primero los de
    // más puntos y, entre empatados, el más cercano.
    private List<AbilitySystemComponent> Candidates(Vector3 from, HashSet<AbilitySystemComponent> exclude)
    {
        var found = new List<AbilitySystemComponent>();
        foreach (Collider col in Physics.OverlapSphere(from, JumpRange, TargetLayer, QueryTriggerInteraction.Collide))
        {
            AbilitySystemComponent asc = col.GetComponentInParent<AbilitySystemComponent>();
            if (asc == null || asc == OwnerASC || exclude.Contains(asc) || found.Contains(asc)) continue;
            if (asc.HasTag(EGameplayTag.State_Dead) || !IsEnemy(asc)) continue;
            if (Physics.Linecast(from, Chest(asc), WallLayer, QueryTriggerInteraction.Ignore)) continue;   // pared en medio
            found.Add(asc);
        }

        found.Sort((a, b) =>
        {
            int byPoints = b.GetTagCount(PointTag).CompareTo(a.GetTagCount(PointTag));
            if (byPoints != 0) return byPoints;
            return (Chest(a) - from).sqrMagnitude.CompareTo((Chest(b) - from).sqrMagnitude);
        });
        return found;
    }

    private Vector3 Chest(AbilitySystemComponent asc) => asc.transform.position + Vector3.up * ChestHeight;

    // Cada pantalla dibuja los tramos con su copia de la habilidad.
    public override void PlayBeams(Vector3[] from, Vector3[] to)
    {
        if (BeamPrefab == null || from == null || to == null) return;
        int n = Mathf.Min(from.Length, to.Length);
        for (int i = 0; i < n; i++) VFX_LightningBeam.Play(BeamPrefab, from[i], to[i]);
    }
}
