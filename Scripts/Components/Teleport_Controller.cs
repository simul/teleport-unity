using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace teleport
{
	using InputID = UInt16;
	public struct ButtonEvent
	{
		public bool pressed;
	}
	public class Teleport_Controller : MonoBehaviour
	{
		public const int MAX_CONTROLLERS = 2;
		public const float TRIGGER_ACTIVATE_STRENGTH = 0.2f;

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
		public float triggerBack = 0.0F;
		[HideInInspector]
		public float triggerGrip = 0.0F;
		[HideInInspector]
		public UInt32 buttons = 0;

		// For each input, we create a queue of  events, so that presses and releases are processed in order.
		public Dictionary<InputID, List<ButtonEvent>> buttonPressesAndReleases = new Dictionary<InputID, List<ButtonEvent>>();
		private Dictionary<InputID, bool> buttonStates = new Dictionary<InputID, bool>();
		private Dictionary<InputID, float> triggerStrengths = new Dictionary<InputID, float>();
		private Dictionary<InputID, avs.Vector2> motionPositions = new Dictionary<InputID, avs.Vector2>();

		private Dictionary<InputID, bool> previousButtonPresses;

		public void SetButtons(UInt32 value)
		{
			buttons = value;
		}

		public void SetJoystick(float x, float y)
		{
			joystick.x = x;
			joystick.y = y;
		}
		public void SetTriggers(float tb, float tg)
		{
			triggerBack = tb;
			triggerGrip = tg;
		}
		public void SetJoystick(Vector2 value)
		{
			joystick.x = value.x;
			joystick.y = value.y;
		}

		// really we need to make this work better.
		// we should for each control receive a stream of events
		public bool IsPressing(InputID inputID)
		{
			if(buttonStates.TryGetValue(inputID, out bool pressed))
			{
				return pressed;
			}
			else
			{
				return false;
			}
		}
		public delegate void ControllerEventDelegate(Teleport_Controller controller);
		//public ControllerEventDelegate triggerReleaseDelegates;
		public Dictionary<InputID,ControllerEventDelegate> pressDelegates=new Dictionary<InputID, ControllerEventDelegate>();
		public Dictionary<InputID, ControllerEventDelegate> releaseDelegates = new Dictionary<InputID, ControllerEventDelegate>();
		public bool StartedPressing(InputID inputID)
		{
			if (buttonPressesAndReleases.TryGetValue(inputID, out List<ButtonEvent> buttonEventList))
			{
				if (buttonEventList.Count == 0)
					return false;
				// Get the first event. Was it a press?
				ButtonEvent buttonEvent = buttonEventList[0];
				if (buttonEvent.pressed)
				{
				// this event has now been processed and can be removed.
					buttonEventList.RemoveAt(0);
					return true;
				}
			}
			return false;
		}

		public bool StoppedPressing(InputID inputID)
		{
			if (buttonPressesAndReleases.TryGetValue(inputID, out List<ButtonEvent> buttonEventList))
			{
				if (buttonEventList.Count == 0)
					return false;
				ButtonEvent buttonEvent = buttonEventList[0];
				if (!buttonEvent.pressed)
				{
					buttonEventList.RemoveAt(0);
					return true;
				}
			}
			return false;
		}

		public bool IsTouching(InputID inputID)
		{
			return false;
		}

		public bool StartedTouching(InputID inputID)
		{
			return false;
		}

		public bool StoppedTouching(InputID inputID)
		{
			return false;
		}

		public Vector2 GetAxis(InputID inputID)
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
			foreach(avs.InputEventBinary binaryEvent in binaryEvents)
			{
				if(!buttonPressesAndReleases.ContainsKey(binaryEvent.inputID))
					buttonPressesAndReleases.Add(binaryEvent.inputID, new List<ButtonEvent>());
				var evt= new ButtonEvent();
				evt.pressed=binaryEvent.activated;
				buttonPressesAndReleases[binaryEvent.inputID].Add(evt);
				if (!buttonStates.ContainsKey(binaryEvent.inputID))
					buttonStates.Add(binaryEvent.inputID, evt.pressed);
				else
					buttonStates[binaryEvent.inputID] =evt.pressed;
				if (!evt.pressed)
				{
					if (releaseDelegates[binaryEvent.inputID] != null)
						releaseDelegates[binaryEvent.inputID](this);
				}
			}

			foreach(avs.InputEventAnalogue analogueEvent in analogueEvents)
			{
				if (!buttonPressesAndReleases.ContainsKey(analogueEvent.inputID))
					buttonPressesAndReleases.Add(analogueEvent.inputID, new List<ButtonEvent>());
				var evt = new ButtonEvent();
				evt.pressed = analogueEvent.strength >= TRIGGER_ACTIVATE_STRENGTH;
				if (!buttonStates.ContainsKey(analogueEvent.inputID))
					buttonStates.Add(analogueEvent.inputID, !evt.pressed);
				if (evt.pressed != buttonStates[analogueEvent.inputID])
				{
					buttonPressesAndReleases[analogueEvent.inputID].Add(evt);
					buttonStates[analogueEvent.inputID] = evt.pressed;
				}
				triggerStrengths[analogueEvent.inputID] = analogueEvent.strength;
				if (!evt.pressed )
				{
					if (releaseDelegates[analogueEvent.inputID] != null)
						releaseDelegates[analogueEvent.inputID](this);
				}
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
