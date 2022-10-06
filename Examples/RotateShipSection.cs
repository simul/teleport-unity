using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateShipSection : MonoBehaviour
{
	// Start is called before the first frame update
	void Start()
	{
		transform.rotation = new Quaternion();
	}

	// Update is called once per frame
	void Update()
	{
		var ea = transform.rotation.eulerAngles;
		ea.z++;
		transform.rotation.eulerAngles.Set(ea.x, ea.y, ea.z);
	}
}
