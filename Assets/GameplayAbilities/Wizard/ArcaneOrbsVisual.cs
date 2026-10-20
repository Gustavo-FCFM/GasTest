using UnityEngine;

// ============================================================
// ArcaneOrbsVisual  (los puntos arcanos del Mago, a la vista — 20 de octubre de 2026)
//
// Va en el VFX de GE_ArcanePoint ("VFX en el objetivo"): bolas azules que giran alrededor del
// marcado, UNA POR PUNTO, repartidas parejo, soltando estrellitas.
//
// La red pone un solo VFX por efecto aunque se acumule (ver NetworkASC.SpawnEffectVfx), así que
// cuántas bolas se ven lo decide este script: lee las acumulaciones del efecto, que ya viajan a
// todas las pantallas (las mismas que el número de la barra de debuffs) y prende tantas bolas
// como puntos. Las demás quedan apagadas hasta que lleguen más puntos.
// ============================================================
public class ArcaneOrbsVisual : MonoBehaviour
{
    [Tooltip("El efecto cuyos puntos se cuentan (GE_ArcanePoint).")]
    [Label("Efecto que cuenta")]
    public GameplayEffect PointEffect;

    [Tooltip("Una bola por punto, en orden: con 2 puntos se prenden las dos primeras.")]
    [Label("Bolas")]
    public Transform[] Orbs;

    [Label("Radio del giro (m)")]
    public float Radius = 0.7f;

    [Label("Vueltas por segundo")]
    public float TurnsPerSecond = 0.4f;

    [Tooltip("Cuánto sube y baja cada bola mientras gira. 0 = giran a la misma altura.")]
    [Label("Subir y bajar (m)")]
    public float BobHeight = 0.12f;

    [Label("Subir y bajar: veces por segundo")]
    public float BobPerSecond = 0.8f;

    private NetworkAbilitySystemComponent _net;
    private int   _effectIndex = -2;   // -2 = todavía no se buscó en el registro
    private float _angle;

    private void Awake() => _net = GetComponentInParent<NetworkAbilitySystemComponent>();

    // Cuántos puntos tiene el marcado. Si no se puede saber (sin red, o el efecto no está en el
    // registro), una bola: el VFX existe porque el efecto está activo.
    private int CurrentPoints()
    {
        if (_net == null || PointEffect == null) return 1;

        if (_effectIndex == -2)
        {
            GameplayEffectRegistry registry = GameplayEffectRegistry.Instance;
            _effectIndex = registry != null ? registry.GetIndex(PointEffect) : -1;
        }
        if (_effectIndex < 0) return 1;

        return _net.TryGetNetActiveEffectStacks(_effectIndex, out int stacks) ? Mathf.Max(1, stacks) : 1;
    }

    private void LateUpdate()
    {
        if (Orbs == null || Orbs.Length == 0) return;

        int shown = Mathf.Clamp(CurrentPoints(), 1, Orbs.Length);
        _angle = Mathf.Repeat(_angle + TurnsPerSecond * 360f * Time.deltaTime, 360f);

        for (int i = 0; i < Orbs.Length; i++)
        {
            if (Orbs[i] == null) continue;

            bool on = i < shown;
            if (Orbs[i].gameObject.activeSelf != on) Orbs[i].gameObject.SetActive(on);
            if (!on) continue;

            float share = i / (float)shown;
            float a     = (_angle + 360f * share) * Mathf.Deg2Rad;
            float bob   = Mathf.Sin((Time.time * BobPerSecond + share) * 2f * Mathf.PI) * BobHeight;
            Orbs[i].localPosition = new Vector3(Mathf.Cos(a) * Radius, bob, Mathf.Sin(a) * Radius);
        }
    }
}
