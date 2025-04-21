using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Joint : MonoBehaviour
{
    public Vector3 rotationAxis;

    public float minAngle = -90f;
    public float maxAngle = 90f;


    private float currentAngle = 0f;
    public float CurrentAngle
    {
        get { return currentAngle; }
        set
        {
            currentAngle = value;

            if (currentAngle > 180)
            {
                currentAngle -= 180;
            }
            else if (currentAngle < -180)
            {
                currentAngle += 180;
            }
        }
    }
}
