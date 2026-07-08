using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ============================================================
// GA_ComboSequence
//
// Encadena otras GameplayAbility una detrás de otra con delays
// configurables (ej: golpe de cono, pausa, golpe en línea). No
// detecta nada por sí misma — crea una instancia temporal de cada
// paso y le delega Activate(); el daño/VFX de cada paso corre con
// la lógica propia de esa habilidad.
// ============================================================
[CreateAssetMenu(fileName = "GA_BerserkerCombo", menuName = "GAS/Generics/Combo Sequence")]
public class GA_ComboSequence : GameplayAbility
{
    // Un paso del combo: qué habilidad lanzar y cuánto esperar después.
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
    // Orden y timing de los pasos del combo.
    public List<ComboStep> Sequence;

    // Valida, cobra costo/cooldown, y arranca la corutina que ejecuta
    // los pasos del combo uno por uno.
    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;

        CommitAbility();

        if (OwnerASC != null)
            OwnerASC.StartAbilityCoroutine(RunComboRoutine());
    }

    // Instancia y activa cada paso del combo en orden, respetando los
    // delays configurados entre uno y otro.
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
