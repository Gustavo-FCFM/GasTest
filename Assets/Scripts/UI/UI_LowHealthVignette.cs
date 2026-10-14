using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_LowHealthVignette
//
// Con poca vida, el borde de la pantalla del jugador local late en rojo: más fuerte y más
// rápido cuanto menos vida queda. Avisa "retírate" sin tener que mirar la barra del HUD.
//
// VIVE EN EL PREFAB DE LA CÁMARA (Player Camera ▸ UI ▸ LowHealthVignette): es una Image que
// cubre la pantalla con una textura transparente en el centro y opaca hacia los bordes
// (Art/UI/Vignette_LowHealth.png). El COLOR de la Image en el prefab es el de la viñeta en su
// punto más fuerte (con la vida casi en cero), opacidad incluida: así se ve en el prefab tal
// cual va a verse. Al empezar el juego este script la apaga, y la va prendiendo según la vida.
// ============================================================
[RequireComponent(typeof(Image))]
public class UI_LowHealthVignette : MonoBehaviour
{
    [Tooltip("Por debajo de esta vida empieza a verse la viñeta, en porcentaje de la vida máxima.")]
    [Label("Se ve con la vida debajo de (%)")]
    [Range(0f, 100f)]
    public float LowHealthPercent = 30f;

    [Tooltip("Qué tan fuerte se ve justo al entrar en vida baja, comparada con la del prefab " +
             "(que es la de la vida casi en cero). 35 = al 35 %. De ahí sube hasta el 100 %.")]
    [Label("Fuerza al entrar en vida baja (%)")]
    [Range(0f, 100f)]
    public float StartStrengthPercent = 35f;

    [Tooltip("Latidos por segundo justo al entrar en vida baja.")]
    [Label("Latidos por segundo al entrar")]
    public float PulsesPerSecondAtStart = 1.2f;

    [Tooltip("Latidos por segundo con la vida casi en cero.")]
    [Label("Latidos por segundo casi muerto")]
    public float PulsesPerSecondNearDeath = 3f;

    [Tooltip("Cuánto se apaga en cada latido. 35 = baja hasta el 65 % y vuelve: se lee como un " +
             "pulso y no como algo que parpadea.")]
    [Label("Cuánto baja en cada latido (%)")]
    [Range(0f, 100f)]
    public float PulseDepthPercent = 35f;

    private Image _image;
    private Color _fullColor;
    private float _pulsePhase;

    // Los componentes del jugador local, cacheados: esto corre cada frame y el jugador
    // local solo cambia al reaparecer o cambiar de clase.
    private PlayerController              _cachedLocal;
    private AbilitySystemComponent        _localAsc;
    private NetworkAbilitySystemComponent _localNet;

    private void Awake()
    {
        _image = GetComponent<Image>();
        _image.raycastTarget = false;
        _fullColor = _image.color;
        _image.enabled = false;
    }

    private void Update()
    {
        float strength = 0f;
        float speed    = PulsesPerSecondAtStart;
        float limit    = LowHealthPercent / 100f;

        if (TryGetLocalHealth(out float health, out float max) && max > 0f && health > 0f && limit > 0f)
        {
            float fraction = health / max;
            if (fraction < limit)
            {
                // 0 justo en el umbral, 1 con la vida casi en cero.
                float danger = 1f - fraction / limit;
                strength = Mathf.Lerp(StartStrengthPercent / 100f, 1f, danger);
                speed    = Mathf.Lerp(PulsesPerSecondAtStart, PulsesPerSecondNearDeath, danger);
            }
        }

        _pulsePhase += Time.deltaTime * speed * Mathf.PI * 2f;
        float depth = PulseDepthPercent / 100f;
        float pulse = 1f - depth * (0.5f - 0.5f * Mathf.Sin(_pulsePhase));

        Color c = _fullColor;
        c.a = _fullColor.a * strength * pulse;
        _image.color   = c;
        _image.enabled = c.a > 0.001f;
    }

    // La vida del jugador local: la sincronizada por red si existe (sirve igual en el
    // host y en un cliente), si no la del ASC.
    private bool TryGetLocalHealth(out float health, out float max)
    {
        health = max = 0f;

        PlayerController local = PlayerController.LocalPlayer;
        if (local != _cachedLocal)
        {
            _cachedLocal = local;
            _localAsc    = local != null ? local.GetComponent<AbilitySystemComponent>()        : null;
            _localNet    = local != null ? local.GetComponent<NetworkAbilitySystemComponent>() : null;
        }
        if (_localAsc == null) return false;

        if (_localNet != null)
        {
            health = _localNet.NetHealth;
            max    = _localNet.NetMaxHealth;
            return true;
        }

        health = _localAsc.GetAttributeValue(EAttributeType.Health);
        max    = _localAsc.GetAttributeValue(EAttributeType.MaxHealth);
        return true;
    }
}
