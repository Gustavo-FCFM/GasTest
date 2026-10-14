using System.Collections.Generic;
using UnityEngine;

// ============================================================
// UI_CompanionPanel  (Maestro de bestias)
//
// Los compañeros en el HUD del jugador (pedido de Gustavo: que siempre sepa cuál tiene activo
// sin buscarlo con la vista). VIVE EN EL PREFAB DE LA CÁMARA (Player Camera ▸ el canvas del
// HUD), donde se acomoda y se le cambia el tamaño como a cualquier otra pieza del HUD:
//
//   · Un cuadrito por compañero (copias de SlotTemplate), en el orden de la rueda, de
//     izquierda a derecha y alineados abajo (desde la esquina de abajo a la izquierda del panel).
//   · El ACTIVO con ActiveSize, marco verde y su barra de vida.
//   · El que murió hace poco, oscurecido y con los segundos que le faltan para volver.
//
// No sabe nada del juego más allá de leer: busca el CompanionManager del jugador local y
// muestra lo que dice. Si el jugador no es Maestro de bestias, se esconde (CanvasGroup).
// ============================================================
[RequireComponent(typeof(CanvasGroup))]
public class UI_CompanionPanel : MonoBehaviour
{
    [Header("Piezas")]
    [Tooltip("El molde de un compañero (un hijo apagado). Se copia una vez por compañero.")]
    [Label("Molde de un compañero")]
    public UI_CompanionSlot SlotTemplate;

    [Header("Tamaños")]
    [Tooltip("Ancho y alto del cuadrito del compañero activo, en píxeles del HUD.")]
    [Label("Tamaño del activo (px)")]
    public Vector2 ActiveSize = new Vector2(34f, 34f);

    [Tooltip("Ancho y alto de los cuadritos de los otros compañeros, en píxeles del HUD.")]
    [Label("Tamaño de los otros (px)")]
    public Vector2 OtherSize = new Vector2(26f, 26f);

    [Tooltip("Espacio entre un cuadrito y el siguiente, en píxeles del HUD.")]
    [Label("Separación entre compañeros (px)")]
    public float Spacing = 4f;

    [Tooltip("Espacio libre debajo de los cuadritos para la barra de vida del activo, en píxeles del HUD.")]
    [Label("Espacio abajo para la barra de vida (px)")]
    public float HealthBarSpace = 7f;

    [Header("Colores")]
    [Label("Marco del activo")]
    public Color ActiveFrameColor = new Color(0.35f, 0.85f, 0.4f, 0.95f);

    [Label("Marco de los otros")]
    public Color OtherFrameColor = new Color(0f, 0f, 0f, 0.6f);

    [Tooltip("Capa oscura sobre los compañeros que no están elegidos.")]
    [Label("Oscurecido de los no elegidos")]
    public Color IdleDimColor = new Color(0f, 0f, 0f, 0.35f);

    [Tooltip("Capa oscura sobre el que murió y espera para volver.")]
    [Label("Oscurecido mientras vuelve")]
    public Color RespawnDimColor = new Color(0f, 0f, 0f, 0.7f);

    private CanvasGroup _group;
    private CompanionManager _manager;
    private float _nextSearch;
    private readonly List<UI_CompanionSlot> _slots = new List<UI_CompanionSlot>();

    private void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        if (SlotTemplate != null) SlotTemplate.gameObject.SetActive(false);
        // Los cuadritos los acomoda este script (de izquierda a derecha, alineados abajo): un
        // LayoutGroup los movería a su manera y no alinea bien tamaños distintos.
        var layout = GetComponent<UnityEngine.UI.LayoutGroup>();
        if (layout != null) layout.enabled = false;
        Show(false);
    }

    private void Update()
    {
        FindManager();
        if (_manager == null || !_manager.isActiveAndEnabled) { Show(false); return; }
        Show(true);
        Refresh();
    }

    // El CompanionManager del jugador local (en el prefab de pasivas del Maestro de bestias).
    // Se busca cada medio segundo: aparece y desaparece con el cambio de clase.
    private void FindManager()
    {
        if (_manager != null && _manager.isActiveAndEnabled) return;
        if (Time.unscaledTime < _nextSearch) return;
        _nextSearch = Time.unscaledTime + 0.5f;

        PlayerController local = PlayerController.LocalPlayer;
        CompanionManager found = local != null ? local.GetComponentInChildren<CompanionManager>() : null;
        if (found != _manager)
        {
            _manager = found;
            Rebuild();
        }
    }

    private void Rebuild()
    {
        foreach (UI_CompanionSlot slot in _slots) if (slot != null) Destroy(slot.gameObject);
        _slots.Clear();
        if (_manager == null || SlotTemplate == null) return;

        foreach (CompanionManager.CompanionOption option in _manager.Companions)
        {
            UI_CompanionSlot slot = Instantiate(SlotTemplate, transform);
            slot.gameObject.SetActive(true);
            if (slot.Icon != null) slot.Icon.sprite = option.Icon;
            _slots.Add(slot);
        }
    }

    private void Refresh()
    {
        int active = _manager.ActiveIndex;
        Entity_Companion companion = _manager.FindCompanionHere();
        float x = 0f;

        for (int i = 0; i < _slots.Count; i++)
        {
            UI_CompanionSlot s = _slots[i];
            bool isActive = i == active;
            float wait = _manager.RespawnRemaining(i);

            Vector2 size = isActive ? ActiveSize : OtherSize;
            var rect = (RectTransform)s.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.sizeDelta        = size;
            rect.anchoredPosition = new Vector2(x, HealthBarSpace);
            x += size.x + Spacing;
            if (s.Frame != null) s.Frame.color = isActive ? ActiveFrameColor : OtherFrameColor;
            if (s.Dim != null)
            {
                s.Dim.enabled = wait > 0f || !isActive;
                s.Dim.color   = wait > 0f ? RespawnDimColor : IdleDimColor;
            }
            if (s.RespawnTimer != null) s.RespawnTimer.text = wait > 0f ? Mathf.CeilToInt(wait).ToString() : "";

            bool showHealth = isActive && companion != null && wait <= 0f;
            if (s.HealthBar != null && s.HealthBar.activeSelf != showHealth) s.HealthBar.SetActive(showHealth);
            if (showHealth && s.HealthFill != null)
            {
                AbilitySystemComponent asc = companion.ASC;
                float max = asc != null ? asc.GetAttributeValue(EAttributeType.MaxHealth) : 0f;
                float hp  = asc != null ? asc.GetAttributeValue(EAttributeType.Health) : 0f;
                s.HealthFill.anchorMax = new Vector2(max > 0f ? Mathf.Clamp01(hp / max) : 1f, 1f);
            }
        }
    }

    private void Show(bool visible)
    {
        float alpha = visible ? 1f : 0f;
        if (!Mathf.Approximately(_group.alpha, alpha)) _group.alpha = alpha;
    }
}
