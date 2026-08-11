using UnityEngine;

// ============================================================
// IHoldAbility
//
// La implementa cualquier GameplayAbility que se MANTENGA apretada: se activa al
// presionar el botón y sigue viva hasta que el jugador lo suelta (o hasta que el
// servidor la corta, ej. por quedarse sin energía). El escudo del Paladín es el
// primer caso; el canalizado del Clérigo y la postura del Guerrero van a usar lo
// mismo.
//
// Se diferencia de IRadialMenuAbility / IGroundTargetAbility en QUÉ pasa mientras
// mantenés: esas dos son "apuntar y soltar" (el efecto ocurre AL SOLTAR y hasta
// entonces solo se muestra un marcador). Esta ocurre MIENTRAS mantenés, y soltar
// es lo que la termina.
//
// FLUJO (ver PlayerController.ProcessAbilityPress / ProcessHoldRelease):
//   presionar → activación normal por el camino de siempre (ServerRequestActivateAbility)
//               → Activate() server-side arranca el estado y avisa la animación de hold
//   soltar    → ServerRequestEndHoldAbility(slot) → EndHold() en el servidor
//   sin recurso → el servidor llama EndHold() por su cuenta y le avisa al dueño
//               por TargetRpc para que baje su animación (ver NetworkASC).
//
// ANIMACIÓN: los tres clips viven en el asset de la habilidad (igual que el
// AnimationClip suelto de GameplayAbility), así cada peer resuelve los suyos
// desde el registro sin sincronizar nada más.
// ============================================================
public interface IHoldAbility
{
    // Termina el mantenido. La llama el servidor: cuando el dueño suelta el botón,
    // cuando se agota el recurso, o al morir/cambiar de clase. TIENE que ser
    // idempotente — llamarla dos veces no debe romper nada, porque esas tres vías
    // pueden pisarse (soltás el botón el mismo frame que se te acaba la energía).
    void EndHold();

    // True si el mantenido está activo ahora mismo (server-side). Lo usa
    // PlayerController/NetworkASC para no mandar un "soltar" de algo que ya terminó.
    bool IsHolding { get; }

    // Clip de MANTENER (en bucle, mientras se sostiene). Es el único que hace falta:
    // los otros tres son opcionales y, si están vacíos (o si el Animator no tiene su
    // estado), simplemente no se reproducen.
    AnimationClip HoldLoopClip { get; }

    // OPCIONAL — clip de LEVANTAR (one-shot, al presionar).
    AnimationClip HoldStartClip { get; }
    // OPCIONAL — clip de BAJAR (one-shot, al soltar).
    AnimationClip HoldEndClip { get; }
    // OPCIONAL — reacción one-shot al recibir un golpe mientras se bloquea, sin salir
    // del mantenido (el escudo "acusa" el impacto).
    AnimationClip HoldImpactClip { get; }
}
