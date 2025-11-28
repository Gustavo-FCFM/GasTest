using UnityEngine;

[System.Serializable]
public class Modifier
{
    [Header("Configuración Básica")]
    // CAMBIO: Usamos el Enum en lugar de string
    public EAttributeType Attribute; 
    
    public enum EModificationType { Add, Multiply, Override }
    public EModificationType Type;

    [Header("Valor Fijo")]
    public float Magnitude;      

    [Header("Escalado de Atributos (Scaling)")]
    public bool UseAttributeScaling; // ¿Escalar con stat del atacante?
    public EAttributeType SourceAttribute; // ¿Qué stat usa? (Ej: Attack)
    public float AttributeCoefficient = 1.0f; // Multiplicador (Ej: 100% del ataque)
}