using UnityEngine;
using System.Collections.Generic;

// ============================================================
// AttributeSetDefinition
//
// Asset de configuración con los valores iniciales de los
// atributos de un rol/clase (ej: Vida=100, Ataque=10). Lo lee
// AbilitySystemComponent.InitializeAttributes() al equipar una
// clase, para poblar el diccionario de atributos desde cero.
// ============================================================
[CreateAssetMenu(fileName = "ASDef_New", menuName = "GAS/Attribute Set Definition")]
public class AttributeSetDefinition : ScriptableObject
{
    // Un par (atributo, valor base) dentro de la lista de abajo.
    [System.Serializable]
    public class BaseAttribute
    {
        public EAttributeType Attribute;
        public float BaseValue;
    }

    // Lista de atributos y sus valores de partida para esta clase. Agregar
    // o quitar entradas acá cambia directamente con qué stats arranca el
    // personaje al equipar esta clase.
    [Section("Atributos base")]
    [Tooltip("Con qué valor arranca cada atributo al equipar la clase (nivel 1). Lo que suben por " +
             "nivel está en la clase (Stats y progresión).")]
    public List<BaseAttribute> InitialAttributes;
}
