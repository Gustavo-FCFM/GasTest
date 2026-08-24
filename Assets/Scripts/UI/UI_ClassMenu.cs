using UnityEngine;
using System.Collections.Generic;
using UnityEngine.EventSystems;

// ============================================================
// UI_ClassMenu
//
// Menú ÚNICO de selección de clase, con dos modos sobre el mismo panel:
//
//   · BaseClasses  (tecla C)  → las clases base del jugador (MainBaseClasses).
//                               Al elegir, la clase arranca en NIVEL 1.
//   · Subclasses   (tecla V)  → las subclases de la clase que tenés puesta AHORA.
//                               Al elegir, CONSERVA el progreso (es una evolución).
//                               También se abre solo al llegar al nivel máximo.
//
// Reemplaza a UI_InitialClassMenu + UI_ClassSelectionMenu, que eran dos paneles
// casi idénticos en el prefab de la cámara. Un solo panel, un solo script.
//
// Se elige de tres formas: clic en la tarjeta, teclas 1/2/3… (solo en instancias
// de teclado, para no cruzar el teclado compartido con la de mando), o control
// (stick/d-pad para moverse + Submit). Mientras está abierto bloquea el input del
// jugador y libera el cursor.
//
// Se ata al jugador DUEÑO local con InitializeMenu(), que llama PlayerController al
// spawnear la cámara — así cada pantalla maneja la selección de SU jugador.
// ============================================================
public class UI_ClassMenu : MonoBehaviour
{
    public enum EMode { BaseClasses, Subclasses }

    [Header("Configuración UI")]
    [Tooltip("Panel raíz del menú (se prende/apaga).")]
    public GameObject MenuContainer;
    [Tooltip("Contenedor donde se instancian las tarjetas.")]
    public Transform  CardsParent;
    [Tooltip("Prefab de UI_ClassCard.")]
    public GameObject ClassCardPrefab;

    [Header("Comportamiento")]
    [Tooltip("Abrir el menú de clases base automáticamente al entrar a la partida.")]
    public bool OpenOnSpawn = true;
    [Tooltip("Abrir el de subclases solo al llegar al nivel máximo (además de la tecla V).")]
    public bool OpenSubclassesOnMaxLevel = true;

    private PlayerController        _player;
    private AbilitySystemComponent  _playerASC;
    private bool _open;
    private EMode _mode;

    // True mientras el menú está abierto. Lo consulta PlayerController para no
    // reabrirlo encima de sí mismo.
    public bool IsOpen => _open;

    // Clases mostradas, en el mismo orden que las tarjetas: el índice acá es el que
    // mapean las teclas 1/2/3 y la navegación con control.
    private readonly List<CharacterClassDefinition> _classes = new List<CharacterClassDefinition>();
    private readonly List<UI_ClassCard> _cards = new List<UI_ClassCard>();
    private int _selectedIndex = -1;

    private void Awake()
    {
        if (MenuContainer != null) MenuContainer.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_playerASC != null) _playerASC.OnMaxLevelReached -= OpenSubclassesFromLevelUp;
    }

    // La llama PlayerController al spawnear (solo el dueño local).
    public void InitializeMenu(PlayerController player)
    {
        if (player == null) return;
        if (player.IsSpawned && !player.IsOwner) return; // en red, solo el dueño local

        if (_playerASC != null) _playerASC.OnMaxLevelReached -= OpenSubclassesFromLevelUp;

        _player    = player;
        _playerASC = player.GetComponent<AbilitySystemComponent>();

        if (_playerASC != null && OpenSubclassesOnMaxLevel)
            _playerASC.OnMaxLevelReached += OpenSubclassesFromLevelUp;

        if (OpenOnSpawn) OpenBaseClasses();
    }

    // =========================================================
    // APERTURA (lo que llama PlayerController con C y V)
    // =========================================================

    // ¿Se puede cambiar de clase base acá y ahora? Sin modo Mercenarios en la escena
    // (una escena de pruebas suelta) siempre se puede: la restricción es una regla de
    // ESE modo, no del menú.
    private bool CanChangeBaseClassHere()
    {
        MercenariesGameMode gm = MercenariesGameMode.Instance;
        if (gm == null || !gm.ClassChangeOnlyInSafeRoom) return true;
        if (_playerASC == null) return true;

        return _playerASC.HasTag(EGameplayTag.Status_SafeZone);
    }

    // Avisa por qué no se abrió. Usa el cartelón del modo si está en la escena; si no,
    // al menos queda en la consola.
    private void WarnClassChangeBlocked()
    {
        UI_MatchAnnouncer announcer = FindFirstObjectByType<UI_MatchAnnouncer>();
        if (announcer != null)
            announcer.Push("Solo podés cambiar de clase dentro de tu base",
                           new Color(1f, 0.75f, 0.3f), 26f);
        else
            Debug.Log("[UI_ClassMenu] Cambio de clase bloqueado: hay que estar en la sala segura del equipo.");
    }

    // Tecla C: elegir entre las clases base. Reinicia el progreso a nivel 1.
    public void OpenBaseClasses() => Open(EMode.BaseClasses);

    // Tecla V: evolucionar a una subclase de la clase actual, conservando progreso.
    public void OpenSubclasses() => Open(EMode.Subclasses);

    private void OpenSubclassesFromLevelUp() => Open(EMode.Subclasses);

    private void Open(EMode mode)
    {
        if (_open || _player == null) return;
        if (MenuContainer == null || CardsParent == null || ClassCardPrefab == null) return;

        // MODO MERCENARIOS: la sala segura de tu base es el ÚNICO lugar donde se puede
        // cambiar de clase. Afuera el menú ni se abre y se avisa por qué, en vez de
        // dejarlo elegir y rechazarlo después (eso se sentiría como un bug).
        //
        // Solo aplica a las clases BASE. La evolución a subclase (tecla V, o la que se
        // abre sola al llegar al nivel máximo) se puede hacer donde sea: es progresión,
        // no un cambio de personaje.
        if (mode == EMode.BaseClasses && !CanChangeBaseClassHere())
        {
            WarnClassChangeBlocked();
            return;
        }

        _mode = mode;
        if (!BuildMenu()) return; // sin clases que mostrar, no bloqueamos al jugador

        EnsureEventSystem();

        MenuContainer.SetActive(true);
        _open = true;

        _player.SetInputLocked(true);

        // Modo UI: apaga el mapa Player y enciende el UI, para navegar con control
        // sin disparar acciones de juego.
        if (PlayerInputProvider.Local != null) PlayerInputProvider.Local.SetUIMode(true);

        // Arrancar con la primera tarjeta resaltada (punto de partida para el control).
        if (_cards.Count > 0) MoveSelection(1);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    // =========================================================
    // CONSTRUCCIÓN
    // =========================================================

    // Arma las tarjetas según el modo. Devuelve false si no hay nada que mostrar
    // (ej. V en una clase que no tiene subclases): así el menú ni se abre y el
    // jugador no queda bloqueado frente a un panel vacío.
    private bool BuildMenu()
    {
        _classes.Clear();
        _cards.Clear();
        _selectedIndex = -1;

        if (_mode == EMode.BaseClasses)
        {
            if (_player.MainBaseClasses != null)
                foreach (var c in _player.MainBaseClasses)
                    if (c != null) _classes.Add(c);
        }
        else
        {
            var subs = _player.CurrentClassDef != null ? _player.CurrentClassDef.AvailableSubclasses : null;
            if (subs != null)
                foreach (var c in subs)
                    if (c != null) _classes.Add(c);
        }

        if (_classes.Count == 0) return false;

        foreach (Transform child in CardsParent) Destroy(child.gameObject);

        for (int i = 0; i < _classes.Count; i++)
        {
            GameObject cardObj = Instantiate(ClassCardPrefab, CardsParent);
            UI_ClassCard cardUI = cardObj.GetComponent<UI_ClassCard>();
            if (cardUI != null)
            {
                cardUI.SetupCard(_classes[i], i + 1);    // el número = la tecla que la elige
                cardUI.OnCardClicked = ConfirmSelection; // clic → elegir
            }
            _cards.Add(cardUI); // en paralelo a _classes (misma posición = misma clase)
        }
        return true;
    }

    // =========================================================
    // SELECCIÓN
    // =========================================================

    private void Update()
    {
        if (!_open) return;

        PlayerInputProvider input = PlayerInputProvider.Local;

        // Teclas 1..9 — solo si esta instancia usa teclado, para no cruzar el
        // teclado compartido con la instancia de mando (MPPM).
        if (input == null || input.UsesKeyboardMouse)
        {
            for (int i = 0; i < _classes.Count && i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                {
                    ConfirmSelection(_classes[i]);
                    return;
                }
            }
        }

        if (input == null) return;

        int step = input.ReadNavigateStep();
        if (step != 0) MoveSelection(step);

        if (input.Submit != null && input.Submit.WasPressedThisFrame() &&
            _selectedIndex >= 0 && _selectedIndex < _classes.Count)
        {
            ConfirmSelection(_classes[_selectedIndex]);
        }
    }

    // Mueve el resaltado al navegar con control (envuelve en los extremos).
    private void MoveSelection(int step)
    {
        if (_cards.Count == 0) return;

        if (_selectedIndex >= 0 && _selectedIndex < _cards.Count && _cards[_selectedIndex] != null)
            _cards[_selectedIndex].SetHighlighted(false);

        _selectedIndex = ((_selectedIndex + step) % _cards.Count + _cards.Count) % _cards.Count;

        if (_cards[_selectedIndex] != null) _cards[_selectedIndex].SetHighlighted(true);
    }

    // Equipa la clase elegida (EquipCharacterClass ya sincroniza por red) y cierra.
    //
    // La diferencia entre los dos modos está acá: elegir una clase BASE reinicia el
    // progreso (empezás esa clase de cero), mientras que evolucionar a una SUBCLASE
    // lo conserva — es la progresión natural del personaje.
    private void ConfirmSelection(CharacterClassDefinition selectedClass)
    {
        if (!_open || _player == null || selectedClass == null) return;

        _player.EquipCharacterClass(selectedClass, resetProgress: _mode == EMode.BaseClasses);
        CloseMenu();
    }

    private void CloseMenu()
    {
        if (MenuContainer != null) MenuContainer.SetActive(false);
        _open = false;

        if (_player != null) _player.SetInputLocked(false);

        if (PlayerInputProvider.Local != null) PlayerInputProvider.Local.SetUIMode(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    // Crea un EventSystem si la escena no tiene uno: sin él no funciona NINGÚN
    // evento de puntero (clic, hover) en toda la UI.
    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }
}
