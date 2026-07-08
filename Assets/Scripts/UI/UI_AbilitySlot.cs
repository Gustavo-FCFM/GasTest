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
    private NetworkAbilitySystemComponent ownerNetASC;
    private EAbilityInput slotInput;

    // Inicializamos el slot con una habilidad específica
    public void Setup(GameplayAbility ability, AbilitySystemComponent asc, NetworkAbilitySystemComponent netAsc, EAbilityInput slot)
    {
        assignedAbility = ability;
        ownerASC = asc;
        ownerNetASC = netAsc;
        slotInput = slot;

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

        float timeRemaining;
        float totalDuration;
        bool  isOnCooldown;

        // ownerASC.ActiveEffects solo existe de verdad en la copia del
        // servidor (host). Si hay NetworkAbilitySystemComponent, leemos el
        // cooldown sincronizado en vez del ASC local — así funciona igual
        // para el host y para un cliente remoto.
        if (ownerNetASC != null)
            isOnCooldown = ownerNetASC.TryGetNetCooldown(slotInput, out timeRemaining, out totalDuration);
        else
            isOnCooldown = ownerASC.GetCooldownStatus(assignedAbility, out timeRemaining, out totalDuration);

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