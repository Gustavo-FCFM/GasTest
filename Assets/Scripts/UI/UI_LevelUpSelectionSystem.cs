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

    // Subclases disponibles al abrir, y el índice resaltado para navegar con
    // control (se marca con ">" en el texto).
    private System.Collections.Generic.List<CharacterClassDefinition> _subs;
    private int _selectedIndex;

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

        PlayerInputProvider input = PlayerInputProvider.Local;

        // Teclas 1/2/3 solo en instancias de teclado (no cruzar el teclado
        // compartido con la instancia de mando).
        if (input == null || input.UsesKeyboardMouse)
        {
            if      (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) { TrySelectSubclass(0); return; }
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) { TrySelectSubclass(1); return; }
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) { TrySelectSubclass(2); return; }
        }

        // Navegación con control: stick/d-pad mueve el marcador, Submit confirma.
        if (input == null) return;

        int step = input.ReadNavigateStep();
        if (step != 0) MoveSelection(step);

        if (input.Submit != null && input.Submit.WasPressedThisFrame())
            TrySelectSubclass(_selectedIndex);
    }

    // Mueve el marcador de selección al navegar con control y refresca el texto.
    private void MoveSelection(int step)
    {
        if (_subs == null || _subs.Count == 0) return;
        _selectedIndex = ((_selectedIndex + step) % _subs.Count + _subs.Count) % _subs.Count;
        RefreshOptionsText();
    }

    // Redibuja la lista de subclases marcando con ">" la resaltada.
    private void RefreshOptionsText()
    {
        if (optionsText == null || _subs == null) return;

        string text = "¡NIVEL MÁXIMO!\n";
        for (int i = 0; i < _subs.Count; i++)
        {
            string marker = (i == _selectedIndex) ? "> " : "   ";
            text += $"{marker}[{i + 1}] {_subs[i].ClassName}\n";
        }
        optionsText.text = text;
    }

    // Se llama al llegar a nivel máximo (OnMaxLevelReached): muestra el
    // panel y lista las subclases disponibles de la clase actual.
    private void EnableSelectionMode()
    {
        isSelectionActive = true;
        if (selectionVisuals) selectionVisuals.SetActive(true);

        _subs = player.CurrentClassDef != null ? player.CurrentClassDef.AvailableSubclasses : null;
        _selectedIndex = 0;
        RefreshOptionsText();

        // Modo UI para navegar/confirmar con control sin disparar acciones de juego.
        if (PlayerInputProvider.Local != null) PlayerInputProvider.Local.SetUIMode(true);
    }

    // Valida el índice elegido y, si existe esa subclase, la equipa y
    // cierra el modo selección.
    private void TrySelectSubclass(int index)
    {
        var currentClass = player.CurrentClassDef;

        if (currentClass != null && index >= 0 && index < currentClass.AvailableSubclasses.Count)
        {
            CharacterClassDefinition chosenClass = currentClass.AvailableSubclasses[index];

            player.EquipCharacterClass(chosenClass);

            isSelectionActive = false;
            if (selectionVisuals) selectionVisuals.SetActive(false);

            // Devolver el input a modo juego.
            if (PlayerInputProvider.Local != null) PlayerInputProvider.Local.SetUIMode(false);

            Debug.Log($"Evolución completada: {chosenClass.ClassName}");
        }
    }
}
