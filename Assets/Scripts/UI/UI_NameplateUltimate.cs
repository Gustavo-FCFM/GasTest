using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_NameplateUltimate
//
// La definitiva de un ALIADO, sobre el ícono de clase de su barra de vida (13 de noviembre de 2026, pedido de
// Gustavo; como en Overwatch): detrás del ícono un relleno que sube como líquido, un aro de progreso alrededor y el
// porcentaje abajo. Cuando la tiene LISTA, el aro se pone dorado, late y un brillo gira alrededor; el número se va.
//
// Las piezas están ARMADAS en el prefab Nameplate (ClassIcon ▸ al lado, "Ultimate"); este script solo las anima.
// Lee PlayerController.UltimatePercent (lo manda el servidor) y se muestra solo a los aliados del jugador local
// (a ti mismo no: tu barra no se ve; a los enemigos tampoco). Un NPC o una clase base sin definitiva: apagado.
// ============================================================
public class UI_NameplateUltimate : MonoBehaviour
{
    [Header("Piezas")]
    [Tooltip("Todo el grupo: se apaga cuando no corresponde mostrarlo.")]
    [Label("Grupo")]
    public GameObject Root;

    [Tooltip("El relleno detrás del ícono (Image en modo Filled, vertical desde abajo).")]
    [Label("Relleno")]
    public Image Fill;

    [Tooltip("El aro de progreso (Image en modo Filled, radial).")]
    [Label("Aro")]
    public Image Ring;

    [Tooltip("El brillo que gira alrededor cuando está lista.")]
    [Label("Brillo de lista")]
    public Image ReadyGlow;

    [Label("Porcentaje")]
    public TMP_Text Percent;

    [Header("Colores")]
    [Label("Cargando")]
    public Color ChargingColor = new Color(0.35f, 0.8f, 1f, 1f);

    [Tooltip("El relleno: el mismo color, más transparente.")]
    [Label("Relleno (opacidad 0 a 1)")]
    [Range(0f, 1f)] public float FillOpacity = 0.45f;

    [Label("Lista")]
    public Color ReadyColor = new Color(1f, 0.82f, 0.25f, 1f);

    [Header("Animación de lista")]
    [Label("Giro del brillo (°/s)")]
    public float GlowSpin = 140f;

    [Label("Latido (veces por segundo)")]
    public float PulseRate = 1.6f;

    [Tooltip("Cuánto crece el aro en cada latido, como fracción.")]
    [Label("Latido: tamaño (0 a 1)")]
    [Range(0f, 0.5f)] public float PulseSize = 0.12f;

    private PlayerController _pc;
    private AbilitySystemComponent _asc;
    private int _shownPercent = -2;
    private Vector3 _ringScale = Vector3.one;

    private void Awake()
    {
        _pc  = GetComponentInParent<PlayerController>();
        _asc = _pc != null ? _pc.GetComponent<AbilitySystemComponent>() : null;
        if (Ring != null) _ringScale = Ring.rectTransform.localScale;
        if (Root != null) Root.SetActive(false);
    }

    private void LateUpdate()
    {
        int percent = Visible() ? _pc.UltimatePercent : -1;
        bool show = percent >= 0;
        if (Root != null && Root.activeSelf != show) Root.SetActive(show);
        if (!show) { _shownPercent = -2; return; }

        bool ready = percent >= 100;
        float t = percent / 100f;

        if (Fill != null)
        {
            Fill.fillAmount = t;
            Color c = ready ? ReadyColor : ChargingColor;
            c.a = FillOpacity;
            Fill.color = c;
        }
        if (Ring != null)
        {
            Ring.fillAmount = t;
            Ring.color = ready ? ReadyColor : ChargingColor;
            float pulse = ready ? 1f + PulseSize * (0.5f + 0.5f * Mathf.Sin(Time.time * PulseRate * Mathf.PI * 2f)) : 1f;
            Ring.rectTransform.localScale = _ringScale * pulse;
        }
        if (ReadyGlow != null)
        {
            if (ReadyGlow.enabled != ready) ReadyGlow.enabled = ready;
            if (ready)
            {
                ReadyGlow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -Time.time * GlowSpin);
                Color g = ReadyColor;
                g.a = 0.65f + 0.35f * Mathf.Sin(Time.time * PulseRate * Mathf.PI * 2f);
                ReadyGlow.color = g;
            }
        }
        if (Percent != null && percent != _shownPercent)
        {
            _shownPercent = percent;
            Percent.enabled = !ready;
            Percent.text = percent + "%";
        }
    }

    // Solo a los aliados del jugador local, y nunca sobre uno mismo.
    private bool Visible()
    {
        if (_pc == null || _asc == null) return false;
        PlayerController local = PlayerController.LocalPlayer;
        if (local == null || local == _pc) return false;
        AbilitySystemComponent localAsc = local.GetComponent<AbilitySystemComponent>();
        return localAsc != null && !localAsc.IsEnemyOf(_asc);
    }
}
