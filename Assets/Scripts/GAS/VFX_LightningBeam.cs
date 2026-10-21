using UnityEngine;

// ============================================================
// VFX_LightningBeam  (un rayito de un punto a otro — 21 de octubre de 2026)
//
// Va en el prefab del rayo de la Descarga eléctrica del Mago (VFX_LightningBeam): una línea
// quebrada que tiembla unos instantes, se afina y desaparece. La dibuja cada pantalla por su
// cuenta (GameplayAbility.PlayBeams): por red viajan solo los dos puntos de cada tramo.
// El color y el grosor son los del LineRenderer del prefab.
// ============================================================
[RequireComponent(typeof(LineRenderer))]
public class VFX_LightningBeam : MonoBehaviour
{
    [Tooltip("Cuántos quiebres por metro: más = más zigzag.")]
    [Label("Quiebres por metro")]
    public float KinksPerMeter = 1.5f;

    [Tooltip("Cuánto se aparta cada quiebre de la línea recta.")]
    [Label("Desvío de los quiebres (m)")]
    public float Jitter = 0.25f;

    [Label("Dura (s)")]
    public float Seconds = 0.25f;

    [Tooltip("Cada cuánto vuelve a quebrarse distinto (el temblor).")]
    [Label("Tiembla cada (s)")]
    public float FlickerSeconds = 0.04f;

    private LineRenderer _line;
    private Vector3 _from, _to;
    private float _bornAt, _nextFlicker, _startWidth;

    // Un rayo de 'from' a 'to' con este prefab. Sin VFX_LightningBeam, se ignora.
    public static void Play(GameObject prefab, Vector3 from, Vector3 to)
    {
        if (prefab == null) return;
        GameObject go = Instantiate(prefab);
        VFX_LightningBeam beam = go.GetComponent<VFX_LightningBeam>();
        if (beam != null) beam.Show(from, to);
        else Destroy(go, 1f);
    }

    public void Show(Vector3 from, Vector3 to)
    {
        _line = GetComponent<LineRenderer>();
        _line.useWorldSpace = true;
        _from       = from;
        _to         = to;
        _bornAt     = Time.time;
        _startWidth = _line.widthMultiplier;
        Rebuild();
    }

    private void Update()
    {
        if (_line == null) return;

        float t = (Time.time - _bornAt) / Mathf.Max(0.01f, Seconds);
        if (t >= 1f) { Destroy(gameObject); return; }

        _line.widthMultiplier = _startWidth * (1f - t);
        if (Time.time >= _nextFlicker) Rebuild();
    }

    private void Rebuild()
    {
        _nextFlicker = Time.time + Mathf.Max(0.01f, FlickerSeconds);

        float length = Vector3.Distance(_from, _to);
        int kinks = Mathf.Clamp(Mathf.RoundToInt(length * KinksPerMeter), 1, 60);
        _line.positionCount = kinks + 1;
        for (int i = 0; i <= kinks; i++)
        {
            Vector3 p = Vector3.Lerp(_from, _to, i / (float)kinks);
            if (i > 0 && i < kinks) p += Random.insideUnitSphere * Jitter;
            _line.SetPosition(i, p);
        }
    }
}
