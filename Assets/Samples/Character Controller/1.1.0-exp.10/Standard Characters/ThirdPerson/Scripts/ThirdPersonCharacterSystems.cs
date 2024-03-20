using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
using Unity.CharacterController;
using Unity.Mathematics;

[Serializable]
public struct ThirdPersonCharacterData : IComponentData
{
    public float RotationSharpness;
    public float GroundMaxSpeed;
    public float GroundedMovementSharpness;
    public float AirAcceleration;
    public float AirMaxSpeed;
    public float AirDrag;
    public float JumpSpeed;
    public float3 Gravity;
    public bool PreventAirAccelerationAgainstUngroundedHits;
    public BasicStepAndSlopeHandlingParameters StepAndSlopeHandling;
    
    public Entity ControlledCamera;
}

[Serializable]
public struct ThirdPersonCharacterControl : IComponentData
{
    public float3 MoveVector;
    public bool Jump;
}

[UpdateInGroup(typeof(KinematicCharacterPhysicsUpdateGroup))]
[BurstCompile]
public partial struct ThirdPersonCharacterPhysicsUpdateSystem : ISystem
{
    ThirdPersonCharacterUpdateContext _context;
    KinematicCharacterUpdateContext _baseContext;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _context.OnSystemCreate(ref state);
        _baseContext.OnSystemCreate(ref state);
        state.RequireForUpdate(
            KinematicCharacterUtilities.GetBaseCharacterQueryBuilder()
            .WithAll<ThirdPersonCharacterData, ThirdPersonCharacterControl>()
            .Build(ref state));
        state.RequireForUpdate<PhysicsWorldSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _context.OnSystemUpdate(ref state);
        _baseContext.OnSystemUpdate(ref state, SystemAPI.Time, SystemAPI.GetSingleton<PhysicsWorldSingleton>());
        _baseContext.EnsureCreationOfTmpCollections();
        foreach (var characterAspect in SystemAPI.Query<ThirdPersonCharacterAspect>())
        {
            characterAspect.PhysicsUpdate(ref _context, ref _baseContext);
        }
    }
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(ThirdPersonPlayerVariableStepControlSystem))]
[UpdateBefore(typeof(TransformSystemGroup))]
[BurstCompile]
public partial struct ThirdPersonCharacterVariableUpdateSystem : ISystem
{
    ThirdPersonCharacterUpdateContext _context;
    KinematicCharacterUpdateContext _baseContext;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _context = new ThirdPersonCharacterUpdateContext();
        _context.OnSystemCreate(ref state);
        _baseContext = new KinematicCharacterUpdateContext();
        _baseContext.OnSystemCreate(ref state);

        state.RequireForUpdate(
            KinematicCharacterUtilities.GetBaseCharacterQueryBuilder()
                .WithAll<ThirdPersonCharacterData, ThirdPersonCharacterControl>()
                .Build(ref state));
        state.RequireForUpdate<PhysicsWorldSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _context.OnSystemUpdate(ref state);
        _baseContext.OnSystemUpdate(ref state, SystemAPI.Time, SystemAPI.GetSingleton<PhysicsWorldSingleton>());
        _baseContext.EnsureCreationOfTmpCollections();
        foreach (var characterAspect in SystemAPI.Query<ThirdPersonCharacterAspect>())
        {
            characterAspect.VariableUpdate(ref _context, ref _baseContext);
        }
    }
}
