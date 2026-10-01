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
// cooldown que muestra el HUD. Todas sus variantes tienen que ser de mantener.
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

        GameplayAbility instance = CurrentInstance();
        if (instance == null)
        {
            EndAbility();
            return;
        }

        _lastResolved = instance;
        instance.AnimationSpeedOverride = AnimationSpeedOverride;
        instance.Activate();   // la variante cobra lo suyo y arranca el mantenido
    }

    // =========================================================
    // IHoldAbility: todo a la variante
    // =========================================================

    public bool UsesHoldInput => true;
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
