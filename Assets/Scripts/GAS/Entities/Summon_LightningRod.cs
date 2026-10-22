using System.Collections.Generic;
using UnityEngine;

// ============================================================
// Summon_LightningRod  (el pararrayos de una invocación — 21 de octubre de 2026)
//
// "Los rayos de mi equipo que me crucen, se frenan en mí y yo los reparto." Va al lado de un
// Entity_Summon en el prefab: la Tormenta eléctrica de la Evocación (el tornado azul; la del kit
// base no lo lleva). Con "Crece por rayo", cada uno la agranda (la Cadena de relámpagos). Cuando una
// Descarga eléctrica (GA_ChainLightning) del dueño o de un aliado cruza este cilindro, el rayo
// se FRENA acá y el tornado tira un rayo a cada enemigo a "Alcance de sus rayos" que vea, y cada
// uno arranca su rebote. Cada Descarga que lo cruce lo dispara otra vez.
//
// No tiene collider: es un cilindro (Radio × Alto, desde el pivote hacia arriba) que se mira con
// cuentas, así no frena a nadie que camine ni a los proyectiles, y solo los rayos lo ven. Lo
// consulta el servidor (es quien resuelve los tiros).
// ============================================================
[RequireComponent(typeof(Entity_Summon))]
public class Summon_LightningRod : MonoBehaviour
{
    [Tooltip("Qué tan ancho es lo que recibe el rayo (el cuerpo del tornado), desde su centro.")]
    [Label("Radio que recibe el rayo (m)")]
    public float Radius = 1.5f;

    [Tooltip("Desde el piso hacia arriba.")]
    [Label("Alto que recibe el rayo (m)")]
    public float Height = 6f;

    [Tooltip("Hasta dónde llegan los rayos que tira a los enemigos (y desde ahí rebotan).")]
    [Label("Alcance de sus rayos (m)")]
    public float StrikeRange = 8f;

    [Tooltip("Cada rayo que lo cruza lo AGRANDA esto: el radio de su zona y el alcance de sus rayos " +
             "(y lo que recibe el rayo crece en proporción). La Cadena de relámpagos de la Evocación. " +
             "0 = no crece.")]
    [Label("Crece por rayo (m)")]
    public float GrowthPerStrike = 0f;

    [Tooltip("Lo más que puede crecer en total.")]
    [Label("Crece hasta (m)")]
    public float MaxGrowth = 4f;

    private Entity_Summon _summon;
    private Summon_Zone   _zone;

    // Lo que creció (lo guarda la invocación: viaja a todas las pantallas).
    private float Growth => _summon != null ? _summon.ZoneGrowth : 0f;

    // Hasta dónde llegan sus rayos ahora.
    public float CurrentStrikeRange => StrikeRange + Growth;

    // Lo que recibe el rayo crece en proporción con su zona (el tornado se ve más grande).
    private float SizeFactor => _zone != null && _zone.Radius > 0.01f ? (_zone.Radius + Growth) / _zone.Radius : 1f;

    // Un rayo lo cruzó: crece (si crece). Servidor; lo llama GA_ChainLightning.
    public void OnStruck()
    {
        if (GrowthPerStrike > 0f && _summon != null && _summon.IsServerInitialized)
            _summon.GrowZone(GrowthPerStrike, MaxGrowth);
    }

    private static readonly List<Summon_LightningRod> s_active = new List<Summon_LightningRod>();

    private void Awake()
    {
        _summon = GetComponent<Entity_Summon>();
        _zone   = GetComponent<Summon_Zone>();
    }

    private void OnEnable()  => s_active.Add(this);
    private void OnDisable() => s_active.Remove(this);

    // El primer pararrayos que corta el rayo (desde 'origin' hacia 'dir', hasta 'maxDistance'), de
    // los del que tira o de su equipo. 'thickness' engorda el cilindro (el grosor de la bala).
    // null = ninguno. Servidor.
    public static Summon_LightningRod FirstAlong(Vector3 origin, Vector3 dir, float maxDistance, float thickness,
                                                 AbilitySystemComponent shooter, out float distance)
    {
        distance = maxDistance;
        Summon_LightningRod best = null;

        foreach (Summon_LightningRod rod in s_active)
        {
            if (rod == null || rod._summon == null || !rod.BelongsTo(shooter)) continue;
            if (!rod.Intersects(origin, dir, thickness, out float t) || t >= distance) continue;
            distance = t;
            best     = rod;
        }
        return best;
    }

    // Del que tira, o de su equipo (el Summoner solo existe en el servidor, que es quien pregunta).
    private bool BelongsTo(AbilitySystemComponent shooter)
        => shooter != null && (_summon.Summoner == shooter || _summon.IsAlly(shooter));

    // El rayo contra el cilindro (vertical, desde el pivote hasta Alto): la distancia a la que
    // entra. Tirado desde ADENTRO no cuenta: el Mago parado en su tornado le pega a lo que apunta.
    private bool Intersects(Vector3 origin, Vector3 dir, float thickness, out float t)
    {
        t = 0f;
        Vector3 basePos = transform.position;
        float   r       = Radius * SizeFactor + Mathf.Max(0f, thickness);

        // En el plano: cuándo está a menos de r del eje.
        float ox = origin.x - basePos.x, oz = origin.z - basePos.z;
        float a  = dir.x * dir.x + dir.z * dir.z;
        float c  = ox * ox + oz * oz - r * r;
        float enter, exit;
        if (a < 1e-6f)
        {
            if (c > 0f) return false;   // vertical y por fuera
            enter = float.NegativeInfinity; exit = float.PositiveInfinity;
        }
        else
        {
            float b    = 2f * (ox * dir.x + oz * dir.z);
            float disc = b * b - 4f * a * c;
            if (disc < 0f) return false;
            float sq = Mathf.Sqrt(disc);
            enter = (-b - sq) / (2f * a);
            exit  = (-b + sq) / (2f * a);
        }

        // En la altura: entre el piso y el tope.
        float bottom = basePos.y, top = basePos.y + Height * SizeFactor;
        if (Mathf.Abs(dir.y) < 1e-6f)
        {
            if (origin.y < bottom || origin.y > top) return false;
        }
        else
        {
            float t0 = (bottom - origin.y) / dir.y, t1 = (top - origin.y) / dir.y;
            if (t0 > t1) (t0, t1) = (t1, t0);
            enter = Mathf.Max(enter, t0);
            exit  = Mathf.Min(exit, t1);
        }

        if (enter > exit || enter < 0f) return false;
        t = enter;
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.6f);
        Vector3 p = transform.position;
        for (int i = 0; i < 24; i++)
        {
            float a0 = i * Mathf.PI * 2f / 24f, a1 = (i + 1) * Mathf.PI * 2f / 24f;
            Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * Radius;
            Vector3 d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * Radius;
            Gizmos.DrawLine(p + d0, p + d1);
            Gizmos.DrawLine(p + d0 + Vector3.up * Height, p + d1 + Vector3.up * Height);
            if (i % 6 == 0) Gizmos.DrawLine(p + d0, p + d0 + Vector3.up * Height);
        }
        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.2f);
        Gizmos.DrawWireSphere(p + Vector3.up * 1f, StrikeRange);
    }
}
