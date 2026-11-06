using System.Collections.Generic;
using UnityEngine;

// ============================================================
// ClassModelSwap
//
// Cada clase es un personaje con su propio modelo (6 de noviembre de 2026, pedido del líder de
// arte). El modelo es un FBX hecho sobre EL MISMO esqueleto que el Dummy (B-hips, B-spine…): se
// apaga el cuerpo que trae el jugador y se le pone la malla nueva sobre sus huesos. Así las
// animaciones, los sockets de las armas, el ragdoll y la mira del torso no se enteran.
//
// Los huesos se buscan por NOMBRE, no por orden: el FBX puede traerlos en otro orden. Un hueso que
// el jugador no tiene (el pelo, una capa) sigue al primero de sus padres que sí tenga, como parte
// rígida. Si no encuentra ninguno, el FBX no es de este esqueleto: avisa y deja el cuerpo de antes.
//
// Corre en cada pantalla por su cuenta: la clase ya viaja por la red (PlayerController.UpdateVisuals),
// así que el modelo no agrega nada. Se agrega solo al Animator la primera vez.
//
// Con un modelo puesto el Animator va en "Always Animate". El jugador usa "Cull Update Transforms",
// que deja de mover los huesos cuando no se ve ninguno de SUS renderers, y esa lista la arma el
// Animator al nacer: la malla nueva no está y el cuerpo está apagado, así que quedaría en pose T.
// ============================================================
public class ClassModelSwap : MonoBehaviour
{
    private const string SpawnedName = "ClassModel";

    private readonly List<SkinnedMeshRenderer> _spawned = new List<SkinnedMeshRenderer>();

    private List<SkinnedMeshRenderer> _body;   // el cuerpo con el que nació
    private Dictionary<string, Transform> _bones;
    private GameObject _current;
    private Animator _animator;
    private AnimatorCullingMode _culling;   // el que traía, para volver al cuerpo original

    // Le pone ese modelo al personaje de ese Animator. Null = vuelve al cuerpo con el que nació.
    public static void Apply(Animator animator, GameObject model)
    {
        if (animator == null) return;
        if (model == null && animator.GetComponent<ClassModelSwap>() == null) return;

        ClassModelSwap swap = animator.GetComponent<ClassModelSwap>();
        if (swap == null) swap = animator.gameObject.AddComponent<ClassModelSwap>();
        swap.Show(model);
    }

    private void Show(GameObject model)
    {
        CaptureBody();
        if (model == _current) return;

        Clear();
        _current = model;

        if (model != null)
            foreach (SkinnedMeshRenderer source in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                Spawn(source);

        if (model != null && _spawned.Count == 0)
            Debug.LogWarning($"[ClassModelSwap] '{model.name}' no trae ninguna malla con huesos de este esqueleto: sigue el cuerpo de antes.");

        bool showBody = _spawned.Count == 0;
        foreach (SkinnedMeshRenderer r in _body)
            if (r != null) r.enabled = showBody;

        _animator.cullingMode = showBody ? _culling : AnimatorCullingMode.AlwaysAnimate;
    }

    // Se lee la primera vez, antes de que haya armas o contornos colgando de los huesos.
    private void CaptureBody()
    {
        if (_body != null) return;

        _animator = GetComponent<Animator>();
        _culling  = _animator.cullingMode;

        _body = new List<SkinnedMeshRenderer>();
        foreach (SkinnedMeshRenderer r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (!r.name.EndsWith(CharacterOutline.Suffix)) _body.Add(r);

        _bones = new Dictionary<string, Transform>();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (!_bones.ContainsKey(t.name)) _bones.Add(t.name, t);
    }

    private void Clear()
    {
        foreach (SkinnedMeshRenderer r in _spawned)
        {
            if (r == null) continue;
            r.enabled = false;   // Destroy recién actúa al final del cuadro
            Destroy(r.gameObject);
        }
        _spawned.Clear();
    }

    private void Spawn(SkinnedMeshRenderer source)
    {
        if (source.sharedMesh == null) return;

        Transform[] sourceBones = source.bones;
        var bones = new Transform[sourceBones.Length];
        int found = 0;
        for (int i = 0; i < sourceBones.Length; i++)
        {
            bones[i] = Resolve(sourceBones[i]);
            if (bones[i] != null) found++;
        }
        if (found == 0) return;

        var go = new GameObject(SpawnedName);
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);

        SkinnedMeshRenderer renderer = go.AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh      = source.sharedMesh;
        renderer.sharedMaterials = source.sharedMaterials;
        renderer.bones           = bones;
        renderer.rootBone        = Resolve(source.rootBone);
        renderer.localBounds     = source.localBounds;

        if (_body.Count > 0 && _body[0] != null)
        {
            go.layer = _body[0].gameObject.layer;
            renderer.updateWhenOffscreen = _body[0].updateWhenOffscreen;
            renderer.shadowCastingMode   = _body[0].shadowCastingMode;
        }

        _spawned.Add(renderer);
    }

    // El hueso del jugador con ese nombre, o el del primer padre que tenga.
    private Transform Resolve(Transform bone)
    {
        for (Transform t = bone; t != null; t = t.parent)
            if (_bones.TryGetValue(t.name, out Transform mine)) return mine;
        return null;
    }
}
