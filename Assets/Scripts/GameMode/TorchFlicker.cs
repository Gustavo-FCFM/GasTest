using UnityEngine;
using UnityEngine.Rendering;

// ============================================================
// TorchFlicker
//
// Hace que una antorcha (Prefabs/Map/Torch, 12 de noviembre de 2026) parezca fuego: la luz sube y baja
// de intensidad y la llama cambia un poco de tamaño, con ruido suave. Cada antorcha arranca en un punto
// distinto del ruido, así cien antorchas no titilan al mismo tiempo.
//
// También lleva su SOMBRA: solo las más cercanas a la cámara la proyectan (las elige TorchShadows), y aparece
// y se va con un fundido. Cuántas y hasta dónde se cambian acá, en el prefab Torch.
//
// No es de red: es adorno, cada pantalla lo calcula por su cuenta.
// ============================================================
public class TorchFlicker : MonoBehaviour
{
    [Tooltip("La luz de la antorcha.")]
    [Label("Luz")]
    public Light Glow;

    [Tooltip("La llama (se agranda y achica un poco).")]
    [Label("Llama")]
    public Transform Flame;

    [Tooltip("Cuánto varía la intensidad de la luz, como fracción de la normal.")]
    [Label("Titileo de la luz (0 a 1)")]
    [Range(0f, 1f)] public float LightFlicker = 0.25f;

    [Tooltip("Cuánto varía el tamaño de la llama, como fracción del normal.")]
    [Label("Titileo de la llama (0 a 1)")]
    [Range(0f, 1f)] public float FlameFlicker = 0.15f;

    [Tooltip("Qué tan rápido titila.")]
    [Label("Rapidez (más = más rápido)")]
    public float Speed = 7f;

    [Header("Sombras")]
    [Tooltip("Cuántas antorchas proyectan sombra a la vez: las más cercanas a la cámara. Cada una dibuja la escena " +
             "seis veces más; 0 = ninguna. Se lee del prefab: es igual para todas.")]
    [Label("Sombras: cuántas a la vez")]
    [Range(0, 8)] public int ShadowCount = 4;

    [Tooltip("Más lejos que esto de la cámara, una antorcha no proyecta sombra aunque esté entre las más cercanas.")]
    [Label("Sombras: hasta (m)")]
    public float ShadowRange = 22f;

    [Tooltip("Cuánto tarda la sombra en aparecer o irse al pasar el turno a otra antorcha.")]
    [Label("Sombras: fundido (s)")]
    public float ShadowFade = 0.4f;

    private float _baseIntensity;
    private Vector3 _baseFlameScale;
    private float _seed;
    private float _shadow;   // 0 = sin sombra, 1 = sombra entera

    private void Awake()
    {
        if (Glow != null)
        {
            _baseIntensity = Glow.intensity;
            Glow.shadows = LightShadows.None;
            Glow.shadowStrength = 0f;
            // La resolución de la sombra (baja) está en la luz del prefab: en juego URP no deja cambiarla.
        }
        if (Flame != null) _baseFlameScale = Flame.localScale;
        _seed = Random.value * 100f;
    }

    private void OnEnable()  => TorchShadows.Register(this);
    private void OnDisable() => TorchShadows.Unregister(this);

    private void Update()
    {
        float t = Time.time * Speed + _seed;
        float n = Mathf.PerlinNoise(t, _seed) * 2f - 1f;   // -1..1

        if (Glow != null) Glow.intensity = _baseIntensity * (1f + n * LightFlicker);
        if (Flame != null)
        {
            float s = 1f + n * FlameFlicker;
            float tall = 1f + (Mathf.PerlinNoise(_seed, t * 1.3f) * 2f - 1f) * FlameFlicker * 1.5f;
            Flame.localScale = new Vector3(_baseFlameScale.x * s, _baseFlameScale.y * tall, _baseFlameScale.z * s);
        }

        UpdateShadow();
    }

    // La sombra entra y sale con un fundido; apagada del todo, la luz no la calcula.
    private void UpdateShadow()
    {
        if (Glow == null) return;
        TorchShadows.Tick(this);

        float target = TorchShadows.IsChosen(this) ? 1f : 0f;
        if (target <= 0f && _shadow <= 0f) return;   // lo común: ni la tiene ni la quiere

        _shadow = Mathf.MoveTowards(_shadow, target, ShadowFade > 0f ? Time.deltaTime / ShadowFade : 1f);
        Glow.shadows = _shadow > 0f ? LightShadows.Soft : LightShadows.None;
        Glow.shadowStrength = _shadow;
    }
}
