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
[CreateAssetMenu(fileName = "GA_ComboSequence", menuName = "GAS/Generics/Combo Sequence")]
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

        [Tooltip("Clip propio de ESTE paso: si lo asignás, pisa el AnimationClip de la habilidad. " +
                 "Es lo que permite que dos pasos de la MISMA habilidad se vean distintos (ej. dos " +
                 "cortes encadenados, o estocada en una secuencia y barrido en otra).")]
        public AnimationClip AnimationClipOverride;

        [Tooltip("Esquema viejo (solo si no usás clips): si > 0, fuerza a la habilidad a usar este AnimationID en lugar del suyo")]
        public int AnimationIDOverride;
    }

    [Header("Secuencia del Combo")]
    // Orden y timing de los pasos del combo.
    public List<ComboStep> Sequence;

    // Clip EFECTIVO de un paso: el override si lo tiene, si no el del asset de su
    // habilidad. Compartido con GA_AlternatingCombo (ambos usan ComboStep) para que
    // haya una sola definición de "qué clip usa este paso".
    public static AnimationClip ResolveStepClip(List<ComboStep> steps, int index)
    {
        if (steps == null || index < 0 || index >= steps.Count) return null;

        ComboStep step = steps[index];
        if (step.AnimationClipOverride != null) return step.AnimationClipOverride;
        return step.AbilityToCast != null ? step.AbilityToCast.AnimationClip : null;
    }

    // Velocidad a la que tiene que correr TODA la secuencia para entrar en el ritmo de
    // ataque del combo. Es el mismo criterio que ResolveAnimationSpeed usa con un clip
    // suelto (duración natural / ritmo), pero midiendo la secuencia completa.
    //
    // Solo aplica si el combo es un ATAQUE BÁSICO (UseAttackSpeedAsCooldown): ahí el
    // cooldown ES cada cuánto podés volver a pegar, así que el combo entero tiene que
    // entrar en ese hueco y acelerarse con los buffs de velocidad de ataque. En un
    // combo con cooldown normal (una espera, no un ritmo) se corre a velocidad natural.
    //
    // Compartido con GA_AlternatingCombo: se le pasa el ritmo ya resuelto porque
    // ResolveCooldownDuration es protected en cada instancia.
    public static float ResolveSequenceSpeed(List<ComboStep> steps, bool usesAttackRhythm, float rhythm)
    {
        if (!usesAttackRhythm || steps == null || steps.Count == 0 || rhythm <= 0f) return 1f;

        // Duración natural: los delays entre pasos, más lo que dure el clip del ÚLTIMO
        // (los anteriores ya están cubiertos por su propio delay).
        float natural = 0f;
        for (int i = 0; i < steps.Count; i++)
        {
            natural += Mathf.Max(0f, steps[i].DelayAfter);
            if (i != steps.Count - 1) continue;

            AnimationClip last = ResolveStepClip(steps, i);
            if (last != null) natural += last.length;
        }

        if (natural <= 0.001f) return 1f;
        // Mismo clamp que ResolveAnimationSpeed: un ritmo minúsculo no debe dar una
        // animación ilegible.
        return Mathf.Clamp(natural / rhythm, 0.1f, 10f);
    }

    // Clip de un paso, para que los observadores puedan replicarlo (ver
    // GameplayAbility.GetStepAnimationClip). Este combo tiene una sola secuencia, así
    // que el índice de secuencia se ignora.
    public override AnimationClip GetStepAnimationClip(int sequenceIndex, int stepIndex)
        => ResolveStepClip(Sequence, stepIndex);

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
        if (Sequence == null) { EndAbility(); yield break; }

        // Ritmo del combo: si es el ataque básico, toda la secuencia (clips y delays)
        // se comprime para entrar en un ciclo de ataque y acelera con los buffs.
        float speed = ResolveSequenceSpeed(Sequence, UseAttackSpeedAsCooldown, ResolveCooldownDuration());
        NetworkAbilitySystemComponent netAsc = OwnerASC != null
            ? OwnerASC.GetComponent<NetworkAbilitySystemComponent>() : null;

        for (int i = 0; i < Sequence.Count; i++)
        {
            ComboStep step = Sequence[i];

            if (step.AbilityToCast != null)
            {
                GameplayAbility stepInstance = Instantiate(step.AbilityToCast);
                stepInstance.Initialize(OwnerASC);
                stepInstance.SourceTemplate = step.AbilityToCast; // para resolver su índice en GameplayAbilityRegistry

                // El ciclo de vida (costo + cooldown) es del COMBO, no de sus pasos: el
                // padre ya hizo CommitAbility. Los limpiamos en el clon porque, si el
                // asset del paso trae su propio CooldownEffect —muy común cuando ese
                // asset también sirve como ataque suelto—, su CanActivate() ve el tag
                // que el padre ACABA de aplicar y el paso no se ejecuta: ni animación
                // ni daño, en silencio.
                stepInstance.CooldownEffect = null;
                stepInstance.CostEffect     = null;
                stepInstance.DisableCharges();

                if (step.AnimationClipOverride != null)
                    stepInstance.AnimationClip = step.AnimationClipOverride;
                if (step.AnimationIDOverride > 0)
                    stepInstance.AnimationID = step.AnimationIDOverride;

                // El paso no conoce el ritmo del combo (no tiene cooldown propio), así
                // que se lo imponemos: lo usan por igual el Animator y el timing del
                // golpe (HitTimingRoutine), así el daño sigue cayendo en el frame que
                // se ve aunque el swing se comprima.
                stepInstance.AnimationSpeedOverride = speed;

                stepInstance.Activate();

                // Activate() corre solo en el servidor y su PlayAnimation tiene guard de
                // dueño: sin esto, los clientes remotos no verían la animación del paso.
                if (netAsc != null) netAsc.ServerBroadcastComboStepAnimation(this, 0, i, stepInstance);
            }

            if (step.DelayAfter > 0)
                yield return new WaitForSeconds(step.DelayAfter / speed);
        }

        EndAbility();
    }
}
