using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rotate : MonoBehaviour
{
	public Vector3 axis=new Vector3(1.0f,0.0f,0.0f);
	public float speedDegreesPerSecond=30.0f;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        transform.Rotate(axis, speedDegreesPerSecond * Time.fixedDeltaTime, Space.World);
        var r=GetComponent<Rigidbody>();
        if (r)
        {
           // r.angularVelocity=axis*speedDegreesPerSecond*(3.1415926536F/180.0F);
        }
    }
}
