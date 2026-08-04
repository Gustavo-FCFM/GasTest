using UnityEngine;

// ============================================================
// GA_CannonBarrage  (Cañones — ultimate del Pirata)
//
// Zona de daño continuo que el Pirata despliega donde apunta, y que además le APUESTA
// a cada enemigo que alcanza (el mismo sistema de la pasiva Apostar): los marcados
// reciben más daño del Pirata y, si los remata antes de que venza la marca, cobra los
// WinEffects de cada apuesta.
//
// Toda la parte de "zona que dura y aplica efectos cada tanto, apuntada con la
// retícula" ya la hace GA_ContinuousAoE — acá solo se engancha el gancho OnTargetHit
// para abrir la apuesta. Configurá el asset con DeployMode = AtReticle.
//
// Las apuestas del ult NO pisan la apuesta automática de la pasiva ni entre sí:
// GamblePassive maneja varias apuestas simultáneas, cada una con su vencimiento
// (ver ServerMarkTarget). Volver a golpear a alguien ya marcado no le refresca la
// marca, así que los ticks repetidos de la zona no la vuelven eterna.
//
// SETUP: el asset necesita el mismo GE de daño que cualquier AoE (EffectsToApply), y
// el Pirata debe tener GamblePassive en su PassiveBehaviorsPrefab (de ahí sale el GE
// de la marca y los efectos de la apuesta — no se configuran de nuevo acá).
// ============================================================
[CreateAssetMenu(fileName = "GA_CannonBarrage", menuName = "GAS/Specific Abilities/Pirate/Cannon Barrage")]
public class GA_CannonBarrage : GA_ContinuousAoE
{
    // La pasiva del dueño, que lleva el registro de apuestas. Se resuelve la primera
    // vez que hace falta (la instancia otorgada vive con el jugador).
    [System.NonSerialized] private GamblePassive _gamble;
    // El aviso de "falta GamblePassive" se da UNA vez: OnTargetHit corre por cada
    // objetivo en cada tick de la zona, así que si no, inundaría la consola.
    [System.NonSerialized] private bool _warnedNoGamble;

    // Cada enemigo alcanzado por la zona entra en la apuesta.
    protected override void OnTargetHit(AbilitySystemComponent target)
    {
        if (OwnerASC == null || target == null) return;

        if (_gamble == null) _gamble = OwnerASC.GetComponentInChildren<GamblePassive>();
        if (_gamble == null)
        {
            if (!_warnedNoGamble)
            {
                _warnedNoGamble = true;
                Debug.LogWarning("[GA_CannonBarrage] El dueño no tiene GamblePassive — la zona hace daño " +
                                 "pero no apuesta. ¿Falta el componente en el PassiveBehaviorsPrefab del Pirata?");
            }
            return;
        }

        _gamble.ServerMarkTarget(target);
    }
}
