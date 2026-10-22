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
// LA TORMENTA ELÉCTRICA DE LA EVOCACIÓN: si el rayo cruza un pararrayos (Summon_LightningRod: el tornado
// azul de la Tormenta de un Evocador, propio o aliado; la del kit base no lo lleva), se FRENA ahí y el tornado tira un rayo a cada
// enemigo a su alcance que vea (sin pared en medio), del más cercano al más lejano, con el daño
// completo; y cada uno de esos arranca su rebote. Igual: un solo rayo por enemigo por ataque.
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

    // El pararrayos que cruza el tiro de esta activación (lo encuentra StopDistance, lo usa
    // OnShotResolved). Servidor.
    [System.NonSerialized] private Summon_LightningRod _rodAhead;

    // El tiro se frena en el primer pararrayos propio o aliado que cruce.
    protected override float StopDistance(Vector3 origin, Vector3 dir, float maxDistance)
    {
        _rodAhead = Summon_LightningRod.FirstAlong(origin, dir, maxDistance, BulletRadius, OwnerASC, out float d);
        return _rodAhead != null ? d : maxDistance;
    }

    protected override void OnShotResolved(Vector3 origin, Vector3 impactPoint, AbilitySystemComponent victim)
    {
        var from   = new List<Vector3> { origin };
        var to     = new List<Vector3> { impactPoint };
        var struck = new List<AbilitySystemComponent>();
        if (victim != null) struck.Add(victim);
        else if (_rodAhead != null) RodStrike(_rodAhead, impactPoint, struck, from, to);   // le pegó al tornado
        _rodAhead = null;

        if (struck.Count > 0) ChainFrom(struck, from, to);
        BroadcastBeams(from.ToArray(), to.ToArray());
    }

    // El tornado reparte el rayo: uno a cada enemigo a su alcance que vea desde donde lo pegó, del
    // más cercano al más lejano (hasta el tope de golpeados), con el daño completo. Los agrega a
    // 'struck' (de ahí rebotan) y sus tramos a from/to.
    private void RodStrike(Summon_LightningRod rod, Vector3 at, List<AbilitySystemComponent> struck,
                           List<Vector3> from, List<Vector3> to)
    {
        rod.OnStruck();   // la Cadena de relámpagos: cada golpe la agranda (si crece)

        var found = new List<AbilitySystemComponent>();
        foreach (Collider col in Physics.OverlapSphere(at, rod.CurrentStrikeRange, TargetLayer, QueryTriggerInteraction.Collide))
        {
            AbilitySystemComponent asc = col.GetComponentInParent<AbilitySystemComponent>();
            if (asc == null || asc == OwnerASC || found.Contains(asc)) continue;
            if (asc.HasTag(EGameplayTag.State_Dead) || !IsEnemy(asc)) continue;
            if (Physics.Linecast(at, Chest(asc), WallLayer, QueryTriggerInteraction.Ignore)) continue;   // pared en medio
            found.Add(asc);
        }
        found.Sort((a, b) => (Chest(a) - at).sqrMagnitude.CompareTo((Chest(b) - at).sqrMagnitude));

        for (int i = 0; i < found.Count && struck.Count < MaxTargets; i++)
        {
            AbilitySystemComponent target = found[i];
            ApplyHitEffects(target, firstHit: struck.Count == 0);
            BroadcastHitVFX(target, withSound: false);
            if (OwnerASC.CompareTag("Player")) ChargeUltimate();

            from.Add(at);
            to.Add(Chest(target));
            struck.Add(target);
        }
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
