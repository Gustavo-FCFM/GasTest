using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

// ============================================================
// UI_ClassSelectionMenu
//
// Menú de selección de subclase con tarjetas (ícono/nombre/descripción).
// Se ata al jugador DUEÑO local vía InitializeMenu() (que llama
// PlayerController al spawnear la cámara) — cada pantalla maneja la
// selección de SU propio jugador, así funciona igual en el host y en los
// clientes que se unen. Se abre solo al llegar a nivel máximo
// (OnMaxLevelReached) y la subclase se elige con 1/2/3, mouse o gamepad.
// EquipCharacterClass ya sincroniza el cambio por red (ServerSetClass).
// ============================================================
public class UI_ClassSelectionMenu : MonoBehaviour
{
    [Header("Configuración UI")]
    public GameObject MenuContainer;
    public Transform CardsParent;
    public GameObject ClassCardPrefab;

    // Subclases mostradas (en orden). Las llena BuildMenu desde la clase
    // actual del jugador; el índice acá es el que mapean las teclas 1/2/3.
    [HideInInspector] public List<CharacterClassDefinition> AvailableClasses = new List<CharacterClassDefinition>();

    private List<Button> instantiatedButtons = new List<Button>();
    private PlayerController targetPlayer;
    private AbilitySystemComponent playerASC;

    void Awake()
    {
        if (MenuContainer != null) MenuContainer.SetActive(false);
    }

    void OnDestroy()
    {
        if (playerASC != null) playerASC.OnMaxLevelReached -= OpenMenu;
    }

    // Ata el menú al jugador dado y se suscribe a su nivel máximo. La llama
    // PlayerController para el dueño local. Solo acepta al dueño: en red hay un
    // PlayerController por jugador y cada pantalla maneja el suyo.
    public void InitializeMenu(PlayerController player)
    {
        if (player == null) return;
        if (player.IsSpawned && !player.IsOwner) return; // en red, solo el dueño local

        if (playerASC != null) playerASC.OnMaxLevelReached -= OpenMenu;

        targetPlayer = player;
        playerASC = player.GetComponent<AbilitySystemComponent>();
        if (playerASC != null) playerASC.OnMaxLevelReached += OpenMenu;
    }

    // Con el menú abierto, permite elegir con 1/2/3 (el mouse/gamepad ya
    // funciona por el Button de cada tarjeta y la navegación del EventSystem).
    void Update()
    {
        if (MenuContainer == null || !MenuContainer.activeSelf) return;

        if      (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) TrySelectByIndex(0);
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) TrySelectByIndex(1);
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) TrySelectByIndex(2);
    }

    // Abre el menú y construye las tarjetas. Lo dispara OnMaxLevelReached.
    private void OpenMenu()
    {
        if (MenuContainer == null) return;

        BuildMenu();
        MenuContainer.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Preseleccionar la primera tarjeta para que el gamepad tenga foco.
        if (instantiatedButtons.Count > 0 && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(instantiatedButtons[0].gameObject);
        }
    }

    // Genera una tarjeta por subclase disponible de la clase actual.
    private void BuildMenu()
    {
        if (targetPlayer == null || CardsParent == null || ClassCardPrefab == null) return;

        if (targetPlayer.CurrentClassDef != null &&
            targetPlayer.CurrentClassDef.AvailableSubclasses != null &&
            targetPlayer.CurrentClassDef.AvailableSubclasses.Count > 0)
        {
            AvailableClasses = targetPlayer.CurrentClassDef.AvailableSubclasses;
        }

        foreach (Transform child in CardsParent) Destroy(child.gameObject);
        instantiatedButtons.Clear();

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

    // Selección por tecla 1/2/3: elige la subclase de ese índice.
    private void TrySelectByIndex(int index)
    {
        if (index >= 0 && index < AvailableClasses.Count)
            ConfirmSelection(AvailableClasses[index]);
    }

    // Confirma la elección enfocada actualmente (botón "confirmar" del gamepad).
    public void ConfirmCurrentSelectionFromGamepad()
    {
        GameObject selectedObj = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selectedObj == null) return;

        UI_ClassCard selectedCard = selectedObj.GetComponent<UI_ClassCard>();
        if (selectedCard != null) ConfirmSelection(selectedCard.AssignedClass);
    }

    // Equipa la subclase elegida (EquipCharacterClass sincroniza por red) y
    // cierra el menú, devolviendo el cursor al modo juego.
    private void ConfirmSelection(CharacterClassDefinition selectedClass)
    {
        if (targetPlayer == null || selectedClass == null) return;

        targetPlayer.EquipCharacterClass(selectedClass);
        if (MenuContainer != null) MenuContainer.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
