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


	InputID[] Trigger;

	InputID[] LeftTrigger;
	InputID[] RightTrigger;
	InputID[] LeftMouseClick;

	public GameObject arcPrefab;
	public GameObject targetCirclePrefab;
	// Start is called before the first frame update
	void Start()
	{
		session = GetComponentInParent<teleport.Teleport_SessionComponent>();
		teleport.Teleport_Controller[] controllers =GetComponentsInChildren<teleport.Teleport_Controller>();
		foreach(var c in controllers)
		{
			if(c.name.Contains("Left Aim"))
				leftController=c.gameObject;
			else if (c.name.Contains("Right Aim"))
				rightController = c.gameObject;
		}
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
		LeftTrigger = teleportSettings.FindInputsByName("Left Trigger Click");
		RightTrigger= teleportSettings.FindInputsByName("Right Trigger Click");
		Trigger		= new InputID[RightTrigger.Length + LeftTrigger.Length];
		LeftTrigger.CopyTo(Trigger, 0);
		RightTrigger.CopyTo(Trigger, LeftTrigger.Length);

		LeftMouseClick = teleportSettings.FindInputsByName("Left Mouse Click");

		session.input.AddDelegate(LeftTrigger, StartTeleportationLeftPointing, teleport.InputEventType.Press);
		session.input.AddDelegate(RightTrigger, StartTeleportationRightPointing, teleport.InputEventType.Press);
		session.input.AddDelegate(LeftMouseClick, StartTeleportationLeftPointing, teleport.InputEventType.Press);

		session.input.AddDelegate(Trigger, DoTeleportation, teleport.InputEventType.Release);
		session.input.AddDelegate(LeftMouseClick, DoTeleportation, teleport.InputEventType.Release);
	}
	GameObject arc;
	GameObject circle;
	GameObject pointingController;
	GameObject leftController;
	GameObject rightController;
	public void DoTeleportation(teleport.Input input, InputID inputId)
	{
		if(targetIsValid&& CircleRadius>=0.9F* MaxCircleRadius)
		{
			transform.SetPositionAndRotation(circle.transform.position, transform.rotation);
		}
		teleport.Teleport_Streamable streamable = GetComponent<teleport.Teleport_Streamable>();
		if (streamable)
        {
			streamable.ResetVelocityTracking();
        }
		pointingController=null;
		arc.transform.localScale = new Vector3(0,0,0);
		circle.transform.localScale = new Vector3(0, 0, 0);
	}
	public void StartTeleportationLeftPointing(teleport.Input input, InputID inputId)
	{ 
		StartTeleportationPointing(input, inputId,leftController);
	}
	public void StartTeleportationRightPointing(teleport.Input input, InputID inputId)
	{
		StartTeleportationPointing(input,inputId,rightController);
	}
	public void StartTeleportationPointing(teleport.Input input, InputID inputId, GameObject controllerObject)
	{
		if(!arc)
		{ 
		// make visible the arc and target circle.
			arc = Instantiate<GameObject>(arcPrefab, gameObject.transform);
			circle = Instantiate<GameObject>(targetCirclePrefab);
			teleport.GeometrySource.GetGeometrySource().AddNode(arc);
			teleport.GeometrySource.GetGeometrySource().AddNode(circle);
			circle.AddComponent<teleport.Teleport_Streamable>();
			// having created new objects, they won't actually be streamed unless we notify the Teleport_Streamable
			// that manages the hierarchy they're in. Therefore:
			var streamable=GetComponent<teleport.Teleport_Streamable>();
			if(streamable)
            {
				streamable.UpdateHierarchy();
            }
		}
		if (!pointingController)
		{
			pointingController= controllerObject;
			CircleRadius=0.0F;
		}
	}
	float MaxCircleRadius=1.0F;
	float CircleRadius=0.0F;
	float forward_backward = 0.0F;
	bool targetIsValid=false;
	// Update is called once per frame
	void Update()
	{
		if (LeftThumbstickX==null||LeftThumbstickX.Length==0)
		{
			HookupInputs();
		}
        SmoothMotionUpdate();

		if (motionType == MotionType.Teleportation)
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
		if (!arc)
			return;
		if (!pointingController)
			return;
		// Make the position of the arc equal to the controller's position.
		// make its pitch equal to the controller's pitch.
		arc.transform.position= pointingController.transform.position;
		Vector3 ZAxis= pointingController.transform.forward;
		float azimuth= pointingController.transform.eulerAngles.y;
		float pitch=-Mathf.Rad2Deg*Mathf.Asin(ZAxis.y);
		arc.transform.rotation=Quaternion.Euler(pitch, azimuth,0);

		Debug.DrawRay(arc.transform.position, arc.transform.forward, Color.blue);
		//scale
		float scale=4.0F;

		arc.transform.localScale=new Vector3(0.05F, scale, scale);

		// What does this arc intersect with?
		RaycastHit hit;
		Vector3 x1 = new Vector3(0, 0,0);
		CircleRadius *= 0.95F;
		targetIsValid=false;
		for (int i = 1; i < 10; i++)
		{
			float angle=Mathf.PI/2.0F*((float)i/10.0F);
			Vector3 x2 = x1;
			x1=new Vector3(0,Mathf.Cos(angle)-1.0F,Mathf.Sin(angle));
			Vector3 r1=arc.transform.TransformPoint(x1);
			Vector3 r2=arc.transform.TransformPoint(x2);

			Debug.DrawRay(r1, r2-r1, Color.green);
			int layerMask = (int)0x7FFFFFFF;
			Vector3 dir=(r2-r1).normalized;
			if (Physics.Raycast(r1, dir, out hit, (r2 - r1).magnitude, layerMask,QueryTriggerInteraction.Ignore))
			{
				Debug.DrawRay(r1, r2 - r1, Color.red);
				Vector3 hit_point=r1+(r2-r1)*hit.distance;
				if (hit.normal.y <- 0.707F)
				{
					circle.transform.position=hit.point;
					CircleRadius+=0.05F*MaxCircleRadius;
					targetIsValid = true;
					break;
				}
			}
			if (Physics.Raycast(r2, -dir, out hit, (r2 - r1).magnitude, layerMask, QueryTriggerInteraction.Ignore))
			{
				Debug.DrawRay(r1, r2 - r1, Color.red);
				Vector3 hit_point = r1 + (r2 - r1) * hit.distance;
				if (hit.normal.y > 0.707F)
				{
					circle.transform.position = hit.point;
					CircleRadius += 0.05F * MaxCircleRadius;
					targetIsValid = true;
					break;
				}
			}
		}
		circle.transform.localScale=new Vector3(CircleRadius,0.1F, CircleRadius);
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
			if (motionType == MotionType.Smooth)
			{
				if (rightInputY > 0.5f)
					moveForward = true;
				else if (rightInputY < -.5f)
					moveBackward = true;
				if (rightInputX > 0.5f)
					moveRight = true;
				else if (rightInputX < -.5f)
					moveLeft = true;
			}
			else
			{
				if (rightInputX > 0.5f)
					turnRight = true;
				else if (rightInputX < -.5f)
					turnLeft = true;
			}
			if (Mathf.Abs(leftInputX) < 0.2F&& Mathf.Abs(rightInputX)<0.2F)
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
			if (motionType == MotionType.Smooth)
			{
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
		}

		if (motionType == MotionType.Smooth)
		{ 
			transform.Translate(forward * moveMod * forward_backward, Space.World);
			transform.Translate(right * moveMod * left_right, Space.World);
		}
		transform.Rotate(0, rotateDegrees, 0, Space.World);
    }
}
