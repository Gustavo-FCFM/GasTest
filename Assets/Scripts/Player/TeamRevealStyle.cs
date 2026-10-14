using UnityEngine;

// ============================================================
// TeamRevealStyle
//
// Cómo se ve, en MI pantalla, la silueta de un enemigo revelado a través de las paredes
// (TeamReveal: la Marca del cazador del Explorador). VIVE EN EL PREFAB DE LA CÁMARA
// (Player Camera ▸ TeamRevealStyle): la silueta usa el material de acá, y su COLOR es el
// del material (Art/Materials/Mat_TeamReveal, shader Mercenaries/XRay).
//
// TeamReveal no va en ningún prefab (el NetworkAbilitySystemComponent se la agrega a cada
// personaje al aparecer): por eso pregunta acá, a la cámara del jugador local.
// ============================================================
public class TeamRevealStyle : MonoBehaviour
{
    [Tooltip("El material de la silueta (shader Mercenaries/XRay). Su color es el de la silueta; " +
             "solo se ve donde algo tapa al personaje.")]
    [Label("Material de la silueta")]
    public Material SilhouetteMaterial;

    // El de la cámara del jugador local. Null si todavía no hay jugador.
    public static TeamRevealStyle Current { get; private set; }

    private void Awake() => Current = this;

    private void OnDestroy()
    {
        if (Current == this) Current = null;
    }
}
