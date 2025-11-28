using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Class_New", menuName = "GAS/Character Class Definition")]
public class CharacterClassDefinition : ScriptableObject
{
    [Header("Identidad")]
    public string ClassName = "Aldeano";
    public Sprite ClassIcon;
    [TextArea] public string Description;

    [Header("Stats Base (Nivel 1)")]
    // Reutilizamos tu estructura existente para definir la vida/mana inicial
    public AttributeSetDefinition BaseAttributes;
    [System.Serializable]
    public struct AbilityAssignment
    {
        public EAbilityInput InputSlot; // ¿En qué botón va?
        public GameplayAbility Ability; // ¿Qué habilidad es?
    }
    [Header("Habilidades")]
    public List<AbilityAssignment> Abilities;
    [Header("Progresión (Level Up)")]
    // Aquí definimos cuánto suben los stats AUTOMÁTICAMENTE al subir de nivel
    public List<AttributeGrowth> StatGrowthPerLevel;
    [Header("Evolución")]
    public List<CharacterClassDefinition> AvailableSubclasses;
    [Header("Visuales y Animación")][Tooltip("Configura aquí los gráficos y animaciones de la clase")]
    // El archivo que cambia las animaciones (Idle agresivo, Ataque pesado vs rápido)
    public AnimatorOverrideController ClassAnimatorOverride;

    [Header("Armamento")][Tooltip("Configura aquí las armas que usa esta clase")]
    public GameObject MainHandWeaponPrefab; // Prefab del Hacha Grande
    public GameObject OffHandWeaponPrefab;  // Prefab del Hacha Pequeña (opcional)

}

[System.Serializable]
public class AttributeGrowth
{
    public EAttributeType Attribute;
    public float AmountPerLevel; // Ej: +5 de Vida por nivel
}