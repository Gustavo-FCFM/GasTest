using UnityEngine;
using System.Collections.Generic;

// ============================================================
// ShieldBlockFlash
//
// Feedback visual del escudo al frenar un golpe: la barrera pega un destello y
// (opcional) suelta un VFX en el punto exacto del impacto.
//
// Escucha el evento OnFlash de Entity_ShieldBarrier, que a diferencia de
// OnDamageBlocked llega a TODOS los peers: el bloqueo se resuelve solo en el
// servidor, así que sin esa réplica el que dispara nunca vería que su tiro pegó
// contra un escudo — que es justo lo que tiene que leer para dejar de disparar y
// buscar el flanco.
//
// SETUP: va en el MISMO GameObject que Entity_ShieldBarrier (no en el hijo visual:
// ese se desactiva al bajar el escudo y este componente dejaría de correr). Pinta
// todos los Renderer que cuelguen de la barrera, así que sirve igual con un Quad
// suelto o con un escudo compuesto de varias piezas.
//
// El tinte se aplica con MaterialPropertyBlock: NO instancia materiales, así que no
// hay fugas ni se pisan entre varios Paladines que compartan el mismo asset.
// ============================================================
[RequireComponent(typeof(Entity_ShieldBarrier))]
public class ShieldBlockFlash : MonoBehaviour
{
    [Header("Destello")]
    [Tooltip("Color al que salta la barrera en el impacto. El alpha importa: con un material " +
             "transparente es lo que la vuelve momentáneamente más sólida.")]
    public Color FlashColor = new Color(1f, 0.95f, 0.6f, 0.75f);

    [Tooltip("Cuánto tarda en volver a su color normal, en segundos.")]
    public float FlashDuration = 0.22f;

    [Tooltip("Curva del apagado: en 1 está a pleno destello y en 0 ya volvió al color base. " +
             "La de por defecto (rápida al principio) es la que mejor lee como 'impacto'.")]
    public AnimationCurve FlashFalloff = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Tooltip("Nombre de la propiedad de color del shader. En URP (Lit y Unlit) es _BaseColor; " +
             "en el pipeline viejo o en un shader propio puede ser _Color.")]
    public string ColorProperty = "_BaseColor";

    [Header("VFX de Impacto (opcional)")]
    [Tooltip("Prefab que se instancia en el punto exacto del impacto, orientado hacia afuera de " +
             "la barrera. Dejalo vacío si solo querés el destello del panel.")]
    public GameObject ImpactVFXPrefab;

    [Tooltip("Segundos tras los que se destruye el VFX instanciado.")]
    public float ImpactVFXLifetime = 1.5f;

    [Header("Filtro")]
    [Tooltip("Impactos seguidos más juntos que esto no reinician el destello, para que un ataque " +
             "rápido o los ticks de un área no lo dejen clavado en el color de destello.")]
    public float MinTimeBetweenFlashes = 0.05f;

    private Entity_ShieldBarrier _barrier;
    private Renderer[]           _renderers;
    private Color[]              _baseColors;
    private MaterialPropertyBlock _block;
    private int                  _colorId;

    private float _flashTimer;      // segundos restantes de destello
    private float _lastFlashTime;

    // =========================================================
    // CICLO DE VIDA
    // =========================================================

    private void Awake()
    {
        _barrier   = GetComponent<Entity_ShieldBarrier>();
        _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        _block     = new MaterialPropertyBlock();
        _colorId   = Shader.PropertyToID(ColorProperty);

        // Color de partida de cada renderer, para saber a qué volver. Se lee del
        // sharedMaterial (no de .material) justamente para no instanciar nada.
        _baseColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            Material mat = _renderers[i] != null ? _renderers[i].sharedMaterial : null;
            _baseColors[i] = (mat != null && mat.HasProperty(_colorId))
                ? mat.GetColor(_colorId)
                : Color.white;
        }

        if (_renderers.Length == 0)
            Debug.LogWarning($"[{name}] ShieldBlockFlash no encontró ningún Renderer bajo la barrera: " +
                             $"el destello no se va a ver. Agregá el visual del escudo como hijo de " +
                             $"este objeto (ver Entity_ShieldBarrier).");
    }

    private void OnEnable()  { if (_barrier != null) _barrier.OnFlash += HandleFlash; }
    private void OnDisable()
    {
        if (_barrier != null) _barrier.OnFlash -= HandleFlash;

        // Si nos apagan a mitad de un destello, dejamos los materiales como estaban.
        _flashTimer = 0f;
        ApplyTint(0f);
    }

    // =========================================================
    // DESTELLO
    // =========================================================

    // Llega un impacto: reinicia el destello y suelta el VFX en el punto de contacto.
    private void HandleFlash(Vector3 hitPoint)
    {
        if (Time.time - _lastFlashTime < MinTimeBetweenFlashes) return;
        _lastFlashTime = Time.time;

        _flashTimer = FlashDuration;
        SpawnImpactVFX(hitPoint);
    }

    private void Update()
    {
        if (_flashTimer <= 0f) return;

        _flashTimer -= Time.deltaTime;

        // 1 recién impactado → 0 ya apagado.
        float t = FlashDuration > 0f ? Mathf.Clamp01(_flashTimer / FlashDuration) : 0f;
        ApplyTint(FlashFalloff != null ? FlashFalloff.Evaluate(1f - t) : t);

        if (_flashTimer <= 0f) ApplyTint(0f);
    }

    // Mezcla el color base con el de destello según 'amount' (0 = base, 1 = destello).
    private void ApplyTint(float amount)
    {
        if (_renderers == null) return;

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer r = _renderers[i];
            if (r == null) continue;

            r.GetPropertyBlock(_block);
            _block.SetColor(_colorId, Color.Lerp(_baseColors[i], FlashColor, amount));
            r.SetPropertyBlock(_block);
        }
    }

    // Instancia el VFX en el impacto, mirando hacia AFUERA de la barrera (o sea, hacia
    // el atacante): así un anillo o un plano de impacto queda encarando bien sin que
    // haya que rotarlo a mano en el prefab.
    private void SpawnImpactVFX(Vector3 hitPoint)
    {
        if (ImpactVFXPrefab == null) return;

        GameObject vfx = Instantiate(ImpactVFXPrefab, hitPoint, Quaternion.LookRotation(transform.forward));
        if (ImpactVFXLifetime > 0f) Destroy(vfx, ImpactVFXLifetime);
    }
}
