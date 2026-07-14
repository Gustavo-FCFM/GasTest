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

    [Tooltip("Texto con el número de cargas. Solo se muestra en habilidades con cargas (ej: el dash). Opcional.")]
    public TextMeshProUGUI chargesText;  // Número de cargas disponibles

    [Tooltip("Texto con la tecla/botón de la habilidad (Q, E, Shift, LMB...). Se rellena solo según el slot. Opcional.")]
    public TextMeshProUGUI keyText;      // Etiqueta de la tecla asignada

    private GameplayAbility assignedAbility;
    private AbilitySystemComponent ownerASC;
    private NetworkAbilitySystemComponent ownerNetASC;
    private EAbilityInput slotInput;

    // > 1 si la habilidad de este slot usa cargas (ej: dash). 0 = sin cargas.
    private int maxCharges;

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

        // Etiqueta de la tecla según el slot (Q, E, Shift, LMB...).
        if (keyText != null) keyText.text = GetKeyLabel(slot);

        // Solo tratamos el slot como "con cargas" si la habilidad tiene más de 1
        // (una sola carga = cooldown normal, no mostramos número).
        maxCharges = (assignedAbility is IChargedAbility charged && charged.MaxChargeCount > 1)
            ? charged.MaxChargeCount : 0;

        cooldownOverlay.fillAmount = 0;
        cooldownText.text = "";
        if (chargesText != null)
        {
            // Antes del primer uso todavía no hay conteo sincronizado: mostramos
            // el máximo (lleno).
            chargesText.text = maxCharges > 0 ? maxCharges.ToString() : "";
            chargesText.gameObject.SetActive(maxCharges > 0);
        }
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

        UpdateCharges();
    }

    // Muestra el número de cargas disponibles (solo en habilidades con cargas).
    // Lee el conteo sincronizado; si todavía no hay (antes del primer uso), deja
    // el máximo que puso Setup().
    void UpdateCharges()
    {
        if (maxCharges <= 0 || chargesText == null) return;

        int charges = maxCharges;
        if (ownerNetASC != null && ownerNetASC.TryGetNetCharges(slotInput, out int netCharges))
            charges = netCharges;

        chargesText.text = charges.ToString();
    }

    // Etiqueta de tecla/botón según el slot, alineada con los botones que revisa
    // PlayerController.HandleAbilityInput (Fire1/Fire2/Action1-3/Fire3). Si
    // cambiás los bindings en el Input Manager, ajustá estos textos. Público y
    // estático para que UI_UltimateSlot use el mismo mapeo (una sola fuente).
    public static string GetKeyLabel(EAbilityInput slot)
    {
        switch (slot)
        {
            case EAbilityInput.PrimaryAttack:   return "LMB";
            case EAbilityInput.SecondaryAttack: return "RMB";
            case EAbilityInput.Action1:         return "Q";
            case EAbilityInput.Action2:         return "E";
            case EAbilityInput.Action3:         return "R";
            case EAbilityInput.Movement:        return "Shift";
            default:                            return "";
        }
    }
}
