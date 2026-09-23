// ============================================================
// EClassRole
//
// El ROL de una clase en el equipo. Además de lo que ya carga cada golpe, decide
// qué le carga la definitiva MÁS RÁPIDO — el incentivo para jugar el rol:
//
//   · Tank    → AGUANTAR daño de enemigos
//   · Damage  → MATAR a un personaje enemigo (jugador o bot, no monstruos)
//   · Support → CURAR a un aliado
//
// None es para las clases BASE: todavía no tienen definitiva, así que no hay nada
// que cargar. Las subclases llevan el rol de su rama.
//
// Cuánto carga cada cosa se ajusta en el PlayerController del prefab del jugador
// (sección "Carga de la definitiva").
//
// Se guarda por NÚMERO en los Class_*.asset: agregar valores SOLO AL FINAL.
// ============================================================
public enum EClassRole
{
    None    = 0,
    Tank    = 1,
    Damage  = 2,
    Support = 3
}
