using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;



[BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
public struct SetupGridJobParallel : IJobParallelFor
{
    [NativeDisableParallelForRestriction]
    [WriteOnly][NoAlias] public NativeArray<GridCell> grid;

    [ReadOnly][NoAlias] public NativeReference<int3> gridSize;


    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
    public void Execute(int index)
    {
        GridCell gridObject = new GridCell(index);

        grid[index] = gridObject;
    }
}