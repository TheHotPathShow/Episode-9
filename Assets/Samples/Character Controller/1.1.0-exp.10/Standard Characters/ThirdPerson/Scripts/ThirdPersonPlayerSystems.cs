using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Unity.CharacterController;

[Serializable]
public struct ThirdPersonPlayerInputs : IComponentData
{
    public float2 MoveInput;
    public float2 CameraLookInput;
    public float CameraZoomInput;
    public bool SprintIsHeld;
    public FixedInputEvent JumpPressed;
}

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class ThirdPersonPlayerInputsSystem : SystemBase
{
    KyleInput m_Input;
    protected override void OnCreate()
    {
        m_Input = new KyleInput();
        m_Input.Enable();
        Cursor.lockState = CursorLockMode.Locked;
        RequireForUpdate<FixedTickSystem.Singleton>();
    }

    protected override void OnDestroy()
    {
        m_Input.Disable();
    }

    protected override void OnUpdate()
    {
        uint tick = SystemAPI.GetSingleton<FixedTickSystem.Singleton>().Tick;
        
        foreach (var playerInputs in SystemAPI.Query<RefRW<ThirdPersonPlayerInputs>>())
        {
            playerInputs.ValueRW.MoveInput = m_Input.Player.Move.ReadValue<Vector2>();
            playerInputs.ValueRW.CameraLookInput = m_Input.Player.Look.ReadValue<Vector2>();
            playerInputs.ValueRW.SprintIsHeld = m_Input.Player.Sprint.IsPressed();
            playerInputs.ValueRW.CameraZoomInput = -Input.mouseScrollDelta.y;
            if (m_Input.Player.Jump.triggered) 
                playerInputs.ValueRW.JumpPressed.Set(tick);
        }
    }
}

/// <summary>
/// Apply inputs that need to be read at a variable rate
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
[BurstCompile]
public partial struct ThirdPersonPlayerVariableStepControlSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (playerInputs, characterData, playerEntity) in SystemAPI
                     .Query<ThirdPersonPlayerInputs, ThirdPersonCharacterData>().WithEntityAccess())
        {
            if (SystemAPI.HasComponent<OrbitCameraControl>(characterData.ControlledCamera))
            {
                ref var cameraControl = ref SystemAPI.GetComponentRW<OrbitCameraControl>(characterData.ControlledCamera).ValueRW;
                cameraControl.FollowedCharacterEntity = playerEntity;
                cameraControl.LookDegreesDelta = playerInputs.CameraLookInput;
                cameraControl.ZoomDelta = playerInputs.CameraZoomInput;
            }
        }
    }
}

/// <summary>
/// Apply inputs that need to be read at a fixed rate.
/// It is necessary to handle this as part of the fixed step group, in case your framerate is lower than the fixed step rate.
/// </summary>
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderFirst = true)]
[BurstCompile]
public partial struct ThirdPersonPlayerFixedStepControlSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<FixedTickSystem.Singleton>();
    }
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        uint tick = SystemAPI.GetSingleton<FixedTickSystem.Singleton>().Tick;
        
        foreach (var (characterControlRef, playerInputs, characterData, transform) in SystemAPI.Query<
                     RefRW<ThirdPersonCharacterControl>, 
                     ThirdPersonPlayerInputs, 
                     ThirdPersonCharacterData, 
                     LocalTransform>())
        {
            ref var characterControl = ref characterControlRef.ValueRW;

            float3 characterUp = MathUtilities.GetUpFromRotation(transform.Rotation);
            
            // Get camera rotation, since our movement is relative to it.
            quaternion cameraRotation = quaternion.identity;
            if (SystemAPI.HasComponent<OrbitCamera>(characterData.ControlledCamera))
            {
                // Camera rotation is calculated rather than gotten from transform, because this allows us to 
                // reduce the size of the camera ghost state in a netcode prediction context.
                // If not using netcode prediction, we could simply get rotation from transform here instead.
                OrbitCamera orbitCamera = SystemAPI.GetComponent<OrbitCamera>(characterData.ControlledCamera);
                cameraRotation = OrbitCameraUtilities.CalculateCameraRotation(characterUp, orbitCamera.PlanarForward, orbitCamera.PitchAngle);
            }
            float3 cameraForwardOnUpPlane = math.normalizesafe(MathUtilities.ProjectOnPlane(MathUtilities.GetForwardFromRotation(cameraRotation), characterUp));
            float3 cameraRight = MathUtilities.GetRightFromRotation(cameraRotation);

            // Move
            characterControl.MoveVector = (playerInputs.MoveInput.y * cameraForwardOnUpPlane) + (playerInputs.MoveInput.x * cameraRight);
            characterControl.MoveVector = MathUtilities.ClampToMaxLength(characterControl.MoveVector, 1f);

            // Jump
            characterControl.Jump = playerInputs.JumpPressed.IsSet(tick);
            
            // Sprint
            characterControl.SprintIsHeld = playerInputs.SprintIsHeld;
        }
    }
}