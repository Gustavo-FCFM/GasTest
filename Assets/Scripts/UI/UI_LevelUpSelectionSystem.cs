using UnityEngine;
using TMPro;

// ============================================================
// LevelUpSelectionSystem
//
// Muestra un panel de selección de subclase cuando el jugador llega
// al nivel máximo (OnMaxLevelReached), y aplica la subclase elegida
// con las teclas 1/2/3. Es un sistema de un solo jugador local: no
// filtra por dueño de red, así que en multijugador puede engancharse
// al primer objeto con tag "Player" que encuentre.
// ============================================================
public class LevelUpSelectionSystem : MonoBehaviour
{
    [Header("Referencias")]
    // Jugador a observar. Si se deja vacío, se busca automáticamente por
    // tag "Player" en Start().
    public PlayerController player;
    // Panel/texto que se muestra mientras está activo el modo selección.
    public GameObject selectionVisuals;
    // Texto opcional donde se listan las subclases disponibles.
    public TextMeshProUGUI optionsText;

    private AbilitySystemComponent playerASC;
    // True mientras se están esperando las teclas 1/2/3 para elegir subclase.
    private bool isSelectionActive = false;

    // Busca al jugador si no está asignado y se suscribe a su evento de
    // nivel máximo.
    void Start()
    {
        if (selectionVisuals) selectionVisuals.SetActive(false);

        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.GetComponent<PlayerController>();
        }

        if (player != null)
        {
            playerASC = player.GetComponent<AbilitySystemComponent>();
            if (playerASC != null)
            {
                playerASC.OnMaxLevelReached += EnableSelectionMode;
            }
        }
    }

    void OnDestroy()
    {
        if (playerASC != null) playerASC.OnMaxLevelReached -= EnableSelectionMode;
    }

    // Mientras el modo selección está activo, revisa las teclas 1/2/3
    // para elegir subclase.
    void Update()
    {
        if (!isSelectionActive || player == null) return;

        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            TrySelectSubclass(0);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            TrySelectSubclass(1);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            TrySelectSubclass(2);
        }
    }

    // Se llama al llegar a nivel máximo (OnMaxLevelReached): muestra el
    // panel y lista las subclases disponibles de la clase actual.
    private void EnableSelectionMode()
    {
        isSelectionActive = true;
        if (selectionVisuals) selectionVisuals.SetActive(true);

        if (optionsText != null && player.CurrentClassDef != null)
        {
            string text = "¡NIVEL MÁXIMO!\n";
            var subs = player.CurrentClassDef.AvailableSubclasses;
            for(int i=0; i<subs.Count; i++)
            {
                text += $"[{i+1}] {subs[i].ClassName}\n";
            }
            optionsText.text = text;
        }
    }

    // Valida el índice elegido y, si existe esa subclase, la equipa y
    // cierra el modo selección.
    private void TrySelectSubclass(int index)
    {
        var currentClass = player.CurrentClassDef;

        if (currentClass != null && index < currentClass.AvailableSubclasses.Count)
        {
            CharacterClassDefinition chosenClass = currentClass.AvailableSubclasses[index];

            player.EquipCharacterClass(chosenClass);

            isSelectionActive = false;
            if (selectionVisuals) selectionVisuals.SetActive(false);

            Debug.Log($"Evolución completada: {chosenClass.ClassName}");
        }
    }
}
