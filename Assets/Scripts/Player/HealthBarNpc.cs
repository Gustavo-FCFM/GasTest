using UnityEngine;
using UnityEngine.UI;

// ============================================================
// HealthBarNPC
//
// Barra de vida flotante sobre un NPC: sigue el valor de Vida del
// ASC objetivo y rota para mirar siempre a la cámara (efecto
// "billboard"). Lee el ASC directo (sin red) — pensada para NPCs
// locales, no para personajes de otros jugadores.
// ============================================================
public class HealthBarNPC : MonoBehaviour
{
    // A quién le muestra la vida esta barra.
    public AbilitySystemComponent targetASC;
    public Slider healthSlider;
    // Cámara contra la que se orienta la barra (para el efecto billboard).
    public Transform cameraTransform;

    // Configura el máximo del slider según la Vida Máxima del objetivo.
    void Start()
    {
        healthSlider.maxValue = targetASC.GetAttributeValue(EAttributeType.MaxHealth);
    }

    // Actualiza el valor de la barra y la rota para que mire a la cámara.
    void Update()
    {
        if (targetASC == null || healthSlider == null) return;

        healthSlider.value = targetASC.GetAttributeValue(EAttributeType.Health);

        if (cameraTransform != null)
        {
            transform.LookAt(transform.position + cameraTransform.forward);
        }
    }
}
