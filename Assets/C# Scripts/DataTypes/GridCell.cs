using Unity.Burst;
using UnityEngine;



[System.Serializable]
[BurstCompile]
public struct GridCell
{
    public CellType type;
    public CellState state;
    public CellOrientation orientation;

    public readonly bool IsEmpty => type == CellType.Empty;

    public int gridId;


    public GridCell(int _gridId)
    {
        gridId = _gridId;

        type = CellType.Empty;
        state = CellState.Default;
        orientation = CellOrientation.Left;
    }

    public static GridCell Empty => new GridCell(-1);
}