using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Teleport_Controller : MonoBehaviour
{
    public uint Index = 0;
	public Vector2 triggerAxis = new Vector2();
	public UInt32 buttons=0;

	// Start is called before the first frame update
	void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

	public void SetButtons(UInt32 b)
	{
		buttons = b;

        // OVR X button 0x100 and mouse left button is 0x001
        if ((buttons & 0x00000101) != 0)
        {
            ExecuteEvents.Execute(EventSystem.current.currentSelectedGameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        }
    }
}
