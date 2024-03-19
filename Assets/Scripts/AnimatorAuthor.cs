using Unity.Entities;
using UnityEngine;

#if UNITY_EDITOR
public class AnimatorAuthor : MonoBehaviour
{
    [SerializeField] Animator Animator;
    
    class AnimatorAuthorBaker : Baker<AnimatorAuthor>
    {
        public override void Bake(AnimatorAuthor authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Renderable);
            AddComponent(entity, new AnimatorInstantiationData
            {
                AnimatorGameObject = authoring.Animator.gameObject
            });

            if (IsBakingForEditor())
            {
                AddComponent(entity, new EditorAnimatorVisualEntityPrefab
                {
                    Prefab = GetEntity(authoring.Animator.gameObject, TransformUsageFlags.Dynamic)
                });
            }
        }
    }
}
#endif
