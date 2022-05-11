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
    void Update()
    {
        transform.Rotate(axis, speedDegreesPerSecond * Time.deltaTime, Space.World);
    }
}
