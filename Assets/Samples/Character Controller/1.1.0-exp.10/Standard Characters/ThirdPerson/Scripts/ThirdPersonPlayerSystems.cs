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
    public FixedInputEvent JumpPressed;
}

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class ThirdPersonPlayerInputsSystem : SystemBase
{
    protected override void OnCreate()
    {
        Cursor.lockState = CursorLockMode.Locked;
        RequireForUpdate<FixedTickSystem.Singleton>();
    }

    protected override void OnUpdate()
    {
        uint tick = SystemAPI.GetSingleton<FixedTickSystem.Singleton>().Tick;
        
        foreach (var playerInputs in SystemAPI.Query<RefRW<ThirdPersonPlayerInputs>>())
        {
            playerInputs.ValueRW.MoveInput = new float2
            {
                x = (Input.GetKey(KeyCode.D) ? 1f : 0f) + (Input.GetKey(KeyCode.A) ? -1f : 0f),
                y = (Input.GetKey(KeyCode.W) ? 1f : 0f) + (Input.GetKey(KeyCode.S) ? -1f : 0f),
            };

            playerInputs.ValueRW.CameraLookInput = new float2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
            playerInputs.ValueRW.CameraZoomInput = -Input.mouseScrollDelta.y;

            if (Input.GetKeyDown(KeyCode.Space)) 
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
        
        foreach (var (playerInputs, characterData, playerEntity) in SystemAPI.Query<ThirdPersonPlayerInputs, ThirdPersonCharacterData>().WithEntityAccess())
        {
            if (SystemAPI.HasComponent<ThirdPersonCharacterControl>(playerEntity))
            {
                ref var characterControl = ref SystemAPI.GetComponentRW<ThirdPersonCharacterControl>(playerEntity).ValueRW;

                float3 characterUp = MathUtilities.GetUpFromRotation(SystemAPI.GetComponent<LocalTransform>(playerEntity).Rotation);
                
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
            }
        }
    }
}