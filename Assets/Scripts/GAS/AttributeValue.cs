using UnityEngine;

public class AttributeValue
{
        public float BaseValue;      // Valor inicial o base (ej: HP Máximo base)
        public float CurrentValue;   // Valor actual (ej: 50/100 de HP)
        // Valores temporales que serán sumados/multiplicados por los efectos activos
        public float AdditiveModifier;    // Suma de todos los modificadores de tipo 'Add'
        public float MultiplicativeModifier; // Producto de todos los modificadores de tipo 'Multiply'
        public AttributeValue(float baseVal)
        {
            BaseValue = baseVal;
            CurrentValue = baseVal;
            AdditiveModifier = 0f;
            MultiplicativeModifier = 1f; // Comienza en 1.0 para la multiplicación
        }
}

