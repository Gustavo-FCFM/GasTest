using System.Collections;
using System.Collections.Generic;
using FishNet;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_LobbyPanel
//
// La sala de espera entera en un solo panel: IP, nombre, los tres equipos con sus
// lugares, la franja de espectadores, el selector de clase y los botones de Confirmar
// y Start.
//
// SE DIBUJA POR CÓDIGO, como el resto del HUD del modo (ver MercUIFactory). No hay
// prefab que armar ni referencias que cablear: se pone el componente en cualquier
// GameObject de la escena y listo. Lo único que pide el inspector son las clases
// elegibles y los colores.
//
// CÓMO FUNCIONA EL FLUJO:
//   1. Entrás → te anotás con tu nombre y quedás de ESPECTADOR.
//   2. Te unís a un equipo tocando un lugar libre → dejás de ser espectador.
//   3. Elegís clase tocando tu propio ícono → se despliega la grilla de clases.
//   4. Confirmar te marca LISTO (tu nombre se pone verde). Volver a tocarlo lo apaga.
//   5. El HOST aprieta Start y arranca la partida para todos.
//
// EL PERSONAJE NO APARECE AL CONFIRMAR, sino cuando la partida arranca. Si no, el que
// confirma primero se queda dando vueltas por el mapa mientras los demás todavía
// eligen — que es justo lo que pasaba antes.
//
// AUTORIDAD: este panel solo PIDE (ServerSubmit / ServerSetReady / ServerRequestStart).
// Quién está en qué equipo, si un nombre se repite y si un equipo está lleno lo decide
// el servidor en LobbyManager; acá solo se dibuja lo que la sala sincronizada dice.
// ============================================================
public class UI_LobbyPanel : MonoBehaviour
{
    [Header("Clases elegibles")]
    [Tooltip("Las mismas que MainBaseClasses del Player. El ícono de cada una sale de su " +
             "ClassIcon, y el índice que se manda a la sala es la posición en esta lista.")]
    public CharacterClassDefinition[] SelectableClasses;

    [Header("Aspecto")]
    [Tooltip("Medidas de la MAQUETA, sobre un canvas de referencia de 1920x1080. Lo que " +
             "se ve en pantalla es esto multiplicado por PanelScale.")]
    public Vector2 PanelSize    = new Vector2(1180f, 660f);
    public float   ColumnWidth  = 250f;
    public float   RowHeight    = 56f;

    [Tooltip("Cuánto se agranda el panel entero — fondo, columnas, textos y botones, todo " +
             "junto y en proporción. 1 es la maqueta original, que ocupaba poco más de la " +
             "mitad de la pantalla. Con 1.35 llega al 80% y se lee bien de lejos.")]
    [Range(0.5f, 2f)]
    public float PanelScale = 1.35f;

    [Tooltip("Tamaño del ícono de clase en cada lugar de equipo. Se recorta al alto de la " +
             "fila, así que para íconos de verdad más grandes hay que subir también " +
             "RowHeight (el ícono nunca pasa de RowHeight - 8).")]
    public float SlotIconSize  = 40f;

    [Tooltip("Tamaño de los íconos en la grilla de elegir clase. El recuadro de la grilla " +
             "crece solo con ellos.")]
    public float ClassIconSize = 84f;

    [Tooltip("Fondo del panel. La grilla de clases usa este mismo color pero SIN " +
             "transparencia, para que no se lea el panel de atrás mientras elegís.")]
    public Color PanelColor   = new Color(0.09f, 0.09f, 0.11f, 0.97f);
    public Color SlotColor    = new Color(0.20f, 0.24f, 0.32f, 1f);
    public Color ReadyColor   = new Color(0.55f, 0.95f, 0.55f, 1f);
    public Color PendingColor = new Color(0.86f, 0.88f, 0.92f, 1f);
    [Tooltip("Color del botón de Start y de Confirmar cuando TODAVÍA no corresponde " +
             "apretarlos. Verde cuando sí.")]
    public Color IdleColor    = new Color(0.17f, 0.24f, 0.40f, 1f);

    [Tooltip("Fondo de una fila OCUPADA: el recuadro detrás del ícono y el nombre. Las " +
             "filas libres no lo llevan — ahí el fondo lo pone el propio botón de Unirte.")]
    public Color RowColor     = new Color(0.16f, 0.19f, 0.26f, 1f);

    [Tooltip("Fondo de TU propia fila. Sirve para encontrarte de un vistazo cuando la " +
             "sala está llena.")]
    public Color OwnRowColor  = new Color(0.22f, 0.29f, 0.42f, 1f);

    [Tooltip("Color del nombre de los BOTS. Distinto del de un jugador listo a propósito: " +
             "en una captura tiene que verse de un vistazo quién es persona y quién no.")]
    public Color BotColor     = new Color(0.72f, 0.66f, 0.95f, 1f);

    // --- construidos en runtime ---
    private Canvas          _canvas;
    private RectTransform   _root;
    private TMP_InputField  _nameInput;
    private TextMeshProUGUI _ipText;
    private Button          _startButton;
    private TextMeshProUGUI _startLabel;
    private Button          _confirmButton;
    private TextMeshProUGUI _confirmLabel;
    private RectTransform   _classPicker;
    private TextMeshProUGUI _statusText;

    // Un lugar de equipo dibujado: su fondo, su ícono de clase y su etiqueta.
    private class Slot
    {
        public RectTransform   Root;
        public Image           Background;   // el recuadro de la fila; solo se pinta si está ocupada
        public Image           Icon;
        public Button          IconButton;   // solo el TUYO abre el selector de clase
        public Button          JoinButton;
        public Button          AddBotButton;   // "+" en un lugar libre: solo lo ve el host
        public Button          RemoveButton;   // "x" sobre un bot: solo lo ve el host
        public TextMeshProUGUI Label;

        // Quién ocupa esta fila AHORA. Se reescribe en cada redibujado, y es lo que deja
        // que los listeners —registrados una sola vez, al construir— sepan sobre quién
        // actuar sin volver a suscribirse en cada refresh.
        public int EntryId;
    }

    private readonly List<Slot>[] _teamSlots = new List<Slot>[LobbyManager.TeamCount];
    private readonly List<Slot>   _spectatorSlots = new List<Slot>();

    // Cuántas filas de espectadores dibujar. Puede haber cualquier cantidad, así que se
    // dibuja un mínimo fijo y crece si hacen falta más.
    private const int MinSpectatorRows = 2;

    // Cupo por equipo. Sale de la sala, que es quien manda; si el panel se construye
    // antes de que exista, se usa el mismo default para no dibujar de menos.
    private static int MaxPerTeam =>
        LobbyManager.Instance != null ? LobbyManager.Instance.MaxPlayersPerTeam : 3;

    private bool  _spawnRequested;
    private bool  _registered;      // ya me anoté en la sala (aunque sea de espectador)
    private bool  _visible;         // el estado que el panel CREE tener; ver Update
    private float _redrawIn;        // segundos hasta el próximo redibujado de reloj

    // Se cachea: se lee para mostrar la IP y buscarlo cada frame es caro y no cambia.
    private ConnectionHUD _hud;

    private void Start()
    {
        Build();
        Refresh();
    }

    private void OnEnable()  { LobbyManager.OnLobbyChanged += Refresh; }
    private void OnDisable()
    {
        LobbyManager.OnLobbyChanged -= Refresh;
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

        if (_canvas != null && _visible != show)
        {
            _visible = show;
            _canvas.gameObject.SetActive(show);
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
        if (lobby == null || _root == null) return;

        lobby.TryGetLocalEntry(out LobbyEntry me);
        int myId = me.ClientId;

        // --- IP ---
        if (_ipText != null)
        {
            if (_hud == null) _hud = FindFirstObjectByType<ConnectionHUD>();
            _ipText.text = $"IP del Host: {(_hud != null ? _hud.HostAddress : "—")}";
        }

        // --- equipos ---
        for (int t = 0; t < LobbyManager.TeamCount; t++)
        {
            int team = t + 1;
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
            Slot slot = _spectatorSlots[s];
            bool used = s <= specs.Count;
            if (slot.Root.gameObject.activeSelf != used) slot.Root.gameObject.SetActive(used);
            if (!used) continue;

            bool occupied = s < specs.Count;
            DrawSlot(slot, occupied ? specs[s] : default, occupied, myId, 0);
        }

        // --- botones ---
        bool inTeam   = !me.Spectator && me.Team > 0;
        bool canReady = inTeam && me.ClassIndex >= 0;
        bool allReady = lobby.AllReady;

        if (_confirmButton != null)
        {
            _confirmButton.interactable = canReady;
            Image bg = _confirmButton.GetComponent<Image>();
            if (bg != null) bg.color = me.Ready ? ReadyColor : IdleColor;
            if (_confirmLabel != null)
                _confirmLabel.text = me.Ready ? "Listo" : "Confirmar";
        }

        if (_startButton != null)
        {
            // El Start solo lo ve quien puede apretarlo.
            bool isHost = LobbyManager.LocalIsHost;
            if (_startButton.gameObject.activeSelf != isHost)
                _startButton.gameObject.SetActive(isHost);

            Image bg = _startButton.GetComponent<Image>();
            if (bg != null) bg.color = allReady ? ReadyColor : IdleColor;
            if (_startLabel != null) _startLabel.color = allReady ? Color.black : Color.white;
        }

        // --- linea de estado ---
        // El host puede arrancar cuando se le cante, asi que el verde del Start no
        // alcanza: hay que DECIRLE cuantos faltan. Y al que todavia no confirmo, por que
        // no puede.
        if (_statusText != null)
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
                text  = "No hay nadie en un equipo todavía.";
                color = PendingColor;
            }
            else if (ready >= players)
            {
                text  = LobbyManager.LocalIsHost
                      ? $"Los {players} están listos — podés arrancar."
                      : $"Los {players} están listos — esperando al host.";
                color = ReadyColor;
            }
            else
            {
                int missing = players - ready;
                text  = missing == 1 ? $"Falta 1 de {players} por confirmar."
                                     : $"Faltan {missing} de {players} por confirmar.";
                color = PendingColor;
            }

            // Lo que te toca hacer A VOS gana sobre el recuento: es la unica linea de
            // texto del panel, y sirve mas como instruccion que como marcador.
            if (inTeam && !canReady)
            {
                text  = "Tocá tu recuadro para elegir clase.";
                color = PendingColor;
            }
            else if (me.Spectator || me.Team <= 0)
            {
                text += "   (sos espectador — toca Unirte para jugar)";
            }

            _statusText.text  = text;
            _statusText.color = color;
        }
    }

    // Pinta un lugar. Ocupado: ícono de clase + nombre. Libre: "Unirte" en un equipo,
    // "Espectador" en la franja de espectadores (que SÍ es un destino: es como te salís
    // de un equipo sin desconectarte).
    private void DrawSlot(Slot slot, LobbyEntry entry, bool occupied, int myId, int team)
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
            slot.Icon.color   = cls != null && cls.ClassIcon != null ? Color.white : SlotColor;

            // Tu ícono lo tocás vos; el de un bot, el host. Nadie toca el de otro jugador.
            slot.IconButton.interactable = inTeam && (isMine || (isBot && isHost));

            bool needsClass = entry.ClassIndex < 0 && inTeam;
            slot.Label.text = (isMine || (isBot && isHost)) && needsClass
                            ? $"{entry.PlayerName}  ← elegí clase"
                            : entry.PlayerName;

            // Los bots van en otro color aunque estén listos: en una captura conviene ver
            // de un vistazo quién es persona y quién no.
            slot.Label.color = isBot   ? BotColor
                             : entry.Ready ? ReadyColor
                                           : PendingColor;
            return;
        }

        slot.JoinButton.interactable = true;

        TextMeshProUGUI joinLabel = slot.JoinButton.GetComponentInChildren<TextMeshProUGUI>();
        if (joinLabel != null) joinLabel.text = inTeam ? "Unirte" : "Espectador";
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
        string n = _nameInput != null ? _nameInput.text : null;
        return string.IsNullOrWhiteSpace(n) ? "Jugador" : n.Trim();
    }

    // =========================================================
    // CONSTRUCCIÓN
    // =========================================================

    private void Build()
    {
        _canvas = MercUIFactory.CreateCanvas("Canvas_LobbyPanel", 70);

        _root = MercUIFactory.CreateRect(_canvas.transform, "Panel",
                                         new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                         new Vector2(0.5f, 0.5f), Vector2.zero, PanelSize);

        // Todo el panel se dibuja con medidas fijas (es una maqueta, no un layout), así
        // que agrandarlo es una escala sobre la raíz: crecen el fondo, las columnas, los
        // textos y los botones a la vez, sin volver a acomodar nada.
        _root.localScale = Vector3.one * Mathf.Max(0.5f, PanelScale);

        Image panelBg = _root.gameObject.AddComponent<Image>();
        panelBg.color = PanelColor;

        BuildHeader();
        BuildNameRow();
        BuildColumns();
        BuildConfirm();
        BuildClassPicker();

        // Nace APAGADO: la sala no existe hasta que hay conexion, y hasta entonces lo
        // unico que va en pantalla es el recuadro para hostear o conectarse.
        _canvas.gameObject.SetActive(false);
    }

    private void BuildHeader()
    {
        _ipText = MercUIFactory.CreateText(_root, "IP", "IP del Host: —", 24f, Color.white,
                                           TextAlignmentOptions.Center,
                                           new Vector2(0f, -40f), new Vector2(600f, 40f),
                                           new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                           new Vector2(0.5f, 1f));

        _startButton = CreateButton(_root, "StartButton", "Start", IdleColor,
                                    new Vector2(0.5f, 1f), new Vector2(420f, -40f),
                                    new Vector2(150f, 44f), out _startLabel);
        _startButton.onClick.AddListener(RequestStart);
    }

    private void BuildNameRow()
    {
        // Etiqueta y campo se posicionan CALCULADOS y no a ojo: puestos a mano el rect
        // del texto terminaba 10 px adentro del campo y la palabra quedaba pegada al
        // borde. Las tres medidas de acá abajo son lo único que hay que tocar.
        const float labelW = 200f;
        const float inputW = 260f;
        const float gap    = 24f;
        const float rowY   = -95f;

        // La fila entera va centrada en el panel: se reparte desde su borde izquierdo.
        float left    = -(labelW + gap + inputW) * 0.5f;
        float labelX  = left + labelW * 0.5f;
        float inputX  = left + labelW + gap + inputW * 0.5f;

        MercUIFactory.CreateText(_root, "NameLabel", "Username:", 22f, Color.white,
                                 TextAlignmentOptions.Right,
                                 new Vector2(labelX, rowY), new Vector2(labelW, 36f),
                                 new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                 new Vector2(0.5f, 1f));

        RectTransform inputRect = MercUIFactory.CreateRect(
            _root, "NameInput", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(inputX, rowY), new Vector2(inputW, 36f));

        Image inputBg = inputRect.gameObject.AddComponent<Image>();
        inputBg.color = SlotColor;

        TextMeshProUGUI text = MercUIFactory.CreateText(
            inputRect, "Text", "", 20f, Color.white, TextAlignmentOptions.Left,
            new Vector2(12f, 0f), new Vector2(inputW - 24f, 32f));

        _nameInput = inputRect.gameObject.AddComponent<TMP_InputField>();
        _nameInput.textComponent = text;
        _nameInput.text          = "Jugador";
        _nameInput.characterLimit = 16;

        // Al TERMINAR de escribir, no en cada tecla: si no, cada letra sería un pedido
        // al servidor y un rebote por "nombre repetido" a mitad de palabra.
        _nameInput.onEndEdit.AddListener(OnNameChanged);
    }

    private void BuildColumns()
    {
        float startX = -(ColumnWidth * 1.5f);
        const float headerY = -160f;

        for (int t = 0; t < LobbyManager.TeamCount; t++)
        {
            int team = t + 1;
            float x = startX + ColumnWidth * t;

            CreateColumnHeader($"Equipo {team}", MercUIFactory.TeamColor(team), x, headerY);

            _teamSlots[t] = new List<Slot>();
            for (int s = 0; s < MaxPerTeam; s++)
            {
                int capturedTeam = team;
                Slot slot = CreateSlot($"T{team}_S{s}", x, headerY - 56f - RowHeight * s);
                Slot capturedSlot = slot;

                slot.JoinButton.onClick.AddListener(() => JoinTeam(capturedTeam));
                slot.IconButton.onClick.AddListener(() => ShowClassPickerFor(capturedSlot.EntryId));
                slot.AddBotButton.onClick.AddListener(() => AddBot(capturedTeam));
                slot.RemoveButton.onClick.AddListener(() => RemoveBot(capturedSlot.EntryId));

                _teamSlots[t].Add(slot);
            }
        }

        float specX = startX + ColumnWidth * LobbyManager.TeamCount;
        CreateColumnHeader("Espectadores", new Color(0.55f, 0.57f, 0.62f, 1f), specX, headerY);

        for (int s = 0; s < MinSpectatorRows; s++)
            AddSpectatorRow(specX, headerY - 56f - RowHeight * s);
    }

    // La franja de espectadores no tiene cupo: si entran más de los dibujados, se
    // agregan filas al vuelo.
    private void EnsureSpectatorRows(int needed)
    {
        float startX = -(ColumnWidth * 1.5f);
        float specX  = startX + ColumnWidth * LobbyManager.TeamCount;

        while (_spectatorSlots.Count < needed)
            AddSpectatorRow(specX, -160f - 56f - RowHeight * _spectatorSlots.Count);
    }

    // El botón de una fila de espectadores te SACA del equipo (equipo 0 = espectador).
    // Es el mismo camino que usa el panel para anotarte al entrar.
    private void AddSpectatorRow(float x, float y)
    {
        Slot slot = CreateSlot($"Spec_{_spectatorSlots.Count}", x, y);
        slot.JoinButton.onClick.AddListener(() => JoinTeam(0));
        _spectatorSlots.Add(slot);
    }

    private void CreateColumnHeader(string title, Color color, float x, float y)
    {
        RectTransform rect = MercUIFactory.CreateRect(
            _root, $"Header_{title}", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(ColumnWidth - 40f, 40f));

        Image bg = rect.gameObject.AddComponent<Image>();
        bg.color = color;

        MercUIFactory.CreateText(rect, "Text", title, 20f, Color.white,
                                 TextAlignmentOptions.Center,
                                 Vector2.zero, new Vector2(ColumnWidth - 44f, 36f),
                                 new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                 new Vector2(0.5f, 0.5f));
    }

    private Slot CreateSlot(string name, float x, float y)
    {
        var slot = new Slot();

        slot.Root = MercUIFactory.CreateRect(
            _root, $"Slot_{name}", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(ColumnWidth - 40f, RowHeight - 8f));

        // El recuadro de la fila. Va PRIMERO para que quede detrás de todo lo demás: el
        // orden de los hijos es el orden de dibujado en un Canvas.
        slot.Background = slot.Root.gameObject.AddComponent<Image>();
        slot.Background.color = RowColor;

        // Ícono de clase, a la izquierda. Es un botón: el TUYO abre el selector.
        //
        // El nombre arranca DESPUÉS del ícono más un aire: antes el rect del texto
        // empezaba 4 px adentro del ícono y quedaban pegados.
        const float iconX = 14f;
        const float gap   = 12f;

        // El ícono no puede ser más alto que la fila: si SlotIconSize se pasa, se recorta
        // (y para agrandarlo de verdad hay que subir RowHeight).
        float iconSize = Mathf.Clamp(SlotIconSize, 8f, RowHeight - 8f);
        float labelX   = iconX + iconSize + gap;

        RectTransform iconRect = MercUIFactory.CreateRect(
            slot.Root, "Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f), new Vector2(iconX, 0f), new Vector2(iconSize, iconSize));

        slot.Icon = iconRect.gameObject.AddComponent<Image>();
        slot.Icon.preserveAspect = true;
        slot.IconButton = iconRect.gameObject.AddComponent<Button>();
        slot.IconButton.targetGraphic = slot.Icon;

        slot.Label = MercUIFactory.CreateText(
            slot.Root, "Name", "", 18f, PendingColor, TextAlignmentOptions.Left,
            new Vector2(labelX, 0f), new Vector2(ColumnWidth - 40f - labelX - 8f, 32f));

        // El "Unirte" deja libre el borde derecho para el "+" de agregar un bot.
        const float sideW = 34f;

        slot.JoinButton = CreateButton(slot.Root, "Join", "Unirte", SlotColor,
                                       new Vector2(0f, 0.5f), new Vector2(14f, 0f),
                                       new Vector2(ColumnWidth - 100f - sideW, RowHeight - 14f), out _);

        // "+" — agregar un bot en este lugar. Solo lo ve el host (ver DrawSlot).
        slot.AddBotButton = CreateButton(slot.Root, "AddBot", "+", IdleColor,
                                         new Vector2(1f, 0.5f), new Vector2(-14f, 0f),
                                         new Vector2(sideW, RowHeight - 14f), out _);

        // "x" — sacar el bot que ocupa este lugar. Mismo sitio que el "+", porque nunca
        // se ven los dos a la vez: uno es para el lugar vacío y el otro para el ocupado.
        slot.RemoveButton = CreateButton(slot.Root, "RemoveBot", "x",
                                         new Color(0.45f, 0.20f, 0.22f, 1f),
                                         new Vector2(1f, 0.5f), new Vector2(-14f, 0f),
                                         new Vector2(sideW, RowHeight - 14f), out _);

        return slot;
    }

    private void BuildConfirm()
    {
        // Una sola línea de texto sobre el botón: dice qué falta para arrancar, y qué
        // te falta hacer a vos. Ver la parte final de Refresh().
        _statusText = MercUIFactory.CreateText(_root, "Status", "", 18f, PendingColor,
                                               TextAlignmentOptions.Center,
                                               new Vector2(0f, 100f), new Vector2(760f, 30f),
                                               new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                               new Vector2(0.5f, 0f));

        _confirmButton = CreateButton(_root, "ConfirmButton", "Confirmar", IdleColor,
                                      new Vector2(0.5f, 0f), new Vector2(0f, 46f),
                                      new Vector2(180f, 46f), out _confirmLabel);
        _confirmButton.onClick.AddListener(ToggleReady);
    }

    // La grilla de clases: SOLO íconos, y con fondo opaco a propósito — con el panel
    // traslúcido de atrás visible, elegir clase era un ruido visual.
    private void BuildClassPicker()
    {
        int count = SelectableClasses != null ? SelectableClasses.Length : 0;
        // La grilla se dimensiona a partir del ícono: paso = ícono + aire, y el recuadro
        // deja un margen alrededor. Así subir ClassIconSize agranda todo junto.
        float iconSize = Mathf.Max(24f, ClassIconSize);
        float step     = iconSize + 12f;
        float width    = Mathf.Max(140f, count * step + 32f);

        _classPicker = MercUIFactory.CreateRect(
            _root, "ClassPicker", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, iconSize + 46f));

        Image bg = _classPicker.gameObject.AddComponent<Image>();
        bg.color = new Color(PanelColor.r, PanelColor.g, PanelColor.b, 1f); // opaco

        // El fondo cierra el selector: si no, la unica salida era elegir una clase — y
        // si abriste la grilla sin querer te quedabas trabado ahi.
        Button close = _classPicker.gameObject.AddComponent<Button>();
        close.targetGraphic = bg;
        close.transition    = Selectable.Transition.None;
        close.onClick.AddListener(() => ShowClassPicker(false));

        for (int i = 0; i < count; i++)
        {
            int captured = i;
            CharacterClassDefinition cls = SelectableClasses[i];

            RectTransform iconRect = MercUIFactory.CreateRect(
                _classPicker, $"Class_{i}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2((i - (count - 1) * 0.5f) * step, 0f), new Vector2(iconSize, iconSize));

            Image icon = iconRect.gameObject.AddComponent<Image>();
            icon.sprite = cls != null ? cls.ClassIcon : null;
            icon.preserveAspect = true;

            Button b = iconRect.gameObject.AddComponent<Button>();
            b.targetGraphic = icon;
            b.onClick.AddListener(() => ChooseClass(captured));
        }

        _classPicker.gameObject.SetActive(false);
    }

    private void ShowClassPicker(bool show)
    {
        if (_classPicker != null) _classPicker.gameObject.SetActive(show);
    }

    // MercUIFactory no tiene botones (el HUD del modo no los necesitaba), así que se
    // arman acá: un Image de fondo + su Button + el texto encima.
    private Button CreateButton(Transform parent, string name, string label, Color color,
                                Vector2 anchor, Vector2 position, Vector2 size,
                                out TextMeshProUGUI labelText)
    {
        RectTransform rect = MercUIFactory.CreateRect(parent, name, anchor, anchor, anchor,
                                                      position, size);

        Image bg = rect.gameObject.AddComponent<Image>();
        bg.color = color;

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;

        labelText = MercUIFactory.CreateText(rect, "Text", label, 18f, Color.white,
                                             TextAlignmentOptions.Center,
                                             Vector2.zero, new Vector2(size.x - 8f, size.y - 6f),
                                             new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                             new Vector2(0.5f, 0.5f));
        return button;
    }
}
