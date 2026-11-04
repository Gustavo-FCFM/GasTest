using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Una tarjeta de habilidad de la ventana de ayuda (UI_HelpScreen): una por botón.
public class UI_HelpAbilityCard : MonoBehaviour
{
    [Label("Ícono")]
    public Image Icon;

    [Tooltip("El botón que la usa (Q, RB…).")]
    [Label("Botón")]
    public TMP_Text Glyph;

    [Label("Nombre")]
    public TMP_Text AbilityName;

    [Tooltip("Cuánto daña y cuánto cura.")]
    [Label("Números")]
    public TMP_Text Numbers;

    [Label("Descripción")]
    public TMP_Text Description;

    [Tooltip("La separación entre los números y la descripción, que va justo debajo de ellos.")]
    [Label("Separación de la descripción (px)")]
    public float DescriptionGap = 10f;

    public void Show(string glyph, GameplayAbility ability, AbilitySystemComponent asc)
    {
        if (Glyph != null) Glyph.text = glyph;

        if (Icon != null)
        {
            Icon.sprite  = ability != null ? ability.AbilityIcon : null;
            Icon.enabled = Icon.sprite != null;
        }

        if (AbilityName != null) AbilityName.text = ability != null ? ability.DisplayName : Loc.T("help.locked_title");
        if (Numbers != null) Numbers.text = AbilityHelpNumbers.Describe(ability, asc);
        if (Description != null) Description.text = ability != null ? ability.DisplayDescription : Loc.T("help.locked");

        PlaceDescription();
    }

    // Los números pueden ocupar dos líneas o ninguna: la descripción arranca donde terminan.
    private void PlaceDescription()
    {
        if (Numbers == null || Description == null) return;

        RectTransform numbers = Numbers.rectTransform;
        RectTransform description = Description.rectTransform;

        float height = string.IsNullOrEmpty(Numbers.text) ? 0f : Numbers.GetPreferredValues(Numbers.text, numbers.rect.width, 0f).y;
        numbers.sizeDelta = new Vector2(numbers.sizeDelta.x, height);

        float bottom = description.anchoredPosition.y - description.rect.height;
        float top = numbers.anchoredPosition.y - height - (height > 0f ? DescriptionGap : 0f);
        description.anchoredPosition = new Vector2(description.anchoredPosition.x, top);
        description.sizeDelta = new Vector2(description.sizeDelta.x, top - bottom);
    }
}
