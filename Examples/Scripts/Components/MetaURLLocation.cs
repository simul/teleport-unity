using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class MetaURLLocation : MonoBehaviour
{
    public string metaURL;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        MetaURLScene metaURLScene = MetaURLScene.GetInstance();
        if(metaURLScene == null) 
            return;
		Vector3 offset=transform.position-metaURLScene.origin;
        Vector3 coords=100.0F*offset/metaURLScene.scaleMetres;
        int X=Mathf.FloorToInt(coords.x);
        int X2=(int)(100.0F*(coords.x-(float)X));
		int Y = Mathf.FloorToInt(coords.y);
		int Y2 = (int)(100.0F * (coords.y - (float)Y));
		int Z = Mathf.FloorToInt(coords.z);
		int Z2 = (int)(100.0F * (coords.z - (float)Z));
        metaURL=X+"."+Y+"."+Z+"/"+X2+"."+Y2+"."+Z2;
	}
}
