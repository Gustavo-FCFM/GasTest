using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ============================================================
// MercMenusUiSetup  (UNA SOLA VEZ — etapa 5c de la revisión de la UI, 14 de octubre de 2026)
//
// Arma el PREFAB DE MENÚS (Prefabs/UI/Menus_UI) con lo que antes se dibujaba solo por código
// desde objetos sueltos de la escena, igual a como se veía (con las medidas y colores que
// tenían cargados las escenas):
//   · MainMenu → la pantalla de inicio (velo, franja, título, subtítulo, tres botones, versión).
//   · Settings → el panel de Ajustes (secciones y filas, cada fila con su UI_SettingRow).
//   · Lobby    → la sala (IP, nombre, columnas con sus lugares UI_LobbySlot, estado, botones y
//                el selector de clase con los MOLDES de pestaña y de ícono).
// Cada uno es un objeto siempre prendido con su componente, y adentro su canvas (lo que se
// prende y se apaga), con el orden de dibujo de siempre.
//
// Y en las escenas ABIERTAS: copia al prefab lo cargado en los componentes viejos (las clases
// de la sala, los colores), pone el prefab y saca los objetos viejos. Si una escena tiene en
// la sala clases que el prefab no tiene, se las agrega.
//
// Cómo usarla: abrir Mercenaries_Gamemode, correrla y guardar; abrir Mercenaries_Graveyard,
// correrla otra vez y guardar. Después se borra este archivo.
// ============================================================
public static class MercMenusUiSetup
{
    private const string PrefabPath = "Assets/Prefabs/UI/Menus_UI.prefab";

    [MenuItem("Mercenarios/Una vez/Crear el prefab de menús y ponerlo en la escena (5c)", false, 102)]
    public static void Run()
    {
        // Los componentes viejos de las escenas abiertas (los que no son del prefab).
        var oldMain = new List<UI_MainMenu>();
        var oldSettings = new List<UI_SettingsPanel>();
        var oldLobby = new List<UI_LobbyPanel>();
        GameObject existing = FindExistingPrefab();
        _menusPath = existing != null ? AssetDatabase.GetAssetPath(existing) : "";
        CollectOld(oldMain, oldSettings, oldLobby);

        GameObject prefab = existing;
        bool created = false;
        if (prefab == null)
        {
            prefab = BuildPrefab(oldMain.Count > 0 ? oldMain[0] : null, oldLobby.Count > 0 ? oldLobby[0] : null);
            created = true;
        }
        if (prefab == null) return;
        _menusPath = AssetDatabase.GetAssetPath(prefab);

        int addedClasses = 0;
        foreach (UI_LobbyPanel old in oldLobby) addedClasses += MergeClasses(old);

        int placed = 0, removed = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            bool dirty = false;

            if (!SceneHasMenus(scene))
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetAsLastSibling();
                placed++;
                dirty = true;
            }

            removed += RemoveOld(scene, ref dirty);
            if (dirty) EditorSceneManager.MarkSceneDirty(scene);
        }

        string msg = (created ? $"Prefab creado en {AssetDatabase.GetAssetPath(prefab)}.\n" : "El prefab ya existía.\n") +
                     (addedClasses > 0 ? $"Le agregué {addedClasses} clase(s) a la sala que tenía esta escena.\n" : "") +
                     (placed > 0 ? "Puesto en la escena abierta.\n" : "La escena ya lo tenía.\n") +
                     (removed > 0 ? $"Saqué {removed} componente(s) viejo(s). Guardá la escena." : "No había componentes viejos.");
        Debug.Log("[Prefab de menús 5c] " + msg.Replace("\n", " "));
        EditorUtility.DisplayDialog("Prefab de menús (5c)", msg, "OK");
        Selection.activeObject = prefab;
    }

    // =========================================================
    // ESCENA
    // =========================================================

    // La ruta del prefab de menús (vacía hasta que exista). Se calcula una vez por corrida.
    private static string _menusPath = "";

    private static bool IsInsideMenusPrefab(Component c)
    {
        if (string.IsNullOrEmpty(_menusPath)) return false;
        GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(c.gameObject);
        if (root == null) return false;
        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(root);
        return source != null && AssetDatabase.GetAssetPath(source) == _menusPath;
    }

    private static void CollectOld(List<UI_MainMenu> main, List<UI_SettingsPanel> settings, List<UI_LobbyPanel> lobby)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (GameObject r in scene.GetRootGameObjects())
            {
                foreach (var c in r.GetComponentsInChildren<UI_MainMenu>(true))      if (!IsInsideMenusPrefab(c)) main.Add(c);
                foreach (var c in r.GetComponentsInChildren<UI_SettingsPanel>(true)) if (!IsInsideMenusPrefab(c)) settings.Add(c);
                foreach (var c in r.GetComponentsInChildren<UI_LobbyPanel>(true))    if (!IsInsideMenusPrefab(c)) lobby.Add(c);
            }
        }
    }

    private static bool SceneHasMenus(Scene scene)
    {
        foreach (GameObject r in scene.GetRootGameObjects())
            foreach (var c in r.GetComponentsInChildren<UI_LobbyPanel>(true))
                if (IsInsideMenusPrefab(c)) return true;
        return false;
    }

    private static int RemoveOld(Scene scene, ref bool dirty)
    {
        var olds = new List<Component>();
        foreach (GameObject r in scene.GetRootGameObjects())
        {
            foreach (var c in r.GetComponentsInChildren<UI_MainMenu>(true))      if (!IsInsideMenusPrefab(c)) olds.Add(c);
            foreach (var c in r.GetComponentsInChildren<UI_SettingsPanel>(true)) if (!IsInsideMenusPrefab(c)) olds.Add(c);
            foreach (var c in r.GetComponentsInChildren<UI_LobbyPanel>(true))    if (!IsInsideMenusPrefab(c)) olds.Add(c);
        }
        var touched = new HashSet<GameObject>();
        foreach (Component c in olds)
        {
            touched.Add(c.gameObject);
            Object.DestroyImmediate(c);
        }
        foreach (GameObject go in touched)
            if (go != null && go.GetComponents<Component>().Length == 1 && go.transform.childCount == 0)
                Object.DestroyImmediate(go);
        if (olds.Count > 0) dirty = true;
        return olds.Count;
    }

    private static GameObject FindExistingPrefab()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (go != null && go.GetComponentInChildren<UI_LobbyPanel>(true) != null &&
                go.GetComponentInChildren<UI_MainMenu>(true) != null)
                return go;
        }
        return null;
    }

    // Las clases de la sala vieja que el prefab no tiene, al final de su lista.
    private static int MergeClasses(UI_LobbyPanel old)
    {
        if (old == null || old.SelectableClasses == null) return 0;
        string path = _menusPath;
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        int added = 0;
        try
        {
            UI_LobbyPanel lobby = root.GetComponentInChildren<UI_LobbyPanel>(true);
            var list = new List<CharacterClassDefinition>(lobby.SelectableClasses ?? new CharacterClassDefinition[0]);
            foreach (CharacterClassDefinition cls in old.SelectableClasses)
                if (cls != null && !list.Contains(cls)) { list.Add(cls); added++; }
            if (added > 0)
            {
                lobby.SelectableClasses = list.ToArray();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        if (added > 0)
            Debug.LogWarning("[Prefab de menús 5c] Agregué clases a la sala: las pestañas y la grilla se arman solas, " +
                             "pero si el selector quedó angosto, se ensancha solo al empezar.");
        return added;
    }

    // =========================================================
    // ARMADO
    // =========================================================

    private static GameObject BuildPrefab(UI_MainMenu oldMain, UI_LobbyPanel oldLobby)
    {
        GameObject root = new GameObject("Menus_UI");
        try
        {
            BuildMainMenu(root.transform, oldMain);
            BuildSettings(root.transform);
            BuildLobby(root.transform, oldLobby);

            System.IO.Directory.CreateDirectory("Assets/Prefabs/UI");
            return PrefabUtility.SaveAsPrefabAsset(root, AssetDatabase.GenerateUniqueAssetPath(PrefabPath));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // ---------- ayudas ----------

    private static GameObject Canvas(Transform parent, string name, int sortOrder)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(parent, false);
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortOrder;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;
        return go;
    }

    private static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
                                      Vector2 pivot, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    private static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 LeftMid = new Vector2(0f, 0.5f);

    private static Image Img(Transform parent, string name, Color color, Vector2 pos, Vector2 size,
                             Vector2? anchorMin = null, Vector2? anchorMax = null, Vector2? pivot = null, bool raycast = false)
    {
        RectTransform rt = Rect(parent, name, anchorMin ?? LeftMid, anchorMax ?? (anchorMin ?? LeftMid), pivot ?? LeftMid, pos, size);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    private static TextMeshProUGUI Txt(Transform parent, string name, string text, float size, Color color,
                                       TextAlignmentOptions align, Vector2 pos, Vector2 box,
                                       Vector2? anchorMin = null, Vector2? anchorMax = null, Vector2? pivot = null)
    {
        RectTransform rt = Rect(parent, name, anchorMin ?? LeftMid, anchorMax ?? (anchorMin ?? LeftMid), pivot ?? LeftMid, pos, box);
        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.color = color; tmp.alignment = align;
        tmp.raycastTarget = false; tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }

    private static void Key(TMP_Text text, string key, bool upper = false)
    {
        LocalizedText l = text.gameObject.AddComponent<LocalizedText>();
        l.Key = key;
        l.UpperCase = upper;
    }

    private static void Shadow(Graphic g, float d)
    {
        Shadow s = g.gameObject.AddComponent<Shadow>();
        s.effectColor = new Color(0f, 0f, 0f, 0.75f);
        s.effectDistance = new Vector2(d, -d);
    }

    // Un botón: fondo de color, su Button (con los colores de resaltado de siempre) y el texto.
    private static Button Btn(Transform parent, string name, Color color, Vector2 anchor, Vector2 pos, Vector2 size,
                              float fontSize, out TextMeshProUGUI label, bool bold = false, bool tint = true)
    {
        RectTransform rt = Rect(parent, name, anchor, anchor, anchor, pos, size);
        Image bg = rt.gameObject.AddComponent<Image>();
        bg.color = color;
        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        if (tint)
        {
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.25f);
            colors.selectedColor    = colors.highlightedColor;
            colors.pressedColor     = Color.Lerp(color, Color.black, 0.25f);
            button.colors = colors;
        }
        label = Txt(rt, "Text", "", fontSize, Color.white, TextAlignmentOptions.Center, Vector2.zero,
                    new Vector2(size.x - 8f, size.y - 6f), Mid, Mid, Mid);
        if (bold) label.fontStyle = FontStyles.Bold;
        return button;
    }

    // =========================================================
    // MENÚ PRINCIPAL (orden 100)
    // =========================================================

    private static void BuildMainMenu(Transform root, UI_MainMenu old)
    {
        Color backdropC = new Color(0f, 0f, 0f, 0.35f), bandC = new Color(0f, 0f, 0f, 0.35f);
        Color buttonC = new Color(0.20f, 0.22f, 0.26f), playC = new Color(0.95f, 0.65f, 0.20f);
        Color textC = new Color(0.94f, 0.94f, 0.94f), dimC = new Color(0.6f, 0.6f, 0.6f);
        Vector2 buttonSize = new Vector2(320f, 58f);
        const float gap = 16f;

        GameObject holder = new GameObject("MainMenu");
        holder.transform.SetParent(root, false);
        UI_MainMenu menu = holder.AddComponent<UI_MainMenu>();
        if (old != null) EditorUtility.CopySerialized(old, menu);

        Transform canvas = Canvas(holder.transform, "MainMenuCanvas", 100).transform;

        // Velo sobre la arena: se traga los clics para que nada de abajo los reciba.
        Img(canvas, "Backdrop", backdropC, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one, Mid, raycast: true);
        Img(canvas, "Band", bandC, Vector2.zero, Vector2.zero, new Vector2(0.3f, 0f), new Vector2(0.7f, 1f), Mid);

        TextMeshProUGUI title = Txt(canvas, "Title", "MERCENARIES", 92f, textC, TextAlignmentOptions.Center,
                                    new Vector2(0f, 200f), new Vector2(1200f, 120f), Mid, Mid, Mid);
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 12f;
        Shadow(title, 3f);

        TextMeshProUGUI subtitle = Txt(canvas, "Subtitle", "", 24f, dimC, TextAlignmentOptions.Center,
                                       new Vector2(0f, 128f), new Vector2(900f, 34f), Mid, Mid, Mid);
        Key(subtitle, "menu.subtitle");

        float y = 10f;
        menu.PlayButton = Btn(canvas, "Play", playC, Mid, new Vector2(0f, y), buttonSize, 24f, out TextMeshProUGUI playL, bold: true);
        Key(playL, "menu.play");
        y -= buttonSize.y + gap;
        menu.SettingsButton = Btn(canvas, "Settings", buttonC, Mid, new Vector2(0f, y), buttonSize, 24f, out TextMeshProUGUI setL, bold: true);
        Key(setL, "menu.settings");
        y -= buttonSize.y + gap;
        menu.QuitButton = Btn(canvas, "Quit", buttonC, Mid, new Vector2(0f, y), buttonSize, 24f, out TextMeshProUGUI quitL, bold: true);
        Key(quitL, "menu.quit");

        Vector2 corner = new Vector2(1f, 0f);
        menu.VersionText = Txt(canvas, "Version", "v", 15f, dimC, TextAlignmentOptions.Right,
                               new Vector2(-16f, 10f), new Vector2(300f, 20f), corner, corner, corner);

        menu.MenuCanvas = canvas.gameObject;
    }

    // =========================================================
    // AJUSTES (orden 300)
    // =========================================================

    private static void BuildSettings(Transform root)
    {
        const float panelWidth = 760f, footer = 110f, rowHeight = 44f, labelWidth = 260f, sectionGap = 18f;
        Color backdropC = new Color(0f, 0f, 0f, 0.8f), panelC = new Color(0.10f, 0.11f, 0.13f, 0.97f);
        Color rowC = new Color(1f, 1f, 1f, 0.04f), accentC = new Color(0.95f, 0.65f, 0.20f);
        Color controlC = new Color(0.20f, 0.22f, 0.26f), textC = new Color(0.92f, 0.92f, 0.92f), dimC = new Color(0.65f, 0.65f, 0.65f);

        GameObject holder = new GameObject("Settings");
        holder.transform.SetParent(root, false);
        UI_SettingsPanel settings = holder.AddComponent<UI_SettingsPanel>();

        GameObject canvasGo = Canvas(holder.transform, "SettingsCanvas", 300);
        Transform canvas = canvasGo.transform;

        // Tapa todo y se traga los clics (no se cierra al clickear afuera).
        Img(canvas, "Backdrop", backdropC, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one, Mid, raycast: true);
        Image panel = Img(canvas, "Panel", panelC, Vector2.zero, new Vector2(panelWidth, 100f), Mid, Mid, Mid, raycast: true);
        Transform p = panel.transform;

        Vector2 top = new Vector2(0.5f, 1f);
        TextMeshProUGUI title = Txt(p, "Title", "", 34f, textC, TextAlignmentOptions.Center,
                                    new Vector2(0f, -36f), new Vector2(panelWidth, 44f), top, top, top);
        title.fontStyle = FontStyles.Bold;
        Key(title, "settings.title");

        float y = -90f;
        Vector2 tl = new Vector2(0f, 1f);

        void Section(string key)
        {
            y -= sectionGap;
            TextMeshProUGUI t = Txt(p, "Section_" + key, "", 16f, accentC, TextAlignmentOptions.Left,
                                    new Vector2(40f, y), new Vector2(panelWidth - 80f, 24f), tl, tl, tl);
            t.fontStyle = FontStyles.Bold;
            Key(t, key, upper: true);
            y -= 28f;
        }

        UI_SettingRow Row(string labelKey, ESettingKind kind, out RectTransform control)
        {
            float width = panelWidth - 80f;
            Image bg = Img(p, "Row_" + labelKey, rowC, new Vector2(40f, y), new Vector2(width, rowHeight), tl, tl, tl);
            TextMeshProUGUI label = Txt(bg.transform, "Label", "", 19f, textC, TextAlignmentOptions.Left,
                                        new Vector2(14f, 0f), new Vector2(labelWidth, rowHeight));
            label.enableAutoSizing = true; label.fontSizeMin = 13f; label.fontSizeMax = 19f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            Key(label, labelKey);
            Vector2 right = new Vector2(1f, 0.5f);
            control = Rect(bg.transform, "Control", right, right, right, new Vector2(-14f, 0f),
                           new Vector2(width - labelWidth - 40f, rowHeight - 10f));
            y -= rowHeight + 4f;
            UI_SettingRow row = bg.gameObject.AddComponent<UI_SettingRow>();
            row.Setting = kind;
            return row;
        }

        void SliderRow(string key, ESettingKind kind)
        {
            UI_SettingRow row = Row(key, kind, out RectTransform control);
            float w = control.sizeDelta.x;
            Vector2 right = new Vector2(1f, 0.5f);
            row.Value = Txt(control, "Value", "", 18f, textC, TextAlignmentOptions.Right, Vector2.zero, new Vector2(80f, rowHeight), right, right, right);

            RectTransform rect = Rect(control, "Slider", LeftMid, LeftMid, LeftMid, Vector2.zero, new Vector2(w - 96f, 24f));
            Img(rect, "Track", controlC, Vector2.zero, new Vector2(0f, 8f), new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Mid);
            RectTransform fillArea = Rect(rect, "FillArea", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Mid, Vector2.zero, new Vector2(-12f, 8f));
            Image fill = Img(fillArea, "Fill", accentC, Vector2.zero, new Vector2(12f, 0f), Vector2.zero, Vector2.one, Mid);
            RectTransform handleArea = Rect(rect, "HandleArea", Vector2.zero, Vector2.one, Mid, Vector2.zero, new Vector2(-12f, 0f));
            Image handle = Img(handleArea, "Handle", Color.white, Vector2.zero, new Vector2(12f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f), Mid, raycast: true);

            Slider slider = rect.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            ColorBlock colors = slider.colors;
            colors.highlightedColor = accentC;
            colors.selectedColor = accentC;
            slider.colors = colors;

            // Toda la fila del slider recibe el clic, no solo la manija.
            Image hit = rect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;

            row.Slider = slider;
        }

        void ToggleRow(string key, ESettingKind kind)
        {
            UI_SettingRow row = Row(key, kind, out RectTransform control);
            float h = rowHeight - 14f;
            Vector2 right = new Vector2(1f, 0.5f);
            Image box = Img(control, "Box", controlC, Vector2.zero, new Vector2(h, h), right, right, right, raycast: true);
            Image mark = Img(box.transform, "Mark", accentC, Vector2.zero, new Vector2(h - 10f, h - 10f), Mid, Mid, Mid);
            Toggle toggle = box.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = box;
            toggle.graphic = mark;
            ColorBlock colors = toggle.colors;
            colors.highlightedColor = Color.Lerp(controlC, Color.white, 0.25f);
            colors.selectedColor = colors.highlightedColor;
            toggle.colors = colors;
            row.Toggle = toggle;
        }

        void CyclerRow(string key, ESettingKind kind)
        {
            UI_SettingRow row = Row(key, kind, out RectTransform control);
            float w = control.sizeDelta.x;
            float h = rowHeight - 10f;
            row.Previous = Btn(control, "Prev", controlC, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(h, h), 18f, out TextMeshProUGUI prevL);
            row.Next     = Btn(control, "Next", controlC, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(h, h), 18f, out TextMeshProUGUI nextL);
            // Sin rich text: TextMeshPro leería el "<" como el inicio de una etiqueta.
            prevL.richText = false; prevL.text = "<";
            nextL.richText = false; nextL.text = ">";
            row.Value = Txt(control, "Value", "", 18f, textC, TextAlignmentOptions.Center, Vector2.zero,
                            new Vector2(w - h * 2f - 8f, h), Mid, Mid, Mid);
        }

        Section("settings.section.language");
        CyclerRow("settings.language", ESettingKind.Language);
        Section("settings.section.controls");
        CyclerRow("settings.input_scheme", ESettingKind.InputScheme);
        Section("settings.section.camera");
        SliderRow("settings.sensitivity", ESettingKind.Sensitivity);
        ToggleRow("settings.invert_y", ESettingKind.InvertY);
        // (La sección de sonido no se arma mientras no haya clips: para prenderla, duplicar una
        // fila de barra y ponerle Volumen general / Música / Efectos.)
        Section("settings.section.display");
        CyclerRow("settings.screen_mode", ESettingKind.ScreenMode);
        CyclerRow("settings.resolution", ESettingKind.Resolution);
        ToggleRow("settings.vsync", ESettingKind.VSync);

        panel.rectTransform.sizeDelta = new Vector2(panelWidth, -y + footer);

        Vector2 bottom = new Vector2(0.5f, 0f);
        settings.ResetButton = Btn(p, "Reset", controlC, bottom, new Vector2(-110f, 36f), new Vector2(190f, 44f), 18f, out TextMeshProUGUI resetL);
        Key(resetL, "settings.reset");
        settings.CloseButton = Btn(p, "Close", accentC, bottom, new Vector2(110f, 36f), new Vector2(190f, 44f), 18f, out TextMeshProUGUI closeL);
        Key(closeL, "settings.close");
        TextMeshProUGUI hint = Txt(p, "Hint", "", 15f, dimC, TextAlignmentOptions.Center, new Vector2(0f, 8f),
                                   new Vector2(panelWidth, 20f), bottom, bottom, bottom);
        Key(hint, "settings.hint");

        settings.PanelCanvas = canvasGo;
        canvasGo.SetActive(false);
    }

    // =========================================================
    // SALA (orden 70)
    // =========================================================

    private static void BuildLobby(Transform root, UI_LobbyPanel old)
    {
        // Las medidas que tenían cargadas las dos escenas.
        Vector2 panelSize = new Vector2(1250f, 600f);
        const float columnWidth = 250f, rowHeight = 56f, panelScale = 1.35f, slotIconSize = 100f, classIconSize = 150f;
        const float nameHeight = 30f, descHeight = 72f, confirmHeight = 56f, roleTabHeight = 34f;

        GameObject holder = new GameObject("Lobby");
        holder.transform.SetParent(root, false);
        UI_LobbyPanel lobby = holder.AddComponent<UI_LobbyPanel>();
        if (old != null) EditorUtility.CopySerialized(old, lobby);   // las clases y los colores

        Color panelC = lobby.PanelColor, slotC = lobby.SlotColor, idleC = lobby.IdleColor, rowC = lobby.RowColor, pendingC = lobby.PendingColor;

        GameObject canvasGo = Canvas(holder.transform, "LobbyCanvas", 70);
        RectTransform panel = Rect(canvasGo.transform, "Panel", Mid, Mid, Mid, Vector2.zero, panelSize);
        panel.localScale = Vector3.one * panelScale;
        Image panelBg = panel.gameObject.AddComponent<Image>();
        panelBg.color = panelC;
        panelBg.raycastTarget = true;

        Vector2 top = new Vector2(0.5f, 1f);

        // --- cabecera: IP y Start ---
        lobby.IpText = Txt(panel, "IP", "Host IP: —", 24f, Color.white, TextAlignmentOptions.Center,
                           new Vector2(0f, -40f), new Vector2(600f, 40f), top, top, top);
        lobby.StartButton = Btn(panel, "StartButton", idleC, top, new Vector2(420f, -40f), new Vector2(150f, 44f), 18f,
                                out TextMeshProUGUI startL, tint: false);
        Key(startL, "lobby.start");
        lobby.StartLabel = startL;

        // --- nombre ---
        const float labelW = 200f, inputW = 260f, gap = 24f, rowY = -95f;
        float left   = -(labelW + gap + inputW) * 0.5f;
        float labelX = left + labelW * 0.5f;
        float inputX = left + labelW + gap + inputW * 0.5f;
        TextMeshProUGUI nameLabel = Txt(panel, "NameLabel", "", 22f, Color.white, TextAlignmentOptions.Right,
                                        new Vector2(labelX, rowY), new Vector2(labelW, 36f), top, top, top);
        Key(nameLabel, "lobby.username");
        RectTransform inputRect = Rect(panel, "NameInput", top, top, top, new Vector2(inputX, rowY), new Vector2(inputW, 36f));
        Image inputBg = inputRect.gameObject.AddComponent<Image>();
        inputBg.color = slotC;
        TextMeshProUGUI inputText = Txt(inputRect, "Text", "", 20f, Color.white, TextAlignmentOptions.Left,
                                        new Vector2(12f, 0f), new Vector2(inputW - 24f, 32f));
        TMP_InputField input = inputRect.gameObject.AddComponent<TMP_InputField>();
        input.textComponent = inputText;
        input.characterLimit = 16;
        lobby.NameInput = input;

        // --- columnas ---
        float startX = -(columnWidth * 1.5f);
        const float headerY = -160f;
        int teams = LobbyManager.TeamCount;
        lobby.TeamHeaders = new TMP_Text[teams];
        MercenariesGameMode gm = Object.FindFirstObjectByType<MercenariesGameMode>();
        Color[] fallback = { new Color(0.85f, 0.3f, 0.3f), new Color(0.3f, 0.75f, 0.35f), new Color(0.3f, 0.5f, 0.9f) };

        for (int t = 0; t < teams; t++)
        {
            int team = t + 1;
            float x = startX + columnWidth * t;
            Color teamC = gm != null ? gm.GetTeamColor(team) : fallback[t % fallback.Length];
            lobby.TeamHeaders[t] = Header(panel, $"Team{team}", teamC, x, headerY, columnWidth);
            lobby.TeamHeaders[t].text = $"Team {team}";
            for (int s = 0; s < 3; s++)
                Slot(panel, $"T{team}_S{s}", team, x, headerY - 56f - rowHeight * s, columnWidth, rowHeight, slotIconSize, rowC, slotC, idleC, pendingC);
        }

        float specX = startX + columnWidth * teams;
        TMP_Text specHeader = Header(panel, "Spectators", new Color(0.55f, 0.57f, 0.62f, 1f), specX, headerY, columnWidth);
        Key(specHeader, "lobby.spectators");
        for (int s = 0; s < 2; s++)
            Slot(panel, $"Spec_{s}", 0, specX, headerY - 56f - rowHeight * s, columnWidth, rowHeight, slotIconSize, rowC, slotC, idleC, pendingC);

        // --- estado y Confirmar ---
        Vector2 bottom = new Vector2(0.5f, 0f);
        lobby.StatusText = Txt(panel, "Status", "", 18f, pendingC, TextAlignmentOptions.Center,
                               new Vector2(0f, 100f), new Vector2(760f, 30f), bottom, bottom, bottom);
        lobby.ConfirmButton = Btn(panel, "ConfirmButton", idleC, bottom, new Vector2(0f, 46f), new Vector2(180f, 46f), 18f,
                                  out TextMeshProUGUI confirmL, tint: false);
        confirmL.text = "Confirm";
        lobby.ConfirmLabel = confirmL;

        BuildClassPicker(panel, lobby, panelC, slotC, classIconSize, nameHeight, descHeight, confirmHeight, roleTabHeight);

        lobby.LobbyCanvas = canvasGo;
        lobby.Panel = panel;
        canvasGo.SetActive(false);
    }

    private static TMP_Text Header(Transform panel, string name, Color color, float x, float y, float columnWidth)
    {
        Vector2 top = new Vector2(0.5f, 1f);
        RectTransform rect = Rect(panel, $"Header_{name}", top, top, top, new Vector2(x, y), new Vector2(columnWidth - 40f, 40f));
        Image bg = rect.gameObject.AddComponent<Image>();
        bg.color = color;
        return Txt(rect, "Text", "", 20f, Color.white, TextAlignmentOptions.Center, Vector2.zero,
                   new Vector2(columnWidth - 44f, 36f), Mid, Mid, Mid);
    }

    private static void Slot(Transform panel, string name, int team, float x, float y, float columnWidth, float rowHeight,
                             float slotIconSize, Color rowC, Color slotC, Color idleC, Color pendingC)
    {
        Vector2 top = new Vector2(0.5f, 1f);
        RectTransform rootRt = Rect(panel, $"Slot_{name}", top, top, top, new Vector2(x, y), new Vector2(columnWidth - 40f, rowHeight - 8f));
        UI_LobbySlot slot = rootRt.gameObject.AddComponent<UI_LobbySlot>();
        slot.Team = team;

        // El recuadro de la fila, detrás de todo.
        slot.Background = rootRt.gameObject.AddComponent<Image>();
        slot.Background.color = rowC;

        const float iconX = 14f, gap = 12f, sideW = 34f;
        float iconSize = Mathf.Clamp(slotIconSize, 8f, rowHeight - 8f);
        float labelX   = iconX + iconSize + gap;

        RectTransform iconRect = Rect(rootRt, "Icon", LeftMid, LeftMid, LeftMid, new Vector2(iconX, 0f), new Vector2(iconSize, iconSize));
        slot.Icon = iconRect.gameObject.AddComponent<Image>();
        slot.Icon.preserveAspect = true;
        slot.IconButton = iconRect.gameObject.AddComponent<Button>();
        slot.IconButton.targetGraphic = slot.Icon;

        slot.Label = Txt(rootRt, "Name", "", 18f, pendingC, TextAlignmentOptions.Left, new Vector2(labelX, 0f),
                         new Vector2(columnWidth - 40f - labelX - 8f, 32f));

        slot.JoinButton = Btn(rootRt, "Join", slotC, LeftMid, new Vector2(14f, 0f),
                              new Vector2(columnWidth - 100f - sideW, rowHeight - 14f), 18f, out TextMeshProUGUI joinL, tint: false);
        joinL.text = "Join";
        slot.JoinLabel = joinL;

        Vector2 right = new Vector2(1f, 0.5f);
        slot.AddBotButton = Btn(rootRt, "AddBot", idleC, right, new Vector2(-14f, 0f), new Vector2(sideW, rowHeight - 14f), 18f,
                                out TextMeshProUGUI addL, tint: false);
        addL.text = "+";
        slot.RemoveButton = Btn(rootRt, "RemoveBot", new Color(0.45f, 0.20f, 0.22f, 1f), right, new Vector2(-14f, 0f),
                                new Vector2(sideW, rowHeight - 14f), 18f, out TextMeshProUGUI remL, tint: false);
        remL.text = "x";
    }

    private static void BuildClassPicker(RectTransform panel, UI_LobbyPanel lobby, Color panelC, Color slotC,
                                         float classIconSize, float nameHeight, float descHeight, float confirmHeight, float roleTabHeight)
    {
        // El tamaño, como antes: el ancho del grupo de rol más grande (o de las pestañas).
        var roles = new Dictionary<EClassRole, int>();
        if (lobby.SelectableClasses != null)
            foreach (CharacterClassDefinition cls in lobby.SelectableClasses)
            {
                EClassRole r = cls != null ? cls.DisplayRole : EClassRole.None;
                roles[r] = roles.TryGetValue(r, out int n) ? n + 1 : 1;
            }
        int widest = 1;
        foreach (int n in roles.Values) widest = Mathf.Max(widest, n);
        const float tabW = 150f, tabGap = 8f;
        float iconSize  = Mathf.Max(24f, classIconSize);
        float step      = iconSize + 12f;
        float tabsWidth = roles.Count * tabW + Mathf.Max(0, roles.Count - 1) * tabGap;
        float width     = Mathf.Max(360f, Mathf.Max(widest * step, tabsWidth) + 32f);
        float height    = roleTabHeight + 12f + iconSize + 46f + nameHeight + descHeight + confirmHeight;

        RectTransform picker = Rect(panel, "ClassPicker", Mid, Mid, Mid, Vector2.zero, new Vector2(width, height));
        Image bg = picker.gameObject.AddComponent<Image>();
        bg.color = new Color(panelC.r, panelC.g, panelC.b, 1f);   // opaco
        Button close = picker.gameObject.AddComponent<Button>();
        close.targetGraphic = bg;
        close.transition = Selectable.Transition.None;
        lobby.PickerCloseButton = close;

        float tabsY  = height * 0.5f - 12f - roleTabHeight * 0.5f;
        float iconsY = tabsY - roleTabHeight * 0.5f - 16f - iconSize * 0.5f;

        // Los moldes: una pestaña y un ícono, apagados, a su altura.
        Button tab = Btn(picker, "TabTemplate", Color.gray, Mid, new Vector2(0f, tabsY), new Vector2(tabW, roleTabHeight), 18f,
                         out _, tint: false);
        tab.gameObject.SetActive(false);
        lobby.TabTemplate = tab;

        RectTransform iconRt = Rect(picker, "ClassIconTemplate", Mid, Mid, Mid, new Vector2(0f, iconsY), new Vector2(iconSize, iconSize));
        Image icon = iconRt.gameObject.AddComponent<Image>();
        icon.preserveAspect = true;
        Button iconB = iconRt.gameObject.AddComponent<Button>();
        iconB.targetGraphic = icon;
        iconRt.gameObject.SetActive(false);
        lobby.ClassIconTemplate = iconB;

        // El nombre y la descripción se estiran con el ancho del selector (crece solo si hacen falta más íconos).
        float nameY = iconsY - iconSize * 0.5f - nameHeight * 0.5f - 6f;
        TextMeshProUGUI className = Txt(picker, "ClassName", "", 22f, Color.white, TextAlignmentOptions.Center,
                                        new Vector2(0f, nameY), new Vector2(-32f, nameHeight),
                                        new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Mid);
        className.fontStyle = FontStyles.Bold;
        lobby.ClassNameLabel = className;

        float descY = nameY - nameHeight * 0.5f - descHeight * 0.5f;
        lobby.ClassDescLabel = Txt(picker, "ClassDescription", "", 15f, new Color(0.78f, 0.80f, 0.85f, 1f), TextAlignmentOptions.Top,
                                   new Vector2(0f, descY), new Vector2(-40f, descHeight),
                                   new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Mid);

        lobby.PickerConfirm = Btn(picker, "ConfirmClass", slotC, new Vector2(0.5f, 0f), new Vector2(0f, 14f),
                                  new Vector2(200f, confirmHeight - 10f), 18f, out TextMeshProUGUI pickL, tint: false);
        Key(pickL, "lobby.confirm");

        picker.gameObject.SetActive(false);
        lobby.ClassPicker = picker;
    }
}
