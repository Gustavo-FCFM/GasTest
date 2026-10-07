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

    // Hacia dónde mueve el desplazamiento de este efecto (ver KnockbackDistance).
    public enum EKnockbackDirection
    {
        AwayFromSource,  // Repeler: lejos de quien lo aplicó (el dash del Monje)
        TowardSource,    // Atraer: hacia quien lo aplicó (un gancho); frena a PullStopDistance
        SourceForward    // Hacia donde mira quien lo aplicó (una patada en línea)
    }

    // Si es Buff/Debuff/Hidden — controla si aparece en la barra de
    // efectos activos y de qué color (ver UI_EffectSlot).
    public enum EEffectType
    {
        Buff,   // Verde, beneficioso
        Debuff, // Rojo, dañino
        Hidden  // No se muestra en UI (ej: el propio cooldown de una habilidad)
    }

    // Si es 0, el efecto se aplica una sola vez y desaparece (daño,
    // curación). Si es mayor a 0, queda activo ese tiempo (buffs,
    // debuffs, cooldowns).
    [Section(EffectSection.Duration)]
    [Tooltip("0 = instantáneo (daño, curación: se aplica y listo). > 0 = dura esos segundos " +
             "(buffs, debuffs, cooldowns).")]
    public float Duration = 0f;

    // Si es mayor a 0, mientras el efecto está activo se re-ejecuta cada
    // tantos segundos (ej: veneno que daña cada 2s).
    [ShowIf(nameof(Duration), ShowIfAttribute.Positive)]
    [Tooltip("Cada cuántos segundos se vuelve a aplicar mientras dura (un veneno que daña cada " +
             "2 s). 0 = no es periódico.")]
    public float Period = 0f;

    [Tooltip("Qué pasa si se vuelve a aplicar mientras ya está activo.\n\n" +
             "· Refresh: reinicia su duración (uno solo).\n" +
             "· Stack: se acumula (dos venenos = el doble).\n" +
             "· Override: el nuevo reemplaza al viejo.")]
    public EStackingType StackingPolicy = EStackingType.Stack;

    [ShowIf(nameof(StackingPolicy), EStackingType.Stack)]
    [Tooltip("Máximo de acumulaciones. 0 = sin límite. Al llegar al tope, aplicarlo de nuevo " +
             "refresca la acumulación que esté por expirar en vez de agregar otra (salvo que uses " +
             "OnMaxStacksEffect).")]
    public int MaxStacks = 0;

    [ShowIf(nameof(StackingPolicy), EStackingType.Stack)]
    [ShowIf(nameof(MaxStacks), ShowIfAttribute.Positive)]
    [Tooltip("Al llegar al tope de acumulaciones, en vez de refrescarlas se CONSUMEN todas y se " +
             "aplica este efecto al objetivo (la 'explosión' de las Heridas del Ilusionista). " +
             "None = el comportamiento normal de tope.")]
    public GameplayEffect OnMaxStacksEffect;

    [Section(EffectSection.Group, startCollapsed: true)]
    [Tooltip("Efectos con el MISMO grupo (≠ None) se excluyen: solo vive el de mayor Priority a la vez. Ej: el buff normal del tótem y su versión potenciada comparten grupo, así no se acumulan.")]
    public EGameplayTag EffectGroup = EGameplayTag.None;

    [ShowIf(nameof(EffectGroup), EGameplayTag.None, true)]
    [Tooltip("Dentro de un EffectGroup, mayor Priority gana. Aplicar uno de Priority MENOR a uno ya activo del grupo no hace nada; uno de Priority MAYOR reemplaza a los inferiores.")]
    public int Priority = 0;

    [ShowIf(nameof(EffectGroup), EGameplayTag.None, true)]
    [Tooltip("Dentro de su EffectGroup, el ÚLTIMO en aplicarse reemplaza a los demás, sin " +
             "importar Priority. Es para estados excluyentes que se alternan, como las posturas " +
             "del Guerrero: entrar a la ofensiva saca la defensiva en el mismo instante, así " +
             "nunca se tienen las dos ventajas a la vez.")]
    public bool ReplacesGroup = false;

    [Section(EffectSection.General)]
    [Tooltip("Ícono en la barra de buffs/debuffs (no hace falta si es Hidden).")]
    public Sprite Icon;

    [Tooltip("Buff (verde) o Debuff (rojo) en la barra de efectos; Hidden no se muestra (un " +
             "cooldown, un daño instantáneo).")]
    public EEffectType EffectType;

    // Qué atributos cambia este efecto y cuánto (ver Modifier.cs).
    [Section(EffectSection.Modifiers)]
    [Tooltip("Qué atributos cambia y cuánto. Cada entrada: atributo · cómo (sumar, multiplicar, " +
             "reemplazar) · cuánto, y si escala con un stat del que lo aplica o con la vida del objetivo.")]
    public List<Modifier> Modifiers = new List<Modifier>();

    [Section(EffectSection.Control)]
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

    [Tooltip("Metros que mueve al objetivo en el instante en que recibe el efecto. 0 = no lo " +
             "mueve. Es CONTROL: no le hace nada a un Imparable (Status_Unstoppable) y la " +
             "Resistencia al control recorta la distancia. El escudo no lo frena (el escudo solo " +
             "mitiga daño). Solo pasa al APLICARSE, no en cada tick.")]
    public float KnockbackDistance = 0f;

    [ShowIf(nameof(KnockbackDistance), ShowIfAttribute.Positive)]
    [Tooltip("Repeler (lejos de quien lo aplicó), atraer (hacia él) o hacia donde mira quien lo aplicó.")]
    public EKnockbackDirection KnockbackDirection = EKnockbackDirection.AwayFromSource;

    [ShowIf(nameof(KnockbackDistance), ShowIfAttribute.Positive)]
    [Tooltip("Segundos que tarda el desplazamiento. Arranca rápido y frena al final.")]
    public float KnockbackDuration = 0.3f;

    [ShowIf(nameof(KnockbackDistance), ShowIfAttribute.Positive)]
    [Tooltip("Velocidad hacia arriba al empujar (lo levanta un poco del piso). 0 = a ras del piso.")]
    public float KnockbackUpVelocity = 0f;

    [ShowIf(nameof(KnockbackDistance), ShowIfAttribute.Positive)]
    [ShowIf(nameof(KnockbackDirection), EKnockbackDirection.TowardSource)]
    [Tooltip("Solo al ATRAER: a cuántos metros de quien lo aplicó se detiene, para que no lo atraviese.")]
    public float PullStopDistance = 1.5f;

    [Section(EffectSection.Visuals)]
    [Tooltip("VFX que aparece sobre QUIEN RECIBE este efecto y vive lo que viva el efecto. " +
             "Pensado para que un debuff importante se vea encima del jugador afectado (las " +
             "flechas cayendo, un aura, unas cadenas), sin tener que instanciarlo a mano desde " +
             "la habilidad. Siempre queda ENGANCHADO al objetivo: lo sigue si se mueve, y se " +
             "destruye solo cuando el efecto expira o se lo quitan. Solo tiene sentido en " +
             "efectos CON duración: uno instantáneo se aplica y se va en el mismo frame. " +
             "Dejalo en None si el efecto no necesita nada visible.")]
    public GameObject TargetVFX;

    [ShowIf(nameof(TargetVFX))]
    [Tooltip("Desplazamiento del VFX respecto al pivote del objetivo, en su espacio local. " +
             "Los pivotes están a los pies, así que casi siempre vas a querer subirlo en Y " +
             "(2-3 para algo que flote sobre la cabeza).")]
    public Vector3 TargetVFXOffset;

    [ShowIf(nameof(TargetVFX))]
    [Tooltip("Rotación extra del VFX respecto al objetivo, en grados.")]
    public Vector3 TargetVFXRotation;

    [ShowIf(nameof(TargetVFX))]
    [Tooltip("Escala del VFX. En CERO usa la escala del prefab tal cual.")]
    public Vector3 TargetVFXScale;

    [ShowIf(nameof(TargetVFX))]
    [Tooltip("Repetir las partículas del VFX mientras dure el efecto. Muchos VFX de los packs " +
             "son de un solo disparo (emiten un segundo y se apagan), así que sin esto el " +
             "objeto sigue vivo pero vacío hasta que el efecto termina. Al terminar, deja de " +
             "emitir y las partículas que quedan se desvanecen solas en vez de cortarse de golpe. " +
             "Apagalo si el VFX está pensado para verse UNA vez al aplicarse.")]
    public bool TargetVFXLoop = true;

    [Section(EffectSection.Sound)]
    [Tooltip("Suena en el personaje cuando el efecto se le aplica (solo efectos CON duración: " +
             "quemadura, stun, escudo...). El daño instantáneo tiene su propio sonido de golpe " +
             "recibido en AudioLibrary. Vacío = silencio.")]
    public SfxCue TargetSound;

    // Tags que se le agregan al objetivo mientras el efecto está activo
    // (ej: Stunned) y se le quitan al terminar. El primer tag de esta
    // lista también sirve como "identidad" del efecto para cooldowns y
    // sincronización en red — ver GameplayAbility.CanActivate() y
    // NetworkAbilitySystemComponent.
    [Section(EffectSection.Tags)]
    [Tooltip("Tags que tiene el objetivo mientras el efecto está activo (State_Stunned, " +
             "Status_Slow...). El PRIMERO es además la identidad del efecto: con él se bloquea un " +
             "cooldown y se reconoce el efecto en la red y en la UI.")]
    public List<EGameplayTag> GrantedTags = new List<EGameplayTag>();
}
