// ============================================================
// AttributeValue
//
// Guarda el estado runtime de UN atributo de un personaje (ej: la
// Vida). Separa el valor base de los modificadores temporales que
// le aplican los GameplayEffect activos, para poder recalcular
// CurrentValue sin perder de dónde salió cada parte. Vive dentro
// del diccionario Attributes de AbilitySystemComponent.
// ============================================================
public class AttributeValue
{
    // Valor inicial del atributo, antes de aplicar ningún modificador
    // (ej: la Vida Máxima base de la clase). Subirlo con UpgradeAttribute
    // sube el "piso" del atributo de forma permanente.
    public float BaseValue;

    // Valor final que realmente se usa en el juego (ej: 50/100 de HP).
    // Se recalcula a partir de BaseValue + los modificadores de abajo.
    public float CurrentValue;

    // Suma de todos los modificadores de tipo 'Add' de los efectos
    // activos. Sube o baja mientras el efecto que lo aportó esté vigente.
    public float AdditiveModifier;

    // Producto de todos los modificadores de tipo 'Multiply' de los
    // efectos activos. Arranca en 1 (sin efecto); un valor de 1.5 sería
    // "+50%".
    public float MultiplicativeModifier;

    // Crea el atributo con su valor base; arranca sin modificadores.
    public AttributeValue(float baseVal)
    {
        BaseValue = baseVal;
        CurrentValue = baseVal;
        AdditiveModifier = 0f;
        MultiplicativeModifier = 1f; // 1.0 = no altera el valor al multiplicar
    }
}
