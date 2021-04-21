using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Teleport_Controller : MonoBehaviour
{
	public const int MAX_CONTROLLERS = 2;
	public const float TRIGGER_ACTIVATE_STRENGTH = 0.8f;

	//VRTK appears to be performing some sort of lookup set, as if you change the name of this, or change it to a property, it stops being assigned a value.
	public uint Index = 0;

	public Vector2 triggerAxis = new Vector2();
	public Vector2 joystick = new Vector2();
	public UInt32 buttons = 0;

	private Dictionary<avs.InputList, bool> buttonPresses = new Dictionary<avs.InputList, bool>();
	private Dictionary<avs.InputList, float> triggerStrengths = new Dictionary<avs.InputList, float>();
	private Dictionary<avs.InputList, avs.Vector2> motionPositions = new Dictionary<avs.InputList, avs.Vector2>();

	private Dictionary<avs.InputList, bool> previousButtonPresses;

	private void Awake()
	{
		controllers[Index] = this;
	}

	void Update()
	{
		joystick.x *= 0.5F;
		joystick.y *= 0.5F;

		if(Math.Abs(joystick.x) < 0.01F)
		{
			joystick.x = 0.0F;
		}

		if(Math.Abs(joystick.y) < 0.01F)
		{
			joystick.y = 0.0F;
		}
	}

	public void SetButtons(UInt32 value)
	{
		buttons = value;
	}

	public void SetJoystick(float x, float y)
	{
		joystick.x = x;
		joystick.y = y;
	}

	public void SetJoystick(Vector2 value)
	{
		joystick.x = value.x;
		joystick.y = value.y;
	}

	public bool IsPressing(avs.InputList inputID)
	{
		if(buttonPresses.TryGetValue(inputID, out bool pressed))
		{
			return pressed;
		}
		else
		{
			return false;
		}
	}

	public bool StartedPressing(avs.InputList inputID)
	{
		if(buttonPresses.TryGetValue(inputID, out bool pressed))
		{
			previousButtonPresses.TryGetValue(inputID, out bool prevPressed);

			return pressed && !prevPressed;
		}
		else
		{
			return false;
		}
	}

	public bool StoppedPressing(avs.InputList inputID)
	{
		if(buttonPresses.TryGetValue(inputID, out bool pressed))
		{
			previousButtonPresses.TryGetValue(inputID, out bool prevPressed);

			return !pressed && prevPressed;
		}
		else
		{
			return false;
		}
	}

	public bool IsTouching(avs.InputList inputID)
	{
		return false;
	}

	public bool StartedTouching(avs.InputList inputID)
	{
		return false;
	}

	public bool StoppedTouching(avs.InputList inputID)
	{
		return false;
	}

	public void ProcessInputEvents(avs.InputEventBinary[] binaryEvents, avs.InputEventAnalogue[] analogueEvents, avs.InputEventMotion[] motionEvents)
	{
		//Copy old button presses for just triggered comparisons.
		previousButtonPresses = new Dictionary<avs.InputList, bool>(buttonPresses);

		foreach(avs.InputEventBinary binaryEvent in binaryEvents)
		{
			buttonPresses[binaryEvent.inputID] = binaryEvent.activated;
		}

		foreach(avs.InputEventAnalogue analogueEvent in analogueEvents)
		{
			triggerStrengths[analogueEvent.inputID] = analogueEvent.strength;
			buttonPresses[analogueEvent.inputID] = analogueEvent.strength >= TRIGGER_ACTIVATE_STRENGTH;
		}

		foreach(avs.InputEventMotion motionEvent in motionEvents)
		{
			motionPositions[motionEvent.inputID] = motionEvent.motion;
		}
	}

	///STATIC FUNCTIONALITY

	private static Teleport_Controller[] controllers = new Teleport_Controller[MAX_CONTROLLERS];

	public static Teleport_Controller GetController(uint index)
	{
		return controllers[index];
	}
}
