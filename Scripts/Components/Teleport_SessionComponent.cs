﻿using System.Collections;
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
		public static extern void ActorEnteredBounds(uid clientID, uid actorID);
		[DllImport("SimulCasterServer")]
		public static extern void ActorLeftBounds(uid clientID, uid actorID);
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

		static bool done = false;
		public static bool StaticDoesSessionExist(uid clientID)
		{
			if(!sessions.ContainsKey(clientID))
			{
				if (!done)
				{
					Debug.LogError("No session component found for client with ID: " + clientID);
					done = true;
				}
				return false;
			}

			return true;
		}

		// Aidan: This is temporary for the capture component
		public static uid GetClientID()
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
		}
		#endregion

		public static Dictionary<uid, Teleport_SessionComponent> sessions = new Dictionary<uid, Teleport_SessionComponent>();

		private CasterMonitor casterMonitor; //Cached reference to the caster monitor.
		private TeleportSettings teleportSettings = null;

		private uid clientID = 0;

		private Teleport_Head head = null;
		private Dictionary<int, Teleport_Controller> controllers = new Dictionary<int, Teleport_Controller>();

		private List<Collider> streamedObjects = new List<Collider>();
		private List<Light> streamedLights = new List<Light>();

		public void Disconnect()
		{
			sessions.Remove(clientID);

			casterMonitor.geometryStreamingService.RemoveAllActors(clientID);
			streamedObjects.Clear();
			streamedLights.Clear();

			clientID = 0;
		}

		public void SetHeadPose(Quaternion newRotation, Vector3 newPosition)
		{
			if (!head)
				return;

			head.transform.rotation = newRotation;
			head.transform.position = newPosition;
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
			casterMonitor = CasterMonitor.GetCasterMonitor();
			teleportSettings = TeleportSettings.GetOrCreateSettings();
			Teleport_Head[] heads = GetComponentsInChildren<Teleport_Head>();
			if (heads.Length != 1)
			{
				Debug.LogError("Precisely ONE Teleport_Head should be found.");
			}
			head = heads[0];
		}
		Vector3 last_sent_origin = new Vector3(0, 0, 0);
		private void LateUpdate()
		{
			if(clientID == 0)
			{
				clientID = GetUnlinkedClientID();
				if(clientID == 0)
				return;

				if(sessions.ContainsKey(clientID))
				{
					Debug.LogError("Session duplicate key!");
				}
				sessions[clientID] = this;
			}

			if(Client_IsConnected(clientID))
			{
				if(head!=null&&(!Client_HasOrigin(clientID)))//||transform.hasChanged))
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
				UpdateGeometryStreaming();
			}
		}
		public void ShowOverlay(int x, int y, GUIStyle font)
		{
			string str=string.Format("Client {0}", clientID);
			int dy = 14;
			GUI.Label(new Rect(x, y+=dy, 300, 20), str, font);
			GUI.Label(new Rect(x, y+=dy, 300, 20), string.Format("sent origin   {0},{1},{2}", last_sent_origin.x, last_sent_origin.y, last_sent_origin.z), font);
			GUI.Label(new Rect(x, y+=dy, 300, 20), string.Format("head position {0},{1},{2}", head.transform.position.x, head.transform.position.y, head.transform.position.z), font);

			GUI.Label(new Rect(x, y += dy, 300, 20), string.Format("Actors {0}", streamedObjects.Count()));
			GUI.Label(new Rect(x, y += dy, 300, 20), string.Format("Lights {0}", streamedLights.Count()));
		}
		private void OnDestroy()
		{
			if(clientID != 0)
				StopSession(clientID);
		}

		public void SetVisibleLights( Light[] lights)
		{
			foreach (var l in lights)
			{
				uid actorID = casterMonitor.geometryStreamingService.AddActor(clientID, l.gameObject);
				if(!streamedLights.Contains(l))
					streamedLights.Add(l);
			}
		}
		private void UpdateGeometryStreaming()
		{
			if(teleportSettings.LayersToStream != 0)
			{
				List<Collider> innerSphereCollisions = new List<Collider>(Physics.OverlapSphere(transform.position, teleportSettings.casterSettings.detectionSphereRadius, teleportSettings.LayersToStream));
				List<Collider> outerSphereCollisions = new List<Collider>(Physics.OverlapSphere(transform.position, teleportSettings.casterSettings.detectionSphereRadius + teleportSettings.casterSettings.detectionSphereBufferDistance, teleportSettings.LayersToStream));

				List<Collider> gainedColliders = new List<Collider>(innerSphereCollisions.Except(streamedObjects));
				List<Collider> lostColliders = new List<Collider>(streamedObjects.Except(outerSphereCollisions));

				foreach(Collider collider in gainedColliders)
				{
					//Skip game objects without the streaming tag.
					if(collider.tag != teleportSettings.TagToStream)
						continue;
					uid actorID = casterMonitor.geometryStreamingService.AddActor(clientID, collider.gameObject);
					if(actorID != 0)
					{
						streamedObjects.Add(collider);
						ActorEnteredBounds(clientID, actorID);
					}
					else
					{
						Debug.LogWarning("Failed to add game object to stream: " + collider.gameObject.name);
					}
				}
				foreach(Collider collider in lostColliders)
				{
					streamedObjects.Remove(collider);

					uid actorID = casterMonitor.geometryStreamingService.RemoveActor(clientID, collider.gameObject);
					if(actorID != 0)
					{
						ActorLeftBounds(clientID, actorID);
					}
					else
					{
						Debug.LogWarning("Attempted to remove actor that was not being streamed: " + collider.gameObject.name);
					}
				}
			}
			else
			{
				Debug.LogError("Teleport geometry streaming layer is not defined! Please assign layer masks under \"Layers To Stream\".");
			}
		}
	}
}