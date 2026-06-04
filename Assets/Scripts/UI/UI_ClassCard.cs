using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // Necesario para detectar selecciones
using TMPro;

// Agregamos las interfaces de selección y puntero
public class UI_ClassCard : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Referencias Visuales")]
    public Image ClassIconImage;
    public TextMeshProUGUI ClassNameText;
    public TextMeshProUGUI DescriptionText;

    [HideInInspector] public CharacterClassDefinition AssignedClass;
    
    private Vector3 originalScale;

    void Awake()
    {
        originalScale = transform.localScale;
    }

    public void SetupCard(CharacterClassDefinition classDef)
    {
        AssignedClass = classDef;

        if (ClassIconImage != null) ClassIconImage.sprite = classDef.ClassIcon;
        if (ClassNameText != null) ClassNameText.text = classDef.ClassName;
        if (DescriptionText != null) DescriptionText.text = classDef.Description;
    }

    // --- LÓGICA DE FEEDBACK VISUAL ---
    public void OnSelect(BaseEventData eventData) => HighlightCard();
    public void OnDeselect(BaseEventData eventData) => UnhighlightCard();
    public void OnPointerEnter(PointerEventData eventData) => HighlightCard();
    public void OnPointerExit(PointerEventData eventData) => UnhighlightCard();

    private void HighlightCard()
    {
        // La tarjeta crece un 15% al seleccionarla
        transform.localScale = originalScale * 1.15f; 
    }

    private void UnhighlightCard()
    {
        // Vuelve a su tamaño normal
        transform.localScale = originalScale;
    }
}