using UnityEngine;
using UnityEngine.UI;

// ============================================================
// AbilityCooldownUI
//
// Overlay simple que oscurece una imagen según cuánto cooldown le
// queda a UN tag puntual (no a un slot completo de habilidad).
// Pensado para indicadores sueltos fuera del HUD principal — para
// los slots reales de habilidades usar UI_AbilitySlot/UI_UltimateSlot,
// que sí funcionan en red.
// ============================================================
public class AbilityCooldownUI : MonoBehaviour
{
    // ASC del personaje a observar.
    public AbilitySystemComponent targetASC;

    // Qué cooldown mostrar — debe coincidir con el GrantedTag del
    // CooldownEffect de la habilidad que se quiere representar.
    public EGameplayTag CooldownTag = EGameplayTag.Ability_Cooldown_Melee;

    // Imagen cuyo fillAmount se usa como relleno del cooldown.
    private Image overlayImage;

    // Cachea la Image de este mismo GameObject.
    void Awake()
    {
        overlayImage = GetComponent<Image>();
    }

    // Cada frame, refleja en el fillAmount cuánto cooldown falta (0 a 1).
    void Update()
    {
        if (targetASC == null || overlayImage == null) return;

        float cooldownFraction = targetASC.GetCooldownRemainingNormalized(CooldownTag);
        overlayImage.fillAmount = cooldownFraction;
    }
}
