using Unity.Entities;
using Unity.Transforms;
using System;


[Serializable]
public struct MainEntityCamera : IComponentData {}

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class MainCameraSystem : SystemBase
{
    protected override void OnUpdate()
    {
        if (MainGameObjectCamera.Instance != null 
            && SystemAPI.TryGetSingletonEntity<MainEntityCamera>(out var mainEntityCameraEntity))
        {
            var targetLocalToWorld = SystemAPI.GetComponent<LocalToWorld>(mainEntityCameraEntity);
            MainGameObjectCamera.Instance.transform.SetPositionAndRotation(targetLocalToWorld.Position, targetLocalToWorld.Rotation);
        }
    }
}