using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // Necesario para detectar selecciones
using TMPro;

// Agregamos las interfaces de selección, puntero y CLICK
public class UI_ClassCard : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Referencias Visuales")]
    public Image ClassIconImage;
    public TextMeshProUGUI ClassNameText;
    public TextMeshProUGUI DescriptionText;
    // Opcional: un texto dedicado para el número de tecla (1/2/3). Si lo
    // dejás en None, el número se antepone al nombre para que igual se vea.
    public TextMeshProUGUI NumberText;

    [HideInInspector] public CharacterClassDefinition AssignedClass;

    // Callback que dispara la tarjeta al hacerle CLICK. Lo setea el menú que la
    // construye (ej: UI_InitialClassMenu). Si queda null (ej: el menú de subclase
    // que se maneja por teclas), el click simplemente no hace nada.
    [HideInInspector] public System.Action<CharacterClassDefinition> OnCardClicked;

    private Vector3 originalScale;

    void Awake()
    {
        originalScale = transform.localScale;
    }

    // number = la tecla que selecciona esta tarjeta (1, 2, 3...).
    public void SetupCard(CharacterClassDefinition classDef, int number)
    {
        AssignedClass = classDef;

        if (ClassIconImage != null) ClassIconImage.sprite = classDef.ClassIcon;
        if (DescriptionText != null) DescriptionText.text = classDef.Description;

        // El número: si hay un NumberText dedicado va ahí (nombre queda limpio);
        // si no, se antepone al nombre para que igual se vea.
        if (NumberText != null)
        {
            NumberText.text = number.ToString();
            if (ClassNameText != null) ClassNameText.text = classDef.ClassName;
        }
        else if (ClassNameText != null)
        {
            ClassNameText.text = $"[{number}]  {classDef.ClassName}";
        }
    }

    // --- LÓGICA DE FEEDBACK VISUAL ---
    public void OnSelect(BaseEventData eventData) => HighlightCard();
    public void OnDeselect(BaseEventData eventData) => UnhighlightCard();
    public void OnPointerEnter(PointerEventData eventData) => HighlightCard();
    public void OnPointerExit(PointerEventData eventData) => UnhighlightCard();

    // Click sobre la tarjeta: avisa al menú qué clase se eligió.
    public void OnPointerClick(PointerEventData eventData) => OnCardClicked?.Invoke(AssignedClass);

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