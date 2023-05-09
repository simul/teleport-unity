using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Translate : MonoBehaviour
{
	public Vector3 axis=new Vector3(1.0f,0.0f,0.0f);
	public float scaleMetres=0.5f;
    public float timeScale =1.0f;
    Vector3 origin;
    // Start is called before the first frame update
    void Start()
    {
        origin=transform.position;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        float angle=2.0F*3.1415926536F*Time.time/timeScale;
        float sn=Mathf.Sin(angle);
        transform.position= origin+axis*sn*scaleMetres;
        var r=GetComponent<Rigidbody>();
        if (r)
        {
           // r.angularVelocity=axis*speedDegreesPerSecond*(3.1415926536F/180.0F);
        }
    }
}
