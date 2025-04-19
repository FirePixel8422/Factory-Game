using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Joint : MonoBehaviour
{
    public Joint childJoint;

    [SerializeField] private Vector3 axis = Vector3.up;


    private void Start()
    {
       foreach(Transform child in transform)
       {
            if (child.TryGetComponent(out Joint joint))
            {
                childJoint = joint;
                break;
            }
       }
    }


    public void Rotate(float angle)
    {
        transform.Rotate(axis, angle);
    }
}
