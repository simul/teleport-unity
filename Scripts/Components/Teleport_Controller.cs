using System;
using System.Collections.Generic;
using UnityEngine;

namespace teleport
{
	public class Teleport_Controller : MonoBehaviour
	{
		public const int MAX_CONTROLLERS = 2;
		public const float TRIGGER_ACTIVATE_STRENGTH = 0.8f;

		//VRTK appears to be performing some sort of lookup set, as if you change the name of this, or change it to a property, it stops being assigned a value.
		public uint Index = 0;

		[Tooltip("GameObject with the controller's mesh component.")]
		public GameObject controllerModel = default;
		[Tooltip("Animation played when the trigger is pressed.")]
		public AnimationClip triggerPressAnimation = default;
		[Tooltip("Override for the animation's current time.")]
		public avs.AnimationTimeControl pressAnimationTimeOverride = avs.AnimationTimeControl.ANIMATION_TIME;

		[HideInInspector]
		public teleport.Teleport_SessionComponent session = default;

		[HideInInspector]
		public Vector2 joystick = new Vector2();
		[HideInInspector]
		public UInt32 buttons = 0;

		private Dictionary<avs.InputList, bool> buttonPresses = new Dictionary<avs.InputList, bool>();
		private Dictionary<avs.InputList, float> triggerStrengths = new Dictionary<avs.InputList, float>();
		private Dictionary<avs.InputList, avs.Vector2> motionPositions = new Dictionary<avs.InputList, avs.Vector2>();

		private Dictionary<avs.InputList, bool> previousButtonPresses;

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

		public Vector2 GetAxis(avs.InputList inputID)
		{
			if(motionPositions.TryGetValue(inputID, out avs.Vector2 motionAxis))
			{
				return motionAxis;
			}
			else if(triggerStrengths.TryGetValue(inputID, out float triggerStrength))
			{
				return new Vector2(triggerStrength, 0.0f);
			}

			return Vector2.zero;
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

		///UNITY MESSAGES

		private void OnEnable()
		{
			controllers[Index] = this;
		}

		private void Update()
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

		///STATIC FUNCTIONALITY

		private static Teleport_Controller[] controllers = new Teleport_Controller[MAX_CONTROLLERS];

		public static Teleport_Controller GetController(uint index)
		{
			return controllers[index];
		}
	}
}
