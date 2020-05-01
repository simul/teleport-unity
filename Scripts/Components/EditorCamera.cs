using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EditorCamera : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        // only runs in play mode.
        Camera cam=GetComponent<Camera>();
        if (cam)
            cam.enabled = false;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
