using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;


[BurstCompile]
public class GridManager : MonoBehaviour
{
    public int3 gridSize;

    private NativeHashMap<int, GridCell> grid;

    private float3 gridToCenterOffset;



    [BurstCompile]
    private void Start()
    {
        SetupGrid();
    }

    [BurstCompile]
    private void SetupGrid()
    {
        gridToCenterOffset = new float3(gridSize.x * 0.5f + 0.5f, 0f, gridSize.z * 0.5f + 0.5f);

        int gridLength = gridSize.x * gridSize.y * gridSize.z;

        grid = new NativeHashMap<int, GridCell>(gridLength, Allocator.Persistent);


        NativeReference<int3> gridSizeRef = new NativeReference<int3>(Allocator.TempJob);
        gridSizeRef.Value = gridSize;

        SetupGridJobParallel setupGridJob = new SetupGridJobParallel()
        {
            grid = grid,
            gridSize = gridSizeRef,
        };

        JobHandle jobHandle = setupGridJob.Schedule(gridLength, 1024);
        jobHandle.Complete();

        gridSizeRef.Dispose();
    }




    private void Update()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            GridCell gridCell = MouseWorldToGridCell(hit.point);
            Debug.Log($"Grid Cell: {gridCell.gridId}");
        }
    }




    #region Helper Methods

    [BurstCompile]
    private GridCell MouseWorldToGridCell(Vector3 worldPos)
    {
        int gridId = GridPosToGridId((int3)math.floor((float3)worldPos + gridToCenterOffset));

        if (grid.TryGetValue(gridId, out GridCell cell))
        {
            return cell;
        }

        return GridCell.Empty;
    }

    [BurstCompile]
    private int3 GridIdToGridPos(int gridId)
    {
        int z = gridId / (gridSize.x * gridSize.y);
        int y = (gridId % (gridSize.x * gridSize.y)) / gridSize.x;
        int x = gridId % gridSize.x;
        return new int3(x, y, z);
    }

    [BurstCompile]
    private int GridPosToGridId(int3 gridPos)
    {
        return gridPos.x + gridPos.y * gridSize.x + gridPos.z * gridSize.x * gridSize.y;
    }

    #endregion



#if UNITY_EDITOR

    [SerializeField] private bool drawGrid;

    [BurstCompile]
    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(new Vector3(0, (float)(gridSize.y * 0.5f) - 0.5f, 0), new Vector3(gridSize.x, gridSize.y, gridSize.z));

        if (drawGrid && Application.isPlaying)
        {
            float halfXOffset = gridSize.x * 0.5f - 0.5f;
            float halfZOffset = gridSize.z * 0.5f - 0.5f;

            foreach (var item in grid)
            {
                int3 gridPos = GridIdToGridPos(item.Key);

                Gizmos.DrawWireCube(new Vector3(gridPos.x - halfXOffset, gridPos.y, gridPos.z - halfZOffset), Vector3.one);

                Gizmos.DrawCube(new Vector3(gridPos.x - halfXOffset, gridPos.y, gridPos.z - halfZOffset), Vector3.one * 0.65f);
            }
        }
    }
#endif
}
