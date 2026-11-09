using System.Collections.Generic;
using FishNet.Object;
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
//
// FishNet, en el host, prende y apaga los renderers de cada objeto según si el host lo ve, y la
// lista la arma al nacer: ahí está el cuerpo y no la malla nueva. Al prenderlo volvía a mostrar el
// Dummy encima del modelo (9 de noviembre de 2026: las copias del Ilusionista salían con los dos).
// Por eso se escucha su aviso (OnHostVisibilityUpdated) y, por si otra cosa vuelve a prender el
// cuerpo, LateUpdate lo apaga de nuevo y muestra el modelo.
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
    private NetworkObject _network;          // el de este personaje, para saber cuándo el host lo muestra

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

        _network = GetComponentInParent<NetworkObject>();
        if (_network != null) _network.OnHostVisibilityUpdated += HandleHostVisibility;

        _body = new List<SkinnedMeshRenderer>();
        foreach (SkinnedMeshRenderer r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (!r.name.EndsWith(CharacterOutline.Suffix)) _body.Add(r);

        _bones = new Dictionary<string, Transform>();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (!_bones.ContainsKey(t.name)) _bones.Add(t.name, t);
    }

    // FishNet acaba de prender o apagar lo que tenía anotado (el cuerpo): el cuerpo vuelve a apagarse
    // y el modelo hace lo que pidió FishNet.
    private void HandleHostVisibility(bool previous, bool visible)
    {
        if (_spawned.Count == 0) return;
        HideBody();
        foreach (SkinnedMeshRenderer r in _spawned)
            if (r != null) r.enabled = visible;
    }

    // Si algo volvió a prender el cuerpo, quería que el personaje se viera: se muestra el modelo.
    private void LateUpdate()
    {
        if (_spawned.Count == 0 || _body == null) return;

        bool bodyShown = false;
        foreach (SkinnedMeshRenderer r in _body)
            if (r != null && r.enabled) bodyShown = true;
        if (!bodyShown) return;

        HideBody();
        foreach (SkinnedMeshRenderer r in _spawned)
            if (r != null) r.enabled = true;
    }

    private void HideBody()
    {
        foreach (SkinnedMeshRenderer r in _body)
            if (r != null) r.enabled = false;
    }

    private void OnDestroy()
    {
        if (_network != null) _network.OnHostVisibilityUpdated -= HandleHostVisibility;
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
