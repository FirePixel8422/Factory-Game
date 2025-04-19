using Unity.Burst;
using Unity.Mathematics;



[BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
public struct FastAABB
{
    public FastAABB(float3 _center, float3 _extents)
    {
        center = _center;
        extents = _extents;
    }

    public float3 center;
    public float3 extents;
}