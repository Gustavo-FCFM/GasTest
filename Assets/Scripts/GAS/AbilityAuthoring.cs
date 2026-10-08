using System;
using UnityEngine;

// ============================================================
// AbilityAuthoring
//
// Las piezas con las que se ARMA una habilidad en el Inspector, compartidas por todos
// los GA:
//
//  · [Section] y [ShowIf]: secciones plegables y campos que aparecen solo cuando tienen
//    sentido. Los lee el inspector a medida (GameplayAbilityEditor); en la build no hacen
//    nada. Un campo escondido por [ShowIf] CONSERVA su valor: si volvés a prender la
//    casilla, lo que tenías sigue ahí.
//
//  · AbilityEffect: una entrada de la lista de efectos de la habilidad. Qué GE, a quién
//    (enemigos, aliados, el lanzador, todos), cuándo (al golpear, al primer golpe, al
//    activarse, al matar) y con qué condición (que el objetivo tenga un tag).
//
//  · AbilityVisual: una entrada de la lista de VFX. Qué prefab, cuándo (al lanzar, en
//    cada golpe, en el punto de impacto), dónde, de qué tamaño y cuánto dura.
//
// Antes cada GA tenía sus propios campos sueltos para esto (DamageEffect +
// AdditionalEffects + TargetEffects + FirstHitEffects, HitVFX / ImpactVFX /
// VisualPrefab...), con nombres y reglas distintas en cada uno. Ahora todos usan las
// mismas dos listas. Los assets viejos se pasan solos al formato nuevo al cargarse (ver
// GameplayAbility.UpgradeLegacyData): no hay que volver a cargar nada.
// ============================================================

// Empieza una sección plegable del Inspector. Vale para el campo que la lleva y todos los
// que siguen (de la misma clase) hasta la próxima. Dos clases que usan el mismo título
// comparten la sección: los campos de la subclase aparecen debajo de los de la base.
[AttributeUsage(AttributeTargets.Field)]
public class SectionAttribute : Attribute
{
    public readonly string Title;
    public readonly bool   StartCollapsed;

    public SectionAttribute(string title, bool startCollapsed = false)
    {
        Title          = title;
        StartCollapsed = startCollapsed;
    }
}

// El campo solo se muestra si OTRO campo de la habilidad vale lo pedido:
//   [ShowIf(nameof(AimBeforeThrow))]                         → casilla prendida
//   [ShowIf(nameof(DeployMode), EAoEDeploy.AtReticle)]        → un valor del enum
//   [ShowIf(nameof(RecoilSpeed), ShowIfAttribute.Positive)]   → número mayor que cero
// Con VARIOS [ShowIf] en el mismo campo, tienen que cumplirse todos.
// Esconder no borra: el valor queda guardado para cuando vuelva a hacer falta.
[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
public class ShowIfAttribute : Attribute
{
    public const string Positive = ">0";

    public readonly string Field;
    public readonly object Value;
    public readonly bool   Invert;

    public ShowIfAttribute(string field)                             { Field = field; Value = true; }
    public ShowIfAttribute(string field, object value)               { Field = field; Value = value; }
    public ShowIfAttribute(string field, object value, bool invert)  { Field = field; Value = value; Invert = invert; }
}

// Los títulos de las secciones comunes, y en qué orden se muestran. Con constantes y no
// con texto suelto en cada GA: así "Efectos (GE)" es la MISMA sección en todos y queda
// siempre en el mismo lugar.
public static class AbilitySection
{
    public const string General      = "General";
    public const string CostCooldown = "Costo y cooldown";
    public const string Rules        = "Reglas de activación";
    public const string Targeting    = "Objetivo";
    public const string Shape        = "Forma y alcance";
    public const string Movement     = "Movimiento";
    public const string Timing       = "Tiempos";
    public const string Effects      = "Efectos (GE)";
    public const string Visuals      = "VFX";
    public const string Animation    = "Animación";
    public const string Sound        = "Sonido";
    public const string Advanced     = "Avanzado";

    // "*" = acá van las secciones propias de cada habilidad ("Proyectil", "La marca",
    // "Tótems"...), en el orden en que se declaran.
    public static readonly string[] Order =
    {
        General, CostCooldown, Rules, Targeting, Shape, Movement, Timing, "*",
        Effects, Visuals, Animation, Sound, Advanced,
    };
}

// Las secciones de un GameplayEffect, en el orden en que se muestran.
public static class EffectSection
{
    public const string General  = "General";
    public const string Duration = "Duración y acumulación";
    public const string Modifiers = "Modificadores";
    public const string Tags     = "Tags";
    public const string Control  = "Control y desplazamiento";
    public const string Group    = "Exclusión (grupo)";
    public const string Visuals  = "VFX en el objetivo";
    public const string Sound    = "Sonido";

    public static readonly string[] Order = { General, Duration, Modifiers, Tags, Control, Group, Visuals, Sound, "*" };
}

// Las secciones de una clase (CharacterClassDefinition), en el orden en que se muestran.
public static class ClassSection
{
    public const string Identity  = "Identidad";
    public const string Stats     = "Stats y progresión";
    public const string Abilities = "Habilidades";
    public const string Passives  = "Pasivas";
    public const string Movement  = "Movimiento";
    public const string Evolution = "Evolución";
    public const string Animation = "Animación";
    public const string Weapons   = "Armas";
    public const string Grow      = "Crecer (Avatar)";

    public static readonly string[] Order = { Identity, Stats, Abilities, Passives, Movement, Evolution, Animation, Weapons, Grow, "*" };
}

// =========================================================
// EFECTOS
// =========================================================

// CUÁNDO se aplica una entrada de la lista de efectos.
public enum EEffectWhen
{
    [InspectorName("Al golpear")]      OnHit      = 0,   // a cada objetivo que alcanza la habilidad
    [InspectorName("Al primer golpe")] OnFirstHit = 1,   // solo al primero (embestidas, proyectiles)
    [InspectorName("Al activarse")]    OnActivate = 2,   // al lanzarla (cuando se cobra el costo)
    [InspectorName("Al matar")]        OnKill     = 3,   // cuando un golpe de esta habilidad mata
}

// A QUIÉN se le aplica.
public enum EEffectTarget
{
    [InspectorName("Enemigos")]              Enemies  = 0,
    [InspectorName("Aliados (y uno mismo)")] Allies   = 1,
    [InspectorName("El lanzador")]           Self     = 2,
    [InspectorName("Todos")]                 Everyone = 3,
}

// Una entrada de GameplayAbility.Effects. El valor por defecto (todo en cero) es "al
// golpear, a los enemigos": lo que se quiere casi siempre al agregar una entrada nueva.
[Serializable]
public struct AbilityEffect
{
    [Tooltip("CUÁNDO se aplica.\n\n" +
             "· Al golpear: a cada objetivo que alcanza la habilidad.\n" +
             "· Al primer golpe: solo al primero que alcanza (el aturdido de una embestida).\n" +
             "· Al activarse: al lanzar la habilidad, cuando se cobra el costo. Va al lanzador.\n" +
             "· Al matar: cuando un golpe de esta habilidad mata. Va al lanzador.")]
    public EEffectWhen When;

    [Tooltip("A QUIÉN se le aplica.\n\n" +
             "· Enemigos / Aliados: a los alcanzados de ese bando. En cuanto haya algo para los " +
             "aliados, la habilidad empieza a tenerlos en cuenta (un ataque que cura a su paso).\n" +
             "· El lanzador: a uno mismo (con 'Al golpear', una vez por cada enemigo golpeado).\n" +
             "· Todos: a cualquiera que alcance, de cualquier bando.")]
    public EEffectTarget ApplyTo;

    public GameplayEffect Effect;

    [Tooltip("CONDICIÓN (opcional): solo se aplica si el objetivo alcanzado tiene este tag. " +
             "Con 'Al activarse' se mira al lanzador. None = siempre.")]
    public EGameplayTag OnlyIfTargetHas;

    public AbilityEffect(GameplayEffect effect, EEffectWhen when = EEffectWhen.OnHit,
                         EEffectTarget applyTo = EEffectTarget.Enemies)
    {
        Effect          = effect;
        When            = when;
        ApplyTo         = applyTo;
        OnlyIfTargetHas = EGameplayTag.None;
    }
}

// =========================================================
// VFX
// =========================================================

// CUÁNDO aparece una entrada de la lista de VFX.
public enum EVisualWhen
{
    [InspectorName("Al lanzar (en el lanzador)")]      OnCast   = 0,
    [InspectorName("Al golpear (en cada objetivo)")]   OnHit    = 1,
    [InspectorName("En el impacto (punto o área)")]    OnImpact = 2,
}

// Una entrada de GameplayAbility.Visuals. El valor 0 de When es OnCast, que es lo que
// eran TODAS las entradas de la vieja VisualsSequence: los assets que ya la tenían
// cargada no cambian.
[Serializable]
public struct AbilityVisual
{
    [Tooltip("CUÁNDO aparece.\n\n" +
             "· Al lanzar: sobre el lanzador, al activar la habilidad (auras, destellos de casteo).\n" +
             "· Al golpear: sobre cada personaje alcanzado.\n" +
             "· En el impacto: en el punto donde cae la habilidad (la explosión de un área, donde " +
             "choca un proyectil, donde aterriza un salto).")]
    public EVisualWhen When;

    public GameObject VFXPrefab;

    [Tooltip("Espera antes de que aparezca, en segundos.")]
    public float Delay;

    [Tooltip("Desplazamiento. Al lanzar es LOCAL al lanzador (Z = adelante); en los demás es " +
             "en el mundo (Y = arriba). (0, 1, 0) = a la altura del pecho.")]
    public Vector3 Offset;

    [Tooltip("Rotación extra, en grados.")]
    public Vector3 RotationOffset;

    [Tooltip("Escala. (0, 0, 0) = la del prefab.")]
    public Vector3 Scale;

    // FormerlySerializedAs: se llamaba AttachToOwner (solo existía el 'al lanzar').
    [UnityEngine.Serialization.FormerlySerializedAs("AttachToOwner")]
    [Tooltip("Lo sigue: al lanzar, pegado al lanzador; al golpear, pegado al objetivo; en el " +
             "impacto, pegado al lanzador SI el área lo sigue (una zona que se mueve con él).")]
    public bool Attach;

    [Tooltip("Solo en el impacto: se escala para calzar con el RADIO del área de la habilidad. " +
             "Con el componente VFX_AreaVisual en el prefab calza exacto; si no, se usa el " +
             "multiplicador de abajo (radio × multiplicador).")]
    public bool MatchAreaSize;

    [Tooltip("Con 'Calzar con el área' y un prefab SIN VFX_AreaVisual: escala = radio × esto. " +
             "0 = 1.")]
    public float AreaSizeMultiplier;

    [Tooltip("Segundos hasta que se borra. 0 = lo que dure la habilidad (un área, su duración; " +
             "un golpe, 2 s). Al lanzar, 0 = no se borra solo (usar el fin por tag o atributo).")]
    public float DestroyTime;

    [Tooltip("Solo al lanzar: en vez de durar un tiempo fijo, vive mientras el lanzador tenga " +
             "este tag (el aura de un buff). None = no se usa.")]
    public EGameplayTag EndWithTag;

    // Fin por ATRIBUTO AGOTADO, en vez de (o además de) por tag. El VFX vive mientras el
    // atributo elegido sea mayor que cero. Existe para el escudo del Frenzy: el buff y el
    // escudo son la MISMA habilidad, pero el escudo se consume con los golpes mientras el
    // buff sigue corriendo. Con las dos condiciones configuradas, muere con la primera que falle.
    [Tooltip("Solo al lanzar: vive mientras este atributo del lanzador sea mayor que cero (la " +
             "burbuja de un escudo que se gasta con los golpes).")]
    public bool EndWhenAttributeDepleted;
    public EAttributeType DepletedAttribute;
}
