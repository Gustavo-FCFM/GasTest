using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_LobbyRoster
//
// El panel de la SALA DE ESPERA: quién está conectado, en qué equipo, con qué clase y
// si ya está listo. Tres columnas (una por equipo) más una franja de espectadores.
//
// Se dibuja SOLO POR CÓDIGO, igual que el resto del HUD del modo (ver MercUIFactory):
// no hay prefab que cablear ni referencias que puedan quedar en None. Poné este
// componente en cualquier GameObject de la escena del lobby y listo.
//
// NO SINCRONIZA NADA: todo sale de LobbyManager, que ya replica la sala entera en una
// SyncList. Acá solo se redibuja cuando esa lista cambia (evento OnLobbyChanged), no
// cada frame — una sala de nueve personas cambia un puñado de veces por partida.
//
// LO QUE SE LEE DE UN VISTAZO, que es para lo que existe:
//   · un "?" donde falta elegir clase (o equipo), que es el problema que hace entrar
//     a alguien sin personaje armado;
//   · el tilde de listo, para saber a quién se está esperando;
//   · cuántos hay por equipo contra el cupo, para poder repartirse entre todos.
// ============================================================
public class UI_LobbyRoster : MonoBehaviour
{
    [Header("Aspecto")]
    [Tooltip("Alto de cada fila de jugador, en píxeles.")]
    public float RowHeight = 34f;

    [Tooltip("Ancho de cada columna de equipo.")]
    public float ColumnWidth = 240f;

    [Tooltip("Color de fondo de las columnas.")]
    public Color PanelColor = new Color(0.05f, 0.06f, 0.09f, 0.82f);

    [Tooltip("Color del texto de quien todavía no está listo.")]
    public Color PendingColor = new Color(0.72f, 0.74f, 0.78f, 1f);

    [Tooltip("Color del texto de quien ya confirmó.")]
    public Color ReadyColor = new Color(0.55f, 0.95f, 0.55f, 1f);

    [Tooltip("Alto visible de cada columna de equipo. Si entran más jugadores que este alto, la " +
             "columna scrollea en vez de desbordarse.")]
    public float ColumnHeight = 300f;

    private Canvas          _canvas;
    private RectTransform   _root;
    private RectTransform[] _teamColumns;
    private RectTransform[] _teamContents;   // dónde van las filas (adentro del scroll)
    private TextMeshProUGUI[] _teamHeaders;
    private RectTransform   _spectatorColumn;
    private RectTransform   _spectatorContent;
    private TextMeshProUGUI _spectatorHeader;
    private TextMeshProUGUI _statusText;

    // Alto del encabezado de cada columna: queda FUERA del área que scrollea, para que
    // el título del equipo no se vaya de pantalla al bajar la lista.
    private const float HeaderHeight = 34f;

    // Las filas vivas, para poder borrarlas al redibujar.
    private readonly List<GameObject> _rows = new List<GameObject>();

    private void Awake()
    {
        Build();
        SetVisible(false);
    }

    private void OnEnable()
    {
        LobbyManager.OnLobbyChanged += Redraw;
        Redraw();
    }

    private void OnDisable() => LobbyManager.OnLobbyChanged -= Redraw;

    private void Update()
    {
        // El panel acompaña al menú de entrada: se ve mientras la partida no arrancó.
        // Se consulta acá y no por evento porque el arranque de la partida es un
        // SyncVar del modo de juego, no un cambio de la sala.
        bool show = LobbyManager.Instance != null
                    && (MercenariesGameMode.Instance == null
                        || MercenariesGameMode.Instance.State == EMatchState.Warmup);

        if (_canvas != null && _canvas.gameObject.activeSelf != show) SetVisible(show);
    }

    private void SetVisible(bool visible)
    {
        if (_canvas != null) _canvas.gameObject.SetActive(visible);
    }

    // =========================================================
    // CONSTRUCCIÓN (una sola vez)
    // =========================================================

    private void Build()
    {
        _canvas = MercUIFactory.CreateCanvas("Canvas_LobbyRoster", 60);

        float totalWidth = ColumnWidth * LobbyManager.TeamCount + 24f;

        _root = MercUIFactory.CreateRect(_canvas.transform, "Roster",
                                         new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                         new Vector2(0.5f, 1f),
                                         new Vector2(0f, -90f), new Vector2(totalWidth, 320f));

        _teamColumns  = new RectTransform[LobbyManager.TeamCount];
        _teamContents = new RectTransform[LobbyManager.TeamCount];
        _teamHeaders  = new TextMeshProUGUI[LobbyManager.TeamCount];

        for (int i = 0; i < LobbyManager.TeamCount; i++)
        {
            int team = i + 1;
            float x = (i - (LobbyManager.TeamCount - 1) * 0.5f) * ColumnWidth;

            Image bg = MercUIFactory.CreateImage(_root, $"Column_Team{team}", PanelColor,
                                                 new Vector2(x, 0f),
                                                 new Vector2(ColumnWidth - 8f, ColumnHeight),
                                                 new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                                 new Vector2(0.5f, 1f));

            _teamColumns[i] = bg.rectTransform;

            // El encabezado va del color del equipo: es la forma más rápida de asociar
            // la columna con lo que después ves en el marcador y en el mapa.
            _teamHeaders[i] = MercUIFactory.CreateText(bg.transform, "Header", "",
                                                       20f, MercUIFactory.TeamColor(team),
                                                       TextAlignmentOptions.Center,
                                                       new Vector2(0f, -6f),
                                                       new Vector2(ColumnWidth - 16f, 28f),
                                                       new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                                       new Vector2(0.5f, 1f));

            _teamContents[i] = MakeScrollArea(bg);
        }

        // Espectadores: una franja abajo, aparte de los equipos.
        Image specBg = MercUIFactory.CreateImage(_root, "Column_Spectators", PanelColor,
                                                 new Vector2(0f, -(ColumnHeight + 8f)),
                                                 new Vector2(totalWidth - 8f, 84f),
                                                 new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                                 new Vector2(0.5f, 1f));
        _spectatorColumn = specBg.rectTransform;

        _spectatorHeader = MercUIFactory.CreateText(specBg.transform, "Header", "",
                                                    18f, PendingColor, TextAlignmentOptions.Center,
                                                    new Vector2(0f, -6f),
                                                    new Vector2(totalWidth - 24f, 24f),
                                                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                                    new Vector2(0.5f, 1f));

        _spectatorContent = MakeScrollArea(specBg);

        _statusText = MercUIFactory.CreateText(_root, "Status", "",
                                               20f, Color.white, TextAlignmentOptions.Center,
                                               new Vector2(0f, -(ColumnHeight + 100f)),
                                               new Vector2(totalWidth, 30f),
                                               new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                               new Vector2(0.5f, 1f));
        MercUIFactory.AddShadow(_statusText);
    }

    // Convierte una columna en un área que scrollea: un viewport recortado con
    // RectMask2D y un content que crece con las filas.
    //
    // El ScrollRect va sobre el FONDO de la columna, que es un Image con raycastTarget:
    // sin un gráfico que reciba el raycast, la rueda del mouse no llegaría a ningún
    // lado y el scroll solo funcionaría arrastrando.
    //
    // El encabezado queda fuera del viewport a propósito: con nueve jugadores repartidos
    // de a tres las columnas entran justas, y lo primero que se pierde al scrollear un
    // panel chico es justamente el título que te dice de qué equipo estás mirando.
    private RectTransform MakeScrollArea(Image columnBg)
    {
        RectTransform viewport = MercUIFactory.CreateRect(
            columnBg.transform, "Viewport",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 1f),
            Vector2.zero, Vector2.zero);

        // Estirado a la columna, dejando el encabezado arriba y un margen abajo.
        viewport.offsetMin = new Vector2(6f, 6f);
        viewport.offsetMax = new Vector2(-6f, -HeaderHeight);
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = MercUIFactory.CreateRect(
            viewport, "Content",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(0f, 0f));

        ScrollRect scroll = columnBg.gameObject.AddComponent<ScrollRect>();
        scroll.viewport         = viewport;
        scroll.content          = content;
        scroll.horizontal       = false;
        scroll.vertical         = true;
        // Clamped y no Elastic: en una lista corta el rebote elástico se siente como si
        // hubiera más contenido del que hay.
        scroll.movementType     = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = RowHeight;
        scroll.inertia          = false;

        return content;
    }

    // =========================================================
    // REDIBUJO
    // =========================================================

    private void Redraw()
    {
        if (_root == null) return;

        foreach (GameObject row in _rows) if (row != null) Destroy(row);
        _rows.Clear();

        LobbyManager lobby = LobbyManager.Instance;
        if (lobby == null) return;

        int[] perTeam = new int[LobbyManager.TeamCount];
        int   spectators = 0;

        foreach (LobbyEntry entry in lobby.Entries)
        {
            if (entry.Spectator)
            {
                AddRow(_spectatorContent, entry, spectators, wide: true);
                spectators++;
                continue;
            }

            // Sin equipo elegido todavía no tiene columna donde ir: se lo cuenta como
            // pendiente en el estado de abajo, para no inventarle un equipo.
            if (entry.Team < 1 || entry.Team > LobbyManager.TeamCount) continue;

            int idx = entry.Team - 1;
            AddRow(_teamContents[idx], entry, perTeam[idx], wide: false);
            perTeam[idx]++;
        }

        // El content tiene que medir lo que ocupan las filas: es de ahí de donde el
        // ScrollRect saca si hay algo fuera de la vista y cuánto se puede bajar.
        for (int i = 0; i < LobbyManager.TeamCount; i++)
            ResizeContent(_teamContents[i], perTeam[i]);

        ResizeContent(_spectatorContent, spectators);

        for (int i = 0; i < LobbyManager.TeamCount; i++)
        {
            string cupo = lobby.MaxPlayersPerTeam > 0
                ? $"{perTeam[i]}/{lobby.MaxPlayersPerTeam}"
                : perTeam[i].ToString();

            _teamHeaders[i].text = $"EQUIPO {i + 1}   {cupo}";
        }

        _spectatorHeader.text = spectators > 0 ? $"ESPECTADORES ({spectators})" : "ESPECTADORES";

        UpdateStatus(lobby);
    }

    // Una fila: "nombre · clase · listo". Nada de esto necesita prefab.
    private void AddRow(RectTransform content, LobbyEntry entry, int slot, bool wide)
    {
        if (content == null) return;

        float width = (wide ? ColumnWidth * LobbyManager.TeamCount : ColumnWidth) - 24f;
        // Relativo al borde superior del CONTENT, no de la columna: el encabezado ya
        // no está adentro del área que scrollea.
        float y     = -slot * RowHeight;

        // La clase se resuelve por índice contra la lista del menú, que es la misma en
        // todos los peers. El "?" es el aviso de que todavía no eligió.
        CharacterClassDefinition cls = UI_LobbyMenu.Instance != null
            ? UI_LobbyMenu.Instance.ClassByIndex(entry.ClassIndex)
            : null;

        string className = entry.Spectator ? "espectador"
                         : cls != null      ? cls.ClassName
                                            : "?";

        string check = entry.Spectator ? "" : (entry.Ready ? "  ✓" : "");

        TextMeshProUGUI text = MercUIFactory.CreateText(
            content, $"Row_{entry.ClientId}", $"{entry.PlayerName}   ·   {className}{check}",
            17f, entry.Ready ? ReadyColor : PendingColor,
            TextAlignmentOptions.Left,
            new Vector2(0f, y), new Vector2(width, RowHeight),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

        _rows.Add(text.gameObject);
    }

    private void ResizeContent(RectTransform content, int rowCount)
    {
        if (content != null) content.sizeDelta = new Vector2(0f, rowCount * RowHeight);
    }

    // La línea de abajo: a quién se está esperando, que es la pregunta que todo el
    // mundo hace en voz alta mientras la sala no arranca.
    private void UpdateStatus(LobbyManager lobby)
    {
        if (_statusText == null) return;

        int pending = 0, unassigned = 0;
        foreach (LobbyEntry entry in lobby.Entries)
        {
            if (entry.Spectator) continue;
            if (entry.Team < 1 || entry.ClassIndex < 0) unassigned++;
            else if (!entry.Ready) pending++;
        }

        if (!lobby.RequireAllReady)
        {
            _statusText.text  = "La partida arranca por reloj";
            _statusText.color = PendingColor;
            return;
        }

        if (lobby.PlayerCount < Mathf.Max(1, lobby.MinPlayersToStart))
        {
            _statusText.text  = $"Esperando jugadores ({lobby.PlayerCount}/{lobby.MinPlayersToStart})";
            _statusText.color = PendingColor;
        }
        else if (unassigned > 0)
        {
            _statusText.text  = unassigned == 1
                ? "1 jugador todavía está eligiendo"
                : $"{unassigned} jugadores todavía están eligiendo";
            _statusText.color = PendingColor;
        }
        else if (pending > 0)
        {
            _statusText.text  = pending == 1 ? "Falta 1 por confirmar" : $"Faltan {pending} por confirmar";
            _statusText.color = PendingColor;
        }
        else
        {
            _statusText.text  = "¡Todos listos!";
            _statusText.color = ReadyColor;
        }
    }
}
