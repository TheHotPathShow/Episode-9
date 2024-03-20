using Unity.Burst;
using Unity.Entities;

public struct FixedInputEvent
{
    byte m_WasEverSet;
    uint m_LastSetTick;
    
    public void Set(uint tick)
    {
        m_LastSetTick = tick;
        m_WasEverSet = 1;
    }
    
    public bool IsSet(uint tick)
    {
        if (m_WasEverSet == 1)
            return tick == m_LastSetTick;
        return false;
    }
}

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderLast = true)]
[BurstCompile]
public partial struct FixedTickSystem : ISystem
{
    public struct Singleton : IComponentData
    {
        public uint Tick;
    }

    public void OnCreate(ref SystemState state)
    {
        state.EntityManager.AddComponentData(state.SystemHandle, new Singleton());
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        SystemAPI.GetSingletonRW<Singleton>().ValueRW.Tick++;
    }
}
