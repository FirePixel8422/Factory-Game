using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;


[BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
public class GridManager : MonoBehaviour
{
    [SerializeField] private InstanceRenderer instanceRenderer;

    [SerializeField] private GameObject[] meshObjs;
    [SerializeField] private Material mat;

    public int3 gridSize;

    private NativeArray<GridCell> grid;

    private CellOrientation beltLineOrientation;

    private int selectedGridCellGridId = -1;
    private Vector3 oldMousePos;



    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
    private void Start()
    {
        int gridLength = gridSize.x * gridSize.y * gridSize.z;

        SetupInstanceRenderer(gridLength);

        SetupGrid(gridLength);
    }


    #region Setup Methods

    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
    private void SetupInstanceRenderer(int gridLength)
    {
        int meshCount = meshObjs.Length;

        Mesh[] meshes = new Mesh[meshCount];
        for (int i = 0; i < meshCount; i++)
        {
            meshes[i] = MeshCombiner.CombineMeshes(meshObjs[i]);
        }

        instanceRenderer = new InstanceRenderer(meshes, meshCount, mat, gridLength);
    }

    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
    private void SetupGrid(int gridLength)
    {
        grid = new NativeArray<GridCell>(gridLength, Allocator.Persistent);


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

    #endregion




    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
    private void Update()
    {
        if (Input.GetMouseButton(0))
        {
            OnLeftClick(Input.mousePosition);
        }
        if (Input.GetMouseButton(1))
        {
            OnRightClick(Input.mousePosition);
        }

        if (Input.GetMouseButtonUp(0))
        {
            selectedGridCellGridId = -1;
        }
    }


    #region Player Input

    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
    private void OnLeftClick(Vector3 newMousPos)
    {
        //if mouse hasnt moved, return
        if (newMousPos == oldMousePos) return;

        //save mousePos
        oldMousePos = newMousPos;

        //shoot ray to world from mouse
        Ray ray = Camera.main.ScreenPointToRay(newMousPos);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            //try get grid cell from hit world point
            if (TryGetGridCellFromWorldPoint(hit.point, gridSize, out GridCell newCell))
            {
                //if the cell is different than the currentSelected cell
                if (newCell.IsEmpty && newCell.gridId != selectedGridCellGridId)
                {
                    //if a new cell is selected
                    if (selectedGridCellGridId != -1)
                    {
                        //get modify and save oldcell
                        GridCell oldCell = grid[selectedGridCellGridId];

                        beltLineOrientation = GetCellOrientation(oldCell, newCell);
                        oldCell.orientation = beltLineOrientation;

                        SetMesh(0, selectedGridCellGridId, GridIdToWorldPos(oldCell.gridId), oldCell.orientation);

                        grid[selectedGridCellGridId] = oldCell;
                    }

                    //modify and save new cell
                    newCell.type = CellType.Conveyor;
                    newCell.orientation = beltLineOrientation;

                    selectedGridCellGridId = newCell.gridId;

                    grid[selectedGridCellGridId] = newCell;

                    SetMesh(0, selectedGridCellGridId, GridIdToWorldPos(selectedGridCellGridId), newCell.orientation);
                }
            }
        }
    }


    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
    private void OnRightClick(Vector3 newMousPos)
    {
        //if mouse hasnt moved, return
        if (newMousPos == oldMousePos) return;

        //save mousePos
        oldMousePos = newMousPos;

        //shoot ray to world from mouse
        Ray ray = Camera.main.ScreenPointToRay(newMousPos);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            //try get grid cell from hit world point
            if (TryGetGridCellFromWorldPoint(hit.point, gridSize, out GridCell newCell))
            {
                //if the cell is different than the currentSelected cell
                if (newCell.IsEmpty == false)
                {
                    //modify and save new cell
                    newCell.type = CellType.Empty;

                    grid[newCell.gridId] = newCell;

                    selectedGridCellGridId = -1;

                    RemoveMesh(0, newCell.gridId);
                }
            }
        }
    }

    #endregion

    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
    public void SetMesh(int meshId, int cellId, float3 pos, CellOrientation orientation)
    {
        Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.Euler(0, (int)orientation * 90, 0), Vector3.one);
        instanceRenderer.SetMeshInstanceMatrix(meshId, cellId, matrix);
    }

    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
    public void RemoveMesh(int meshId, int cellId)
    {
        instanceRenderer.RemoveMeshInstanceMatrix(meshId, cellId);
    }




    #region Helper Methods

    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
    public bool TryGetGridCellFromWorldPoint(float3 worldPosition, int3 gridSize, out GridCell gridCell)
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

        gridCell = grid[GridPosToGridId(new int3(x, 0, z), gridSize)];
        return true;
    }


    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
    /// <summary>
    /// Convert int gridId to int3 gridPos
    /// </summary>
    private int3 GridIdToGridPos(int gridId, int3 gridSize)
    {
        int z = gridId / (gridSize.x * gridSize.y);
        int y = (gridId % (gridSize.x * gridSize.y)) / gridSize.x;
        int x = gridId % gridSize.x;
        return new int3(x, y, z);
    }

    /// <summary>
    /// Convert int3 gridPos to int gridId
    /// </summary>
    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
    private int GridPosToGridId(int3 gridPos, int3 gridSize)
    {
        return gridPos.x + gridPos.y * gridSize.x + gridPos.z * gridSize.x * gridSize.y;
    }


    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
    public float3 GridIdToWorldPos(int gridId)
    {
        int3 gridPos = GridIdToGridPos(gridId, gridSize);
        float3 worldPos = new float3(
            gridPos.x - gridSize.x * 0.5f + 0.5f,
            gridPos.y,
            gridPos.z - gridSize.z * 0.5f + 0.5f
        );
        return worldPos;
    }

    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
    public CellOrientation GetCellOrientation(GridCell cellA, GridCell cellB)
    {
        int3 posA = GridIdToGridPos(cellA.gridId, gridSize);
        int3 posB = GridIdToGridPos(cellB.gridId, gridSize);

        int3 direction = posA - posB;

        if (direction.x == 1) return CellOrientation.Left;
        if (direction.x == -1) return CellOrientation.Right;
        if (direction.z == 1) return CellOrientation.Down;
        if (direction.z == -1) return CellOrientation.Up;

        // Default case, should not happen if cells are adjacent
        return CellOrientation.Up;
    }

    #endregion



    private void OnDestroy()
    {
        if (grid.IsCreated)
        {
            grid.Dispose();
        }

        instanceRenderer?.Dispose();
    }



#if UNITY_EDITOR

    [SerializeField] private bool drawGrid;

    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(new Vector3(0, (float)(gridSize.y * 0.5f) - 0.5f, 0), new Vector3(gridSize.x, gridSize.y, gridSize.z));

        if (drawGrid && Application.isPlaying)
        {
            float halfXOffset = gridSize.x * 0.5f - 0.5f;
            float halfZOffset = gridSize.z * 0.5f - 0.5f;

            for (int i = 0; i < grid.Length; i++)
            {
                int3 gridPos = GridIdToGridPos(i, gridSize);

                if (grid[i].state == CellState.Selected)
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
