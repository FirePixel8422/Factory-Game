using Unity.Collections;



[System.Serializable]
public struct MeshDrawData
{
    //last id is the total added matrixCount
    public NativeArray<int> matrixIds;
    private readonly int matrixCountId;

    public readonly int MatrixCount => matrixIds[matrixCountId];


    public MeshDrawData(int _matrixCount)
    {
        matrixIds = new NativeArray<int>(_matrixCount + 1, Allocator.Persistent);
        matrixCountId = _matrixCount;
    }

    public void AddMatrixId(int matrixId)
    {
        int cMatrixId = matrixIds[matrixCountId]++;

        matrixIds[cMatrixId] = matrixId;
    }
}