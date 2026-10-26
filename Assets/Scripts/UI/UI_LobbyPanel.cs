using System.Collections;
using System.Collections.Generic;
using FishNet;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ============================================================
// UI_LobbyPanel
//
// La sala de espera entera en un solo panel: IP, nombre, los tres equipos con sus
// lugares, la franja de espectadores, el selector de clase y los botones de Confirmar
// y Start.
//
// VIVE EN EL PREFAB DE MENÚS (Menus_UI ▸ Lobby), desde el 14 de octubre de 2026: el panel,
// la IP, el nombre, las cabeceras de las columnas, los lugares (cada uno con su
// UI_LobbySlot), la línea de estado, los botones y el selector de clase están armados ahí y
// se acomodan a mano. Lo que depende de la LISTA de clases (las pestañas por rol y los
// íconos de la grilla) se arma al empezar copiando dos MOLDES del selector
// ("TabTemplate" y "ClassIconTemplate"): su tamaño y su altura son los del molde. Este
// componente vive en un objeto siempre prendido (el servidor lo consulta para saber con qué
// clase aparece cada bot); lo que se prende y se apaga es su canvas.
//
// CÓMO FUNCIONA EL FLUJO:
//   1. Entrás → te anotás con tu nombre y quedás de ESPECTADOR.
//   2. Te unís a un equipo tocando un lugar libre → dejás de ser espectador.
//   3. Elegís clase tocando tu propio ícono → se despliega la grilla de clases.
//   4. Confirmar te marca LISTO (tu nombre se pone verde). Volver a tocarlo lo apaga.
//   5. El HOST aprieta Start y arranca la partida para todos.
//   Salir (arriba a la izquierda) vuelve al menú principal; siendo host, cierra la sala.
//
// EL PERSONAJE NO APARECE AL CONFIRMAR, sino cuando la partida arranca. Si no, el que
// confirma primero se queda dando vueltas por el mapa mientras los demás todavía
// eligen — que es justo lo que pasaba antes.
//
// CON CONTROL (26 de octubre de 2026): es una pantalla de MenuInput. Arranca con algo elegido
// (ver DefaultSelection: unirte a un equipo, elegir tu clase, Confirmar, Start) y, cuando el
// botón elegido desaparece (al unirte, el "Unirte" se apaga), el control pasa al siguiente
// paso. B cierra el selector de clase; Start o ESC abren la pausa.
//
// AUTORIDAD: este panel solo PIDE (ServerSubmit / ServerSetReady / ServerRequestStart).
// Quién está en qué equipo, si un nombre se repite y si un equipo está lleno lo decide
// el servidor en LobbyManager; acá solo se dibuja lo que la sala sincronizada dice.
// ============================================================
public class UI_LobbyPanel : MonoBehaviour, IMenuScreen
{
    [Header("Clases elegibles")]
    [Tooltip("Las mismas que MainBaseClasses del Player. El ícono de cada una sale de su " +
             "ClassIcon, y el índice que se manda a la sala es la posición en esta lista.")]
    [Label("Clases elegibles")]
    public CharacterClassDefinition[] SelectableClasses;

    [Header("Piezas")]
    [Tooltip("El canvas de la sala: se prende con la conexión y se apaga al arrancar la partida.")]
    [Label("Canvas de la sala")]
    public GameObject LobbyCanvas;

    [Tooltip("El panel (lo que contiene todo; su escala agranda la sala entera).")]
    [Label("Panel")]
    public RectTransform Panel;

    [Label("IP del host")]
    public TMP_Text IpText;

    [Label("Campo del nombre")]
    public TMP_InputField NameInput;

    [Tooltip("Solo lo ve el host.")]
    [Label("Botón Start")]
    public Button StartButton;

    [Label("Texto de Start")]
    public TMP_Text StartLabel;

    [Label("Botón Confirmar")]
    public Button ConfirmButton;

    [Label("Texto de Confirmar")]
    public TMP_Text ConfirmLabel;

    [Tooltip("Salir de la sala al menú principal. Siendo host, cierra la sala para todos (el botón lo dice).")]
    [Label("Botón Salir")]
    public Button LeaveButton;

    [Label("Texto de Salir")]
    public TMP_Text LeaveLabel;

    [Tooltip("Siendo host, Salir se aprieta dos veces: la primera pregunta \"¿Cerrar para todos?\". Lo que espera la segunda antes de volver a decir \"Cerrar sala\".")]
    [Label("Tiempo para confirmar Salir (s)")]
    public float LeaveConfirmSeconds = 4f;

    [Tooltip("La línea sobre Confirmar: qué falta para arrancar, o qué te falta a vos.")]
    [Label("Línea de estado")]
    public TMP_Text StatusText;

    [Tooltip("El texto de la cabecera de cada equipo (\"Equipo 1\"...), en orden.")]
    [Label("Cabeceras de los equipos")]
    public TMP_Text[] TeamHeaders = new TMP_Text[LobbyManager.TeamCount];

    [Header("Piezas: selector de clase")]
    [Tooltip("El recuadro del selector. Su fondo es un botón que lo cierra.")]
    [Label("Selector de clase")]
    public RectTransform ClassPicker;

    [Label("Botón de cerrar (el fondo)")]
    public Button PickerCloseButton;

    [Tooltip("MOLDE de una pestaña de rol (apagado): un botón con un texto. Su altura en el selector es la de las pestañas.")]
    [Label("Molde de pestaña")]
    public Button TabTemplate;

    [Tooltip("MOLDE de un ícono de clase (apagado): un botón con su Image. Su tamaño y su altura en el selector son los de la grilla.")]
    [Label("Molde de ícono de clase")]
    public Button ClassIconTemplate;

    [Tooltip("El nombre de la clase que está bajo el mouse.")]
    [Label("Nombre de la clase")]
    public TMP_Text ClassNameLabel;

    [Tooltip("La descripción de la clase seleccionada.")]
    [Label("Descripción de la clase")]
    public TMP_Text ClassDescLabel;

    [Label("Botón Confirmar clase")]
    public Button PickerConfirm;

    [Header("Selector: separaciones (px del panel)")]
    [Label("Entre pestañas (px)")]
    public float TabGap = 8f;

    [Label("Entre íconos (px)")]
    public float IconGap = 12f;

    [Tooltip("Margen a los costados de la grilla: el selector se ensancha si las clases de un rol no entran.")]
    [Label("Margen de la grilla (px)")]
    public float PickerMargin = 32f;

    [Header("Colores (los que cambian en juego)")]
    [Tooltip("Las pestañas que no están abiertas se mezclan con este color.")]
    [Label("Color del panel")]
    public Color PanelColor   = new Color(0.09f, 0.09f, 0.11f, 0.97f);
    [Tooltip("El ícono de un lugar sin clase elegida.")]
    [Label("Color del lugar")]
    public Color SlotColor    = new Color(0.20f, 0.24f, 0.32f, 1f);
    [Label("Color: listo")]
    public Color ReadyColor   = new Color(0.55f, 0.95f, 0.55f, 1f);
    [Label("Color: pendiente")]
    public Color PendingColor = new Color(0.86f, 0.88f, 0.92f, 1f);
    [Tooltip("Color del botón de Start y de Confirmar cuando TODAVÍA no corresponde " +
             "apretarlos. Verde cuando sí.")]
    [Label("Color: inactivo")]
    public Color IdleColor    = new Color(0.17f, 0.24f, 0.40f, 1f);

    [Tooltip("Fondo de una fila OCUPADA: el recuadro detrás del ícono y el nombre. Las " +
             "filas libres no lo llevan — ahí el fondo lo pone el propio botón de Unirte.")]
    [Label("Color de fila")]
    public Color RowColor     = new Color(0.16f, 0.19f, 0.26f, 1f);

    [Tooltip("Fondo de TU propia fila. Sirve para encontrarte de un vistazo cuando la " +
             "sala está llena.")]
    [Label("Color de tu fila")]
    public Color OwnRowColor  = new Color(0.22f, 0.29f, 0.42f, 1f);

    [Tooltip("Color del nombre de los BOTS. Distinto del de un jugador listo a propósito: " +
             "en una captura tiene que verse de un vistazo quién es persona y quién no.")]
    [Label("Color de los bots")]
    public Color BotColor     = new Color(0.72f, 0.66f, 0.95f, 1f);

    // --- armados al empezar ---
    private bool  _built;
    private Image[] _classIcons;
    private int   _pickerSelection = -1;

    // Los grupos por rol del selector: qué clases van en cada uno (índices de
    // SelectableClasses), la pestaña de cada grupo, y cuál está abierto.
    private readonly List<EClassRole> _roleGroups = new List<EClassRole>();
    private readonly Dictionary<EClassRole, List<int>> _roleMembers = new Dictionary<EClassRole, List<int>>();
    private readonly List<Image> _roleTabs = new List<Image>();

    private readonly List<UI_LobbySlot>[] _teamSlots = new List<UI_LobbySlot>[LobbyManager.TeamCount];
    private readonly List<UI_LobbySlot>   _spectatorSlots = new List<UI_LobbySlot>();

    private bool  _spawnRequested;
    private bool  _registered;      // ya me anoté en la sala (aunque sea de espectador)
    private bool  _visible;         // el estado que el panel CREE tener; ver Update
    private float _redrawIn;        // segundos hasta el próximo redibujado de reloj
    private readonly LeaveConfirm _leaveConfirm = new LeaveConfirm();

    private void Start()
    {
        Build();
        Refresh();
    }

    // Cambiar de idioma (en Ajustes) redibuja lo que se calcula en Refresh; los textos fijos
    // están atados con LocalizedText y se reescriben solos.
    private void OnEnable()
    {
        LobbyManager.OnLobbyChanged += Refresh;
        Loc.OnLanguageChanged      += Refresh;
    }

    private void OnDisable()
    {
        LobbyManager.OnLobbyChanged -= Refresh;
        Loc.OnLanguageChanged      -= Refresh;
        MenuInput.Pop(this);
        UICursor.Release(this);
    }

    // =========================================================
    // CICLO
    // =========================================================

    private void Update()
    {
        LobbyManager lobby = LobbyManager.Instance;

        // La sala se muestra SOLO cuando ya hay conexión: IsLobbyReady se prende en
        // OnStartClient, o sea al iniciar el host o al conectarse como cliente. Antes
        // aparecía sobre el menú vacío, encima del recuadro de conexión.
        //
        // Y se va cuando arranca la partida: ahí ya estás en el mapa con personaje y HUD.
        bool show = lobby != null && lobby.IsLobbyReady && !lobby.MatchStarted;

        // Al desconectarse se vuelve al estado inicial. Si no, reconectarse te deja sin
        // anotar en la sala (y sin personaje cuando arranque la siguiente partida).
        if (lobby == null || !lobby.IsLobbyReady)
        {
            _registered     = false;
            _spawnRequested = false;
        }

        // Vuelta a la sala al terminar una partida: hay que poder volver a PEDIR
        // personaje cuando el host arranque la siguiente. Sin esto, la bandera se queda
        // en true de la partida anterior y el segundo Start te deja de espectador.
        if (lobby != null && !lobby.MatchStarted) _spawnRequested = false;

        if (LobbyCanvas != null && _visible != show)
        {
            _visible = show;
            LobbyCanvas.SetActive(show);
            if (show) MenuInput.Push(this); else MenuInput.Pop(this);
            _leaveConfirm.Cancel();
        }

        // El cursor tiene que estar libre para escribir y clickear en la sala. Se pide
        // por UICursor y no a mano: el recuadro de red también lo pide con ESC, y el
        // último en soltarlo no puede llevarse el de los demás.
        //
        // Va FUERA del if de visibilidad —o sea, cada frame— a propósito. Antes se
        // pedía y se soltaba solo en el FLANCO, y ese suelto ocurre cuando arranca la
        // partida, justo cuando el jugador todavía no existe: Apply() no tenía a quién
        // avisarle y el pedido quedaba anotado en el aire. Request y Release son
        // idempotentes (si el conjunto no cambia no hacen nada), así que repetirlos sale
        // gratis y el estado converge solo en vez de depender de un flanco.
        if (show) UICursor.Request(this);
        else      UICursor.Release(this);

        if (lobby == null) return;

        // Anotarse apenas la sala responde: entrar te deja de ESPECTADOR, sin tener que
        // tocar nada. Recién al elegir equipo dejás de serlo.
        if (!_registered && lobby.IsLobbyReady)
        {
            _registered = true;
            if (!lobby.TryGetLocalEntry(out _)) Submit(0, -1, spectator: true);
        }

        // El personaje se pide RECIÉN cuando arranca la partida, no al confirmar (ver
        // la cabecera). El espectador no pide nada: mira desde la cámara del lobby.
        if (lobby.MatchStarted && !_spawnRequested)
        {
            _spawnRequested = true;
            RequestSpawnIfPlaying(lobby);
        }

        // La sala avisa sola cuando cambia (OnLobbyChanged), así que acá alcanza con un
        // repaso lento para lo que NO viaja por la lista: la IP y quién es host.
        if (!show) return;
        _redrawIn -= Time.unscaledDeltaTime;
        if (_leaveConfirm.Changed(LeaveButton != null ? LeaveButton.gameObject : null)) _redrawIn = 0f;
        if (_redrawIn <= 0f)
        {
            _redrawIn = 0.5f;
            Refresh();
        }
    }

    private void RequestSpawnIfPlaying(LobbyManager lobby)
    {
        if (!lobby.TryGetLocalEntry(out LobbyEntry me)) return;
        if (me.Spectator || me.Team <= 0) return;

        // Va por BROADCAST y no por ServerRpc: un RPC necesita un NetworkObject ya
        // inicializado del lado del que llama, y el personaje todavía no existe.
        InstanceFinder.ClientManager.Broadcast(new SpawnRequestBroadcast
        {
            PlayerName = me.PlayerName,
            TeamID     = me.Team,
        });

        StartCoroutine(EquipClassWhenSpawned(me.ClassIndex));
    }

    // El personaje tarda un momento en llegar (viaje de red + spawn). Cuando aparece,
    // el DUEÑO equipa la clase por el camino normal, que ya se sincroniza a todos.
    private IEnumerator EquipClassWhenSpawned(int classIndex)
    {
        float timeout = 10f;
        while (PlayerController.LocalPlayer == null && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        CharacterClassDefinition cls = ClassAt(classIndex);
        if (PlayerController.LocalPlayer != null && cls != null)
            PlayerController.LocalPlayer.EquipCharacterClass(cls);
    }

    // =========================================================
    // PEDIDOS A LA SALA
    // =========================================================

    // Todo cambio (nombre, equipo, clase) se manda con el estado COMPLETO: la sala no
    // tiene mensajes parciales, así que siempre se le cuenta todo lo que tenemos.
    private void Submit(int team, int classIndex, bool spectator)
    {
        LobbyManager lobby = LobbyManager.Instance;
        if (lobby == null || !lobby.IsLobbyReady) return;

        lobby.ServerSubmit(CurrentName(), team, classIndex, spectator);
    }

    private void JoinTeam(int team)
    {
        LobbyManager lobby = LobbyManager.Instance;
        if (lobby == null) return;

        // El servidor revalida el cupo igual; esto solo evita el viaje de ida y vuelta
        // cuando ya se ve lleno desde acá.
        if (team > 0 && lobby.IsTeamFull(team)) return;

        int classIndex = lobby.TryGetLocalEntry(out LobbyEntry me) ? me.ClassIndex : -1;
        Submit(team, classIndex, spectator: team <= 0);
    }

    // Sobre QUIÉN va a actuar el selector de clase cuando se elija: 0 = yo, negativo = ese
    // bot. Se decide al abrirlo, no al elegir, porque la fila puede cambiar mientras está
    // abierto (alguien se conecta y se corren los lugares).
    private int _pickerTarget;

    // El host mete un bot en un lugar libre. Entra ya listo y con la primera clase de la
    // lista; cambiársela es tocarle el ícono.
    private void AddBot(int team)
    {
        LobbyManager lobby = LobbyManager.Instance;
        if (lobby == null || !lobby.IsLobbyReady) return;
        if (team > 0 && lobby.IsTeamFull(team)) return;

        lobby.ServerAddBot(team, 0);
    }

    private void RemoveBot(int botId)
    {
        LobbyManager lobby = LobbyManager.Instance;
        if (lobby == null || !lobby.IsLobbyReady || !LobbyManager.IsBot(botId)) return;

        lobby.ServerRemoveBot(botId);
    }

    private void ShowClassPickerFor(int entryId)
    {
        _pickerTarget = LobbyManager.IsBot(entryId) ? entryId : 0;
        ShowClassPicker(true);
    }

    private void ChooseClass(int classIndex)
    {
        LobbyManager lobby = LobbyManager.Instance;
        if (lobby == null || !lobby.IsLobbyReady) return;

        // Elegirle la clase a un bot es otro pedido: el bot no tiene nombre ni equipo que
        // reenviar, solo cambia su clase.
        if (LobbyManager.IsBot(_pickerTarget))
        {
            lobby.ServerSetBotClass(_pickerTarget, classIndex);
            ShowClassPicker(false);
            return;
        }

        if (!lobby.TryGetLocalEntry(out LobbyEntry me)) return;

        Submit(me.Team, classIndex, me.Spectator);
        ShowClassPicker(false);
    }

    // Confirmar = alternar "listo". Solo tiene sentido en un equipo y con clase elegida.
    private void ToggleReady()
    {
        LobbyManager lobby = LobbyManager.Instance;
        if (lobby == null || !lobby.IsLobbyReady) return;
        if (!lobby.TryGetLocalEntry(out LobbyEntry me)) return;
        if (me.Spectator || me.Team <= 0 || me.ClassIndex < 0) return;

        lobby.ServerSetReady(!me.Ready);
    }

    private void RequestStart()
    {
        LobbyManager lobby = LobbyManager.Instance;
        if (lobby != null && lobby.IsLobbyReady) lobby.ServerRequestStart();
    }

    // Salir al menú principal. Siendo host cierra la sala para todos: pregunta antes (LeaveConfirm).
    private void Leave()
    {
        if (!_leaveConfirm.Press(LobbyManager.LocalIsHost, LeaveConfirmSeconds)) { Refresh(); return; }
        UI_MainMenu.ReturnToMenu();
    }

    private void OnNameChanged(string _)
    {
        LobbyManager lobby = LobbyManager.Instance;
        if (lobby == null || !lobby.TryGetLocalEntry(out LobbyEntry me))
        {
            // Todavía no estás en la sala: anotarte como espectador es lo primero que
            // pasa al escribir tu nombre (ver el paso 1 de la cabecera).
            Submit(0, -1, spectator: true);
            return;
        }

        Submit(me.Team, me.ClassIndex, me.Spectator);
    }

    // =========================================================
    // DIBUJO
    // =========================================================

    private void Refresh()
    {
        LobbyManager lobby = LobbyManager.Instance;
        if (lobby == null || !_built) return;

        lobby.TryGetLocalEntry(out LobbyEntry me);
        int myId = me.ClientId;

        // --- IP ---
        if (IpText != null)
        {
            // El host ve SU IP en la red (la que les da a los demás); un cliente, a dónde se conectó.
            string ip = LobbyManager.LocalIsHost ? NetAddress.LanAddress()
                      : string.IsNullOrEmpty(NetAddress.Joined) ? "—" : NetAddress.Joined;
            IpText.text = Loc.T("lobby.host_ip", ("ip", ip));
        }

        // --- equipos ---
        for (int t = 0; t < LobbyManager.TeamCount; t++)
        {
            int team = t + 1;
            if (TeamHeaders != null && t < TeamHeaders.Length && TeamHeaders[t] != null)
                TeamHeaders[t].text = Loc.T("lobby.team", ("team", team));
            List<LobbyEntry> members = MembersOf(lobby, team);

            for (int s = 0; s < _teamSlots[t].Count; s++)
            {
                bool occupied = s < members.Count;
                DrawSlot(_teamSlots[t][s], occupied ? members[s] : default, occupied, myId, team);
            }
        }

        // --- espectadores ---
        // Una fila LIBRE de más al final: ese es el botón "Espectador", el camino de
        // vuelta para salirse de un equipo sin desconectarse. Las que sobran se apagan
        // para no dejar una escalera de botones repetidos.
        List<LobbyEntry> specs = MembersOf(lobby, 0);
        EnsureSpectatorRows(specs.Count + 1);

        for (int s = 0; s < _spectatorSlots.Count; s++)
        {
            UI_LobbySlot slot = _spectatorSlots[s];
            bool used = s <= specs.Count;
            if (slot.gameObject.activeSelf != used) slot.gameObject.SetActive(used);
            if (!used) continue;

            bool occupied = s < specs.Count;
            DrawSlot(slot, occupied ? specs[s] : default, occupied, myId, 0);
        }

        // --- botones ---
        bool inTeam   = !me.Spectator && me.Team > 0;
        bool canReady = inTeam && me.ClassIndex >= 0;
        bool allReady = lobby.AllReady;

        if (ConfirmButton != null)
        {
            ConfirmButton.interactable = canReady;
            Image bg = ConfirmButton.GetComponent<Image>();
            ButtonTint.Paint(bg, me.Ready ? ReadyColor : IdleColor);
            if (ConfirmLabel != null)
                ConfirmLabel.text = Loc.T(me.Ready ? "lobby.ready" : "lobby.confirm");
        }

        // Salir: siendo host, eso cierra la sala para todos; el botón lo dice y pregunta antes.
        if (LeaveLabel != null)
            LeaveLabel.text = Loc.T(!LobbyManager.LocalIsHost ? "lobby.leave"
                                  : _leaveConfirm.Asking ? "lobby.leave_confirm" : "lobby.leave_host");

        if (StartButton != null)
        {
            // El Start solo lo ve quien puede apretarlo.
            bool isHost = LobbyManager.LocalIsHost;
            if (StartButton.gameObject.activeSelf != isHost)
                StartButton.gameObject.SetActive(isHost);

            Image bg = StartButton.GetComponent<Image>();
            ButtonTint.Paint(bg, allReady ? ReadyColor : IdleColor);
            if (StartLabel != null) StartLabel.color = allReady ? Color.black : Color.white;
        }

        // --- linea de estado ---
        // El host puede arrancar cuando se le cante, asi que el verde del Start no
        // alcanza: hay que DECIRLE cuantos faltan. Y al que todavia no confirmo, por que
        // no puede.
        if (StatusText != null)
        {
            int players = 0, ready = 0;
            foreach (LobbyEntry e in lobby.Entries)
            {
                if (e.Spectator || e.Team <= 0) continue;
                players++;
                if (e.Ready) ready++;
            }

            string text;
            Color   color;

            if (players == 0)
            {
                text  = Loc.T("lobby.status.nobody");
                color = PendingColor;
            }
            else if (ready >= players)
            {
                text  = LobbyManager.LocalIsHost
                      ? Loc.T("lobby.status.all_ready_host", ("players", players))
                      : Loc.T("lobby.status.all_ready_wait", ("players", players));
                color = ReadyColor;
            }
            else
            {
                int missing = players - ready;
                text  = missing == 1 ? Loc.T("lobby.status.one_missing", ("players", players))
                                     : Loc.T("lobby.status.missing", ("missing", missing), ("players", players));
                color = PendingColor;
            }

            // Lo que te toca hacer A VOS gana sobre el recuento: es la unica linea de
            // texto del panel, y sirve mas como instruccion que como marcador.
            if (inTeam && !canReady)
            {
                text  = Loc.T("lobby.status.choose_class");
                color = PendingColor;
            }
            else if (me.Spectator || me.Team <= 0)
            {
                text += "   " + Loc.T("lobby.status.spectating");
            }

            StatusText.text  = text;
            StatusText.color = color;
        }

        FollowStep();
    }

    // Pinta un lugar. Ocupado: ícono de clase + nombre. Libre: "Unirte" en un equipo,
    // "Espectador" en la franja de espectadores (que SÍ es un destino: es como te salís
    // de un equipo sin desconectarte).
    private void DrawSlot(UI_LobbySlot slot, LobbyEntry entry, bool occupied, int myId, int team)
    {
        bool isMine  = occupied && entry.ClientId == myId;
        bool isHost  = LobbyManager.LocalIsHost;
        bool isBot   = occupied && LobbyManager.IsBot(entry.ClientId);
        bool inTeam  = team > 0;

        slot.EntryId = occupied ? entry.ClientId : 0;

        // El recuadro solo se pinta cuando hay alguien: en un lugar libre, el fondo ya lo
        // pone el botón de Unirte, y dos recuadros encimados se ven sucios.
        if (slot.Background != null)
            slot.Background.color = !occupied ? Color.clear
                                  : isMine    ? OwnRowColor
                                              : RowColor;

        slot.Icon.gameObject.SetActive(occupied);
        slot.Label.gameObject.SetActive(occupied);
        slot.JoinButton.gameObject.SetActive(!occupied);

        // El "+" solo en un lugar LIBRE de equipo, y solo para el host. El "x" solo sobre
        // un bot, también solo para el host: a un jugador no se lo echa desde acá.
        slot.AddBotButton.gameObject.SetActive(!occupied && inTeam && isHost);
        slot.RemoveButton.gameObject.SetActive(isBot && isHost);

        if (occupied)
        {
            CharacterClassDefinition cls = ClassAt(entry.ClassIndex);

            // El ícono SIEMPRE se dibuja, tenga clase o no. Sin clase queda un recuadro
            // vacío pero visible: es el botón que hay que tocar para elegirla, y si se
            // apagaba no había nada que clickear — no se podía elegir clase, y sin clase
            // el Confirmar quedaba muerto.
            slot.Icon.enabled = true;
            slot.Icon.sprite  = cls != null ? cls.ClassIcon : null;
            ButtonTint.Paint(slot.Icon, cls != null && cls.ClassIcon != null ? Color.white : SlotColor);

            // Tu ícono lo tocás vos; el de un bot, el host. Nadie toca el de otro jugador.
            slot.IconButton.interactable = inTeam && (isMine || (isBot && isHost));

            bool needsClass = entry.ClassIndex < 0 && inTeam;
            slot.Label.text = (isMine || (isBot && isHost)) && needsClass
                            ? Loc.T("lobby.choose_class", ("name", entry.PlayerName))
                            : entry.PlayerName;

            // Los bots van en otro color aunque estén listos: en una captura conviene ver
            // de un vistazo quién es persona y quién no.
            slot.Label.color = isBot   ? BotColor
                             : entry.Ready ? ReadyColor
                                           : PendingColor;
            return;
        }

        slot.JoinButton.interactable = true;

        if (slot.JoinLabel != null) slot.JoinLabel.text = Loc.T(inTeam ? "lobby.join" : "lobby.spectate");
    }

    private List<LobbyEntry> MembersOf(LobbyManager lobby, int team)
    {
        var list = new List<LobbyEntry>();
        foreach (LobbyEntry e in lobby.Entries)
        {
            bool isSpectator = e.Spectator || e.Team <= 0;
            if (team == 0 ? isSpectator : (!isSpectator && e.Team == team)) list.Add(e);
        }
        return list;
    }

    // Pública: la sala la consulta al arrancar para saber con qué clase spawnear cada bot.
    public CharacterClassDefinition ClassAt(int index)
        => SelectableClasses != null && index >= 0 && index < SelectableClasses.Length
           ? SelectableClasses[index] : null;

    private string CurrentName()
    {
        string n = NameInput != null ? NameInput.text : null;
        return string.IsNullOrWhiteSpace(n) ? Loc.T("lobby.default_name") : n.Trim();
    }

    // =========================================================
    // CONECTAR LAS PIEZAS (una vez, al empezar)
    // =========================================================

    private void Build()
    {
        if (_built) return;
        _built = true;

        // Nace APAGADO: la sala no existe hasta que hay conexion, y hasta entonces lo
        // unico que va en pantalla es el recuadro para hostear o conectarse.
        if (LobbyCanvas != null) LobbyCanvas.SetActive(false);

        if (StartButton != null)   { StartButton.onClick.AddListener(AudioManager.Click);   StartButton.onClick.AddListener(RequestStart); }
        if (ConfirmButton != null) { ConfirmButton.onClick.AddListener(AudioManager.Click); ConfirmButton.onClick.AddListener(ToggleReady); }
        if (LeaveButton != null)   { LeaveButton.onClick.AddListener(AudioManager.Click);   LeaveButton.onClick.AddListener(Leave); }

        if (NameInput != null)
        {
            NameInput.text = Loc.T("lobby.default_name");
            // Al TERMINAR de escribir, no en cada tecla: si no, cada letra sería un pedido
            // al servidor y un rebote por "nombre repetido" a mitad de palabra.
            NameInput.onEndEdit.AddListener(OnNameChanged);
            UI_OnScreenKeyboard.SetTitle(NameInput, "keyboard.name");   // con control se escribe con el teclado en pantalla
        }

        // Los lugares, por columna y en el orden de la jerarquía.
        for (int t = 0; t < _teamSlots.Length; t++) _teamSlots[t] = new List<UI_LobbySlot>();
        Transform root = Panel != null ? Panel : transform;
        foreach (UI_LobbySlot slot in root.GetComponentsInChildren<UI_LobbySlot>(true))
        {
            if (slot.Team <= 0) { _spectatorSlots.Add(slot); WireSlot(slot); continue; }
            if (slot.Team <= _teamSlots.Length) { _teamSlots[slot.Team - 1].Add(slot); WireSlot(slot); }
        }

        BuildClassPicker();
    }

    // Los botones de un lugar. Se registran UNA vez; sobre quién actúan lo dice EntryId,
    // que se reescribe en cada redibujado.
    private void WireSlot(UI_LobbySlot slot)
    {
        int team = slot.Team;
        if (slot.JoinButton != null)
        {
            slot.JoinButton.onClick.AddListener(AudioManager.Click);
            slot.JoinButton.onClick.AddListener(() => JoinTeam(team));
        }
        if (slot.IconButton != null)   slot.IconButton.onClick.AddListener(() => ShowClassPickerFor(slot.EntryId));
        if (slot.AddBotButton != null) { slot.AddBotButton.onClick.AddListener(AudioManager.Click); slot.AddBotButton.onClick.AddListener(() => AddBot(team)); }
        if (slot.RemoveButton != null) { slot.RemoveButton.onClick.AddListener(AudioManager.Click); slot.RemoveButton.onClick.AddListener(() => RemoveBot(slot.EntryId)); }
    }

    // La franja de espectadores no tiene cupo: si entran más de los armados, se copia el
    // último lugar y se pone debajo, a la misma distancia que hay entre los dos primeros.
    private void EnsureSpectatorRows(int needed)
    {
        if (_spectatorSlots.Count == 0) return;

        while (_spectatorSlots.Count < needed)
        {
            UI_LobbySlot last = _spectatorSlots[_spectatorSlots.Count - 1];
            RectTransform lastRt = (RectTransform)last.transform;

            Vector2 step = _spectatorSlots.Count >= 2
                ? lastRt.anchoredPosition - ((RectTransform)_spectatorSlots[_spectatorSlots.Count - 2].transform).anchoredPosition
                : new Vector2(0f, -(lastRt.sizeDelta.y + 8f));

            UI_LobbySlot copy = Instantiate(last, last.transform.parent);
            copy.name = $"Slot_Spec_{_spectatorSlots.Count}";
            ((RectTransform)copy.transform).anchoredPosition = lastRt.anchoredPosition + step;
            _spectatorSlots.Add(copy);
            WireSlot(copy);
        }
    }

    // La grilla de clases: íconos arriba, y debajo el nombre y la descripción de la que
    // estás mirando.
    //
    // ELEGIR NO ES CONFIRMAR. Tocar un ícono solo la SELECCIONA: se resalta y aparece su
    // descripción, y recién el botón Confirm la aplica.
    //
    // El nombre sigue al MOUSE (pasar por encima alcanza, sin tocar nada) y la
    // descripción sigue a la SELECCIÓN.
    //
    // AGRUPADAS POR ROL (8 de octubre, pedido de Gustavo). Arriba, una pestaña por rol
    // —Tanks, Damage, Supports, y Other si alguna clase no tiene— y debajo solo las clases
    // del rol abierto. Una clase base no tiene rol: va con el de sus subclases
    // (CharacterClassDefinition.DisplayRole). El índice que se manda a la sala sigue siendo
    // la posición en SelectableClasses: agrupar solo cambia dónde se DIBUJA cada ícono.
    //
    // Las pestañas y los íconos se copian de sus MOLDES: la altura de la fila y el tamaño
    // son los del molde; acá solo se reparten a lo ancho.
    private void BuildClassPicker()
    {
        if (ClassPicker == null) return;

        if (PickerCloseButton != null) PickerCloseButton.onClick.AddListener(() => ShowClassPicker(false));
        if (PickerConfirm != null)
        {
            PickerConfirm.onClick.AddListener(AudioManager.Click);
            PickerConfirm.onClick.AddListener(ConfirmClassSelection);
        }

        int count = SelectableClasses != null ? SelectableClasses.Length : 0;

        // Los grupos, en el orden de los menús; solo los que tienen alguna clase.
        _roleGroups.Clear();
        _roleMembers.Clear();
        _roleTabs.Clear();
        for (int i = 0; i < count; i++)
        {
            EClassRole role = RoleOf(i);
            if (!_roleMembers.TryGetValue(role, out List<int> members))
                _roleMembers[role] = members = new List<int>();
            members.Add(i);
        }
        foreach (EClassRole role in ClassRoleStyle.MenuOrder)
            if (_roleMembers.ContainsKey(role)) _roleGroups.Add(role);

        int widest = 1;
        foreach (List<int> members in _roleMembers.Values) widest = Mathf.Max(widest, members.Count);

        RectTransform tabRt  = TabTemplate != null ? (RectTransform)TabTemplate.transform : null;
        RectTransform iconRt = ClassIconTemplate != null ? (RectTransform)ClassIconTemplate.transform : null;
        float tabW      = tabRt != null ? tabRt.sizeDelta.x : 150f;
        float iconSize  = iconRt != null ? iconRt.sizeDelta.x : 84f;
        float step      = iconSize + IconGap;
        float tabsWidth = _roleGroups.Count * tabW + Mathf.Max(0, _roleGroups.Count - 1) * TabGap;

        // El ancho es el del grupo MÁS grande, para que el recuadro no cambie al cambiar de
        // pestaña. Nunca más angosto que como está armado.
        float needed = Mathf.Max(widest * step, tabsWidth) + PickerMargin;
        if (needed > ClassPicker.sizeDelta.x) ClassPicker.sizeDelta = new Vector2(needed, ClassPicker.sizeDelta.y);

        // Las pestañas de rol, del color de su rol.
        if (TabTemplate != null)
        {
            TabTemplate.gameObject.SetActive(false);
            for (int g = 0; g < _roleGroups.Count; g++)
            {
                int capturedGroup = g;
                EClassRole role = _roleGroups[g];
                int memberCount = _roleMembers[role].Count;

                Button tab = Instantiate(TabTemplate, TabTemplate.transform.parent);
                tab.name = $"Tab_{role}";
                tab.gameObject.SetActive(true);
                RectTransform rt = (RectTransform)tab.transform;
                rt.anchoredPosition = new Vector2(-tabsWidth * 0.5f + tabW * 0.5f + g * (tabW + TabGap), tabRt.anchoredPosition.y);

                Image bg = tab.targetGraphic as Image;
                ButtonTint.Paint(bg, ClassRoleStyle.Color(role));

                TMP_Text label = tab.GetComponentInChildren<TMP_Text>(true);
                if (label != null) LocalizedText.Bind(label, () => $"{ClassRoleStyle.GroupName(role)} ({memberCount})");

                tab.onClick.AddListener(AudioManager.Click);
                tab.onClick.AddListener(() => OpenRoleGroup(capturedGroup));
                _roleTabs.Add(bg);
            }
        }

        // Los íconos: cada uno se centra dentro de SU grupo (solo se ve el grupo abierto).
        _classIcons = new Image[count];
        if (ClassIconTemplate != null)
        {
            ClassIconTemplate.gameObject.SetActive(false);
            for (int i = 0; i < count; i++)
            {
                int captured = i;
                CharacterClassDefinition cls = SelectableClasses[i];

                List<int> group = _roleMembers[RoleOf(i)];
                int slot = group.IndexOf(i);

                Button b = Instantiate(ClassIconTemplate, ClassIconTemplate.transform.parent);
                b.name = $"Class_{i}";
                b.gameObject.SetActive(true);
                RectTransform rt = (RectTransform)b.transform;
                rt.anchoredPosition = new Vector2((slot - (group.Count - 1) * 0.5f) * step, iconRt.anchoredPosition.y);

                Image icon = b.targetGraphic as Image;
                if (icon == null) icon = b.GetComponent<Image>();
                if (icon != null)
                {
                    icon.sprite = cls != null ? cls.ClassIcon : null;
                    icon.preserveAspect = true;
                }
                _classIcons[i] = icon;

                b.onClick.AddListener(() => SelectClassInPicker(captured));

                // Pasar el mouse por encima muestra el nombre, sin elegir nada.
                EventTrigger trigger = b.GetComponent<EventTrigger>();
                if (trigger == null) trigger = b.gameObject.AddComponent<EventTrigger>();
                AddTriggerEntry(trigger, EventTriggerType.PointerEnter, () => ShowClassName(captured));
                AddTriggerEntry(trigger, EventTriggerType.PointerExit,  () => ShowClassName(_pickerSelection));

                // Con control, lo mismo al pasar por encima con el stick.
                AddTriggerEntry(trigger, EventTriggerType.Select,   () => ShowClassName(captured));
                AddTriggerEntry(trigger, EventTriggerType.Deselect, () => ShowClassName(_pickerSelection));
            }
        }

        ClassPicker.gameObject.SetActive(false);
    }

    private static void AddTriggerEntry(EventTrigger trigger, EventTriggerType type, System.Action action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    // El rol con el que se agrupa la clase en esa posición de SelectableClasses.
    private EClassRole RoleOf(int index)
    {
        CharacterClassDefinition cls = ClassAt(index);
        return cls != null ? cls.DisplayRole : EClassRole.None;
    }

    // Abre la pestaña de un rol: muestra solo sus clases y resalta la pestaña (las otras
    // quedan apagadas, mezcladas con el fondo).
    private void OpenRoleGroup(int group)
    {
        if (group < 0 || group >= _roleGroups.Count) return;
        EClassRole open = _roleGroups[group];

        if (_classIcons != null)
            for (int i = 0; i < _classIcons.Length; i++)
                if (_classIcons[i] != null) _classIcons[i].gameObject.SetActive(RoleOf(i) == open);

        for (int g = 0; g < _roleTabs.Count; g++)
        {
            if (_roleTabs[g] == null) continue;
            Color c = ClassRoleStyle.Color(_roleGroups[g]);
            ButtonTint.Paint(_roleTabs[g], g == group ? c : Color.Lerp(c, PanelColor, 0.7f));
        }
    }

    // Solo mira: escribe el nombre de esa clase (o lo limpia con índice inválido).
    private void ShowClassName(int index)
    {
        CharacterClassDefinition cls = ClassAt(index);
        if (ClassNameLabel != null) ClassNameLabel.text = cls != null ? cls.DisplayName : "";
    }

    // Selecciona sin aplicar: resalta el ícono y muestra su descripción.
    private void SelectClassInPicker(int index)
    {
        _pickerSelection = index;
        ShowClassName(index);

        CharacterClassDefinition cls = ClassAt(index);
        if (ClassDescLabel != null) ClassDescLabel.text = cls != null ? cls.DisplayDescription : "";

        // El resaltado es el propio ícono, más brillante. Los demás quedan apagados, así
        // se ve de un vistazo cuál está elegida.
        if (_classIcons != null)
            for (int i = 0; i < _classIcons.Length; i++)
                if (_classIcons[i] != null)
                    ButtonTint.Paint(_classIcons[i], i == index ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f));

        if (PickerConfirm != null) PickerConfirm.interactable = cls != null;
    }

    // Recién acá se aplica de verdad.
    private void ConfirmClassSelection()
    {
        if (ClassAt(_pickerSelection) == null) return;
        ChooseClass(_pickerSelection);
    }

    private void ShowClassPicker(bool show)
    {
        if (ClassPicker == null) return;

        // Al abrirlo, arrancar en la clase que ya tenés puesta: así el selector muestra
        // de entrada qué elegiste y se puede confirmar sin volver a tocar el ícono.
        if (show)
        {
            int current = -1;
            LobbyManager lobby = LobbyManager.Instance;
            if (lobby != null && !LobbyManager.IsBot(_pickerTarget)
                && lobby.TryGetLocalEntry(out LobbyEntry me)) current = me.ClassIndex;

            // Se abre en la pestaña del rol de esa clase (o en la primera, sin clase).
            int group = ClassAt(current) != null ? _roleGroups.IndexOf(RoleOf(current)) : -1;
            OpenRoleGroup(group >= 0 ? group : 0);

            if (ClassAt(current) != null) SelectClassInPicker(current);
            else
            {
                _pickerSelection = -1;
                ShowClassName(-1);
                if (ClassDescLabel != null) ClassDescLabel.text = "";
                if (PickerConfirm != null) PickerConfirm.interactable = false;
                if (_classIcons != null)
                    foreach (Image icon in _classIcons)
                        ButtonTint.Paint(icon, Color.white);
            }
        }

        ClassPicker.gameObject.SetActive(show);

        // Con control: al abrirlo, el ícono de la clase que tenías (o el primero); al cerrarlo,
        // lo que toque en la sala (tu ícono, Confirmar). Con el mouse no hace falta.
        if (MenuInput.UsingPad) MenuInput.Select(DefaultSelection);
    }

    // =========================================================
    // IMenuScreen
    // =========================================================

    public int MenuLayer => 0;

    private bool PickerOpen => ClassPicker != null && ClassPicker.gameObject.activeInHierarchy;

    // Con control, a dónde va la selección según lo que te falta: unirte a un equipo, elegir
    // tu clase, Confirmar y, siendo host y ya listo, Start. Con el selector abierto, la clase
    // que estás mirando (o la primera de la pestaña).
    public GameObject DefaultSelection
    {
        get
        {
            if (PickerOpen)
            {
                if (_classIcons != null)
                {
                    if (_pickerSelection >= 0 && _pickerSelection < _classIcons.Length && Usable(_classIcons[_pickerSelection]))
                        return _classIcons[_pickerSelection].gameObject;
                    foreach (Image icon in _classIcons)
                        if (Usable(icon)) return icon.gameObject;
                }
                return PickerConfirm != null ? PickerConfirm.gameObject : null;
            }

            LobbyManager lobby = LobbyManager.Instance;
            if (lobby != null && lobby.TryGetLocalEntry(out LobbyEntry me) && !me.Spectator && me.Team > 0)
            {
                if (me.ClassIndex < 0)
                {
                    UI_LobbySlot mine = SlotOf(me.ClientId);
                    if (mine != null && Usable(mine.IconButton)) return mine.IconButton.gameObject;
                }
                if (me.Ready && LobbyManager.LocalIsHost && Usable(StartButton)) return StartButton.gameObject;
                if (Usable(ConfirmButton)) return ConfirmButton.gameObject;
            }

            // De espectador: el primer "Unirte" libre.
            foreach (List<UI_LobbySlot> team in _teamSlots)
            {
                if (team == null) continue;
                foreach (UI_LobbySlot slot in team)
                    if (slot != null && Usable(slot.JoinButton)) return slot.JoinButton.gameObject;
            }

            return ConfirmButton != null ? ConfirmButton.gameObject : null;
        }
    }

    public Transform SelectionRoot =>
        PickerOpen ? ClassPicker : LobbyCanvas != null ? LobbyCanvas.transform : transform;

    // B cierra el selector de clase; en la sala no hay a dónde volver (se sale por la pausa).
    public void OnBack()
    {
        if (PickerOpen) ShowClassPicker(false);
    }

    public void OnPause() => UI_PauseMenu.Get()?.Open();

    private UI_LobbySlot SlotOf(int entryId)
    {
        foreach (List<UI_LobbySlot> team in _teamSlots)
        {
            if (team == null) continue;
            foreach (UI_LobbySlot slot in team)
                if (slot != null && slot.EntryId == entryId) return slot;
        }
        return null;
    }

    // Con control, la selección sigue al paso que toca: si estaba en el paso anterior (Unirte,
    // tu ícono, Confirmar) o se apagó, pasa al siguiente. Si la llevaste a otro lado (el "+" de
    // un bot), se queda ahí.
    private GameObject _lastStep;

    private void FollowStep()
    {
        if (!MenuInput.UsingPad || PickerOpen || !ReferenceEquals(MenuInput.Top, this)) { _lastStep = null; return; }

        GameObject next = DefaultSelection;
        GameObject current = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        bool onLastStep = current == null || !current.activeInHierarchy || current == _lastStep;
        if (next != _lastStep && onLastStep) MenuInput.Select(next);
        _lastStep = next;
    }

    // Prendido y se puede apretar.
    private static bool Usable(Component c)
    {
        if (c == null || !c.gameObject.activeInHierarchy) return false;
        Selectable s = c as Selectable;
        if (s == null) s = c.GetComponent<Selectable>();
        return s == null || s.IsInteractable();
    }
}
