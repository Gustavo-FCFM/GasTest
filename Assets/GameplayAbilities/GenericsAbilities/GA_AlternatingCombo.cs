using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ============================================================
// GA_AlternatingCombo
//
// Como GA_ComboSequence, pero con DOS secuencias que se ALTERNAN en cada activación:
// la 1ª activación corre FirstSequence, la 2ª corre SecondSequence, la 3ª FirstSequence,
// y así. Pensado para el ataque principal del Pirata: una activación = combo de 2
// estocadas (GA_LineAttack), la siguiente = combo de 2 barridos (GA_ConeAttack).
//
// Es una sola habilidad (mismo ciclo de vida que un combo normal: un CommitAbility,
// un cooldown, un EndAbility), así que el cooldown/costo van en ESTE asset. Reutiliza
// GA_ComboSequence.ComboStep para que cada paso se configure igual (habilidad + delay +
// override de AnimationID).
//
// El alternado es estado de runtime en el SERVIDOR (Activate es server-only): solo
// avanza cuando el ataque realmente sale (pasó el cooldown), así el patrón no se
// desincroniza al presionar durante el cooldown.
// ============================================================
[CreateAssetMenu(fileName = "GA_AlternatingCombo", menuName = "GAS/Generics/Alternating Combo")]
public class GA_AlternatingCombo : GameplayAbility
{
    [Header("Secuencia A (activaciones impares: 1ª, 3ª, ...)")]
    [Tooltip("Ej: combo de 2 estocadas (GA_LineAttack).")]
    public List<GA_ComboSequence.ComboStep> FirstSequence;

    [Header("Secuencia B (activaciones pares: 2ª, 4ª, ...)")]
    [Tooltip("Ej: combo de 2 barridos (GA_ConeAttack).")]
    public List<GA_ComboSequence.ComboStep> SecondSequence;

    // Cuál secuencia toca la próxima vez. Estado por instancia otorgada (server), no
    // se guarda en el asset.
    [System.NonSerialized] private bool _useSecond;

    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;

        CommitAbility();

        // Elegimos y alternamos SOLO cuando el ataque efectivamente sale.
        List<GA_ComboSequence.ComboStep> sequence = _useSecond ? SecondSequence : FirstSequence;
        _useSecond = !_useSecond;

        if (OwnerASC != null)
            OwnerASC.StartAbilityCoroutine(RunSequenceRoutine(sequence));
    }

    // Instancia y activa cada paso de la secuencia en orden, con sus delays (igual que
    // GA_ComboSequence.RunComboRoutine).
    private IEnumerator RunSequenceRoutine(List<GA_ComboSequence.ComboStep> sequence)
    {
        if (sequence != null)
        {
            foreach (var step in sequence)
            {
                if (step.AbilityToCast != null)
                {
                    GameplayAbility stepInstance = Instantiate(step.AbilityToCast);
                    stepInstance.Initialize(OwnerASC);
                    stepInstance.SourceTemplate = step.AbilityToCast; // para su índice en GameplayAbilityRegistry

                    if (step.AnimationClipOverride != null)
                        stepInstance.AnimationClip = step.AnimationClipOverride;
                    if (step.AnimationIDOverride > 0)
                        stepInstance.AnimationID = step.AnimationIDOverride;

                    stepInstance.Activate();
                }

                if (step.DelayAfter > 0)
                    yield return new WaitForSeconds(step.DelayAfter);
            }
        }

        EndAbility();
    }
}
