using Unity.Entities;
using UnityEngine;

[RequireComponent(typeof(AnimatorAuthor))]
public class KyleAnimatorAuthor : MonoBehaviour
{
    [Header("Kyle Animator Settings")]
    public float SpeedChangeRate = 10.0f;
    public float FallTimeout = 0.15f;
    
    [Header("Kyle Animator Event Settings")]
    public AudioClip LandingAudioClip;
    public AudioClip[] FootstepAudioClips;
    [Range(0, 1)] public float FootstepAudioVolume = 0.5f;
    
    class KyleAnimatorAuthorBaker : Baker<KyleAnimatorAuthor>
    {
        public override void Bake(KyleAnimatorAuthor authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new KyleAnimationData
            {
                SpeedChangeRate = authoring.SpeedChangeRate,
                FallTimeout = authoring.FallTimeout,
                FootstepAudioVolume = authoring.FootstepAudioVolume
            });
            AddComponentObject(entity, new KyleAnimationManagedData
            {
                FootstepAudioClips = authoring.FootstepAudioClips,
                LandingAudioClip = authoring.LandingAudioClip
            });
        }
    }
}
