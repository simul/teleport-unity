using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using InputID = System.UInt16;
public class ExampleMotion : MonoBehaviour
{
	public enum MotionType
    {
		Smooth,
		Teleportation
    }

    public MotionType motionType=MotionType.Smooth;
	teleport.Teleport_Controller[]  controllers;
	teleport.Teleport_Head head;
	teleport.Teleport_SessionComponent session=null;
	InputID LeftThumbstickX=0;
	InputID LeftThumbstickY=0;
	// Start is called before the first frame update
	void Start()
	{
		session = GetComponentInParent<teleport.Teleport_SessionComponent>();
		controllers =GetComponentsInChildren<teleport.Teleport_Controller>(); 
		head = GetComponentInChildren<teleport.Teleport_Head>();
	}
	
	void HookupInputs()
	{
		teleport.TeleportSettings teleportSettings = teleport.TeleportSettings.GetOrCreateSettings();
		LeftThumbstickX= teleportSettings.FindInput("Left Thumbstick X");
		LeftThumbstickY = teleportSettings.FindInput("Left Thumbstick Y");
	}

   Vector2 stick = new Vector2(0,0);
	// Update is called once per frame
	void Update()
	{
		if (LeftThumbstickX==0)
		{
			HookupInputs();
		}
		if (motionType == MotionType.Smooth)
        {
			SmoothMotionUpdate();
        }
		else
        {
			TeleportMotionUpdate();
        }
		if(!session)
			session = GetComponentInParent<teleport.Teleport_SessionComponent>();
	}
	// In teleporation motion, the player moves by jumps between positions.
	// A hand controller is used to point to where you want to go. The new position is highlighted while the Move button is depressed.
	void TeleportMotionUpdate()
    {

    }
	void SmoothMotionUpdate()
    {
		Vector3 forward = head.transform.forward;
		forward.y = 0;
		Vector3 right = head.transform.right;
		right.y = 0;
		float playerMoveMultiplier = 2.0F;
		float sprintMultiplier = 1.0F;
		float moveMod = Time.deltaTime * playerMoveMultiplier * sprintMultiplier;
		stick *= 0.5F;

		if (session&&session.input &&LeftThumbstickX!= 0)
		{ 
			Vector2 axis = new Vector2(session.input.GetFloatState(LeftThumbstickX), session.input.GetFloatState(LeftThumbstickY));	
			stick += 0.5F * (axis );
		}

		transform.Translate(forward * moveMod * stick.y, Space.World);
		//transform.Translate(right * moveMod * stick.x, Space.World);
		float rotateMod = Time.deltaTime * 150.0F;
		transform.Rotate(0, rotateMod * stick.x, 0, Space.World);
    }
}
