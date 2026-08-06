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

    // Índices de secuencia que viajan por red en la réplica de animación de cada paso
    // (ver GetStepAnimationClip). Son parte del contrato con el observador: si cambian,
    // se rompe la resolución del clip del otro lado.
    private const int FirstSequenceIndex  = 0;
    private const int SecondSequenceIndex = 1;

    // Clip de un paso, para que los observadores puedan replicarlo (ver
    // GameplayAbility.GetStepAnimationClip). Acá el índice de secuencia SÍ importa:
    // es lo que distingue la secuencia A de la B.
    public override AnimationClip GetStepAnimationClip(int sequenceIndex, int stepIndex)
        => GA_ComboSequence.ResolveStepClip(
               sequenceIndex == SecondSequenceIndex ? SecondSequence : FirstSequence, stepIndex);

    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;

        CommitAbility();

        // Elegimos y alternamos SOLO cuando el ataque efectivamente sale.
        int sequenceIndex = _useSecond ? SecondSequenceIndex : FirstSequenceIndex;
        List<GA_ComboSequence.ComboStep> sequence = _useSecond ? SecondSequence : FirstSequence;
        _useSecond = !_useSecond;

        if (OwnerASC != null)
            OwnerASC.StartAbilityCoroutine(RunSequenceRoutine(sequence, sequenceIndex));
    }

    // Instancia y activa cada paso de la secuencia en orden, con sus delays (igual que
    // GA_ComboSequence.RunComboRoutine).
    private IEnumerator RunSequenceRoutine(List<GA_ComboSequence.ComboStep> sequence, int sequenceIndex)
    {
        if (sequence != null)
        {
            // Ritmo del combo: si es el ataque básico, toda la secuencia (clips y
            // delays) se comprime para entrar en un ciclo de ataque y acelera con los
            // buffs de velocidad de ataque.
            float speed = GA_ComboSequence.ResolveSequenceSpeed(
                sequence, UseAttackSpeedAsCooldown, ResolveCooldownDuration());

            NetworkAbilitySystemComponent netAsc = OwnerASC != null
                ? OwnerASC.GetComponent<NetworkAbilitySystemComponent>() : null;

            for (int i = 0; i < sequence.Count; i++)
            {
                GA_ComboSequence.ComboStep step = sequence[i];

                if (step.AbilityToCast != null)
                {
                    GameplayAbility stepInstance = Instantiate(step.AbilityToCast);
                    stepInstance.Initialize(OwnerASC);
                    stepInstance.SourceTemplate = step.AbilityToCast; // para su índice en GameplayAbilityRegistry

                    // El ciclo de vida (costo + cooldown) es del COMBO, no de sus pasos:
                    // el padre ya hizo CommitAbility. Los limpiamos en el clon porque, si
                    // el asset del paso trae su propio CooldownEffect —muy común cuando
                    // ese asset también sirve como ataque suelto—, su CanActivate() ve el
                    // tag que el padre ACABA de aplicar y el paso no se ejecuta: ni
                    // animación ni daño, en silencio. Era justo el caso del Berserker,
                    // cuyo paso compartía el CooldownEffect del combo.
                    stepInstance.CooldownEffect = null;
                    stepInstance.CostEffect     = null;

                    if (step.AnimationClipOverride != null)
                        stepInstance.AnimationClip = step.AnimationClipOverride;
                    if (step.AnimationIDOverride > 0)
                        stepInstance.AnimationID = step.AnimationIDOverride;

                    // El paso no conoce el ritmo del combo (no tiene cooldown propio),
                    // así que se lo imponemos: lo usan por igual el Animator y el timing
                    // del golpe (HitTimingRoutine), así el daño sigue cayendo en el frame
                    // que se ve aunque el swing se comprima.
                    stepInstance.AnimationSpeedOverride = speed;

                    stepInstance.Activate();

                    // Activate() corre solo en el servidor y su PlayAnimation tiene guard
                    // de dueño: sin esto, los clientes remotos no verían la animación del
                    // paso (se quedaban con la del combo padre, que no tiene clip propio).
                    if (netAsc != null)
                        netAsc.ServerBroadcastComboStepAnimation(this, sequenceIndex, i, stepInstance);
                }

                if (step.DelayAfter > 0)
                    yield return new WaitForSeconds(step.DelayAfter / speed);
            }
        }

        EndAbility();
    }
}
