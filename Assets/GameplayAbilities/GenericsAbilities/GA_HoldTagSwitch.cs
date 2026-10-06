using UnityEngine;

// ============================================================
// GA_HoldTagSwitch  (genérico — un GA_TagSwitch entre habilidades de MANTENER)
//
// El clic derecho del Maestro de batalla: en postura defensiva es el escudo, en
// ofensiva el ataque cargado. Las dos son de mantener (se activan al apretar y algo
// pasa al soltar), y el cliente trata distinto a esas: soltar el botón las termina. Un
// GA_TagSwitch normal no es de mantener, así que el cliente nunca avisaba el soltar.
//
// Este switch ES de mantener (IHoldAbility) y le pasa todo a la variante activa:
// si sostiene, el soltar (EndHold), los clips de la animación de mantener y el
// cooldown que muestra el HUD.
//
// UNA VARIANTE PUEDE NO SER DE MANTENER (desde el 6 de octubre de 2026): el Apuntado del
// Monje es el bloqueo (mantener), pero con Ki es la Defensa paciente, un buff de un toque.
// UsesHoldInput pregunta a la variante que se dispararía AHORA, así el botón se comporta
// como ella. Y ConsumeTag de la variante funciona igual que en el GA_TagSwitch (el Ki se
// gasta al usarlo).
//
// DIFERENCIAS CON EL GA_TagSwitch:
//   · Cada variante paga SU costo y SU cooldown (StripVariantCooldowns apagado): el
//     escudo no tiene, el ataque cargado sí, y no tienen por qué compartirlo. El switch
//     no cobra nada propio.
//   · Se puede activar solo si la variante que se dispararía puede (su cooldown, su
//     energía).
//   · Su fin lo avisan las variantes con el nombre del switch (ReportEndAs), que es lo
//     que el dueño apretó y está sosteniendo.
// ============================================================
[CreateAssetMenu(fileName = "GA_HoldTagSwitch", menuName = "GAS/Generics/Hold Tag Switch")]
public class GA_HoldTagSwitch : GA_TagSwitch, IHoldAbility
{
    protected override bool StripVariantCooldowns => false;

    // La variante que se dispararía ahora, ya clonada (con dueño, para poder preguntarle).
    private GameplayAbility CurrentInstance()
    {
        GameplayAbility template = ResolveTemplate(out _, out _);
        return template != null ? GetOrCreateInstance(template) : null;
    }

    // La que está sosteniendo, o si no la que se dispararía.
    private IHoldAbility ActiveHold
        => (_lastResolved is IHoldAbility held && held.IsHolding) ? held
         : ResolveTemplate(out _, out _) as IHoldAbility;

    // =========================================================
    // ACTIVACIÓN
    // =========================================================

    public override bool CanActivate()
    {
        if (!base.CanActivate()) return false;
        GameplayAbility instance = CurrentInstance();
        return instance != null && instance.CanActivate();
    }

    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;

        GameplayAbility template = ResolveTemplate(out bool consumeTag, out EGameplayTag usedTag);
        GameplayAbility instance = template != null ? GetOrCreateInstance(template) : null;
        if (instance == null)
        {
            EndAbility();
            return;
        }

        _lastResolved      = instance;
        _lastResolvedFrame = Time.frameCount;
        instance.AnimationSpeedOverride = AnimationSpeedOverride;
        instance.Activate();   // la variante cobra lo suyo y arranca el mantenido

        // La de un solo uso (el Ki) se gasta al usarla.
        if (consumeTag) ConsumeTagFromOwner(usedTag);
    }

    // =========================================================
    // IHoldAbility: todo a la variante
    // =========================================================

    // Es de mantener si lo es la variante: la que acaba de correr (en el mismo frame, para
    // la capa de red, que pregunta DESPUÉS de activar y con el Ki ya gastado), o si no la
    // que se dispararía ahora según los tags.
    [System.NonSerialized] private int _lastResolvedFrame = -1;

    public bool UsesHoldInput
    {
        get
        {
            if (IsHolding) return true;
            if (_lastResolved != null && _lastResolvedFrame == Time.frameCount) return HoldInput.IsHold(_lastResolved);
            GameplayAbility template = ResolveTemplate(out _, out _);
            return template == null || HoldInput.IsHold(template);
        }
    }
    public bool IsHolding => _lastResolved is IHoldAbility held && held.IsHolding;

    public void EndHold()
    {
        if (_lastResolved is IHoldAbility held) held.EndHold();
    }

    public AnimationClip HoldLoopClip   => ActiveHold?.HoldLoopClip;
    public AnimationClip HoldStartClip  => ActiveHold?.HoldStartClip;
    public AnimationClip HoldEndClip    => ActiveHold?.HoldEndClip;
    public AnimationClip HoldImpactClip => ActiveHold?.HoldImpactClip;

    // La cámara de apuntar, si la variante la pide (en ofensiva, apuntar la lanza).
    public override bool AimsCameraWhileHeld => ActiveHold is GameplayAbility held && held.AimsCameraWhileHeld;

    // La barra de carga es la de la variante (en ofensiva, el ataque cargado).
    public override bool GetChargeStages(System.Collections.Generic.List<float> stageTimes, out float maxTime)
    {
        maxTime = 0f;
        return ActiveHold is GameplayAbility held && held.GetChargeStages(stageTimes, out maxTime);
    }

    // Al soltar, la variante muestra lo suyo en el dueño (el lanzamiento de la lanza).
    public override void PredictOwnerReleaseVisuals(PlayerController pc)
    {
        GameplayAbility instance = CurrentInstance();
        if (instance != null) instance.PredictOwnerReleaseVisuals(pc);
    }

    // El HUD muestra el cooldown de la variante que se dispararía ahora (en ofensiva, el
    // del ataque cargado; en defensiva, el del escudo).
    public override GameplayEffect CooldownEffectForDisplay
    {
        get
        {
            GameplayAbility template = ResolveTemplate(out _, out _);
            return template != null ? template.CooldownEffect : CooldownEffect;
        }
    }
}
