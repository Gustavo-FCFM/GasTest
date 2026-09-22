using UnityEngine;

// ============================================================
// UpperBodyAim
//
// Inclina el torso hacia donde está apuntando la cámara: mirando al cielo el personaje
// se arquea hacia atrás, mirando al piso se encorva. El YAW (a dónde mira de costado)
// no se toca — de eso ya se encarga FaceCameraForward rotando el cuerpo entero.
//
// CÓMO VIAJA POR LA RED: el dueño mide el ángulo de SU cámara y se lo pasa al
// PlayerController, que lo manda por RPC no confiable (ver "INCLINACIÓN DEL TORSO"
// allá). Las demás copias lo leen de PlayerController.NetworkAimPitch y lo suavizan.
//
// El plan original era que viajara gratis en un parámetro float del Animator, por el
// NetworkAnimator del prefab. No sirve: ese componente tiene su campo Animator VACÍO y
// el Animator está en un hijo (el modelo), así que queda inerte y no sincroniza nada.
// El parámetro AimPitch se sigue escribiendo, pero solo como ventana para mirar el
// valor en el Animator mientras se prueba: la inclinación se aplica desde _pitch, así
// que esto funciona exista o no el parámetro.
//
// POR QUÉ EN LateUpdate: el Animator escribe la pose en Update, así que rotar antes lo
// pisaría en el mismo frame. Acá se rota el hueso DESPUÉS, encima de lo que haya
// animado — por eso funciona igual corriendo, atacando o con el escudo arriba.
//
// EN EL EDITOR: agregar este componente a la raíz del jugador. Los huesos los encuentra
// solo si el avatar es humanoide; si no, se asignan a mano en Bones.
// ============================================================
public class UpperBodyAim : MonoBehaviour
{
    [Header("Animator")]
    [Tooltip("Si se deja vacío, se busca en los hijos.")]
    public Animator Target;

    [Tooltip("Parámetro FLOAT del Animator donde se copia el ángulo (grados; + = mirando " +
             "arriba). Es opcional: sirve para ver el valor en la ventana del Animator " +
             "mientras se prueba. La inclinación no depende de él.")]
    public string PitchParam = "AimPitch";

    [Header("Huesos")]
    [Tooltip("Los huesos que se inclinan, de la cadera hacia arriba. Vacío = se resuelven solos " +
             "del avatar humanoide (Spine, Chest, UpperChest, Head).")]
    public Transform[] Bones;

    [Tooltip("Cuánto del ángulo total se lleva cada hueso, en el mismo orden que Bones. " +
             "Repartirlo entre varios huesos se ve natural; cargarlo todo en uno, quebrado. " +
             "La suma normalmente da 1.")]
    public float[] Weights = { 0.3f, 0.35f, 0.2f, 0.15f };

    [Header("Límites")]
    [Tooltip("Tope de inclinación hacia arriba, en grados.")]
    public float MaxUp = 50f;

    [Tooltip("Tope de inclinación hacia abajo, en grados.")]
    public float MaxDown = 40f;

    [Tooltip("Suavizado del ángulo, en segundos. En el dueño persigue a la mira; en las " +
             "copias remotas, al último valor que llegó por la red.")]
    public float Smooth = 0.08f;

    [Header("Red")]
    [Tooltip("Cuántas veces por segundo el dueño manda su ángulo. 15 alcanza de sobra: " +
             "el resto lo tapa el suavizado.")]
    public float SendRate = 15f;

    [Tooltip("Grados que tiene que cambiar el ángulo para que valga la pena mandarlo. " +
             "Quieto, no se manda nada.")]
    public float MinDeltaToSend = 0.75f;

    private PlayerController _player;
    private AbilitySystemComponent _asc;
    private float _pitch;
    private float _pitchVelocity;
    private bool  _hasParam;

    private float _lastSent = float.NaN;
    private float _nextSendTime;

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
        _asc    = GetComponent<AbilitySystemComponent>();
        if (Target == null) Target = GetComponentInChildren<Animator>();

        ResolveBones();
        ResolveParam();
    }

    // Los huesos del avatar humanoide, de abajo hacia arriba. UpperChest no existe en
    // todos los rigs: los que falten simplemente no entran en la lista.
    private void ResolveBones()
    {
        if (Bones != null && Bones.Length > 0) return;
        if (Target == null || !Target.isHuman) return;

        var wanted = new[] { HumanBodyBones.Spine, HumanBodyBones.Chest,
                             HumanBodyBones.UpperChest, HumanBodyBones.Head };

        var found = new System.Collections.Generic.List<Transform>();
        foreach (HumanBodyBones b in wanted)
        {
            Transform t = Target.GetBoneTransform(b);
            if (t != null) found.Add(t);
        }
        Bones = found.ToArray();
    }

    private void ResolveParam()
    {
        _hasParam = false;
        if (Target == null || string.IsNullOrEmpty(PitchParam)) return;

        foreach (AnimatorControllerParameter p in Target.parameters)
            if (p.type == AnimatorControllerParameterType.Float && p.name == PitchParam) { _hasParam = true; return; }
    }

    private void Update()
    {
        if (_player == null) return;

        if (_player.IsOwner) UpdateAsOwner();
        else                 FollowNetwork();

        // Copia para poder mirarlo en la ventana del Animator. No lo lee nadie.
        if (_hasParam && Target != null) Target.SetFloat(PitchParam, _pitch);
    }

    // Solo el dueño sabe hacia dónde apunta: mide el ángulo de SU cámara, lo aplica de
    // inmediato (sin esperar la ida y vuelta) y lo manda.
    private void UpdateAsOwner()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        // Ángulo sobre el horizonte de la dirección a la que mira la cámara.
        float wanted = -Mathf.Asin(Mathf.Clamp(cam.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
        wanted = Mathf.Clamp(wanted, -MaxUp, MaxDown);

        _pitch = Smooth > 0f
            ? Mathf.SmoothDamp(_pitch, wanted, ref _pitchVelocity, Smooth)
            : wanted;

        Broadcast();
    }

    // Mandar solo cuando cambió y a un ritmo fijo: quieto mirando al frente no gasta un
    // solo paquete, y moviendo la mira gasta un byte cada 66 ms.
    private void Broadcast()
    {
        if (Time.time < _nextSendTime) return;
        if (!float.IsNaN(_lastSent) && Mathf.Abs(_pitch - _lastSent) < MinDeltaToSend) return;

        _player.SendAimPitch(_pitch);
        _lastSent     = _pitch;
        _nextSendTime = Time.time + (SendRate > 0f ? 1f / SendRate : 0f);
    }

    // En las demás copias, perseguir el último valor que llegó. El suavizado tapa el
    // escalón entre paquete y paquete (y los que se pierdan: van por canal no confiable).
    private void FollowNetwork()
    {
        float wanted = Mathf.Clamp(_player.NetworkAimPitch, -MaxUp, MaxDown);

        _pitch = Smooth > 0f
            ? Mathf.SmoothDamp(_pitch, wanted, ref _pitchVelocity, Smooth)
            : wanted;
    }

    private void LateUpdate()
    {
        if (Target == null || !Target.enabled || Bones == null || Bones.Length == 0) return;
        if (Mathf.Abs(_pitch) < 0.01f) return;

        // Un muerto no apunta (y con ragdoll el Animator está apagado, así que ni
        // llegamos acá). Aturdido tampoco: está fuera de combate.
        if (_asc != null && (_asc.HasTag(EGameplayTag.State_Dead) || _asc.HasTag(EGameplayTag.State_Stunned))) return;

        // El eje es el "derecha" del PERSONAJE, no el del hueso: así la inclinación es
        // siempre hacia adelante/atrás por más que el hueso venga girado por la animación.
        Vector3 axis = transform.right;

        for (int i = 0; i < Bones.Length; i++)
        {
            if (Bones[i] == null) continue;

            float weight = Weights != null && i < Weights.Length ? Weights[i] : 0f;
            if (Mathf.Approximately(weight, 0f)) continue;

            Bones[i].rotation = Quaternion.AngleAxis(_pitch * weight, axis) * Bones[i].rotation;
        }
    }
}
