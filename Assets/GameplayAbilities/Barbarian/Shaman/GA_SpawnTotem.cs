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
        [Label("Nombre (inglés)")]
        public string Name;

        [Tooltip("Nombre en la rueda, en español. Vacío = sale el inglés.")]
        [Label("Nombre (español)")]
        public string NameEs;

        [Tooltip("Qué da, debajo del nombre en la rueda, en inglés.")]
        [Label("Descripción (inglés)")]
        public string Description;

        [Tooltip("Qué da, en español. Vacío = sale el inglés.")]
        [Label("Descripción (español)")]
        public string DescriptionEs;

        [Label("Ícono")]
        public Sprite Icon;

        [Tooltip("El tótem que se invoca (con Entity_Totem y NetworkObject).")]
        [Label("Prefab del tótem")]
        public GameObject Prefab;

        [Tooltip("Cooldown propio de este tótem: mientras su PRIMER GrantedTag esté puesto, no se " +
                 "puede volver a invocar (y en la rueda se ve apagado).")]
        [Label("Cooldown del tótem (GE)")]
        public GameplayEffect Cooldown;
    }

    [Section("Tótems")]
    [Tooltip("Las opciones de la rueda, en orden (la primera arriba, después en sentido horario).")]
    [Label("Tótems")]
    public List<TotemOption> Totems = new List<TotemOption>();

    [Tooltip("Cuántos tótems puede tener puestos a la vez (de cualquier tipo): al poner uno más, se va " +
             "el más viejo.")]
    [Label("Máximo a la vez")]
    public int MaxTotems = 2;

    [Section(AbilitySection.Shape)]
    [Tooltip("Hasta dónde se puede poner el tótem desde el jugador.")]
    [Label("Alcance máximo (m)")]
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
    public override bool SupportsVisualTiming(EVisualWhen when)
        => when == EVisualWhen.OnCast || when == EVisualWhen.OnImpact;

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

        // El piso donde apuntó (apuntando al techo, el de abajo; ver Entity_Summon.GroundUnderAim).
        // Antes se buscaba desde 10 m arriba y quedaba el más alto: bajo techo, el techo.
        Vector3 eyes = OwnerASC.transform.position + Vector3.up * 1.6f;
        Vector3 groundPosition = Entity_Summon.GroundUnderAim(spawnPosition, eyes);

        // La invocación (Entity_Summon): la crea en red, le da el equipo del Chamán y saca el
        // tótem más viejo si se pasa de MaxTotems. Si el Chamán muere, se van (lo dice el prefab).
        Entity_Summon.Spawn(option.Prefab, OwnerASC, groundPosition, Quaternion.identity, MaxTotems);

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
