using UnityEngine;
using System.Collections.Generic;
using FishNet;
using FishNet.Object;

// ============================================================
// GA_SpawnTotem
//
// Habilidad de menú radial (IRadialMenuAbility): el jugador elige
// qué tótem invocar en un círculo de opciones, y esta clase lo
// spawnea en el suelo bajo el cursor (con ground-snapping), respeta
// un máximo de tótems activos (despawneando el más viejo si se
// pasa), y cobra un cooldown individual por tipo de tótem.
//
// Cada opción de la rueda es UNA entrada de la lista Totems, con todo junto: nombre,
// descripción, ícono, prefab y cooldown. Antes eran cinco listas sueltas que había que
// mantener en el mismo orden a mano; los assets viejos se pasan solos a la lista.
// ============================================================
[CreateAssetMenu(fileName = "GA_SpawnTotem", menuName = "GAS/Specific Abilities/Barbarian/Shaman/Spawn Totem")]
public class GA_SpawnTotem : GameplayAbility, IRadialMenuAbility
{
    // Una opción de la rueda.
    [System.Serializable]
    public struct TotemOption
    {
        [Tooltip("Nombre en la rueda, en inglés.")]
        public string Name;

        [Tooltip("Nombre en la rueda, en español. Vacío = sale el inglés.")]
        public string NameEs;

        [Tooltip("Qué da, debajo del nombre en la rueda, en inglés.")]
        public string Description;

        [Tooltip("Qué da, en español. Vacío = sale el inglés.")]
        public string DescriptionEs;

        public Sprite Icon;

        [Tooltip("El tótem que se invoca (con Entity_Totem y NetworkObject).")]
        public GameObject Prefab;

        [Tooltip("Cooldown propio de este tótem: mientras su PRIMER GrantedTag esté puesto, no se " +
                 "puede volver a invocar (y en la rueda se ve apagado).")]
        public GameplayEffect Cooldown;
    }

    [Section("Tótems")]
    [Tooltip("Las opciones de la rueda, en orden (la primera arriba, después en sentido horario).")]
    public List<TotemOption> Totems = new List<TotemOption>();

    [Section(AbilitySection.Shape)]
    [Tooltip("Hasta dónde se puede poner el tótem desde el jugador.")]
    public float MaxSpawnRange = 10f;

    // Implementación de IRadialMenuAbility (la rueda lee arrays: se arman de la lista).
    public float    MaxRadialRange     => MaxSpawnRange;
    public Sprite[] RadialIcons        => Collect(t => t.Icon);
    public string[] RadialLabels       => Collect(t => Loc.Pick(t.Name, t.NameEs));
    public string[] RadialDescriptions => Collect(t => Loc.Pick(t.Description, t.DescriptionEs));

    private T[] Collect<T>(System.Func<TotemOption, T> pick)
    {
        if (Totems == null) return new T[0];
        T[] result = new T[Totems.Count];
        for (int i = 0; i < Totems.Count; i++) result[i] = pick(Totems[i]);
        return result;
    }

    // No busca personajes ni golpea: invoca. Sus VFX "en el impacto" salen donde aparece.
    public override bool UsesTargetLayer => false;
    public override bool UsesHitEffects  => false;
    public override bool SupportsVisualTiming(EVisualWhen when) => when != EVisualWhen.OnHit;

    // En la rueda, el tótem se ve apagado mientras corre su cooldown propio. Corre en el
    // dueño, con los tags sincronizados.
    public bool IsRadialOptionAvailable(int index) => !IsOnCooldown(index);

    private bool IsOnCooldown(int index)
    {
        if (OwnerASC == null || Totems == null || index < 0 || index >= Totems.Count) return false;

        GameplayEffect cd = Totems[index].Cooldown;
        if (cd == null || cd.GrantedTags == null || cd.GrantedTags.Count == 0) return false;
        return OwnerASC.HasTag(cd.GrantedTags[0]);
    }

    // Tótems actualmente invocados por este jugador, en orden de
    // aparición (para despawnear el más viejo al pasar el límite).
    private Queue<GameObject> activeTotems = new Queue<GameObject>();
    private const int MAX_TOTEMS = 2;

    // Vacío a propósito: esta habilidad se activa vía el menú radial
    // (ActivateWithSelection), no con un click normal.
    public override void Activate() { }

    // Valida el tótem elegido (incluyendo su cooldown individual),
    // lo spawnea en red en el suelo bajo el punto elegido, respeta el
    // límite de tótems activos, cobra su cooldown, y reproduce el VFX
    // de invocación.
    public void ActivateWithSelection(int totemIndex, Vector3 spawnPosition)
    {
        if (!IsServer) return;

        if (totemIndex == -1) { EndAbility(); return; }
        if (!CanActivate())   { EndAbility(); return; }

        if (OwnerASC == null) { EndAbility(); return; }

        if (Totems == null || totemIndex < 0 || totemIndex >= Totems.Count || Totems[totemIndex].Prefab == null)
        {
            EndAbility();
            return;
        }

        // Verificar cooldown individual del tótem. El tag es opcional: si el GE no
        // tiene GrantedTags no podemos saber si está en cooldown.
        if (IsOnCooldown(totemIndex))
        {
            EndAbility();
            return;
        }

        TotemOption option = Totems[totemIndex];

        CommitAbility();

        // Ground snapping
        Vector3 groundPosition = spawnPosition;
        RaycastHit[] hits = Physics.RaycastAll(spawnPosition + Vector3.up * 10f, Vector3.down, 20f);
        float closestDistance = float.MaxValue;
        foreach (var hit in hits)
        {
            if (!hit.collider.isTrigger && hit.collider.GetComponentInParent<AbilitySystemComponent>() == null)
            {
                if (hit.distance < closestDistance)
                {
                    closestDistance  = hit.distance;
                    groundPosition   = hit.point;
                }
            }
        }

        GameObject totemObj = Instantiate(option.Prefab, groundPosition, Quaternion.identity);

        Entity_Totem totemScript = totemObj.GetComponent<Entity_Totem>();
        if (totemScript != null)
        {
            totemScript.MyTeamID   = OwnerASC.TeamID;
            totemScript.CreatorASC = OwnerASC;
        }

        // Instantiate() normal solo crea el tótem en el servidor — sin esto,
        // es invisible para cualquier cliente que no sea el host.
        NetworkObject totemNob = totemObj.GetComponent<NetworkObject>();
        if (totemNob != null)
            InstanceFinder.ServerManager.Spawn(totemNob);
        else
            Debug.LogWarning("[GA_SpawnTotem] El prefab del tótem no tiene NetworkObject — no se va a replicar a los clientes.");

        // Límite de tótems activos
        activeTotems.Enqueue(totemObj);
        if (activeTotems.Count > MAX_TOTEMS)
        {
            GameObject oldestTotem = activeTotems.Dequeue();
            if (oldestTotem != null)
            {
                NetworkObject oldestNob = oldestTotem.GetComponent<NetworkObject>();
                if (oldestNob != null && oldestNob.IsSpawned) InstanceFinder.ServerManager.Despawn(oldestNob);
                else Destroy(oldestTotem);
            }
        }

        // Cooldown individual
        if (option.Cooldown != null) OwnerASC.ApplyGameplayEffect(option.Cooldown, OwnerASC);

        // Instantiate() acá solo se vería en el proceso servidor —
        // BroadcastImpactVFX lo reproduce en todos los peers.
        BroadcastImpactVFX(groundPosition);

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        if (pc != null) pc.PlayAnimation(this);

        EndAbility();
    }

    public override void DrawGizmos(Transform origin)
    {
        if (origin == null) return;
        Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.6f);
        Gizmos.DrawWireSphere(origin.position, MaxSpawnRange);
    }

#if UNITY_EDITOR
    public override void DrawSceneHandles(Transform origin)
    {
        if (origin == null) return;
        AbilityHandles.Radius(this, "Max Spawn Range", origin.position, ref MaxSpawnRange,
                              AbilityHandles.RadiusColor, origin.forward);
    }
#endif

    // =========================================================
    // DATOS VIEJOS (solo para pasarlos a la lista; ver GameplayAbility.UpgradeLegacyData)
    // =========================================================

    [SerializeField, HideInInspector] private GameObject[]     TotemPrefabs;
    [SerializeField, HideInInspector] private Sprite[]         TotemIcons;
    [SerializeField, HideInInspector] private GameplayEffect[] IndividualCooldownEffects;
    [SerializeField, HideInInspector] private string[]         TotemNames;
    [SerializeField, HideInInspector] private string[]         TotemDescriptions;
    [SerializeField, HideInInspector] private GameObject       SpawnVFX;

    protected override void OnUpgradeLegacyData(ref bool changed)
    {
        base.OnUpgradeLegacyData(ref changed);

        if (TotemPrefabs != null && TotemPrefabs.Length > 0)
        {
            if (Totems == null) Totems = new List<TotemOption>();

            for (int i = 0; i < TotemPrefabs.Length; i++)
            {
                Totems.Add(new TotemOption
                {
                    Prefab      = TotemPrefabs[i],
                    Icon        = TotemIcons != null && i < TotemIcons.Length ? TotemIcons[i] : null,
                    Cooldown    = IndividualCooldownEffects != null && i < IndividualCooldownEffects.Length
                                    ? IndividualCooldownEffects[i] : null,
                    Name        = TotemNames != null && i < TotemNames.Length ? TotemNames[i] : "",
                    Description = TotemDescriptions != null && i < TotemDescriptions.Length ? TotemDescriptions[i] : "",
                });
            }

            TotemPrefabs = null;
            TotemIcons = null;
            IndividualCooldownEffects = null;
            TotemNames = null;
            TotemDescriptions = null;
            changed = true;
        }

        UpgradeVisual(ref SpawnVFX, new AbilityVisual { When = EVisualWhen.OnImpact, DestroyTime = 2f }, ref changed);
    }
}
