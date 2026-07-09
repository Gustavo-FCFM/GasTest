using UnityEngine;
using TMPro;

// ============================================================
// LevelUpSelectionSystem
//
// Muestra un panel de selección de subclase cuando el jugador dueño
// llega al nivel máximo (OnMaxLevelReached), y aplica la subclase
// elegida con las teclas 1/2/3. En multijugador se ata al jugador
// LOCAL vía Initialize(), que llama PlayerController al spawnear
// (mismo patrón que UI_PlayerHUD.InitializeHUD) — no por tag, porque
// en Start() los jugadores todavía no existen y habría varios.
// ============================================================
public class LevelUpSelectionSystem : MonoBehaviour
{
    [Header("Referencias")]
    // Jugador dueño local. Lo asigna Initialize() en runtime; solo dejalo
    // puesto a mano en escenas de un solo jugador sin red.
    public PlayerController player;
    // Panel/texto que se muestra mientras está activo el modo selección.
    public GameObject selectionVisuals;
    // Texto opcional donde se listan las subclases disponibles.
    public TextMeshProUGUI optionsText;

    private AbilitySystemComponent playerASC;
    // True mientras se están esperando las teclas 1/2/3 para elegir subclase.
    private bool isSelectionActive = false;

    // Arranca oculto. Si hay un player pre-asignado (escena sin red), se
    // engancha directo; si no, espera a que PlayerController llame Initialize().
    void Start()
    {
        if (selectionVisuals) selectionVisuals.SetActive(false);
        if (player != null) Initialize(player);
    }

    void OnDestroy()
    {
        if (playerASC != null) playerASC.OnMaxLevelReached -= EnableSelectionMode;
    }

    // Ata este sistema al jugador dado y se suscribe a su nivel máximo. La
    // llama PlayerController (para el dueño local) al spawnear. Solo acepta
    // al dueño: en multijugador hay un PlayerController por jugador y cada
    // pantalla maneja la selección de SU propio jugador.
    public void Initialize(PlayerController ownerPlayer)
    {
        if (ownerPlayer == null) return;
        if (ownerPlayer.IsSpawned && !ownerPlayer.IsOwner) return; // en red, solo el dueño local

        if (playerASC != null) playerASC.OnMaxLevelReached -= EnableSelectionMode;

        player = ownerPlayer;
        playerASC = player.GetComponent<AbilitySystemComponent>();
        if (playerASC != null) playerASC.OnMaxLevelReached += EnableSelectionMode;
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
