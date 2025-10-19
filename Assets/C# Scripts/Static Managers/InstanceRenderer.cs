using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;


[System.Serializable]
public class InstanceRenderer
{
    public InstanceRenderer(Mesh[] _meshes, int _meshCount, Material mat, int _perMeshArraySize)
    {
        meshes = _meshes;

        meshCount = _meshCount;
        perMeshArraySize = _perMeshArraySize;

        renderParams = new RenderParams()
        {
            material = mat,

            shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
            receiveShadows = true,

            motionVectorMode = MotionVectorGenerationMode.ForceNoMotion,
        };

        SetupMatrixData();

        UpdateScheduler.RegisterUpdate(OnUpdate);
    }

    
    private void SetupMatrixData()
    {
        int totalArraySize = perMeshArraySize * meshCount;

        matrixKeys = new NativeArray<int>(totalArraySize, Allocator.Persistent);
        cellIdKeys = new NativeArray<int>(totalArraySize, Allocator.Persistent);

        IntArrayFillJobParallel fillMatrixKeys = new IntArrayFillJobParallel()
        {
            array = matrixKeys,
            value = -1,
        };

        IntArrayFillJobParallel fillCellIdKeys = new IntArrayFillJobParallel()
        {
            array = cellIdKeys,
            value = -1,
        };

        JobHandle fillArraysJobHandle = JobHandle.CombineDependencies(
            fillCellIdKeys.Schedule(totalArraySize, 1024),
            fillMatrixKeys.Schedule(totalArraySize, 1024)
            );

        matrices = new NativeArray<Matrix4x4>(totalArraySize, Allocator.Persistent);
        matrixCounts = new NativeArray<int>(meshCount, Allocator.Persistent);

        culledInstanceMatrices = new NativeList<Matrix4x4>(perMeshArraySize, Allocator.Persistent);

        frustumPlanes = new NativeArray<FastFrustumPlane>(6, Allocator.Persistent);

        cam = Camera.main;
        lastCamPos = cam.transform.position;
        lastCamRot = cam.transform.rotation;

        fillArraysJobHandle.Complete();
    }




    private readonly Mesh[] meshes;

    private readonly int meshCount;
    private readonly int perMeshArraySize;

    private RenderParams renderParams;

    [Tooltip("Flattened array that acts as multiple arrays, 1 for every mesh accesed by meshId multiplied by perMeshArraySize")]
    private NativeArray<Matrix4x4> matrices;

    [Tooltip("CellId to MatrixId")]
    private NativeArray<int> matrixKeys;

    [Tooltip("MatrixId to CellId")]
    private NativeArray<int> cellIdKeys;

    [Tooltip("Number of instances for each mesh")]
    private NativeArray<int> matrixCounts;


    [Tooltip("List that holds calculated matrices that are in camera frustum for target mesh instance")]
    private NativeList<Matrix4x4> culledInstanceMatrices;

    private Camera cam;
    private Vector3 lastCamPos;
    private Quaternion lastCamRot;

    private NativeArray<FastFrustumPlane> frustumPlanes;


    private void OnUpdate()
    {
        bool camMoved = cam.transform.position != lastCamPos || cam.transform.rotation != lastCamRot;

        //only if camera has moved or rotated, recalculate frustum planes
        if (camMoved)
        {
            lastCamPos = cam.transform.position;
            lastCamRot = cam.transform.rotation;

            Plane[] newplanes = GeometryUtility.CalculateFrustumPlanes(cam);
            for (int i = 0; i < 6; i++)
            {
                frustumPlanes[i] = new FastFrustumPlane(newplanes[i].normal, newplanes[i].distance);
            }
        }


        for (int meshId = 0; meshId < meshCount; meshId++)
        {
            int meshInstanceCount = matrixCounts[meshId];

            //skip currentmesh if there are 0 instances of it (nothing to render)
            if (meshInstanceCount == 0)
            {
                continue;
            }

            //Frustom Culling job
            FrustumCullingJobParallel frustomCullingJob = new FrustumCullingJobParallel
            {
                meshBounds = meshes[meshId].bounds,
                frustumPlanes = frustumPlanes,

                matrices = matrices,
                startIndex = meshId * perMeshArraySize,

                culledMatrices = culledInstanceMatrices.AsParallelWriter(),
            };

            frustomCullingJob.Schedule(meshInstanceCount, 1024).Complete();

            //if no mesh instances are visible, skip rendering that mesh
            if (culledInstanceMatrices.Length != 0)
            {
                RenderMeshInstance(meshId);

                //reset visibleMeshMatrices List to allow filling it with new data
                culledInstanceMatrices.Clear();
            }
        }

//#if UNITY_EDITOR
//        if (perMeshArraySize > 100)
//        {
//            Debug.LogWarning("Attempted to display DEBUG data for too large arrays, please lower the gridSize or disable the debug array display");
//            return;
//        }

//        DEBUG_matrices = matrices.ToArray();
//        DEBUG_matrixKeys = matrixKeys.ToArray();
//        DEBUG_cellIdKeys = cellIdKeys.ToArray();
//        DEBUG_matrixCounts = matrixCounts.ToArray();
//        DEBUG_visibleMeshMatrices = culledInstanceMatrices.AsArray().ToArray();
//#endif
    }


    /// <summary>
    /// Render all instances of meshId with matrixData from <see cref="culledMatrices"/> starting at startId and rendering instanceCount amount of instances.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RenderMeshInstance(int meshId)
    {
        //render the instances of currentmesh
        Graphics.RenderMeshInstanced(renderParams, meshes[meshId], 0, culledInstanceMatrices.AsArray());    
    }





    public void SetMeshInstanceMatrix(int meshId, int cellId, float4x4 matrix)
    {
        //if matrixKeys[cellId] == -1, there is no mesh for that cell, so assign a new matrix 
        if (matrixKeys[cellId] == -1)
        {
            int matrixArrayIndex = meshId * perMeshArraySize + matrixCounts[meshId];

            //save matrix to nest spot in matrixArray
            matrices[matrixArrayIndex] = matrix;

            //save cellId to matrixArray in the same index
            cellIdKeys[matrixArrayIndex] = cellId;

            //save matrixArray index to cellId in matrixKeys
            matrixKeys[cellId] = matrixArrayIndex;

            //increment matrixCount for this mesh by 1
            matrixCounts[meshId] += 1;
        }
        //if matrixKeys[cellId] has a value, modify the equivelenat matrix instead of asigning a new one
        else
        {
            matrices[matrixKeys[cellId]] = matrix;
        }
    }

    public void RemoveMeshInstanceMatrix(int toRemoveCellId)
    {
        int toRemoveMatrixId = matrixKeys[toRemoveCellId];
        int meshId = toRemoveMatrixId / perMeshArraySize;

        int lastMatrixId = meshId * perMeshArraySize + matrixCounts[meshId] - 1;
        int lastCellId = cellIdKeys[lastMatrixId];

        //swap last matrix with the one to be removed
        matrices[toRemoveMatrixId] = matrices[lastMatrixId];

        //swap last cellId with the one to be removed (get last cellId from lastMatrixId in cellIdKeys array)
        cellIdKeys[toRemoveMatrixId] = lastCellId;


        //swap last matrixKey with the one to be removed (get last cellId from lastMatrixId in cellIdKeys array)
        matrixKeys[lastCellId] = toRemoveMatrixId;

        //remove matrixKey for swapped from back matrix
        matrixKeys[toRemoveCellId] = -1;

        //update matrixCount for this mesh to reflect the removal
        matrixCounts[meshId] -= 1;
    }




    /// <summary>
    /// Dispose all native memory allocated and unregister from the update scheduler.
    /// </summary>
    public void Dispose()
    {
        matrices.Dispose();
        matrixKeys.Dispose();
        cellIdKeys.Dispose();
        matrixCounts.Dispose();
        culledInstanceMatrices.Dispose();
        frustumPlanes.Dispose();

        UpdateScheduler.UnregisterUpdate(OnUpdate);
    }




#if UNITY_EDITOR
    [Header ("Array consisting of multiple arrays, 1 for every mesh accesed by meshId multiplied by perMeshArraySize")]
    [SerializeField] private Matrix4x4[] DEBUG_matrices;

    [Header("Same as above, but only the matrices that are currently visible in camera frustum")]
    [SerializeField] private Matrix4x4[] DEBUG_visibleMeshMatrices;

    [Header("CellId to MatrixId")]
    [SerializeField] private int[] DEBUG_matrixKeys;

    [Header("MatrixId to CellId")]
    [SerializeField] private int[] DEBUG_cellIdKeys;

    [Header("Number of instances for each mesh")]
    [SerializeField] private int[] DEBUG_matrixCounts;
#endif
}
