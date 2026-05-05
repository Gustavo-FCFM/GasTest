using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "GA_ElementalFury", menuName = "GAS/Abilities/Shaman/Elemental Fury")]
public class GA_ElementalFury : GameplayAbility
{
    [Header("Configuración de Invocación")]
    [Tooltip("Arreglo con los 4 prefabs (Oso, Águila, Tigre, Lobo)")]
    public GameObject[] TotemPrefabs; 
    
    [Tooltip("Distancia desde el centro hasta cada tótem (mitad del tamaño del cuadrado)")]
    public float SquareRadius = 4f;
    
    [Tooltip("Duración exacta del tornado y los tótems")]
    public float Duration = 10f;

    [Header("Tornado de Daño")]
    public GameplayEffect DamageEffect;
    public float DamageRadius = 5f;
    public float TickRate = 0.5f;
    public GameObject TornadoVFXPrefab;
    
    [Tooltip("Ajuste manual para que el tamaño visual del tornado coincida con el radio de daño.")]
    public float TornadoVfxScaleMultiplier = 2.0f;

    [Tooltip("¿El tornado se queda donde se invocó (False) o persigue al chamán (True)?")]
    public bool FollowPlayer = false; 

    public override void Activate()
    {
        if (!CanActivate()) return;
        CommitAbility();

        if (OwnerASC != null)
        {
            // Iniciamos la animación
            PlayerController pc = OwnerASC.GetComponent<PlayerController>();
            if (pc != null) pc.PlayAnimation(AnimationTriggerName, AnimationID);
            
            // Arrancamos el motor principal de la habilidad
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
        
        // 1. ESPERAMOS LA ANIMACIÓN Y LIBERAMOS AL JUGADOR
        // Esperamos medio segundo para que haga la animación de invocación antes de aparecer los tótems y el tornado.
        yield return new WaitForSeconds(0.5f);
        
        // ¡IMPORTANTE! Liberamos el combate aquí para que el Chamán pueda moverse 
        // y usar otras habilidades mientras el tornado sigue activo durante 10s.
        if (pc != null) pc.FinishAttack(); 

        // 2. CREACIÓN DEL CUADRADO DE TÓTEMS
        List<GameObject> ultimateTotems = new List<GameObject>();
        
        // Vectores diagonales para formar las 4 esquinas del cuadrado
        Vector3[] corners = new Vector3[]
        {
            new Vector3(1, 0, 1).normalized * SquareRadius,   // Arriba-Derecha
            new Vector3(1, 0, -1).normalized * SquareRadius,  // Abajo-Derecha
            new Vector3(-1, 0, 1).normalized * SquareRadius,  // Arriba-Izquierda
            new Vector3(-1, 0, -1).normalized * SquareRadius  // Abajo-Izquierda
        };

        for (int i = 0; i < TotemPrefabs.Length && i < 4; i++)
        {
            if (TotemPrefabs[i] != null)
            {
                Vector3 spawnPos = centerPos + corners[i];
                
                // Ground Snapping para que no floten
                if (Physics.Raycast(spawnPos + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 15f))
                {
                    spawnPos = hit.point;
                }

                GameObject totemObj = Instantiate(TotemPrefabs[i], spawnPos, Quaternion.identity);
                Entity_Totem totemScript = totemObj.GetComponent<Entity_Totem>();
                
                if (totemScript != null)
                {
                    totemScript.MyTeamID = OwnerASC.TeamID;
                    totemScript.CreatorASC = OwnerASC; // Para que apliquen el buff doble si usas Enfurecer
                }

                // HACERLOS INDESTRUCTIBLES
                AbilitySystemComponent totemASC = totemObj.GetComponent<AbilitySystemComponent>();
                if (totemASC != null)
                {
                    // Le ponemos el tag Inmortal que tú mismo programaste para que la vida no baje de 1
                    totemASC.AddTag(EGameplayTag.Status_Inmortal); 
                }

                ultimateTotems.Add(totemObj);
            }
        }

        // 3. INSTANCIAR EL TORNADO VISUAL
        GameObject tornadoInstance = null;
        if (TornadoVFXPrefab != null)
        {
            tornadoInstance = Instantiate(TornadoVFXPrefab, centerPos, Quaternion.identity);

            float finalScale = DamageRadius * TornadoVfxScaleMultiplier;
            tornadoInstance.transform.localScale = new Vector3(finalScale, finalScale, finalScale);
            
            if (FollowPlayer) tornadoInstance.transform.SetParent(OwnerASC.transform);
        }

        // 4. BUCLE DE DAÑO EN ÁREA (10 SEGUNDOS)
        float timeElapsed = 0f;
        while (timeElapsed < Duration)
        {
            Vector3 currentCenter = FollowPlayer ? OwnerASC.transform.position : centerPos;
            
            Collider[] hits = Physics.OverlapSphere(currentCenter, DamageRadius, TargetLayer);
            foreach (var hitCol in hits)
            {
                AbilitySystemComponent targetASC = hitCol.GetComponentInParent<AbilitySystemComponent>();
                
                // Usamos la validación lógica IsEnemy de la clase padre
                if (targetASC != null && IsEnemy(targetASC))
                {
                    if (DamageEffect != null) targetASC.ApplyGameplayEffect(DamageEffect, OwnerASC);
                }
            }

            yield return new WaitForSeconds(TickRate);
            timeElapsed += TickRate;
        }

        // 5. LIMPIEZA FINAL (Al terminar los 10 segundos)
        if (tornadoInstance != null) Destroy(tornadoInstance);

        foreach (var t in ultimateTotems)
        {
            if (t != null) Destroy(t); // Los tótems de la ulti desaparecen
        }

        // Finalizamos formalmente la habilidad
        EndAbility(); 
    }
}