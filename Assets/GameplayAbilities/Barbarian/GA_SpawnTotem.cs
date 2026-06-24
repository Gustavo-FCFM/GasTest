using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "GA_SpawnTotem", menuName = "GAS/Abilities/Shaman/Spawn Totem")]
public class GA_SpawnTotem : GameplayAbility, IRadialMenuAbility
{
    [Header("Configuración de Invocación")]
    public GameObject[]   TotemPrefabs;
    public Sprite[]       TotemIcons;
    public GameplayEffect[] IndividualCooldownEffects;
    public float          MaxSpawnRange = 10f;
    public GameObject     SpawnVFX;

    public float   MaxRadialRange => MaxSpawnRange;
    public Sprite[] RadialIcons   => TotemIcons;

    private Queue<GameObject> activeTotems = new Queue<GameObject>();
    private const int MAX_TOTEMS = 2;

    // Activate vacío — manejado por el menú radial
    public override void Activate() { }

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

        // Verificar cooldown individual del tótem
        if (IndividualCooldownEffects != null &&
            IndividualCooldownEffects.Length > totemIndex &&
            IndividualCooldownEffects[totemIndex] != null)
        {
            EGameplayTag cdTag = IndividualCooldownEffects[totemIndex].GrantedTags[0];
            if (OwnerASC.HasTag(cdTag))
            {
                Debug.LogWarning($"El tótem {totemIndex} está en cooldown.");
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

        // Límite de tótems activos
        activeTotems.Enqueue(totemObj);
        if (activeTotems.Count > MAX_TOTEMS)
        {
            GameObject oldestTotem = activeTotems.Dequeue();
            if (oldestTotem != null) Destroy(oldestTotem);
        }

        // Cooldown individual
        if (IndividualCooldownEffects != null &&
            IndividualCooldownEffects.Length > totemIndex &&
            IndividualCooldownEffects[totemIndex] != null)
        {
            OwnerASC.ApplyGameplayEffect(IndividualCooldownEffects[totemIndex], OwnerASC);
        }

        if (SpawnVFX != null)
            Destroy(Instantiate(SpawnVFX, groundPosition, Quaternion.identity), 2f);

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        if (pc != null) pc.PlayAnimation(AnimationTriggerName, AnimationID);

        EndAbility();
    }
}