using System.Collections.Generic;
using UnityEngine;

// ============================================================
// MercTeamBase
//
// La base de UN equipo. Junta las tres cosas que un equipo tiene en su rincón del
// escenario:
//
//   1. SALA SEGURA: mientras un jugador de ESTE equipo está adentro, su vida se
//      mantiene topeada al máximo y no puede recibir daño (tag Status_Immunity, que
//      el core ya trata como inmunidad real). También es el ÚNICO lugar donde puede
//      cambiar de clase — se marca con el tag Status_SafeZone, que UI_ClassMenu
//      consulta antes de abrirse.
//   2. PUNTO DE ENTREGA: adónde hay que llevar el Objetivo para sumar un punto.
//   3. PUNTOS DE APARICIÓN: dónde entran y reaparecen sus jugadores.
//
// La detección corre SOLO EN EL SERVIDOR (los tags viajan solos a los clientes por
// NetTags) y usa un OverlapBox contra la capa de los personajes en vez de un
// OnTriggerEnter: así no depende de que el collider del jugador dispare eventos, no
// se pierde a nadie que aparezca YA adentro (el caso del spawn y del respawn), y no
// hace falta que la sala tenga un Rigidbody.
//
// La sala segura de un equipo NO protege a los enemigos que entren: ahí adentro son
// carne, que es justamente lo que desalienta acampar en la base ajena.
// ============================================================
public class MercTeamBase : MonoBehaviour
{
    [Header("Identidad")]
    [Tooltip("Equipo dueño de esta base: 1, 2 o 3.")]
    public int TeamID = 1;

    [Header("Sala segura")]
    [Tooltip("Centro de la sala segura. Si lo dejás vacío se usa el transform de este objeto.")]
    public Transform SafeRoomCenter;

    [Tooltip("Tamaño de la caja de la sala segura, en metros.")]
    public Vector3 SafeRoomSize = new Vector3(14f, 6f, 14f);

    [Tooltip("Capa donde viven los personajes. En este proyecto es 'Character' (7).")]
    public LayerMask CharacterLayer = 1 << 7;

    [Header("Entrega del Objetivo")]
    [Tooltip("Adónde hay que llevar el Objetivo. Si lo dejás vacío se usa el centro de la sala segura.")]
    public Transform DeliveryPoint;

    [Tooltip("Radio de entrega en metros: con acercarse a esta distancia alcanza.")]
    public float DeliveryRadius = 4f;

    [Header("Aparición de jugadores")]
    [Tooltip("Puntos donde aparecen y reaparecen los jugadores de este equipo. Se reparten en orden.")]
    public Transform[] SpawnPoints;

    // Quiénes están adentro AHORA (solo servidor). Guardamos el ASC porque es lo que
    // hay que tocar para curar y para poner/sacar los tags.
    private readonly HashSet<AbilitySystemComponent> _inside = new HashSet<AbilitySystemComponent>();

    // Buffer reutilizado por el OverlapBox: evita una alocación por chequeo.
    private readonly Collider[] _overlapBuffer = new Collider[32];

    private readonly List<AbilitySystemComponent> _leftBuffer = new List<AbilitySystemComponent>();
    private readonly HashSet<AbilitySystemComponent> _seenBuffer = new HashSet<AbilitySystemComponent>();

    private float _tickTimer;
    private int   _nextSpawnIndex;

    public Vector3 SafeRoomWorldCenter => SafeRoomCenter != null ? SafeRoomCenter.position : transform.position;
    public Vector3 DeliveryWorldPoint  => DeliveryPoint  != null ? DeliveryPoint.position  : SafeRoomWorldCenter;

    // =========================================================
    // CHEQUEO PERIÓDICO (SOLO SERVIDOR)
    // =========================================================

    private void Update()
    {
        // La lógica de la sala segura es autoritativa: solo la corre quien manda.
        // Sin MercenariesGameMode (escena de pruebas suelta) igual funciona local.
        if (!IsAuthority()) return;

        _tickTimer += Time.deltaTime;
        if (_tickTimer < 0.2f) return;
        _tickTimer = 0f;

        TickSafeRoom();
    }

    private static bool IsAuthority()
    {
        // En una escena en red, solo el servidor. En una escena sin red (probando
        // solo), no hay NetworkManager iniciado y corre igual para que se pueda testear.
        var im = FishNet.InstanceFinder.NetworkManager;
        if (im == null) return true;
        return im.IsServerStarted || !im.IsClientStarted;
    }

    private void TickSafeRoom()
    {
        Vector3 center     = SafeRoomWorldCenter;
        Quaternion rot     = SafeRoomCenter != null ? SafeRoomCenter.rotation : transform.rotation;
        Vector3 halfExtent = SafeRoomSize * 0.5f;

        int count = Physics.OverlapBoxNonAlloc(center, halfExtent, _overlapBuffer, rot,
                                               CharacterLayer, QueryTriggerInteraction.Collide);

        // 1) Marcar y curar a los que están adentro.
        HashSet<AbilitySystemComponent> seen = _seenBuffer;
        seen.Clear();
        for (int i = 0; i < count; i++)
        {
            Collider col = _overlapBuffer[i];
            if (col == null) continue;

            AbilitySystemComponent asc = col.GetComponentInParent<AbilitySystemComponent>();
            if (asc == null || asc.TeamID != TeamID) continue;      // la base solo cuida a los suyos
            if (asc.GetComponent<PlayerController>() == null) continue; // los NPCs no se refugian

            seen.Add(asc);

            if (_inside.Add(asc)) EnterSafeRoom(asc);

            // Vida topeada al máximo mientras esté adentro.
            float max = asc.GetAttributeValue(EAttributeType.MaxHealth);
            if (max > 0f && asc.GetAttributeValue(EAttributeType.Health) < max)
                asc.SetCurrentAttributeValue(EAttributeType.Health, max);
        }

        // 2) Sacarle el estado a los que ya no están (o dejaron de existir).
        _leftBuffer.Clear();
        foreach (AbilitySystemComponent asc in _inside)
            if (asc == null || !seen.Contains(asc)) _leftBuffer.Add(asc);

        foreach (AbilitySystemComponent asc in _leftBuffer)
        {
            _inside.Remove(asc);
            if (asc != null) ExitSafeRoom(asc);
        }
    }

    private void EnterSafeRoom(AbilitySystemComponent asc)
    {
        // Status_Immunity: el core lo trata como inmunidad REAL al daño (se saltea el
        // modificador entero en ExecuteInstantEffect, sin gastar escudo ni anotar
        // atacante). Status_SafeZone es solo la marca para la UI y el cambio de clase.
        asc.AddTag(EGameplayTag.Status_Immunity);
        asc.AddTag(EGameplayTag.Status_SafeZone);
    }

    private void ExitSafeRoom(AbilitySystemComponent asc)
    {
        asc.RemoveTag(EGameplayTag.Status_Immunity);
        asc.RemoveTag(EGameplayTag.Status_SafeZone);
    }

    // Si la base se destruye o se apaga con gente adentro, hay que devolverles los
    // tags: el conteo de AddTag/RemoveTag tiene que quedar balanceado o el jugador se
    // queda inmortal para siempre.
    private void OnDisable()
    {
        foreach (AbilitySystemComponent asc in _inside)
            if (asc != null) ExitSafeRoom(asc);
        _inside.Clear();
    }

    // =========================================================
    // CONSULTAS
    // =========================================================

    // ¿Está este punto dentro de la zona de entrega de esta base?
    public bool IsInDeliveryZone(Vector3 worldPos)
    {
        Vector3 a = worldPos;      a.y = 0f;
        Vector3 b = DeliveryWorldPoint; b.y = 0f;
        return Vector3.Distance(a, b) <= DeliveryRadius;
    }

    // ¿Está este punto dentro de la sala segura?
    public bool IsInsideSafeRoom(Vector3 worldPos)
    {
        Quaternion rot = SafeRoomCenter != null ? SafeRoomCenter.rotation : transform.rotation;
        Vector3 local  = Quaternion.Inverse(rot) * (worldPos - SafeRoomWorldCenter);
        Vector3 half   = SafeRoomSize * 0.5f;
        return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
    }

    // Punto de aparición siguiente (round-robin, para no encimar a los tres jugadores).
    public Transform GetSpawnPoint()
    {
        if (SpawnPoints == null || SpawnPoints.Length == 0) return transform;

        for (int i = 0; i < SpawnPoints.Length; i++)
        {
            Transform t = SpawnPoints[_nextSpawnIndex % SpawnPoints.Length];
            _nextSpawnIndex++;
            if (t != null) return t;
        }
        return transform;
    }

    // =========================================================
    // EDITOR
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        Color c = MercenariesGameMode.Instance != null
            ? MercenariesGameMode.Instance.GetTeamColor(TeamID)
            : Color.cyan;

        // Sala segura.
        Gizmos.color = new Color(c.r, c.g, c.b, 0.25f);
        Matrix4x4 old = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(SafeRoomWorldCenter,
            SafeRoomCenter != null ? SafeRoomCenter.rotation : transform.rotation, Vector3.one);
        Gizmos.DrawCube(Vector3.zero, SafeRoomSize);
        Gizmos.color = c;
        Gizmos.DrawWireCube(Vector3.zero, SafeRoomSize);
        Gizmos.matrix = old;

        // Zona de entrega.
        Gizmos.color = c;
        Gizmos.DrawWireSphere(DeliveryWorldPoint, DeliveryRadius);

        // Puntos de aparición.
        if (SpawnPoints == null) return;
        foreach (Transform t in SpawnPoints)
        {
            if (t == null) continue;
            Gizmos.DrawWireCube(t.position + Vector3.up, new Vector3(0.6f, 2f, 0.6f));
        }
    }
}
