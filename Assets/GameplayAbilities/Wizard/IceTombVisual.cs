using UnityEngine;

// ============================================================
// IceTombVisual  (la Tumba helada del Mago, a la vista — 21 de octubre de 2026)
//
// Va en el VFX de GE_IceTomb ("VFX en el objetivo"): el cubo de hielo que encierra al Mago.
//
//   · CONGELA LA POSE: el Animator del personaje se queda quieto (velocidad 0) mientras el cubo
//     exista, y sigue como estaba cuando el cubo se va (se acabó el efecto o apretó la Q otra vez).
//   · El cubo NO GIRA con el personaje: se queda con el giro con que apareció, aunque el jugador
//     siga moviendo la cámara adentro.
//
// Corre en todas las pantallas (el VFX del efecto sale en todas) y no toca nada de juego: lo de
// no moverse, no atacar y no recibir daño lo dan los tags del efecto.
// ============================================================
public class IceTombVisual : MonoBehaviour
{
    [Tooltip("Congela la animación del personaje mientras el cubo exista.")]
    [Label("Congelar la pose")]
    public bool FreezePose = true;

    [Tooltip("El cubo se queda con el giro con que apareció, aunque el personaje gire adentro.")]
    [Label("No gira con el personaje")]
    public bool KeepWorldRotation = true;

    private Animator   _animator;
    private float      _speedBefore = 1f;
    private Quaternion _rotation;
    private bool       _started;

    private void OnEnable()
    {
        if (!FreezePose) return;

        AbilitySystemComponent asc = GetComponentInParent<AbilitySystemComponent>();
        _animator = asc != null ? asc.GetComponentInChildren<Animator>() : null;
        if (_animator == null) return;

        _speedBefore    = _animator.speed;
        _animator.speed = 0f;
    }

    private void OnDisable()
    {
        if (_animator != null) _animator.speed = _speedBefore > 0f ? _speedBefore : 1f;
        _animator = null;
    }

    // El giro, recién en Start: quien lo crea ya terminó de acomodarlo.
    private void Start()
    {
        _rotation = transform.rotation;
        _started  = true;
    }

    private void LateUpdate()
    {
        if (KeepWorldRotation && _started) transform.rotation = _rotation;
    }
}
