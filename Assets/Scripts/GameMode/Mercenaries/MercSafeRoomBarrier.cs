using System.Collections.Generic;
using UnityEngine;

// ============================================================
// MercSafeRoomBarrier
//
// La pared invisible de una sala segura: sólida para los EXTRAÑOS, atravesable para los
// de casa. Es lo que hace que la base de un equipo sea de verdad suya en vez de un lugar
// del que hay que echar gente a cada rato.
//
// CÓMO SE HACE ALGO QUE COLISIONA SOLO CON ALGUNOS: un collider no se puede "filtrar por
// equipo" desde el editor —las capas son globales, y los equipos se deciden en runtime—.
// Lo que sí existe es Physics.IgnoreCollision, que apaga el par collider-a-collider. Así
// que la pared es UNA sola, sólida para todos, y a cada personaje del equipo dueño se le
// apaga el par. El que no es de casa nunca recibe ese permiso y choca.
//
// Se refresca cada tanto porque los personajes van y vienen: alguien se conecta, muere,
// respawnea, o cambia de equipo en la sala.
//
// LOS BOTS NO CHOCAN CON ESTO CAMINANDO: se mueven con un NavMeshAgent, que navega el
// NavMesh y no mira colliders. Para ellos la regla vive en BotController
// (AvoidEnemySafeRooms), que directamente no los deja poner rumbo adentro. Esta pared es
// la que frena a las PERSONAS, y a cualquiera que llegue saltando o dasheando.
// ============================================================
[RequireComponent(typeof(MercTeamBase))]
public class MercSafeRoomBarrier : MonoBehaviour
{
    [Header("Forma")]
    [Tooltip("Cuánto se agranda la pared respecto del área de la sala. Un poco más grande " +
             "para que nadie quede justo en el borde, medio adentro y medio afuera.")]
    public float Padding = 0.5f;

    [Tooltip("Altura de la pared. Alta a propósito: tiene que frenar también al que entra " +
             "saltando, no solo al que camina.")]
    public float Height = 14f;

    [Tooltip("Grosor de cada pared. Gruesa para que un dash rápido no la atraviese en un " +
             "solo frame.")]
    public float Thickness = 1.2f;

    [Header("Capa")]
    [Tooltip("Capa de las paredes. Por defecto 'Ignore Raycast' (2), igual que los límites " +
             "de la arena: siguen frenando —eso lo decide la matriz de colisiones— pero no " +
             "ensucian los raycasts de puntería ni la oclusión de las barras de vida.")]
    public int BarrierLayer = 2;

    [Header("Ritmo")]
    [Tooltip("Cada cuánto se revisa quién puede pasar. Los personajes entran y salen de la " +
             "partida todo el tiempo, así que la lista no puede armarse una sola vez.")]
    public float RefreshInterval = 0.5f;

    [Tooltip("Hasta dónde busca personajes a los que darles permiso. Con el tamaño de la " +
             "sala más un margen alcanza.")]
    public float RefreshRadius = 40f;

    public int TeamID => _base != null ? _base.TeamID : 0;

    private MercTeamBase _base;
    private readonly List<Collider> _walls = new List<Collider>();

    // A quién ya se le dio permiso, para no repetir el IgnoreCollision en cada refresco.
    private readonly HashSet<Collider> _allowed = new HashSet<Collider>();

    private readonly Collider[] _buffer = new Collider[64];
    private float _timer;

    private const string ChildName = "SafeRoomBarrier";

    private void Awake()
    {
        _base = GetComponent<MercTeamBase>();
        Build();
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < RefreshInterval) return;
        _timer = 0f;

        RefreshPermissions();
    }

    // =========================================================
    // CONSTRUCCIÓN
    // =========================================================

    // Cuatro paredes alrededor del área, sin techo ni piso: entrar por arriba lo frena el
    // techo invisible de la arena (MercArenaBounds), y por abajo no hay por dónde.
    [ContextMenu("Armar la pared ahora")]
    public void Build()
    {
        Clear();

        if (_base == null) _base = GetComponent<MercTeamBase>();
        if (_base == null) return;

        Transform anchor = _base.SafeRoomCenter != null ? _base.SafeRoomCenter : transform;

        Transform root = new GameObject(ChildName).transform;
        root.SetParent(anchor, false);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        root.gameObject.layer = BarrierLayer;

        Vector3 size = _base.SafeRoomSize + Vector3.one * (Padding * 2f);
        float hx = size.x * 0.5f;
        float hz = size.z * 0.5f;
        float y  = Height * 0.5f - 1f;   // arranca un poco bajo el piso para no dejar hueco

        AddWall(root, "Wall_X+", new Vector3( hx, y, 0f), new Vector3(Thickness, Height, size.z));
        AddWall(root, "Wall_X-", new Vector3(-hx, y, 0f), new Vector3(Thickness, Height, size.z));
        AddWall(root, "Wall_Z+", new Vector3(0f, y,  hz), new Vector3(size.x, Height, Thickness));
        AddWall(root, "Wall_Z-", new Vector3(0f, y, -hz), new Vector3(size.x, Height, Thickness));
    }

    private void AddWall(Transform parent, string name, Vector3 localPos, Vector3 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.layer = BarrierLayer;

        BoxCollider box = go.AddComponent<BoxCollider>();
        box.size = size;

        _walls.Add(box);
    }

    [ContextMenu("Borrar la pared")]
    public void Clear()
    {
        _walls.Clear();
        _allowed.Clear();

        Transform anchor = _base != null && _base.SafeRoomCenter != null ? _base.SafeRoomCenter : transform;
        Transform existing = anchor.Find(ChildName);
        if (existing == null) return;

        if (Application.isPlaying) Destroy(existing.gameObject);
        else                       DestroyImmediate(existing.gameObject);
    }

    // =========================================================
    // PERMISOS
    // =========================================================

    // Le apaga la colisión con la pared a todo personaje de ESTE equipo que esté cerca.
    // Al que no es de casa no se le toca nada: para él la pared es sólida.
    private void RefreshPermissions()
    {
        if (_walls.Count == 0 || _base == null) return;

        int count = Physics.OverlapSphereNonAlloc(
            _base.SafeRoomWorldCenter, RefreshRadius, _buffer,
            _base.CharacterLayer, QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            Collider col = _buffer[i];
            if (col == null || _allowed.Contains(col)) continue;

            AbilitySystemComponent asc = col.GetComponentInParent<AbilitySystemComponent>();
            if (asc == null || asc.TeamID != _base.TeamID) continue;

            _allowed.Add(col);
            foreach (Collider wall in _walls)
                if (wall != null) Physics.IgnoreCollision(wall, col, true);
        }

        // Los colliders destruidos (jugadores que se fueron) se sacan del registro: si no,
        // el HashSet crece toda la partida y bloquea el permiso de un collider reciclado.
        _allowed.RemoveWhere(c => c == null);
    }

    private void OnDrawGizmosSelected()
    {
        MercTeamBase b = _base != null ? _base : GetComponent<MercTeamBase>();
        if (b == null) return;

        Transform anchor = b.SafeRoomCenter != null ? b.SafeRoomCenter : transform;

        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.35f);
        Gizmos.matrix = Matrix4x4.TRS(anchor.position + Vector3.up * (Height * 0.5f - 1f),
                                      anchor.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero,
                            new Vector3(b.SafeRoomSize.x + Padding * 2f, Height,
                                        b.SafeRoomSize.z + Padding * 2f));
    }
}
