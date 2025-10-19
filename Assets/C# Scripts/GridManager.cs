using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;


public class GridManager : MonoBehaviour
{
    [SerializeField] private InstanceRenderer instanceRenderer;

    [SerializeField] private GameObject[] meshObjs;
    [SerializeField] private Material mat;

    [SerializeField] private int meshId;

    [Range(1, 25)]
    [SerializeField] private int brushSize = 5;

    public int3 gridSize;

    private NativeArray<GridCell> grid;

    private CellOrientation beltLineOrientation;

    private int selectedGridCellGridId = -1;
    private Vector3 oldMousePos;



    private void Start()
    {
        int gridLength = gridSize.x * gridSize.y * gridSize.z;

        SetupInstanceRenderer(gridLength);

        SetupGrid(gridLength);

        UpdateScheduler.RegisterUpdate(OnUpdate);
    }


    #region Setup Methods

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



    private void OnUpdate()
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

    private void OnLeftClick(Vector3 newMousePos)
    {
        if (newMousePos == oldMousePos) return;
        oldMousePos = newMousePos;

        Ray ray = Camera.main.ScreenPointToRay(newMousePos);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (TryGetGridCellFromWorldPoint(hit.point, gridSize, out GridCell centerCell))
            {
                int3 centerPos = GridIdToGridPos(centerCell.gridId, gridSize);
                int halfBrush = brushSize / 2;

                for (int z = -halfBrush; z <= halfBrush; z++)
                {
                    for (int x = -halfBrush; x <= halfBrush; x++)
                    {
                        int3 gridPos = centerPos + new int3(x, 0, z);
                        if (!IsWithinGridBounds(gridPos)) continue;

                        int gridId = GridPosToGridId(gridPos, gridSize);
                        GridCell cell = grid[gridId];

                        if (cell.IsEmpty && gridId != selectedGridCellGridId)
                        {
                            if (selectedGridCellGridId != -1)
                            {
                                GridCell oldCell = grid[selectedGridCellGridId];
                                beltLineOrientation = GetCellOrientation(oldCell, cell);
                                oldCell.orientation = beltLineOrientation;

                                SetMesh(meshId, selectedGridCellGridId, GridIdToWorldPos(oldCell.gridId), oldCell.orientation);
                                grid[selectedGridCellGridId] = oldCell;
                            }

                            cell.type = CellType.Conveyor;
                            cell.orientation = beltLineOrientation;

                            selectedGridCellGridId = gridId;
                            grid[gridId] = cell;

                            SetMesh(meshId, gridId, GridIdToWorldPos(gridId), cell.orientation);
                        }
                    }
                }
            }
        }
    }


    private void OnRightClick(Vector3 newMousePos)
    {
        if (newMousePos == oldMousePos) return;
        oldMousePos = newMousePos;

        Ray ray = Camera.main.ScreenPointToRay(newMousePos);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (TryGetGridCellFromWorldPoint(hit.point, gridSize, out GridCell centerCell))
            {
                int3 centerPos = GridIdToGridPos(centerCell.gridId, gridSize);
                int halfBrush = brushSize / 2;

                for (int z = -halfBrush; z <= halfBrush; z++)
                {
                    for (int x = -halfBrush; x <= halfBrush; x++)
                    {
                        int3 gridPos = centerPos + new int3(x, 0, z);
                        if (!IsWithinGridBounds(gridPos)) continue;

                        int gridId = GridPosToGridId(gridPos, gridSize);
                        GridCell cell = grid[gridId];

                        if (!cell.IsEmpty)
                        {
                            grid[gridId] = new GridCell(gridId);
                            RemoveMesh(gridId);
                        }
                    }
                }

                selectedGridCellGridId = -1;
            }
        }
    }


    #endregion



    #region Helper Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    /// <summary>
    /// Convert int3 gridPos to int gridId
    /// </summary>
    private int GridPosToGridId(int3 gridPos, int3 gridSize)
    {
        return gridPos.x + gridPos.y * gridSize.x + gridPos.z * gridSize.x * gridSize.y;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsWithinGridBounds(int3 gridPos)
    {
        return gridPos.x >= 0 && gridPos.x < gridSize.x &&
               gridPos.y >= 0 && gridPos.y < gridSize.y &&
               gridPos.z >= 0 && gridPos.z < gridSize.z;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetMesh(int meshId, int cellId, float3 pos, CellOrientation orientation)
    {
        Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.Euler(0, (int)orientation * 90, 0), Vector3.one);
        instanceRenderer.SetMeshInstanceMatrix(meshId, cellId, matrix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RemoveMesh(int cellId)
    {
        instanceRenderer.RemoveMeshInstanceMatrix(cellId);
    }


    private void OnDestroy()
    {
        if (grid.IsCreated)
        {
            grid.Dispose();
        }

        instanceRenderer?.Dispose();

        UpdateScheduler.UnregisterUpdate(OnUpdate);
    }



#if UNITY_EDITOR

    [SerializeField] private bool drawGrid;

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(new Vector3(0, (float)(gridSize.y * 0.5f) - 0.5f, 0), new Vector3(gridSize.x, gridSize.y, gridSize.z));

        if (drawGrid && Application.isPlaying)
        {
            if (grid.Length > 10000)
            {
                Debug.LogWarning("You attempted to display way too many Gizmos, please lower the gridSize or disable the drawGrid bool");
                return;
            }

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
