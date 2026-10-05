using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ============================================================
// GA_ChargedAttack  (genérico — "mantener para cargar, soltar para pegar")
//
// Como la Q de Sion del LoL: se aprieta y empieza a cargar; al SOLTAR sale el golpe de
// la etapa a la que llegó. Tocar y soltar enseguida da la primera (el golpe rápido);
// mantener más da las siguientes. Al llegar a MaxChargeTime se suelta solo.
//
// CADA ETAPA ES OTRA HABILIDAD (Stages): un cono, una línea, un proyectil... Así el
// mismo script sirve para el ataque cargado del Maestro de batalla (tres conos con cada
// vez más daño, el tercero aturde), para "mantener para apuntar, soltar para lanzar" en
// una de distancia, y para pasar a este sistema el Golpe final del Inmortal. La etapa se
// clona y se ejecuta igual que un paso de combo (sin cooldown propio: el cooldown es de
// esta habilidad y se cobra AL SOLTAR).
//
// Es de MANTENER (IHoldAbility): el cliente avisa al soltar (como con el escudo), y la
// pose de carga son los clips de mantener (ChargeStartClip / ChargeLoopClip). Mientras
// carga se le puede aplicar un efecto (ChargingEffect: una ralentización). Un aturdido
// o la muerte cortan la carga SIN golpe.
//
// La animación de la etapa llega a todas las pantallas por el mismo RPC que los pasos de
// combo (GetStepAnimationClip devuelve el clip de cada etapa).
// ============================================================
[CreateAssetMenu(fileName = "GA_ChargedAttack", menuName = "GAS/Generics/Charged Attack")]
public class GA_ChargedAttack : GameplayAbility, IHoldAbility
{
    [System.Serializable]
    public struct ChargeStage
    {
        [Tooltip("Segundos de carga para llegar a esta etapa. La primera va en 0 (tocar y soltar).")]
        public float MinChargeTime;

        [Tooltip("Lo que sale al soltar en esta etapa (un cono, una línea, un proyectil...). Trae " +
                 "su propio daño, efectos y clip.")]
        public GameplayAbility Ability;
    }

    [Header("Etapas")]
    [Tooltip("En orden de MinChargeTime. Al soltar sale la última a la que se llegó.")]
    public List<ChargeStage> Stages = new List<ChargeStage>();

    [Tooltip("Al llegar a este tiempo de carga se suelta solo. 0 = sin tope (solo la red de " +
             "seguridad de abajo).")]
    public float MaxChargeTime = 2f;

    [Tooltip("Corte de seguridad si MaxChargeTime está en 0 y el aviso de soltar nunca llega.")]
    public float HoldSafetyTimeout = 10f;

    [Header("Mientras Carga")]
    [Tooltip("Efecto que se aplica al empezar a cargar y se saca al soltar (una ralentización, " +
             "por ejemplo). Opcional.")]
    public GameplayEffect ChargingEffect;

    [Header("Animación de Carga")]
    [Tooltip("Pose de carga EN BUCLE. Con este solo ya se ve.")]
    public AnimationClip ChargeLoopClip;

    [Tooltip("OPCIONAL: el gesto de empezar a cargar (una vez). Vacío = directo al bucle.")]
    public AnimationClip ChargeStartClip;

    // IHoldAbility: al soltar no hay clip de "bajar", sale el golpe de la etapa.
    public AnimationClip HoldLoopClip   => ChargeLoopClip;
    public AnimationClip HoldStartClip  => ChargeStartClip;
    public AnimationClip HoldEndClip    => null;
    public AnimationClip HoldImpactClip => null;

    [System.NonSerialized] private bool  _holding;
    [System.NonSerialized] private float _chargeStartedAt;
    public bool UsesHoldInput => true;
    public bool IsHolding => _holding;

    // Para el RPC de animación de los pasos: la "secuencia" 0 son las etapas.
    public override AnimationClip GetStepAnimationClip(int sequenceIndex, int stepIndex)
        => stepIndex >= 0 && stepIndex < Stages.Count && Stages[stepIndex].Ability != null
            ? Stages[stepIndex].Ability.AnimationClip : null;

    // Para la barra de carga (UI_ChargeBar): dónde empieza cada etapa y cuándo se suelta
    // sola. Sin tope (MaxChargeTime en 0), la barra termina un poco después de la última.
    public override bool GetChargeStages(List<float> stageTimes, out float maxTime)
    {
        stageTimes.Clear();
        float last = 0f;
        foreach (ChargeStage stage in Stages)
        {
            if (stage.Ability == null) continue;
            stageTimes.Add(stage.MinChargeTime);
            last = Mathf.Max(last, stage.MinChargeTime);
        }

        maxTime = MaxChargeTime > 0f ? MaxChargeTime : last + 0.5f;
        return stageTimes.Count > 1 && maxTime > 0f;
    }

    // =========================================================
    // CARGAR
    // =========================================================

    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;
        if (_holding) return;

        _holding         = true;
        _chargeStartedAt = Time.time;

        if (CostEffect != null)     OwnerASC.ApplyGameplayEffect(CostEffect, this);
        if (ChargingEffect != null) OwnerASC.ApplyGameplayEffect(ChargingEffect, OwnerASC);

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        if (pc != null) pc.PlayHoldAnimation(this);

        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
        if (netAsc != null)
            netAsc.ServerBroadcastHoldAnimation(this, NetworkAbilitySystemComponent.EHoldAnimationPhase.Start);

        OwnerASC.StartAbilityCoroutine(ChargeRoutine());
    }

    // Vigila la carga en el servidor: se suelta sola al tope, y se corta SIN golpe si lo
    // aturden o muere.
    private IEnumerator ChargeRoutine()
    {
        while (_holding)
        {
            if (OwnerASC == null || OwnerASC.HasTag(EGameplayTag.State_Dead) ||
                OwnerASC.HasTag(EGameplayTag.State_Stunned))
            {
                Cancel();
                yield break;
            }

            float charged = Time.time - _chargeStartedAt;
            if (MaxChargeTime > 0f && charged >= MaxChargeTime) break;
            if (MaxChargeTime <= 0f && HoldSafetyTimeout > 0f && charged >= HoldSafetyTimeout) break;

            yield return null;
        }

        if (_holding) Release();
    }

    // =========================================================
    // SOLTAR (IHoldAbility.EndHold)
    // =========================================================

    // La llama el dueño al soltar el botón (vía NetworkASC.ServerRequestEndHoldAbility) o
    // la carga al llegar al tope. Idempotente.
    public void EndHold() => Release();

    private void Release()
    {
        if (!_holding || OwnerASC == null) return;

        float charged = Time.time - _chargeStartedAt;
        StopCharging();

        if (CooldownEffect != null)
            OwnerASC.ApplyGameplayEffect(CooldownEffect, this, ResolveCooldownDuration());

        int stageIndex = ResolveStage(charged);
        GameplayAbility stage = FireStage(stageIndex);

        // El jugador queda ocupado hasta que termina el golpe. El aviso de fin va con el
        // nombre de esta habilidad (o del switch que la envuelve), que es lo que el dueño
        // está sosteniendo: así se libera aunque la carga se haya soltado sola.
        float duration = 0.1f;
        if (stage != null && stage.AnimationClip != null)
            duration = stage.AnimationClip.length / Mathf.Max(0.1f, stage.ResolveAnimationSpeed());
        OwnerASC.StartAbilityCoroutine(EndAfter(duration));
    }

    // Cortada sin golpe (aturdido, muerte): sale el cooldown igual y se libera al jugador.
    private void Cancel()
    {
        if (!_holding) return;
        StopCharging();

        if (OwnerASC != null && CooldownEffect != null)
            OwnerASC.ApplyGameplayEffect(CooldownEffect, this, ResolveCooldownDuration());

        EndAbility();
    }

    private void StopCharging()
    {
        _holding = false;

        if (ChargingEffect != null) OwnerASC.RemoveEffectsByDefinition(ChargingEffect);

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        if (pc != null) pc.StopHoldAnimation();

        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
        if (netAsc != null)
            netAsc.ServerBroadcastHoldAnimation(this, NetworkAbilitySystemComponent.EHoldAnimationPhase.Stop);
    }

    // La última etapa cuyo tiempo mínimo se alcanzó.
    private int ResolveStage(float charged)
    {
        int best = -1;
        for (int i = 0; i < Stages.Count; i++)
            if (Stages[i].Ability != null && charged >= Stages[i].MinChargeTime) best = i;
        return best;
    }

    // Clona y ejecuta la etapa, igual que un paso de combo.
    private GameplayAbility FireStage(int index)
    {
        if (index < 0) return null;

        GameplayAbility template = Stages[index].Ability;
        GameplayAbility instance = Instantiate(template);
        instance.Initialize(OwnerASC);
        instance.SourceTemplate  = template;
        instance.CooldownEffect  = null;   // el cooldown es de la carga, no de la etapa
        instance.CostEffect      = null;
        instance.DisableCharges();
        instance.IsInterruptible = false;

        instance.Activate();

        // Activate() corre en el servidor y su PlayAnimation solo anima al host: esto se
        // la manda al dueño remoto y a los demás (ver GetStepAnimationClip).
        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
        if (netAsc != null) netAsc.ServerBroadcastComboStepAnimation(this, 0, index, instance);

        return instance;
    }

    private IEnumerator EndAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        EndAbility();
    }

    public override void DrawGizmos(Transform origin)
    {
        if (Stages.Count > 0 && Stages[Stages.Count - 1].Ability != null)
            Stages[Stages.Count - 1].Ability.DrawGizmos(origin);
    }
}
