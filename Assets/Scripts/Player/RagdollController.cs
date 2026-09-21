using System.Collections.Generic;
using UnityEngine;

// ============================================================
// RagdollController
//
// El cuerpo del personaje como muñeco de trapo al morir: el Animator se apaga, los
// huesos pasan a la física y el cuerpo cae (o sale despedido) en la dirección contraria
// al golpe que lo mató. Al revivir se deshace todo y el Animator vuelve a mandar.
//
// QUÉ NECESITA EN EL EDITOR (una sola vez, en el prefab del jugador): correr el
// Ragdoll Wizard de Unity sobre el modelo (GameObject ▸ 3D Object ▸ Ragdoll…) asignando
// los huesos del esqueleto. Eso crea Rigidbodies, Colliders y CharacterJoints en los
// huesos. Después, este componente en la RAÍZ del jugador: los encuentra solos.
//
// Sin ragdoll armado (cero Rigidbodies en los hijos) IsReady da false y PlayerController
// usa la animación de muerte de siempre. Así el prefab funciona igual con o sin él.
//
// ES SOLO PRESENTACIÓN: cada peer simula su propio muñeco con la misma dirección de
// golpe (llega por el mismo RPC que la muerte). No es determinista entre peers y no
// hace falta: nadie interactúa con el cuerpo.
//
// Los colliders del ragdoll van a la capa RagdollLayer mientras está activo, para que
// los ataques (que buscan la capa Character) no le peguen a un cadáver.
// ============================================================
public class RagdollController : MonoBehaviour
{
    [Tooltip("Fuerza del empujón al morir, en la dirección contraria al golpe. Se aplica " +
             "como impulso sobre la cadera. 300–600 se ve bien con la masa por defecto (~70).")]
    public float ImpulseForce = 450f;

    [Tooltip("Cuánto del empujón va hacia arriba, para que el cuerpo se despegue un poco del " +
             "piso en vez de deslizarse.")]
    public float UpwardBias = 0.35f;

    [Tooltip("Capa de los colliders del ragdoll mientras está activo. 2 = Ignore Raycast: los " +
             "ataques buscan la capa Character y los cadáveres no la tienen.")]
    public int RagdollLayer = 2;

    public bool IsActive { get; private set; }
    public bool IsReady  => _bodies.Count > 0;

    private readonly List<Rigidbody> _bodies    = new List<Rigidbody>();
    private readonly List<Collider>  _colliders = new List<Collider>();
    private readonly List<int>       _layers    = new List<int>();

    // Pose de los huesos al armar, para dejarlos como estaban al revivir (el Animator
    // los vuelve a escribir igual, pero así no hay un frame de cuerpo retorcido).
    private readonly List<Transform>  _bones     = new List<Transform>();
    private readonly List<Vector3>    _localPos  = new List<Vector3>();
    private readonly List<Quaternion> _localRot  = new List<Quaternion>();

    private Rigidbody _hips;
    private Animator  _animator;

    // La cápsula del CharacterController se apaga mientras el cuerpo está suelto: si no,
    // los huesos chocan contra la cápsula del propio personaje y el cuerpo sale disparado
    // de adentro de sí mismo. Un muerto no se mueve, así que apagarla no cuesta nada.
    private CharacterController _controller;

    private void Awake()
    {
        _animator   = GetComponentInChildren<Animator>();
        _controller = GetComponent<CharacterController>();

        foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>(true))
        {
            // El Rigidbody de la raíz (si lo hubiera) no es parte del muñeco.
            if (rb.transform == transform) continue;
            _bodies.Add(rb);
        }

        if (_bodies.Count == 0) return;

        foreach (Rigidbody rb in _bodies)
        {
            foreach (Collider c in rb.GetComponents<Collider>())
            {
                _colliders.Add(c);
                _layers.Add(c.gameObject.layer);
            }

            _bones.Add(rb.transform);
            _localPos.Add(rb.transform.localPosition);
            _localRot.Add(rb.transform.localRotation);
        }

        // La cadera: el hueso del ragdoll más cercano a la raíz en la jerarquía.
        _hips = _bodies[0];
        int bestDepth = int.MaxValue;
        foreach (Rigidbody rb in _bodies)
        {
            int depth = 0;
            for (Transform t = rb.transform; t != null && t != transform; t = t.parent) depth++;
            if (depth < bestDepth) { bestDepth = depth; _hips = rb; }
        }

        SetPhysics(false);
    }

    // Suelta el cuerpo y lo empuja. hitDirection = de dónde a dónde fue el golpe
    // (normalizado); Vector3.zero = se desploma sin empujón.
    public void Activate(Vector3 hitDirection)
    {
        if (!IsReady || IsActive) return;
        IsActive = true;

        if (_animator != null)   _animator.enabled   = false;
        if (_controller != null) _controller.enabled = false;

        SetPhysics(true);

        if (hitDirection.sqrMagnitude > 0.001f && _hips != null)
        {
            Vector3 dir = (hitDirection.normalized + Vector3.up * UpwardBias).normalized;
            _hips.AddForce(dir * ImpulseForce, ForceMode.Impulse);
        }
    }

    // Vuelve el cuerpo al Animator.
    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;

        SetPhysics(false);

        for (int i = 0; i < _bones.Count; i++)
        {
            _bones[i].localPosition = _localPos[i];
            _bones[i].localRotation = _localRot[i];
        }

        // Sin Rebind(): resetea TODOS los parámetros a su default (ActionSpeedMult a 0,
        // por ejemplo) y las animaciones quedarían congeladas. Con prenderlo alcanza: el
        // Animator vuelve a escribir los huesos en el próximo frame.
        if (_animator != null)   _animator.enabled   = true;
        if (_controller != null) _controller.enabled = true;
    }

    private void SetPhysics(bool on)
    {
        foreach (Rigidbody rb in _bodies)
        {
            rb.isKinematic      = !on;
            rb.detectCollisions = on;
            if (on)
            {
                rb.linearVelocity  = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        for (int i = 0; i < _colliders.Count; i++)
        {
            _colliders[i].enabled = on;
            _colliders[i].gameObject.layer = on ? RagdollLayer : _layers[i];
        }
    }
}
