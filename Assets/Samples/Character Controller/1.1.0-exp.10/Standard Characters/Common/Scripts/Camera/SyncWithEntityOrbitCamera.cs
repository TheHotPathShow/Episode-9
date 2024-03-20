using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class SyncWithEntityOrbitCamera : MonoBehaviour
{
    public static Camera Instance;
    void Awake() => Instance = GetComponent<Camera>();
}

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class MainCameraSystem : SystemBase
{
    protected override void OnUpdate()
    {
        if (SyncWithEntityOrbitCamera.Instance != null 
            && SystemAPI.TryGetSingletonEntity<OrbitCamera>(out var mainEntityCameraEntity))
        {
            var targetLocalToWorld = SystemAPI.GetComponent<LocalToWorld>(mainEntityCameraEntity);
            SyncWithEntityOrbitCamera.Instance.transform.SetPositionAndRotation(targetLocalToWorld.Position, targetLocalToWorld.Rotation);
        }
    }
}