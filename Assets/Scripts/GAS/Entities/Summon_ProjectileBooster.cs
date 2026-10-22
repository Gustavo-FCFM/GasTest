using System.Collections.Generic;
using UnityEngine;

// ============================================================
// Summon_ProjectileBooster  (lo que potencia proyectiles — 22 de octubre de 2026)
//
// "Los proyectiles de mi dueño que me crucen salen potenciados." Va al lado de un Entity_Summon
// en el prefab: el Muro de fuego del Mago. Un proyectil del MISMO dueño que lo atraviesa queda
// potenciado (si su habilidad tiene versión potenciada: GA_ProjectileShoot "Explosión
// potenciada"), crece a la vista y al chocar suelta la explosión potenciada: la Gran bola de fuego
// de la Evocación. Los demás proyectiles lo cruzan sin cambios.
//
// No tiene collider: es la caja de su Summon_Zone (forma Caja), o la de acá si no tiene zona, que
// se mira con cuentas contra el tramo que recorre cada proyectil (GC_Projectile, en el servidor).
// ============================================================
[RequireComponent(typeof(Entity_Summon))]
public class Summon_ProjectileBooster : MonoBehaviour
{
    [Tooltip("Si la invocación no tiene una Summon_Zone con forma Caja: el tamaño de lo que potencia " +
             "(ancho de lado a lado × alto × largo), apoyado en el piso.")]
    [Label("Caja si no hay zona (m)")]
    public Vector3 FallbackBox = new Vector3(10f, 2f, 2f);

    private Entity_Summon _summon;
    private Summon_Zone   _zone;

    private static readonly List<Summon_ProjectileBooster> s_active = new List<Summon_ProjectileBooster>();

    private void Awake()
    {
        _summon = GetComponent<Entity_Summon>();
        _zone   = GetComponent<Summon_Zone>();
    }
    private void OnEnable()  => s_active.Add(this);
    private void OnDisable() => s_active.Remove(this);

    // ¿El tramo from → to cruza alguno de los de este tirador? Servidor (el Summoner solo existe ahí).
    public static bool Crosses(Vector3 from, Vector3 to, AbilitySystemComponent shooter)
    {
        if (shooter == null) return false;
        foreach (Summon_ProjectileBooster booster in s_active)
            if (booster != null && booster._summon != null && booster._summon.Summoner == shooter &&
                booster.SegmentHits(from, to))
                return true;
        return false;
    }

    private Vector3 BoxSize => _zone != null && _zone.Shape == Summon_Zone.EShape.Box
        ? new Vector3(_zone.BoxWidth, _zone.BoxHeight, _zone.BoxDepth) : FallbackBox;

    // El tramo contra la caja (apoyada en el piso y girada con la invocación), en su espacio local.
    private bool SegmentHits(Vector3 from, Vector3 to)
    {
        Vector3 size   = BoxSize;
        Vector3 center = transform.position + transform.up * (size.y * 0.5f);
        Quaternion inv = Quaternion.Inverse(transform.rotation);
        Vector3 a = inv * (from - center), b = inv * (to - center);
        Vector3 half = size * 0.5f, d = b - a;

        float tMin = 0f, tMax = 1f;
        for (int axis = 0; axis < 3; axis++)
        {
            if (Mathf.Abs(d[axis]) < 1e-6f)
            {
                if (a[axis] < -half[axis] || a[axis] > half[axis]) return false;
                continue;
            }
            float t1 = (-half[axis] - a[axis]) / d[axis], t2 = (half[axis] - a[axis]) / d[axis];
            if (t1 > t2) (t1, t2) = (t2, t1);
            tMin = Mathf.Max(tMin, t1);
            tMax = Mathf.Min(tMax, t2);
            if (tMin > tMax) return false;
        }
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 size = Application.isPlaying ? BoxSize
                     : (GetComponent<Summon_Zone>() is Summon_Zone z && z.Shape == Summon_Zone.EShape.Box
                        ? new Vector3(z.BoxWidth, z.BoxHeight, z.BoxDepth) : FallbackBox);
        Gizmos.color  = new Color(1f, 0.6f, 0.1f, 0.5f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position + transform.up * (size.y * 0.5f), transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, size);
        Gizmos.matrix = Matrix4x4.identity;
    }
}
