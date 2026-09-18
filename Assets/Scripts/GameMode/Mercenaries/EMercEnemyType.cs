// ============================================================
// EMercEnemyType
//
// Los tipos de enemigo que pueden aparecer en el modo Mercenarios. El enum es lo que
// se elige en cada campamento; qué PREFAB le corresponde a cada tipo lo resuelve
// MercEnemyCatalog, en un solo lugar para todo el juego.
//
// Por qué un enum y no arrastrar el prefab en cada campamento: hay nueve campamentos y
// cada uno puede tener varios tipos. Con prefabs sueltos, cambiar el fantasma por otro
// modelo obligaría a tocar veintipico de casillas y con que se te escape una, ese
// campamento sigue sacando el viejo. Con el enum, se cambia el catálogo y listo.
//
// IMPORTANTE: agregar tipos nuevos SIEMPRE al final. Estos valores se serializan por
// NÚMERO en los campamentos de la escena; meter uno en el medio corre los índices y un
// campamento que sacaba fantasmas pasaría a sacar jefes sin que nadie lo toque.
// ============================================================
public enum EMercEnemyType
{
    Ghost,        // el melee básico
    Mage,         // a distancia, bola de fuego
    IceMage,      // a distancia, hielo
    RockMage,     // a distancia, roca
    Boss,         // jefe
    IceBoss,      // jefe de hielo
    DamageBoss,   // jefe de daño
}
