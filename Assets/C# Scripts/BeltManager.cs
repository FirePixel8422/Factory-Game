using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;


[BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
public class BeltManager : MonoBehaviour
{
    public static BeltManager Instance { get; private set; }
    private void Awake()
    {
        Instance = this;
    }


    public GameObject beltPrefab;


    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
    public void SpawnBelt(float3 pos, CellOrientation orientation)
    {
        Instantiate(beltPrefab, pos, Quaternion.Euler(0, (int)orientation * 90, 0));
    }
}
