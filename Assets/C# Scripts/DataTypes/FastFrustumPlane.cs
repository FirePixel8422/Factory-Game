using Unity.Mathematics;

public struct FastFrustumPlane
{
    public float3 normal;
    public float distance;

    public FastFrustumPlane(float3 _normal, float _distance)
    {
        normal = _normal;
        distance = _distance;
    }
}
