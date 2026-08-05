using UnityEngine;
using System.Collections.Generic;

// ============================================================
// GA_SelfBuff
//
// Habilidad de buff: se aplica a sí mismo un GameplayEffect con duración
// (y opcionalmente varios más). Pensada para habilidades tipo "Grito de
// guerra", "Postura defensiva" o "Enfurecer".
//
// VISUALES: usá el VisualsSequence del GameplayAbility base (soporta varios
// VFX, delays, offsets y fin por tag — ideal para un aura que dure lo mismo
// que el buff, con EndWithTag = el tag que otorga el BuffEffect). Antes esta
// clase tenía además un ParticlePrefab propio que hacía casi lo mismo; se
// quitó por redundante (GA_Rage llegaba a instanciar el VFX dos veces, una
// por cada sistema).
// ============================================================
[CreateAssetMenu(fileName = "GA_SelfBuff", menuName = "GAS/Generics/Self Buff")]
public class GA_SelfBuff : GameplayAbility
{
    [Header("Buff Settings")]
    // Efecto que se aplica al propio dueño al activar.
    public GameplayEffect BuffEffect;
    // Efectos EXTRA que se aplican al propio dueño además del BuffEffect
    // (varios buffs a la vez, un escudo, un tag de estado, etc.). Opcional.
    public List<GameplayEffect> AdditionalEffects;

    // Valida, cobra costo/cooldown (y reproduce el VisualsSequence), y aplica
    // el buff al dueño.
    public override void Activate()
    {
        if (!IsServer) return;   // ← NUEVO
        if (!CanActivate()) return;

        CommitAbility();

        if (BuffEffect != null)
            OwnerASC.ApplyGameplayEffect(BuffEffect, OwnerASC);
        else
            Debug.LogWarning("GA_SelfBuff activado sin un BuffEffect asignado.");

        ApplyEffectsTo(AdditionalEffects, OwnerASC);

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        if (pc != null) pc.PlayAnimation(this);

        EndAbility();
    }
}
