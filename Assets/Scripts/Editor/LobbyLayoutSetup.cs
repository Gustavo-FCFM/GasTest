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
            menuVlg.childForceExpandWidth  = true;
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

                // Fila de los 3 botones. El "Gap: Auto" de Figma (repartir el espacio)
                // se traduce en Unity a forzar la expansión del ancho.
                Transform buttonsRow = FindDeep(teamSection, "ButtonsLayout");
                if (buttonsRow != null)
                {
                    HorizontalLayoutGroup h = Ensure<HorizontalLayoutGroup>(buttonsRow);
                    h.padding            = new RectOffset(8, 8, 8, 8);
                    h.spacing            = 0;
                    h.childAlignment     = TextAnchor.MiddleCenter;
                    h.childControlWidth  = true;
                    h.childControlHeight = true;
                    h.childForceExpandWidth  = true;
                    h.childForceExpandHeight = false;
                }

                foreach (string n in new[] { "Team1", "Team2", "Team3" })
                    SetPreferredSize(FindDeep(teamSection, n), 139, 70);
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
                    h.childControlWidth  = false; // que cada tarjeta conserve su tamaño
                    h.childControlHeight = false;
                    h.childForceExpandWidth  = false;
                    h.childForceExpandHeight = false;
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[LobbyLayout] Layout aplicado a UI_LobbyMenu.prefab. " +
                      "Revisá el prefab: el panel quedó de 768 de ancho y las filas ordenadas.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
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
