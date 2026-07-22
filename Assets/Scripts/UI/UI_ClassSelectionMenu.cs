using UnityEngine;
using System.Collections.Generic;

// ============================================================
// UI_ClassSelectionMenu
//
// Menú de selección de subclase con tarjetas (ícono/nombre/descripción).
// Se ata al jugador DUEÑO local vía InitializeMenu() (que llama
// PlayerController al spawnear la cámara) — cada pantalla maneja la
// selección de SU propio jugador, así funciona igual en el host y en los
// clientes que se unen. Se abre solo al llegar a nivel máximo
// (OnMaxLevelReached) y la subclase se elige SOLO con las teclas 1/2/3
// (cada tarjeta muestra su número). EquipCharacterClass ya sincroniza el
// cambio por red (ServerSetClass).
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

    private PlayerController targetPlayer;
    private AbilitySystemComponent playerASC;

    // Tarjetas instanciadas, en paralelo a AvailableClasses (para resaltar la
    // selección al navegar con control), y el índice resaltado (-1 = ninguno).
    private readonly List<UI_ClassCard> _cards = new List<UI_ClassCard>();
    private int _selectedIndex = -1;

    void Awake()
    {
        if (MenuContainer != null) MenuContainer.SetActive(false);
    }

    void OnDestroy()
    {
        if (playerASC != null) playerASC.OnMaxLevelReached -= OpenMenu;
    }

    // Ata el menú al jugador dueño local y se suscribe a su nivel máximo.
    // Solo acepta al dueño: en red hay un PlayerController por jugador y cada
    // pantalla maneja el suyo.
    public void InitializeMenu(PlayerController player)
    {
        if (player == null) return;
        if (player.IsSpawned && !player.IsOwner) return; // en red, solo el dueño local

        if (playerASC != null) playerASC.OnMaxLevelReached -= OpenMenu;

        targetPlayer = player;
        playerASC = player.GetComponent<AbilitySystemComponent>();
        if (playerASC != null) playerASC.OnMaxLevelReached += OpenMenu;
    }

    // Con el menú abierto, se elige la subclase con teclas 1/2/3, con click, o
    // con el control (stick/d-pad para moverse + Submit para confirmar).
    void Update()
    {
        if (MenuContainer == null || !MenuContainer.activeSelf) return;

        PlayerInputProvider input = PlayerInputProvider.Local;

        // Teclas 1/2/3 solo en instancias de teclado (no cruzar el teclado
        // compartido con la instancia de mando).
        if (input == null || input.UsesKeyboardMouse)
        {
            if      (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) { TrySelectByIndex(0); return; }
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) { TrySelectByIndex(1); return; }
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) { TrySelectByIndex(2); return; }
        }

        if (input == null) return;

        int step = input.ReadNavigateStep();
        if (step != 0) MoveSelection(step);

        if (input.Submit != null && input.Submit.WasPressedThisFrame() &&
            _selectedIndex >= 0 && _selectedIndex < _cards.Count && _cards[_selectedIndex] != null)
        {
            ConfirmSelection(_cards[_selectedIndex].AssignedClass);
        }
    }

    // Mueve el resaltado de tarjeta al navegar con control (envuelve los extremos).
    private void MoveSelection(int step)
    {
        if (_cards.Count == 0) return;

        if (_selectedIndex >= 0 && _selectedIndex < _cards.Count && _cards[_selectedIndex] != null)
            _cards[_selectedIndex].SetHighlighted(false);

        _selectedIndex = ((_selectedIndex + step) % _cards.Count + _cards.Count) % _cards.Count;

        if (_cards[_selectedIndex] != null) _cards[_selectedIndex].SetHighlighted(true);
    }

    // Abre el menú y construye las tarjetas. Lo dispara OnMaxLevelReached.
    private void OpenMenu()
    {
        if (MenuContainer == null) return;
        BuildMenu();
        MenuContainer.SetActive(true);

        // Modo UI para navegar/confirmar con control sin disparar acciones de
        // juego, y liberar el cursor para poder también clickear una tarjeta.
        if (PlayerInputProvider.Local != null) PlayerInputProvider.Local.SetUIMode(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        // Arrancar con la primera tarjeta resaltada.
        if (_cards.Count > 0) MoveSelection(1);
    }

    // Genera una tarjeta por subclase disponible, pasándole su número de
    // tecla (1, 2, 3...) para que lo muestre.
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
        _cards.Clear();
        _selectedIndex = -1;

        for (int i = 0; i < AvailableClasses.Count; i++)
        {
            CharacterClassDefinition classDef = AvailableClasses[i];
            if (classDef == null) continue;

            GameObject cardObj = Instantiate(ClassCardPrefab, CardsParent);
            UI_ClassCard cardUI = cardObj.GetComponent<UI_ClassCard>();
            if (cardUI != null)
            {
                cardUI.SetupCard(classDef, i + 1);       // i+1 = la tecla que la selecciona
                cardUI.OnCardClicked = ConfirmSelection; // clic → elegir esta subclase
            }
            _cards.Add(cardUI); // en paralelo a AvailableClasses
        }
    }

    // Selección por tecla 1/2/3: elige la subclase de ese índice.
    private void TrySelectByIndex(int index)
    {
        if (index >= 0 && index < AvailableClasses.Count)
            ConfirmSelection(AvailableClasses[index]);
    }

    // Equipa la subclase elegida (sincroniza por red) y cierra el menú.
    private void ConfirmSelection(CharacterClassDefinition selectedClass)
    {
        if (targetPlayer == null || selectedClass == null) return;

        targetPlayer.EquipCharacterClass(selectedClass);
        if (MenuContainer != null) MenuContainer.SetActive(false);

        // Devolver el input a modo juego y re-bloquear el cursor.
        if (PlayerInputProvider.Local != null) PlayerInputProvider.Local.SetUIMode(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }
}
