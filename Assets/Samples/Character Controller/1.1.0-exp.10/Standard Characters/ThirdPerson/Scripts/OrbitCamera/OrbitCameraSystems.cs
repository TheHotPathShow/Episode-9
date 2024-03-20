using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.CharacterController;

[Serializable]
public struct CameraTarget : IComponentData
{
    public Entity TargetEntity;
}

[Serializable]
public struct OrbitCamera : IComponentData
{
    public float RotationSpeed;
    public float MaxVAngle;
    public float MinVAngle;
    public bool RotateWithCharacterParent;

    public float MinDistance;
    public float MaxDistance;
    public float DistanceMovementSpeed;
    public float DistanceMovementSharpness;

    public float ObstructionRadius;
    public float ObstructionInnerSmoothingSharpness;
    public float ObstructionOuterSmoothingSharpness;
    public bool PreventFixedUpdateJitter;
    
    public float TargetDistance;
    public float SmoothedTargetDistance;
    public float ObstructedDistance;
    public float PitchAngle;
    public float3 PlanarForward;
}

[Serializable]
public struct OrbitCameraControl : IComponentData
{
    public Entity FollowedCharacterEntity;
    public float2 LookDegreesDelta;
    public float ZoomDelta;
}

[Serializable]
public struct OrbitCameraIgnoredEntityBufferElement : IBufferElementData
{
    public Entity Entity;
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(ThirdPersonPlayerVariableStepControlSystem))]
[UpdateAfter(typeof(ThirdPersonCharacterVariableUpdateSystem))]
[UpdateBefore(typeof(TransformSystemGroup))]
[BurstCompile]
public partial struct OrbitCameraSimulationSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>();
        var parentLookup = SystemAPI.GetComponentLookup<Parent>(true);
        var postTransformMatrixLookup = SystemAPI.GetComponentLookup<PostTransformMatrix>(true);
        var cameraTargetLookup = SystemAPI.GetComponentLookup<CameraTarget>(true);
        var kinematicCharacterBodyLookup = SystemAPI.GetComponentLookup<KinematicCharacterBody>(true);

        foreach (var (orbitCameraRef, cameraTransform, cameraControl) in SystemAPI
                     .Query<RefRW<OrbitCamera>, RefRW<LocalTransform>, OrbitCameraControl>())
        {
            // Skip if we don't have a camera target
            if (!OrbitCameraUtilities.TryGetCameraTargetSimulationWorldTransform(
                    cameraControl.FollowedCharacterEntity,
                    ref localTransformLookup,
                    ref parentLookup,
                    ref postTransformMatrixLookup,
                    ref cameraTargetLookup,
                    out float4x4 targetWorldTransform)) 
                continue;
            
            // Initial information
            ref var orbitCamera = ref orbitCameraRef.ValueRW;
            float3 targetUp = targetWorldTransform.Up();
            float3 targetPosition = targetWorldTransform.Translation();

            // Update planar forward based on target up direction and rotation from parent
            {
                quaternion tmpPlanarRotation = MathUtilities.CreateRotationWithUpPriority(targetUp, orbitCamera.PlanarForward);
                    
                // Rotation from character parent 
                if (orbitCamera.RotateWithCharacterParent &&
                    kinematicCharacterBodyLookup.TryGetComponent(cameraControl.FollowedCharacterEntity, out var characterBody))
                {
                    // Only consider rotation around the character up, since the camera is already adjusting itself to character up
                    KinematicCharacterUtilities.AddVariableRateRotationFromFixedRateRotation(
                        ref tmpPlanarRotation, characterBody.RotationFromParent, 
                        SystemAPI.Time.DeltaTime, characterBody.LastPhysicsUpdateDeltaTime);
                }
                    
                orbitCamera.PlanarForward = MathUtilities.GetForwardFromRotation(tmpPlanarRotation);
            }

            // Yaw
            float yawAngleChange = cameraControl.LookDegreesDelta.x * orbitCamera.RotationSpeed;
            quaternion yawRotation = quaternion.Euler(targetUp * math.radians(yawAngleChange));
            orbitCamera.PlanarForward = math.rotate(yawRotation, orbitCamera.PlanarForward);
                
            // Pitch
            orbitCamera.PitchAngle += -cameraControl.LookDegreesDelta.y * orbitCamera.RotationSpeed;
            orbitCamera.PitchAngle = math.clamp(orbitCamera.PitchAngle, orbitCamera.MinVAngle, orbitCamera.MaxVAngle);

            // Calculate final rotation
            quaternion cameraRotation = OrbitCameraUtilities.CalculateCameraRotation(targetUp, orbitCamera.PlanarForward, orbitCamera.PitchAngle);

            // Distance input
            float desiredDistanceMovementFromInput = cameraControl.ZoomDelta * orbitCamera.DistanceMovementSpeed;
            orbitCamera.TargetDistance = math.clamp(orbitCamera.TargetDistance + desiredDistanceMovementFromInput, orbitCamera.MinDistance, orbitCamera.MaxDistance);

            // Calculate camera position (no smoothing or obstructions yet; these are done in the camera late update)
            float3 cameraPosition = OrbitCameraUtilities.CalculateCameraPosition(targetPosition, cameraRotation, orbitCamera.TargetDistance);

            // Write back to component
            cameraTransform.ValueRW = LocalTransform.FromPositionRotation(cameraPosition, cameraRotation);
        }
    }
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(TransformSystemGroup))]
[BurstCompile]
public partial struct OrbitCameraLateUpdateSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PhysicsWorldSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
        var localToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>();
        var cameraTargetLookup = SystemAPI.GetComponentLookup<CameraTarget>(true);

        foreach (var (orbitCameraRef, cameraLocalToWorld, cameraControl, ignoredEntitiesBuffer) in SystemAPI.Query<
                     RefRW<OrbitCamera>,
                     RefRW<LocalToWorld>,
                     OrbitCameraControl, 
                     DynamicBuffer<OrbitCameraIgnoredEntityBufferElement>>())
        {
            ref var orbitCamera = ref orbitCameraRef.ValueRW;
            // Skip if we don't have a camera target
            if (!OrbitCameraUtilities.TryGetCameraTargetInterpolatedWorldTransform(
                    cameraControl.FollowedCharacterEntity,
                    ref localToWorldLookup,
                    ref cameraTargetLookup,
                    out LocalToWorld targetWorldTransform)) 
                continue;
            
            quaternion cameraRotation = OrbitCameraUtilities.CalculateCameraRotation(targetWorldTransform.Up, orbitCamera.PlanarForward, orbitCamera.PitchAngle);
                
            float3 cameraForward = math.mul(cameraRotation, math.forward());
            float3 targetPosition = targetWorldTransform.Position;
                
            // Distance smoothing
            orbitCamera.SmoothedTargetDistance = math.lerp(orbitCamera.SmoothedTargetDistance, orbitCamera.TargetDistance, 
                MathUtilities.GetSharpnessInterpolant(orbitCamera.DistanceMovementSharpness, SystemAPI.Time.DeltaTime));
                
            // Obstruction handling
            // Obstruction detection is handled here, because we have to adjust the obstruction distance
            // to match the interpolated physics body transform (as opposed to the "simulation" transform). Otherwise, a
            // camera getting obstructed by a moving physics body would have visible jitter. 
            if (orbitCamera.ObstructionRadius > 0f)
            {
                float obstructionCheckDistance = orbitCamera.SmoothedTargetDistance;

                CameraObstructionHitsCollector collector = new CameraObstructionHitsCollector(cameraControl.FollowedCharacterEntity, ignoredEntitiesBuffer, cameraForward);
                physicsWorld.SphereCastCustom(
                    targetPosition,
                    orbitCamera.ObstructionRadius,
                    -cameraForward,
                    obstructionCheckDistance,
                    ref collector,
                    CollisionFilter.Default,
                    QueryInteraction.IgnoreTriggers);

                float newObstructedDistance = obstructionCheckDistance;
                if (collector.NumHits > 0)
                {
                    newObstructedDistance = obstructionCheckDistance * collector.ClosestHit.Fraction;

                    // Redo cast with the interpolated body transform to prevent FixedUpdate jitter in obstruction detection
                    if (orbitCamera.PreventFixedUpdateJitter)
                    {
                        RigidBody hitBody = physicsWorld.Bodies[collector.ClosestHit.RigidBodyIndex];
                        if (localToWorldLookup.TryGetComponent(hitBody.Entity, out LocalToWorld hitBodyLocalToWorld))
                        {
                            // Adjust the rigidbody transform for interpolation, so we can raycast it in that state
                            hitBody.WorldFromBody = new RigidTransform(quaternion.LookRotationSafe(hitBodyLocalToWorld.Forward, hitBodyLocalToWorld.Up), hitBodyLocalToWorld.Position);

                            collector = new CameraObstructionHitsCollector(cameraControl.FollowedCharacterEntity, ignoredEntitiesBuffer, cameraForward);
                            hitBody.SphereCastCustom(
                                targetPosition,
                                orbitCamera.ObstructionRadius,
                                -cameraForward,
                                obstructionCheckDistance,
                                ref collector,
                                CollisionFilter.Default,
                                QueryInteraction.IgnoreTriggers);

                            if (collector.NumHits > 0)
                            {
                                newObstructedDistance = obstructionCheckDistance * collector.ClosestHit.Fraction;
                            }
                        }
                    }
                }

                // Update current distance based on obstructed distance
                if (orbitCamera.ObstructedDistance < newObstructedDistance)
                {
                    // Move outer
                    orbitCamera.ObstructedDistance = math.lerp(orbitCamera.ObstructedDistance, newObstructedDistance, 
                        MathUtilities.GetSharpnessInterpolant(orbitCamera.ObstructionOuterSmoothingSharpness, SystemAPI.Time.DeltaTime));
                }
                else if (orbitCamera.ObstructedDistance > newObstructedDistance)
                {
                    // Move inner
                    orbitCamera.ObstructedDistance = math.lerp(orbitCamera.ObstructedDistance, newObstructedDistance, 
                        MathUtilities.GetSharpnessInterpolant(orbitCamera.ObstructionInnerSmoothingSharpness, SystemAPI.Time.DeltaTime));
                }
            }
            else
            {
                orbitCamera.ObstructedDistance = orbitCamera.SmoothedTargetDistance;
            }
                
            // Place camera at the final distance (includes smoothing and obstructions)
            float3 cameraPosition = OrbitCameraUtilities.CalculateCameraPosition(targetPosition, cameraRotation, orbitCamera.ObstructedDistance);
                
            // Write to LtW
            cameraLocalToWorld.ValueRW.Value = new float4x4(cameraRotation, cameraPosition);
        }
    }
}

public static class OrbitCameraUtilities
{
    public static bool TryGetCameraTargetSimulationWorldTransform(
        Entity targetCharacterEntity, 
        ref ComponentLookup<LocalTransform> localTransformLookup,
        ref ComponentLookup<Parent> parentLookup,
        ref ComponentLookup<PostTransformMatrix> postTransformMatrixLookup,
        ref ComponentLookup<CameraTarget> cameraTargetLookup,
        out float4x4 worldTransform)
    {
        bool foundValidCameraTarget = false;
        worldTransform = float4x4.identity;

        // Camera target is either defined by the CameraTarget component, or if not, the transform of the followed character
        if (cameraTargetLookup.TryGetComponent(targetCharacterEntity, out CameraTarget cameraTarget) &&
            localTransformLookup.HasComponent(cameraTarget.TargetEntity))
        {
            TransformHelpers.ComputeWorldTransformMatrix(
                cameraTarget.TargetEntity,
                out worldTransform,
                ref localTransformLookup,
                ref parentLookup,
                ref postTransformMatrixLookup);
            foundValidCameraTarget = true;
        }
        else if (localTransformLookup.TryGetComponent(targetCharacterEntity, out LocalTransform characterLocalTransform))
        {
            worldTransform = float4x4.TRS(characterLocalTransform.Position, characterLocalTransform.Rotation, 1f);
            foundValidCameraTarget = true;
        }

        return foundValidCameraTarget;
    }
    
    public static bool TryGetCameraTargetInterpolatedWorldTransform(
        Entity targetCharacterEntity, 
        ref ComponentLookup<LocalToWorld> localToWorldLookup,
        ref ComponentLookup<CameraTarget> cameraTargetLookup,
        out LocalToWorld worldTransform)
    {
        bool foundValidCameraTarget = false;
        worldTransform = default;

        // Get the interpolated transform of the target
        if (cameraTargetLookup.TryGetComponent(targetCharacterEntity, out CameraTarget cameraTarget) &&
            localToWorldLookup.TryGetComponent(cameraTarget.TargetEntity, out worldTransform))
        {
            foundValidCameraTarget = true;
        }
        else if (localToWorldLookup.TryGetComponent(targetCharacterEntity, out worldTransform))
        {
            foundValidCameraTarget = true;
        }

        return foundValidCameraTarget;
    }
    
    public static quaternion CalculateCameraRotation(float3 targetUp, float3 planarForward, float pitchAngle)
    {
        quaternion pitchRotation = quaternion.Euler(math.right() * math.radians(pitchAngle));
        quaternion cameraRotation = MathUtilities.CreateRotationWithUpPriority(targetUp, planarForward);
        cameraRotation = math.mul(cameraRotation, pitchRotation);
        return cameraRotation;
    }
    
    public static float3 CalculateCameraPosition(float3 targetPosition, quaternion cameraRotation, float distance)
    {
        return targetPosition + (-MathUtilities.GetForwardFromRotation(cameraRotation) * distance);
    }
}

public struct CameraObstructionHitsCollector : ICollector<ColliderCastHit>
{
    public bool EarlyOutOnFirstHit => false;
    public float MaxFraction => 1f;
    public int NumHits { get; private set; }

    public ColliderCastHit ClosestHit;

    float _closestHitFraction;
    float3 _cameraDirection;
    Entity _followedCharacter;
    DynamicBuffer<OrbitCameraIgnoredEntityBufferElement> _ignoredEntitiesBuffer;

    public CameraObstructionHitsCollector(Entity followedCharacter, DynamicBuffer<OrbitCameraIgnoredEntityBufferElement> ignoredEntitiesBuffer, float3 cameraDirection)
    {
        NumHits = 0;
        ClosestHit = default;

        _closestHitFraction = float.MaxValue;
        _cameraDirection = cameraDirection;
        _followedCharacter = followedCharacter;
        _ignoredEntitiesBuffer = ignoredEntitiesBuffer;
    }

    public bool AddHit(ColliderCastHit hit)
    {
        if (_followedCharacter == hit.Entity)
        {
            return false;
        }
        
        if (math.dot(hit.SurfaceNormal, _cameraDirection) < 0f || !PhysicsUtilities.IsCollidable(hit.Material))
        {
            return false;
        }

        for (int i = 0; i < _ignoredEntitiesBuffer.Length; i++)
        {
            if (_ignoredEntitiesBuffer[i].Entity == hit.Entity)
            {
                return false;
            }
        }

        // Process valid hit
        if (hit.Fraction < _closestHitFraction)
        {
            _closestHitFraction = hit.Fraction;
            ClosestHit = hit;
        }
        NumHits++;

        return true;
    }
}