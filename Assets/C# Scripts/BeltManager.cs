
using Unity.Burst;
using Unity.Collections;
using UnityEngine;


[BurstCompile]
public class BeltManager : MonoBehaviour
{   
    public static BeltManager Instance { get; private set; }
    private void Awake()
    {
        Instance = this;
    }
}
