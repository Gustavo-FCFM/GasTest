using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// ============================================================
// TeamReveal
//
// La silueta de color de un personaje a través de las paredes, en la pantalla de los que
// lo tienen REVELADO (NetworkAbilitySystemComponent.NetRevealedUntilTick): la Marca del
// cazador del Explorador lo revela al equipo del que lo marcó. Cada pantalla decide sola,
// comparando contra el equipo de SU jugador local, igual que PlayerVisibility.
//
// Es una copia de cada malla del cuerpo (con los mismos huesos si es animada) con el
// shader Mercenaries/XRay: solo se ve donde algo lo tapa. No toca los materiales del
// personaje. Un invisible que no fue revelado (Status_Invisible) sigue sin verse.
//
// No va en ningún prefab: el NetworkAbilitySystemComponent la agrega solo al aparecer en
// cada pantalla. Mientras nadie lo revela no hace nada más que mirar un diccionario.
// ============================================================
public class TeamReveal : MonoBehaviour
{
    // Mismo sufijo que los contornos: así nadie contornea ni copia una silueta.
    private const string CopyName = "XRay" + CharacterOutline.Suffix;

    private static readonly Color RevealColor = new Color(1f, 0.35f, 0.2f, 0.55f);
    private static Material _sharedMaterial;

    private NetworkAbilitySystemComponent _net;
    private AbilitySystemComponent        _asc;
    private readonly List<Renderer> _copies = new List<Renderer>();
    private bool _shown;

    private void Awake()
    {
        _net = GetComponent<NetworkAbilitySystemComponent>();
        _asc = GetComponent<AbilitySystemComponent>();
    }

    private void OnDisable() => Hide();

    private void LateUpdate()
    {
        bool wanted = ShouldShow();
        if (wanted == _shown) return;
        if (wanted) Show(); else Hide();
    }

    private bool ShouldShow()
    {
        if (_net == null || _asc == null || _net.NetRevealedUntilTick.Count == 0) return false;
        if (_asc.HasTag(EGameplayTag.State_Dead) || _asc.IsHiddenFromEnemies) return false;

        PlayerController local = PlayerController.LocalPlayer;
        if (local == null) return false;
        AbilitySystemComponent localAsc = local.GetComponent<AbilitySystemComponent>();
        if (localAsc == null || ReferenceEquals(localAsc, _asc)) return false;

        return _net.IsRevealedToTeam(localAsc.TeamID);
    }

    private void Show()
    {
        _shown = true;
        Material material = SharedMaterial();
        if (material == null) return;

        // Se copian ahora y no al aparecer: el modelo cambia con la clase. Solo el MODELO
        // (lo que cuelga del Animator, armas incluidas): ni la barra de vida ni los VFX.
        Animator model = GetComponentInChildren<Animator>();
        Transform root = model != null ? model.transform : transform;
        foreach (Renderer source in root.GetComponentsInChildren<Renderer>())
        {
            if (!source.enabled || source.name.EndsWith(CharacterOutline.Suffix)) continue;

            if (source is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
            {
                var go = new GameObject(CopyName);
                go.layer = source.gameObject.layer;
                go.transform.SetParent(source.transform, false);
                var copy = go.AddComponent<SkinnedMeshRenderer>();
                copy.sharedMesh          = skinned.sharedMesh;
                copy.rootBone            = skinned.rootBone;
                copy.bones               = skinned.bones;
                copy.updateWhenOffscreen = true;
                Finish(copy, material, skinned.sharedMesh.subMeshCount);
            }
            else if (source is MeshRenderer && source.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
            {
                var go = new GameObject(CopyName);
                go.layer = source.gameObject.layer;
                go.transform.SetParent(source.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                Finish(go.AddComponent<MeshRenderer>(), material, filter.sharedMesh.subMeshCount);
            }
        }
    }

    private void Finish(Renderer copy, Material material, int subMeshes)
    {
        copy.shadowCastingMode = ShadowCastingMode.Off;
        copy.receiveShadows    = false;
        var mats = new Material[Mathf.Max(1, subMeshes)];
        for (int i = 0; i < mats.Length; i++) mats[i] = material;
        copy.sharedMaterials = mats;
        _copies.Add(copy);
    }

    private void Hide()
    {
        _shown = false;
        foreach (Renderer r in _copies)
            if (r != null) Destroy(r.gameObject);
        _copies.Clear();
    }

    private static Material SharedMaterial()
    {
        if (_sharedMaterial != null) return _sharedMaterial;
        Shader shader = Resources.Load<Shader>("MercXRay");
        if (shader == null)
        {
            Debug.LogWarning("[TeamReveal] No encontré el shader MercXRay (Assets/Shaders/Resources): sin silueta.");
            return null;
        }
        _sharedMaterial = new Material(shader);
        _sharedMaterial.SetColor("_Color", RevealColor);
        return _sharedMaterial;
    }
}
