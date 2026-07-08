using UnityEngine;
using System.Collections.Generic;

// ============================================================
// CharacterClassDefinition
//
// Asset que define una clase jugable completa: identidad visual,
// stats base, qué habilidad va en cada slot, cómo escala al subir
// de nivel, y a qué subclases puede evolucionar. PlayerController
// lo usa en EquipCharacterClass() para configurar todo el personaje
// de una sola vez.
// ============================================================
[CreateAssetMenu(fileName = "Class_New", menuName = "GAS/Character Class Definition")]
public class CharacterClassDefinition : ScriptableObject
{
    // Qué habilidad ocupa cada slot de input (Shift, Q, E, R, LMB, RMB).
    [System.Serializable]
    public struct AbilityAssignment
    {
        public EAbilityInput InputSlot;
        public GameplayAbility Ability;
    }

    [Header("Identidad")]
    public string ClassName = "Aldeano";
    public Sprite ClassIcon;
    [TextArea] public string Description;

    [Header("Stats Base (Nivel 1)")]
    // Vida/maná/etc. iniciales de esta clase.
    public AttributeSetDefinition BaseAttributes;

    [Header("Habilidades")]
    // Qué GameplayAbility se otorga en cada slot al equipar esta clase.
    public List<AbilityAssignment> Abilities;

    [Header("Progresión (Level Up)")]
    // Cuánto sube cada stat automáticamente por cada nivel ganado.
    public List<AttributeGrowth> StatGrowthPerLevel;

    [Header("Evolución")]
    // A qué subclases se puede evolucionar al llegar al nivel máximo
    // (ver UI_LevelUpSelectionSystem).
    public List<CharacterClassDefinition> AvailableSubclasses;

    [Header("Visuales y Animación")]
    [Tooltip("Configura aquí los gráficos y animaciones de la clase")]
    // Override de animaciones específico de esta clase (idle agresivo,
    // ataque pesado vs rápido, etc.).
    public AnimatorOverrideController ClassAnimatorOverride;

    [Header("Armamento")]
    [Tooltip("Configura aquí las armas que usa esta clase")]
    public GameObject MainHandWeaponPrefab; // Arma principal (mano derecha)
    public GameObject OffHandWeaponPrefab;  // Arma secundaria, opcional
}

// Cuánto sube UN atributo por cada nivel que gana el personaje. La lista
// StatGrowthPerLevel de arriba tiene una de estas por stat que quiera
// crecer con el nivel.
[System.Serializable]
public class AttributeGrowth
{
    public EAttributeType Attribute;
    public float AmountPerLevel; // Ej: +5 de Vida por nivel
}
