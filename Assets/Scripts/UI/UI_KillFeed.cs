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
// VIVE EN EL PREFAB DE PARTIDA (Match_UI ▸ KillFeed), desde el 14 de octubre de 2026:
//   · "Entries" es la esquina donde se apilan (su posición es el margen contra la esquina).
//   · "EntryTemplate" es el MOLDE apagado de una línea: su fondo, su texto (tamaño de letra,
//     sombra) y su alto se cambian ahí. Cada baja es una copia del molde.
// Este script solo escribe el texto, ajusta el ancho al texto y las apaga con el tiempo.
//
// QUIÉN LO LLAMA: el servidor, a todos (PlayerController.ObserversKillFeed).
// ============================================================
public class UI_KillFeed : MonoBehaviour
{
    [Header("Piezas")]
    [Tooltip("Donde se apilan las líneas (anclado a la esquina superior derecha).")]
    [Label("Contenedor de las líneas")]
    public RectTransform Entries;

    [Tooltip("El MOLDE de una línea (apagado): un fondo (Image) estirado y un texto. Su alto es el alto de cada línea.")]
    [Label("Molde de una línea")]
    public RectTransform EntryTemplate;

    [Header("Perillas")]
    [Tooltip("Cuántas líneas se ven a la vez; la más vieja se va al llegar una nueva.")]
    [Label("Líneas a la vez")]
    public int MaxEntries = 5;

    [Tooltip("Segundos que dura cada línea (el último cuarto se usa para apagarse).")]
    [Label("Duración de cada línea (s)")]
    public float EntrySeconds = 6f;

    [Tooltip("Espacio entre una línea y la siguiente, en px del canvas.")]
    [Label("Separación entre líneas (px)")]
    public float RowGap = 4f;

    [Tooltip("Margen a cada lado del texto, dentro del fondo, en px del canvas.")]
    [Label("Margen del texto (px)")]
    public float Padding = 14f;

    [Tooltip("Fondo de las líneas donde apareces tú (mataste o te mataron). Las demás usan el color del fondo del molde.")]
    [Label("Fondo de tus líneas")]
    public Color LocalBackgroundColor = new Color(1f, 1f, 1f, 0.22f);

    private class Entry
    {
        public RectTransform Root;
        public CanvasGroup   Group;
        public float         BornAt;
    }

    private static UI_KillFeed _instance;
    private static bool _warnedMissing;

    // El de la escena (el prefab de partida). Null si la escena no lo tiene: avisa una vez.
    public static UI_KillFeed Get()
    {
        if (_instance != null) return _instance;
        _instance = FindFirstObjectByType<UI_KillFeed>(FindObjectsInactive.Include);
        if (_instance == null && !_warnedMissing)
        {
            _warnedMissing = true;
            Debug.LogWarning("[UI_KillFeed] La escena no tiene el prefab de partida (Match_UI): no hay registro de bajas.");
        }
        return _instance;
    }

    private readonly List<Entry> _entries = new List<Entry>();

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        if (EntryTemplate != null) EntryTemplate.gameObject.SetActive(false);
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
        if (Entries == null || EntryTemplate == null) return;

        string victim = Name(victimName, victimClass, victimTeam);
        string text   = string.IsNullOrEmpty(killerName)
            ? $"{victim}  <color=#BBBBBB>{Loc.T("killfeed.died")}</color>"
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
    // UNA LÍNEA: copia del molde
    // =========================================================

    private Entry BuildEntry(string text, bool highlight)
    {
        RectTransform row = Instantiate(EntryTemplate, Entries);
        row.name = "Kill";
        row.gameObject.SetActive(true);

        CanvasGroup group = row.GetComponent<CanvasGroup>();
        if (group == null) group = row.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable   = false;

        Image background = row.GetComponentInChildren<Image>(true);
        if (background != null && highlight) background.color = LocalBackgroundColor;

        TMP_Text label = row.GetComponentInChildren<TMP_Text>(true);
        float height = row.sizeDelta.y;
        if (label != null)
        {
            label.text = text;
            // El fondo se ajusta al texto: una baja corta no ocupa media pantalla.
            float width = label.GetPreferredValues(text, 2000f, height).x + Padding * 2f;
            row.sizeDelta = new Vector2(width, height);
        }

        return new Entry { Root = row, Group = group, BornAt = Time.time };
    }

    // La más nueva arriba, pegadas a la derecha.
    private void Relayout()
    {
        for (int i = 0; i < _entries.Count; i++)
            if (_entries[i].Root != null)
                _entries[i].Root.anchoredPosition = new Vector2(0f, -i * (_entries[i].Root.sizeDelta.y + RowGap));
    }
}
