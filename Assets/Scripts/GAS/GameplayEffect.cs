// GameplayEffect.cs (Scriptable Object)
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "GE_Base", menuName = "GAS/Gameplay Effect")]
public class GameplayEffect : ScriptableObject
{
    public enum EStackingType
    {
        Refresh,  // Se reinicia el tiempo (Veneno + Veneno = 1 Veneno con duración reiniciada)
        Stack, //Se acumula (Veneno + Veneno = 2 Venenos haciendo daño)
        Override // El nuevo Reemplaza al viejo (Buffs que cambian de nivel, por ejemplo)
    }
    [Header("Tipo de Efecto")]
    [Tooltip("0 = Instantáneo, > 0 = Duración")]
    public float Duration = 0f; 
    [Tooltip("0 = No periódico, > 0 = Intervalo entre ticks")]
    public float Period = 0f;
    [Header("Visuales UI")]

    [Header("Reglas de Acumulación")]
    [Tooltip("Refresh: Si ya tienes este efecto, solo reinicia su duración.")]
    public EStackingType StackingPolicy = EStackingType.Stack; // Por defecto se acumulan
    public Sprite Icon; // El dibujito (ej: escudo, calavera)
    public EEffectType EffectType; // ¿Es bueno o malo?

    public enum EEffectType
    {
        Buff,   // Verde (Beneficioso)
        Debuff, // Rojo (Dañino)
        Hidden  // No se muestra en UI (ej: mecánicas internas)
    }

    [Header("Modificadores")]
    public List<Modifier> Modifiers = new List<Modifier>();
    
    [Header("Tags")]
    // Tags a aplicar mientras el efecto está activo (ej: Stunned)

    public List<EGameplayTag> GrantedTags = new List<EGameplayTag>();
}