using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// ============================================================
// ThirdPersonOrbitCam
//
// La cámara de tercera persona: orbita alrededor del jugador con el mouse o el stick
// derecho, con la vista por encima del hombro.
//
// Tres cosas que van juntas y por eso viven en el mismo script:
//
//   1. COLISIÓN (el "brazo" de la cámara). Antes la posición se calculaba y se aplicaba
//      sin preguntarle al mundo, así que contra una pared la cámara quedaba del otro
//      lado y se veía a través del escenario. Ahora se tira un SphereCast desde la
//      cabeza del jugador hacia donde querría estar: si hay algo en el medio, se queda
//      pegada a eso.
//
//   2. OCULTAR AL JUGADOR DE CERCA. Consecuencia inevitable de lo anterior: contra una
//      pared la cámara termina encima del personaje y se ve el interior de la cabeza.
//      Pasado cierto punto, el modelo se deja de dibujar PERO SIGUE PROYECTANDO SOMBRA,
//      así no desaparece del todo. Esto solo afecta la pantalla del dueño: cada cliente
//      dibuja su propia copia del personaje, así que los demás lo siguen viendo entero.
//
//   3. MIRAR ARRIBA Y ABAJO DEL TODO. Sin lo anterior no se podía: mirando al piso la
//      cámara atravesaba el suelo, y mirando al cielo se metía dentro del personaje.
//      Con el brazo y el ocultado, los topes se pueden abrir casi por completo — que es
//      lo que hace falta para apuntar un dash hacia abajo o hacia arriba.
// ============================================================
public class ThirdPersonOrbitCam : MonoBehaviour
{
    [Header("Objetivo")]
    public Transform Target; // Tu Player

    [Header("Configuración de Hombro")]
    // (0, 1.5, 0) es la altura de la cabeza/pivote
    public Vector3 PivotOffset = new Vector3(0, 1.5f, 0);
    // (0.8, 0, -2.5) lo mueve a la derecha y atrás (Shoulder View)
    public Vector3 CamOffset = new Vector3(0.8f, 0f, -2.5f);

    [Header("Input - Mouse")]
    // Sensibilidad del mouse. Se aplica al delta puntual del mouse (acción Look,
    // Input System). El *0.1 interno compensa la escala del viejo eje "Mouse X"
    // (que tenía sensibilidad 0.1) para que estos valores se sientan igual que antes.
    public float SensitivityX = 2.0f;
    public float SensitivityY = 2.0f;

    [Tooltip("Cuánto puede mirar hacia ABAJO, en grados. -85 es prácticamente a los pies. " +
             "No conviene llegar a -90: justo ahí la órbita se degenera (la cámara queda " +
             "en el eje del jugador y el giro horizontal deja de tener sentido).")]
    public float MinY = -85f;

    [Tooltip("Cuánto puede mirar hacia ARRIBA, en grados. 85 es prácticamente al cielo.")]
    public float MaxY = 85f;

    [Header("Input - Control (stick derecho)")]
    // El stick manda un valor constante (-1 a 1) mientras se mantiene
    // inclinado, a diferencia del mouse que manda un delta puntual — por
    // eso necesita su propia sensibilidad y se multiplica por
    // Time.deltaTime (grados por segundo), no se suma directo como el mouse.
    public float GamepadSensitivity = 120f;

    [Header("Colisión con el escenario")]
    [Tooltip("Acercar la cámara cuando hay una pared en el medio. Apagalo solo para depurar.")]
    public bool CollideWithGeometry = true;

    [Tooltip("Qué frena a la cámara. Por defecto TODO menos la capa de personajes (7): si los " +
             "personajes la frenaran, pasar al lado de un compañero te tiraría la cámara encima.")]
    public LayerMask CollisionMask = ~(1 << 7);

    [Tooltip("Grosor de la cámara. Se usa un SphereCast y no un Raycast para que no se cuele " +
             "por las esquinas: un rayo pasa por el filo de una pared y la cámara termina " +
             "media adentro.")]
    public float CameraRadius = 0.28f;

    [Tooltip("Cuánto se despega de la pared con la que chocó.")]
    public float CollisionSkin = 0.15f;

    [Tooltip("Lo más cerca del jugador que puede quedar.")]
    public float MinDistance = 0.4f;

    [Tooltip("Qué tan rápido se ACERCA al chocar. Alto a propósito: si se acercara despacio, " +
             "durante esos instantes ya estaría del otro lado de la pared.")]
    public float PullInSpeed = 60f;

    [Tooltip("Qué tan rápido vuelve a su distancia normal al despejarse. Lento, porque un " +
             "regreso brusco marea.")]
    public float PushOutSpeed = 8f;

    [Header("Ocultar al jugador de cerca")]
    [Tooltip("Dejar de dibujar el modelo cuando la cámara se le pega. Solo en la pantalla del dueño.")]
    public bool HideTargetWhenClose = true;

    [Tooltip("A esta distancia o menos, el modelo se deja de dibujar.")]
    public float HideDistance = 1.2f;

    [Tooltip("A esta distancia vuelve a aparecer. Tiene que ser MAYOR que la de ocultar: la " +
             "diferencia entre las dos evita el parpadeo cuando quedás justo en el límite.")]
    public float ShowDistance = 1.5f;

    private float rotX = 0f;
    private float rotY = 0f;

    // Distancia actual del brazo. Empieza en la distancia natural del hombro.
    private float _currentDistance;

    private Renderer[] _targetRenderers;
    private bool _targetHidden;

    void Start()
    {
        /*
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;*/

        // Inicializar rotación con la actual
        Vector3 angles = transform.eulerAngles;
        rotX = angles.x;
        rotY = angles.y;

        _currentDistance = CamOffset.magnitude;
    }

    void LateUpdate()
    {
        if (Target == null) return;

        // 1. Leer la mira (acción "Look" del Input System: mouse o stick derecho).
        // (Si estás en el menú de pausa, deberías bloquear esto)
        PlayerInputProvider input = PlayerInputProvider.Local;
        if (Time.timeScale > 0 && input != null && input.IsReady)
        {
            Vector2 look = input.LookValue;

            // El mouse y el stick tienen semánticas distintas: el mouse manda un
            // DELTA por frame (se suma directo), el stick manda una inclinación
            // SOSTENIDA (-1 a 1) que se escala por Time.deltaTime para girar a
            // grados/segundo constantes. Detectamos cuál está moviendo la acción
            // por el dispositivo de su control activo.
            bool fromGamepad = input.Look.activeControl != null &&
                               input.Look.activeControl.device is Gamepad;

            if (fromGamepad)
            {
                rotY += look.x * GamepadSensitivity * Time.deltaTime;
                rotX -= look.y * GamepadSensitivity * Time.deltaTime;
            }
            else
            {
                // *0.1 para replicar la escala del viejo eje "Mouse X" (sensibilidad 0.1).
                rotY += look.x * SensitivityX * 0.1f;
                rotX -= look.y * SensitivityY * 0.1f;
            }

            rotX = Mathf.Clamp(rotX, MinY, MaxY);
        }

        // 2. Calcular Rotación (Orbita)
        Quaternion targetRotation = Quaternion.Euler(rotX, rotY, 0);

        // 3. Calcular Posición, respetando las paredes
        Vector3 focusPoint = Target.position + PivotOffset;

        // El offset del hombro se descompone en DIRECCIÓN y DISTANCIA: la dirección se
        // mantiene siempre (para no perder la vista sobre el hombro) y lo único que se
        // acorta al chocar es la distancia.
        float   armLength = CamOffset.magnitude;
        Vector3 armDir    = armLength > 0.001f
            ? (targetRotation * CamOffset).normalized
            : targetRotation * Vector3.back;

        float desiredDistance = ResolveArmLength(focusPoint, armDir, armLength);

        // Acercarse rápido, alejarse despacio. La asimetría es a propósito: al chocar
        // hay que salir del muro YA, pero al despejarse un tirón hacia atrás se nota
        // mucho más feo que un regreso suave.
        float speed = desiredDistance < _currentDistance ? PullInSpeed : PushOutSpeed;
        _currentDistance = Mathf.MoveTowards(_currentDistance, desiredDistance, speed * Time.deltaTime);

        // 4. Aplicar
        transform.position = focusPoint + armDir * _currentDistance;
        transform.rotation = targetRotation;

        // 5. Y si quedó encima del jugador, dejar de dibujarlo
        UpdateTargetVisibility();
    }

    // =========================================================
    // COLISIÓN
    // =========================================================

    // Hasta dónde puede estirarse el brazo sin meterse en una pared.
    private float ResolveArmLength(Vector3 focusPoint, Vector3 armDir, float armLength)
    {
        if (!CollideWithGeometry) return armLength;

        // El cast sale del punto de enfoque (la cabeza) hacia donde la cámara QUIERE
        // estar. Se usa esfera y no rayo para que la cámara no se cuele por el filo de
        // las esquinas, que es donde un rayo pasa limpio y la cámara igual queda adentro.
        if (Physics.SphereCast(focusPoint, CameraRadius, armDir, out RaycastHit hit,
                               armLength, CollisionMask, QueryTriggerInteraction.Ignore))
        {
            return Mathf.Max(MinDistance, hit.distance - CollisionSkin);
        }

        return armLength;
    }

    // =========================================================
    // OCULTAR AL JUGADOR
    // =========================================================

    private void UpdateTargetVisibility()
    {
        if (!HideTargetWhenClose)
        {
            if (_targetHidden) SetTargetHidden(false);
            return;
        }

        // Dos umbrales distintos (histéresis): si usáramos uno solo, quedarse parado
        // justo en esa distancia haría parpadear el modelo varias veces por segundo.
        if (!_targetHidden && _currentDistance <= HideDistance)      SetTargetHidden(true);
        else if (_targetHidden && _currentDistance >= ShowDistance)  SetTargetHidden(false);
    }

    private void SetTargetHidden(bool hidden)
    {
        // Se releen los renderers cada vez que se oculta, no una sola vez al arrancar:
        // el jugador cambia de clase en partida y con eso cambian sus armas, así que la
        // lista de antes se queda vieja (y el arma nueva seguiría dibujándose).
        if (hidden || _targetRenderers == null) CacheTargetRenderers();

        _targetHidden = hidden;

        foreach (Renderer r in _targetRenderers)
        {
            if (r == null) continue;

            // ShadowsOnly en vez de apagar el Renderer: el personaje desaparece de la
            // vista pero sigue proyectando su sombra, así no se pierde la referencia de
            // dónde estás parado — que es justo lo que uno necesita cuando la cámara se
            // pegó contra una pared.
            r.shadowCastingMode = hidden ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        }
    }

    private void CacheTargetRenderers()
    {
        _targetRenderers = Target != null
            ? Target.GetComponentsInChildren<Renderer>(true)
            : new Renderer[0];
    }

    // Si la cámara se destruye con el jugador oculto (cambio de escena, respawn), hay
    // que devolverlo a la normalidad o queda invisible sin nadie que lo arregle.
    private void OnDisable()
    {
        if (_targetHidden) SetTargetHidden(false);
    }
}
