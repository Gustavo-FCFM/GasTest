using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using UnityEngine;

// ============================================================
// GA_BombBarrage  (Bombas súper explosivas — definitiva del Trampero, 15 de octubre de 2026)
//
// Se apunta una zona (el marcador del piso, como las áreas) y caen bombas de pólvora negra en
// ella durante "Caen durante". Cada bomba es una TRAMPA (Entity_Trap): explota si la pisa un
// jugador enemigo o al vencerse (las bombas duran menos que las trampas normales).
//
// Cuántas caen y cómo se reparten se ajusta como las zonas del Aliento de dragón:
//   · Al azar en el círculo: "Cantidad de bombas" en puntos al azar.
//   · En anillo: "Cantidad de bombas" repartidas en el borde.
//   · Rellenar el círculo: las que hagan falta para cubrirlo a "Separación" entre centros
//     (la cantidad la deciden el radio y la separación; el gizmo las muestra).
// ============================================================
[CreateAssetMenu(fileName = "GA_BombBarrage", menuName = "GAS/Specific Abilities/Ranger/Trapper/Bomb Barrage")]
public class GA_BombBarrage : GameplayAbility, IGroundTargetAbility
{
    public enum EBombLayout
    {
        [InspectorName("Al azar en el círculo")] Random   = 0,
        [InspectorName("En anillo")]             Ring     = 1,
        [InspectorName("Rellenar el círculo")]   FillCircle = 2,
    }

    [Section(AbilitySection.Targeting)]
    [Tooltip("Hasta dónde se puede marcar la zona.")]
    [Label("Alcance máximo (m)")]
    public float MaxRange = 30f;

    [Tooltip("Radio del círculo donde caen las bombas (también el tamaño del marcador).")]
    [Label("Radio del círculo (m)")]
    public float CircleRadius = 7f;

    [Section("Las bombas")]
    [Tooltip("El prefab de la bomba (Entity_Trap + NetworkObject, con \"Explota al vencerse\").")]
    [Label("Prefab de la bomba")]
    public GameObject BombPrefab;

    [Label("Distribución de las bombas")]
    public EBombLayout Layout = EBombLayout.Random;

    [ShowIf(nameof(Layout), EBombLayout.FillCircle, true)]
    [Min(1)]
    [Label("Cantidad de bombas")]
    public int BombCount = 6;

    [ShowIf(nameof(Layout), EBombLayout.FillCircle)]
    [Tooltip("Distancia entre los centros de dos bombas vecinas al rellenar el círculo. Más chica = más bombas.")]
    [Label("Separación (m)")]
    public float Spacing = 3f;

    [Tooltip("En cuántos segundos caen todas (una detrás de otra, repartidas en ese tiempo).")]
    [Label("Caen durante (s)")]
    public float DropSeconds = 2f;

    public float MaxTargetRange   => MaxRange;
    public float TargetRadius     => CircleRadius;
    public bool  UsesGroundTarget => true;

    public override bool UsesTargetLayer => false;
    public override bool UsesHitEffects  => false;

    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;
        if (BombPrefab == null)
        {
            Debug.LogWarning($"[{AbilityName}] no tiene Prefab de la bomba.");
            EndAbility();
            return;
        }

        CommitAbility();

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        Vector3 origin = OwnerASC.transform.position;
        Vector3 center = pc != null ? pc.GetAimPoint(MaxRange) : origin + OwnerASC.transform.forward * MaxRange;
        Vector3 flat = center - origin; flat.y = 0f;
        if (flat.magnitude > MaxRange) center = origin + flat.normalized * MaxRange;
        center = GA_PlaceTrap.GroundBelow(center);

        if (pc != null)
        {
            pc.RotateToAim();
            pc.PlayAnimation(this);
        }

        OwnerASC.StartAbilityCoroutine(DropRoutine(BombSpots(center)));
        EndAbility();
    }

    private IEnumerator DropRoutine(List<Vector3> spots)
    {
        float wait = spots.Count > 1 ? DropSeconds / (spots.Count - 1) : 0f;
        foreach (Vector3 spot in spots)
        {
            if (OwnerASC == null) yield break;
            Vector3 ground = GA_PlaceTrap.GroundBelow(spot);
            GameObject obj = Instantiate(BombPrefab, ground, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            NetworkObject nob = obj.GetComponent<NetworkObject>();
            if (nob != null)
            {
                InstanceFinder.ServerManager.Spawn(nob);
                Entity_Trap bomb = obj.GetComponent<Entity_Trap>();
                if (bomb != null) bomb.Initialize(OwnerASC, 0);
                BroadcastImpactVFX(ground);
            }
            else Destroy(obj);

            if (wait > 0f) yield return new WaitForSeconds(wait);
        }
    }

    // Dónde cae cada bomba, alrededor de 'center' (en el plano).
    public List<Vector3> BombSpots(Vector3 center)
    {
        var spots = new List<Vector3>();
        switch (Layout)
        {
            case EBombLayout.Ring:
                for (int i = 0; i < BombCount; i++)
                {
                    float a = i * Mathf.PI * 2f / Mathf.Max(1, BombCount);
                    spots.Add(center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * CircleRadius);
                }
                break;

            case EBombLayout.FillCircle:
                // Una grilla hexagonal recortada al círculo: sin huecos y sin amontonarse.
                float step = Mathf.Max(0.5f, Spacing);
                float rowStep = step * 0.866f;
                int rows = Mathf.CeilToInt(CircleRadius / rowStep);
                for (int r = -rows; r <= rows; r++)
                {
                    float z = r * rowStep;
                    float offset = (r & 1) == 0 ? 0f : step * 0.5f;
                    int cols = Mathf.CeilToInt(CircleRadius / step) + 1;
                    for (int c = -cols; c <= cols; c++)
                    {
                        Vector3 p = new Vector3(c * step + offset, 0f, z);
                        if (p.magnitude <= CircleRadius) spots.Add(center + p);
                    }
                }
                break;

            default:
                for (int i = 0; i < BombCount; i++)
                {
                    Vector2 r = Random.insideUnitCircle * CircleRadius;
                    spots.Add(center + new Vector3(r.x, 0f, r.y));
                }
                break;
        }

        // Que no caigan siempre en el mismo orden (de un lado al otro).
        for (int i = spots.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (spots[i], spots[j]) = (spots[j], spots[i]);
        }
        return spots;
    }

    // El círculo y dónde caerían, delante del personaje a media distancia.
    public override void DrawGizmos(Transform origin)
    {
        if (origin == null) return;
        Vector3 center = origin.position + origin.forward * Mathf.Min(MaxRange, CircleRadius + 4f);
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.8f);
        const int segments = 48;
        for (int i = 0; i < segments; i++)
        {
            float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
            Gizmos.DrawLine(center + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * CircleRadius,
                            center + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * CircleRadius);
        }
        if (Layout != EBombLayout.Random)
            foreach (Vector3 p in BombSpots(center)) Gizmos.DrawWireSphere(p, 0.35f);
    }
}
