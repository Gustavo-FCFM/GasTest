using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Object;

// ============================================================
// GA_ElementalFury
//
// Ultimate de Chamán: invoca hasta 4 tótems inmortales en las
// esquinas de un cuadrado alrededor del dueño, y además crea un
// tornado de daño continuo (con VFX) que dura Duration segundos.
// Al terminar, despawnea los tótems invocados.
// ============================================================
[CreateAssetMenu(fileName = "GA_ElementalFury", menuName = "GAS/Specific Abilities/Shaman/Elemental Fury")]
public class GA_ElementalFury : GameplayAbility
{
    [Header("Configuración de Invocación")]
    // Hasta 4 prefabs de tótem, uno por esquina del cuadrado.
    public GameObject[] TotemPrefabs;
    // Distancia del centro a cada esquina donde aparecen los tótems.
    public float SquareRadius = 4f;
    // Cuánto dura el tornado (y por lo tanto, cuánto viven los tótems).
    public float Duration = 10f;

    [Header("Tornado de Daño")]
    public GameplayEffect DamageEffect;
    public float DamageRadius = 5f;
    // Cada cuánto se reaplica el daño a quien esté dentro del tornado.
    public float TickRate = 0.5f;
    public GameObject TornadoVFXPrefab;
    // Multiplica DamageRadius para el tamaño del VFX (no afecta el área real).
    public float TornadoVfxScaleMultiplier = 2.0f;
    // Si el tornado sigue al dueño o queda fijo donde se activó.
    public bool FollowPlayer = false;

    // Valida, cobra costo/cooldown y arranca la secuencia de invocación +
    // tornado.
    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;

        CommitAbility();

        if (OwnerASC != null)
        {
            PlayerController pc = OwnerASC.GetComponent<PlayerController>();
            if (pc != null) pc.PlayAnimation(AnimationTriggerName, AnimationID);
            OwnerASC.StartAbilityCoroutine(ElementalFuryRoutine());
        }
        else
        {
            EndAbility();
        }
    }

    // Invoca los tótems en red en las esquinas del cuadrado, reproduce el
    // VFX del tornado, aplica daño periódico a los enemigos dentro de
    // DamageRadius durante Duration segundos, y al terminar despawnea los
    // tótems invocados.
    private IEnumerator ElementalFuryRoutine()
    {
        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        Vector3 centerPos = OwnerASC.transform.position;

        yield return new WaitForSeconds(0.5f);

        if (pc != null) pc.FinishAttack();

        // Crear tótems en esquinas del cuadrado
        List<GameObject> ultimateTotems = new List<GameObject>();
        Vector3[] corners = new Vector3[]
        {
            new Vector3( 1, 0,  1).normalized * SquareRadius,
            new Vector3( 1, 0, -1).normalized * SquareRadius,
            new Vector3(-1, 0,  1).normalized * SquareRadius,
            new Vector3(-1, 0, -1).normalized * SquareRadius
        };

        for (int i = 0; i < TotemPrefabs.Length && i < 4; i++)
        {
            if (TotemPrefabs[i] == null) continue;

            Vector3 spawnPos = centerPos + corners[i];
            if (Physics.Raycast(spawnPos + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 15f))
                spawnPos = hit.point;

            GameObject totemObj = Instantiate(TotemPrefabs[i], spawnPos, Quaternion.identity);

            Entity_Totem totemScript = totemObj.GetComponent<Entity_Totem>();
            if (totemScript != null)
            {
                totemScript.MyTeamID   = OwnerASC.TeamID;
                totemScript.CreatorASC = OwnerASC;
            }

            AbilitySystemComponent totemASC = totemObj.GetComponent<AbilitySystemComponent>();
            if (totemASC != null)
                totemASC.AddTag(EGameplayTag.Status_Immortal);

            // Instantiate() normal solo crea el tótem en el servidor — sin
            // esto, es invisible para cualquier cliente que no sea el host.
            NetworkObject totemNob = totemObj.GetComponent<NetworkObject>();
            if (totemNob != null)
                InstanceFinder.ServerManager.Spawn(totemNob);
            else
                Debug.LogWarning("[GA_ElementalFury] El TotemPrefab no tiene NetworkObject — no se va a replicar a los clientes.");

            ultimateTotems.Add(totemObj);
        }

        // Instanciar tornado visual. Instantiate() acá solo se vería en el
        // proceso servidor — ServerPlayAbilityVFX lo reproduce en todos los
        // peers (cada uno con su propia copia, que se autodestruye sola
        // tras Duration en vez de que la sigamos con una referencia acá).
        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
        if (netAsc != null) netAsc.ServerPlayAbilityVFX(this, centerPos);
        else PlayImpactVFX(centerPos);

        // Bucle de daño
        float timeElapsed = 0f;
        while (timeElapsed < Duration)
        {
            Vector3 currentCenter = FollowPlayer ? OwnerASC.transform.position : centerPos;
            Collider[] hits = Physics.OverlapSphere(currentCenter, DamageRadius, TargetLayer);
            foreach (var hitCol in hits)
            {
                AbilitySystemComponent targetASC = hitCol.GetComponentInParent<AbilitySystemComponent>();
                if (targetASC != null && IsEnemy(targetASC))
                    if (DamageEffect != null) targetASC.ApplyGameplayEffect(DamageEffect, OwnerASC);
            }

            yield return new WaitForSeconds(TickRate);
            timeElapsed += TickRate;
        }

        // Limpieza
        foreach (var t in ultimateTotems)
        {
            if (t == null) continue;
            NetworkObject nob = t.GetComponent<NetworkObject>();
            if (nob != null && nob.IsSpawned) InstanceFinder.ServerManager.Despawn(nob);
            else Destroy(t);
        }

        EndAbility();
    }

    // Instancia TornadoVFXPrefab escalado según DamageRadius (parentado al
    // dueño si FollowPlayer) y lo destruye tras Duration segundos.
    public override void PlayImpactVFX(Vector3 position)
    {
        if (TornadoVFXPrefab == null || OwnerASC == null) return;

        GameObject tornadoInstance = Instantiate(TornadoVFXPrefab, position, Quaternion.identity);
        float finalScale = DamageRadius * TornadoVfxScaleMultiplier;
        tornadoInstance.transform.localScale = new Vector3(finalScale, finalScale, finalScale);
        if (FollowPlayer) tornadoInstance.transform.SetParent(OwnerASC.transform);

        Destroy(tornadoInstance, Duration);
    }
}
