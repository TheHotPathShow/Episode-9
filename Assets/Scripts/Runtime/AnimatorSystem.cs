using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

struct AnimatorInstantiationData : IComponentData
{
    public UnityObjectRef<GameObject> AnimatorGameObject;
}

class AnimatorCleanup : ICleanupComponentData
{
    public Animator DestroyThisAnimator;
}

[UpdateAfter(typeof(TransformSystemGroup))]
partial struct AnimatorSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            var playerEntity = SystemAPI.GetSingletonEntity<AnimatorInstantiationData>();
            state.EntityManager.DestroyEntity(playerEntity);
        }
        
        // Instantiate the Animator if it doesn't exist
        foreach (var entity in SystemAPI.QueryBuilder()
                     .WithAll<AnimatorInstantiationData>().WithNone<Animator>()
                     .Build().ToEntityArray(state.WorldUpdateAllocator))
        {
            var data = SystemAPI.GetComponent<AnimatorInstantiationData>(entity);
            var spawnedAnimator = Object.Instantiate(data.AnimatorGameObject.Value).GetComponent<Animator>();
            state.EntityManager.AddComponentObject(entity, spawnedAnimator);
            state.EntityManager.AddComponentData(entity, new AnimatorCleanup
            {
                DestroyThisAnimator = spawnedAnimator
            });
        }

        // Sync the Animator's transform with the LocalToWorld
        foreach (var (ltw, animator) in SystemAPI.Query<LocalToWorld, SystemAPI.ManagedAPI.UnityEngineComponent<Animator>>())
        {
            animator.Value.transform.SetPositionAndRotation(ltw.Position, ltw.Rotation);
        }
        
        // If the Animator is destroyed, remove the AnimatorCleanup component
        foreach (var entity in SystemAPI.QueryBuilder()
                     .WithAll<AnimatorCleanup>().WithNone<AnimatorInstantiationData>()
                     .Build().ToEntityArray(state.WorldUpdateAllocator))
        {
            var data = SystemAPI.ManagedAPI.GetComponent<AnimatorCleanup>(entity);
            Object.Destroy(data.DestroyThisAnimator.gameObject);
            state.EntityManager.RemoveComponent<AnimatorCleanup>(entity);
        }
    }
}


#if UNITY_EDITOR
struct EditorAnimatorVisualEntityPrefab : IComponentData
{
    public Entity Prefab;
}

[WorldSystemFilter(WorldSystemFilterFlags.Editor)]
partial struct EditorAnimatorSystem : ISystem, ISystemStartStop
{
    [BurstCompile]
    public void OnCreate(ref SystemState state) => state.RequireForUpdate<EditorAnimatorVisualEntityPrefab>();

    [BurstCompile]
    public void OnStartRunning(ref SystemState state)
    {
        var query = SystemAPI.QueryBuilder().WithAll<EditorAnimatorVisualEntityPrefab>().Build();
        foreach (var originalEntity in query.ToEntityArray(state.WorldUpdateAllocator))
        {
            var data = SystemAPI.GetComponent<EditorAnimatorVisualEntityPrefab>(originalEntity);
            var originalLocalToWorld = SystemAPI.GetComponent<LocalToWorld>(originalEntity);
            var spawnedPrefab = state.EntityManager.Instantiate(data.Prefab);
            
            // Make original entity parent of spawned prefab - so it moves with the original entity
            if (SystemAPI.HasComponent<Parent>(spawnedPrefab))
                SystemAPI.SetComponent(spawnedPrefab, new Parent { Value = originalEntity });
            else
                state.EntityManager.AddComponentData(spawnedPrefab, new Parent { Value = originalEntity });
            
            // Ensure parent has a LocalTransform with correct values from the start
            if (SystemAPI.HasComponent<LocalTransform>(originalEntity))
                SystemAPI.SetComponent(originalEntity, LocalTransform.FromMatrix(originalLocalToWorld.Value));
            else 
                state.EntityManager.AddComponentData(originalEntity, LocalTransform.FromMatrix(originalLocalToWorld.Value));
        }
        
        // Remove the EditorAnimatorVisualEntityPrefab component
        state.EntityManager.RemoveComponent<EditorAnimatorVisualEntityPrefab>(query);
    }

    public void OnStopRunning(ref SystemState state) {}
}
#endif