using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ============================================================
// UI_UltimateSlot
//
// Ícono central de la ultimate: en vez de mostrar el cooldown
// "vaciándose" como UI_AbilitySlot, lo muestra como una CARGA que
// va llenándose de 0% a 100% ("READY") a medida que el cooldown
// baja. Misma lógica dual host/cliente remoto que UI_AbilitySlot.
// ============================================================
public class UI_UltimateSlot : MonoBehaviour
{
    [Header("UI References")]
    public Image iconBackground;           // Ícono oscurecido de fondo
    public Image iconFill;                 // Ícono que se va llenando de color (Type: Filled)
    public TextMeshProUGUI percentageText; // Texto "0%" → "99%" → "READY"
    public GameObject readyEffects;        // Partículas/marco que se prende al llegar al 100%

    private GameplayAbility assignedAbility;
    private AbilitySystemComponent ownerASC;
    private NetworkAbilitySystemComponent ownerNetASC;
    private EAbilityInput slotInput;

    // Asocia este slot a la ultimate del personaje. Si no tiene ultimate
    // asignada todavía (ej: nivel bajo), se oculta el GameObject entero.
    public void Setup(GameplayAbility ability, AbilitySystemComponent asc, NetworkAbilitySystemComponent netAsc, EAbilityInput slot)
    {
        assignedAbility = ability;
        ownerASC = asc;
        ownerNetASC = netAsc;
        slotInput = slot;

        if (assignedAbility != null)
        {
            if(iconBackground)
            {
                iconBackground.sprite = assignedAbility.AbilityIcon;
                iconBackground.enabled = true;
                iconBackground.color = new Color(0.3F, 0.3F, 0.3F, 1f);
            }
            if(iconFill)
            {
                iconFill.sprite = assignedAbility.AbilityIcon;
                iconFill.enabled = true;
                iconFill.type = Image.Type.Filled;
                iconFill.color = Color.white;
            }
            gameObject.SetActive(true);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    // Cada frame, calcula el porcentaje de carga (inverso del cooldown
    // restante) y actualiza el relleno, el texto y el efecto de "listo".
    void Update()
    {
        if (assignedAbility == null || ownerASC == null) return;

        float timeRemaining;
        float totalDuration;

        bool isOnCooldown = ownerNetASC != null
            ? ownerNetASC.TryGetNetCooldown(slotInput, out timeRemaining, out totalDuration)
            : ownerASC.GetCooldownStatus(assignedAbility, out timeRemaining, out totalDuration);

        if (isOnCooldown && totalDuration > 0)
        {
            // 0% recién activada la ultimate, 100% al terminarse el cooldown
            float chargePercent = 1f - (timeRemaining / totalDuration);

            if(iconFill) iconFill.fillAmount = chargePercent;
            if(percentageText) percentageText.text = $"{chargePercent * 100:F0}%";
            if(readyEffects) readyEffects.SetActive(false);
        }
        else
        {
            // Sin cooldown activo = lista para usar
            if(iconFill) iconFill.fillAmount = 1f;
            if(percentageText) percentageText.text = "READY";
            if(readyEffects) readyEffects.SetActive(true);
        }
    }
}
