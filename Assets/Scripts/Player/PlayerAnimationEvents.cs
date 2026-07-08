using UnityEngine;

// ============================================================
// PlayerAnimationEvents
//
// Puente entre los Animation Events de un clip (configurados en el
// modelo, un hijo del jugador) y los métodos correspondientes de
// PlayerController. Existe porque Unity solo puede llamar Animation
// Events sobre el mismo GameObject que tiene el Animator, no sobre
// el padre.
// ============================================================
public class PlayerAnimationEvents : MonoBehaviour
{
    private PlayerController playerController;

    // Busca el PlayerController en algún padre de este GameObject.
    void Awake()
    {
        playerController = GetComponentInParent<PlayerController>();
    }

    // Reenvía cada Animation Event al método homónimo de PlayerController.
    public void AnimationEvent_HitFrame()
    {
        if (playerController != null) playerController.AnimationEvent_HitFrame();
    }
    public void AnimationEvent_EnableTrail()
    {
        if (playerController != null) playerController.AnimationEvent_EnableTrail();
    }
    public void AnimationEvent_DisableTrail()
    {
        if (playerController != null) playerController.AnimationEvent_DisableTrail();
    }
}
