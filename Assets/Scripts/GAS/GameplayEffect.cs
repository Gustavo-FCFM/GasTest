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

    [Tooltip("Máximo de acumulaciones (solo con StackingPolicy = Stack). 0 = sin límite. " +
             "Al llegar al tope, aplicarlo de nuevo refresca la acumulación que esté por " +
             "expirar en vez de agregar otra (salvo que uses OnMaxStacksEffect).")]
    public int MaxStacks = 0;

    [Tooltip("Solo con Stack + MaxStacks > 0. Al llegar al tope de acumulaciones, en vez de " +
             "refrescarlas se CONSUMEN todas y se aplica este efecto al objetivo (la 'explosión' " +
             "de las Heridas del Ilusionista). Dejar en None para el comportamiento normal de tope.")]
    public GameplayEffect OnMaxStacksEffect;

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

    [Header("Control de Masas (CC)")]
    [Tooltip("Marca este efecto como CONTROL: su duración se acorta (o se alarga) con la " +
             "Resistencia al control del objetivo — ver EAttributeType.CCResistance.\n\n" +
             "No hace falta marcarlo si el efecto otorga State_Stunned, State_Rooted o " +
             "State_Silenced: esos ya cuentan como control solos. Es para el CC que no pasa por " +
             "esos tags (cegueras, miedos, ralentizaciones fuertes) y que igual querés que la " +
             "resistencia recorte.")]
    public bool IsCrowdControl = false;

    // Tags de control "duros" del core: un efecto que otorgue cualquiera de estos cuenta
    // como CC aunque no tenga la casilla marcada. Existe para que los GEs que YA estaban
    // (GE_Stun y compañía) obedezcan la resistencia sin tener que editarlos uno por uno.
    private static readonly EGameplayTag[] CoreControlTags =
    {
        EGameplayTag.State_Stunned,
        EGameplayTag.State_Rooted,
        EGameplayTag.State_Silenced,
    };

    // True si a este efecto le corresponde que la Resistencia al control le toque la
    // duración. Lo consulta AbilitySystemComponent.ApplyGameplayEffect.
    public bool CountsAsCrowdControl
    {
        get
        {
            if (IsCrowdControl) return true;
            if (GrantedTags == null) return false;

            foreach (EGameplayTag granted in GrantedTags)
                foreach (EGameplayTag control in CoreControlTags)
                    if (granted == control) return true;

            return false;
        }
    }

    [Header("VFX en el Objetivo")]
    [Tooltip("VFX que aparece sobre QUIEN RECIBE este efecto y vive lo que viva el efecto. " +
             "Pensado para que un debuff importante se vea encima del jugador afectado (las " +
             "flechas cayendo, un aura, unas cadenas), sin tener que instanciarlo a mano desde " +
             "la habilidad. Siempre queda ENGANCHADO al objetivo: lo sigue si se mueve, y se " +
             "destruye solo cuando el efecto expira o se lo quitan. Solo tiene sentido en " +
             "efectos CON duración: uno instantáneo se aplica y se va en el mismo frame. " +
             "Dejalo en None si el efecto no necesita nada visible.")]
    public GameObject TargetVFX;

    [Tooltip("Desplazamiento del VFX respecto al pivote del objetivo, en su espacio local. " +
             "Los pivotes están a los pies, así que casi siempre vas a querer subirlo en Y " +
             "(2-3 para algo que flote sobre la cabeza).")]
    public Vector3 TargetVFXOffset;

    [Tooltip("Rotación extra del VFX respecto al objetivo, en grados.")]
    public Vector3 TargetVFXRotation;

    [Tooltip("Escala del VFX. En CERO usa la escala del prefab tal cual.")]
    public Vector3 TargetVFXScale;

    [Header("Tags")]
    // Tags que se le agregan al objetivo mientras el efecto está activo
    // (ej: Stunned) y se le quitan al terminar. El primer tag de esta
    // lista también sirve como "identidad" del efecto para cooldowns y
    // sincronización en red — ver GameplayAbility.CanActivate() y
    // NetworkAbilitySystemComponent.
    public List<EGameplayTag> GrantedTags = new List<EGameplayTag>();
}
