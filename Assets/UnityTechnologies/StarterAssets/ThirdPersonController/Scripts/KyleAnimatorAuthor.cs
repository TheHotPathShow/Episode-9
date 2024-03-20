using Unity.Entities;
using UnityEngine;

[RequireComponent(typeof(AnimatorAuthor))]
public class KyleAnimatorAuthor : MonoBehaviour
{
    public float SpeedChangeRate = 10.0f;
    public float FallTimeout = 0.15f;
    class KyleAnimatorAuthorBaker : Baker<KyleAnimatorAuthor>
    {
        public override void Bake(KyleAnimatorAuthor authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new KyleAnimationData
            {
                SpeedChangeRate = authoring.SpeedChangeRate,
                FallTimeout = authoring.FallTimeout
            });
        }
    }
}
