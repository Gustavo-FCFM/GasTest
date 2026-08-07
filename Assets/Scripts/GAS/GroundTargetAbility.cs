// ============================================================
// IGroundTargetAbility
//
// La implementa cualquier GameplayAbility que se apunte marcando una ZONA en el
// suelo antes de lanzarse (ej: "Marcado para morir" del Asesino, y más adelante
// el Muro de fuego del Mago, los Cañones del Pirata, el Salto heroico del
// Comandante, etc.).
//
// Flujo (mismo estilo que IRadialMenuAbility): PlayerController detecta el
// MANTENER del botón, muestra el marcador en el suelo siguiendo la mira, y al
// SOLTAR activa la habilidad por el camino normal — que ya le manda el punto de
// mira al servidor (ver ServerRequestActivateAbility / NetworkAimPoint). Por eso
// la habilidad no necesita nada extra: lee el punto con GetAimPoint() como
// siempre.
// ============================================================
public interface IGroundTargetAbility
{
    // Alcance máximo al que se puede marcar la zona. El marcador se recorta a
    // esta distancia para que la vista previa coincida con lo que hará el servidor.
    float MaxTargetRange { get; }

    // Radio de la zona objetivo: define el tamaño del marcador.
    float TargetRadius { get; }

    // Si ESTA configuración se apunta con el marcador. Existe porque hay habilidades
    // genéricas que pueden desplegarse de dos formas según su asset (ver el DeployMode
    // de GA_ContinuousAoE / GA_InstantAoE): implementan la interfaz siempre, pero solo
    // entran en modo apuntado cuando esto da true. Las que SIEMPRE se apuntan
    // (ej. Marcado para morir) devuelven true fijo.
    bool UsesGroundTarget { get; }
}
