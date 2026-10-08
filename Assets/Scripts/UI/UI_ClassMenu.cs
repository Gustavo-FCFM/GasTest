using UnityEngine;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
// jugador y libera el cursor. Solo se abre con los pies en el piso, y recibir daño lo
// CIERRA sin elegir: abrirlo afuera de la base es bajo tu riesgo.
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
    [Tooltip("Abrir el menú de subclases SOLO al llegar al nivel máximo.\n\n" +
             "Apagado (lo normal): al llegar a nivel máximo el HUD avisa que se puede evolucionar " +
             "y el jugador abre el menú con la tecla V cuando le conviene. Abrirlo solo lo dejaba " +
             "plantado en medio de una pelea, sin poder moverse hasta elegir.")]
    public bool OpenSubclassesOnMaxLevel = false;

    [Tooltip("Recibir daño con el menú abierto lo cierra sin elegir. Elegir subclase no " +
             "es un momento de invulnerabilidad: el que lo abre en medio de una pelea se " +
             "lleva los golpes igual, así que lo sano es que vuelva a su base y lo abra ahí.")]
    public bool CloseOnDamage = true;

    [Header("Tarjetas (grilla)")]
    [Tooltip("Desde cuántas clases se reparten en DOS filas. Con menos, una sola fila.")]
    public int TwoRowsFrom = 5;
    [Tooltip("Tamaño MÁXIMO de cada tarjeta, en unidades del canvas. Si no entran en el panel, se achican solas.")]
    public Vector2 MaxCardSize = new Vector2(230f, 170f);
    [Tooltip("Espacio entre tarjetas (horizontal, vertical).")]
    public Vector2 CardSpacing = new Vector2(20f, 16f);
    [Tooltip("Lado del ícono de clase en cada tarjeta.")]
    public float CardIconSize = 56f;
    [Tooltip("Tamaño del nombre de la clase (se achica si no entra en un renglón).")]
    public float CardTitleSize = 16f;
    [Tooltip("Tamaño de la descripción. Si no entra en la tarjeta, se achica sola hasta el mínimo.")]
    public float CardDescriptionSize = 10f;
    public float CardDescriptionMinSize = 6f;

    // La grilla donde van las tarjetas (ver EnsureGrid).
    private RectTransform _grid;

    // Última vida conocida mientras el menú está abierto. Bajar de ahí = daño.
    private float _lastHealth;

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
        if (_playerASC != null)
        {
            _playerASC.OnMaxLevelReached        -= OpenSubclassesFromLevelUp;
            _playerASC.OnAttributeChangedCallback -= OnPlayerAttributeChanged;
        }
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
        Announce(Loc.T("classmenu.only_in_base"), new Color(1f, 0.75f, 0.3f), 26f);
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

        // Solo con los pies en el piso. Elegir en el aire cambiaba el Animator a mitad de
        // un salto y la animación quedaba trabada; dejarlo caer con el menú abierto
        // también se veía feo. Así que el menú directamente no se abre hasta aterrizar.
        if (!_player.IsGrounded)
        {
            Announce(Loc.T("classmenu.land_first"), new Color(1f, 0.75f, 0.3f), 24f);
            return;
        }

        _mode = mode;
        if (!BuildMenu()) return; // sin clases que mostrar, no bloqueamos al jugador

        EnsureEventSystem();

        MenuContainer.SetActive(true);
        _open = true;

        _player.SetInputLocked(true);

        // Vigilar la vida mientras está abierto (ver OnPlayerAttributeChanged).
        if (_playerASC != null)
        {
            _lastHealth = _playerASC.GetAttributeValue(EAttributeType.Health);
            _playerASC.OnAttributeChangedCallback -= OnPlayerAttributeChanged;
            _playerASC.OnAttributeChangedCallback += OnPlayerAttributeChanged;
        }

        // Elegir subclase se puede en cualquier lado, pero afuera de la base es
        // arriesgado: se avisa, no se bloquea.
        if (mode == EMode.Subclasses && CloseOnDamage && !CanChangeBaseClassHere())
            Announce(Loc.T("classmenu.outside_base"), new Color(1f, 0.75f, 0.3f), 26f);

        // Suelta el cursor y pasa al modo UI (apaga el mapa Player, enciende el UI) para
        // navegar con control sin disparar acciones de juego. Lo hace UICursor, que es el
        // único dueño de las dos cosas — ver ahí por qué.
        UICursor.Request(this);

        // Arrancar con la primera tarjeta resaltada (punto de partida para el control).
        if (_cards.Count > 0) MoveSelection(1);
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

        RectTransform grid = EnsureGrid(_classes.Count);

        // Las de la vez anterior. Se APAGAN además de destruirse: Destroy espera al fin del
        // frame, y mientras tanto la grilla las seguiría contando al acomodar.
        foreach (Transform child in grid)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        for (int i = 0; i < _classes.Count; i++)
        {
            GameObject cardObj = Instantiate(ClassCardPrefab, grid);
            UI_ClassCard cardUI = cardObj.GetComponent<UI_ClassCard>();
            if (cardUI != null)
            {
                cardUI.SetupCard(_classes[i], i + 1);    // el número = la tecla que la elige
                cardUI.ApplyCompact(CardIconSize, CardTitleSize, CardDescriptionSize, CardDescriptionMinSize);
                cardUI.OnCardClicked = ConfirmSelection; // clic → elegir
            }
            _cards.Add(cardUI); // en paralelo a _classes (misma posición = misma clase)
        }
        return true;
    }

    // La grilla de tarjetas (8 de octubre, pedido de Gustavo: con 6 clases todo se
    // estiraba). El panel del prefab las acomodaba en UNA fila estirada a todo el alto
    // (HorizontalLayoutGroup con expandir): quedaban angostas y altas y el nombre se
    // partía en tres renglones. Ahora van en una grilla centrada con tarjetas de tamaño
    // fijo: una fila con pocas clases (las subclases), dos desde TwoRowsFrom. Se arma por
    // código para no tocar el prefab: se apaga ese layout y las tarjetas van a un hijo nuevo.
    private RectTransform EnsureGrid(int count)
    {
        if (_grid == null)
        {
            HorizontalOrVerticalLayoutGroup oldLayout = CardsParent.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (oldLayout != null) oldLayout.enabled = false;

            // Lo que quedó del layout viejo (tarjetas sueltas de antes) se va.
            foreach (Transform child in CardsParent) Destroy(child.gameObject);

            var go = new GameObject("CardsGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            go.layer = CardsParent.gameObject.layer;
            _grid = (RectTransform)go.transform;
            _grid.SetParent(CardsParent, false);
            _grid.anchorMin = _grid.anchorMax = _grid.pivot = new Vector2(0.5f, 0.5f);
            _grid.anchoredPosition = Vector2.zero;
        }

        int columns = count >= Mathf.Max(2, TwoRowsFrom) ? Mathf.CeilToInt(count / 2f) : count;
        columns = Mathf.Max(1, columns);
        int rows = Mathf.CeilToInt(count / (float)columns);

        // Las tarjetas entran en el panel: si el máximo no entra, se achican.
        Vector2 area = ((RectTransform)CardsParent).rect.size;
        float w = MaxCardSize.x, h = MaxCardSize.y;
        if (area.x > 1f) w = Mathf.Min(w, (area.x - CardSpacing.x * (columns - 1)) / columns);
        if (area.y > 1f) h = Mathf.Min(h, (area.y - CardSpacing.y * (rows - 1)) / rows);
        w = Mathf.Max(60f, w);
        h = Mathf.Max(60f, h);

        GridLayoutGroup grid = _grid.GetComponent<GridLayoutGroup>();
        grid.cellSize        = new Vector2(w, h);
        grid.spacing         = CardSpacing;
        grid.startCorner     = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis       = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment  = TextAnchor.MiddleCenter;
        grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;

        _grid.sizeDelta = new Vector2(columns * w + (columns - 1) * CardSpacing.x,
                                      rows * h + (rows - 1) * CardSpacing.y);
        return _grid;
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

        // Se cierra ANTES de equipar. Equipar reinicia la vida con el máximo de la clase
        // nueva, y con el menú todavía abierto el vigilante de daño (ver
        // OnPlayerAttributeChanged) veía esa bajada como un golpe: cerraba "por daño" y
        // mostraba "You got hit" justo al elegir.
        bool resetProgress = _mode == EMode.BaseClasses;
        CloseMenu();

        _player.EquipCharacterClass(selectedClass, resetProgress: resetProgress);

        // Se apaga el aviso de "podés evolucionar" del HUD: ya evolucionaste. Nadie lo
        // apagaba, asi que una vez encendido se quedaba puesto el resto de la partida.
        UI_PlayerHUD hud = FindFirstObjectByType<UI_PlayerHUD>();
        if (hud != null) hud.HideLevelUpNotification();
    }

    private void CloseMenu()
    {
        if (MenuContainer != null) MenuContainer.SetActive(false);
        _open = false;

        if (_playerASC != null) _playerASC.OnAttributeChangedCallback -= OnPlayerAttributeChanged;
        if (_player != null) _player.SetInputLocked(false);

        UICursor.Release(this);
    }

    // Llega por cada atributo que cambia, en el dueño (en el host directo desde el ASC;
    // en un cliente remoto vía el SyncVar de vida, que lo vuelve a escribir en el ASC
    // local). Solo nos importa la VIDA, y solo cuando BAJA: una curación no cierra nada.
    private void OnPlayerAttributeChanged(EAttributeType type, float value)
    {
        if (!_open || type != EAttributeType.Health) return;

        // Un bono de vida máxima que se acaba recorta la vida al nuevo máximo: baja sin
        // que nadie pegue. Se reconoce porque queda justo EN el máximo (igual que en la
        // entrega del Objetivo); un golpe de verdad la deja por debajo.
        float max = _playerASC != null ? _playerASC.GetAttributeValue(EAttributeType.MaxHealth) : 0f;
        bool clampedToMax = max > 0f && value >= max - 0.01f;

        bool damaged = value < _lastHealth - 0.01f && !clampedToMax;
        _lastHealth = value;
        if (!damaged || !CloseOnDamage) return;

        CloseMenu();
        Announce(Loc.T("classmenu.hit_cancelled", ("key", InputGlyphs.Subclass)),
                 new Color(1f, 0.4f, 0.3f), 26f);
    }

    // Usa el cartelón del modo si está en la escena; si no, al menos queda en la consola.
    private void Announce(string text, Color color, float fontSize)
    {
        UI_MatchAnnouncer announcer = FindFirstObjectByType<UI_MatchAnnouncer>();
        if (announcer != null) announcer.Push(text, color, fontSize);
        else                   Debug.Log("[UI_ClassMenu] " + text);
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
