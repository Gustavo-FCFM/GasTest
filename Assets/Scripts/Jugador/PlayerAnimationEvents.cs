using UnityEngine;

public class PlayerAnimationEvents : MonoBehaviour
{
    private PlayerController playerController;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        playerController = GetComponentInParent<PlayerController>();
    }

    // Update is called once per frame
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
