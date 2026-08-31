using UnityEngine;

// ============================================================
// IChanneledAbility
//
// La implementan las habilidades que se SOSTIENEN un rato por tiempo (no por botón) y
// quieren una animación que dure lo que dure el canalizado: el molinete del bárbaro es
// el caso. Sin esto, la habilidad dispara un clip suelto que termina en un segundo y el
// personaje se queda quieto mientras el daño sigue ticando.
//
// TRES CLIPS, la misma forma que el mantenido:
//   · ChannelStartClip — el arranque. Opcional.
//   · ChannelLoopClip  — el bucle. Es el único imprescindible.
//   · ChannelEndClip   — el remate al terminar. Opcional.
//
// REUSA LAS RANURAS DEL MANTENIDO (PLACEHOLDER_HoldStart/HoldLoop/HoldEnd), así que NO
// hay que crear estados nuevos en el Animator: los que ya armaste para el escudo hacen
// exactamente esta forma. Lo que NO se reusa es la interfaz IHoldAbility, porque esa
// arrastra un IsHolding de runtime que haría que el resto del sistema tratara a la
// habilidad como un mantenido de botón.
//
// EL GIRO es aparte y puramente cosmético: rota el MODELO, no la raíz, así que no toca
// la mira ni el movimiento (ver PlayerController.SetModelSpin).
// ============================================================
public interface IChanneledAbility
{
    // Arranque. Opcional: sin él se entra directo al bucle.
    AnimationClip ChannelStartClip { get; }

    // Bucle sostenido. El único imprescindible.
    AnimationClip ChannelLoopClip { get; }

    // Remate al terminar. Opcional.
    AnimationClip ChannelEndClip { get; }

    // Grados por segundo que gira el modelo mientras dura. 0 = sin giro.
    float SpinSpeed { get; }
}
