using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;



[BurstCompile]
public struct SetupGridJobParallel : IJobParallelFor
{
    [NativeDisableParallelForRestriction]
    [WriteOnly][NoAlias] public NativeHashMap<int, GridCell> grid;

    [ReadOnly][NoAlias] public NativeReference<int3> gridSize;


    [BurstCompile]
    public void Execute(int index)
    {
        GridCell gridObject = new GridCell(index);

        grid.Add(index, gridObject);
    }
}