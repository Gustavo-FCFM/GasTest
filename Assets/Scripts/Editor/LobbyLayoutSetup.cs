using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

// ============================================================
// LobbyLayoutSetup
//
// Herramienta de un solo uso: configura el menú de entrada (UI_LobbyMenu.prefab)
// con los Layout Groups y medidas del diseño de Figma, para no tener que poner
// decenas de valores a mano en el inspector.
//
// Lo hace por código y no editando el .prefab a mano porque así lo construye la
// propia API de Unity: los componentes quedan bien serializados y no hay riesgo de
// romper el asset.
//
// Se corre desde el menú:  Tools ▸ GasTest ▸ Aplicar layout al menú de entrada
// Es idempotente: si lo corrés dos veces, no duplica nada (reusa los componentes
// que ya estén).
//
// Valores sacados del diseño: contenido de 720 de ancho, padding 24, y el ritmo
// vertical de 33 que se deduce de las posiciones de las filas en Figma.
// ============================================================
public static class LobbyLayoutSetup
{
    private const string PrefabPath = "Assets/Scripts/UI/UI_LobbyMenu.prefab";

    // Medidas del diseño.
    private const int ContentWidth = 720;  // ancho útil (sin el padding del panel)
    private const int PanelPadding = 24;
    private const int PanelWidth   = ContentWidth + PanelPadding * 2; // 768
    private const int RowSpacing   = 33;

    [MenuItem("Tools/GasTest/Aplicar layout al menú de entrada")]
    public static void Apply()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            Debug.LogError($"[LobbyLayout] No encontré el prefab en {PrefabPath}");
            return;
        }

        try
        {
            Transform menu = FindDeep(root.transform, "Menu");
            if (menu == null)
            {
                Debug.LogError("[LobbyLayout] No encontré el objeto 'Menu' dentro del prefab.");
                return;
            }

            // ---- Clics ------------------------------------------------------
            // Sin GraphicRaycaster en el Canvas, NINGÚN elemento de esa UI recibe
            // clics: los botones se ven pero no responden. Es un componente que Unity
            // agrega solo al crear un Canvas desde el menú, pero se pierde fácil al
            // armar la jerarquía a mano.
            Canvas canvas = root.GetComponentInChildren<Canvas>(true);
            if (canvas != null)
            {
                Ensure<GraphicRaycaster>(canvas.transform);
            }
            else
            {
                Debug.LogWarning("[LobbyLayout] El prefab no tiene Canvas: la UI no se va a ver ni a poder clickear.");
            }

            // ---- Panel raíz -------------------------------------------------
            // Ancho fijo y alto que se adapta al contenido: es lo que evita que los
            // textos se partan en varias líneas (el síntoma de un panel angosto).
            RectTransform menuRt = menu as RectTransform;
            if (menuRt != null)
            {
                menuRt.sizeDelta = new Vector2(PanelWidth, menuRt.sizeDelta.y);
            }

            VerticalLayoutGroup menuVlg = Ensure<VerticalLayoutGroup>(menu);
            menuVlg.padding             = new RectOffset(PanelPadding, PanelPadding, PanelPadding, PanelPadding);
            menuVlg.spacing             = RowSpacing;
            menuVlg.childAlignment      = TextAnchor.UpperCenter;
            menuVlg.childControlWidth   = true;
            menuVlg.childControlHeight  = true;
            // NO forzar la expansión horizontal: si no, TODO lo que cuelgue del panel
            // se estira a los 720 (los botones de equipo quedaban como barras pegadas
            // y Confirmar como una línea de lado a lado). Cada fila toma el ancho que
            // pide y el Upper Center la centra; a las que necesitan ocupar todo el
            // ancho se les da un LayoutElement explícito más abajo.
            menuVlg.childForceExpandWidth  = false;
            menuVlg.childForceExpandHeight = false;

            ContentSizeFitter menuFitter = Ensure<ContentSizeFitter>(menu);
            menuFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            menuFitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            // ---- Fila del nombre --------------------------------------------
            Transform usernameRow = FindDeep(menu, "UsernameLayout");
            if (usernameRow != null)
            {
                HorizontalLayoutGroup h = Ensure<HorizontalLayoutGroup>(usernameRow);
                h.padding            = new RectOffset(24, 24, 8, 8);
                h.spacing            = 8;
                h.childAlignment     = TextAnchor.MiddleCenter;
                h.childControlWidth  = true;
                h.childControlHeight = true;
                h.childForceExpandWidth  = false;
                h.childForceExpandHeight = false;
            }

            // El campo de texto tiene medida propia (342 x 70 en el diseño).
            SetPreferredSize(FindDeep(menu, "NameInput"), 342, 70);

            // ---- Sección de equipo ------------------------------------------
            Transform teamSection = FindDeep(menu, "TeamLayout");
            if (teamSection != null)
            {
                VerticalLayoutGroup v = Ensure<VerticalLayoutGroup>(teamSection);
                v.padding            = new RectOffset(24, 24, 8, 8);
                v.spacing            = 16;
                v.childAlignment     = TextAnchor.UpperCenter;
                v.childControlWidth  = true;
                v.childControlHeight = true;
                v.childForceExpandWidth  = true;
                v.childForceExpandHeight = false;

                // Fila de los 3 botones. El "Gap: Auto" de Figma = repartir el espacio
                // sobrante ENTRE los botones, sin deformarlos. En Unity eso es:
                // childControlWidth OFF (cada botón conserva su tamaño) +
                // childForceExpand ON (el hueco restante se reparte).
                Transform buttonsRow = FindDeep(teamSection, "ButtonsLayout");
                if (buttonsRow != null)
                {
                    HorizontalLayoutGroup h = Ensure<HorizontalLayoutGroup>(buttonsRow);
                    h.padding            = new RectOffset(8, 8, 8, 8);
                    h.spacing            = 0;
                    h.childAlignment     = TextAnchor.MiddleCenter;
                    h.childControlWidth  = false;
                    h.childControlHeight = false;
                    h.childForceExpandWidth  = true;
                    h.childForceExpandHeight = false;

                    // La fila SÍ ocupa todo el ancho útil: es lo que da el espacio a
                    // repartir entre los tres botones.
                    SetPreferredSize(buttonsRow, ContentWidth - 48, 86);
                }

                // Con childControlWidth OFF el Layout Group ignora el LayoutElement y
                // usa el tamaño real del RectTransform: hay que fijarlo directo.
                foreach (string n in new[] { "Team1", "Team2", "Team3" })
                {
                    Transform b = FindDeep(teamSection, n);
                    if (b is RectTransform brt) brt.sizeDelta = new Vector2(139, 70);
                }
            }

            // ---- Sección de clase -------------------------------------------
            Transform classSection = FindDeep(menu, "ClassSelectionLayout");
            if (classSection != null)
            {
                VerticalLayoutGroup v = Ensure<VerticalLayoutGroup>(classSection);
                v.padding            = new RectOffset(24, 24, 0, 0);
                v.spacing            = 72;
                v.childAlignment     = TextAnchor.UpperCenter;
                v.childControlWidth  = true;
                v.childControlHeight = true;
                v.childForceExpandWidth  = true;
                v.childForceExpandHeight = false;

                // Contenedor de las tarjetas (CardsParent). Las tarjetas las instancia
                // UI_LobbyMenu en runtime, así que acá solo se define cómo se ordenan.
                Transform cardsRow = FindDeep(classSection, "ButtonsLayout");
                if (cardsRow != null)
                {
                    HorizontalLayoutGroup h = Ensure<HorizontalLayoutGroup>(cardsRow);
                    h.padding            = new RectOffset(0, 0, 0, 0);
                    h.spacing            = 45;
                    h.childAlignment     = TextAnchor.MiddleCenter;
                    // En TRUE para que se respete el LayoutElement cuadrado que
                    // UI_LobbyMenu le pone a cada tarjeta. Con esto en false el grupo
                    // ignora ese tamaño y usa el RectTransform del prefab, que viene
                    // estirado — por eso las tarjetas ocupaban toda la pantalla.
                    h.childControlWidth  = true;
                    h.childControlHeight = true;
                    h.childForceExpandWidth  = false;
                    h.childForceExpandHeight = false;

                    // Ancho útil completo, para que las tarjetas queden centradas.
                    SetPreferredSize(cardsRow, ContentWidth - 48, 200);
                }
            }

            // ---- Botón de confirmar -----------------------------------------
            // Tamaño propio y centrado (antes se estiraba de lado a lado).
            SetPreferredSize(FindDeep(menu, "ConfirmButton"), 300, 64);

            // ---- Títulos centrados ------------------------------------------
            // El centrado de un texto es del propio TMP, no del Layout Group: por eso
            // "Choose Team:" y "Choose a class:" quedaban pegados a la izquierda.
            foreach (string n in new[] { "IPText", "TeamIDText", "ClassSelectionText" })
                CenterText(FindDeep(menu, n));

            // ---- Referencias del script -------------------------------------
            // Se asignan por código porque son las que más fácil quedan mal al
            // arrastrarlas a mano: TeamButtons vacío deja la selección de equipo sin
            // conectar (parece que "no guarda"), y ClassCardPrefab apuntando a un
            // objeto INTERNO del prefab en vez de al asset hace que no salga ninguna
            // tarjeta.
            UI_LobbyMenu lobby = root.GetComponentInChildren<UI_LobbyMenu>(true);
            if (lobby != null)
            {
                var buttons = new System.Collections.Generic.List<Button>();
                foreach (string n in new[] { "Team1", "Team2", "Team3" })
                {
                    Transform t = FindDeep(menu, n);
                    Button b = t != null ? t.GetComponent<Button>() : null;
                    if (b != null) buttons.Add(b);
                }
                if (buttons.Count > 0) lobby.TeamButtons = buttons.ToArray();

                // Colores del diseño: fondo 2B3239 y celeste al elegir. El botón sin
                // elegir tiene que ser el oscuro, no blanco.
                lobby.NormalTeamColor   = Hex("2B3239");
                lobby.SelectedTeamColor = new Color(0.3f, 0.8f, 1f, 1f);
                lobby.HoverTeamColor    = Hex("1E2733"); // azul oscuro al pasar el mouse

                // CardsParent tiene que ser la FILA horizontal, no la sección entera
                // (que es vertical): si apunta a la sección, las tarjetas salen una
                // debajo de la otra.
                Transform classSectionForCards = FindDeep(menu, "ClassSelectionLayout");
                Transform cardsHolder = classSectionForCards != null
                    ? FindDeep(classSectionForCards, "ButtonsLayout") : null;
                if (cardsHolder != null) lobby.CardsParent = cardsHolder;

                // En el lobby la tarjeta muestra icono + nombre; la descripción sobra
                // (y con el tamaño cuadrado no entra).
                lobby.HiddenCardParts = new[] { "ClassDescription" };

                // Y el texto de cada botón en blanco (sobre el fondo oscuro).
                foreach (Button b in buttons)
                    foreach (TMPro.TMP_Text t in b.GetComponentsInChildren<TMPro.TMP_Text>(true))
                        t.color = Color.white;

                // El ColorBlock arranca con el color "sin elegir": si no, el botón se
                // ve blanco hasta que lo tocás.
                foreach (Button b in buttons)
                {
                    ColorBlock cb = b.colors;
                    cb.normalColor      = lobby.NormalTeamColor;
                    cb.selectedColor    = lobby.NormalTeamColor;
                    cb.highlightedColor = lobby.HoverTeamColor;
                    cb.pressedColor     = lobby.SelectedTeamColor;
                    b.colors = cb;
                }

                // El prefab de tarjeta es un ASSET externo, no un hijo de este prefab.
                GameObject card = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scripts/UI/ClassCard.prefab");
                if (card != null) lobby.ClassCardPrefab = card;
                else Debug.LogWarning("[LobbyLayout] No encontré ClassCard.prefab — asigná ClassCardPrefab a mano.");

                EditorUtility.SetDirty(lobby);
            }

            // ---- Tamaño de fuente del nombre --------------------------------
            // El texto del InputField viene con la fuente por defecto (muy chica al
            // lado del resto del menú).
            Transform nameInput = FindDeep(menu, "NameInput");
            if (nameInput != null)
            {
                foreach (TMPro.TMP_Text t in nameInput.GetComponentsInChildren<TMPro.TMP_Text>(true))
                {
                    t.enableAutoSizing = false;
                    t.fontSize = 28;
                }
            }

            // ---- Diagnóstico ------------------------------------------------
            // Avisa qué piezas no aparecieron, en vez de dejar un menú a medias sin
            // explicación (ej. la sección de clases sin mostrarse).
            foreach (string n in new[] { "IPText", "NameInput", "Team1", "Team2", "Team3",
                                         "ClassSelectionLayout", "ClassSelectionText", "ConfirmButton" })
            {
                if (FindDeep(menu, n) == null)
                    Debug.LogWarning($"[LobbyLayout] No encontré '{n}' en el prefab — revisá que exista y esté activo.");
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[LobbyLayout] Layout aplicado a UI_LobbyMenu.prefab (con GraphicRaycaster para los clics).");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Color desde hexadecimal ("2B3239"), para poder usar los valores del diseño.
    private static Color Hex(string hex)
    {
        return ColorUtility.TryParseHtmlString("#" + hex, out Color c) ? c : Color.white;
    }

    // Agrega el componente si falta, o devuelve el que ya está (idempotente).
    private static T Ensure<T>(Transform t) where T : Component
    {
        T comp = t.GetComponent<T>();
        return comp != null ? comp : t.gameObject.AddComponent<T>();
    }

    // Fija el tamaño que el objeto le pide al Layout Group que lo contiene.
    private static void SetPreferredSize(Transform t, float width, float height)
    {
        if (t == null) return;

        LayoutElement le = Ensure<LayoutElement>(t);
        le.preferredWidth  = width;
        le.preferredHeight = height;
    }

    // Centra el texto y lo hace ocupar el ancho útil, para que quede centrado
    // respecto del panel y no pegado a la izquierda.
    private static void CenterText(Transform t)
    {
        if (t == null) return;

        TMPro.TMP_Text text = t.GetComponent<TMPro.TMP_Text>();
        if (text == null) return;

        text.alignment = TMPro.TextAlignmentOptions.Center;
        SetPreferredSize(t, ContentWidth, text.preferredHeight > 0 ? text.preferredHeight : 44);
    }

    // Busca por nombre en toda la jerarquía (no solo en los hijos directos).
    private static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
