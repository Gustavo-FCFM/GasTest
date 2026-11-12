using System;
using UnityEngine;

// ============================================================
// StrikeTarget
//
// Algo del mapa que se GOLPEA pero no es un personaje (11 de noviembre de 2026; lo primero que lo usa
// es la campana de las torres, MatchBell). Va junto a un AbilitySystemComponent: así lo encuentran todas
// las habilidades que buscan a quién pegarle (conos, líneas, zonas, rayos, proyectiles), sin tocar cada
// una. Su collider va en la capa Character (la 7), que es la que miran los ataques.
//
// Con este componente el ASC NO es enemigo ni aliado de nadie (ver ASC.IsEnemyOf / IsAllyOf): los bots,
// la mira automática, los NPCs y las curaciones lo ignoran. Un golpe que lo alcanza no le aplica nada:
// GameplayAbility.ApplyHitEffects (y el proyectil) avisan acá, con quién pegó, y siguen de largo. No
// carga la definitiva ni saca números ni VFX de golpe; un proyectil se deshace contra él como contra una
// pared.
//
// Solo en el SERVIDOR: los golpes se resuelven ahí. Quien lo usa escucha OnStruck.
// ============================================================
[RequireComponent(typeof(AbilitySystemComponent))]
public class StrikeTarget : MonoBehaviour
{
    // Lo golpearon: quién (el ASC del autor del golpe) y con qué habilidad (puede venir null).
    public event Action<AbilitySystemComponent, GameplayAbility> OnStruck;

    public void Strike(AbilitySystemComponent source, GameplayAbility ability)
    {
        if (source == null) return;
        OnStruck?.Invoke(source, ability);
    }
}
