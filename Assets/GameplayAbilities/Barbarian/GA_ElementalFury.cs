using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "GA_ElementalFury", menuName = "GAS/Abilities/Shaman/Elemental Fury")]
public class GA_ElementalFury : GameplayAbility
{
    [Header("Configuración de Invocación")]
    public GameObject[] TotemPrefabs;
    public float SquareRadius = 4f;
    public float Duration = 10f;

    [Header("Tornado de Daño")]
    public GameplayEffect DamageEffect;
    public float DamageRadius = 5f;
    public float TickRate = 0.5f;
    public GameObject TornadoVFXPrefab;
    public float TornadoVfxScaleMultiplier = 2.0f;
    public bool FollowPlayer = false;

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
                totemASC.AddTag(EGameplayTag.Status_Inmortal);

            ultimateTotems.Add(totemObj);
        }

        // Instanciar tornado visual
        GameObject tornadoInstance = null;
        if (TornadoVFXPrefab != null)
        {
            tornadoInstance = Instantiate(TornadoVFXPrefab, centerPos, Quaternion.identity);
            float finalScale = DamageRadius * TornadoVfxScaleMultiplier;
            tornadoInstance.transform.localScale = new Vector3(finalScale, finalScale, finalScale);
            if (FollowPlayer) tornadoInstance.transform.SetParent(OwnerASC.transform);
        }

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
        if (tornadoInstance != null) Destroy(tornadoInstance);
        foreach (var t in ultimateTotems)
            if (t != null) Destroy(t);

        EndAbility();
    }
}