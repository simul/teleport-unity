using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Teleport_Controller : MonoBehaviour
{
    public uint Index = 0;
	public Vector2 triggerAxis = new Vector2();
    public Vector2 joystick = new Vector2();
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
    }

    public void SetJoystick(float x,float y)
    {
        joystick.x = x;
        joystick.y = y;
    }
    
    public void AddEvents(UInt32 num, avs.InputEvent[] events)
    { 
        // OVR X button 0x100 and mouse left button released is 0x010
        for(int i=0;i<num;i++)
        {
            var evt = events[i];
            if (evt.inputUid == 1)// e.g. mouse left btn
            {
                if (EventSystem.current!=null)
                    ExecuteEvents.Execute(EventSystem.current.currentSelectedGameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            }
        }
    }
}
