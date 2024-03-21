using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
using Unity.CharacterController;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

[Serializable]
public struct ThirdPersonCharacterData : IComponentData
{
    public float RotationSharpness;
    public float WalkSpeed;
    public float SprintSpeed;
    public float GroundedMovementSharpness;
    public float AirAcceleration;
    public float AirDrag;
    public float JumpSpeed;
    public float3 Gravity;
    public bool PreventAirAccelerationAgainstUngroundedHits;
    public BasicStepAndSlopeHandlingParameters StepAndSlopeHandling;
    
    public Entity ControlledCamera;
    public Entity AnimationEntity;
}

[Serializable]
public struct ThirdPersonCharacterInput : IComponentData
{
    public float3 MoveVector;
    public bool SprintIsHeld;
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
            .WithAll<ThirdPersonCharacterData, ThirdPersonCharacterInput>()
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

struct KyleAnimationData : IComponentData
{
    // Read-Only Properties
    public float SpeedChangeRate;
    public float FallTimeout;
    public float FootstepAudioVolume;
    
    // Read-Write Properties
    public float MotionBlend;
    public float FallTimeoutDelta;
}

public class KyleAnimationManagedData : IComponentData
{
    public AudioClip LandingAudioClip;
    public AudioClip[] FootstepAudioClips;
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
    static readonly int AnimIDSpeed = Animator.StringToHash("Speed");
    static readonly int AnimIDGrounded = Animator.StringToHash("Grounded");
    static readonly int AnimIDJump = Animator.StringToHash("Jump");
    static readonly int AnimIDFreeFall = Animator.StringToHash("FreeFall");
    static readonly int AnimIDMotionSpeed = Animator.StringToHash("MotionSpeed");

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _context = new ThirdPersonCharacterUpdateContext();
        _context.OnSystemCreate(ref state);
        _baseContext = new KinematicCharacterUpdateContext();
        _baseContext.OnSystemCreate(ref state);

        state.RequireForUpdate(
            KinematicCharacterUtilities.GetBaseCharacterQueryBuilder()
                .WithAll<ThirdPersonCharacterData, ThirdPersonCharacterInput>()
                .Build(ref state));
        state.RequireForUpdate<PhysicsWorldSingleton>();
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<Animator>().Build());
    }
    
    public void OnUpdate(ref SystemState state)
    {
        _context.OnSystemUpdate(ref state);
        _baseContext.OnSystemUpdate(ref state, SystemAPI.Time, SystemAPI.GetSingleton<PhysicsWorldSingleton>());
        _baseContext.EnsureCreationOfTmpCollections();
        foreach (var characterAspect in SystemAPI.Query<ThirdPersonCharacterAspect>())
        {
            characterAspect.VariableUpdate(ref _context, ref _baseContext);
            
            // Handle Animation
            var characterData = characterAspect.CharacterData.ValueRO;
            if (characterData.AnimationEntity == Entity.Null) 
                continue;
            
            // Get Animation Data
            var animator = SystemAPI.ManagedAPI.GetComponent<Animator>(characterData.AnimationEntity);
            ref var animationData = ref SystemAPI.GetComponentRW<KyleAnimationData>(characterData.AnimationEntity).ValueRW;
            var characterInput = characterAspect.CharacterInput.ValueRO;
            var characterBody = characterAspect.CharacterAspect.CharacterBody.ValueRO;
            var localTransform = characterAspect.CharacterAspect.LocalTransform.ValueRO;
            var targetSpeed = characterInput.SprintIsHeld ? characterData.SprintSpeed : characterData.WalkSpeed;
                
            // Update Motion Blend
            animationData.MotionBlend = math.lerp(animationData.MotionBlend, 
                targetSpeed * math.length(characterInput.MoveVector), 
                SystemAPI.Time.DeltaTime * animationData.SpeedChangeRate);
            if (animationData.MotionBlend < 0.01f) 
                animationData.MotionBlend = 0f;
            animator.SetFloat(AnimIDSpeed, animationData.MotionBlend);
            animator.SetFloat(AnimIDMotionSpeed, 1);
                
            // Update Jump and Grounded States
            animator.SetBool(AnimIDJump, characterInput.Jump);
            animator.SetBool(AnimIDGrounded, characterBody.IsGrounded);
            if (characterBody.IsGrounded)
            {
                animationData.FallTimeoutDelta = animationData.FallTimeout;
                animator.SetBool(AnimIDFreeFall, false);
            }
            else
            {
                if (animationData.FallTimeoutDelta >= 0.0f)
                    animationData.FallTimeoutDelta -= SystemAPI.Time.DeltaTime;
                else
                    animator.SetBool(AnimIDFreeFall, true);
            }
            
            // Handle Animation Events
            var animatorEvents = SystemAPI.ManagedAPI.GetComponent<KyleAnimatorEvents>(characterData.AnimationEntity);
            var managedData = SystemAPI.ManagedAPI.GetComponent<KyleAnimationManagedData>(characterData.AnimationEntity);
            while (animatorEvents.MoveNextFootstep())
            {
                if (managedData.FootstepAudioClips.Length > 0)
                {
                    var index = Random.Range(0, managedData.FootstepAudioClips.Length);
                    AudioSource.PlayClipAtPoint(managedData.FootstepAudioClips[index], localTransform.Position, animationData.FootstepAudioVolume);
                }
            }
            
            while (animatorEvents.MoveNextLand())
            {
                AudioSource.PlayClipAtPoint(managedData.LandingAudioClip, localTransform.Position, animationData.FootstepAudioVolume);
            }
        }
    }
}
