using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    float moveSpeed = 30;
    Rigidbody rbRef;

    void Start()
    {
        rbRef = GetComponent<Rigidbody>();
    }

    public void Move(byte mask)
    {
        //Debug.Log("Received Delta: " + delta );

        if ((mask & 1) != 0)
        {
            rbRef.AddForce(Vector3.forward * moveSpeed * Time.fixedDeltaTime, ForceMode.Impulse);
        }

        if ((mask & 2) != 0)
        {
            rbRef.AddForce(-Vector3.forward * moveSpeed * Time.fixedDeltaTime, ForceMode.Impulse);
        }

        if ((mask & 4) != 0)
        {
            rbRef.AddForce(-Vector3.right * moveSpeed * Time.fixedDeltaTime, ForceMode.Impulse);
        }

        if ((mask & 8) != 0)
        {
            rbRef.AddForce(Vector3.right * moveSpeed * Time.fixedDeltaTime, ForceMode.Impulse);
        }
    }
}
