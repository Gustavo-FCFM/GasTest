using UnityEngine;
using System.Collections;

// ============================================================
// VFX_AreaVisual
//
// Va en la RAÍZ de un prefab de VFX circular (auras, zonas de daño, explosiones) y
// resuelve los dos problemas de usar assets descargados con las habilidades:
//
//  1) TAMAÑO. Antes cada habilidad escalaba el VFX con un "VisualScaleMultiplier"
//     ajustado a ojo, que no tenía ninguna relación con el radio real del asset: al
//     cambiar el Radius de la habilidad (o al usar otro prefab) había que volver a
//     tantear. Acá se declara UNA vez cuánto mide el efecto a escala 1
//     (RadiusAtScaleOne) y el cálculo pasa a ser exacto: escala = radio / esa medida.
//     Con eso el círculo coincide con el área real de daño, siempre.
//
//  2) DURACIÓN. Estos assets suelen ser 'looping', así que se reinician solos cada
//     pocos segundos y, al destruirlos, las partículas vivas desaparecen de golpe.
//     PlayFor() corta la EMISIÓN cuando la habilidad termina y recién destruye el
//     objeto cuando las últimas partículas se apagaron: el efecto se desvanece en vez
//     de cortarse.
//
// Es solo presentación: no toca gameplay ni red (cada peer instancia su propia copia).
// ============================================================
public class VFX_AreaVisual : MonoBehaviour
{
    [Tooltip("Radio en METROS que cubre este efecto con la escala en 1. Es la medida que " +
             "permite calcular la escala exacta para cualquier radio de habilidad.\n\n" +
             "Para medirlo: poné el prefab en la escena con escala 1 junto a un objeto con " +
             "AbilityPreview (que dibuja anillos de 1m) y fijate hasta qué anillo llega el borde. " +
             "También podés usar 'Estimar desde las partículas' en el menú del componente.")]
    public float RadiusAtScaleOne = 4f;

    [Tooltip("Segundos extra que se deja vivo el objeto tras cortar la emisión, para que las " +
             "partículas que quedaron se desvanezcan. 0 = usar el startLifetime más largo del prefab.")]
    public float FadeOutTime = 0f;

    // Ajusta la escala para que el efecto cubra EXACTAMENTE ese radio.
    public void SetRadius(float radius)
    {
        if (RadiusAtScaleOne <= 0.0001f || radius <= 0f) return;

        float scale = radius / RadiusAtScaleOne;
        transform.localScale = new Vector3(scale, scale, scale);
    }

    // Deja el efecto vivo 'duration' segundos y después lo apaga con desvanecido.
    // duration <= 0 = no se apaga solo (lo destruye quien lo creó).
    public void PlayFor(float duration)
    {
        if (duration > 0f) StartCoroutine(StopAfter(duration));
    }

    private IEnumerator StopAfter(float duration)
    {
        yield return new WaitForSeconds(duration);

        // Cortamos la emisión pero dejamos vivir a las partículas ya emitidas: sin
        // esto, destruir el objeto las borra de golpe y el corte se nota.
        float fade = FadeOutTime;
        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (FadeOutTime <= 0f)
                fade = Mathf.Max(fade, ps.main.startLifetime.constantMax);
        }

        Destroy(gameObject, Mathf.Max(0.1f, fade));
    }

    // Punto de entrada para las habilidades: escala y programa el apagado en una
    // llamada, y si el prefab NO tiene este componente cae al multiplicador de siempre.
    // Así los prefabs viejos siguen funcionando igual.
    public static void Configure(GameObject vfx, float radius, float fallbackMultiplier, float duration)
    {
        if (vfx == null) return;

        VFX_AreaVisual area = vfx.GetComponent<VFX_AreaVisual>();
        if (area != null)
        {
            area.SetRadius(radius);
            area.PlayFor(duration);
            return;
        }

        // Sin el componente: comportamiento anterior (escala a ojo, corte seco).
        float scale = radius * fallbackMultiplier;
        vfx.transform.localScale = new Vector3(scale, scale, scale);
        if (duration > 0f) Destroy(vfx, duration);
    }

    // Vista previa del radio que cubre AHORA (ya con la escala aplicada). Combinada con
    // los anillos de AbilityPreview, sirve para medir RadiusAtScaleOne sin adivinar.
    private void OnDrawGizmosSelected()
    {
        float radius = RadiusAtScaleOne * transform.lossyScale.x;
        if (radius <= 0f) return;

        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.8f);
        Vector3 prev = transform.position + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= 48; i++)
        {
            float a = (Mathf.PI * 2f) * i / 48;
            Vector3 p = transform.position + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }

    // Estimación automática: en estos assets el círculo suele ser UNA partícula plana
    // cuyo tamaño ES el diámetro, así que el radio ≈ startSize / 2. Toma el sistema
    // más grande del prefab. Es un punto de partida: verificá con el gizmo.
    [ContextMenu("Estimar desde las partículas")]
    private void EstimateFromParticles()
    {
        float biggest = 0f;
        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
            biggest = Mathf.Max(biggest, ps.main.startSize.constantMax);

        if (biggest <= 0f) { Debug.LogWarning("[VFX_AreaVisual] No encontré partículas con tamaño."); return; }

        RadiusAtScaleOne = biggest * 0.5f;
        Debug.Log($"[VFX_AreaVisual] RadiusAtScaleOne estimado en {RadiusAtScaleOne:0.##}m " +
                  $"(partícula más grande: {biggest:0.##}). Verificá con el gizmo y los anillos de AbilityPreview.");
    }
}
