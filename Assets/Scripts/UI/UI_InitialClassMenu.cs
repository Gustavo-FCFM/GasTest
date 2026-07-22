using UnityEngine;
using System.Collections.Generic;
using UnityEngine.EventSystems;

// ============================================================
// UI_InitialClassMenu
//
// Menú de selección de clase que aparece al CONECTARSE: muestra una tarjeta
// (UI_ClassCard) por cada clase base del jugador (PlayerController.MainBaseClasses).
// Se elige CLICKEANDO la tarjeta o con las teclas 1/2/3... (cada tarjeta muestra
// su número). Mientras está abierto, bloquea el input del jugador y libera el
// cursor; al elegir, equipa la clase (EquipCharacterClass ya sincroniza por red)
// y devuelve el control.
//
// Se ata al jugador DUEÑO local vía InitializeMenu() (que llama PlayerController
// al spawnear la cámara), así cada pantalla maneja la selección de SU jugador —
// funciona igual en el host y en los clientes que se unen. Vive en el mismo
// prefab de cámara que el menú de subclase (UI_ClassSelectionMenu).
// ============================================================
public class UI_InitialClassMenu : MonoBehaviour
{
    [Header("Configuración UI")]
    public GameObject MenuContainer;   // Panel raíz del menú (se prende/apaga)
    public Transform  CardsParent;     // Contenedor donde se instancian las tarjetas
    public GameObject ClassCardPrefab; // Prefab de UI_ClassCard

    private PlayerController targetPlayer;
    private bool _open;

    // Clases en el mismo orden en que se muestran las tarjetas — el índice acá
    // es el que mapean las teclas 1/2/3...
    private readonly List<CharacterClassDefinition> _built = new List<CharacterClassDefinition>();
    // Tarjetas instanciadas, en paralelo a _built (misma posición = misma clase).
    // Sirven para resaltar la selección al navegar con el control.
    private readonly List<UI_ClassCard> _cards = new List<UI_ClassCard>();
    // Índice resaltado al navegar con control (-1 = nada resaltado todavía).
    private int _selectedIndex = -1;

    void Awake()
    {
        if (MenuContainer != null) MenuContainer.SetActive(false);
    }

    // La llama PlayerController al spawnear (solo el dueño local). Arma las
    // tarjetas de las clases base y abre el menú. Si no hay clases válidas, no
    // hace nada (para no dejar al jugador bloqueado sin nada que elegir).
    public void InitializeMenu(PlayerController player)
    {
        if (player == null) return;
        if (player.IsSpawned && !player.IsOwner) return; // en red, solo el dueño local

        targetPlayer = player;

        if (MenuContainer == null || CardsParent == null || ClassCardPrefab == null) return;
        if (player.MainBaseClasses == null || player.MainBaseClasses.Length == 0) return;

        BuildMenu();
        OpenMenu();
    }

    // Con el menú abierto, permite elegir de tres formas: click (EventSystem),
    // teclas 1/2/3 (solo en instancias de teclado), y control (mapa UI del
    // jugador local: stick/d-pad para moverse + Submit para confirmar).
    void Update()
    {
        if (!_open) return;

        PlayerInputProvider input = PlayerInputProvider.Local;

        // Teclas 1..9 (alternativa directa) — solo si esta instancia usa teclado,
        // para no cruzar el teclado compartido con la instancia de mando.
        if (input == null || input.UsesKeyboardMouse)
        {
            for (int i = 0; i < _built.Count && i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                {
                    ConfirmSelection(_built[i]);
                    return;
                }
            }
        }

        // Navegación con control.
        if (input == null) return;

        int step = input.ReadNavigateStep();
        if (step != 0) MoveSelection(step);

        if (input.Submit != null && input.Submit.WasPressedThisFrame() &&
            _selectedIndex >= 0 && _selectedIndex < _built.Count)
        {
            ConfirmSelection(_built[_selectedIndex]);
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

    // Genera una tarjeta por clase base y le conecta el callback de click.
    private void BuildMenu()
    {
        foreach (Transform child in CardsParent) Destroy(child.gameObject);
        _built.Clear();
        _cards.Clear();
        _selectedIndex = -1;

        CharacterClassDefinition[] classes = targetPlayer.MainBaseClasses;
        for (int i = 0; i < classes.Length; i++)
        {
            CharacterClassDefinition classDef = classes[i];
            if (classDef == null) continue;

            GameObject cardObj = Instantiate(ClassCardPrefab, CardsParent);
            UI_ClassCard cardUI = cardObj.GetComponent<UI_ClassCard>();
            if (cardUI != null)
            {
                cardUI.SetupCard(classDef, _built.Count + 1); // número = tecla que la selecciona
                cardUI.OnCardClicked = ConfirmSelection;      // clic → elegir esta clase
            }
            _built.Add(classDef);
            _cards.Add(cardUI); // en paralelo a _built (misma posición = misma clase)
        }
    }

    // Abre el menú: asegura un EventSystem (sin él no hay click ni hover),
    // bloquea el input del jugador y libera el cursor para clickear.
    private void OpenMenu()
    {
        EnsureEventSystem();

        MenuContainer.SetActive(true);
        _open = true;

        if (targetPlayer != null) targetPlayer.SetInputLocked(true);

        // Pasar el input del jugador a modo UI (apaga el mapa Player, enciende el
        // UI) para navegar con control sin disparar acciones de juego.
        if (PlayerInputProvider.Local != null) PlayerInputProvider.Local.SetUIMode(true);

        // Arrancar con la primera tarjeta resaltada, para que el control tenga
        // un punto de partida visible.
        if (_cards.Count > 0) MoveSelection(1);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    // Equipa la clase elegida (se sincroniza por red vía EquipCharacterClass) y
    // cierra el menú devolviendo el control al jugador.
    private void ConfirmSelection(CharacterClassDefinition selectedClass)
    {
        if (!_open || targetPlayer == null || selectedClass == null) return;

        targetPlayer.EquipCharacterClass(selectedClass);
        CloseMenu();
    }

    // Cierra el menú: restaura el input y vuelve a bloquear/ocultar el cursor.
    private void CloseMenu()
    {
        if (MenuContainer != null) MenuContainer.SetActive(false);
        _open = false;

        if (targetPlayer != null) targetPlayer.SetInputLocked(false);

        // Devolver el input a modo juego.
        if (PlayerInputProvider.Local != null) PlayerInputProvider.Local.SetUIMode(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    // Crea un EventSystem si la escena no tiene uno. Sin EventSystem, NINGÚN
    // evento de puntero (click, hover, select) funciona en toda la UI. Usa el
    // StandaloneInputModule (input legacy), acorde al resto del proyecto que
    // lee con Input.GetAxis/GetKeyDown.
    private void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }
}
