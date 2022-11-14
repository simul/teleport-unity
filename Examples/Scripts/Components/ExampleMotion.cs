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
	InputID [] LeftThumbstickX;
	InputID [] LeftThumbstickY;
	InputID [] RightThumbstickX;
	InputID [] RightThumbstickY;


	InputID[] RotateLeft;
	InputID[] RotateRight;
	InputID[] Forward;
	InputID[] Backward;
	InputID[] Left;
	InputID[] Right;
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
		LeftThumbstickX= teleportSettings.FindInputsByName("Left Thumbstick X");
		LeftThumbstickY = teleportSettings.FindInputsByName("Left Thumbstick Y");
		RightThumbstickX = teleportSettings.FindInputsByName("Right Thumbstick X");
		RightThumbstickY = teleportSettings.FindInputsByName("Right Thumbstick Y");

		RotateLeft	= teleportSettings.FindInputsByName("RotateLeft");
		RotateRight = teleportSettings.FindInputsByName("RotateRight");
		Forward		= teleportSettings.FindInputsByName("Forward");
		Backward	= teleportSettings.FindInputsByName("Backward");
		Left		= teleportSettings.FindInputsByName("Left");
		Right		= teleportSettings.FindInputsByName("Right");
	}

   float forward_backward = 0.0F;
	// Update is called once per frame
	void Update()
	{
		if (LeftThumbstickX==null||LeftThumbstickX.Length==0)
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
	float leftInputX=0.0f;
	float leftInputY=0.0f;
	float rightInputX = 0.0f;
	float rightInputY = 0.0f;
	bool turnRight=false;
	bool turnLeft=false;
	bool moveForward = false;
	bool moveBackward = false;
	bool moveLeft = false;
	bool moveRight = false;
	void SmoothMotionUpdate()
    {
		Vector3 forward = head.transform.forward;
		forward.y = 0;
		Vector3 right = head.transform.right;
		right.y = 0;
		float playerMoveMultiplier = 0.2F;
		//float sprintMultiplier = 1.0F;
		float moveMod = playerMoveMultiplier;//Time.deltaTime * playerMoveMultiplier * sprintMultiplier;
		forward_backward = 0.0F;
		float left_right=0.0F;
		float rotateDegrees = 0.0F;
		if (session&&session.input )
		{
			leftInputX*=0.9F;
			leftInputY*=0.9F;
			rightInputX *= 0.9F;
			rightInputY *= 0.9F;
			
			leftInputX	+=0.1F*(session.input.GetFloatState(LeftThumbstickX) +(session.input.GetBooleanState(RotateRight)?1.0F:0.0F)-(session.input.GetBooleanState(RotateLeft)?1.0F:0.0F));
			leftInputY	+=0.1F*(session.input.GetFloatState(LeftThumbstickY) );
			rightInputX +=0.1F*(session.input.GetFloatState(RightThumbstickX)+(session.input.GetBooleanState(Right)?1.0F:0.0F)-(session.input.GetBooleanState(Left)?1.0F:0.0F));
			rightInputY +=0.1F*(session.input.GetFloatState(RightThumbstickY)+(session.input.GetBooleanState(Forward)?1.0F:0.0F)-(session.input.GetBooleanState(Backward)?1.0F:0.0F));
			if(leftInputX > 0.5f)
				turnRight=true;
			else if(leftInputX < -.5f)
				turnLeft=true;
			if (rightInputY > 0.5f)
				moveForward = true;
			else if (rightInputY < -.5f)
				moveBackward = true;
			if (rightInputX > 0.5f)
				moveRight = true;
			else if (rightInputX < -.5f)
				moveLeft = true;
			if (Mathf.Abs(leftInputX) < 0.2F)
			{
				if (turnRight)
				{
					rotateDegrees+=10.0F;
					turnRight=false;
				}
				if (turnLeft)
				{
					rotateDegrees-= 10.0F;
					turnLeft = false;
				}
			}
			if (Mathf.Abs(rightInputY) < 0.2F)
			{
				if (moveForward)
				{
					forward_backward += 1.0F;
					moveForward = false;
				}
				if (moveBackward)
				{
					forward_backward -= 1.0F;
					moveBackward = false;
				}
			}
			if (Mathf.Abs(rightInputX) < 0.2F)
			{
				if (moveLeft)
				{
					left_right -= 1.0F;
					moveLeft = false;
				}
				if (moveRight)
				{
					left_right += 1.0F;
					moveRight = false;
				}
			}
		}

		transform.Translate(forward * moveMod * forward_backward, Space.World);
		transform.Translate(right * moveMod * left_right, Space.World);
		transform.Rotate(0, rotateDegrees, 0, Space.World);
    }
}
