using UnityEngine;

// ============================================================
// CombatCameraShake
//
// La sacudida de cámara: un temblor corto al recibir un golpe fuerte y uno más notorio al lanzar
// la definitiva. La fuerza se acumula y se va gastando, y el temblor es la fuerza al cuadrado: así
// un golpe flojo apenas se nota y uno fuerte sí, y se calma suave.
//
// SOLO GIRA y solo mientras se dibuja: se aplica justo antes de dibujar (onBeforeRender) y se
// deshace al empezar el cuadro siguiente, antes que todo lo demás (por eso el orden). Así la mira,
// el cuerpo que sigue a la cámara y los números flotantes nunca ven la cámara sacudida, y al no
// moverla de lugar no se mete en las paredes.
//
// VIVE EN EL PREFAB DE PARTIDA (Match_UI ▸ CombatFeel). Sus números, en el asset CombatFeel.
// ============================================================
[DefaultExecutionOrder(-1000)]
public class CombatCameraShake : MonoBehaviour
{
    private readonly LocalDamageWatch _watch = new LocalDamageWatch();

    private float      _strength;   // 0 a 1
    private Transform  _shaken;     // la cámara, mientras está girada
    private Quaternion _restore;

    private void OnEnable()
    {
        Application.onBeforeRender += Apply;
        CombatFeel.UltimateCast    += OnUltimateCast;
    }

    private void OnDisable()
    {
        Application.onBeforeRender -= Apply;
        CombatFeel.UltimateCast    -= OnUltimateCast;
        Restore();
    }

    private void OnUltimateCast() => Add(CombatFeel.Config.ShakeUltimateStrength);

    private void Add(float strength) => _strength = Mathf.Max(_strength, strength);

    private void Update()
    {
        Restore();

        CombatFeel feel = CombatFeel.Config;

        float lost = _watch.PollLostPercent();
        if (lost >= feel.ShakeMinHitPercent)
        {
            float power = Mathf.InverseLerp(feel.ShakeMinHitPercent, feel.ShakeFullHitPercent, lost);
            Add(Mathf.Lerp(feel.ShakeWeakStrength, feel.ShakeStrongStrength, power));
        }

        _strength = Mathf.MoveTowards(_strength, 0f, Time.deltaTime / Mathf.Max(0.01f, feel.ShakeSeconds));
    }

    private void Apply()
    {
        if (_strength <= 0f || _shaken != null || !CombatFeel.CameraShakeOn) return;

        Camera camera = Camera.main;
        if (camera == null) return;

        CombatFeel feel = CombatFeel.Config;
        float angle = feel.ShakeMaxAngle * _strength * _strength;
        float time  = Time.time * feel.ShakeFrequency;

        _shaken  = camera.transform;
        _restore = _shaken.localRotation;
        _shaken.localRotation = _restore * Quaternion.Euler(Noise(time, 11f) * angle,
                                                            Noise(time, 37f) * angle,
                                                            Noise(time, 71f) * angle * 0.5f);
    }

    private void Restore()
    {
        if (_shaken != null) _shaken.localRotation = _restore;
        _shaken = null;
    }

    // Un temblor suave entre -1 y 1. Cada eje lee otra fila del ruido para no moverse juntos.
    private static float Noise(float time, float row) => Mathf.PerlinNoise(time, row) * 2f - 1f;
}
