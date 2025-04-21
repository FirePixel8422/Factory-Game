using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RobotArm : MonoBehaviour
{
    [SerializeField] private Joint[] joints;

    [SerializeField] private Transform target;
    [SerializeField] private int iterationsPerFrame;


    private void Start()
    {
        joints = GetComponentsInChildren<Joint>();

        UpdateScheduler.Register(OnUpdate);
    }


    private void OnUpdate()
    {
        SolveIKCCD(joints, joints[joints.Length - 1].transform, target, iterationsPerFrame, 0.01f);
    }


    public void SolveIKCCD(Joint[] joints, Transform endEffector, Transform target, int iterations = 10, float threshold = 0.01f)
    {
        for (int i = 0; i < iterations; i++)
        {
            for (int j = joints.Length - 1; j >= 0; j--)
            {
                Joint joint = joints[j];
                Transform t = joint.transform;

                // Skip rotation for this joint if it's locked (minAngle == maxAngle == 0)
                if (joint.minAngle == 0f && joint.maxAngle == 0f)
                {
                    continue; // Skip rotating this joint
                }

                Vector3 toEnd = (endEffector.position - t.position).normalized;
                Vector3 toTarget = (target.position - t.position).normalized;

                Quaternion deltaRot = Quaternion.FromToRotation(toEnd, toTarget);

                // Convert joint's local axis to world space
                Vector3 axisWorld = t.TransformDirection(joint.rotationAxis.normalized);

                // Extract rotation angle around that axis
                deltaRot.ToAngleAxis(out float angle, out Vector3 rawAxis);

                // Inside CCD loop, calculate signed angle
                float signedAngle = Vector3.Dot(rawAxis, axisWorld) * angle;

                // Clamp angle based on joint limits
                float newAngle = Mathf.Clamp(joint.CurrentAngle + signedAngle, joint.minAngle, joint.maxAngle);
                float clampedDelta = newAngle - joint.CurrentAngle;


                float prevDistance = Vector3.Distance(endEffector.position, target.position);

                // Apply clamped rotation (but not yet!)
                Quaternion rotation = Quaternion.AngleAxis(clampedDelta, joint.rotationAxis);

                // Simulate rotation
                Quaternion originalLocal = t.localRotation;
                t.localRotation *= rotation;

                float newDistance = Vector3.Distance(endEffector.position, target.position);

                // Only keep the rotation if it's better
                if (newDistance < prevDistance)
                {
                    joint.CurrentAngle += clampedDelta; // Accept rotation
                }
                else
                {
                    t.localRotation = originalLocal; // Revert
                }

            }

            // Stop if the end effector is close enough to the target
            if (Vector3.Distance(endEffector.position, target.position) < threshold)
                break;
        }
    }



    private void OnDestroy()
    {
        UpdateScheduler.Unregister(OnUpdate);
    }
}
