using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RobotArm : MonoBehaviour
{
    [SerializeField] private Joint[] joints;

    [SerializeField] private Transform target;
    [SerializeField] private int iterationsPerFrame;
    [SerializeField] private float moveSpeed = 5f;

    private Vector3 currentIKTarget;

    private void Start()
    {
        joints = GetComponentsInChildren<Joint>();
        currentIKTarget = target.position;

        UpdateScheduler.RegisterUpdate(OnUpdate);
    }

    private void OnUpdate()
    {
        // Smoothly move the internal IK target toward the actual target
        currentIKTarget = Vector3.MoveTowards(currentIKTarget, target.position, moveSpeed * Time.deltaTime);

        // Solve IK toward the internal target
        SolveIKCCD(joints, joints[joints.Length - 1].transform, currentIKTarget, iterationsPerFrame, 0.01f);
    }

    public void SolveIKCCD(Joint[] joints, Transform endEffector, Vector3 targetPosition, int iterations = 10, float threshold = 0.01f)
    {
        for (int i = 0; i < iterations; i++)
        {
            for (int j = joints.Length - 1; j >= 0; j--)
            {
                Joint joint = joints[j];
                Transform t = joint.transform;

                if (joint.minAngle == 0f && joint.maxAngle == 0f)
                    continue;

                Vector3 toEnd = (endEffector.position - t.position).normalized;
                Vector3 toTarget = (targetPosition - t.position).normalized;

                Quaternion deltaRot = Quaternion.FromToRotation(toEnd, toTarget);
                Vector3 axisWorld = t.TransformDirection(joint.rotationAxis.normalized);

                deltaRot.ToAngleAxis(out float angle, out Vector3 rawAxis);
                float signedAngle = Vector3.Dot(rawAxis, axisWorld) * angle;

                float newAngle = Mathf.Clamp(joint.CurrentAngle + signedAngle, joint.minAngle, joint.maxAngle);
                float clampedDelta = newAngle - joint.CurrentAngle;

                float prevDistance = Vector3.Distance(endEffector.position, targetPosition);

                Quaternion rotation = Quaternion.AngleAxis(clampedDelta, joint.rotationAxis);
                Quaternion originalLocal = t.localRotation;
                t.localRotation *= rotation;

                float newDistance = Vector3.Distance(endEffector.position, targetPosition);

                if (newDistance < prevDistance)
                {
                    joint.CurrentAngle += clampedDelta;
                }
                else
                {
                    t.localRotation = originalLocal;
                }
            }

            if (Vector3.Distance(endEffector.position, targetPosition) < threshold)
                break;
        }
    }

    private void OnDestroy()
    {
        UpdateScheduler.UnregisterUpdate(OnUpdate);
    }
}
