using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UI_UltimateSlot : MonoBehaviour
{
    [Header("UI References")]
    public Image iconBackground; // El icono de la habilidad
    public Image iconFill; // La imagen que se llena (Type: Filled)
    public TextMeshProUGUI percentageText; // El texto "0%" -> "99%" -> "READY"
    public GameObject readyEffects; // Opcional: Partículas o marco brillante al llegar al 100%

    private GameplayAbility assignedAbility;
    private AbilitySystemComponent ownerASC;

    public void Setup(GameplayAbility ability, AbilitySystemComponent asc)
    {
        assignedAbility = ability;
        ownerASC = asc;

        if (assignedAbility != null)
        {
            if(iconBackground) 
            {
                iconBackground.sprite = assignedAbility.AbilityIcon;
                iconBackground.enabled = true;
                iconBackground.color = new Color(0.3F, 0.3F, 0.3F, 1f); // Oscurecido al inicio
            }
            if(iconFill)
            {
                iconFill.sprite = assignedAbility.AbilityIcon;
                iconFill.enabled = true;
                iconFill.type = Image.Type.Filled; // Forzamos que sea tipo relleno
                iconFill.color = Color.white;
            }
            gameObject.SetActive(true);
        }
        else
        {
            gameObject.SetActive(false); // Si no hay Ulti (Nivel 1), nos ocultamos
        }
    }

    void Update()
    {
        if (assignedAbility == null || ownerASC == null) return;

        float timeRemaining;
        float totalDuration;
        
        bool isOnCooldown = ownerASC.GetCooldownStatus(assignedAbility, out timeRemaining, out totalDuration);

        // NOTA: isOnCooldown devuelve TRUE si hay tiempo restante.
        // Pero tu Ultimate empieza en Cooldown (0 carga) y baja el tiempo.
        // Ojo: Si tu sistema de Ulti funciona al revés (Empieza en 0 y sube a 100), la lógica cambia.
        // Asumiré que funciona como un Cooldown normal que reduces golpeando.

        if (isOnCooldown && totalDuration > 0)
        {
            // CALCULO DE CARGA (0% a 99%)
            // Si cooldown es 60 y faltan 60 -> Fill = 0
            // Si cooldown es 60 y faltan 0  -> Fill = 1
            float chargePercent = 1f - (timeRemaining / totalDuration);
            
            // Llenamos el icono de color
            if(iconFill) iconFill.fillAmount = chargePercent;
            
            if(percentageText) percentageText.text = $"{chargePercent * 100:F0}%";

            if(readyEffects) readyEffects.SetActive(false);
        }
        else
        {
            // LISTO (100%)
            // Icono totalmente a color
            if(iconFill) iconFill.fillAmount = 1f;
            
            if(percentageText) percentageText.text = "READY"; // O borrar el texto
            
            if(readyEffects) readyEffects.SetActive(true);
        }
    }
}