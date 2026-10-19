using UnityEngine;

// ============================================================
// Entity_Totem  (los tótems del Chamán)
//
// Desde el 19 de octubre de 2026 es una invocación (Entity_Summon) con una zona (Summon_Zone)
// para el aura, y no le queda nada propio: el equipo, el límite de tótems por Chamán (la base
// saca el más viejo), irse si el Chamán muere (pedido de Gustavo) y romperse con su vida a 0 los
// resuelve la base; el aura a los aliados del radio, potenciada si el Chamán está enfurecido,
// la zona.
//
// La clase sigue existiendo para que los prefabs y las habilidades (GA_SpawnTotem,
// GA_ElementalFury) la sigan reconociendo como tótem.
//
// PREFAB: NetworkObject + AbilitySystemComponent (con su vida) + NetworkAbilitySystemComponent
// + un collider en la capa de personajes + este script + una Summon_Zone.
// ============================================================
[RequireComponent(typeof(AbilitySystemComponent))]
public class Entity_Totem : Entity_Summon
{
    // =========================================================
    // DATOS VIEJOS: el aura vivía acá. "Mercenarios ▸ Una vez ▸ Pasar los tótems a la base de
    // invocaciones" los copia a la Summon_Zone de cada prefab. Después de correrla no se usan.
    // =========================================================

#pragma warning disable CS0414   // solo los lee la herramienta, por SerializedObject
    [SerializeField, HideInInspector] private GameplayEffect AuraEffect;
    [SerializeField, HideInInspector] private GameplayEffect EmpoweredAuraEffect;
    [SerializeField, HideInInspector] private EGameplayTag   RageTag;
    [SerializeField, HideInInspector] private float          AuraRadius = 7f;
    [SerializeField, HideInInspector] private float          TickRate   = 0.5f;
    [SerializeField, HideInInspector] private LayerMask      CharacterLayer;
    [SerializeField, HideInInspector] private GameObject     AuraVFXPrefab;
    [SerializeField, HideInInspector] private float          VfxScaleMultiplier = 2f;
#pragma warning restore CS0414
}
