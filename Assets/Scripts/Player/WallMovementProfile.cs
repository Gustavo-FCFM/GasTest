using System.Collections.Generic;
using UnityEngine;

// ============================================================
// WallMovementProfile
//
// Cómo se mueve una clase en las paredes. Lo lee PlayerController (en el DUEÑO, como el
// resto del movimiento: el transform es client-authoritative) desde
// CharacterClassDefinition.WallMovement. Las subclases apuntan al mismo perfil que su
// clase base, así se ajusta en un solo lugar.
//
//  · Pegarse (el Pícaro): en el aire, yendo contra una pared, se frena y queda pegado
//    ClingTime; después resbala SlideTime y se suelta solo.
//  · Correr (el Monje): en el aire, yendo contra una pared, queda en ella y WASD lo lleva
//    a lo largo (solo de costado, nunca hacia arriba), bajando poco a poco hasta tocar
//    el piso. Si la pared se acaba, cae con el impulso que traía.
//
// En los dos: Espacio salta en la dirección OPUESTA a la pared, más un impulso hacia
// arriba. Los saltos se encadenan sin límite, pero no se vuelve a pegar a la MISMA pared
// de la que se acaba de soltar hasta tocar el piso. Pegado se pueden usar las
// habilidades; las de movimiento (dash, Blink, la patada) lo despegan. Con la bolsa del
// objetivo también se puede (decisiones de Gustavo, 8 de octubre de 2026).
//
// No cuentan como pared: las paredes invisibles (Ignore Raycast: los límites de la arena
// y de las salas, la misma regla del Destello), los personajes, tótems y NPCs (todo lo
// que tiene AbilitySystemComponent), lo que tiene Rigidbody y los triggers.
// ============================================================
[CreateAssetMenu(fileName = "WallMove_New", menuName = "GAS/Wall Movement Profile")]
public class WallMovementProfile : ScriptableObject
{
    public enum EWallMode
    {
        [InspectorName("Pegarse (Pícaro)")] Cling = 0,
        [InspectorName("Correr (Monje)")]   Run   = 1,
    }

    [Section("General")]
    [Tooltip("Pegarse: queda quieto un rato y después resbala y se suelta. Correr: se mueve a lo " +
             "largo de la pared mientras baja de a poco.")]
    public EWallMode Mode = EWallMode.Cling;

    [Tooltip("Capas que cuentan como pared. Las paredes invisibles (Ignore Raycast) no cuentan nunca, " +
             "aunque estén marcadas acá.")]
    public LayerMask WallLayers = 1;   // Default

    [Tooltip("Con cualquiera de estos tags no se engancha (y si ya estaba, se suelta). Aturdido, " +
             "enraizado, repelido y volando ya lo sueltan siempre, no hace falta ponerlos.")]
    public List<EGameplayTag> BlockedByTags = new List<EGameplayTag>();

    [Section("Engancharse")]
    [Tooltip("Qué tan vertical tiene que ser la superficie. 0.3 acepta hasta unos 17° de inclinación; " +
             "más alto acepta paredes más inclinadas.")]
    [Range(0f, 0.7f)] public float MaxNormalY = 0.3f;

    [Tooltip("Cuánto tiene que empujar el WASD contra la pared para engancharse. 0 = con cualquier " +
             "roce; 1 = solo yendo derecho contra ella.")]
    [Range(0f, 1f)] public float MinInputIntoWall = 0.3f;

    [Tooltip("No se engancha si el piso está más cerca que esto (metros). Así saltar al lado de una " +
             "pared no lo pega por accidente.")]
    public float MinHeightAboveGround = 0.8f;

    [Tooltip("Hasta qué distancia de la cápsula busca la pared mientras está enganchado (metros). " +
             "Si deja de encontrarla —se acabó la pared—, se suelta.")]
    public float StickDistance = 0.3f;

    [Section("Pegarse")]
    [ShowIf(nameof(Mode), EWallMode.Cling)]
    [Tooltip("Segundos que queda quieto, pegado, antes de empezar a resbalar.")]
    public float ClingTime = 1f;

    [ShowIf(nameof(Mode), EWallMode.Cling)]
    [Tooltip("Segundos que resbala antes de soltarse. Mientras resbala todavía puede saltar.")]
    public float SlideTime = 0.5f;

    [ShowIf(nameof(Mode), EWallMode.Cling)]
    [Tooltip("Velocidad a la que resbala hacia abajo (m/s).")]
    public float SlideSpeed = 1.5f;

    [Section("Correr")]
    [ShowIf(nameof(Mode), EWallMode.Run)]
    [Tooltip("Velocidad a lo largo de la pared, multiplicando su velocidad de movimiento.")]
    public float RunSpeedMultiplier = 1f;

    [ShowIf(nameof(Mode), EWallMode.Run)]
    [Tooltip("Qué tan rápido va bajando (m/s por segundo). Bajo = baja de a poco.")]
    public float SlideGravity = 2f;

    [ShowIf(nameof(Mode), EWallMode.Run)]
    [Tooltip("Tope de la velocidad de bajada (m/s).")]
    public float MaxSlideSpeed = 3f;

    [ShowIf(nameof(Mode), EWallMode.Run)]
    [Tooltip("Qué parte de la velocidad de SUBIDA conserva al engancharse: si llega saltando, sigue " +
             "subiendo un poco antes de empezar a bajar. 0 = se frena en seco.")]
    [Range(0f, 1f)] public float KeepUpwardSpeed = 0.5f;

    [Section("Saltar de la pared")]
    [Tooltip("Velocidad hacia AFUERA de la pared al saltar (m/s).")]
    public float JumpOffSpeed = 7f;

    [Tooltip("Velocidad hacia ARRIBA al saltar (m/s). El salto normal del piso es 8.")]
    public float JumpOffUp = 7f;

    [Tooltip("Cuánto se puede desviar el salto hacia donde mira la cámara, en grados a cada lado " +
             "de la dirección opuesta a la pared. 30 = sale hasta 30° hacia el lado que mira, nunca " +
             "derecho a donde mira. 0 = siempre perpendicular a la pared.")]
    [Range(0f, 90f)] public float JumpAimMaxAngle = 30f;

    [Tooltip("Segundos después del salto en los que el WASD no puede empujar de vuelta contra la " +
             "pared (de costado sí). Sin esto, apretando W hacia la pared el salto se anularía.")]
    public float JumpOffLockTime = 0.25f;

    [Tooltip("Qué tan rápido se disipa el impulso del salto. Más alto = vuela menos lejos.")]
    [Range(0.5f, 10f)] public float JumpOffDamping = 1.5f;

    [Section("Cómo se ve")]
    [Tooltip("Cuánto se inclina el modelo hacia afuera de la pared mientras está enganchado (grados). " +
             "Se ve en todas las pantallas.")]
    [Range(0f, 45f)] public float LeanAngle = 20f;
}
