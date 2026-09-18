using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// MercUIFactory
//
// Fabriquita de trozos de UI para el HUD del modo Mercenarios.
//
// POR QUÉ EL HUD SE CONSTRUYE EN CÓDIGO Y NO CON PREFABS: el HUD de partida son
// rectángulos de color y texto (marcador, avisos, marcador del Objetivo). Armarlo por
// código evita tener que cablear treinta referencias en el Inspector y, sobre todo,
// evita que se rompa cada vez que se toca el prefab. Se cae solo en su lugar: ponés el
// componente en la escena y ya está.
//
// El HUD de la CLASE (vida, habilidades, cooldowns) sigue siendo el prefab de siempre
// (UI_PlayerHUD, en la cámara del jugador). Esto es solo la capa de PARTIDA.
// ============================================================
public static class MercUIFactory
{
    // Sprite blanco de 1x1 reutilizado por todos los recuadros. Se crea una sola vez.
    private static Sprite _whiteSprite;

    public static Sprite WhiteSprite
    {
        get
        {
            if (_whiteSprite != null) return _whiteSprite;

            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            _whiteSprite.name = "MercWhite";
            return _whiteSprite;
        }
    }

    // Canvas de pantalla completa para el HUD de partida. Order alto para quedar por
    // encima del HUD de clase, pero por debajo de los menús modales.
    public static Canvas CreateCanvas(string name, int sortOrder = 50)
    {
        GameObject go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortOrder;

        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        return canvas;
    }

    // Rectángulo vacío (contenedor).
    public static RectTransform CreateRect(Transform parent, string name,
                                           Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
                                           Vector2 anchoredPos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin       = anchorMin;
        rt.anchorMax       = anchorMax;
        rt.pivot           = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta       = size;
        return rt;
    }

    // Recuadro de color liso.
    public static Image CreateImage(Transform parent, string name, Color color,
                                    Vector2 anchoredPos, Vector2 size,
                                    Vector2? anchorMin = null, Vector2? anchorMax = null,
                                    Vector2? pivot = null)
    {
        RectTransform rt = CreateRect(parent, name,
            anchorMin ?? new Vector2(0f, 0.5f),
            anchorMax ?? new Vector2(0f, 0.5f),
            pivot     ?? new Vector2(0f, 0.5f),
            anchoredPos, size);

        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite        = WhiteSprite;
        img.color         = color;
        img.raycastTarget = false;
        return img;
    }

    // Texto TMP. La fuente la resuelve TextMeshPro solo (la default del proyecto).
    public static TextMeshProUGUI CreateText(Transform parent, string name, string text,
                                             float fontSize, Color color,
                                             TextAlignmentOptions alignment,
                                             Vector2 anchoredPos, Vector2 size,
                                             Vector2? anchorMin = null, Vector2? anchorMax = null,
                                             Vector2? pivot = null)
    {
        RectTransform rt = CreateRect(parent, name,
            anchorMin ?? new Vector2(0f, 0.5f),
            anchorMax ?? new Vector2(0f, 0.5f),
            pivot     ?? new Vector2(0f, 0.5f),
            anchoredPos, size);

        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text          = text;
        tmp.fontSize      = fontSize;
        tmp.color         = color;
        tmp.alignment     = alignment;
        tmp.raycastTarget = false;
        tmp.overflowMode  = TextOverflowModes.Overflow;
        return tmp;
    }

    // Sombra dura detrás de un texto: en un escenario claro, sin esto el HUD se pierde.
    public static void AddShadow(Graphic graphic, float distance = 2f)
    {
        Shadow shadow = graphic.gameObject.AddComponent<Shadow>();
        shadow.effectColor    = new Color(0f, 0f, 0f, 0.75f);
        shadow.effectDistance = new Vector2(distance, -distance);
    }

    // El color del equipo, o gris si todavía no hay game mode en la escena.
    public static Color TeamColor(int team)
    {
        MercenariesGameMode gm = MercenariesGameMode.Instance;
        return gm != null ? gm.GetTeamColor(team) : Color.gray;
    }

    // Segundos → "m:ss".
    public static string FormatTime(float seconds)
    {
        if (seconds < 0f) seconds = 0f;
        int total = Mathf.CeilToInt(seconds);
        return $"{total / 60}:{total % 60:00}";
    }

    // El equipo del jugador de ESTA pantalla (0 si todavía no entró a la partida).
    public static int LocalTeam()
    {
        PlayerController local = PlayerController.LocalPlayer;
        if (local == null) return 0;

        NetworkAbilitySystemComponent netASC = local.GetComponent<NetworkAbilitySystemComponent>();
        if (netASC != null && netASC.NetTeamID > 0) return netASC.NetTeamID;

        AbilitySystemComponent asc = local.GetComponent<AbilitySystemComponent>();
        return asc != null ? asc.TeamID : 0;
    }
}
