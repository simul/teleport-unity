using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MetaURLScene : MonoBehaviour
{
    // Start is called before the first frame update
    public Vector3 origin=new Vector3(0,0,0);
    public float scaleMetres=100.0F;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public static MetaURLScene GetInstance()
    {
		MetaURLScene[] metaURLScenes = GameObject.FindObjectsOfType<MetaURLScene>();
		if (metaURLScenes.Length == 0)
		{
			UnityEngine.Debug.LogError("No MetaURLScene found, add precisely one to an object in the scene.");
			return null;
		}
		if (metaURLScenes.Length > 1)
		{
			UnityEngine.Debug.LogError("More than one MetaURLScenes were found, there should be precisely one in the scene.");
			return null;
		}
		MetaURLScene metaURLScene = metaURLScenes[0];
        return metaURLScene;
	}
}
