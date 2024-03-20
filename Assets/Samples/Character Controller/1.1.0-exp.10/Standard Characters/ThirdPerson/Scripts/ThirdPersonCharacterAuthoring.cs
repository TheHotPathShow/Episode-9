using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.CharacterController;

[DisallowMultipleComponent]
public class ThirdPersonCharacterAuthoring : MonoBehaviour
{
    [Header("Setup")]
    public GameObject ControlledCamera;
    
    public AuthoringKinematicCharacterProperties CharacterProperties = AuthoringKinematicCharacterProperties.GetDefault();
    
    [Header("Additional Character Specific Settings")]
    public float RotationSharpness = 25f;
    public float WalkSpeed = 10f;
    public float SprintSpeed = 15f;
    public float GroundedMovementSharpness = 10f;
    public float AirAcceleration = 50f;
    public float AirDrag = 1f;
    public float JumpSpeed = 10f;
    public float3 Gravity = math.up() * -25;
    public bool PreventAirAccelerationAgainstUngroundedHits = true;
    public BasicStepAndSlopeHandlingParameters StepAndSlopeHandling = BasicStepAndSlopeHandlingParameters.GetDefault();

    public class Baker : Baker<ThirdPersonCharacterAuthoring>
    {
        public override void Bake(ThirdPersonCharacterAuthoring authoring)
        {
            KinematicCharacterUtilities.BakeCharacter(this, authoring, authoring.CharacterProperties);

            Entity entity = GetEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.WorldSpace);

            AddComponent(entity, new ThirdPersonCharacterData
            {
                RotationSharpness = authoring.RotationSharpness,
                WalkSpeed = authoring.WalkSpeed,
                SprintSpeed = authoring.SprintSpeed,
                GroundedMovementSharpness = authoring.GroundedMovementSharpness,
                AirAcceleration = authoring.AirAcceleration,
                AirDrag = authoring.AirDrag,
                JumpSpeed = authoring.JumpSpeed,
                Gravity = authoring.Gravity,
                PreventAirAccelerationAgainstUngroundedHits = authoring.PreventAirAccelerationAgainstUngroundedHits,
                StepAndSlopeHandling = authoring.StepAndSlopeHandling,
                ControlledCamera = GetEntity(authoring.ControlledCamera, TransformUsageFlags.Dynamic),
            });
            AddComponent<ThirdPersonCharacterInput>(entity);
            AddComponent<ThirdPersonPlayerInputs>(entity);
        }
    }

}
