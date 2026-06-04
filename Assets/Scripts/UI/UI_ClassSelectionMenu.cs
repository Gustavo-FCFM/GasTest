using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class UI_ClassSelectionMenu : MonoBehaviour
{
    [Header("Configuración UI")]
    public GameObject MenuContainer; 
    public Transform CardsParent;    
    public GameObject ClassCardPrefab; 

    // ¡Nueva lista oculta para inyectar las clases base al iniciar!
    [HideInInspector] public List<CharacterClassDefinition> AvailableClasses = new List<CharacterClassDefinition>();

    private List<Button> instantiatedButtons = new List<Button>();
    private PlayerController targetPlayer;

    void Awake()
    {
        // Ya no usamos "Instance" porque en pantalla dividida cada jugador tiene su propia UI local
        if (MenuContainer != null) MenuContainer.SetActive(false);
    }

    public void InitializeMenu(PlayerController player)
    {
        targetPlayer = player;
    }

    private void BuildMenu()
    {
        if (targetPlayer == null) return;

        // Si el jugador YA tiene una clase equipada y esa clase tiene evoluciones, usamos las evoluciones
        if (targetPlayer.CurrentClassDef != null && targetPlayer.CurrentClassDef.AvailableSubclasses != null && targetPlayer.CurrentClassDef.AvailableSubclasses.Count > 0)
        {
            AvailableClasses = targetPlayer.CurrentClassDef.AvailableSubclasses;
        }

        // 1. Limpiamos tarjetas previas
        foreach (Transform child in CardsParent) Destroy(child.gameObject);
        instantiatedButtons.Clear();

        // 2. Generamos las tarjetas según la lista
        foreach (var classDef in AvailableClasses)
        {
            if (classDef == null) continue;

            GameObject cardObj = Instantiate(ClassCardPrefab, CardsParent);
            
            UI_ClassCard cardUI = cardObj.GetComponent<UI_ClassCard>();
            if (cardUI != null) cardUI.SetupCard(classDef);

            Button btn = cardObj.GetComponent<Button>();
            if (btn != null)
            {
                CharacterClassDefinition classToEquip = classDef; 
                btn.onClick.AddListener(() => ConfirmSelection(classToEquip));
                instantiatedButtons.Add(btn);
            }
        }
    }

    public void ToggleMenu()
    {
        if (MenuContainer == null) return;

        bool isOpening = !MenuContainer.activeSelf;
        
        if (isOpening)
        {
            BuildMenu();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        MenuContainer.SetActive(isOpening);

        if (isOpening && instantiatedButtons.Count > 0)
        {
            EventSystem.current.SetSelectedGameObject(null); 
            EventSystem.current.SetSelectedGameObject(instantiatedButtons[0].gameObject);
        }
    }

    public void ConfirmCurrentSelectionFromGamepad()
    {
        GameObject selectedObj = EventSystem.current.currentSelectedGameObject;
        
        if (selectedObj != null)
        {
            UI_ClassCard selectedCard = selectedObj.GetComponent<UI_ClassCard>();
            if (selectedCard != null && targetPlayer != null)
            {
                ConfirmSelection(selectedCard.AssignedClass);
            }
        }
    }

    private void ConfirmSelection(CharacterClassDefinition selectedClass)
    {
        if (targetPlayer != null && selectedClass != null)
        {
            targetPlayer.EquipCharacterClass(selectedClass);
            MenuContainer.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}