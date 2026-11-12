using UnityEngine;

// ============================================================
// TorchFlicker
//
// Hace que una antorcha (Prefabs/Map/Torch, 12 de noviembre de 2026) parezca fuego: la luz sube y baja
// de intensidad y la llama cambia un poco de tamaño, con ruido suave. Cada antorcha arranca en un punto
// distinto del ruido, así cien antorchas no titilan al mismo tiempo.
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

    private float _baseIntensity;
    private Vector3 _baseFlameScale;
    private float _seed;

    private void Awake()
    {
        if (Glow != null) _baseIntensity = Glow.intensity;
        if (Flame != null) _baseFlameScale = Flame.localScale;
        _seed = Random.value * 100f;
    }

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
    }
}
