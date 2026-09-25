using UnityEngine;
using UnityEngine.UI;

// ============================================================
// UI_CombatFeedback
//
// Las tres respuestas que el juego le debe al jugador cuando pasa algo en combate:
//
//   · PEGUÉ           → una X roja en la retícula. Cuanto MENOS vida le queda al que
//                       recibió el golpe, más opaca aparece: de un vistazo se sabe si
//                       le estás haciendo cosquillas o está por caer.
//   · MATÉ A ALGUIEN  → una calavera en el centro, a plena opacidad, que se apaga
//                       rápido. Solo por matar a un PERSONAJE (jugador o bot), no por
//                       un monstruo: una baja es un evento del partido.
//   · ME ESTÁN PEGANDO→ un arco rojo en un círculo grande alrededor de la mira, del
//                       lado por donde vino el golpe. Arriba = de frente, abajo = por
//                       la espalda, y los costados en su ángulo real.
//
// POR QUÉ IMPORTA EL DE RECIBIR: sin él, morir por la espalda se siente injusto — no
// llegaste a saber que te estaban pegando. Con el arco, te das vuelta.
//
// NO HAY NADA QUE CABLEAR. Se dibuja solo con MercUIFactory, igual que el resto del HUD
// del modo, y quien lo necesita lo pide con UI_CombatFeedback.Get(). Si querés cambiar
// colores o tamaños sin tocar código, poné el componente en un GameObject de la escena y
// el que se use va a ser ese.
//
// QUIÉN LO LLAMA: el servidor, que es el único que sabe cuánto daño entró y a quién. Se
// lo manda SOLO al dueño que corresponde por TargetRpc — ver
// NetworkAbilitySystemComponent.ServerReportDamage.
// ============================================================
public class UI_CombatFeedback : MonoBehaviour
{
    // =========================================================
    // PERILLAS
    // =========================================================

    [Header("X de golpe")]
    [Tooltip("Largo de cada PUNTA de la X. Es la perilla para agrandarla o achicarla.")]
    public float HitMarkLength = 26f;

    [Tooltip("Hueco en el centro: a qué distancia del medio arrancan las puntas. Sin " +
             "hueco la X tapa la retícula justo cuando estás apuntando.")]
    public float HitMarkGap = 9f;

    [Tooltip("Grosor de cada punta.")]
    public float HitMarkThickness = 5f;

    [Tooltip("Cuánto tarda en apagarse la X.")]
    public float HitFadeSeconds = 0.45f;

    [Tooltip("Opacidad MÍNIMA de la X. Sin esto, pegarle a alguien con la vida llena no " +
             "se vería casi nada y parecería que el golpe no entró.")]
    [Range(0f, 1f)]
    public float HitMinAlpha = 0.25f;

    public Color HitColor = new Color(0.95f, 0.15f, 0.15f, 1f);

    [Tooltip("Color de la X cuando el golpe fue CRÍTICO (el mismo amarillo del número).")]
    public Color HitCriticalColor = new Color(1f, 0.85f, 0.1f, 1f);

    [Tooltip("Opacidad mínima de la X de crítico: un crítico siempre se tiene que notar, " +
             "aunque el golpeado tenga la vida llena.")]
    [Range(0f, 1f)]
    public float HitCriticalMinAlpha = 0.85f;

    [Header("Calavera de baja")]
    [Tooltip("Tamaño de la calavera.")]
    public float KillMarkSize = 46f;

    [Tooltip("Cuánto tarda en apagarse. Corto a propósito: es un premio, no un cartel.")]
    public float KillFadeSeconds = 0.9f;

    [Tooltip("Si le asignás un sprite de calavera, se usa ese en vez de la que se dibuja " +
             "por código (una cabeza, dos ojos y la mandíbula).")]
    public Sprite KillIcon;

    public Color KillColor = new Color(1f, 0.2f, 0.2f, 1f);

    [Header("Arco de daño recibido")]
    [Tooltip("Radio del círculo, en píxeles de la maqueta. Grande a propósito: tiene que " +
             "verse por el rabillo del ojo sin tapar la pelea.")]
    public float DamageRingRadius = 190f;

    [Tooltip("En cuántos pedacitos se divide el círculo. Más = arco más suave y más " +
             "objetos en pantalla.")]
    public int DamageRingSegments = 24;

    [Tooltip("Largo y grosor de cada pedacito del arco.")]
    public Vector2 DamageSegmentSize = new Vector2(34f, 9f);

    [Tooltip("Ancho del arco que se enciende, en grados a cada lado de por donde vino el " +
             "golpe. Los del borde se encienden menos.")]
    public float DamageArcHalfWidth = 38f;

    [Tooltip("Cuánto tarda en apagarse el arco.")]
    public float DamageFadeSeconds = 1.1f;

    public Color DamageColor = new Color(0.9f, 0.1f, 0.1f, 1f);

    // =========================================================
    // ACCESO
    // =========================================================

    private static UI_CombatFeedback _instance;

    public static UI_CombatFeedback Get()
    {
        if (_instance != null) return _instance;

        _instance = FindFirstObjectByType<UI_CombatFeedback>(FindObjectsInactive.Include);
        if (_instance != null) return _instance;

        GameObject go = new GameObject("UI_CombatFeedback");
        _instance = go.AddComponent<UI_CombatFeedback>();
        return _instance;
    }

    // --- construido en runtime ---
    private Canvas        _canvas;
    private RectTransform _root;

    private CanvasGroup     _hitGroup;
    private RectTransform[] _hitArms;
    private CanvasGroup     _killGroup;
    private Image[]     _ringSegments;

    private float _hitAlpha, _hitFrom;
    private float _killAlpha;
    private float[] _segmentAlpha;

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;

        Build();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // =========================================================
    // LO QUE SE LE PIDE DESDE AFUERA
    // =========================================================

    // Pegaste. 'victimHealthFraction' es la vida que le QUEDA al golpeado, de 0 a 1:
    // cuanto más baja, más opaca la X.
    // critical: el golpe fue crítico → la X sale amarilla y bien visible.
    public void ShowHit(float victimHealthFraction, bool critical = false)
    {
        float health = Mathf.Clamp01(victimHealthFraction);

        // La opacidad es lo que le falta de vida, con un piso para que un golpe a
        // alguien entero igual se note.
        _hitFrom  = Mathf.Max(critical ? HitCriticalMinAlpha : HitMinAlpha, 1f - health);
        _hitAlpha = _hitFrom;

        if (_hitArms == null) return;
        Color color = critical ? HitCriticalColor : HitColor;
        foreach (RectTransform arm in _hitArms)
        {
            Image image = arm != null ? arm.GetComponent<Image>() : null;
            if (image != null) image.color = color;
        }
    }

    // Mataste a un personaje.
    public void ShowKill() => _killAlpha = 1f;

    // Te pegaron desde 'worldPosition'. El arco se enciende en ese ángulo, medido
    // respecto de hacia dónde estás MIRANDO (no hacia dónde apunta el cuerpo): lo que
    // el jugador tiene que corregir es la cámara.
    public void ShowDamageFrom(Vector3 worldPosition, Transform victim)
    {
        if (_ringSegments == null || victim == null) return;

        Vector3 toAttacker = worldPosition - victim.position;
        toAttacker.y = 0f;
        if (toAttacker.sqrMagnitude < 0.0001f)
        {
            // Sin dirección utilizable (te pegaste a vos mismo, o el atacante está justo
            // encima): se enciende el círculo entero. Algo pasó, y eso ya es información.
            for (int i = 0; i < _segmentAlpha.Length; i++) _segmentAlpha[i] = 1f;
            return;
        }

        Camera cam = Camera.main;
        Vector3 forward = cam != null ? cam.transform.forward : victim.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = victim.forward;

        // Ángulo con signo entre "hacia dónde miro" y "de dónde vino": 0 = de frente,
        // 180 = por la espalda, positivo = por la derecha.
        float angle = Vector3.SignedAngle(forward.normalized, toAttacker.normalized, Vector3.up);

        for (int i = 0; i < _ringSegments.Length; i++)
        {
            float segmentAngle = SegmentAngle(i);
            float delta        = Mathf.Abs(Mathf.DeltaAngle(segmentAngle, angle));
            if (delta > DamageArcHalfWidth) continue;

            // Los del borde del arco encienden menos: así el arco se ve como una mancha
            // y no como un bloque recortado.
            float strength = 1f - delta / DamageArcHalfWidth;
            _segmentAlpha[i] = Mathf.Max(_segmentAlpha[i], strength);
        }
    }

    // =========================================================
    // APAGARSE
    // =========================================================

    private void Update()
    {
        if (_hitGroup != null)
        {
            if (_hitAlpha > 0f && HitFadeSeconds > 0f)
                _hitAlpha = Mathf.Max(0f, _hitAlpha - _hitFrom * Time.deltaTime / HitFadeSeconds);
            _hitGroup.alpha = _hitAlpha;
        }

        if (_killGroup != null)
        {
            if (_killAlpha > 0f && KillFadeSeconds > 0f)
                _killAlpha = Mathf.Max(0f, _killAlpha - Time.deltaTime / KillFadeSeconds);
            _killGroup.alpha = _killAlpha;
        }

        if (_ringSegments == null) return;

        float step = DamageFadeSeconds > 0f ? Time.deltaTime / DamageFadeSeconds : 1f;
        for (int i = 0; i < _ringSegments.Length; i++)
        {
            if (_segmentAlpha[i] > 0f) _segmentAlpha[i] = Mathf.Max(0f, _segmentAlpha[i] - step);

            Color c = DamageColor;
            c.a = _segmentAlpha[i] * DamageColor.a;
            _ringSegments[i].color = c;
        }
    }

    // =========================================================
    // CONSTRUCCIÓN
    // =========================================================

    // A qué ángulo mira el pedacito i del círculo. 0 grados = arriba de la pantalla =
    // de frente.
    private float SegmentAngle(int index) => index * 360f / Mathf.Max(1, DamageRingSegments);

    private void Build()
    {
        _canvas = MercUIFactory.CreateCanvas("CombatFeedbackCanvas", 120);
        _canvas.transform.SetParent(transform, false);

        Vector2 mid = new Vector2(0.5f, 0.5f);
        _root = MercUIFactory.CreateRect(_canvas.transform, "Root", mid, mid, mid,
                                         Vector2.zero, Vector2.zero);

        BuildHitMark(mid);
        BuildKillMark(mid);
        BuildDamageRing(mid);
    }

    // La X: CUATRO puntas con un hueco en el medio, no dos barras cruzadas enteras.
    //
    // POR QUÉ CON HUECO: la X vive encima de la retícula, y una equis maciza tapa
    // justamente el punto al que estás apuntando en el momento en que más lo mirás.
    // Con el hueco se lee igual de bien —el ojo completa la forma— y la mira queda
    // libre. Es como la hacen los juegos de disparos.
    //
    // No hace falta ningún sprite: con el blanco de 1x1 y cuatro rotaciones alcanza.
    private void BuildHitMark(Vector2 mid)
    {
        RectTransform holder = MercUIFactory.CreateRect(_root, "HitMark", mid, mid, mid,
                                                        Vector2.zero, Vector2.zero);
        _hitGroup = holder.gameObject.AddComponent<CanvasGroup>();
        _hitGroup.alpha = 0f;
        _hitGroup.blocksRaycasts = false;
        _hitGroup.interactable   = false;

        _hitArms = new RectTransform[4];
        for (int i = 0; i < 4; i++)
            _hitArms[i] = MakeArm(holder, $"Arm_{i}", 45f + i * 90f);

        ApplyHitMarkLayout();
    }

    private RectTransform MakeArm(Transform parent, string name, float degrees)
    {
        Vector2 mid = new Vector2(0.5f, 0.5f);
        Image arm = MercUIFactory.CreateImage(parent, name, HitColor, Vector2.zero,
                                              Vector2.one, mid, mid, mid);
        return arm.rectTransform;
    }

    // Coloca las cuatro puntas según las medidas de ahora. Aparte para poder retocarlo
    // en vivo desde el Inspector mientras se juega (ver OnValidate).
    private void ApplyHitMarkLayout()
    {
        if (_hitArms == null) return;

        for (int i = 0; i < _hitArms.Length; i++)
        {
            if (_hitArms[i] == null) continue;

            float degrees = 45f + i * 90f;
            float radians = degrees * Mathf.Deg2Rad;

            // Cada punta se corre del centro lo que dure el hueco más media punta: así
            // crece hacia afuera y el hueco del medio no cambia al alargarlas.
            float distance = HitMarkGap + HitMarkLength * 0.5f;

            _hitArms[i].sizeDelta        = new Vector2(HitMarkLength, HitMarkThickness);
            _hitArms[i].anchoredPosition = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * distance;
            _hitArms[i].localRotation    = Quaternion.Euler(0f, 0f, degrees);
        }
    }

#if UNITY_EDITOR
    // Tocar las medidas en el Inspector mientras corre el juego las aplica al momento.
    // Sin esto habría que salir y volver a entrar para ver si el tamaño quedó bien, que
    // es la peor forma de ajustar algo que se juzga de un vistazo.
    private void OnValidate()
    {
        if (Application.isPlaying) ApplyHitMarkLayout();
    }
#endif

    // La calavera. Si hay sprite asignado se usa ese; si no, se dibuja una a la que le
    // alcanza para leerse a este tamaño: cabeza, dos ojos y la mandíbula.
    private void BuildKillMark(Vector2 mid)
    {
        RectTransform holder = MercUIFactory.CreateRect(_root, "KillMark", mid, mid, mid,
                                                        Vector2.zero,
                                                        new Vector2(KillMarkSize, KillMarkSize));
        _killGroup = holder.gameObject.AddComponent<CanvasGroup>();
        _killGroup.alpha = 0f;
        _killGroup.blocksRaycasts = false;
        _killGroup.interactable   = false;

        if (KillIcon != null)
        {
            Image icon = MercUIFactory.CreateImage(holder, "Icon", KillColor, Vector2.zero,
                                                   new Vector2(KillMarkSize, KillMarkSize),
                                                   mid, mid, mid);
            icon.sprite         = KillIcon;
            icon.preserveAspect = true;
            return;
        }

        float s = KillMarkSize;
        // Cráneo.
        MercUIFactory.CreateImage(holder, "Head", KillColor, new Vector2(0f, s * 0.1f),
                                  new Vector2(s * 0.8f, s * 0.7f), mid, mid, mid);
        // Mandíbula, más angosta y debajo.
        MercUIFactory.CreateImage(holder, "Jaw", KillColor, new Vector2(0f, -s * 0.34f),
                                  new Vector2(s * 0.45f, s * 0.22f), mid, mid, mid);

        // Los ojos van del color del FONDO, no negros: sobre cualquier escenario, dos
        // huecos oscuros se leen como ojos aunque el fondo sea claro.
        Color sockets = new Color(0.05f, 0.05f, 0.05f, 1f);
        MercUIFactory.CreateImage(holder, "EyeL", sockets, new Vector2(-s * 0.17f, s * 0.16f),
                                  new Vector2(s * 0.22f, s * 0.26f), mid, mid, mid);
        MercUIFactory.CreateImage(holder, "EyeR", sockets, new Vector2(s * 0.17f, s * 0.16f),
                                  new Vector2(s * 0.22f, s * 0.26f), mid, mid, mid);
        // Nariz.
        MercUIFactory.CreateImage(holder, "Nose", sockets, new Vector2(0f, -s * 0.08f),
                                  new Vector2(s * 0.1f, s * 0.14f), mid, mid, mid);
    }

    // El círculo de recibir daño: pedacitos sueltos alrededor del centro, todos
    // invisibles hasta que alguien te pega.
    //
    // POR QUÉ PEDACITOS Y NO UN ANILLO CON RELLENO RADIAL: un anillo necesita un sprite
    // con agujero, y el proyecto no tiene ninguno; un círculo relleno con Radial360
    // daría una porción de torta que taparía el centro de la pantalla justo cuando más
    // se necesita ver.
    private void BuildDamageRing(Vector2 mid)
    {
        int count = Mathf.Max(4, DamageRingSegments);
        _ringSegments = new Image[count];
        _segmentAlpha = new float[count];

        RectTransform holder = MercUIFactory.CreateRect(_root, "DamageRing", mid, mid, mid,
                                                        Vector2.zero, Vector2.zero);

        for (int i = 0; i < count; i++)
        {
            float degrees = SegmentAngle(i);
            float radians = degrees * Mathf.Deg2Rad;

            // 0 grados arriba y creciendo hacia la derecha, que es como se lee una
            // brújula — y como devuelve el ángulo SignedAngle de ShowDamageFrom.
            Vector2 position = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)) * DamageRingRadius;

            Color transparent = DamageColor; transparent.a = 0f;
            Image segment = MercUIFactory.CreateImage(holder, $"Seg_{i}", transparent, position,
                                                      DamageSegmentSize, mid, mid, mid);
            // Acostado sobre el círculo: el lado largo sigue la curva.
            segment.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -degrees);
            _ringSegments[i] = segment;
        }
    }
}
