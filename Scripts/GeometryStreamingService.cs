﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

using uid = System.UInt64;

namespace teleport
{
	public class GeometryStreamingService
	{
		#region DLLImports
		[DllImport("SimulCasterServer")]
		private static extern void Client_AddNode(uid clientID, uid nodeID, avs.Transform currentTransform);
		[DllImport("SimulCasterServer")]
		private static extern void Client_RemoveNodeByID(uid clientID, uid nodeID);

		[DllImport("SimulCasterServer")]
		public static extern void Client_NodeEnteredBounds(uid clientID, uid nodeID);
		[DllImport("SimulCasterServer")]
		public static extern void Client_NodeLeftBounds(uid clientID, uid nodeID);
		[DllImport("SimulCasterServer")]
		private static extern bool Client_IsStreamingNodeID(uid clientID, uid nodeID);

		[DllImport("SimulCasterServer")]
		public static extern void Client_ShowNode(uid clientID, uid nodeID);
		[DllImport("SimulCasterServer")]
		public static extern void Client_HideNode(uid clientID, uid nodeID);
		[DllImport("SimulCasterServer")]
		public static extern void Client_SetNodeVisible(uid clientID, uid nodeID, bool isVisible);
		[DllImport("SimulCasterServer")]
		public static extern bool Client_IsClientRenderingNodeID(uid clientID, uid nodeID);

		[DllImport("SimulCasterServer")]
		public static extern bool Client_HasResource(uid clientID, uid resourceID);

		[DllImport("SimulCasterServer")]
		private static extern void Client_UpdateNodeMovement(uid clientID, avs.MovementUpdate[] updates, int updateAmount);
		#endregion

		private readonly Teleport_SessionComponent session;
		private readonly TeleportSettings teleportSettings;

		private List<Collider> streamedColliders = new List<Collider>();
		private List<GameObject> streamedGameObjects = new List<GameObject>();
		private List<GameObject> failedGameObjects = new List<GameObject>(); //Objects that have already failed to stream, and as such we won't attempt to stream again.
		private Dictionary<uid,Light> streamedLights = new Dictionary<uid, Light>();

		//Stores handles to game objects, so the garbage collector doesn't move/delete the objects while they're being referenced by the native plug-in.
		// Roderick's note: this does not appear to work. Instead, we get null pointers.
		//private Dictionary<GameObject, GCHandle> gameObjectHandles = new Dictionary<GameObject, GCHandle>();

		private Dictionary<uid, avs.MovementUpdate> previousMovements = new Dictionary<uid, avs.MovementUpdate>();
		private float timeSincePositionUpdate = 0;

		public GeometryStreamingService(Teleport_SessionComponent parentComponent)
		{
			session = parentComponent;

			teleportSettings = TeleportSettings.GetOrCreateSettings();
			timeSincePositionUpdate = 1 / teleportSettings.moveUpdatesPerSecond;
		}

		public void Clear()
		{
			RemoveAllNodes();

			streamedColliders.Clear();
			streamedGameObjects.Clear();
			failedGameObjects.Clear();
			streamedLights.Clear();
			previousMovements.Clear();

			if(session.HasClient())
			{
				StreamPlayerBody();
			}
        }

		public void RemoveAllNodes()
		{
			foreach(GameObject gameObject in streamedGameObjects)
			{
				Client_RemoveNodeByID(session.GetClientID(), gameObject.GetComponent<Teleport_Streamable>().GetUid());
			}
		}

		uid AddNode(GameObject node)
		{
			GeometrySource geometrySource = GeometrySource.GetGeometrySource();

			uid nodeID = geometrySource.AddNode(node);
			if(nodeID == 0)
			{
				Debug.LogError($"AddNode failed for {node.name}. Received 0 for node ID.");
			}
			else
			{
				Client_AddNode(session.GetClientID(), nodeID, avs.Transform.FromGlobalUnityTransform(node.transform));
			}

			return nodeID;
		}

		public bool IsStreamingNode(GameObject gameObject)
		{
			GeometrySource geometrySource = GeometrySource.GetGeometrySource();
			uid nodeID = geometrySource.FindResourceID(gameObject);
			if(nodeID == 0)
			{
				return false;
			}

			return Client_IsStreamingNodeID(session.GetClientID(), nodeID);
		}

		/// <summary>
		/// Set the lights to be streamed to this client.
		///		If they are streamable, this will:
		///			- ensure that their nodes are streamed.
		///			- tell the client that these are the active lights.
		///		If not streamable, the lights will not be sent.
		/// </summary>
		/// <param name="lights"></param>
		public void SetStreamedLights(Light[] lights)
		{
			HashSet<uid> streamedNow = new HashSet<uid>();
			foreach(Light light in lights)
			{
				// if the light is not streamable, make it so.
				var streamable = light.gameObject.GetComponentInParent<Teleport_Streamable>();
				if (!streamable)
				{
					streamable=light.gameObject.AddComponent<Teleport_Streamable>();
				}
				if (!streamedGameObjects.Contains(streamable.gameObject))
				{
					StartStreamingGameObject(streamable.gameObject);
				}
				var uid = streamable.GetUid();
				if (uid == 0)
					continue;
				streamedNow.Add(uid);
				if (!streamedLights.ContainsKey(uid))
				{
					streamedLights[uid] = light;
				}
			}
			while(streamedLights.Count> streamedNow.Count)
			foreach(var u in streamedLights)
			{
				if(!streamedNow.Contains(u.Key))
				{
					streamedLights.Remove(u.Key);
					if (u.Value != null)
					{
						var streamable = u.Value.GetComponentInParent<Teleport_Streamable>();
						StopStreamingGameObject(streamable.gameObject);
					}
					break;
				}
			}
		}

		public int GetStreamedObjectCount()
		{
			return streamedGameObjects.Count;
		}

		public List<GameObject> GetStreamedObjects()
		{
			return streamedGameObjects;
		}

		public List<uid> GetStreamedObjectIDs()
		{
			List<uid> uids = new List<uid>();

			GeometrySource geometrySource = GeometrySource.GetGeometrySource();
			foreach(GameObject gameObject in streamedGameObjects)
			{
				uid nodeID = geometrySource.FindResourceID(gameObject);
				if(nodeID != 0)
				{
					uids.Add(nodeID);
				}
			}

			return uids;
		}

		public int GetStreamedLightCount()
		{
			return streamedLights.Count;
		}

		public Dictionary<uid, Light> GetStreamedLights()
		{
			return streamedLights;
		}

		public void StreamPlayerBody()
		{
			CasterMonitor monitor = CasterMonitor.GetCasterMonitor();
			List<GameObject> bodyParts = monitor.GetPlayerBodyParts();
			foreach(GameObject part in bodyParts)
			{
				StartStreamingGameObject(part);
			}
		}

		public void UpdateGeometryStreaming()
		{
			//Detect changes in geometry that needs to be streamed to the client.
			if(teleportSettings.LayersToStream != 0)
			{
				List<Collider> innerSphereCollisions = new List<Collider>(Physics.OverlapSphere(session.head.transform.position, teleportSettings.casterSettings.detectionSphereRadius, teleportSettings.LayersToStream));
				List<Collider> outerSphereCollisions = new List<Collider>(Physics.OverlapSphere(session.head.transform.position, teleportSettings.casterSettings.detectionSphereRadius + teleportSettings.casterSettings.detectionSphereBufferDistance, teleportSettings.LayersToStream));

				List<Collider> gainedColliders = new List<Collider>(innerSphereCollisions.Except(streamedColliders));
				List<Collider> lostColliders = new List<Collider>(streamedColliders.Except(outerSphereCollisions));

				foreach (Collider collider in gainedColliders)
				{			
					//Skip game objects without the streaming tag.
					if (teleportSettings.TagToStream.Length == 0 || collider.CompareTag(teleportSettings.TagToStream))
					{
						StartStreamingGameObject(collider.gameObject);
					}
				}

				foreach(Collider collider in lostColliders)
				{
					StopStreamingGameObject(collider.gameObject);
				}
			}
			else
			{
				TeleportLog.LogErrorOnce("Teleport geometry streaming layer is not defined! Please assign layer masks under \"Layers To Stream\".");
			}

			//Send position updates, if enough time has elapsed.
			timeSincePositionUpdate += Time.deltaTime;
			if(session.IsConnected() && timeSincePositionUpdate >= 1.0f / teleportSettings.moveUpdatesPerSecond)
			{
				timeSincePositionUpdate = 0;

				DetectInvalidStreamables();
				SendPositionUpdates();
			}
		}

		private bool StartStreamingGameObject(GameObject gameObject)
		{
			if (failedGameObjects.Contains(gameObject))
			{
				return false;
			}

			if (streamedGameObjects.Contains(gameObject))
			{
				Debug.LogError($"StartStreamingGameObject called on {gameObject.name}, which is already streamed.");
				return false;
			}

			Teleport_Streamable streamable = gameObject.GetComponent<Teleport_Streamable>();
			if (streamable == null)
			{
				Debug.LogError($"Attempted to stream GameObject \"{gameObject.name}\", which has no Teleport_Streamable component!");
				return false;
			}

			uid gameObjectID = AddNode(gameObject);
			if(gameObjectID != 0)
			{
				streamable.SetUid(gameObjectID);
				streamable.AddStreamingClient(session);

				streamedGameObjects.Add(gameObject);
				Client_NodeEnteredBounds(session.GetClientID(), gameObjectID);
			}
			else
			{
				failedGameObjects.Add(gameObject);
				Debug.LogWarning($"Failed to add GameObject <b>\"{gameObject.name}\"</b> for streaming! ");
			}

			//Stream child hierarchy this GameObject Streamable is responsible for.
			foreach(GameObject node in streamable.childHierarchy)
			{
				if(failedGameObjects.Contains(node))
				{
					continue;
				}

				if(streamedGameObjects.Contains(node))
				{
					continue;
				}

				uid childID = AddNode(node);
				if (childID == 0)
				{
					failedGameObjects.Add(node);
					Debug.LogWarning($"Failed to add GameObject <b>\"{node.name}\"</b> for streaming! ");
				}
				else
				{
					streamedGameObjects.Add(node);
					Client_NodeEnteredBounds(session.GetClientID(), childID);
				}
			}
			Collider[] colliders = gameObject.GetComponents<Collider>();
			foreach(Collider collider in colliders)
			{
				streamedColliders.Add(collider);
			}
			return true;
		}

		public void StopStreamingGameObject(GameObject gameObject)
		{
			GeometrySource geometrySource = GeometrySource.GetGeometrySource();
			uid gameObjectID = geometrySource.FindResourceID(gameObject);

			streamedGameObjects.Remove(gameObject);
			Client_RemoveNodeByID(session.GetClientID(), gameObjectID);
			Client_NodeLeftBounds(session.GetClientID(), gameObjectID);

			Teleport_Streamable streamable = gameObject.GetComponent<Teleport_Streamable>();
			if(streamable)
			{
				streamable.RemoveStreamingClient(session);
			}
			else
			{
				Debug.LogError($"GameObject \"{gameObject.name}\" has no Teleport_Streamable component.");
			}

			//Stop streaming child hierarchy.
			foreach(GameObject child in streamable.childHierarchy)
			{
				uid childID = geometrySource.FindResourceID(child);
				if(childID != 0)
				{
					streamedGameObjects.Remove(child);
					Client_RemoveNodeByID(session.GetClientID(), childID);
					Client_NodeLeftBounds(session.GetClientID(), childID);
				}
				else
				{
					Debug.LogWarning($"Attempted to stop streaming GameObject <b>{child.name}</b> (child of {gameObject.name}({gameObjectID})), but received 0 for ID from GeometrySource!");
				}
			}

			//Remove GameObject's colliders from list.
			Collider[] colliders = gameObject.GetComponents<Collider>();
			foreach(Collider collider in colliders)
				{
				streamedColliders.Remove(collider);
				}
			}

		private void DetectInvalidStreamables()
		{
			for(int i = streamedColliders.Count - 1; i >= 0; i--)
			{
				Collider collider = streamedColliders[i];
				if(!collider.CompareTag(teleportSettings.TagToStream))
				{
					StopStreamingGameObject(collider.gameObject);
				}
			}
		}

		private void SendPositionUpdates()
		{
			TeleportSettings teleportSettings = TeleportSettings.GetOrCreateSettings();
			GeometrySource geometrySource = GeometrySource.GetGeometrySource();
			if(teleportSettings == null || geometrySource == null)
			{
				return;
			}

			avs.MovementUpdate[] updates = new avs.MovementUpdate[streamedGameObjects.Count];
			long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

			int i = 0;
			foreach(GameObject node in streamedGameObjects)
			{
				if(node == null)
				{
					Debug.LogWarning($"Attempted to update movement of \"{node.name}\", but it has an ID of 0!");
					continue;
				}

				uid nodeID = geometrySource.FindResourceID(node);

				if(nodeID == 0)
				{
					Debug.LogWarning($"Attempted to update movement of \"{node.name}\", but it has an ID of zero.");
					continue;
				}

				updates[i].timestamp = timestamp;
				updates[i].nodeID = nodeID;

				if(IsClientRenderingParent(node))
				{
					updates[i].isGlobal = false;
					updates[i].position = node.transform.localPosition;
					updates[i].rotation = node.transform.localRotation;
				}
				else
				{
					updates[i].isGlobal = true;
					updates[i].position = node.transform.position;
					updates[i].rotation = node.transform.rotation;
				}

				if(previousMovements.TryGetValue(nodeID, out avs.MovementUpdate previousMovement))
				{
					Vector3 position;
					Quaternion rotation;

					//Velocity and angular velocity must be calculated in the same basis as the previous movement.
					if(previousMovement.isGlobal)
					{
						position = node.transform.position;
						rotation = node.transform.rotation;
					}
					else
					{
						position = node.transform.localPosition;
						rotation = node.transform.localRotation;
					}

					//We cast to the unity engine types to take advantage of the existing vector subtraction operators.
					//We multiply by the amount of move updates per second to get the movement per second, rather than per update.
					updates[i].velocity = (position - previousMovement.position) * teleportSettings.moveUpdatesPerSecond;

					(rotation * Quaternion.Inverse(previousMovement.rotation)).ToAngleAxis(out updates[i].angularVelocityAngle, out Vector3 angularVelocityAxis);
					updates[i].angularVelocityAxis = angularVelocityAxis;
					//Angle needs to be inverted, for some reason.
					updates[i].angularVelocityAngle *= teleportSettings.moveUpdatesPerSecond * -Mathf.Deg2Rad;
				}

				previousMovements[nodeID] = updates[i];

				++i;
			}

			Client_UpdateNodeMovement(session.GetClientID(), updates, updates.Length);
		}

		private bool IsClientRenderingParent(GameObject child)
        {
			if(child.transform.parent)
			{
				uid parentID = GeometrySource.GetGeometrySource().FindResourceID(child.transform.parent.gameObject);
				if(parentID == 0)
				{
					return false;
				}

				return Client_IsClientRenderingNodeID(session.GetClientID(), parentID);
			}

			return false;
		}
	}
}