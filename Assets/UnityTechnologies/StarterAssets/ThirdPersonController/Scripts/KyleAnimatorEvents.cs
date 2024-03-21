using UnityEngine;

public class KyleAnimatorEvents : MonoBehaviour, IAddMonoBehaviourToEntityOnAnimatorInstantiation
{
    // Count footsteps triggered
    int m_FootstepTriggerCount;
    public bool MoveNextFootstep()
    {
        if (m_FootstepTriggerCount <= 0) 
            return false;
        m_FootstepTriggerCount--;
        return true;
    }
    void OnFootstep(AnimationEvent animationEvent) => m_FootstepTriggerCount++;

    // Count landings triggered
    int m_LandTriggerCount;
    public bool MoveNextLand()
    {
        if (m_LandTriggerCount <= 0) 
            return false;
        m_LandTriggerCount--;
        return true;
    }
    void OnLand(AnimationEvent animationEvent) => m_LandTriggerCount++;
}