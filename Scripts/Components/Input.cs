﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace teleport
{
	using InputID = UInt16;
	public struct ButtonEvent
	{
		public bool pressed;
		public bool released;
	}
	public enum InputEventType
	{
		None=0, Press=1, Release=2, Either=3
	}
	[HelpURL("https://docs.teleportvr.io/unity.html")]
	public class Input : MonoBehaviour
	{
		private void OnEnable()
		{
			var sess= GetComponent<Teleport_SessionComponent>();
			if (sess == null)
			{
				Debug.LogError("Must have a SessionComponent where there is an teleport.Input component.");
			}

			TeleportSettings teleportSettings = TeleportSettings.GetOrCreateSettings();
			floatStateIDs.Clear();
			booleanStateIDs.Clear();
			for (UInt16 i = 0; i < teleportSettings.inputDefinitions.Count; i++)
			{
				if(teleportSettings.inputDefinitions[i].inputType==avs.InputType.BooleanState)
					booleanStateIDs.Add(i);
				if (teleportSettings.inputDefinitions[i].inputType == avs.InputType.FloatState)
					floatStateIDs.Add(i);
			}
		}
		List<InputID> floatStateIDs = new List<InputID>();
		public InputID [] GetFloatStateIDs()
		{
			return floatStateIDs.ToArray();
		}
		List<InputID> booleanStateIDs = new List<InputID>();
		public InputID[] GetBooleanStateIDs()
		{
			return booleanStateIDs.ToArray();
		}
		public delegate void InputEventDelegate(Input input,InputID inputId);
		public delegate void FloatEventDelegate(Input input, InputID inputId,float value);
		public Dictionary<InputID, InputEventDelegate> pressDelegates = new Dictionary<InputID, InputEventDelegate>();
		public Dictionary<InputID, InputEventDelegate> releaseDelegates = new Dictionary<InputID, InputEventDelegate>();
		public Dictionary<InputID, FloatEventDelegate> floatEventDelegates = new Dictionary<InputID, FloatEventDelegate>();
		private Dictionary<InputID, bool> buttonStates = new Dictionary<InputID, bool>();
		private Dictionary<InputID, float> floatStates = new Dictionary<InputID, float>();
		private Dictionary<InputID, avs.Vector2> motionPositions = new Dictionary<InputID, avs.Vector2>();

		private Dictionary<InputID, bool> previousButtonPresses;
		public void AddDelegate(InputID inputID, InputEventDelegate d, InputEventType t)
		{
			//Debug.Log("AddDelegate " + inputID + " with " + d.ToString());
			if (t==InputEventType.Press||t==InputEventType.Either)
			{ 
				if(pressDelegates.ContainsKey(inputID))
					pressDelegates[inputID]+=d;
				else
					pressDelegates.Add(inputID, d);
			}
			if (t == InputEventType.Release || t == InputEventType.Either)
			{
				if (releaseDelegates.ContainsKey(inputID))
					releaseDelegates[inputID] += d;
				else
					releaseDelegates.Add(inputID, d);
			}
		}
		public void AddDelegate(InputID [] inputIDs, InputEventDelegate d, InputEventType t)
		{
			foreach (var id in inputIDs)
			{
				AddDelegate(id,d,t);
			}	
		}
		public void AddFloatDelegate(InputID inputID, FloatEventDelegate d)
		{
			if (floatEventDelegates.ContainsKey(inputID))
				floatEventDelegates[inputID] += d;
			else
				floatEventDelegates.Add(inputID, d);
		}
		public void AddFloatDelegate(InputID [] inputIDs, FloatEventDelegate d)
		{
			foreach (var id in inputIDs)
			{
				AddFloatDelegate(id, d);
			}
		}
		public void RemoveDelegate(InputID inputID, InputEventDelegate d, InputEventType t)
		{
			if (t == InputEventType.Press || t == InputEventType.Either)
			{
				if (pressDelegates.ContainsKey(inputID))
					pressDelegates[inputID]-=d;
			}
			if (t == InputEventType.Release || t == InputEventType.Either)
			{
				if (releaseDelegates.ContainsKey(inputID))
					releaseDelegates[inputID]-=d;
			}
		}
		public void RemoveDelegate(InputID [] inputIDs, InputEventDelegate d, InputEventType t)
		{
			foreach (var id in inputIDs)
			{
				RemoveDelegate(id, d, t);
			}
		}
		public void RemoveFloatDelegate(InputID inputID, FloatEventDelegate d)
		{
			if (floatEventDelegates.ContainsKey(inputID))
				floatEventDelegates[inputID] -= d;
		}
		public void RemoveFloatDelegate(InputID[] inputIDs, FloatEventDelegate d)
		{
			foreach (var id in inputIDs)
			{
				RemoveFloatDelegate(id, d);
			}
		}
		// really we need to make this work better.
		// we should for each control receive a stream of events
		public bool IsPressing(InputID inputID)
		{
			if (buttonStates.TryGetValue(inputID, out bool pressed))
			{
				return pressed;
			}
			else
			{
				return false;
			}
		}
		public float GetFloatState(InputID [] inputIDs)
		{
			float v=0.0F;
			foreach (InputID i in inputIDs)
			{
				v+= GetFloatState(i);
			}
			return v;
		}
		public float GetFloatState(InputID inputID)
		{
			if (floatStates.TryGetValue(inputID, out float value))
			{
				return value;
			}
			return 0.0f;
		}
		public bool GetBooleanState(InputID[] inputIDs)
		{
			bool v = false;
			foreach (InputID i in inputIDs)
			{
				v |= GetBooleanState(i);
			}
			return v;
		}
		public bool GetBooleanState(InputID inputID)
		{
			if (buttonStates.TryGetValue(inputID, out bool value))
			{
				return value;
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
			if (motionPositions.TryGetValue(inputID, out avs.Vector2 motionAxis))
			{
				return motionAxis;
			}
			else if (floatStates.TryGetValue(inputID, out float f))
			{
				return new Vector2(f, 0.0f);
			}

			return Vector2.zero;
		}
		public void ProcessInputStates(byte[] booleanStates, float[] analogueStates)
		{
			for (int i = 0; i < booleanStates.Length && i < booleanStateIDs.Count; i++)
			{
				var b = booleanStates[i] != 0;
				InputID inputID = booleanStateIDs[i];
				if (!buttonStates.ContainsKey(inputID))
					buttonStates.Add(inputID, b);
				buttonStates[inputID] = b;
			}
			for (int i = 0; i < analogueStates.Length&&i< floatStateIDs.Count; i++)
			{
				InputID inputID = floatStateIDs[i];
				float f = analogueStates[i];
				if (f < -1.0F || f > 1.0F)
				{
					Debug.LogError("Analogue state too large " + f.ToString());
					continue;
				}
				if (!floatStates.ContainsKey(inputID))
					floatStates.Add(inputID, f);
				floatStates[inputID] = f;
			}
		}
		public void ProcessInputEvents(avs.InputEventBinary[] binaryEvents, avs.InputEventAnalogue[] analogueEvents, avs.InputEventMotion[] motionEvents)
		{
			foreach (avs.InputEventBinary binaryEvent in binaryEvents)
			{
				//Debug.Log("binaryEvent " + binaryEvent.ToString());
				if (binaryEvent.activated)
				{
					if (pressDelegates.ContainsKey(binaryEvent.inputID) && pressDelegates[binaryEvent.inputID] != null)
						pressDelegates[binaryEvent.inputID](this, binaryEvent.inputID);
				}
				else
				{
					if (releaseDelegates.ContainsKey(binaryEvent.inputID) && releaseDelegates[binaryEvent.inputID] != null)
						releaseDelegates[binaryEvent.inputID](this, binaryEvent.inputID);
				}
			}

			foreach (avs.InputEventAnalogue analogueEvent in analogueEvents)
			{
				if (floatEventDelegates.ContainsKey(analogueEvent.inputID) && floatEventDelegates[analogueEvent.inputID] != null)
					floatEventDelegates[analogueEvent.inputID](this, analogueEvent.inputID, analogueEvent.value);
			}

			foreach (avs.InputEventMotion motionEvent in motionEvents)
			{
				motionPositions[motionEvent.inputID] = motionEvent.motion;
			}
		}

	}
}
