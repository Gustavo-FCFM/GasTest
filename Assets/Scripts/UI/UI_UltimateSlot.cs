using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ============================================================
// UI_UltimateSlot
//
// Ícono central de la ultimate: en vez de mostrar el cooldown
// "vaciándose" como UI_AbilitySlot, lo muestra como una CARGA que
// va llenándose de 0% a 100% ("READY") a medida que el cooldown
// baja. Misma lógica dual host/cliente remoto que UI_AbilitySlot.
// ============================================================
public class UI_UltimateSlot : MonoBehaviour
{
    [Header("UI References")]
    public Image iconBackground;           // Ícono oscurecido de fondo
    public Image iconFill;                 // Ícono que se va llenando de color (Type: Filled)
    public TextMeshProUGUI percentageText; // Texto "0%" → "99%" → "READY"
    public GameObject readyEffects;        // Partículas/marco que se prende al llegar al 100%

    [Tooltip("Texto con la tecla/botón de la ultimate (R). Se rellena solo según el slot. Opcional.")]
    public TextMeshProUGUI keyText;        // Etiqueta de la tecla asignada

    [Header("Lista para usar")]
    [Tooltip("Cuánto crece el ícono al latir con la definitiva cargada. 0.08 = un 8 %.")]
    public float ReadyPulseScale = 0.08f;
    [Tooltip("Latidos por segundo con la definitiva cargada.")]
    public float ReadyPulseSpeed = 1.5f;
    public Color ReadyToastColor = new Color(1f, 0.82f, 0.25f, 1f);

    private GameplayAbility assignedAbility;
    private AbilitySystemComponent ownerASC;
    private NetworkAbilitySystemComponent ownerNetASC;
    private EAbilityInput slotInput;

    // Para avisar SOLO al pasar de cargando a lista (no cada frame, ni al armar el slot
    // con la definitiva ya llena).
    private bool    _readyKnown;
    private bool    _wasReady;
    private Vector3 _baseScale = Vector3.one;
    private bool    _baseScaleKnown;

    // Asocia este slot a la ultimate del personaje. Si no tiene ultimate
    // asignada todavía (ej: nivel bajo), se oculta el GameObject entero.
    public void Setup(GameplayAbility ability, AbilitySystemComponent asc, NetworkAbilitySystemComponent netAsc, EAbilityInput slot)
    {
        assignedAbility = ability;
        ownerASC = asc;
        ownerNetASC = netAsc;
        slotInput = slot;
        _readyKnown = false;

        if (!_baseScaleKnown) { _baseScale = transform.localScale; _baseScaleKnown = true; }
        transform.localScale = _baseScale;

        // Etiqueta del botón (mismo mapeo que UI_AbilitySlot; para la ultimate, "R" con
        // teclado, "Y" con Xbox, "TRI" con PlayStation).
        RefreshKeyLabel();

        if (assignedAbility != null)
        {
            if(iconBackground)
            {
                iconBackground.sprite = assignedAbility.AbilityIcon;
                iconBackground.enabled = true;
                iconBackground.color = new Color(0.3F, 0.3F, 0.3F, 1f);
            }
            if(iconFill)
            {
                iconFill.sprite = assignedAbility.AbilityIcon;
                iconFill.enabled = true;
                iconFill.type = Image.Type.Filled;
                iconFill.color = Color.white;
            }
            gameObject.SetActive(true);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    // Cada frame, calcula el porcentaje de carga (inverso del cooldown
    // restante) y actualiza el relleno, el texto y el efecto de "listo".
    void Update()
    {
        if (assignedAbility == null || ownerASC == null) return;

        float timeRemaining;
        float totalDuration;

        bool isOnCooldown = ownerNetASC != null
            ? ownerNetASC.TryGetNetCooldown(slotInput, out timeRemaining, out totalDuration)
            : ownerASC.GetCooldownStatus(assignedAbility, out timeRemaining, out totalDuration);

        if (isOnCooldown && totalDuration > 0)
        {
            // 0% recién activada la ultimate, 100% al terminarse el cooldown
            float chargePercent = 1f - (timeRemaining / totalDuration);

            if(iconFill) iconFill.fillAmount = chargePercent;
            if(percentageText) percentageText.text = $"{chargePercent * 100:F0}%";
            if(readyEffects) readyEffects.SetActive(false);
            UpdateReadyState(false);
        }
        else
        {
            // Sin cooldown activo = lista para usar
            if(iconFill) iconFill.fillAmount = 1f;
            if(percentageText) percentageText.text = "READY";
            if(readyEffects) readyEffects.SetActive(true);
            UpdateReadyState(true);
        }
    }

    // Lista: el ícono late, y al momento de cargarse sale un aviso bajo la mira. Con la
    // carga por rol la definitiva llega sin que uno la esté mirando, y es fácil olvidar
    // que ya está.
    private void UpdateReadyState(bool ready)
    {
        if (!_baseScaleKnown) { _baseScale = transform.localScale; _baseScaleKnown = true; }

        // El primer frame solo anota el estado: armar el slot con la definitiva ya llena
        // (el modo de prueba) no es "se acaba de cargar".
        if (_readyKnown && ready && !_wasReady)
        {
            string key = UI_AbilitySlot.GetKeyLabel(slotInput);
            UI_ScreenFeedback.Get().ShowToast($"ULTIMATE READY!  [{key}]", ReadyToastColor);
        }
        _readyKnown = true;
        _wasReady   = ready;

        float pulse = ready ? 1f + ReadyPulseScale * (0.5f + 0.5f * Mathf.Sin(Time.time * ReadyPulseSpeed * Mathf.PI * 2f)) : 1f;
        transform.localScale = _baseScale * pulse;
    }

    private void RefreshKeyLabel()
    {
        if (keyText != null) keyText.text = UI_AbilitySlot.GetKeyLabel(slotInput);
    }

    // Igual que en UI_AbilitySlot: cambiar de esquema en Ajustes reescribe la etiqueta
    // al instante, sin esperar a que el slot se vuelva a armar.
    private void OnEnable()  { GameSettings.OnChanged += RefreshKeyLabel; RefreshKeyLabel(); }
    private void OnDisable() { GameSettings.OnChanged -= RefreshKeyLabel; }
}
