using UnityEngine;
using UnityEngine.UI;

public class AbilityCooldownUI : MonoBehaviour
{
    public AbilitySystemComponent targetASC; // El ASC del jugador
    public EGameplayTag CooldownTag = EGameplayTag.Ability_Cooldown_Melee; // El tag que bloquea la habilidad
    
    private Image overlayImage; // La imagen del relleno oscuro

    void Awake()
    {
        overlayImage = GetComponent<Image>();
    }

    void Update()
    {
        if (targetASC == null || overlayImage == null) return;

        // Obtenemos el valor normalizado (0 a 1) directamente del ASC
        float cooldownFraction = targetASC.GetCooldownRemainingNormalized(CooldownTag);

        // Actualizamos el relleno de la imagen
        overlayImage.fillAmount = cooldownFraction;
    }
}