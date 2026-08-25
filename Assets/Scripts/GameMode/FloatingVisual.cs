using UnityEngine;

// ============================================================
// FloatingVisual
//
// Hace que algo FLOTE: sube y baja, se bambolea despacio, y se inclina hacia adelante
// cuando avanza. Pensado para el fantasma, pero sirve para cualquier cosa que tenga que
// verse suspendida en el aire (el Objetivo, un tótem, un orbe).
//
// POR QUÉ UN SCRIPT Y NO UNA ANIMACIÓN: un clip de Animator para esto sale caro de
// hacer y sale mal — hay que animar a mano una curva senoidal, queda igual para todas
// las copias (tres fantasmas de un campamento subiendo y bajando EXACTAMENTE al mismo
// tiempo se ve a la legua) y encima habría que mezclarlo con las animaciones de caminar
// y atacar. Acá son cuatro líneas de seno, cada instancia arranca en una fase distinta,
// y no se pelea con nada.
//
// DÓNDE PONERLO: en el HIJO que tiene el modelo (en Net_Enemy es "Ghost 1 con anim"),
// nunca en la raíz. La raíz la manejan el NavMeshAgent y el NetworkTransform, y moverla
// desde acá sería pelearse con ellos: el fantasma se iría flotando fuera del NavMesh o
// llegaría a los saltos a las otras máquinas. Moviendo solo el modelo, la lógica sigue
// pegada al piso y lo que flota es la vista.
//
// Corre en LateUpdate a propósito: así escribe DESPUÉS del Animator y no hay forma de
// que un clip le pise la posición.
//
// No necesita red: es puro adorno. Que cada máquina lo calcule por su cuenta (y que
// hasta esté en distinta fase) no lo nota nadie y no gasta un solo byte.
// ============================================================
[DisallowMultipleComponent]
public class FloatingVisual : MonoBehaviour
{
    private const float Tau = Mathf.PI * 2f;

    [Header("Qué flota")]
    [Tooltip("El objeto que se mueve. Si lo dejás vacío, se mueve este mismo — que es lo " +
             "normal: se pone el componente directamente en el modelo.")]
    public Transform Target;

    [Header("Subir y bajar")]
    [Tooltip("Cuánto sube y baja respecto de su posición original, en metros.")]
    public float BobHeight = 0.22f;

    [Tooltip("Ciclos por segundo. 0.6 = una subida y bajada completa cada segundo y medio.")]
    public float BobSpeed = 0.6f;

    [Tooltip("Cada copia arranca en un momento distinto del ciclo. Dejalo prendido: es lo " +
             "que evita que un campamento entero suba y baje al mismo tiempo, que es " +
             "justo lo que delata que es un truco.")]
    public bool RandomizePhase = true;

    [Header("Bamboleo")]
    [Tooltip("Cuánto se ladea mientras flota, en grados. Poco alcanza: 3 o 4.")]
    public float SwayAngle = 4f;

    [Tooltip("Velocidad del bamboleo. A propósito NO es la misma que la de subir y bajar: " +
             "si las dos coincidieran, el movimiento se repetiría de forma obvia.")]
    public float SwaySpeed = 0.37f;

    [Header("Inclinarse al avanzar")]
    [Tooltip("Cuánto se inclina hacia adelante cuando persigue a alguien, en grados. " +
             "En 0 se apaga.")]
    public float LeanAngle = 8f;

    [Tooltip("A qué velocidad de movimiento llega a la inclinación máxima (m/s).")]
    public float LeanReferenceSpeed = 3.5f;

    [Tooltip("Qué tan rápido acompaña los cambios de velocidad. Bajo = más perezoso.")]
    public float LeanSmoothing = 4f;

    // Posición y rotación de partida: todo se calcula COMO OFFSET de esto, así podés
    // acomodar el modelo donde quieras y el flotado lo respeta.
    private Vector3 _basePosition;
    private Quaternion _baseRotation;

    private Transform _floater;
    private Transform _root;
    private Vector3 _lastRootPosition;
    private float _phase;
    private float _lean;

    private void Awake()
    {
        _floater      = Target != null ? Target : transform;
        _basePosition = _floater.localPosition;
        _baseRotation = _floater.localRotation;

        // La fase es fija por instancia (se sortea una vez), no un ruido por frame: el
        // movimiento tiene que ser suave, solo desfasado respecto de los hermanos.
        _phase = RandomizePhase ? Random.value * Tau : 0f;

        // El "cuerpo" es el padre: de ahí se lee cuánto se está moviendo para inclinarlo.
        _root = _floater.parent != null ? _floater.parent : _floater;
        _lastRootPosition = _root.position;

        WarnIfOnTheWrongObject();
    }

    private void LateUpdate()
    {
        float time = Time.time;

        // --- subir y bajar ---
        float bob = Mathf.Sin(time * BobSpeed * Tau + _phase) * BobHeight;

        // --- inclinación según cuánto avanza el cuerpo ---
        UpdateLean();

        // --- bamboleo: dos senos con frecuencias que no son múltiplos entre sí, para
        //     que el ciclo tarde muchísimo en repetirse de forma reconocible ---
        float yaw  = Mathf.Sin(time * SwaySpeed * Tau + _phase * 1.7f) * SwayAngle;
        float roll = Mathf.Sin(time * SwaySpeed * 0.61f * Tau + _phase) * SwayAngle * 0.6f;

        _floater.localPosition = _basePosition + Vector3.up * bob;
        _floater.localRotation = _baseRotation * Quaternion.Euler(_lean, yaw, roll);
    }

    // Se mide cuánto se movió el cuerpo entre frames en vez de preguntarle al
    // NavMeshAgent: en los clientes el agente está apagado (la posición llega por red),
    // así que esta es la única cuenta que da lo mismo en todas las máquinas.
    private void UpdateLean()
    {
        if (Mathf.Approximately(LeanAngle, 0f) || Time.deltaTime <= 0f) { _lean = 0f; return; }

        Vector3 delta = _root.position - _lastRootPosition;
        delta.y = 0f;
        _lastRootPosition = _root.position;

        float speed  = delta.magnitude / Time.deltaTime;
        float target = Mathf.Clamp01(speed / Mathf.Max(0.1f, LeanReferenceSpeed)) * LeanAngle;

        // Suavizado exponencial: se comporta igual a 30 que a 144 fps.
        _lean = Mathf.Lerp(_lean, target, 1f - Mathf.Exp(-LeanSmoothing * Time.deltaTime));
    }

    // El error clásico es ponerlo en la raíz del personaje. Ahí pelea contra el
    // NavMeshAgent y el NetworkTransform, y el resultado es un fantasma que tiembla o
    // que se va del NavMesh. Mejor avisar que dejar que lo descubra jugando.
    private void WarnIfOnTheWrongObject()
    {
        bool isBody = _floater.GetComponent<UnityEngine.AI.NavMeshAgent>() != null
                   || _floater.GetComponent<CharacterController>() != null
                   || _floater.GetComponent<FishNet.Object.NetworkObject>() != null;

        if (isBody)
            Debug.LogWarning($"[FloatingVisual] '{name}' está en la raíz del personaje, que la " +
                             "manejan el NavMeshAgent y la red. Movelo al hijo que tiene el modelo " +
                             "(en Net_Enemy es 'Ghost 1 con anim') o asignale ese hijo en Target.", this);
    }

    // Deja el modelo en su posición original. Sirve si alguna vez se apaga el flotado en
    // caliente: sin esto quedaría congelado a media subida.
    private void OnDisable()
    {
        if (_floater == null) return;
        _floater.localPosition = _basePosition;
        _floater.localRotation = _baseRotation;
    }
}
