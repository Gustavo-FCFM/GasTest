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

    // Con el menú abierto, se elige la subclase SOLO con 1/2/3.
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

        for (int i = 0; i < AvailableClasses.Count; i++)
        {
            CharacterClassDefinition classDef = AvailableClasses[i];
            if (classDef == null) continue;

            GameObject cardObj = Instantiate(ClassCardPrefab, CardsParent);
            UI_ClassCard cardUI = cardObj.GetComponent<UI_ClassCard>();
            if (cardUI != null) cardUI.SetupCard(classDef, i + 1); // i+1 = la tecla que la selecciona
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
    }
}
