using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ASDef_Guardia", menuName = "GAS/Attribute Set Definition")]
public class AttributeSetDefinition : ScriptableObject
{
    // Usamos una clase serializable simple para la configuración
    [System.Serializable]
    public class BaseAttribute
    {
        public EAttributeType Attribute;
        public float BaseValue;
    }
    
    [Header("Atributos Base para esta Clase")]
    public List<BaseAttribute> InitialAttributes;

    // Métodos para obtener el valor inicial, si fuera necesario.
}