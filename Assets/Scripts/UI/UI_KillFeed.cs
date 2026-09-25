using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_KillFeed
//
// El registro de bajas de la esquina superior derecha: "Gus (Rogue) » Pedro (Paladin)",
// cada nombre del color de su equipo. Solo bajas de PERSONAJES (jugadores y bots): un
// fantasma que muere no es noticia.
//
// POR QUÉ IMPORTA: es lo que permite entender una partida sin estar jugándola — el que
// mira de espectador, el que acaba de reaparecer, y cualquiera que pase por el stand del
// showcase. Las líneas donde apareces tú se resaltan.
//
// NO HAY NADA QUE CABLEAR: se arma solo la primera vez que llega una baja. La manda el
// servidor a todos (PlayerController.ObserversKillFeed).
// ============================================================
public class UI_KillFeed : MonoBehaviour
{
    [Header("Perillas")]
    [Tooltip("Cuántas líneas se ven a la vez; la más vieja se va al llegar una nueva.")]
    public int MaxEntries = 5;

    [Tooltip("Segundos que dura cada línea (el último se usa para apagarse).")]
    public float EntrySeconds = 6f;

    public float FontSize = 20f;

    [Tooltip("Margen contra la esquina superior derecha. Y bajado para no tapar el reloj " +
             "ni el marcador de arriba.")]
    public Vector2 Margin = new Vector2(24f, 120f);

    public Color BackgroundColor = new Color(0f, 0f, 0f, 0.5f);
    [Tooltip("Fondo de las líneas donde apareces tú (mataste o te mataron).")]
    public Color LocalBackgroundColor = new Color(1f, 1f, 1f, 0.22f);

    private const float RowHeight = 32f;
    private const float RowGap    = 4f;
    private const float Padding   = 14f;

    private class Entry
    {
        public RectTransform Root;
        public CanvasGroup   Group;
        public float         BornAt;
    }

    private static UI_KillFeed _instance;

    public static UI_KillFeed Get()
    {
        if (_instance != null) return _instance;

        _instance = FindFirstObjectByType<UI_KillFeed>(FindObjectsInactive.Include);
        if (_instance != null) return _instance;

        GameObject go = new GameObject("UI_KillFeed");
        _instance = go.AddComponent<UI_KillFeed>();
        return _instance;
    }

    private Canvas _canvas;
    private RectTransform _root;
    private readonly List<Entry> _entries = new List<Entry>();

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;

        _canvas = MercUIFactory.CreateCanvas("KillFeedCanvas", 105);
        _canvas.transform.SetParent(transform, false);

        Vector2 topRight = new Vector2(1f, 1f);
        _root = MercUIFactory.CreateRect(_canvas.transform, "Entries", topRight, topRight, topRight,
                                         new Vector2(-Margin.x, -Margin.y), Vector2.zero);
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // =========================================================
    // LO QUE SE LE PIDE DESDE AFUERA
    // =========================================================

    // killerName vacío = murió sin atacante (el entorno). involvesLocal = en esta baja
    // aparece el jugador de esta pantalla, y la línea se resalta.
    public void AddKill(string killerName, string killerClass, int killerTeam,
                        string victimName, string victimClass, int victimTeam, bool involvesLocal)
    {
        string victim = Name(victimName, victimClass, victimTeam);
        string text   = string.IsNullOrEmpty(killerName)
            ? $"{victim}  <color=#BBBBBB>died</color>"
            : $"{Name(killerName, killerClass, killerTeam)}  <color=#BBBBBB>»</color>  {victim}";

        Entry entry = BuildEntry(text, involvesLocal);
        _entries.Insert(0, entry);

        while (_entries.Count > Mathf.Max(1, MaxEntries))
        {
            Entry old = _entries[_entries.Count - 1];
            _entries.RemoveAt(_entries.Count - 1);
            if (old.Root != null) Destroy(old.Root.gameObject);
        }

        Relayout();
    }

    // Nombre del color del equipo, con la clase más chica y gris al lado. Equipo 0 (un
    // NPC) sale en gris.
    private static string Name(string name, string klass, int team)
    {
        Color color = team > 0 ? MercUIFactory.TeamColor(team) : new Color(0.75f, 0.75f, 0.75f);
        string hex  = ColorUtility.ToHtmlStringRGB(color);
        string suffix = string.IsNullOrEmpty(klass) ? "" : $" <size=75%><color=#AAAAAA>{klass}</color></size>";
        return $"<b><color=#{hex}>{name}</color></b>{suffix}";
    }

    // =========================================================
    // CADA FRAME: apagar y sacar las viejas
    // =========================================================

    private void Update()
    {
        bool removed = false;
        float fade = Mathf.Min(1f, EntrySeconds * 0.25f);

        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            Entry e = _entries[i];
            float age = Time.time - e.BornAt;

            if (age >= EntrySeconds || e.Root == null)
            {
                if (e.Root != null) Destroy(e.Root.gameObject);
                _entries.RemoveAt(i);
                removed = true;
                continue;
            }

            float left = EntrySeconds - age;
            e.Group.alpha = left < fade ? left / fade : 1f;
        }

        if (removed) Relayout();
    }

    // =========================================================
    // CONSTRUCCIÓN
    // =========================================================

    private Entry BuildEntry(string text, bool highlight)
    {
        Vector2 topRight = new Vector2(1f, 1f);
        RectTransform row = MercUIFactory.CreateRect(_root, "Kill", topRight, topRight, topRight,
                                                     Vector2.zero, new Vector2(100f, RowHeight));
        CanvasGroup group = row.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable   = false;

        Image background = MercUIFactory.CreateImage(row, "Background",
            highlight ? LocalBackgroundColor : BackgroundColor,
            Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        background.rectTransform.sizeDelta = Vector2.zero;   // estirado a la fila

        Vector2 mid = new Vector2(0.5f, 0.5f);
        TextMeshProUGUI label = MercUIFactory.CreateText(row, "Text", text, FontSize, Color.white,
            TextAlignmentOptions.Center, Vector2.zero, new Vector2(600f, RowHeight), mid, mid, mid);
        label.richText = true;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        MercUIFactory.AddShadow(label);

        // El fondo se ajusta al texto: una baja corta no ocupa media pantalla.
        float width = label.GetPreferredValues(text, 2000f, RowHeight).x + Padding * 2f;
        row.sizeDelta = new Vector2(width, RowHeight);
        label.rectTransform.sizeDelta = new Vector2(width, RowHeight);

        return new Entry { Root = row, Group = group, BornAt = Time.time };
    }

    // La más nueva arriba, pegadas a la derecha.
    private void Relayout()
    {
        for (int i = 0; i < _entries.Count; i++)
            if (_entries[i].Root != null)
                _entries[i].Root.anchoredPosition = new Vector2(0f, -i * (RowHeight + RowGap));
    }
}
