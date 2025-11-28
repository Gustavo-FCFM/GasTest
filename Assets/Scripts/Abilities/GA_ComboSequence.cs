using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "GA_BerserkerCombo", menuName = "GAS/Generics/Combo Sequence")]
public class GA_ComboSequence : GameplayAbility
{
    [System.Serializable]
    public struct ComboStep
    {
        [Tooltip("La habilidad a ejecutar (Cono, Linea, etc)")]
        public GameplayAbility AbilityToCast;
        
        [Tooltip("Tiempo a esperar DESPUÉS de lanzar esta habilidad antes de la siguiente")]
        public float DelayAfter;
    }

    [Header("Secuencia del Combo")]
    public List<ComboStep> Sequence;

    public override void Activate()
    {
        if (!CanActivate()) return;

        // 1. Pagar el coste inicial (maná/energía) de TODO el combo aquí
        if (CostEffect != null) OwnerASC.ApplyGameplayEffect(CostEffect, this);

        // 2. Iniciar la secuencia usando el ASC como motor
        if (OwnerASC != null)
        {
            OwnerASC.StartAbilityCoroutine(RunComboRoutine());
        }
    }

    private IEnumerator RunComboRoutine()
    {
        // Recorremos la lista paso a paso
        foreach (var step in Sequence)
        {
            if (step.AbilityToCast != null)
            {
                // A) Creamos una instancia temporal de la sub-habilidad
                // Usamos Instantiate para no modificar el asset original y poder inicializarla
                GameplayAbility stepInstance = Instantiate(step.AbilityToCast);
                stepInstance.Initialize(OwnerASC);

                // B) La activamos
                // Nota: Las sub-habilidades deberían tener coste 0 para que no cobren doble
                stepInstance.Activate();
            }

            // C) Esperamos el intervalo definido
            if (step.DelayAfter > 0)
            {
                yield return new WaitForSeconds(step.DelayAfter);
            }
        }

        // 3. Al terminar todos los pasos, termina la habilidad principal
        // Aquí es donde aplicamos el Cooldown final del combo completo
        EndAbility();
    }
}