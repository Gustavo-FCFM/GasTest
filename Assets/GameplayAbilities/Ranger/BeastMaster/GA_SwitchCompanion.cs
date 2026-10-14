using UnityEngine;

// ============================================================
// GA_SwitchCompanion  (Cambio de compañero — Maestro de bestias, 14 de octubre de 2026)
//
// La rueda para elegir compañero (Lobo, Oso, Búho). Las opciones NO viven acá: las lee
// del CompanionManager del dueño (su PassiveBehaviorsPrefab), que es quien hace aparecer
// al compañero. Así nombre, ícono, prefab y espera están en un solo lugar.
//
//   · El ícono del botón es el del compañero ACTIVO: el jugador sabe siempre cuál tiene.
//   · En la rueda, el que está esperando para volver (murió hace poco) se ve apagado.
//   · Elegir el que ya está no hace nada ni cobra.
// ============================================================
[CreateAssetMenu(fileName = "GA_SwitchCompanion", menuName = "GAS/Specific Abilities/Ranger/Switch Companion")]
public class GA_SwitchCompanion : GameplayAbility, IRadialMenuAbility
{
    private CompanionManager Manager => OwnerASC != null ? OwnerASC.GetComponentInChildren<CompanionManager>() : null;

    public float MaxRadialRange => 10f;

    public Sprite[] RadialIcons
    {
        get
        {
            CompanionManager m = Manager;
            if (m == null) return new Sprite[0];
            var icons = new Sprite[m.Companions.Count];
            for (int i = 0; i < icons.Length; i++) icons[i] = m.Companions[i].Icon;
            return icons;
        }
    }

    public string[] RadialLabels
    {
        get
        {
            CompanionManager m = Manager;
            if (m == null) return new string[0];
            int active = m.ActiveIndex;
            var labels = new string[m.Companions.Count];
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i] = m.DisplayName(i);
                if (i == active) labels[i] += Loc.T("companion.active_suffix");
            }
            return labels;
        }
    }

    public string[] RadialDescriptions
    {
        get
        {
            CompanionManager m = Manager;
            if (m == null) return new string[0];
            var lines = new string[m.Companions.Count];
            for (int i = 0; i < lines.Length; i++)
            {
                CompanionManager.CompanionOption o = m.Companions[i];
                float wait = m.RespawnRemaining(i);
                lines[i] = wait > 0f
                    ? Loc.T("companion.respawning", ("seconds", Mathf.CeilToInt(wait)))
                    : Loc.Pick(o.Description, o.DescriptionEs);
            }
            return lines;
        }
    }

    public bool IsRadialOptionAvailable(int index)
    {
        CompanionManager m = Manager;
        return m != null && !m.IsOnCooldown(index);
    }

    // El botón muestra al compañero activo.
    public override Sprite CurrentIcon
    {
        get
        {
            CompanionManager m = Manager;
            int active = m != null ? m.ActiveIndex : -1;
            return active >= 0 && m.Companions[active].Icon != null ? m.Companions[active].Icon : AbilityIcon;
        }
    }

    public override bool UsesTargetLayer => false;
    public override bool UsesHitEffects  => false;

    // Se activa por la rueda (ActivateWithSelection), no con un click.
    public override void Activate() { }

    public void ActivateWithSelection(int index, Vector3 targetPosition)
    {
        if (!IsServer) return;
        if (index < 0 || !CanActivate()) { EndAbility(); return; }

        CompanionManager m = Manager;
        if (m != null && m.ServerSwitch(index))
        {
            CommitAbility();
            PlayerController pc = OwnerASC.GetComponent<PlayerController>();
            if (pc != null) pc.PlayAnimation(this);
        }
        EndAbility();
    }
}
