using UnityEngine;

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
//
// LAS BANSHEES (12 de noviembre de 2026, pedido de Gustavo): los "jefes" de los campamentos pasaron a
// llamarse Banshees, para no confundirlos con el jefe del evento de mitad de partida. "Boss" es la
// Banshee de curación (el jefe de siempre; se dejó el nombre en el código porque lo usan el evento y los
// campamentos). El jefe de hielo de la jam no existe: su lugar (5) es ahora la Banshee del silencio.
// ============================================================
public enum EMercEnemyType
{
    [InspectorName("Fantasma")]            Ghost,           // el melee básico
    [InspectorName("Mago")]                Mage,            // a distancia, bola de fuego (quema)
    [InspectorName("Mago de hielo")]       IceMage,         // a distancia, bola de hielo (ralentiza)
    [InspectorName("Mago de roca")]        RockMage,        // a distancia, roca lenta (aturde)
    [InspectorName("Banshee de curación")] Boss,            // cura a los NPCs y daña alrededor
    [InspectorName("Banshee del silencio")] SilenceBanshee, // silencia y daña alrededor (era IceBoss, sin usar)
    [InspectorName("Banshee de daño")]     DamageBanshee,   // daña alrededor y potencia a los NPCs (era DamageBoss)
    [InspectorName("Zombi hinchado")]      BloatedZombie,   // revienta al morir y deja veneno (12 de noviembre de 2026)
}
