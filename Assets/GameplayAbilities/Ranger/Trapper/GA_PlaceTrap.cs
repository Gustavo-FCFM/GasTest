using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using UnityEngine;

// ============================================================
// GA_PlaceTrap  (Maestro en trampas — habilidad extra del Trampero, 15 de octubre de 2026)
//
// La rueda (IRadialMenuAbility, como los tótems del Chamán): se elige la trampa y, al soltar,
// queda pegada a la superficie donde apunta la mira (hasta "Alcance"): en el PISO, o en una PARED
// (pedido de Gustavo), con su base apoyada en ella. Mirando al aire, cae al piso de abajo. La
// cámara no gira con la rueda abierta, así que queda donde apuntabas al abrirla.
//
// Cada trampa es un prefab con Entity_Trap (lo que hace, cuánto dura, cuándo salta). Esta
// habilidad solo la pone: la deja en el piso, le dice quién es su dueño y respeta el límite de
// trampas puestas (la más vieja desaparece). La espera entre una y otra es la de la habilidad
// (su "Cooldown (s)").
// ============================================================
[CreateAssetMenu(fileName = "GA_PlaceTrap", menuName = "GAS/Specific Abilities/Ranger/Trapper/Place Trap")]
public class GA_PlaceTrap : GameplayAbility, IRadialMenuAbility
{
    [System.Serializable]
    public struct TrapOption
    {
        [Label("Nombre (inglés)")]
        public string Name;
        [Label("Nombre (español)")]
        public string NameEs;
        [Label("Descripción (inglés)")]
        public string Description;
        [Label("Descripción (español)")]
        public string DescriptionEs;
        [Label("Ícono")]
        public Sprite Icon;

        [Tooltip("El prefab de la trampa (Entity_Trap + NetworkObject).")]
        [Label("Prefab de la trampa")]
        public GameObject Prefab;
    }

    [Section("Trampas")]
    [Tooltip("Las opciones de la rueda, en orden.")]
    [Label("Trampas")]
    public List<TrapOption> Traps = new List<TrapOption>();

    [Tooltip("Hasta dónde se puede poner, medido desde el Trampero.")]
    [Label("Alcance (m)")]
    public float MaxPlaceRange = 25f;

    [Tooltip("Cuántas trampas puede tener puestas a la vez. Al poner una más, desaparece la más vieja.")]
    [Label("Trampas puestas a la vez")]
    public int MaxTraps = 6;

    public float    MaxRadialRange     => MaxPlaceRange;
    public Sprite[] RadialIcons        => Collect(t => t.Icon);
    public string[] RadialLabels       => Collect(t => Loc.Pick(t.Name, t.NameEs));
    public string[] RadialDescriptions => Collect(t => Loc.Pick(t.Description, t.DescriptionEs));

    private T[] Collect<T>(System.Func<TrapOption, T> pick)
    {
        var result = new T[Traps != null ? Traps.Count : 0];
        for (int i = 0; i < result.Length; i++) result[i] = pick(Traps[i]);
        return result;
    }

    public override bool UsesTargetLayer => false;
    public override bool UsesHitEffects  => false;

    // Se activa por la rueda (ActivateWithSelection), no con un clic.
    public override void Activate() { }

    public void ActivateWithSelection(int index, Vector3 targetPosition)
    {
        if (!IsServer) return;
        if (index < 0 || Traps == null || index >= Traps.Count || Traps[index].Prefab == null) { EndAbility(); return; }
        if (!CanActivate() || OwnerASC == null) { EndAbility(); return; }

        CommitAbility();

        Vector3 aimed = ClampToRange(targetPosition);
        Vector3 spot;
        Quaternion rotation;
        if (TrySurface(aimed, out RaycastHit surface))
        {
            spot = surface.point;
            rotation = SurfaceRotation(surface.normal);
        }
        else
        {
            spot = GroundBelow(aimed);
            rotation = Quaternion.Euler(0f, OwnerASC.transform.eulerAngles.y, 0f);
        }

        GameObject obj = Instantiate(Traps[index].Prefab, spot, rotation);
        NetworkObject nob = obj.GetComponent<NetworkObject>();
        if (nob == null)
        {
            Debug.LogWarning($"[{AbilityName}] '{Traps[index].Prefab.name}' no tiene NetworkObject.");
            Destroy(obj);
            EndAbility();
            return;
        }
        InstanceFinder.ServerManager.Spawn(nob);

        Entity_Trap trap = obj.GetComponent<Entity_Trap>();
        if (trap != null) trap.Initialize(OwnerASC, MaxTraps);
        else Debug.LogWarning($"[{AbilityName}] '{Traps[index].Prefab.name}' no tiene Entity_Trap.");

        BroadcastImpactVFX(spot);
        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        if (pc != null) pc.PlayAnimation(this);

        EndAbility();
    }

    // Más lejos que el alcance: en esa dirección, a la distancia máxima.
    private Vector3 ClampToRange(Vector3 point)
    {
        Vector3 origin = OwnerASC.transform.position;
        Vector3 flat = point - origin;
        flat.y = 0f;
        if (flat.magnitude <= MaxPlaceRange) return point;
        Vector3 clamped = origin + flat.normalized * MaxPlaceRange;
        clamped.y = point.y;
        return clamped;
    }

    // La superficie donde apuntó: un rayo desde los ojos del Trampero hasta el punto de la mira, que
    // choca con lo mismo que vio la mira (piso, pared o techo). Si no hay nada cerca del punto (apuntó
    // al aire), false.
    private bool TrySurface(Vector3 aimed, out RaycastHit hit)
    {
        int mask = ~((1 << 7) | (1 << 2));
        Vector3 eye = OwnerASC.transform.position + Vector3.up * 1.6f;
        Vector3 dir = aimed - eye;
        float distance = dir.magnitude;
        if (distance < 0.1f) { hit = default; return false; }
        return Physics.Raycast(eye, dir / distance, out hit, distance + 0.6f, mask, QueryTriggerInteraction.Ignore) &&
               (hit.point - aimed).sqrMagnitude < 1.5f * 1.5f;
    }

    // La trampa con la base contra la superficie: su "arriba" sale de la superficie. En el piso, además,
    // mira hacia donde mira el Trampero.
    private Quaternion SurfaceRotation(Vector3 normal)
    {
        Vector3 forward = Vector3.ProjectOnPlane(OwnerASC.transform.forward, normal);
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.ProjectOnPlane(Vector3.up, normal);
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        return Quaternion.LookRotation(forward.normalized, normal);
    }

    // El piso debajo del punto (sin contar personajes ni las paredes invisibles).
    public static Vector3 GroundBelow(Vector3 point)
    {
        int mask = ~((1 << 7) | (1 << 2));
        if (Physics.Raycast(point + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 40f, mask, QueryTriggerInteraction.Ignore))
            return hit.point;
        return point;
    }

    public override void DrawGizmos(Transform origin)
    {
        if (origin == null) return;
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(origin.position, MaxPlaceRange);
    }
}
