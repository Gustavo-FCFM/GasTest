using UnityEngine;

// ============================================================
// ILeapAbility
//
// La implementan las habilidades que MANDAN AL PERSONAJE POR EL AIRE y quieren que la
// animación se sostenga mientras dure el vuelo, en vez de reproducirse una vez y
// terminar aunque el personaje siga arriba (que es lo que hace la ranura de acción
// normal, pensada para golpes en el piso).
//
// Son TRES clips, con la misma forma que el sistema de mantener (IHoldAbility):
//   · AirStartClip — el despegue. Una sola vez, y encadena al bucle.
//   · AirLoopClip  — la pose sostenida en el aire (el hacha arriba). EN BUCLE: dura lo
//     que dure el vuelo, sea corto o largo.
//   · AirLandClip  — el remate al tocar el suelo. Una sola vez.
//
// POR QUÉ TRES Y NO DOS: si el despegue vive dentro del clip del bucle, el salto
// entero se repite una y otra vez mientras el personaje está en el aire. El bucle
// tiene que ser SOLO la pose sostenida.
//
// QUIÉN CORTA EL BUCLE: nadie desde código. El Animator sale del bucle con el
// parámetro "IsJumping", que PlayerController.UpdateAnimations ya alimenta cada frame
// desde !characterController.isGrounded. Así el aterrizaje se detecta solo, y da igual
// si el vuelo vino de un salto normal o de una habilidad.
//
// LO QUE HAY QUE ARMAR EN EL ANIMATOR (ver PlayerController.AirLoopSlotName):
//   · 1 estado con el clip placeholder AirStartSlotName.
//   · 1 estado con el clip placeholder AirLoopSlotName, en LOOP.
//   · 1 estado con el clip placeholder AirLandSlotName.
//   · AnyState  → AirStart: ActionID == LeapStateID + trigger de acción.
//   · AirStart  → AirLoop:  por Exit Time, sin condiciones.
//   · AirLoop   → AirLand:  IsJumping == false.
//   · AirLand   → Locomoción: por Exit Time.
//
// Si no hay estado de despegue, la transición de AnyState va directo al bucle y el
// clip de despegue se ignora. Sin la ranura del BUCLE la habilidad funciona igual (el
// salto y el daño no dependen de la animación), solo que se anima con el esquema de
// siempre. Se avisa una vez por consola.
// ============================================================
public interface ILeapAbility
{
    // Despegue. Opcional: sin él se entra directo al bucle.
    AnimationClip AirStartClip { get; }

    // Bucle del vuelo. Es el único imprescindible.
    AnimationClip AirLoopClip { get; }

    // Remate del aterrizaje. Opcional: sin él se vuelve a la locomoción directo.
    AnimationClip AirLandClip { get; }
}
