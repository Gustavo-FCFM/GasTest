using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// ============================================================
// CharacterOutline
//
// Contorno de color alrededor de un personaje, solo visual y LOCAL. Es una copia de cada
// SkinnedMeshRenderer del cuerpo, con los MISMOS huesos y el shader Mercenaries/Outline
// (malla inflada, caras de atrás): sigue la animación y el ragdoll sin tocar los
// materiales del personaje. Lo usan el contorno de los aliados muertos del Clérigo de la
// Vida (DeadAllyHighlighter) y la franja del Corte final del Samurái
// (UI_GroundTargetIndicator), que marca a quién va a alcanzar.
// ============================================================
public static class CharacterOutline
{
    // Todo renderer de contorno se llama "...Outline": así nunca se contornea un contorno.
    public const string Suffix = "Outline";

    // Un material de contorno nuevo (cada quien el suyo: el color va por MaterialPropertyBlock
    // o en el material). null si falta el shader (Assets/Shaders/Resources/MercOutline).
    public static Material CreateMaterial(float width)
    {
        Shader shader = Resources.Load<Shader>("MercOutline");
        if (shader == null)
        {
            Debug.LogWarning("[CharacterOutline] No encontré el shader MercOutline (Assets/Shaders/Resources): sin contorno.");
            return null;
        }

        var material = new Material(shader);
        material.SetFloat("_Width", width);
        return material;
    }

    // Crea el contorno de 'body' con ese material y devuelve sus renderers (para pintarlos
    // o borrarlos). 'name' tiene que terminar en "Outline".
    public static List<SkinnedMeshRenderer> Create(Component body, Material material, string name)
    {
        var created = new List<SkinnedMeshRenderer>();
        if (body == null || material == null) return created;

        foreach (SkinnedMeshRenderer source in body.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!source.enabled || source.sharedMesh == null || source.name.EndsWith(Suffix)) continue;

            var go = new GameObject(name);
            go.layer = source.gameObject.layer;
            go.transform.SetParent(source.transform, false);

            // Mismos huesos que el cuerpo: se deforma y cae con él (ragdoll incluido).
            SkinnedMeshRenderer outline = go.AddComponent<SkinnedMeshRenderer>();
            outline.sharedMesh          = source.sharedMesh;
            outline.rootBone            = source.rootBone;
            outline.bones               = source.bones;
            outline.updateWhenOffscreen = true;   // el ragdoll lo saca de sus límites
            outline.shadowCastingMode   = ShadowCastingMode.Off;
            outline.receiveShadows      = false;

            // Un material por submalla: si no, solo se contornea la primera.
            var mats = new Material[source.sharedMesh.subMeshCount];
            for (int i = 0; i < mats.Length; i++) mats[i] = material;
            outline.sharedMaterials = mats;

            created.Add(outline);
        }
        return created;
    }

    public static void Destroy(List<SkinnedMeshRenderer> renderers)
    {
        if (renderers == null) return;
        foreach (SkinnedMeshRenderer r in renderers)
            if (r != null) Object.Destroy(r.gameObject);
        renderers.Clear();
    }
}
