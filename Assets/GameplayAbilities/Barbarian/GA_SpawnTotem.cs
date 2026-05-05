using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "GA_SpawnTotem", menuName = "GAS/Abilities/Shaman/Spawn Totem")]
public class GA_SpawnTotem : GameplayAbility, IRadialMenuAbility 
{
    [Header("Configuración de Invocación")]
    public GameObject[] TotemPrefabs; 
    public Sprite[] TotemIcons;
    
    [Tooltip("Efectos de enfriamiento individuales para cada tótem (mismo orden que los prefabs)")]
    public GameplayEffect[] IndividualCooldownEffects;
    
    public float MaxSpawnRange = 10f;
    public GameObject SpawnVFX;

    public float MaxRadialRange => MaxSpawnRange;
    public Sprite[] RadialIcons => TotemIcons;

    // Estructura para mantener el registro de los tótems invocados (Límite de 2)
    private Queue<GameObject> activeTotems = new Queue<GameObject>();
    private const int MAX_TOTEMS = 2;

    public override void Activate()
    {
        // Vacío, manejado por el menú radial.
    }

    public void ActivateWithSelection(int totemIndex, Vector3 spawnPosition)
    {
        if (totemIndex == -1)
        {
            EndAbility(); 
            return;       
        }

        if (!CanActivate()) return;

        if (OwnerASC != null)
        {
            if (TotemPrefabs != null && totemIndex >= 0 && totemIndex < TotemPrefabs.Length && TotemPrefabs[totemIndex] != null)
            {
                // 1. VERIFICACIÓN DE ENFRIAMIENTO INDIVIDUAL
                if (IndividualCooldownEffects != null && IndividualCooldownEffects.Length > totemIndex && IndividualCooldownEffects[totemIndex] != null)
                {
                    // Comprobamos si el jugador tiene la etiqueta de enfriamiento de este tótem específico
                    EGameplayTag cdTag = IndividualCooldownEffects[totemIndex].GrantedTags[0];
                    if (OwnerASC.HasTag(cdTag))
                    {
                        Debug.LogWarning($"El tótem {totemIndex} aún está en enfriamiento.");
                        EndAbility();
                        return; // Abortamos la invocación
                    }
                }

                // 2. APLICAR COSTES (Solo si pasó la validación del cooldown)
                CommitAbility();

                // 3. GROUND SNAPPING
                Vector3 groundPosition = spawnPosition;
                RaycastHit[] hits = Physics.RaycastAll(spawnPosition + Vector3.up * 10f, Vector3.down, 20f);
                float closestDistance = float.MaxValue;

                foreach (var hit in hits)
                {
                    if (!hit.collider.isTrigger && hit.collider.GetComponentInParent<AbilitySystemComponent>() == null)
                    {
                        if (hit.distance < closestDistance)
                        {
                            closestDistance = hit.distance;
                            groundPosition = hit.point;
                        }
                    }
                }

                // 4. INSTANCIACIÓN Y HERENCIA
                GameObject totemObj = Instantiate(TotemPrefabs[totemIndex], groundPosition, Quaternion.identity);
                
                Entity_Totem totemScript = totemObj.GetComponent<Entity_Totem>();
                if (totemScript != null)
                {
                    totemScript.MyTeamID = OwnerASC.TeamID; 

                    totemScript.CreatorASC = OwnerASC; // Referencia al Chamán para sinergias
                }

                // 5. LÓGICA DE LÍMITE DE TÓTEMS (Máximo 2)
                activeTotems.Enqueue(totemObj);
                if (activeTotems.Count > MAX_TOTEMS)
                {
                    // Extraemos el más antiguo de la cola y lo destruimos
                    GameObject oldestTotem = activeTotems.Dequeue();
                    if (oldestTotem != null)
                    {
                        // Para activar correctamente OnDeath, podríamos aplicar daño masivo, pero Destroy directo es más limpio aquí.
                        Destroy(oldestTotem);
                    }
                }

                // 6. APLICAR ENFRIAMIENTO INDIVIDUAL AL JUGADOR
                if (IndividualCooldownEffects != null && IndividualCooldownEffects.Length > totemIndex && IndividualCooldownEffects[totemIndex] != null)
                {
                    OwnerASC.ApplyGameplayEffect(IndividualCooldownEffects[totemIndex], OwnerASC);
                }

                if (SpawnVFX != null)
                {
                    Destroy(Instantiate(SpawnVFX, groundPosition, Quaternion.identity), 2f);
                }
            }
            
            PlayerController pc = OwnerASC.GetComponent<PlayerController>();
            if (pc != null) pc.PlayAnimation(AnimationTriggerName, AnimationID);
        }

        EndAbility();
    }
}