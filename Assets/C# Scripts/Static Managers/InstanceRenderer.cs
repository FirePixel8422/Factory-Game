using System;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;


[BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
public class InstanceRenderer
{
    public InstanceRenderer(Material mat)
    {
        renderParams = new RenderParams()
        {
            material = mat,

            shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
            receiveShadows = true,

            motionVectorMode = MotionVectorGenerationMode.ForceNoMotion,

            worldBounds = new Bounds(Vector3.zero, Vector3.one * 100),
        };

        UpdateScheduler.Register(OnUpdate);
    }



    private Mesh[] meshes;

    //stores material
    private RenderParams renderParams;

    private NativeArray<Matrix4x4> meshInstanceMatrices;
    private NativeArray<int> meshInstanceMatrixCounts;

    private int meshCount;
    private int perMeshArraySize;




    public void Dispose()
    {
        UpdateScheduler.Unregister(OnUpdate);
    }


    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
    private void OnUpdate()
    {
        for (int meshIndex = 0; meshIndex < meshes.Length; meshIndex++)
        {
            // Render the instances
            Graphics.RenderMeshInstanced(renderParams, meshes[meshIndex], 0, meshInstanceMatrices, meshInstanceMatrixCounts[meshIndex], meshIndex * perMeshArraySize);
        }
    }
}
