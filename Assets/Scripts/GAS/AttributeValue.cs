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
    // "+50%". Se recalcula de la lista de abajo cada vez que entra o sale uno.
    public float MultiplicativeModifier;

    // Los multiplicadores activos, uno por efecto (y por acumulación) que lo aporta.
    //
    // POR QUÉ UNA LISTA Y NO UN ACUMULADOR: antes se acumulaba (magnitud − 1) y se
    // restaba al quitar el efecto, o sea que los multiplicadores se SUMABAN: ×0.5 y
    // ×0.5 daban ×0 (el Chamán con Enfurecer + Tigre atacaba al tope), y la bolsa
    // (×0.75) + una ralentización (×0.3) dejaba al portador casi quieto (×0.05).
    // Multiplicando de verdad dan ×0.25 y ×0.225. Guardar la lista permite quitar
    // uno exacto sin dividir (un ×0 no se podría deshacer dividiendo).
    public readonly System.Collections.Generic.List<float> Multipliers =
        new System.Collections.Generic.List<float>();

    // Crea el atributo con su valor base; arranca sin modificadores.
    public AttributeValue(float baseVal)
    {
        BaseValue = baseVal;
        CurrentValue = baseVal;
        AdditiveModifier = 0f;
        MultiplicativeModifier = 1f; // 1.0 = no altera el valor al multiplicar
    }

    // Suma o saca un multiplicador y rehace el producto.
    public void AddMultiplier(float factor)
    {
        Multipliers.Add(factor);
        RecomputeMultiplier();
    }

    public void RemoveMultiplier(float factor)
    {
        // Saca UNO igual (el mismo GE siempre aporta la misma magnitud). Si no está
        // —el atributo se rehízo desde cero mientras el efecto seguía— no hace nada,
        // en vez de dejar el valor corrido como pasaba al restar.
        Multipliers.Remove(factor);
        RecomputeMultiplier();
    }

    private void RecomputeMultiplier()
    {
        float product = 1f;
        for (int i = 0; i < Multipliers.Count; i++) product *= Multipliers[i];
        MultiplicativeModifier = product;
    }
}
