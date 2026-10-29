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
        [InspectorName("Se refresca (uno solo)")] Refresh,  // Reinicia la duración (Veneno + Veneno = 1 Veneno con el tiempo reiniciado)
        [InspectorName("Se acumula")]             Stack,    // Se acumula (Veneno + Veneno = 2 Venenos haciendo daño)
        [InspectorName("Se reemplaza")]           Override  // El nuevo reemplaza al viejo (ej: un buff que cambia de nivel)
    }

    // Hacia dónde mueve el desplazamiento de este efecto (ver KnockbackDistance).
    public enum EKnockbackDirection
    {
        [InspectorName("Repeler (lejos)")]           AwayFromSource,  // Repeler: lejos de quien lo aplicó (el dash del Monje)
        [InspectorName("Atraer (hacia él)")]         TowardSource,    // Atraer: hacia quien lo aplicó (un gancho); frena a PullStopDistance
        [InspectorName("Hacia donde mira el que lo aplica")] SourceForward    // Hacia donde mira quien lo aplicó (una patada en línea)
    }

    // Si es Buff/Debuff/Hidden — controla si aparece en la barra de
    // efectos activos y de qué color (ver UI_EffectSlot).
    public enum EEffectType
    {
        [InspectorName("Buff (verde)")]   Buff,   // Verde, beneficioso
        [InspectorName("Debuff (rojo)")]  Debuff, // Rojo, dañino
        [InspectorName("Oculto")]         Hidden  // No se muestra en UI (ej: el propio cooldown de una habilidad)
    }

    // Si es 0, el efecto se aplica una sola vez y desaparece (daño,
    // curación). Si es mayor a 0, queda activo ese tiempo (buffs,
    // debuffs, cooldowns).
    [Section(EffectSection.Duration)]
    [Tooltip("0 = instantáneo (daño, curación: se aplica y listo). > 0 = dura esos segundos " +
             "(buffs, debuffs, cooldowns).")]
    [Label("Duración (s, 0 = instantáneo)")]
    public float Duration = 0f;

    // Si es mayor a 0, mientras el efecto está activo se re-ejecuta cada
    // tantos segundos (ej: veneno que daña cada 2s).
    [ShowIf(nameof(Duration), ShowIfAttribute.Positive)]
    [Tooltip("Cada cuántos segundos se vuelve a aplicar mientras dura (un veneno que daña cada " +
             "2 s). 0 = no es periódico.")]
    [Label("Período (s)")]
    public float Period = 0f;

    [Tooltip("Qué pasa si se vuelve a aplicar mientras ya está activo.\n\n" +
             "· Refresh: reinicia su duración (uno solo).\n" +
             "· Stack: se acumula (dos venenos = el doble).\n" +
             "· Override: el nuevo reemplaza al viejo.")]
    [Label("Al reaplicarse")]
    public EStackingType StackingPolicy = EStackingType.Stack;

    [ShowIf(nameof(StackingPolicy), EStackingType.Stack)]
    [Tooltip("Máximo de acumulaciones. 0 = sin límite. Al llegar al tope, aplicarlo de nuevo " +
             "refresca la acumulación que esté por expirar en vez de agregar otra (salvo que uses " +
             "OnMaxStacksEffect).")]
    [Label("Acumulaciones máximas (0 = sin límite)")]
    public int MaxStacks = 0;

    [ShowIf(nameof(StackingPolicy), EStackingType.Stack)]
    [ShowIf(nameof(MaxStacks), ShowIfAttribute.Positive)]
    [Tooltip("Al llegar al tope de acumulaciones, en vez de refrescarlas se CONSUMEN todas y se " +
             "aplica este efecto al objetivo (la 'explosión' de las Heridas del Ilusionista). " +
             "None = el comportamiento normal de tope.")]
    [Label("Efecto al tope")]
    public GameplayEffect OnMaxStacksEffect;

    [Section(EffectSection.Group, startCollapsed: true)]
    [Tooltip("Efectos con el MISMO grupo (≠ None) se excluyen: solo vive el de mayor Priority a la vez. Ej: el buff normal del tótem y su versión potenciada comparten grupo, así no se acumulan.")]
    [Label("Grupo")]
    public EGameplayTag EffectGroup = EGameplayTag.None;

    [ShowIf(nameof(EffectGroup), EGameplayTag.None, true)]
    [Tooltip("Dentro de un EffectGroup, mayor Priority gana. Aplicar uno de Priority MENOR a uno ya activo del grupo no hace nada; uno de Priority MAYOR reemplaza a los inferiores.")]
    [Label("Prioridad (mayor gana)")]
    public int Priority = 0;

    [ShowIf(nameof(EffectGroup), EGameplayTag.None, true)]
    [Tooltip("Dentro de su EffectGroup, el ÚLTIMO en aplicarse reemplaza a los demás, sin " +
             "importar Priority. Es para estados excluyentes que se alternan, como las posturas " +
             "del Guerrero: entrar a la ofensiva saca la defensiva en el mismo instante, así " +
             "nunca se tienen las dos ventajas a la vez.")]
    [Label("Reemplaza al grupo")]
    public bool ReplacesGroup = false;

    [Section(EffectSection.General)]
    [Tooltip("Ícono en la barra de buffs/debuffs (no hace falta si es Hidden).")]
    [Label("Ícono")]
    public Sprite Icon;

    [Tooltip("Buff (verde) o Debuff (rojo) en la barra de efectos; Hidden no se muestra (un " +
             "cooldown, un daño instantáneo).")]
    [Label("Tipo")]
    public EEffectType EffectType;

    // Qué atributos cambia este efecto y cuánto (ver Modifier.cs).
    [Section(EffectSection.Modifiers)]
    [Tooltip("Qué atributos cambia y cuánto. Cada entrada: atributo · cómo (sumar, multiplicar, " +
             "reemplazar) · cuánto, y si escala con un stat del que lo aplica o con la vida del objetivo.")]
    [Label("Modificadores")]
    public List<Modifier> Modifiers = new List<Modifier>();

    [Section(EffectSection.Control)]
    [Tooltip("Marca este efecto como CONTROL: su duración se acorta (o se alarga) con la " +
             "Resistencia al control del objetivo — ver EAttributeType.CCResistance.\n\n" +
             "No hace falta marcarlo si el efecto otorga State_Stunned, State_Rooted o " +
             "State_Silenced: esos ya cuentan como control solos. Es para el CC que no pasa por " +
             "esos tags (cegueras, miedos, ralentizaciones fuertes) y que igual querés que la " +
             "resistencia recorte.")]
    [Label("Es control")]
    public bool IsCrowdControl = false;

    // ¿Sube la vida? Un modificador sumado sobre Health con algo positivo (fijo o escalado).
    // Lo usa la habilidad para sus VFX "al curar" (ver GameplayAbility.ApplyHitEffects).
    public bool HealsHealth
    {
        get
        {
            if (Modifiers == null) return false;
            foreach (Modifier m in Modifiers)
            {
                if (m.Attribute != EAttributeType.Health || m.Type != Modifier.EModificationType.Add) continue;
                if (m.Magnitude > 0f ||
                    (m.UseAttributeScaling && m.AttributeCoefficient > 0f) ||
                    (m.UseTargetHealthScaling && m.TargetHealthCoefficient > 0f))
                    return true;
            }
            return false;
        }
    }

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

            // Un BUFF que da un tag de control se lo pone uno mismo (la Tumba helada del Mago:
            // quieto y a salvo): no es un control que le hacen, la resistencia no lo recorta.
            if (EffectType == EEffectType.Buff) return false;

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
    [Label("Distancia (m)")]
    public float KnockbackDistance = 0f;

    [ShowIf(nameof(KnockbackDistance), ShowIfAttribute.Positive)]
    [Tooltip("Repeler (lejos de quien lo aplicó), atraer (hacia él) o hacia donde mira quien lo aplicó.")]
    [Label("Dirección")]
    public EKnockbackDirection KnockbackDirection = EKnockbackDirection.AwayFromSource;

    [ShowIf(nameof(KnockbackDistance), ShowIfAttribute.Positive)]
    [Tooltip("Segundos que tarda el desplazamiento. Arranca rápido y frena al final.")]
    [Label("Duración (s)")]
    public float KnockbackDuration = 0.3f;

    [ShowIf(nameof(KnockbackDistance), ShowIfAttribute.Positive)]
    [Tooltip("Velocidad hacia arriba al empujar (lo levanta un poco del piso). 0 = a ras del piso.")]
    [Label("Impulso hacia arriba (m/s)")]
    public float KnockbackUpVelocity = 0f;

    [ShowIf(nameof(KnockbackDistance), ShowIfAttribute.Positive)]
    [ShowIf(nameof(KnockbackDirection), EKnockbackDirection.TowardSource)]
    [Tooltip("Solo al ATRAER: a cuántos metros de quien lo aplicó se detiene, para que no lo atraviese.")]
    [Label("Se detiene a (m)")]
    public float PullStopDistance = 1.5f;

    [Tooltip("EJECUTAR: mientras tenga este efecto, si su vida queda en esta fracción de su vida " +
             "máxima o menos, muere (0.05 = al 5 %). Se mira al recibir el efecto y en cada daño " +
             "mientras dura. La baja es de quien le puso el efecto. No mata a un inmortal (Preservar " +
             "vida) ni a un inmune. 0 = no ejecuta (el Corte final del Samurái).")]
    [Label("Ejecutar con vida bajo (0 a 1)")]
    [Range(0f, 1f)] public float ExecuteBelowHealth = 0f;

    [Tooltip("El daño de este efecto sale CRÍTICO si el que lo recibe tiene este tag (la estocada " +
             "del Trampero: crítica contra un enemigo con heridas, Status_Wound). Es la misma capa que " +
             "el golpe por la espalda: no se suma a ella. Nada = como siempre.")]
    [Label("Crítico si el objetivo tiene")]
    public EGameplayTag CritIfTargetHas = EGameplayTag.None;

    [Section(EffectSection.Visuals)]
    [Tooltip("VFX que aparece sobre QUIEN RECIBE este efecto y vive lo que viva el efecto. " +
             "Pensado para que un debuff importante se vea encima del jugador afectado (las " +
             "flechas cayendo, un aura, unas cadenas), sin tener que instanciarlo a mano desde " +
             "la habilidad. Siempre queda ENGANCHADO al objetivo: lo sigue si se mueve, y se " +
             "destruye solo cuando el efecto expira o se lo quitan. Solo tiene sentido en " +
             "efectos CON duración: uno instantáneo se aplica y se va en el mismo frame. " +
             "Dejalo en None si el efecto no necesita nada visible.")]
    [Label("VFX en el objetivo")]
    public GameObject TargetVFX;

    [ShowIf(nameof(TargetVFX))]
    [Tooltip("Desplazamiento del VFX respecto al pivote del objetivo, en su espacio local. " +
             "Los pivotes están a los pies, así que casi siempre vas a querer subirlo en Y " +
             "(2-3 para algo que flote sobre la cabeza).")]
    [Label("VFX: posición (m)")]
    public Vector3 TargetVFXOffset;

    [ShowIf(nameof(TargetVFX))]
    [Tooltip("Rotación extra del VFX respecto al objetivo, en grados.")]
    [Label("VFX: rotación (°)")]
    public Vector3 TargetVFXRotation;

    [ShowIf(nameof(TargetVFX))]
    [Tooltip("Escala del VFX. En CERO usa la escala del prefab tal cual.")]
    [Label("VFX: escala (×)")]
    public Vector3 TargetVFXScale;

    [ShowIf(nameof(TargetVFX))]
    [Tooltip("Repetir las partículas del VFX mientras dure el efecto. Muchos VFX de los packs " +
             "son de un solo disparo (emiten un segundo y se apagan), así que sin esto el " +
             "objeto sigue vivo pero vacío hasta que el efecto termina. Al terminar, deja de " +
             "emitir y las partículas que quedan se desvanecen solas en vez de cortarse de golpe. " +
             "Apagalo si el VFX está pensado para verse UNA vez al aplicarse.")]
    [Label("Repetir partículas")]
    public bool TargetVFXLoop = true;

    [Tooltip("Mientras dura, cada SALTO de quien lo tiene (del piso o desde una pared) deja este " +
             "VFX en el lugar donde saltó: el humo del Manto de oscuridad del Shinobi. Se ve en " +
             "todas las pantallas, también en las de los enemigos. Vacío = nada.")]
    [Label("VFX al saltar")]
    public GameObject JumpVFX;

    [Section(EffectSection.Sound)]
    [Tooltip("Suena en el personaje cuando el efecto se le aplica (solo efectos CON duración: " +
             "quemadura, stun, escudo...). El daño instantáneo tiene su propio sonido de golpe " +
             "recibido en AudioLibrary. Vacío = silencio.")]
    [Label("Sonido al aplicarse")]
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
    [Label("Tags que da")]
    public List<EGameplayTag> GrantedTags = new List<EGameplayTag>();

    // =========================================================
    // COPIAS CONGELADAS (el proyectil devuelto por un parry)
    // =========================================================

    // Cuando este efecto es una COPIA hecha en juego (CreateFrozenCopy), apunta al asset
    // original. La red y la UI la tratan como el original: mismo índice en
    // GameplayEffectRegistry, mismo ícono y mismo VFX. NonSerialized: es de runtime.
    [System.NonSerialized] public GameplayEffect SourceTemplate;

    // El asset que este efecto "es": el original si es una copia, él mismo si no.
    public GameplayEffect Template => SourceTemplate != null ? SourceTemplate : this;

    // Una copia de este efecto con los stats de 'statsFrom' ya puestos como número fijo:
    // cada modificador que escala con un stat del autor deja de leerlo y se queda con lo
    // que valía AHORA. Todo lo demás queda igual (duración, ticks, tags, VFX), y también el
    // tipo de daño: un daño mágico sigue diciendo que escala del daño mágico, con
    // coeficiente 0, así que sigue ignorando la armadura.
    //
    // La usa el proyectil devuelto por un parry (GC_Projectile.Reflect): lo firma el que
    // lo devolvió (la baja es suya), pero hace el daño que traía, calculado con los stats
    // de quien lo tiró. Con el efecto original, el daño se recalcularía con los del
    // Guardián, y un rayo mágico devuelto (el Guardián no tiene daño mágico) no haría nada.
    public GameplayEffect CreateFrozenCopy(AbilitySystemComponent statsFrom)
    {
        // Instantiate copia la lista de modificadores entera (objetos nuevos): tocar los
        // de la copia no toca el asset.
        GameplayEffect copy = Instantiate(this);
        copy.name           = name + "(congelado)";
        copy.SourceTemplate = Template;
        if (statsFrom == null) return copy;

        foreach (Modifier m in copy.Modifiers)
        {
            if (!m.UseAttributeScaling) continue;

            float value = m.Magnitude + statsFrom.GetAttributeValue(m.SourceAttribute) * m.AttributeCoefficient;

            // Con el stat en 0 quedaría en 0, y el piso (MinMagnitude) ya no sabría si era daño
            // o curación (lo saca del coeficiente): se deja una pizca con el signo que tenía.
            if (m.MinMagnitude != 0f && Mathf.Abs(value) < 0.0001f)
                value = (m.Magnitude != 0f ? Mathf.Sign(m.Magnitude) : Mathf.Sign(m.AttributeCoefficient)) * 0.0001f;

            m.Magnitude            = value;
            m.AttributeCoefficient = 0f;
        }
        return copy;
    }

    // Con qué escala de daño se hizo esta copia (CreateDamageScaledCopy); 1 en los assets.
    [System.NonSerialized] public float CreatedWithDamageScale = 1f;

    // Una copia que hace 'scale' del DAÑO: los modificadores sumados que le BAJAN la vida (fijos,
    // escalados por stat o por la vida del objetivo) se multiplican; todo lo demás queda igual
    // (duración, ticks, tags, lentitud, VFX). Si no baja vida, devuelve el mismo efecto. La usan
    // las copias de los ecos (la Proyección arcana: la mitad del daño, también en la quemadura).
    public GameplayEffect CreateDamageScaledCopy(float scale)
    {
        if (Mathf.Approximately(scale, 1f) || !DamagesHealth) return this;

        GameplayEffect copy = Instantiate(this);
        copy.name                   = name + $"(×{scale:0.##})";
        copy.SourceTemplate         = Template;
        copy.CreatedWithDamageScale = scale;

        foreach (Modifier m in copy.Modifiers)
        {
            if (!IsDamage(m)) continue;
            m.Magnitude               *= scale;
            m.AttributeCoefficient    *= scale;
            m.TargetHealthCoefficient *= scale;
            m.MinMagnitude            *= scale;
        }
        return copy;
    }

    // Una copia cuyo DAÑO es mágico: los modificadores que bajan vida escalando del Ataque pasan a
    // escalar del Daño mágico (con el mismo coeficiente), así ignora la armadura y le rompe el doble
    // al escudo. Sin esos, devuelve el mismo efecto. La usa la Espada mágica del Filo danzante (el
    // "Daño mágico con el tag" de sus cortes).
    public GameplayEffect CreateMagicCopy()
    {
        if (!HasPhysicalScaledDamage) return this;

        GameplayEffect copy = Instantiate(this);
        copy.name                   = name + "(mágico)";
        copy.SourceTemplate         = Template;
        copy.CreatedWithDamageScale = CreatedWithDamageScale;

        foreach (Modifier m in copy.Modifiers)
            if (IsPhysicalScaledDamage(m)) m.SourceAttribute = EAttributeType.MagicDamage;
        return copy;
    }

    private bool HasPhysicalScaledDamage
    {
        get
        {
            if (Modifiers == null) return false;
            foreach (Modifier m in Modifiers)
                if (IsPhysicalScaledDamage(m)) return true;
            return false;
        }
    }

    private static bool IsPhysicalScaledDamage(Modifier m)
        => IsDamage(m) && m.UseAttributeScaling && m.SourceAttribute == EAttributeType.Attack;

    // ¿Le baja la vida a alguien? (un modificador sumado sobre Health con algo negativo)
    public bool DamagesHealth
    {
        get
        {
            if (Modifiers == null) return false;
            foreach (Modifier m in Modifiers)
                if (IsDamage(m)) return true;
            return false;
        }
    }

    private static bool IsDamage(Modifier m)
        => m.Attribute == EAttributeType.Health && m.Type == Modifier.EModificationType.Add &&
           (m.Magnitude < 0f ||
            (m.UseAttributeScaling && m.AttributeCoefficient < 0f) ||
            (m.UseTargetHealthScaling && m.TargetHealthCoefficient < 0f));

    // ¿Algún modificador depende de los stats del autor? Si no, congelarlo no cambia nada.
    public bool ScalesWithSource
    {
        get
        {
            foreach (Modifier m in Modifiers)
                if (m.UseAttributeScaling) return true;
            return false;
        }
    }
}
