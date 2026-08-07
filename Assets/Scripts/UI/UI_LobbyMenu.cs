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
//   · NOMBRE y EQUIPO viajan al servidor (NetworkGameManager.ServerRequestSpawn), que
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
    public Color NormalTeamColor   = Color.white;

    [Header("Clase inicial")]
    [Tooltip("Clases elegibles al entrar. Poné las mismas que MainBaseClasses del Player.")]
    public CharacterClassDefinition[] SelectableClasses;
    [Tooltip("Contenedor donde se instancian las tarjetas de clase.")]
    public Transform CardsParent;
    [Tooltip("Prefab de UI_ClassCard.")]
    public GameObject ClassCardPrefab;

    [Header("Confirmar")]
    [Tooltip("Botón para entrar a la partida. Se habilita al elegir equipo y clase.")]
    public Button ConfirmButton;

    private int _teamID = -1;
    private CharacterClassDefinition _chosenClass;
    private readonly List<UI_ClassCard> _cards = new List<UI_ClassCard>();
    private bool _sent;

    private void Awake()
    {
        if (MenuContainer != null) MenuContainer.SetActive(false);
    }

    private void Start()
    {
        BuildTeamButtons();
        BuildClassCards();
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
    }

    private void RefreshTeamButtons()
    {
        if (TeamButtons == null) return;

        for (int i = 0; i < TeamButtons.Length; i++)
        {
            if (TeamButtons[i] == null) continue;
            Image img = TeamButtons[i].GetComponent<Image>();
            if (img != null) img.color = (i + 1 == _teamID) ? SelectedTeamColor : NormalTeamColor;
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
            _cards.Add(card);
        }
    }

    private void SelectClass(CharacterClassDefinition cls)
    {
        _chosenClass = cls;

        // Resaltar la elegida.
        for (int i = 0; i < _cards.Count; i++)
            if (_cards[i] != null) _cards[i].SetHighlighted(_cards[i].AssignedClass == cls);

        UpdateConfirmState();
    }

    // =========================================================
    // CONFIRMAR
    // =========================================================

    private void UpdateConfirmState()
    {
        if (ConfirmButton == null) return;

        bool ready = _teamID > 0 && _chosenClass != null;
        if (ConfirmButton.interactable != ready) ConfirmButton.interactable = ready;

        ConfirmButton.onClick.RemoveAllListeners();
        ConfirmButton.onClick.AddListener(Confirm);
    }

    // Le pide al servidor que cree el personaje con el nombre y equipo elegidos, y
    // se queda esperando a que aparezca para equipar la clase.
    public void Confirm()
    {
        if (_sent || _teamID <= 0 || _chosenClass == null) return;

        NetworkGameManager gm = FindFirstObjectByType<NetworkGameManager>();
        if (gm == null)
        {
            Debug.LogError("[Lobby] No encontré el NetworkGameManager en la escena — no puedo entrar.");
            return;
        }

        string playerName = NameInput != null && !string.IsNullOrWhiteSpace(NameInput.text)
            ? NameInput.text.Trim()
            : DefaultName;

        _sent = true;
        if (MenuContainer != null) MenuContainer.SetActive(false);

        gm.ServerRequestSpawn(playerName, _teamID);
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
