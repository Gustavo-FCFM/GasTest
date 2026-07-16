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
// ============================================================
[CreateAssetMenu(fileName = "GA_SpawnTotem", menuName = "GAS/Specific Abilities/Shaman/Spawn Totem")]
public class GA_SpawnTotem : GameplayAbility, IRadialMenuAbility
{
    [Header("Configuración de Invocación")]
    public GameObject[]   TotemPrefabs;
    public Sprite[]       TotemIcons;
    // Un cooldown independiente por cada tipo de tótem (mismo índice que
    // TotemPrefabs/TotemIcons).
    public GameplayEffect[] IndividualCooldownEffects;
    public float          MaxSpawnRange = 10f;
    public GameObject     SpawnVFX;

    // Implementación de IRadialMenuAbility.
    public float   MaxRadialRange => MaxSpawnRange;
    public Sprite[] RadialIcons   => TotemIcons;

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

        if (TotemPrefabs == null || totemIndex < 0 || totemIndex >= TotemPrefabs.Length || TotemPrefabs[totemIndex] == null)
        {
            EndAbility();
            return;
        }

        // Verificar cooldown individual del tótem. El tag es opcional: si el GE no
        // tiene GrantedTags no podemos saber si está en cooldown (y antes esto
        // reventaba con IndexOutOfRange al leer GrantedTags[0] a ciegas).
        if (IndividualCooldownEffects != null &&
            IndividualCooldownEffects.Length > totemIndex &&
            IndividualCooldownEffects[totemIndex] != null &&
            IndividualCooldownEffects[totemIndex].GrantedTags != null &&
            IndividualCooldownEffects[totemIndex].GrantedTags.Count > 0)
        {
            EGameplayTag cdTag = IndividualCooldownEffects[totemIndex].GrantedTags[0];
            if (OwnerASC.HasTag(cdTag))
            {
                EndAbility();
                return;
            }
        }

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

        GameObject totemObj = Instantiate(TotemPrefabs[totemIndex], groundPosition, Quaternion.identity);

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
            Debug.LogWarning("[GA_SpawnTotem] El TotemPrefab no tiene NetworkObject — no se va a replicar a los clientes.");

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
        if (IndividualCooldownEffects != null &&
            IndividualCooldownEffects.Length > totemIndex &&
            IndividualCooldownEffects[totemIndex] != null)
        {
            OwnerASC.ApplyGameplayEffect(IndividualCooldownEffects[totemIndex], OwnerASC);
        }

        // Instantiate() acá solo se vería en el proceso servidor —
        // ServerPlayAbilityVFX lo reproduce en todos los peers.
        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
        if (netAsc != null) netAsc.ServerPlayAbilityVFX(this, groundPosition);
        else PlayImpactVFX(groundPosition);

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        if (pc != null) pc.PlayAnimation(AnimationTriggerName, AnimationID);

        EndAbility();
    }

    // Instancia SpawnVFX en el punto de invocación. La llama cada peer con
    // su propia copia (ver ServerPlayAbilityVFX).
    public override void PlayImpactVFX(Vector3 position)
    {
        if (SpawnVFX == null) return;
        Destroy(Instantiate(SpawnVFX, position, Quaternion.identity), 2f);
    }
}
