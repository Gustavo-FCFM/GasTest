using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using FishNet;

// ============================================================
// UI_LobbyMenu
//
// Menú de ENTRADA a la partida: aparece después de conectarse y ANTES de que exista
// tu personaje. Pide nombre, equipo (1/2/3) y clase inicial; recién al confirmar el
// servidor spawnea al jugador.
//
// Por qué antes de spawnear: el equipo define quién es aliado de quién, y cambiarlo
// con el personaje ya en juego obligaría a resincronizar media partida. Así entra
// directo con su equipo y su nombre puestos.
//
// Reparto de responsabilidades:
//   · NOMBRE y EQUIPO viajan al servidor por broadcast (SpawnRequestBroadcast), que
//     es quien crea el personaje y los asigna.
//   · La CLASE la equipa el propio dueño en cuanto su personaje aparece, por el mismo
//     camino de siempre (EquipCharacterClass, que ya se sincroniza solo). No hace
//     falta mandarla al servidor.
//
// ESCENA: este menú va en un Canvas de la ESCENA, no en el prefab de la cámara del
// jugador — tiene que verse cuando todavía no hay jugador.
// ============================================================
public class UI_LobbyMenu : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("Raíz del menú (se prende/apaga).")]
    public GameObject MenuContainer;

    [Header("Datos de la sala")]
    [Tooltip("Muestra la IP a la que conectarse. Opcional.")]
    public TMPro.TMP_Text HostAddressText;
    [Tooltip("Campo donde el jugador escribe su nombre.")]
    public TMPro.TMP_InputField NameInput;
    [Tooltip("Nombre por defecto si lo deja vacío.")]
    public string DefaultName = "Jugador";

    [Header("Equipo")]
    [Tooltip("Botones de equipo, EN ORDEN: el primero es el equipo 1, el segundo el 2, etc.")]
    public Button[] TeamButtons;
    [Tooltip("Color del botón del equipo elegido.")]
    public Color SelectedTeamColor = new Color(0.3f, 0.8f, 1f, 1f);
    [Tooltip("Color del botón sin elegir.")]
    public Color NormalTeamColor   = Color.white;
    [Tooltip("Color al pasar el mouse por encima (sin elegir todavía).")]
    public Color HoverTeamColor    = new Color(0.12f, 0.16f, 0.22f, 1f);

    [Header("Clase inicial")]
    [Tooltip("Clases elegibles al entrar. Poné las mismas que MainBaseClasses del Player.")]
    public CharacterClassDefinition[] SelectableClasses;
    [Tooltip("Contenedor donde se instancian las tarjetas de clase.")]
    public Transform CardsParent;
    [Tooltip("Prefab de UI_ClassCard.")]
    public GameObject ClassCardPrefab;
    [Tooltip("Simplificar la tarjeta en el lobby: deja el icono y el NOMBRE, y oculta lo demás. " +
             "El mismo prefab se sigue usando completo en el menú de clases del juego.")]
    public bool IconsOnly = true;
    [Tooltip("Objetos de la tarjeta que se ocultan con 'Icons Only', por nombre. El nombre de la " +
             "clase (ClassTittle) se deja visible para saber qué es cada icono.")]
    public string[] HiddenCardParts = { "ClassDescription" };
    [Tooltip("Lado de cada tarjeta en píxeles: quedan cuadradas y todas iguales. El Canvas Scaler " +
             "es el que las escala según el tamaño de pantalla.")]
    public float CardSize = 180f;
    [Tooltip("Color del contorno que marca la clase elegida.")]
    public Color SelectedClassColor = new Color(0.3f, 0.8f, 1f, 1f);
    [Tooltip("Grosor de ese contorno.")]
    public float SelectedOutlineSize = 4f;

    [Header("Confirmar")]
    [Tooltip("Botón para entrar a la partida. Se habilita al elegir equipo y clase.")]
    public Button ConfirmButton;

    [Header("Sala de espera (opcional)")]
    [Tooltip("Casilla para entrar como ESPECTADOR: sin personaje, sin equipo y sin frenar el " +
             "arranque de la partida. Dejala en None si no querés la opción.")]
    public Toggle SpectatorToggle;

    [Tooltip("Dónde se avisa que el nombre está repetido o que el equipo está lleno. Opcional.")]
    public TMPro.TMP_Text WarningText;

    // La lista de clases del menú es la MISMA en todos los peers (mismo asset, misma
    // escena, mismo build), así que su índice sirve para decir "elegí esta clase" por
    // red sin mandar el ScriptableObject. Lo usa LobbyEntry.ClassIndex, y el panel de
    // la sala lo resuelve de vuelta con ClassByIndex para mostrar el icono.
    public static UI_LobbyMenu Instance { get; private set; }

    public CharacterClassDefinition ClassByIndex(int index)
        => SelectableClasses != null && index >= 0 && index < SelectableClasses.Length
            ? SelectableClasses[index]
            : null;

    private int IndexOfClass(CharacterClassDefinition cls)
    {
        if (SelectableClasses == null || cls == null) return -1;
        for (int i = 0; i < SelectableClasses.Length; i++)
            if (SelectableClasses[i] == cls) return i;
        return -1;
    }

    private int _teamID = -1;
    private CharacterClassDefinition _chosenClass;
    private readonly List<UI_ClassCard> _cards = new List<UI_ClassCard>();
    private bool _sent;

    private void Awake()
    {
        Instance = this;
        if (MenuContainer != null) MenuContainer.SetActive(false);
    }

    private void OnEnable()  => LobbyManager.OnRejected += HandleRejected;
    private void OnDisable() => LobbyManager.OnRejected -= HandleRejected;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // El servidor rechazó lo que mandamos. El TEXTO se arma acá y no viaja por red
    // (mismo criterio que los avisos del modo de juego).
    private void HandleRejected(ELobbyRejection reason)
    {
        switch (reason)
        {
            case ELobbyRejection.NameTaken:
                ShowWarning("Ese nombre ya está en uso. Elegí otro.");
                break;
            case ELobbyRejection.TeamFull:
                ShowWarning("Ese equipo está lleno.");
                break;
        }
    }

    private void ShowWarning(string message)
    {
        if (WarningText != null) WarningText.text = message;
        else Debug.LogWarning($"[Lobby] {message}");
    }

    // Le cuenta al servidor lo que tenemos elegido AHORA, aunque esté a medias: así el
    // resto ve aparecer tu fila con un "?" en vez de no verte hasta que confirmes, que
    // es justo lo que hace imposible acomodar los equipos entre todos.
    private void PushSelection()
    {
        LobbyManager lobby = LobbyManager.Instance;
        if (lobby == null || !InstanceFinder.IsClientStarted) return;

        string playerName = NameInput != null && !string.IsNullOrWhiteSpace(NameInput.text)
            ? NameInput.text.Trim()
            : DefaultName;

        lobby.ServerSubmit(playerName, Mathf.Max(0, _teamID), IndexOfClass(_chosenClass), IsSpectator);
    }

    private bool IsSpectator => SpectatorToggle != null && SpectatorToggle.isOn;

    private void Start()
    {
        BuildTeamButtons();
        BuildClassCards();

        // El nombre también habilita/deshabilita Confirmar, así que hay que
        // reevaluarlo mientras se escribe.
        if (NameInput != null)
        {
            NameInput.onValueChanged.AddListener(_ => UpdateConfirmState());
            // El nombre se manda al TERMINAR de escribir, no en cada tecla: si no, cada
            // letra sería un pedido al servidor y un rebote por "nombre repetido" a
            // mitad de palabra.
            NameInput.onEndEdit.AddListener(_ => PushSelection());
        }

        if (SpectatorToggle != null)
            SpectatorToggle.onValueChanged.AddListener(_ => { PushSelection(); UpdateConfirmState(); });
        if (ConfirmButton != null)
        {
            ConfirmButton.onClick.RemoveAllListeners();
            ConfirmButton.onClick.AddListener(Confirm);
        }

        UpdateConfirmState();
    }

    private void Update()
    {
        // Se muestra apenas hay conexión de cliente y todavía no pediste entrar.
        bool connected = InstanceFinder.IsClientStarted;
        bool shouldShow = connected && !_sent;

        if (MenuContainer != null && MenuContainer.activeSelf != shouldShow)
        {
            MenuContainer.SetActive(shouldShow);
            if (shouldShow) OnOpened();
        }
    }

    private void OnOpened()
    {
        EnsureEventSystem();

        // El cursor tiene que estar libre para escribir y clickear.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        if (HostAddressText != null)
        {
            ConnectionHUD hud = FindFirstObjectByType<ConnectionHUD>();
            string ip = hud != null ? hud.HostAddress : "—";
            HostAddressText.text = $"IP del Host: {ip}";
        }

        // Nos anotamos ya, sin haber elegido nada: los demás tienen que VER que estás
        // conectado y todavía decidiendo (aparecés con "?"), no descubrirte recién
        // cuando confirmás.
        PushSelection();
    }

    // =========================================================
    // EQUIPO
    // =========================================================

    private void BuildTeamButtons()
    {
        if (TeamButtons == null) return;

        for (int i = 0; i < TeamButtons.Length; i++)
        {
            if (TeamButtons[i] == null) continue;
            int team = i + 1; // el índice del botón define el equipo: 1, 2, 3...
            TeamButtons[i].onClick.RemoveAllListeners();
            TeamButtons[i].onClick.AddListener(() => SelectTeam(team));
        }
        RefreshTeamButtons();
    }

    private void SelectTeam(int team)
    {
        _teamID = team;
        RefreshTeamButtons();
        UpdateConfirmState();
        PushSelection();
    }

    private void RefreshTeamButtons()
    {
        if (TeamButtons == null) return;

        for (int i = 0; i < TeamButtons.Length; i++)
        {
            if (TeamButtons[i] == null) continue;

            // Se pinta el ColorBlock del Button, NO el color de su Image: el Button
            // tiñe su Image solo en cada evento de puntero (hover, salir, clic), así
            // que escribir Image.color se perdía apenas movías el mouse — parecía que
            // el equipo "se deseleccionaba".
            bool selected = (i + 1 == _teamID);
            ColorBlock cb = TeamButtons[i].colors;
            cb.normalColor      = selected ? SelectedTeamColor : NormalTeamColor;
            cb.selectedColor    = cb.normalColor;
            // Al pasar el mouse: azul oscuro si no está elegido (antes se ponía
            // blanco, el default de Unity). Si ya está elegido, mantiene su azul.
            cb.highlightedColor = selected ? SelectedTeamColor : HoverTeamColor;
            cb.pressedColor     = SelectedTeamColor;
            TeamButtons[i].colors = cb;
        }
    }

    // =========================================================
    // CLASE
    // =========================================================

    private void BuildClassCards()
    {
        if (CardsParent == null || ClassCardPrefab == null || SelectableClasses == null) return;

        foreach (Transform child in CardsParent) Destroy(child.gameObject);
        _cards.Clear();

        for (int i = 0; i < SelectableClasses.Length; i++)
        {
            CharacterClassDefinition cls = SelectableClasses[i];
            if (cls == null) continue;

            GameObject cardObj = Instantiate(ClassCardPrefab, CardsParent);
            UI_ClassCard card = cardObj.GetComponent<UI_ClassCard>();
            if (card != null)
            {
                card.SetupCard(cls, i + 1);
                // Acá la tarjeta NO equipa: solo deja anotada la elección. El personaje
                // todavía no existe.
                card.OnCardClicked = SelectClass;
            }

            if (IconsOnly) SimplifyCard(card, cls);

            // Todas del mismo tamaño y cuadradas, para que la fila quede pareja.
            LayoutElement le = cardObj.GetComponent<LayoutElement>();
            if (le == null) le = cardObj.AddComponent<LayoutElement>();
            le.preferredWidth  = CardSize;
            le.preferredHeight = CardSize;

            _cards.Add(card);
        }
    }

    // Deja la tarjeta con lo justo para el lobby: ICONO + NOMBRE.
    //
    // Usa las referencias que la propia tarjeta ya tiene (DescriptionText,
    // ClassNameText) en vez de buscar objetos por nombre: es lo que hace que funcione
    // aunque en el prefab los objetos se llamen distinto.
    private void SimplifyCard(UI_ClassCard card, CharacterClassDefinition cls)
    {
        if (card == null) return;

        // Fuera la descripción.
        if (card.DescriptionText != null) card.DescriptionText.gameObject.SetActive(false);

        // El nombre, SIN el "[1]" de adelante: ese prefijo lo agrega SetupCard cuando
        // la tarjeta no tiene un NumberText propio, y en el lobby se elige con el
        // mouse, así que el número no aporta nada.
        if (card.ClassNameText != null && cls != null) card.ClassNameText.text = cls.ClassName;
        if (card.NumberText != null) card.NumberText.gameObject.SetActive(false);

        // Escape hatch por si querés ocultar algo más, por nombre de objeto.
        if (HiddenCardParts == null) return;
        foreach (Transform t in card.GetComponentsInChildren<Transform>(true))
            foreach (string hidden in HiddenCardParts)
                if (t.name == hidden) t.gameObject.SetActive(false);
    }

    private void SelectClass(CharacterClassDefinition cls)
    {
        _chosenClass = cls;

        for (int i = 0; i < _cards.Count; i++)
            if (_cards[i] != null) MarkSelected(_cards[i], _cards[i].AssignedClass == cls);

        UpdateConfirmState();
        PushSelection();
    }

    // Marca la tarjeta elegida con un CONTORNO, no con el agrandado de UI_ClassCard:
    // esa tarjeta usa el mismo efecto para el hover y lo revierte en OnPointerExit, así
    // que la elección se borraba apenas sacabas el mouse. El contorno es independiente
    // y queda puesto.
    private void MarkSelected(UI_ClassCard card, bool selected)
    {
        if (card == null) return;

        // Va sobre el icono porque un Outline necesita un Graphic donde dibujarse, y
        // el icono es el que siempre existe en la tarjeta.
        Transform icon = FindChild(card.transform, "ClassIcon");
        Graphic target = icon != null ? icon.GetComponent<Graphic>() : card.GetComponent<Graphic>();
        if (target == null) return;

        Outline outline = target.GetComponent<Outline>();
        if (outline == null) outline = target.gameObject.AddComponent<Outline>();

        outline.effectColor    = SelectedClassColor;
        outline.effectDistance = new Vector2(SelectedOutlineSize, SelectedOutlineSize);
        outline.enabled        = selected;
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    // =========================================================
    // CONFIRMAR
    // =========================================================

    // Confirmar solo se habilita con las TRES cosas elegidas: nombre, equipo y clase.
    private void UpdateConfirmState()
    {
        if (ConfirmButton == null) return;

        bool hasName = NameInput == null || !string.IsNullOrWhiteSpace(NameInput.text);

        // El espectador no elige equipo ni clase: con el nombre alcanza.
        bool chosen = IsSpectator || (_teamID > 0 && _chosenClass != null);

        // Aviso en vivo del nombre repetido, sin esperar a que el servidor lo rechace:
        // así no confirmás para enterarte recién ahí.
        bool nameTaken = false;
        LobbyManager lobby = LobbyManager.Instance;
        if (lobby != null && hasName && NameInput != null)
        {
            int myId = InstanceFinder.ClientManager != null && InstanceFinder.ClientManager.Connection != null
                ? InstanceFinder.ClientManager.Connection.ClientId
                : -1;
            nameTaken = lobby.IsNameTaken(NameInput.text, myId);
        }

        if (WarningText != null)
            WarningText.text = nameTaken ? "Ese nombre ya está en uso. Elegí otro." : string.Empty;

        bool ready = hasName && chosen && !nameTaken;

        if (ConfirmButton.interactable != ready) ConfirmButton.interactable = ready;
    }

    // Le pide al servidor que cree el personaje con el nombre y equipo elegidos, y
    // se queda esperando a que aparezca para equipar la clase.
    public void Confirm()
    {
        // El espectador entra sin equipo ni clase: no se le pide ninguna de las dos.
        bool spectator = IsSpectator;
        if (_sent || (!spectator && (_teamID <= 0 || _chosenClass == null))) return;

        if (!InstanceFinder.IsClientStarted)
        {
            Debug.LogWarning("[Lobby] Todavía no hay conexión — no puedo pedir el spawn.");
            return;
        }

        string playerName = NameInput != null && !string.IsNullOrWhiteSpace(NameInput.text)
            ? NameInput.text.Trim()
            : DefaultName;

        _sent = true;
        if (MenuContainer != null) MenuContainer.SetActive(false);

        // "Listo" en la sala: es lo que destraba el arranque de la preparación para
        // todos (ver LobbyManager.AllReady y el gate de MercenariesGameMode).
        LobbyManager lobby = LobbyManager.Instance;
        if (lobby != null)
        {
            lobby.ServerSubmit(playerName, spectator ? 0 : _teamID,
                               spectator ? -1 : IndexOfClass(_chosenClass), spectator);
            lobby.ServerSetReady(true);
        }

        // El espectador NO pide personaje: se queda mirando con la cámara del lobby.
        // Tampoco frena el arranque (ver LobbyManager.AllReady).
        if (spectator)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
            return;
        }

        // Se manda por BROADCAST y no por ServerRpc: un RPC necesita un NetworkObject
        // ya inicializado en el cliente, y acá el jugador todavía no tiene personaje.
        InstanceFinder.ClientManager.Broadcast(new SpawnRequestBroadcast
        {
            PlayerName = playerName,
            TeamID     = _teamID,
        });

        StartCoroutine(EquipChosenClassWhenSpawned());
    }

    // El personaje tarda un momento en llegar (viaje de red + spawn). Cuando aparece,
    // el DUEÑO equipa la clase por el camino normal, que ya se sincroniza a todos.
    private IEnumerator EquipChosenClassWhenSpawned()
    {
        float timeout = 10f;
        while (PlayerController.LocalPlayer == null && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        PlayerController player = PlayerController.LocalPlayer;
        if (player == null)
        {
            Debug.LogWarning("[Lobby] El personaje nunca apareció — no se pudo equipar la clase elegida.");
            yield break;
        }

        player.EquipCharacterClass(_chosenClass, resetProgress: true);

        // Devolver el cursor al modo juego.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }
}
