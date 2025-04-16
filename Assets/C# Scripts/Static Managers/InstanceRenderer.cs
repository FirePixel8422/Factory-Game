using Unity.Burst;
using Unity.Collections;
using UnityEngine;


[BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
public class InstanceRenderer
{
    private Mesh[] meshes;

    //stores material
    private RenderParams renderParams;

    private NativeArray<MeshDrawData> meshData;

    private NativeArray<Matrix4x4> matrixData;
    private int matrixId;

    private NativeArray<Matrix4x4> meshMatrices;

    public void AddMeshData(int meshId, Matrix4x4 matrix)
    {
        matrixData[matrixId] = matrix;

        meshData[meshId].AddMatrixId(matrixId);

        matrixId += 1;
    }



    public InstanceRenderer(Material mat)
    {
        renderParams = new RenderParams()
        {
            material = mat,

            shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
            receiveShadows = true,
            lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off,

            motionVectorMode = MotionVectorGenerationMode.ForceNoMotion,

            worldBounds = new Bounds(Vector3.zero, Vector3.one * 100),
        };

        UpdateScheduler.Register(OnUpdate);
    }
    ~InstanceRenderer()
    {
        UpdateScheduler.Unregister(OnUpdate);
    }


    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
    private void OnUpdate()
    {
        for (int meshIndex = 0; meshIndex < meshData.Length; meshIndex++)
        {
            MeshDrawData cMeshDrawData = meshData[meshIndex];

            int matrixCount = cMeshDrawData.MatrixCount;

            if (matrixCount == 0)
            {
                continue;
            }

            // Copy the relevant matrices into the temporary array
            for (int i2 = 0; i2 < matrixCount; i2++)
            {
                meshMatrices[i2] = matrixData[cMeshDrawData.matrixIds[i2]];
            }

            // Render the instances
            Graphics.RenderMeshInstanced(default, meshes[meshIndex], 0, meshMatrices, matrixCount);
        }
    }
}
