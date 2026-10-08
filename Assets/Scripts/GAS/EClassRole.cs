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

// Cómo se muestra cada rol en los menús: su color (Tanque azul, Daño rojo, Soporte verde)
// y su nombre para el jugador (en inglés, como todo lo que se ve en el juego). Lo usan la
// sala, el menú de clases y el inspector de las clases, para que sean los mismos colores
// en todos lados.
public static class ClassRoleStyle
{
    // El orden en que se listan los grupos en los menús.
    public static readonly EClassRole[] MenuOrder =
        { EClassRole.Tank, EClassRole.Damage, EClassRole.Support, EClassRole.None };

    public static UnityEngine.Color Color(EClassRole role)
    {
        switch (role)
        {
            case EClassRole.Tank:    return new UnityEngine.Color(0.35f, 0.6f, 1f);
            case EClassRole.Damage:  return new UnityEngine.Color(1f, 0.4f, 0.4f);
            case EClassRole.Support: return new UnityEngine.Color(0.4f, 0.9f, 0.45f);
            default:                 return new UnityEngine.Color(0.7f, 0.7f, 0.7f);
        }
    }

    // El nombre del GRUPO en los menús ("Tanks", "Damage", "Supports").
    public static string GroupName(EClassRole role)
    {
        switch (role)
        {
            case EClassRole.Tank:    return "Tanks";
            case EClassRole.Damage:  return "Damage";
            case EClassRole.Support: return "Supports";
            default:                 return "Other";
        }
    }
}
