using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

using uid = System.UInt64;

namespace teleport
{
	public class Teleport_SessionComponent : MonoBehaviour
	{
		#region DLLImports

		[DllImport("SimulCasterServer")]
		public static extern void Client_StopSession(uid clientID);
		[DllImport("SimulCasterServer")]
		public static extern void Client_StopStreaming(uid clientID);
		[DllImport("SimulCasterServer")]
		public static extern bool Client_HasHost(uid clientID);
		[DllImport("SimulCasterServer")]
		public static extern bool Client_HasPeer(uid clientID);
		[DllImport("SimulCasterServer")]
		public static extern uint Client_GetClientIP(uid clientID, uint bufferLength, StringBuilder buffer);
		public string Client_GetClientIPAddr(uid clientID)
		{
			StringBuilder str = new StringBuilder("", 20);
			try
			{
				uint newlen = Client_GetClientIP(clientID,(uint)16, str);
				if (newlen > 0)
				{
					str = new StringBuilder("",(int)newlen + (int)2);
					Client_GetClientIP(clientID,newlen + 1, str );
				}
			}
			catch (Exception exc)
			{
				UnityEngine.Debug.Log(exc.ToString());
			}
			return str.ToString();
		}
		[DllImport("SimulCasterServer")]
		public static extern System.UInt16 Client_GetClientPort(uid clientID);
		[DllImport("SimulCasterServer")]
		public static extern System.UInt16 Client_GetServerPort(uid clientID);
		[DllImport("SimulCasterServer")]
		private static extern uid GetUnlinkedClientID();

		// C# treats bool as 4 bytes, like C, but not like C++, which correctly uses only one byte.
		[DllImport("SimulCasterServer")]
		private static extern bool Client_SetOrigin(uid clientID, Vector3 pos, [MarshalAs(UnmanagedType.I1)] bool set_rel, Vector3 rel_pos);
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
			if (!sessions.ContainsKey(clientID))
			{
				teleport.TeleportLog.LogErrorOnce("No session component found for client with ID: " + clientID);
				return false;
			}

			return true;
		}
		public string ClientName = "Client1";
		public int Layer = 6;

		// Aidan: This is temporary for the capture component
		public static uid GetLastClientID()
		{
			if (sessions.Count > 0)
			{
				return sessions.Last().Key;
			}
			else
			{
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
			if (!StaticDoesSessionExist(clientID))
				return;
			latestRotation.Set(newHeadPose.orientation.x, newHeadPose.orientation.y, newHeadPose.orientation.z, newHeadPose.orientation.w);
			latestPosition.Set(newHeadPose.position.x, newHeadPose.position.y, newHeadPose.position.z);
			sessions[clientID].SetHeadPose(latestRotation, latestPosition);
		}

		public static void StaticSetControllerPose(uid clientID, int index, in avs.HeadPose newPose)
		{
			if (!StaticDoesSessionExist(clientID))
				return;

			latestRotation.Set(newPose.orientation.x, newPose.orientation.y, newPose.orientation.z, newPose.orientation.w);
			latestPosition.Set(newPose.position.x, newPose.position.y, newPose.position.z);
			sessions[clientID].SetControllerPose(index, latestRotation, latestPosition);
		}

		public static void StaticProcessInput(uid clientID, in avs.InputState inputState, in IntPtr inputEventsPtr)
		{
			if (!StaticDoesSessionExist(clientID))
				return;
			int index = 1;
			sessions[clientID].SetControllerInput(index, inputState.buttonsPressed);
			if (inputEventsPtr != null)
			{
				int EventSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(avs.InputEvent));
				//convert the pointer array into a struct array
				avs.InputEvent[] inputEvents = new avs.InputEvent[inputState.numEvents];
				//inputEvents = Marshal.PtrToStructure<avs.InputEvent[]>(inputEventsPtr);
				IntPtr ptr=  inputEventsPtr;
				for (int i = 0; i < inputState.numEvents; i++)
				{
					inputEvents[i]=Marshal.PtrToStructure<avs.InputEvent>(ptr);
					ptr += EventSize;
				}
				sessions[clientID].SetControllerEvents(index, inputState.numEvents, inputEvents);
			}
		}
		#endregion

		public static Dictionary<uid, Teleport_SessionComponent> sessions = new Dictionary<uid, Teleport_SessionComponent>();

		private TeleportSettings teleportSettings = null;

		//One per session, as we stream geometry on a per-client basis.
		private GeometryStreamingService geometryStreamingService = null;

		public GeometryStreamingService GeometryStreamingService
		{
			get
			{
				return geometryStreamingService;
			}
		}

		private uid clientID = 0;

		public Teleport_Head head = null;
		// The *client* root is the 
		public Teleport_ClientspaceRoot clientspaceRoot = null;
		public Teleport_CollisionRoot collisionRoot = null;
		public Teleport_SceneCaptureComponent sceneCaptureComponent = null;

		private Dictionary<int, Teleport_Controller> controllers = new Dictionary<int, Teleport_Controller>();

		private Vector3 last_sent_origin = new Vector3(0, 0, 0);

		public bool IsConnected()
		{
			return Client_IsConnected(clientID);
		}

		public void Disconnect()
		{
			Client_StopStreaming(clientID);

			sessions.Remove(clientID);
			if (geometryStreamingService != null)
				geometryStreamingService.Clear();

			clientID = 0;
		}
		public avs.AxesStandard axesStandard = new avs.AxesStandard();
		public void ReportHandshake(avs.Handshake handshake)
		{
			axesStandard = handshake.axesStandard;
		}

		public void SetHeadPose(Quaternion newRotation, Vector3 newPosition)
		{
			if (!head)
				return;

			head.transform.rotation = newRotation;
			head.transform.position = clientspaceRoot.transform.position+ newPosition;
		}
		public void SetControllerInput(int index, UInt32 buttons)
		{
			if (controllers.ContainsKey(index))
			{
				var controller = controllers[index];
				controller.SetButtons(buttons);
			}
		}
		public void SetControllerEvents(int index, UInt32 num, avs.InputEvent[] events)
		{
			if (controllers.ContainsKey(index))
			{
				var controller = controllers[index];
				controller.AddEvents(num, events);
			}
		}

		public void SetControllerPose(int index, Quaternion newRotation, Vector3 newPosition)
		{
			if (!controllers.ContainsKey(index))
			{
				Teleport_Controller[] controller_components = GetComponentsInChildren<Teleport_Controller>();
				foreach (var c in controller_components)
				{
					if (c.Index == index)
						controllers[index] = c;
				}
				if (!controllers.ContainsKey(index))
					return;
			}
			var controller = controllers[index];

			//   controller.transform.rotation = newRotation;
			controller.transform.SetPositionAndRotation(newPosition, newRotation);
		}

		private void OnDisable()
		{
			if (sessions.ContainsKey(clientID))
			{
				sessions.Remove(clientID);
			}
		}
		void GetSingleChild<T>(ref T t)
		{
			T[] ts = GetComponentsInChildren<T>();
			if (ts.Length != 1)
			{
				Debug.LogError($"Precisely ONE "+typeof(T).Name+" should be found. <color=red><b>"+ts.Length+ "</b></color> were found!");
			}
			if(ts.Length != 0)
			{
				t= ts[0];
			}
		}
		private void Start()
		{
			teleportSettings = TeleportSettings.GetOrCreateSettings();
			geometryStreamingService = new GeometryStreamingService(this);

			GetSingleChild(ref head);
			GetSingleChild(ref clientspaceRoot);
			GetSingleChild(ref collisionRoot);
			GetSingleChild(ref sceneCaptureComponent);
		}
		bool resetOrigin = true;
		public void ResetOrigin()
		{
			resetOrigin = true;
		}

		private void LateUpdate()
		{
			if (clientID == 0)
			{
				clientID = GetUnlinkedClientID();
				if (clientID == 0)
					return;
				if (sessions.ContainsKey(clientID))
				{
					Debug.LogError("Session duplicate key!");
				}
				sessions[clientID] = this;
			}

			if (Client_IsConnected(clientID))
			{
				if (head != null && clientspaceRoot != null&&(!Client_HasOrigin(clientID))|| resetOrigin)//||transform.hasChanged))
				{
					if (Client_SetOrigin(clientID, clientspaceRoot.transform.position,true, head.transform.position- clientspaceRoot.transform.position))
					{
						last_sent_origin = clientspaceRoot.transform.position;
						transform.hasChanged = false;
						resetOrigin = false;
					}
				}
				else if(clientspaceRoot.transform.hasChanged)
				{
					if (Client_SetOrigin(clientID, clientspaceRoot.transform.position, false, head.transform.position - clientspaceRoot.transform.position))
						clientspaceRoot.transform.hasChanged = false;
				}
				if (collisionRoot != null && collisionRoot.transform.hasChanged)
				{
					collisionRoot.transform.hasChanged = false;
				}
			}

			if (teleportSettings.casterSettings.isStreamingGeometry)
			{
				if (geometryStreamingService != null)
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

			string str = string.Format("Client {0} {1}", clientID, Client_GetClientIPAddr(clientID));
			int dy = 14;
			GUI.Label(new Rect(x, y += dy, 300, 20), str, font);
			GUI.Label(new Rect(x, y += dy, 300, 20), string.Format("sent origin\t{0}", last_sent_origin), font);
			GUI.Label(new Rect(x, y += dy, 300, 20), string.Format("head position\t{0}", headPosition), font);
			if (geometryStreamingService != null)
			{
				int num_actors = geometryStreamingService.GetStreamedObjectCount();
				GUI.Label(new Rect(x, y += dy, 300, 20), string.Format("Actors {0}", num_actors));
				List<GameObject> streamedGameObjects = geometryStreamingService.GetStreamedObjects();
				for (int i = 0; i < num_actors; i++)
				{
					var actor = streamedGameObjects[i];
					uid actor_uid = geometryStreamingService.GetActorID(actor);
					GUI.Label(new Rect(x, y += dy, 500, 20), string.Format("\t{0} {1}", actor_uid, actor.name));
				}
				int num_lights = geometryStreamingService.GetStreamedLightCount();
				GUI.Label(new Rect(x, y += dy, 300, 20), string.Format("Lights {0}", num_lights));
				int j = 0;
				foreach (var l in geometryStreamingService.GetStreamedLights())
				{
					var light = l.Value;
					if (light != null)
					{
						GUI.Label(new Rect(x, y += dy, 500, 20), string.Format("\t{0} {1}:{2},{3},{4}", l.Key, light.name, light.transform.forward.x, light.transform.forward.y, light.transform.forward.z));
						if (sceneCaptureComponent.VideoEncoder != null && j < sceneCaptureComponent.VideoEncoder.cubeTagDataWrapper.data.lightCount)
						{
							var shadow_pos = sceneCaptureComponent.VideoEncoder.cubeTagDataWrapper.data.lights[j].position;
							GUI.Label(new Rect(x, y += dy, 500, 20), string.Format("\t\tshadow orig {0},{1},{2}", shadow_pos.x, shadow_pos.y, shadow_pos.z));

						}
						j++;
					}
				}
			}
			foreach (var c in controllers)
			{
				GUI.Label(new Rect(x, y += dy, 300, 20), string.Format("Controller {0}, buttons: {1}", c.Key, c.Value.buttons));
			}
		}
		private void OnDestroy()
		{
			if (clientID != 0)
				Client_StopSession(clientID);
		}

		public void SetStreamedLights(Light[] lights)
		{
			geometryStreamingService.SetStreamedLights(lights);
		}
	}
}