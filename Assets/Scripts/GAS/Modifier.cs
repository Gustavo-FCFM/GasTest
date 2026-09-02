using UnityEngine;

// ============================================================
// Modifier
//
// Describe UN cambio que un GameplayEffect le hace a un atributo
// (ej: "-10 de Vida" o "+20% de Velocidad"). Un GameplayEffect
// puede tener varios Modifier en su lista — cada uno se aplica por
// separado en AbilitySystemComponent.ExecuteInstantEffect /
// ApplyEffectModifiers.
// ============================================================
[System.Serializable]
public class Modifier
{
    // Tipo de cambio: sumar/restar un valor fijo, multiplicar, o
    // reemplazar el valor entero.
    public enum EModificationType { Add, Multiply, Override }

    [Header("Configuración Básica")]
    // A qué atributo del objetivo afecta este modificador.
    public EAttributeType Attribute;

    // Cómo se combina Magnitude con el valor actual del atributo.
    public EModificationType Type;

    [Header("Valor Fijo")]
    // Cantidad base del cambio (ej: -10 de daño, o 1.2 para Multiply =
    // +20%). Si UseAttributeScaling está activo, esto se SUMA al
    // escalado calculado abajo.
    public float Magnitude;

    [Header("Escalado de Atributos (Scaling)")]
    // Si está activo, el modificador también escala con un stat de quien
    // aplicó el efecto (ej: el Ataque del atacante), además de Magnitude.
    public bool UseAttributeScaling;

    // Qué atributo DEL ATACANTE se usa para el escalado (ej: Atq).
    public EAttributeType SourceAttribute;

    // Cuánto de ese atributo del atacante se suma (ej: 1.0 = 100% del
    // Ataque se suma a Magnitude).
    public float AttributeCoefficient = 1.0f;

    [Header("Escalado por Vida del Objetivo (Ejecución)")]
    // Si está activo, el modificador escala también con la VIDA del OBJETIVO
    // (el que recibe el efecto), no la del atacante. Sirve para:
    //  - Daño según la vida faltante del enemigo (ej: Golpe mortal del Pícaro).
    //  - Otorgar según la PROPIA vida faltante, si el efecto se aplica a uno
    //    mismo (ej: escudo del Berserker en un GA_SelfBuff).
    // DONDE SE APLICA: en efectos instantaneos (ExecuteInstantEffect) para cualquier
    // atributo, y en efectos CON duracion solo para el atributo Shield, que tiene su
    // propio camino (GrantTemporaryShield) porque es un pool y no un modificador. El
    // escudo del Berserker es justamente ese caso: GE_Frenzy dura 10s y su escudo si
    // escala por vida faltante.
    public bool UseTargetHealthScaling;

    // Sobre qué porción de la vida del objetivo escalar.
    public enum ETargetHealthMode
    {
        MissingHealth, // VidaMáx - VidaActual (vida faltante)
        CurrentHealth, // Vida actual
        MaxHealth      // Vida máxima
    }
    public ETargetHealthMode TargetHealthMode = ETargetHealthMode.MissingHealth;

    // Fracción de esa vida que se aplica. El SIGNO marca la dirección, igual que
    // Magnitude:
    //   NEGATIVO = quita   (ej: -0.3 → daño por el 30% de la vida faltante)
    //   POSITIVO = otorga  (ej:  0.5 → escudo/curación por el 50% de la vida faltante)
    // Solo se usa si UseTargetHealthScaling está activo.
    public float TargetHealthCoefficient = 0f;

    [Header("Piso del Resultado")]
    [Tooltip("Valor MÍNIMO que puede dar este modificador después de todos los escalados, en " +
             "valor absoluto. En 0 (el default) no hay piso.\n\n" +
             "Se aplica en la dirección que el modificador YA tiene: poner 1 en un efecto de " +
             "daño significa \"al menos 1 de daño\", y en uno de escudo, \"al menos 1 de escudo\". " +
             "El signo que escribas no importa — se usa el de la configuración del modificador.\n\n" +
             "PARA QUÉ SIRVE: un escalado por vida faltante da CERO cuando el objetivo está a " +
             "vida llena. El escudo del Berserker a vida completa vale 0 — se otorga y se agota " +
             "en el mismo instante, y su burbuja aparece y desaparece de golpe. Con un piso, " +
             "siempre hay algo que mostrar y que absorber.\n\n" +
             "En modificadores de tipo Multiply el piso es un FACTOR mínimo, no un valor plano.")]
    public float MinMagnitude = 0f;
}
