using UnityEngine;
using TMPro; // Si usas TextMeshPro para avisar en pantalla

public class LevelUpSelectionSystem : MonoBehaviour
{
    [Header("Referencias")]
    public PlayerController player;
    public GameObject selectionVisuals; // Un texto simple en pantalla: "Pulsa 1 para Berserker"
    public TextMeshProUGUI optionsText; // Opcional: para listar las clases dinámicamente

    private AbilitySystemComponent playerASC;
    private bool isSelectionActive = false;

    void Start()
    {
        if (selectionVisuals) selectionVisuals.SetActive(false);

        // Buscar al jugador si no está asignado
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

    void Update()
    {
        // Solo escuchamos teclas si estamos en modo selección
        if (!isSelectionActive || player == null) return;

        // Tecla 1 -> Subclase índice 0
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            TrySelectSubclass(0);
        }
        // Tecla 2 -> Subclase índice 1 (si existe)
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            TrySelectSubclass(1);
        }
    }

    private void EnableSelectionMode()
    {
        isSelectionActive = true;
        if (selectionVisuals) selectionVisuals.SetActive(true);
        
        // Opcional: Mostrar nombres en pantalla
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

    private void TrySelectSubclass(int index)
    {
        var currentClass = player.CurrentClassDef;
        
        // Validar que exista la subclase
        if (currentClass != null && index < currentClass.AvailableSubclasses.Count)
        {
            CharacterClassDefinition chosenClass = currentClass.AvailableSubclasses[index];
            
            // 1. Equipar la nueva clase (Berserker)
            // Esto automáticamente cargará sus stats base (que deben ser altos)
            player.EquipCharacterClass(chosenClass);

            // 2. Apagar el sistema de selección
            isSelectionActive = false;
            if (selectionVisuals) selectionVisuals.SetActive(false);

            Debug.Log($"Evolución completada: {chosenClass.ClassName}");
        }
    }
}