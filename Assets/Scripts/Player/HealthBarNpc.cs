using UnityEngine;
using UnityEngine.UI;

public class HealthBarNPC : MonoBehaviour
{
    public AbilitySystemComponent targetASC;
    public Slider healthSlider; // El componente Slider
    public Transform cameraTransform; // La cámara del jugador (para rotación)

    void Start()
    {
        // En un juego real, la barra podría auto-encontrarse o ser asignada por el NPC.
        healthSlider.maxValue = targetASC.GetAttributeValue(EAttributeType.MaxHealth);
    }

    void Update()
    {
        if (targetASC == null || healthSlider == null) return;

        // 1. Actualizar el valor de la barra
        healthSlider.value = targetASC.GetAttributeValue(EAttributeType.Health);

        // 2. Rotar la barra para que siempre mire a la cámara
        if (cameraTransform != null)
        {
            // Hace que la barra mire directamente hacia la cámara
            transform.LookAt(transform.position + cameraTransform.forward);
        }
    }
}