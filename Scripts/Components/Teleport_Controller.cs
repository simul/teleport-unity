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

        // OVR X button 0x100 and mouse left button released is 0x010
        if ((buttons & 0x110) != 0)
        {
            ExecuteEvents.Execute(EventSystem.current.currentSelectedGameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        }
    }

    public void AddEvents(UInt32 num, avs.InputEvent[] events)
    { 
        // OVR X button 0x100 and mouse left button released is 0x010
        for(int i=0;i<num;i++)
        {
            ExecuteEvents.Execute(EventSystem.current.currentSelectedGameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        }
    }
}
