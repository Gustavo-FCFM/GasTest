using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ============================================================
// UI_AbilitySlot
//
// Un ícono de habilidad del HUD (Q, E, LMB, RMB, Shift) con su
// overlay de cooldown. Funciona igual para el host y para un
// cliente remoto: si hay NetworkAbilitySystemComponent disponible,
// lee el cooldown sincronizado en vez del ASC local (que en un
// cliente remoto nunca tiene ActiveEffects poblado).
// ============================================================
public class UI_AbilitySlot : MonoBehaviour
{
    [Header("UI References")]
    public Image iconImage;              // Ícono fijo de la habilidad
    public Image cooldownOverlay;        // Imagen oscura, Type: Filled (Radial 360), tapa el ícono según cooldown
    public TextMeshProUGUI cooldownText; // Texto numérico del tiempo restante (ej: "3.5")

    private GameplayAbility assignedAbility;
    private AbilitySystemComponent ownerASC;
    private NetworkAbilitySystemComponent ownerNetASC;
    private EAbilityInput slotInput;

    // Asocia este slot a una habilidad concreta y configura su ícono.
    // Pasar netAsc null fuerza el modo "lectura local" (solo válido si el
    // dueño no tiene red, ej. un NPC).
    public void Setup(GameplayAbility ability, AbilitySystemComponent asc, NetworkAbilitySystemComponent netAsc, EAbilityInput slot)
    {
        assignedAbility = ability;
        ownerASC = asc;
        ownerNetASC = netAsc;
        slotInput = slot;

        if (assignedAbility != null)
        {
            iconImage.sprite = assignedAbility.AbilityIcon;
            iconImage.enabled = true;
        }
        else
        {
            iconImage.enabled = false;
        }

        cooldownOverlay.fillAmount = 0;
        cooldownText.text = "";
    }

    // Cada frame, actualiza el overlay y el texto según el cooldown
    // restante de la habilidad asignada.
    void Update()
    {
        if (assignedAbility == null || ownerASC == null) return;

        float timeRemaining;
        float totalDuration;
        bool  isOnCooldown;

        if (ownerNetASC != null)
            isOnCooldown = ownerNetASC.TryGetNetCooldown(slotInput, out timeRemaining, out totalDuration);
        else
            isOnCooldown = ownerASC.GetCooldownStatus(assignedAbility, out timeRemaining, out totalDuration);

        if (isOnCooldown)
        {
            cooldownOverlay.fillAmount = timeRemaining / totalDuration;
            cooldownText.text = timeRemaining.ToString("F1");
        }
        else
        {
            cooldownOverlay.fillAmount = 0;
            cooldownText.text = "";
        }
    }
}
