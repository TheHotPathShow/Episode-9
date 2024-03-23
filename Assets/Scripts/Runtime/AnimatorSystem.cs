using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

struct AnimatorInstantiationData : IComponentData
{
    public UnityObjectRef<GameObject> GameObject;
    public ulong ComponentIndices;
}

class GameObjectCleanup : ICleanupComponentData
{
    public GameObject DestroyThisGameObject;
}

[UpdateAfter(typeof(TransformSystemGroup))]
partial struct AnimatorSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // Instantiate the Animator if it doesn't exist
        foreach (var entity in SystemAPI.QueryBuilder()
                     .WithAll<AnimatorInstantiationData>().WithNone<GameObjectCleanup>()
                     .Build().ToEntityArray(state.WorldUpdateAllocator))
        {
            var data = SystemAPI.GetComponent<AnimatorInstantiationData>(entity);
            var spawned = Object.Instantiate(data.GameObject.Value);
            state.EntityManager.AddComponentData(entity, new GameObjectCleanup
            {
                DestroyThisGameObject = spawned
            });
            var componentIndices = data.ComponentIndices;
            foreach (var component in spawned.GetComponents<Component>())
            {
                if ((componentIndices & 1) != 0)
                    state.EntityManager.AddComponentObject(entity, component);
                componentIndices >>= 1;
            }
        }

        // Sync the Animator's transform with the LocalToWorld
        foreach (var (ltw, gameObjectCleanup) in SystemAPI.Query<LocalToWorld, GameObjectCleanup>())
        {
            gameObjectCleanup.DestroyThisGameObject.transform.SetPositionAndRotation(ltw.Position, ltw.Rotation);
        }
        
        // If the Animator is destroyed, remove the AnimatorCleanup component
        foreach (var entity in SystemAPI.QueryBuilder()
                     .WithAll<GameObjectCleanup>().WithNone<AnimatorInstantiationData>()
                     .Build().ToEntityArray(state.WorldUpdateAllocator))
        {
            var data = SystemAPI.ManagedAPI.GetComponent<GameObjectCleanup>(entity);
            Object.Destroy(data.DestroyThisGameObject);
            state.EntityManager.RemoveComponent<GameObjectCleanup>(entity);
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