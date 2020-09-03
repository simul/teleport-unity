﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

using uid = System.UInt64;

namespace teleport
{
	public class Teleport_SessionComponent : MonoBehaviour
	{
		#region DLLImports
		[DllImport("SimulCasterServer")]
		public static extern void StopSession(uid clientID);
		[DllImport("SimulCasterServer")]
		public static extern bool HasHost(uid clientID);
		[DllImport("SimulCasterServer")]
		public static extern bool HasPeer(uid clientID);
		[DllImport("SimulCasterServer")]
		public static extern string GetClientIP(uid clientID);
		[DllImport("SimulCasterServer")]
		public static extern System.UInt16 GetClientPort(uid clientID);
		[DllImport("SimulCasterServer")]
		public static extern System.UInt16 GetServerPort(uid clientID);
		[DllImport("SimulCasterServer")]
		private static extern uid GetUnlinkedClientID();

		[DllImport("SimulCasterServer")]
		private static extern bool Client_SetOrigin(uid clientID, Vector3 pos);
		[DllImport("SimulCasterServer")]
		private static extern bool Client_IsConnected(uid clientID);
		[DllImport("SimulCasterServer")]
		private static extern bool Client_HasOrigin(uid clientID);
		#endregion

		#region StaticCallbacks
		private static Quaternion latestRotation = new Quaternion();
		private static Vector3 latestPosition = new Vector3();

		public static bool StaticDoesSessionExist(uid clientID)
		{
			if(!sessions.ContainsKey(clientID))
			{
				teleport.TeleportLog.LogErrorOnce("No session component found for client with ID: " + clientID);
				return false;
			}

			return true;
		}

		// Aidan: This is temporary for the capture component
		public static uid GetLastClientID()
		{
			if (sessions.Count > 0)
			{
				return sessions.Last().Key;
			}
			else
			{
				//Debug.Log("There are currently no clients");
				return 0;
			}
		}

		public static void StaticDisconnect(uid clientID)
		{
			if (sessions.ContainsKey(clientID))
				sessions[clientID].Disconnect();
		}

		public static void StaticSetHeadPose(uid clientID, in avs.HeadPose newHeadPose)
		{
			if(!StaticDoesSessionExist(clientID)) return;

			latestRotation.Set(newHeadPose.orientation.x, newHeadPose.orientation.y, newHeadPose.orientation.z, newHeadPose.orientation.w);
			latestPosition.Set(newHeadPose.position.x, newHeadPose.position.y, newHeadPose.position.z);
			sessions[clientID].SetHeadPose(latestRotation, latestPosition);
		}

		public static void StaticSetControllerPose(uid clientID, int index, in avs.HeadPose newPose)
		{
			if(!StaticDoesSessionExist(clientID))
				return;

			latestRotation.Set(newPose.orientation.x, newPose.orientation.y, newPose.orientation.z, newPose.orientation.w);
			latestPosition.Set(newPose.position.x, newPose.position.y, newPose.position.z);
			sessions[clientID].SetControllerPose(index, latestRotation, latestPosition);
		}

		public static void StaticProcessInput(uid clientID, in avs.InputState newInput)
		{
			if (!StaticDoesSessionExist(clientID))
				return;
			int index = 1;
			sessions[clientID].SetControllerInput(index, newInput.buttonsPressed);
		}
		#endregion

		public static Dictionary<uid, Teleport_SessionComponent> sessions = new Dictionary<uid, Teleport_SessionComponent>();

		private TeleportSettings teleportSettings = null;

		//One per session, as we stream geometry on a per-client basis.
		private GeometryStreamingService geometryStreamingService=null;

		private uid clientID = 0;

		private Teleport_Head head = null;
		private Dictionary<int, Teleport_Controller> controllers = new Dictionary<int, Teleport_Controller>();

		private Vector3 last_sent_origin = new Vector3(0, 0, 0);

		public bool IsConnected()
        {
			return Client_IsConnected(clientID);
        }

		public void Disconnect()
		{
			//StopSession(clientID);

			sessions.Remove(clientID);

			geometryStreamingService.Clear();

			clientID = 0;
		}

		public void SetHeadPose(Quaternion newRotation, Vector3 newPosition)
		{
			if (!head)
				return;

			head.transform.rotation = newRotation;
			head.transform.position = newPosition;
		}
		public void SetControllerInput(int index,UInt32 buttons)
		{
			if (controllers.ContainsKey(index))
			{
				var controller = controllers[index];
				controller.SetButtons(buttons);
			}
		}
		public void SetControllerPose(int index, Quaternion newRotation, Vector3 newPosition)
		{
			if(!controllers.ContainsKey(index))
			{
				Teleport_Controller[] controller_components = GetComponentsInChildren<Teleport_Controller>();
				foreach(var c in controller_components)
				{
					if(c.Index == index)
						controllers[index] = c;
				}
				if(!controllers.ContainsKey(index))
					return;
			}
			var controller = controllers[index];
			
		 //   controller.transform.rotation = newRotation;
			controller.transform.SetPositionAndRotation( newPosition,newRotation);
		}

		private void OnDisable()
		{
			if(sessions.ContainsKey(clientID))
			{
				sessions.Remove(clientID);
			}
		}

		private void Start()
		{
			teleportSettings = TeleportSettings.GetOrCreateSettings();
			geometryStreamingService = new GeometryStreamingService(this);

			Teleport_Head[] heads = GetComponentsInChildren<Teleport_Head>();
			if (heads.Length != 1)
			{
				Debug.LogError("Precisely ONE Teleport_Head should be found.");
			}
			head = heads[0];
		}

		private void LateUpdate()
		{
			if(clientID == 0)
			{
				clientID = GetUnlinkedClientID();
				if(clientID == 0)
					return;
				// Make sure we have a Teleport Render Pipeline, or we won't get a video stream.
				if (UnityEngine.Rendering.RenderPipelineManager.currentPipeline == null || UnityEngine.Rendering.RenderPipelineManager.currentPipeline.GetType() != typeof(TeleportRenderPipeline))
				{
					Debug.LogError("currentPipeline is not TeleportRenderPipeline.");
				}
				if (sessions.ContainsKey(clientID))
				{
					Debug.LogError("Session duplicate key!");
				}
				sessions[clientID] = this;
			}

			if(Client_IsConnected(clientID))
			{
				if(head != null && (!Client_HasOrigin(clientID)))//||transform.hasChanged))
				{
					if(Client_SetOrigin(clientID, head.transform.position))
					{
						last_sent_origin = head.transform.position;
						transform.hasChanged = false;
					}
				}
			}

			if(teleportSettings.casterSettings.isStreamingGeometry)
			{
				if(geometryStreamingService!=null)
					geometryStreamingService.UpdateGeometryStreaming();
			}
		}

		public uid GetClientID()
		{
			return clientID;
		}

		public void ShowOverlay(int x, int y, GUIStyle font)
		{
			Vector3 headPosition = head ? head.transform.position : new Vector3();

			string str=string.Format("Client {0}", clientID);
			int dy = 14;
			GUI.Label(new Rect(x, y+=dy, 300, 20), str, font);
			GUI.Label(new Rect(x, y+=dy, 300, 20), string.Format("sent origin\t{0}", last_sent_origin), font);
			GUI.Label(new Rect(x, y += dy, 300, 20), string.Format("head position\t{0}", headPosition), font);
			if (geometryStreamingService != null)
			{
				GUI.Label(new Rect(x, y += dy, 300, 20), string.Format("Actors {0}", geometryStreamingService.GetStreamedObjectCount()));
				GUI.Label(new Rect(x, y += dy, 300, 20), string.Format("Lights {0}", geometryStreamingService.GetStreamedLightCount()));
			}
			foreach (var c in controllers)
			{
				GUI.Label(new Rect(x, y += dy, 300, 20), string.Format("Controller {0}, buttons: {1}", c.Key,c.Value.buttons));
			}
		}
		private void OnDestroy()
		{
			if(clientID != 0)
				StopSession(clientID);
		}

		public void SetVisibleLights(Light[] lights)
		{
			geometryStreamingService.SetVisibleLights(lights);
		}
	}
}