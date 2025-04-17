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

            worldBounds = new Bounds(Vector3.zero, Vector3.one * 100),
        };

        SetupMatrixData();

        UpdateScheduler.Register(OnUpdate);
    }


    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
    private void SetupMatrixData()
    {
        int totalArraySize = perMeshArraySize * meshCount;

        matrices = new NativeArray<Matrix4x4>(totalArraySize, Allocator.Persistent);

        matrixKeys = new NativeArray<int>(totalArraySize, Allocator.Persistent);
        cellIdKeys = new NativeArray<int>(totalArraySize, Allocator.Persistent);

        matrixCounts = new NativeArray<int>(meshCount, Allocator.Persistent);

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

        JobHandle jobHandle = JobHandle.CombineDependencies(
            fillCellIdKeys.Schedule(totalArraySize, 1024),
            fillMatrixKeys.Schedule(totalArraySize, 1024)
            );

        jobHandle.Complete();
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


    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
    private void OnUpdate()
    {
        for (int meshIndex = 0; meshIndex < meshCount; meshIndex++)
        {
            int meshInstanceCount = matrixCounts[meshIndex];

            //skip currentmesh if there are 0 instances of it (nothing to render)
            if (meshInstanceCount == 0)
            {
                continue;
            }

            //render the instances of currentmesh
            Graphics.RenderMeshInstanced(renderParams, meshes[0], 0, matrices, matrixCounts[meshIndex], meshIndex * perMeshArraySize);
        }

        DEBUG_matrices = matrices.ToArray();
        DEBUG_matrixKeys = matrixKeys.ToArray();
        DEBUG_cellIdKeys = cellIdKeys.ToArray();
        DEBUG_matrixCounts = matrixCounts.ToArray();
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
    public void RemoveMeshInstanceMatrix(int meshIndex, int toRemoveCellId)
    {
        int toRemoveMatrixId = matrixKeys[toRemoveCellId];

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

    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
    private void RemoveMeshInstadnceMatrix(int meshIndex, int cellId)
    {
        // Retrieve the index of the matrix to be removed
        int toRemoveMatrixId = matrixKeys[cellId];

        // Calculate the index of the last matrix in the array for this mesh
        int lastMatrixId = meshIndex * perMeshArraySize + matrixCounts[meshIndex] - 1;

        // Retrieve the cellId of the last matrix to ensure proper mapping after the swap
        int lastCellId = cellIdKeys[lastMatrixId];

        // Swap the matrix at toRemoveMatrixId with the matrix at lastMatrixId
        matrices[toRemoveMatrixId] = matrices[lastMatrixId];

        // Swap the corresponding cellId in the cellIdKeys array
        cellIdKeys[toRemoveMatrixId] = lastCellId;

        // Update matrixKeys to reflect that the last matrix is now in the position of the removed one
        matrixKeys[lastCellId] = toRemoveMatrixId;

        // Mark the removed matrix as empty in matrixKeys (the original cellId's index)
        matrixKeys[cellId] = -1;

        // Decrease the count of matrices for this mesh, as one has been removed
        matrixCounts[meshIndex] -= 1;
    }



    /// <summary>
    /// Dispose all native memory allocated and unregister from the update scheduler.
    /// </summary>
    public void Dispose()
    {
        UpdateScheduler.Unregister(OnUpdate);

        matrices.Dispose();
        matrixKeys.Dispose();
        cellIdKeys.Dispose();
        matrixCounts.Dispose();
    }











    [Header ("Array consisting of multiple arrays, 1 for every mesh accesed by meshIndex multiplied by perMeshArraySize")]
    [SerializeField] private Matrix4x4[] DEBUG_matrices;

    [Header("CellId to MatrixId")]
    [SerializeField] private int[] DEBUG_matrixKeys;

    [Header("MatrixId to CellId")]
    [SerializeField] private int[] DEBUG_cellIdKeys;

    [Header("Number of instances for each mesh")]
    [SerializeField] private int[] DEBUG_matrixCounts;
}
