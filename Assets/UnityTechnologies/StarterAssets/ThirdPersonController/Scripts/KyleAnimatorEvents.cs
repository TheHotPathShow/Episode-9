using UnityEngine;

namespace StarterAssets
{
    public class KyleAnimatorEvents : MonoBehaviour
    {
        ThirdPersonController m_ThirdPersonController;

        void Start()
        {
            m_ThirdPersonController = GetComponentInParent<ThirdPersonController>();
        }

        void OnFootstep(AnimationEvent animationEvent)
        {
            m_ThirdPersonController.OnFootstep(animationEvent.animatorClipInfo.weight);
        }

        void OnLand(AnimationEvent animationEvent)
        {
            m_ThirdPersonController.OnLand(animationEvent.animatorClipInfo.weight);
        }
    }
}