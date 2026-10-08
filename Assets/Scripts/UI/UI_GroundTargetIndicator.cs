using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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
//
// SE CREA SOLO (8 de octubre de 2026): no estaba puesto en ninguna escena ni prefab, así
// que hasta entonces ninguna habilidad de zona mostraba nada. Ahora Get() lo crea la
// primera vez que hace falta, y todo se dibuja por código (si se le asigna un
// MarkerPrefab, la zona usa ese en vez del círculo).
//
// Solo es visual y solo corre en el DUEÑO (nadie más necesita ver a dónde está por
// apuntar), así que no lleva nada de red.
// ============================================================
public class UI_GroundTargetIndicator : MonoBehaviour
{
    // Instancia única de la escena. Para MOSTRAR se usa Get(), que la crea si no está.
    public static UI_GroundTargetIndicator Instance;

    public static UI_GroundTargetIndicator Get()
    {
        if (Instance == null) new GameObject("GroundTargetIndicator").AddComponent<UI_GroundTargetIndicator>();
        return Instance;
    }

    [Header("Marcador")]
    [Tooltip("OPCIONAL: un prefab propio para la ZONA (un quad de 1×1 que se escala al diámetro). " +
             "Vacío = un círculo relleno con borde, dibujado por código.")]
    public GameObject MarkerPrefab;

    [Tooltip("Capas del suelo/entorno sobre las que se apoya el marcador.")]
    public LayerMask GroundLayer = ~((1 << 7) | (1 << 2));

    [Tooltip("Color del relleno (zona y franja). Transparente: que se vea el piso.")]
    public Color FillColor = new Color(1f, 0.8f, 0.25f, 0.22f);
    [Tooltip("Color del borde (zona y franja).")]
    public Color LineColor = new Color(1f, 0.85f, 0.3f, 0.95f);
    [Tooltip("Grosor del borde, en metros.")]
    public float LineThickness = 0.1f;

    [Header("Franja (Corte final)")]
    [Tooltip("Color del contorno de los enemigos que la franja alcanzaría.")]
    public Color TargetOutlineColor = new Color(1f, 0.35f, 0.25f, 1f);
    [Tooltip("Grosor del contorno, en metros.")]
    public float TargetOutlineWidth = 0.03f;
    [Tooltip("Lo que CORTA la franja (las paredes, también las invisibles). Los personajes no la cortan.")]
    public LayerMask LineBlockLayers = ~(1 << 7);

    private const int CircleSegments = 48;

    // Lo dibujado: el relleno (una malla) y el borde (una línea) de la zona y de la franja.
    // Todo vive SUELTO en la escena, no de hijo de nada: el borde se acuesta según la
    // rotación de su propio transform (ver CreateBorder).
    private GameObject   _marker;          // el MarkerPrefab, si hay
    private Transform    _zoneFill;
    private LineRenderer _zoneBorder;
    private Transform    _lineFill;
    private LineRenderer _lineBorder;
    private Material     _fillMaterial, _borderMaterial, _outlineMaterial;
    private readonly List<GameObject> _created = new List<GameObject>();

    // Los enemigos marcados por la franja (con los renderers de su contorno).
    private readonly Dictionary<AbilitySystemComponent, List<SkinnedMeshRenderer>> _marked =
        new Dictionary<AbilitySystemComponent, List<SkinnedMeshRenderer>>();
    private readonly HashSet<AbilitySystemComponent> _inside = new HashSet<AbilitySystemComponent>();
    private readonly List<AbilitySystemComponent>    _gone   = new List<AbilitySystemComponent>();
    private readonly Collider[] _overlap = new Collider[32];
    private readonly RaycastHit[] _castHits = new RaycastHit[16];

    private float _zoneRadius = 1f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        ClearMarked();
        foreach (GameObject go in _created) if (go != null) Destroy(go);
        if (_marker != null)          Destroy(_marker);
        if (_fillMaterial != null)    Destroy(_fillMaterial);
        if (_borderMaterial != null)  Destroy(_borderMaterial);
        if (_outlineMaterial != null) Destroy(_outlineMaterial);
    }

    // =========================================================
    // ZONA
    // =========================================================

    // Muestra la zona con su tamaño real (radius = radio).
    public void Show(float radius)
    {
        _zoneRadius = Mathf.Max(0.1f, radius);

        if (MarkerPrefab != null)
        {
            if (_marker == null) _marker = Instantiate(MarkerPrefab);
            _marker.transform.localScale = new Vector3(_zoneRadius * 2f, 1f, _zoneRadius * 2f);
            _marker.SetActive(true);
            return;
        }

        if (_zoneFill == null)
        {
            _zoneFill   = CreateFill("GroundTarget_ZoneFill", DiscMesh());
            _zoneBorder = CreateBorder("GroundTarget_ZoneBorder", CircleSegments);
        }
        _zoneFill.localScale = new Vector3(_zoneRadius, 1f, _zoneRadius);
        _zoneFill.gameObject.SetActive(true);
        _zoneBorder.gameObject.SetActive(true);
    }

    // Mueve la zona al punto apuntado, apoyada en el suelo. El punto de mira puede quedar
    // en el aire (apuntando al cielo o a una pared): se baja con un rayo hasta el piso.
    public void UpdatePosition(Vector3 aimPoint)
    {
        Vector3 center = OnGround(aimPoint, 10f, 30f);

        if (_marker != null && _marker.activeSelf) _marker.transform.position = center;
        if (_zoneFill == null || !_zoneFill.gameObject.activeSelf) return;

        _zoneFill.position = center;
        for (int i = 0; i < CircleSegments; i++)
        {
            float a = i * Mathf.PI * 2f / CircleSegments;
            _zoneBorder.SetPosition(i, center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * _zoneRadius);
        }
    }

    public void Hide()
    {
        if (_marker != null)     _marker.SetActive(false);
        if (_zoneFill != null)   _zoneFill.gameObject.SetActive(false);
        if (_zoneBorder != null) _zoneBorder.gameObject.SetActive(false);
        if (_lineFill != null)   _lineFill.gameObject.SetActive(false);
        if (_lineBorder != null) _lineBorder.gameObject.SetActive(false);
        ClearMarked();
    }

    // =========================================================
    // FRANJA (ILineTargetAbility)
    // =========================================================

    public void ShowLine()
    {
        if (_lineFill == null)
        {
            _lineFill   = CreateFill("GroundTarget_LineFill", RectMesh());
            _lineBorder = CreateBorder("GroundTarget_LineBorder", 4);
        }
        _lineFill.gameObject.SetActive(true);
        _lineBorder.gameObject.SetActive(true);
    }

    // origin: los pies del jugador. direction: hacia la mira (se aplana). viewer: el ASC del
    // jugador local, para saber quién es enemigo. targets: las capas que la habilidad golpea.
    public void UpdateLine(Vector3 origin, Vector3 direction, float length, float width,
                           LayerMask targets, AbilitySystemComponent viewer)
    {
        if (_lineFill == null) return;

        Vector3 dir = new Vector3(direction.x, 0f, direction.z);
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();

        length = Mathf.Min(length, WallDistance(origin + Vector3.up, dir, width * 0.25f, length));

        Vector3 side   = Vector3.Cross(Vector3.up, dir) * (width * 0.5f);
        Vector3 end    = origin + dir * length;
        Vector3 middle = OnGround(origin + dir * (length * 0.5f), 2f, 6f);

        // La caja rellena: la malla mide 1×1 en el plano del piso; se estira al ancho y al largo.
        _lineFill.SetPositionAndRotation(middle, Quaternion.LookRotation(dir));
        _lineFill.localScale = new Vector3(width, 1f, length);

        _lineBorder.SetPosition(0, OnGround(origin - side, 2f, 6f));
        _lineBorder.SetPosition(1, OnGround(end - side, 2f, 6f));
        _lineBorder.SetPosition(2, OnGround(end + side, 2f, 6f));
        _lineBorder.SetPosition(3, OnGround(origin + side, 2f, 6f));

        MarkInside(origin + dir * (length * 0.5f) + Vector3.up,
                   new Vector3(width * 0.5f, 1.2f, length * 0.5f), Quaternion.LookRotation(dir), targets, viewer);
    }

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
        return p + Vector3.up * 0.04f;
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

    // =========================================================
    // LO DIBUJADO
    // =========================================================

    private Material OutlineMaterial()
    {
        if (_outlineMaterial != null) return _outlineMaterial;
        _outlineMaterial = CharacterOutline.CreateMaterial(TargetOutlineWidth);
        if (_outlineMaterial != null) _outlineMaterial.SetColor("_Color", TargetOutlineColor);
        return _outlineMaterial;
    }

    // Sin luces ni sombras, transparente, se ve de los dos lados (el de los sprites).
    private Material UnlitMaterial(Color color)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        var material = new Material(shader) { color = color };
        return material;
    }

    // El relleno: una malla acostada en el piso.
    private Transform CreateFill(string name, Mesh mesh)
    {
        if (_fillMaterial == null) _fillMaterial = UnlitMaterial(FillColor);

        var go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial    = _fillMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows    = false;
        _created.Add(go);
        return go.transform;
    }

    // El borde: una línea cerrada de 'points' puntos, acostada en el piso (con TransformZ
    // mirando hacia abajo, la línea queda plana).
    private LineRenderer CreateBorder(string name, int points)
    {
        if (_borderMaterial == null) _borderMaterial = UnlitMaterial(Color.white);

        var go = new GameObject(name);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace     = true;
        line.loop              = true;
        line.positionCount     = points;
        line.widthMultiplier   = LineThickness;
        line.numCornerVertices = 2;
        line.alignment         = LineAlignment.TransformZ;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows    = false;
        line.sharedMaterial    = _borderMaterial;
        line.startColor        = LineColor;
        line.endColor          = LineColor;
        _created.Add(go);
        return line;
    }

    // Un rectángulo de 1×1 en el plano del piso, centrado (el largo va en Z).
    private static Mesh RectMesh()
    {
        var mesh = new Mesh { name = "GroundTargetRect" };
        mesh.vertices  = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f),
                                 new Vector3(0.5f, 0f, 0.5f),   new Vector3(0.5f, 0f, -0.5f) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        return mesh;
    }

    // Un disco de radio 1 en el plano del piso (abanico de triángulos).
    private static Mesh DiscMesh()
    {
        var vertices  = new Vector3[CircleSegments + 1];
        var triangles = new int[CircleSegments * 3];
        vertices[0] = Vector3.zero;
        for (int i = 0; i < CircleSegments; i++)
        {
            float a = i * Mathf.PI * 2f / CircleSegments;
            vertices[i + 1] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            triangles[i * 3]     = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i == CircleSegments - 1 ? 1 : i + 2;
        }

        var mesh = new Mesh { name = "GroundTargetDisc", vertices = vertices, triangles = triangles };
        mesh.RecalculateBounds();
        return mesh;
    }
}
