using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "GA_BerserkerCombo", menuName = "GAS/Generics/Combo Sequence")]
public class GA_ComboSequence : GameplayAbility
{
    [System.Serializable]
    public struct ComboStep
    {
        [Tooltip("La habilidad a ejecutar (Cono, Línea, etc)")]
        public GameplayAbility AbilityToCast;

        [Tooltip("Tiempo a esperar DESPUÉS de lanzar esta habilidad antes de la siguiente")]
        public float DelayAfter;

        [Tooltip("Si > 0, fuerza a la habilidad a usar este AnimationID en lugar del suyo")]
        public int AnimationIDOverride;
    }

    [Header("Secuencia del Combo")]
    public List<ComboStep> Sequence;

    public override void Activate()
    {
        if (!IsServer) return;   // ← NUEVO
        if (!CanActivate()) return;

        CommitAbility();

        if (OwnerASC != null)
            OwnerASC.StartAbilityCoroutine(RunComboRoutine());
    }

    private IEnumerator RunComboRoutine()
    {
        foreach (var step in Sequence)
        {
            if (step.AbilityToCast != null)
            {
                GameplayAbility stepInstance = Instantiate(step.AbilityToCast);
                stepInstance.Initialize(OwnerASC);

                if (step.AnimationIDOverride > 0)
                    stepInstance.AnimationID = step.AnimationIDOverride;

                stepInstance.Activate();
            }

            if (step.DelayAfter > 0)
                yield return new WaitForSeconds(step.DelayAfter);
        }

        EndAbility();
    }
}