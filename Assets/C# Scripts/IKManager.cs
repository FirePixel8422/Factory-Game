using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
public class IKManager : MonoBehaviour
{
    [SerializeField] private Joint rootJoint;
    [SerializeField] private Joint endJoint;

    [SerializeField] private Transform target;

    [SerializeField] private float threshold = 0.05f;
    [SerializeField] private float rotSpeed = 15;
    [SerializeField] private int stepsPerCycle = 3;



    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
    private void Start()
    {
        UpdateScheduler.Register(OnUpdate);
    }

    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
    public void OnUpdate()
    {
        Joint cJoint;
        float slope;

        for (int i = 0; i < stepsPerCycle; i++)
        {
            if (GetDistance(endJoint.transform.position, target.position) > threshold)
            {
                cJoint = rootJoint;

                while (cJoint != null)
                {
                    slope = CalculateSlope(cJoint);
                    cJoint.Rotate(-slope * rotSpeed);

                    cJoint = cJoint.childJoint;
                }
            }
        }
    }


    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
    private float CalculateSlope(Joint targetJoint)
    {
        float deltaTheta = 0.1f;
        float dist1 = GetDistance(endJoint.transform.position, target.position);

        targetJoint.Rotate(deltaTheta);

        float dist2 = GetDistance(endJoint.transform.position, target.position);

        targetJoint.Rotate(-deltaTheta);

        return (dist2 - dist1) / deltaTheta;
    }

    [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
    private float GetDistance(float3 posA, float3 posB)
    {
        return Vector3.Distance(posA, posB);
    }



    private void OnDestroy()
    {
        UpdateScheduler.Unregister(OnUpdate);
    }
}
