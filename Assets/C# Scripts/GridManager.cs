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

    [SerializeField] private int lastSelectedGridCellGridId = -10;
    [SerializeField] private Transform cube;



    [BurstCompile]
    private void Start()
    {
        SetupGrid();
    }


    [BurstCompile]
    private void SetupGrid()
    {
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




    [BurstCompile]
    private void Update()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (GridCellFromWorldPoint(hit.point, out GridCell gridCell))
            {
                cube.position = hit.point;

                if (gridCell.gridId != lastSelectedGridCellGridId)
                {
                    lastSelectedGridCellGridId = gridCell.gridId;

                    gridCell.state = CellState.Selected;
                    grid[gridCell.gridId] = gridCell;
                }
            }
        }
    }




    #region Helper Methods

    [BurstCompile]
    public bool GridCellFromWorldPoint(float3 worldPosition, out GridCell gridCell)
    {
        float percentX = (worldPosition.x + gridSize.x * 0.5F) / gridSize.x;
        float percentZ = (worldPosition.z + gridSize.z * 0.5F) / gridSize.z;

        if (percentX < 0 || percentX > 1 || percentZ < 0 || percentZ > 1)
        {
            gridCell = GridCell.Uninitialized;
            return false;
        }

        int x = (int)math.round((gridSize.x - 1) * percentX);
        int z = (int)math.round((gridSize.z - 1) * percentZ);

        gridCell = grid[GridPosToGridId(new int3(x, 0, z))];
        return true;
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

                if(lastSelectedGridCellGridId == item.Key)
                {
                    Gizmos.color = Color.red;
                }
                else
                {
                    Gizmos.color = Color.white;
                }

                Gizmos.DrawWireCube(new Vector3(gridPos.x - halfXOffset, gridPos.y, gridPos.z - halfZOffset), Vector3.one);

                Gizmos.DrawCube(new Vector3(gridPos.x - halfXOffset, gridPos.y, gridPos.z - halfZOffset), Vector3.one * 0.65f);
            }
        }
    }
#endif
}
