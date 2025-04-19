using TMPro;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;


[BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
public struct FrustumCullingJobParallel : IJobParallelFor
{
    [ReadOnly][NoAlias] public Bounds meshBounds;
    [ReadOnly][NoAlias] public NativeArray<FastFrustumPlane> frustumPlanes;

    [ReadOnly][NoAlias] public NativeArray<Matrix4x4> matrices;
    [ReadOnly][NoAlias] public int startIndex;

    [NativeDisableParallelForRestriction]
    [WriteOnly][NoAlias] public NativeList<Matrix4x4>.ParallelWriter visibleMatrices;


    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
    public void Execute(int index)
    {
        Matrix4x4 targetMatrix = matrices[startIndex + index];

        FastAABB transformedBounds = TransformBounds(meshBounds, targetMatrix);

        //if mesh with targetMatrix is visible in the frustum, add it to visibleMatrices
        if (IsAABBInsideFrustum(frustumPlanes, transformedBounds))
        {
            visibleMatrices.AddNoResize(targetMatrix);
        }
    }

    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
    private bool IsAABBInsideFrustum(NativeArray<FastFrustumPlane> planes, FastAABB bounds)
    {
        for (int i = 0; i < planes.Length; i++)
        {
            float3 normal = planes[i].normal;
            float distance = planes[i].distance;

            // AABB extents
            float3 extents = bounds.extents;
            float3 center = bounds.center;

            float projectedCenter = math.dot(center, normal);
            float projectedExtents =
                math.abs(extents.x * normal.x) +
                math.abs(extents.y * normal.y) +
                math.abs(extents.z * normal.z);

            if (projectedCenter + projectedExtents < -distance)
                return false;
        }
        return true;
    }

    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
    private FastAABB TransformBounds(Bounds localBounds, Matrix4x4 matrix)
    {
        float3 extents = localBounds.extents;

        float3 axisX = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
        float3 axisY = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
        float3 axisZ = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));

        float3 worldExtents = new float3(
            math.abs(axisX.x) + math.abs(axisY.x) + math.abs(axisZ.x),
            math.abs(axisX.y) + math.abs(axisY.y) + math.abs(axisZ.y),
            math.abs(axisX.z) + math.abs(axisY.z) + math.abs(axisZ.z)
        );

        return new FastAABB(matrix.MultiplyPoint3x4(localBounds.center), worldExtents * 2f);
    }
}