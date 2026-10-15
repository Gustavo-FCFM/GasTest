using UnityEngine;

// ============================================================
// GA_Prey  (Presa — habilidad extra del Cazador, 15 de octubre de 2026)
//
// "Selecciona a un enemigo y obtiene un beneficio según su ROL. Tanque: +daño según la vida
// máxima del objetivo · Daño: +daño según su vida faltante · Soporte: le aplica un debuff que
// reduce su curación. Si lo abate, el beneficio pasa a aplicarse contra todos los enemigos por
// un corto periodo."
//
// La habilidad solo ELIGE y marca: apunta al enemigo más centrado en la mira (como el Enemigo
// jurado del Paladín) y le pide a HunterPreyPassive que lo marque PreySeconds. Lo que da cada
// rol, el daño extra y la cacería al matarlo los resuelve esa pasiva.
//
// SOLO JUGADORES (y bots), no NPCs (pedido de Gustavo, 15 de octubre): apuntando a un fantasma no
// se activa; si hay un jugador cerca de la mira, lo elige a él.
//
// Sin enemigo a la vista no se activa (el botón no responde), igual que las otras de objetivo.
// ============================================================
[CreateAssetMenu(fileName = "GA_Prey", menuName = "GAS/Specific Abilities/Ranger/Hunter/Prey")]
public class GA_Prey : TargetImpactAbility
{
    [Section(AbilitySection.Targeting)]
    [Tooltip("Alcance máximo para buscar al enemigo a marcar.")]
    [Label("Alcance máximo (m)")]
    public float MaxRange = 30f;

    [Tooltip("Ángulo máximo entre la mira y el enemigo para que cuente como objetivo.")]
    [Label("Ángulo de selección (°)")]
    public float SelectionAngle = 30f;

    [Section("La presa")]
    [Tooltip("Cuánto dura la marca de presa.")]
    [Label("Dura (s)")]
    public float PreySeconds = 8f;

    public override bool CanActivate()
    {
        if (!base.CanActivate()) return false;
        return FindBestTargetInAim(MaxRange, SelectionAngle, ETargetAffiliation.Enemies, filter: HunterPreyPassive.IsPlayer) != null;
    }

    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;

        AbilitySystemComponent target = FindBestTargetInAim(MaxRange, SelectionAngle, ETargetAffiliation.Enemies, filter: HunterPreyPassive.IsPlayer);
        if (target == null) { EndAbility(); return; }

        HunterPreyPassive passive = OwnerASC.GetComponentInChildren<HunterPreyPassive>();
        if (passive == null)
        {
            Debug.LogWarning($"[{AbilityName}] el dueño no tiene HunterPreyPassive (va en el prefab de pasivas del Cazador).");
            EndAbility();
            return;
        }

        CommitAbility();
        passive.MarkPrey(target, PreySeconds);
        ApplyHitEffects(target, firstHit: true);   // lo que la lista tenga para el marcado

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.RotateToAim();
            pc.PlayAnimation(this);
        }

        PlayImpactVFXOnTarget(target);
        EndAbility();
    }

    public override void DrawGizmos(Transform origin)
        => DrawSelectionGizmo(origin, MaxRange, SelectionAngle, new Color(0.95f, 0.45f, 0.2f, 0.9f));

#if UNITY_EDITOR
    public override void DrawSceneHandles(Transform origin)
        => AbilityHandles.Selection(this, origin, ref MaxRange, ref SelectionAngle);
#endif
}
