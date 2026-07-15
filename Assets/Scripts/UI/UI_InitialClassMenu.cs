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

    // Con el menú abierto, permite elegir con las teclas 1/2/3... como
    // alternativa al click (útil si el mouse/EventSystem falla).
    void Update()
    {
        if (!_open) return;

        for (int i = 0; i < _built.Count && i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
            {
                ConfirmSelection(_built[i]);
                return;
            }
        }
    }

    // Genera una tarjeta por clase base y le conecta el callback de click.
    private void BuildMenu()
    {
        foreach (Transform child in CardsParent) Destroy(child.gameObject);
        _built.Clear();

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
