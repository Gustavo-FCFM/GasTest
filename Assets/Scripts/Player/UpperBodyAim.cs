using UnityEngine;

// ============================================================
// UpperBodyAim
//
// Inclina el torso hacia donde está apuntando la cámara: mirando al cielo el personaje
// se arquea hacia atrás, mirando al piso se encorva. El YAW (a dónde mira de costado)
// no se toca — de eso ya se encarga FaceCameraForward rotando el cuerpo entero.
//
// CÓMO VIAJA POR LA RED, sin un solo RPC nuevo: el ángulo se guarda en un parámetro
// FLOAT del Animator (AimPitch), y el NetworkAnimator del prefab ya sincroniza los
// floats con suavizado. El dueño lo escribe cada frame; las demás copias lo reciben y
// aplican la misma inclinación. El parámetro no lo usa ningún estado ni transición: es
// solo el vehículo.
//
// POR QUÉ EN LateUpdate: el Animator escribe la pose en Update, así que rotar antes lo
// pisaría en el mismo frame. Acá se rota el hueso DESPUÉS, encima de lo que haya
// animado — por eso funciona igual corriendo, atacando o con el escudo arriba.
//
// EN EL EDITOR: agregar este componente a la raíz del jugador y crear en AC_Player un
// parámetro Float llamado AimPitch. Los huesos los encuentra solo si el avatar es
// humanoide; si no, se asignan a mano en Bones.
// ============================================================
public class UpperBodyAim : MonoBehaviour
{
    [Header("Animator")]
    [Tooltip("Si se deja vacío, se busca en los hijos.")]
    public Animator Target;

    [Tooltip("Parámetro FLOAT del Animator que lleva el ángulo (grados; + = mirando arriba). " +
             "Tiene que existir en AC_Player, aunque ningún estado lo use: es lo que el " +
             "NetworkAnimator sincroniza a los demás peers.")]
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

    [Tooltip("Suavizado del ángulo en el DUEÑO (segundos hasta alcanzar la mira). A los demás " +
             "peers les llega ya suavizado por el NetworkAnimator.")]
    public float Smooth = 0.08f;

    private PlayerController _player;
    private AbilitySystemComponent _asc;
    private float _pitch;
    private float _pitchVelocity;
    private bool  _hasParam;

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
        // Solo el dueño sabe hacia dónde apunta: mide el ángulo de SU cámara y lo deja
        // en el parámetro. El resto de las copias lo reciben por el NetworkAnimator.
        if (_player == null || !_player.IsOwner || !_hasParam || Target == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        // Ángulo sobre el horizonte de la dirección a la que mira la cámara.
        float wanted = -Mathf.Asin(Mathf.Clamp(cam.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
        wanted = Mathf.Clamp(wanted, -MaxUp, MaxDown);

        _pitch = Smooth > 0f
            ? Mathf.SmoothDamp(_pitch, wanted, ref _pitchVelocity, Smooth)
            : wanted;

        Target.SetFloat(PitchParam, _pitch);
    }

    private void LateUpdate()
    {
        if (Target == null || !Target.enabled || Bones == null || Bones.Length == 0) return;
        if (!_hasParam) return;

        // Un muerto no apunta (y con ragdoll el Animator está apagado, así que ni
        // llegamos acá). Aturdido tampoco: está fuera de combate.
        if (_asc != null && (_asc.HasTag(EGameplayTag.State_Dead) || _asc.HasTag(EGameplayTag.State_Stunned))) return;

        float pitch = Target.GetFloat(PitchParam);
        if (Mathf.Abs(pitch) < 0.01f) return;

        // El eje es el "derecha" del PERSONAJE, no el del hueso: así la inclinación es
        // siempre hacia adelante/atrás por más que el hueso venga girado por la animación.
        Vector3 axis = transform.right;

        for (int i = 0; i < Bones.Length; i++)
        {
            if (Bones[i] == null) continue;

            float weight = Weights != null && i < Weights.Length ? Weights[i] : 0f;
            if (Mathf.Approximately(weight, 0f)) continue;

            Bones[i].rotation = Quaternion.AngleAxis(pitch * weight, axis) * Bones[i].rotation;
        }
    }
}
