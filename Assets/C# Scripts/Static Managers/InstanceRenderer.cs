using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;


[BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
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

        UpdateScheduler.Register(OnUpdate);
    }


    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
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

        visibleMeshMatrices = new NativeList<Matrix4x4>(perMeshArraySize, Allocator.Persistent);

        frustumPlanes = new NativeArray<FastFrustumPlane>(6, Allocator.Persistent);

        cam = Camera.main;
        lastCamPos = cam.transform.position;
        lastCamRot = cam.transform.rotation;

        fillArraysJobHandle.Complete();
    }




    private Mesh[] meshes;

    private int meshCount;
    private int perMeshArraySize;

    private RenderParams renderParams;

    [Tooltip("Array consisting of multiple arrays, 1 for every mesh accesed by meshIndex multiplied by perMeshArraySize")]
    private NativeArray<Matrix4x4> matrices;

    [Tooltip("CellId to MatrixId")]
    private NativeArray<int> matrixKeys;

    [Tooltip("MatrixId to CellId")]
    private NativeArray<int> cellIdKeys;

    [Tooltip("Number of instances for each mesh")]
    private NativeArray<int> matrixCounts;


    [Tooltip("List that holds calculated matrices that are in camera frustum")]
    private NativeList<Matrix4x4> visibleMeshMatrices;

    private Camera cam;
    private Vector3 lastCamPos;
    private Quaternion lastCamRot;

    private NativeArray<FastFrustumPlane> frustumPlanes;


    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
    private void OnUpdate()
    {
        Matrix4x4 currentMatrix = cam.transform.localToWorldMatrix;

        //only if camera has moved or rotated, recalculate frustum planes
        if (cam.transform.position != lastCamPos || cam.transform.rotation != lastCamRot)
        {
            lastCamPos = cam.transform.position;
            lastCamRot = cam.transform.rotation;

            Plane[] newplanes = GeometryUtility.CalculateFrustumPlanes(cam);
            for (int i = 0; i < 6; i++)
            {
                frustumPlanes[i] = new FastFrustumPlane(newplanes[i].normal, newplanes[i].distance);
            }
        }

        for (int meshIndex = 0; meshIndex < meshCount; meshIndex++)
        {
            int meshInstanceCount = matrixCounts[meshIndex];

            //skip currentmesh if there are 0 instances of it (nothing to render)
            if (meshInstanceCount == 0)
            {
                continue;
            }


            //reset visibleMeshMatrices List to allow filling it with new data
            visibleMeshMatrices.Clear();

            //Frustom Culling job
            FrustumCullingJobParallel frustomCullingJob = new FrustumCullingJobParallel
            {
                meshBounds = meshes[meshIndex].bounds,
                frustumPlanes = frustumPlanes,

                matrices = matrices,
                startIndex = meshIndex * perMeshArraySize,

                visibleMatrices = visibleMeshMatrices.AsParallelWriter()
            };

            frustomCullingJob.Schedule(meshInstanceCount, 1024).Complete();

            //if no mesh instances are visible, skip rendering
            if (visibleMeshMatrices.Length == 0)
            {
                continue;
            }

            //render the instances of currentmesh
            Graphics.RenderMeshInstanced(renderParams, meshes[meshIndex], 0, visibleMeshMatrices.AsArray());
        }

#if UNITY_EDITOR
        //if (perMeshArraySize > 100)
        //{
        //    Debug.LogWarning("Attempted to display DEBUG data for too large arrays, please lower the gridSize or disable the debug array display");
        //    return;
        //}

        //DEBUG_matrices = matrices.ToArray();
        //DEBUG_matrixKeys = matrixKeys.ToArray();
        //DEBUG_cellIdKeys = cellIdKeys.ToArray();
        //DEBUG_matrixCounts = matrixCounts.ToArray();
#endif
    }





    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
    public void SetMeshInstanceMatrix(int meshIndex, int cellId, Matrix4x4 matrix)
    {
        //if matrixKeys[cellId] == -1, there is no mesh for that cell, so assign a new matrix 
        if (matrixKeys[cellId] == -1)
        {
            int matrixArrayIndex = meshIndex * perMeshArraySize + matrixCounts[meshIndex];

            //save matrix to nest spot in matrixArray
            matrices[matrixArrayIndex] = matrix;

            //save cellId to matrixArray in the same index
            cellIdKeys[matrixArrayIndex] = cellId;

            //save matrixArray index to cellId in matrixKeys
            matrixKeys[cellId] = matrixArrayIndex;

            //increment matrixCount for this mesh by 1
            matrixCounts[meshIndex] += 1;
        }
        //if matrixKeys[cellId] has a value, modify the equivelenat matrix instead of asigning a new one
        else
        {
            matrices[matrixKeys[cellId]] = matrix;
        }
    }

    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
    public void RemoveMeshInstanceMatrix(int toRemoveCellId)
    {
        int toRemoveMatrixId = matrixKeys[toRemoveCellId];
        int meshIndex = toRemoveMatrixId / perMeshArraySize;

        int lastMatrixId = meshIndex * perMeshArraySize + matrixCounts[meshIndex] - 1;
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
        matrixCounts[meshIndex] -= 1;
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
        visibleMeshMatrices.Dispose();
        frustumPlanes.Dispose();

        UpdateScheduler.Unregister(OnUpdate);
    }




#if UNITY_EDITOR
    [Header ("Array consisting of multiple arrays, 1 for every mesh accesed by meshIndex multiplied by perMeshArraySize")]
    [SerializeField] private Matrix4x4[] DEBUG_matrices;

    [Header("CellId to MatrixId")]
    [SerializeField] private int[] DEBUG_matrixKeys;

    [Header("MatrixId to CellId")]
    [SerializeField] private int[] DEBUG_cellIdKeys;

    [Header("Number of instances for each mesh")]
    [SerializeField] private int[] DEBUG_matrixCounts;
#endif
}
