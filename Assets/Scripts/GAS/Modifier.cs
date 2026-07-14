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
    // Si está activo, el modificador escala también con la VIDA del objetivo
    // (el que recibe el efecto), no la del atacante. Pensado para habilidades
    // tipo "hace daño según la vida faltante del enemigo" (ej: Golpe mortal del
    // Pícaro). El valor calculado se suma en la MISMA dirección que Magnitude
    // (para un modificador de daño —Health negativo— agrega más daño).
    public bool UseTargetHealthScaling;

    // Sobre qué porción de la vida del objetivo escalar.
    public enum ETargetHealthMode
    {
        MissingHealth, // VidaMáx - VidaActual (vida faltante)
        CurrentHealth, // Vida actual
        MaxHealth      // Vida máxima
    }
    public ETargetHealthMode TargetHealthMode = ETargetHealthMode.MissingHealth;

    // Fracción de esa vida que se aplica (ej: 0.3 = 30% de la vida faltante del
    // objetivo). Solo se usa si UseTargetHealthScaling está activo.
    public float TargetHealthCoefficient = 0.3f;
}
