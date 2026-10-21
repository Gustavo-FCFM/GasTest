using System.Collections.Generic;
using UnityEngine;

// ============================================================
// UI_GroundTargetIndicator
//
// Marcador en el suelo para las habilidades que se apuntan antes de lanzarse — las que
// implementan IGroundTargetAbility. PlayerController lo muestra mientras se mantiene el
// botón, lo mueve siguiendo la mira, y lo esconde al soltar.
//
//  · ZONA (la mayoría: Marcado para morir, el Salto heroico, las áreas "en la retícula"):
//    un círculo relleno con borde, del tamaño real del área (TargetRadius).
//  · FRANJA (ILineTargetAbility, el Corte final del Samurái): una caja rellena con borde
//    que sale del jugador hacia la mira, recortada en la primera pared, y un contorno en
//    los enemigos que quedan adentro (los que va a alcanzar).
//  · MURO (IWallTargetAbility, el Muro de fuego del Mago): una fila de casillas atravesada a
//    la mira, cada una apoyada en el piso que tiene debajo. La rueda del mouse la gira (eso
//    lo lleva PlayerController; acá llega el giro ya hecho).
//
// VIVE EN EL PREFAB DE LA CÁMARA (Player Camera ▸ GroundTargetIndicator; desde el 21 de
// octubre de 2026 — antes se armaba entero por código): sus piezas están armadas ahí, y el
// COLOR, el GROSOR y el MATERIAL se cambian en cada una (los rellenos en su MeshRenderer, los
// bordes en su LineRenderer). La casilla del muro es un MOLDE apagado que se clona una vez por
// casilla. Al jugar arrancan apagadas.
//
// Solo es visual y solo corre en el DUEÑO (la cámara solo existe para él), así que no lleva
// nada de red. Cuelga de la cámara solo para vivir en su prefab: al arrancar se SUELTA de ella
// (si no, cada vez que la cámara gira se llevaría el marcador un frame, y a 15 m se nota) y se
// borra solo cuando la cámara se va. Las piezas se ubican en coordenadas de mundo.
// ============================================================
public class UI_GroundTargetIndicator : MonoBehaviour
{
    // El del jugador local (vive en su cámara). PlayerController lo busca en su cámara.
    public static UI_GroundTargetIndicator Instance;

    [Header("Piezas")]
    [Tooltip("El relleno de la zona: un disco de radio 1 acostado en el piso (se escala al radio).")]
    [Label("Zona: relleno")]
    public Transform ZoneFill;

    [Tooltip("El borde de la zona: una línea cerrada (sus puntos son los del círculo).")]
    [Label("Zona: borde")]
    public LineRenderer ZoneBorder;

    [Tooltip("El relleno de la franja: un rectángulo de 1×1 acostado (el largo va en Z).")]
    [Label("Franja: relleno")]
    public Transform LineFill;

    [Tooltip("El borde de la franja: una línea cerrada de 4 puntos.")]
    [Label("Franja: borde")]
    public LineRenderer LineBorder;

    [Tooltip("MOLDE apagado de una casilla del muro: un relleno (rectángulo de 1×1 acostado) y su " +
             "borde (una línea cerrada de 4 puntos) como hijos. Se clona una vez por casilla.")]
    [Label("Muro: casilla (molde)")]
    public Transform WallSquareTemplate;

    [Tooltip("El contorno de los enemigos que la franja alcanzaría. Vacío = uno con el shader del " +
             "contorno (MercOutline) y el ancho de abajo.")]
    [Label("Material del contorno")]
    public Material TargetOutlineMaterial;

    [Header("Piso")]
    [Tooltip("Capas del suelo/entorno sobre las que se apoya el marcador.")]
    [Label("Capas del suelo")]
    public LayerMask GroundLayer = ~((1 << 7) | (1 << 2));

    [Tooltip("Lo que CORTA la franja (las paredes, también las invisibles). Los personajes no la cortan.")]
    [Label("Capas que cortan la franja")]
    public LayerMask LineBlockLayers = ~(1 << 7);

    [Tooltip("Hueco entre una casilla del muro y la siguiente, para que se vean separadas.")]
    [Label("Muro: hueco entre casillas (m)")]
    public float WallGap = 0.12f;

    private const float Lift = 0.04f;   // un poco arriba del piso, para que no pelee con él (z-fighting)

    // Las casillas del muro (clones del molde), cada una con su relleno y su borde.
    private struct WallSquare { public Transform Root, Fill; public LineRenderer Border; }
    private readonly List<WallSquare> _wallSquares = new List<WallSquare>();
    private int   _wallSegments = 1;
    private float _wallWidth = 1f, _wallDepth = 1f;
    private float _zoneRadius = 1f;

    private Material  _fallbackOutline;
    private Transform _camera;   // de quien se soltó al arrancar: si se va, el marcador también
    private bool      _hadCamera;

    // Los enemigos marcados por la franja (con los renderers de su contorno).
    private readonly Dictionary<AbilitySystemComponent, List<SkinnedMeshRenderer>> _marked =
        new Dictionary<AbilitySystemComponent, List<SkinnedMeshRenderer>>();
    private readonly HashSet<AbilitySystemComponent> _inside = new HashSet<AbilitySystemComponent>();
    private readonly List<AbilitySystemComponent>    _gone   = new List<AbilitySystemComponent>();
    private readonly Collider[] _overlap = new Collider[32];
    private readonly RaycastHit[] _castHits = new RaycastHit[16];

    private void Awake()
    {
        Instance = this;
        PrepareBorder(ZoneBorder);
        PrepareBorder(LineBorder);
        if (WallSquareTemplate != null) WallSquareTemplate.gameObject.SetActive(false);
        Hide();

        _camera    = transform.parent;
        _hadCamera = _camera != null;
        transform.SetParent(null, false);
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        transform.localScale = Vector3.one;
    }

    // La cámara se fue (desconectarse, cambiar de escena): el marcador también.
    private void LateUpdate()
    {
        if (_hadCamera && _camera == null) Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        ClearMarked();
        if (_fallbackOutline != null) Destroy(_fallbackOutline);
    }

    // =========================================================
    // ZONA
    // =========================================================

    // Muestra la zona con su tamaño real (radius = radio).
    public void Show(float radius)
    {
        _zoneRadius = Mathf.Max(0.1f, radius);
        SetActive(ZoneFill, true);
        SetActive(ZoneBorder, true);
    }

    // Mueve la zona al punto apuntado, apoyada en el suelo. El punto de mira puede quedar
    // en el aire (apuntando al cielo o a una pared): se baja con un rayo hasta el piso.
    public void UpdatePosition(Vector3 aimPoint)
    {
        Vector3 center = OnGround(aimPoint, 10f, 30f);

        if (ZoneFill != null && ZoneFill.gameObject.activeSelf)
        {
            ZoneFill.SetPositionAndRotation(center, Quaternion.identity);
            ZoneFill.localScale = new Vector3(_zoneRadius, 1f, _zoneRadius);
        }

        if (ZoneBorder == null || !ZoneBorder.gameObject.activeSelf) return;
        FlatBorder(ZoneBorder);
        int points = ZoneBorder.positionCount;
        for (int i = 0; i < points; i++)
        {
            float a = i * Mathf.PI * 2f / points;
            ZoneBorder.SetPosition(i, center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * _zoneRadius);
        }
    }

    public void Hide()
    {
        SetActive(ZoneFill, false);
        SetActive(ZoneBorder, false);
        SetActive(LineFill, false);
        SetActive(LineBorder, false);
        foreach (WallSquare square in _wallSquares) SetActive(square.Root, false);
        ClearMarked();
    }

    // =========================================================
    // FRANJA (ILineTargetAbility)
    // =========================================================

    public void ShowLine()
    {
        SetActive(LineFill, true);
        SetActive(LineBorder, true);
    }

    // origin: los pies del jugador. direction: hacia la mira (se aplana). viewer: el ASC del
    // jugador local, para saber quién es enemigo. targets: las capas que la habilidad golpea.
    public void UpdateLine(Vector3 origin, Vector3 direction, float length, float width,
                           LayerMask targets, AbilitySystemComponent viewer)
    {
        if (LineFill == null || LineBorder == null) return;

        Vector3 dir = new Vector3(direction.x, 0f, direction.z);
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();

        length = Mathf.Min(length, WallDistance(origin + Vector3.up, dir, width * 0.25f, length));

        Vector3 side   = Vector3.Cross(Vector3.up, dir) * (width * 0.5f);
        Vector3 end    = origin + dir * length;
        Vector3 middle = OnGround(origin + dir * (length * 0.5f), 2f, 6f);

        // La caja rellena: la malla mide 1×1 en el plano del piso; se estira al ancho y al largo.
        LineFill.SetPositionAndRotation(middle, Quaternion.LookRotation(dir));
        LineFill.localScale = new Vector3(width, 1f, length);

        FlatBorder(LineBorder);
        LineBorder.positionCount = 4;
        LineBorder.SetPosition(0, OnGround(origin - side, 2f, 6f));
        LineBorder.SetPosition(1, OnGround(end - side, 2f, 6f));
        LineBorder.SetPosition(2, OnGround(end + side, 2f, 6f));
        LineBorder.SetPosition(3, OnGround(origin + side, 2f, 6f));

        MarkInside(origin + dir * (length * 0.5f) + Vector3.up,
                   new Vector3(width * 0.5f, 1.2f, length * 0.5f), Quaternion.LookRotation(dir), targets, viewer);
    }

    // =========================================================
    // MURO (IWallTargetAbility)
    // =========================================================

    // Muestra el muro: 'segments' casillas en fila que suman 'width' de lado a lado, de 'depth'
    // de largo.
    public void ShowWall(float width, float depth, int segments)
    {
        if (WallSquareTemplate == null) return;

        _wallWidth    = Mathf.Max(0.1f, width);
        _wallDepth    = Mathf.Max(0.1f, depth);
        _wallSegments = Mathf.Max(1, segments);

        while (_wallSquares.Count < _wallSegments)
        {
            Transform root = Instantiate(WallSquareTemplate, WallSquareTemplate.parent);
            root.name = $"WallSquare_{_wallSquares.Count}";
            LineRenderer border = root.GetComponentInChildren<LineRenderer>(true);
            MeshRenderer fill   = root.GetComponentInChildren<MeshRenderer>(true);
            PrepareBorder(border);
            _wallSquares.Add(new WallSquare { Root = root, Fill = fill != null ? fill.transform : null, Border = border });
        }

        for (int i = 0; i < _wallSquares.Count; i++) SetActive(_wallSquares[i].Root, i < _wallSegments);
    }

    // El centro del muro (donde apunta) y su giro: X de lado a lado, Z hacia adelante. Cada
    // casilla se apoya en el piso que tiene debajo.
    public void UpdateWall(Vector3 aimPoint, Quaternion rotation)
    {
        Vector3 center  = OnGround(aimPoint, 10f, 30f);
        Vector3 right   = rotation * Vector3.right;
        Vector3 forward = rotation * Vector3.forward;
        float   cell    = _wallWidth / _wallSegments;
        float   halfW   = Mathf.Max(0.05f, cell - WallGap) * 0.5f;
        float   halfD   = _wallDepth * 0.5f;

        for (int i = 0; i < _wallSegments && i < _wallSquares.Count; i++)
        {
            WallSquare square = _wallSquares[i];
            float   offset = (i - (_wallSegments - 1) * 0.5f) * cell;
            Vector3 c      = OnGround(center + right * offset, 2f, 6f);

            if (square.Root != null) square.Root.SetPositionAndRotation(c, rotation);
            if (square.Fill != null)
            {
                square.Fill.SetPositionAndRotation(c, rotation);
                square.Fill.localScale = new Vector3(halfW * 2f, 1f, halfD * 2f);
            }
            if (square.Border != null)
            {
                FlatBorder(square.Border);
                square.Border.positionCount = 4;
                square.Border.SetPosition(0, c - right * halfW - forward * halfD);
                square.Border.SetPosition(1, c - right * halfW + forward * halfD);
                square.Border.SetPosition(2, c + right * halfW + forward * halfD);
                square.Border.SetPosition(3, c + right * halfW - forward * halfD);
            }
        }
    }

    // =========================================================
    // AYUDAS
    // =========================================================

    // Hasta dónde llega la franja antes de la primera pared. No cuentan los personajes (ni el
    // propio) ni los triggers: la embestida los atraviesa.
    private float WallDistance(Vector3 from, Vector3 dir, float radius, float max)
    {
        int count = Physics.SphereCastNonAlloc(from, radius, dir, _castHits, max, LineBlockLayers,
                                               QueryTriggerInteraction.Ignore);
        float best = max;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = _castHits[i];
            if (hit.distance <= 0f || hit.distance >= best) continue;
            if (hit.collider.GetComponentInParent<AbilitySystemComponent>() != null) continue;
            best = hit.distance;
        }
        return Mathf.Max(0.5f, best);
    }

    // Un punto apoyado en el piso, un poco arriba para que no pelee con él (z-fighting).
    private Vector3 OnGround(Vector3 p, float above, float depth)
    {
        if (Physics.Raycast(p + Vector3.up * above, Vector3.down, out RaycastHit hit, above + depth, GroundLayer,
                            QueryTriggerInteraction.Ignore))
            p.y = hit.point.y;
        return p + Vector3.up * Lift;
    }

    // Marca con un contorno a los enemigos dentro de la caja y se lo saca a los que salieron.
    // A un enemigo invisible no se lo marca: lo delataría.
    private void MarkInside(Vector3 center, Vector3 halfExtents, Quaternion rotation, LayerMask targets,
                            AbilitySystemComponent viewer)
    {
        _inside.Clear();
        int count = Physics.OverlapBoxNonAlloc(center, halfExtents, _overlap, rotation, targets);
        for (int i = 0; i < count; i++)
        {
            AbilitySystemComponent asc = _overlap[i].GetComponentInParent<AbilitySystemComponent>();
            if (asc == null || viewer == null || ReferenceEquals(asc, viewer)) continue;
            if (!viewer.IsEnemyOf(asc) || asc.HasTag(EGameplayTag.State_Dead) || asc.IsHiddenFromEnemies) continue;
            _inside.Add(asc);
        }

        foreach (AbilitySystemComponent asc in _inside)
            if (!_marked.ContainsKey(asc)) _marked[asc] = CharacterOutline.Create(asc, OutlineMaterial(), "TargetOutline");

        _gone.Clear();
        foreach (AbilitySystemComponent asc in _marked.Keys)
            if (asc == null || !_inside.Contains(asc)) _gone.Add(asc);
        foreach (AbilitySystemComponent asc in _gone)
        {
            CharacterOutline.Destroy(_marked[asc]);
            _marked.Remove(asc);
        }
    }

    private void ClearMarked()
    {
        foreach (List<SkinnedMeshRenderer> renderers in _marked.Values) CharacterOutline.Destroy(renderers);
        _marked.Clear();
    }

    private Material OutlineMaterial()
    {
        if (TargetOutlineMaterial != null) return TargetOutlineMaterial;
        if (_fallbackOutline == null) _fallbackOutline = CharacterOutline.CreateMaterial(0.03f);
        return _fallbackOutline;
    }

    // Los bordes van en coordenadas de mundo y cerrados.
    private static void PrepareBorder(LineRenderer border)
    {
        if (border == null) return;
        border.useWorldSpace = true;
        border.loop          = true;
    }

    // El borde se acuesta según el giro de su propio transform (alineado en TransformZ, mirando
    // hacia abajo). Cuelga de la cámara, así que se lo vuelve a acostar en el mundo cada vez.
    private static void FlatBorder(LineRenderer border)
    {
        if (border != null) border.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private static void SetActive(Component piece, bool on)
    {
        if (piece != null && piece.gameObject.activeSelf != on) piece.gameObject.SetActive(on);
    }
}
