using UnityEngine;
using System.Collections.Generic;

// ============================================================
// GameplayEffect
//
// Asset que describe UN efecto que se le puede aplicar a un
// personaje: daño/curación instantánea, un buff temporal, un
// debuff periódico, un cooldown, etc. Es la "receta" — el estado
// runtime de una aplicación concreta vive en ActiveGameplayEffect.
// AbilitySystemComponent.ApplyGameplayEffect() es quien lo procesa.
// ============================================================
[CreateAssetMenu(fileName = "GE_Base", menuName = "GAS/Gameplay Effect")]
public class GameplayEffect : ScriptableObject
{
    // Cómo se comporta este efecto si se vuelve a aplicar mientras ya
    // está activo.
    public enum EStackingType
    {
        Refresh,  // Reinicia la duración (Veneno + Veneno = 1 Veneno con el tiempo reiniciado)
        Stack,    // Se acumula (Veneno + Veneno = 2 Venenos haciendo daño)
        Override  // El nuevo reemplaza al viejo (ej: un buff que cambia de nivel)
    }

    // Si es Buff/Debuff/Hidden — controla si aparece en la barra de
    // efectos activos y de qué color (ver UI_EffectSlot).
    public enum EEffectType
    {
        Buff,   // Verde, beneficioso
        Debuff, // Rojo, dañino
        Hidden  // No se muestra en UI (ej: el propio cooldown de una habilidad)
    }

    [Header("Tipo de Efecto")]
    [Tooltip("0 = Instantáneo, > 0 = Duración")]
    // Si es 0, el efecto se aplica una sola vez y desaparece (daño,
    // curación). Si es mayor a 0, queda activo ese tiempo (buffs,
    // debuffs, cooldowns).
    public float Duration = 0f;

    [Tooltip("0 = No periódico, > 0 = Intervalo entre ticks")]
    // Si es mayor a 0, mientras el efecto está activo se re-ejecuta cada
    // tantos segundos (ej: veneno que daña cada 2s).
    public float Period = 0f;

    [Header("Reglas de Acumulación")]
    [Tooltip("Refresh: Si ya tienes este efecto, solo reinicia su duración.")]
    public EStackingType StackingPolicy = EStackingType.Stack;

    [Header("Exclusión Mutua (Jerarquía)")]
    [Tooltip("Efectos con el MISMO grupo (≠ None) se excluyen: solo vive el de mayor Priority a la vez. Ej: el buff normal del tótem y su versión potenciada comparten grupo, así no se acumulan.")]
    public EGameplayTag EffectGroup = EGameplayTag.None;

    [Tooltip("Dentro de un EffectGroup, mayor Priority gana. Aplicar uno de Priority MENOR a uno ya activo del grupo no hace nada; uno de Priority MAYOR reemplaza a los inferiores.")]
    public int Priority = 0;

    // Icono que se muestra en la barra de buffs/debuffs.
    public Sprite Icon;

    // Si es Buff, Debuff o Hidden para la UI.
    public EEffectType EffectType;

    [Header("Modificadores")]
    // Qué atributos cambia este efecto y cuánto (ver Modifier.cs).
    public List<Modifier> Modifiers = new List<Modifier>();

    [Header("Tags")]
    // Tags que se le agregan al objetivo mientras el efecto está activo
    // (ej: Stunned) y se le quitan al terminar. El primer tag de esta
    // lista también sirve como "identidad" del efecto para cooldowns y
    // sincronización en red — ver GameplayAbility.CanActivate() y
    // NetworkAbilitySystemComponent.
    public List<EGameplayTag> GrantedTags = new List<EGameplayTag>();
}
