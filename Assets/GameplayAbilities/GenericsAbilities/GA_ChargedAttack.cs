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
// carga se le pueden aplicar efectos (WhileChargingEffects: una ralentización). Un aturdido
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
        [Label("Carga mínima (s)")]
        public float MinChargeTime;

        [Tooltip("Lo que sale al soltar en esta etapa (un cono, una línea, un proyectil...). Trae " +
                 "su propio daño, efectos y clip.")]
        [Label("Habilidad al soltar")]
        public GameplayAbility Ability;

        [Tooltip("ZOOM de la cámara del que carga al llegar a esta etapa: su campo de visión " +
                 "multiplicado por esto (0.8 = se acerca un 20 %). 0 o 1 = sin zoom. El arco del " +
                 "Explorador se acerca un poco más en cada fase.")]
        [Label("Zoom de cámara (× visión; 1 = sin zoom)")]
        [Range(0f, 1f)] public float CameraZoom;
    }

    [Section("Etapas de carga")]
    [Tooltip("En orden de MinChargeTime. Al soltar sale la última a la que se llegó.")]
    [Label("Etapas")]
    public List<ChargeStage> Stages = new List<ChargeStage>();

    [Tooltip("Al llegar a este tiempo de carga se suelta solo. 0 = sin tope (solo la red de " +
             "seguridad de abajo).")]
    [Label("Carga máxima (s)")]
    public float MaxChargeTime = 2f;

    [Tooltip("Mientras carga, la cámara pasa a la de apuntar (la de los lanzamientos apuntados: cerca, " +
             "sobre el hombro), para apuntar con calma mientras se mantiene (la Mordedura de hielo de la " +
             "Evocación). Apagado = solo el zoom de cada etapa (el arco del Explorador).")]
    [Label("Cámara de apuntar al cargar")]
    public bool AimCameraWhileCharging = false;

    public override bool AimsCameraWhileHeld => AimCameraWhileCharging;

    [Tooltip("Soltar antes de llegar a la PRIMERA etapa no la pierde: sigue cargando y sale apenas " +
             "llega (la Mordedura de hielo de la Evocación: la flecha normal necesita 1 s). Apagado = " +
             "soltar antes no saca nada (lo de siempre).")]
    [Label("Soltar antes: sale al llegar a la primera")]
    public bool ReleaseWaitsForFirstStage = false;

    [Tooltip("Lo que se aplica al empezar a cargar y se saca al soltar (una ralentización, " +
             "por ejemplo). Opcional.")]
    [Label("Efectos al cargar")]
    public List<GameplayEffect> WhileChargingEffects = new List<GameplayEffect>();

    [Tooltip("CARGA COMPLETA AL INSTANTE: con este tag, apretar suelta YA la última etapa, sin " +
             "cargar, y gasta UNA acumulación del tag (Ataque rápido del Explorador: sus próximos 3 " +
             "disparos salen con la carga máxima). None = siempre se carga.")]
    [Label("Carga completa con tag")]
    public EGameplayTag FullChargeTag = EGameplayTag.None;

    [Section(AbilitySection.Advanced, startCollapsed: true)]
    [Tooltip("Corte de seguridad si MaxChargeTime está en 0 y el aviso de soltar nunca llega.")]
    [Label("Corte de seguridad (s)")]
    public float HoldSafetyTimeout = 10f;

    [Section(AbilitySection.Animation)]
    [Tooltip("Pose de carga EN BUCLE. Con este solo ya se ve.")]
    [Label("Clip: cargando (bucle)")]
    public AnimationClip ChargeLoopClip;

    [Tooltip("OPCIONAL: el gesto de empezar a cargar (una vez). Vacío = directo al bucle.")]
    [Label("Clip: empezar a cargar")]
    public AnimationClip ChargeStartClip;

    // IHoldAbility: al soltar no hay clip de "bajar", sale el golpe de la etapa.
    public AnimationClip HoldLoopClip   => ChargeLoopClip;
    public AnimationClip HoldStartClip  => ChargeStartClip;
    public AnimationClip HoldEndClip    => null;
    public AnimationClip HoldImpactClip => null;

    [System.NonSerialized] private bool  _holding;
    [System.NonSerialized] private float _chargeStartedAt;
    [System.NonSerialized] private bool  _releaseQueued;   // soltó antes de la primera etapa (ReleaseWaitsForFirstStage)
    // Con la carga completa al instante (Ataque rápido) NO es de mantener: apretar dispara ya.
    // Lo mira también el dueño (los tags le llegan), así no arranca la pose ni la barra de carga.
    public bool UsesHoldInput => !FiresInstantly;

    private bool FiresInstantly
        => FullChargeTag != EGameplayTag.None && OwnerASC != null && OwnerASC.HasTag(FullChargeTag);

    // Cada disparo tiene su número: el aviso de fin de uno viejo no corta una carga nueva.
    [System.NonSerialized] private int _shotId;
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

        // Un disparo anterior que todavía no avisó su fin (su animación dura más que el
        // cooldown) ya no tiene que cortar esta carga.
        _shotId++;

        // Carga completa al instante (Ataque rápido): sale la última etapa ya, sin cargar.
        if (FiresInstantly)
        {
            FireInstantly();
            return;
        }

        _holding         = true;
        _chargeStartedAt = Time.time;
        _releaseQueued   = false;

        if (CostEffect != null) OwnerASC.ApplyGameplayEffect(CostEffect, CostSource);
        ApplyEffectsTo(WhileChargingEffects, OwnerASC);

        // No pasa por CommitAbility (el cooldown se cobra al soltar): los efectos "al
        // activarse" y los VFX "al lanzar" se piden acá, al empezar a cargar.
        ApplyActivationEffects();
        PlayCastVisuals();

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
            if (_releaseQueued && charged >= FirstStageTime()) break;   // soltó antes: sale al llegar
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

        // Soltó antes de la primera etapa: se anota y la carga sigue hasta llegar (ChargeRoutine).
        if (ReleaseWaitsForFirstStage && charged < FirstStageTime())
        {
            _releaseQueued = true;
            return;
        }

        StopCharging();

        if (CooldownEffect != null)
            OwnerASC.ApplyGameplayEffect(CooldownEffect, this, ResolveCooldownDuration());

        int stageIndex = ResolveStage(charged);
        GameplayAbility stage = FireStage(stageIndex);

        // El jugador queda ocupado hasta que termina el golpe. El aviso de fin va con el
        // nombre de esta habilidad (o del switch que la envuelve), que es lo que el dueño
        // está sosteniendo: así se libera aunque la carga se haya soltado sola.
        ScheduleEnd(stage);
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

        if (WhileChargingEffects != null)
            foreach (GameplayEffect effect in WhileChargingEffects)
                if (effect != null) OwnerASC.RemoveEffectsByDefinition(effect);

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        if (pc != null) pc.StopHoldAnimation();

        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
        if (netAsc != null)
            netAsc.ServerBroadcastHoldAnimation(this, NetworkAbilitySystemComponent.EHoldAnimationPhase.Stop);
    }

    // Cuándo se llega a la primera etapa que tiene habilidad.
    private float FirstStageTime()
    {
        foreach (ChargeStage stage in Stages)
            if (stage.Ability != null) return stage.MinChargeTime;
        return 0f;
    }

    // La última etapa cuyo tiempo mínimo se alcanzó.
    private int ResolveStage(float charged)
    {
        int best = -1;
        for (int i = 0; i < Stages.Count; i++)
            if (Stages[i].Ability != null && charged >= Stages[i].MinChargeTime) best = i;
        return best;
    }

    // Lo que saldría si soltara ahora, con 'heldSeconds' de carga (para la trayectoria que
    // ve el dueño mientras tensa: TrajectoryPreview).
    public GameplayAbility StageAt(float heldSeconds)
    {
        int stage = ResolveStage(heldSeconds);
        return stage >= 0 ? Stages[stage].Ability : null;
    }

    // La curva de la flecha de la fase que saldría ahora (si esa fase dispara un proyectil y
    // tiene la curva prendida).
    public override GA_ProjectileShoot ProjectileToPreview(float heldSeconds)
        => StageAt(heldSeconds) is GA_ProjectileShoot shot && shot.ShowTrajectoryWhileAiming ? shot : null;

    // La última etapa que tiene habilidad (la carga completa).
    private int LastStageIndex()
    {
        for (int i = Stages.Count - 1; i >= 0; i--)
            if (Stages[i].Ability != null) return i;
        return -1;
    }

    // Zoom de la cámara del dueño: el de la etapa a la que llegó (ver ChargeStage.CameraZoom).
    public override float HeldCameraZoom(float heldSeconds)
    {
        int stage = ResolveStage(heldSeconds);
        if (stage < 0) return 1f;
        float zoom = Stages[stage].CameraZoom;
        return zoom > 0f ? zoom : 1f;
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

    // Sin cargar: gasta una acumulación del tag, cobra y suelta la última etapa.
    private void FireInstantly()
    {
        OwnerASC.RemoveOneEffectWithTag(FullChargeTag);

        if (CostEffect != null) OwnerASC.ApplyGameplayEffect(CostEffect, CostSource);
        ApplyActivationEffects();
        PlayCastVisuals();

        if (CooldownEffect != null)
            OwnerASC.ApplyGameplayEffect(CooldownEffect, this, ResolveCooldownDuration());

        ScheduleEnd(FireStage(LastStageIndex()));
    }

    // El jugador queda ocupado hasta que termina el golpe de la etapa.
    private void ScheduleEnd(GameplayAbility stage)
    {
        float duration = 0.1f;
        if (stage != null && stage.AnimationClip != null)
            duration = stage.AnimationClip.length / Mathf.Max(0.1f, stage.ResolveAnimationSpeed());
        OwnerASC.StartAbilityCoroutine(EndAfter(duration, _shotId));
    }

    private IEnumerator EndAfter(float seconds, int shotId)
    {
        yield return new WaitForSeconds(seconds);
        if (shotId != _shotId || _holding) yield break;   // ya empezó otro disparo: ese avisa solo
        EndAbility();
    }

    // La forma es la de la ÚLTIMA etapa (la carga completa).
    private GameplayAbility LastStage
        => Stages != null && Stages.Count > 0 ? Stages[Stages.Count - 1].Ability : null;

    public override void DrawGizmos(Transform origin)
    {
        if (LastStage != null) LastStage.DrawGizmos(origin);
    }

#if UNITY_EDITOR
    // Las manijas ajustan el asset de la última etapa (su forma es la que se ve).
    public override void DrawSceneHandles(Transform origin)
    {
        if (LastStage != null && LastStage != this) LastStage.DrawSceneHandles(origin);
    }
#endif

    // Envoltorio: busca y golpea la etapa que sale, no esta.
    public override bool UsesTargetLayer => false;
    public override bool UsesHitEffects  => false;

    // =========================================================
    // DATOS VIEJOS (solo para pasarlos a la lista; ver GameplayAbility.UpgradeLegacyData)
    // =========================================================

    [SerializeField, HideInInspector] private GameplayEffect ChargingEffect;

    protected override void OnUpgradeLegacyData(ref bool changed)
    {
        base.OnUpgradeLegacyData(ref changed);

        if (ChargingEffect != null)
        {
            if (WhileChargingEffects == null) WhileChargingEffects = new List<GameplayEffect>();
            WhileChargingEffects.Add(ChargingEffect);
            ChargingEffect = null;
            changed = true;
        }
    }
}
