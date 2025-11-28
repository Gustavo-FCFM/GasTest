using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UI_AbilitySlot : MonoBehaviour
{
    [Header("UI References")]
    public Image iconImage;       // El icono de la habilidad (fijo)
    public Image cooldownOverlay; // Imagen negra con alpha, Type: Filled (Radial 360)
    public TextMeshProUGUI cooldownText; // Texto numérico (3.5s)

    private GameplayAbility assignedAbility;
    private AbilitySystemComponent ownerASC;

    // Inicializamos el slot con una habilidad específica
    public void Setup(GameplayAbility ability, AbilitySystemComponent asc)
    {
        assignedAbility = ability;
        ownerASC = asc;

        if (assignedAbility != null)
        {
            iconImage.sprite = assignedAbility.AbilityIcon; // Asume que agregaste un campo 'Sprite' a tu GA
            iconImage.enabled = true;
        }
        else
        {
            iconImage.enabled = false; // Si no hay habilidad, ocultamos
        }

        cooldownOverlay.fillAmount = 0;
        cooldownText.text = "";
    }

    void Update()
    {
        if (assignedAbility == null || ownerASC == null) return;

        // PREGUNTA DE ARQUITECTURA:
        // Necesitamos saber si la habilidad está en Cooldown.
        // Usualmente el ASC maneja esto revisando si tienes el Tag de Cooldown.
        
        float timeRemaining = 0f;
        float totalDuration = 1f;
        
        // Aquí asumimos que tu ASC o la Habilidad tienen un método para consultar esto.
        // Si no lo tienes, lo crearemos abajo.
        bool isOnCooldown = ownerASC.GetCooldownStatus(assignedAbility, out timeRemaining, out totalDuration);

        if (isOnCooldown)
        {
            cooldownOverlay.fillAmount = timeRemaining / totalDuration;
            // Mostrar solo 1 decimal
            cooldownText.text = timeRemaining.ToString("F1"); 
        }
        else
        {
            cooldownOverlay.fillAmount = 0;
            cooldownText.text = "";
        }
    }
}