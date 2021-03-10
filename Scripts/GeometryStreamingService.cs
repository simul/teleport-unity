using System;
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

		private readonly Teleport_SessionComponent session = null;
		private readonly TeleportSettings teleportSettings = null;

		private List<Collider> streamedColliders = new List<Collider>();
		private List<GameObject> streamedGameObjects = new List<GameObject>();
		private List<Teleport_Streamable> streamedHierarchies = new List<Teleport_Streamable>();
		//Objects that have already failed to stream, and as such we won't attempt to stream again.
		private List<GameObject> failedGameObjects = new List<GameObject>();
		private Dictionary<uid,Light> streamedLights = new Dictionary<uid, Light>();

		private float timeSincePositionUpdate = 0;

		static public bool IsClientRenderingParent(uid clientID, GameObject gameObject)
		{
			if(gameObject.transform.parent)
			{
				uid parentID = GeometrySource.GetGeometrySource().FindResourceID(gameObject.transform.parent.gameObject);
				if(parentID == 0)
				{
					return false;
				}

				return Client_IsClientRenderingNodeID(clientID, parentID);
			}

			return false;
		}

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
			streamedHierarchies.Clear();
			failedGameObjects.Clear();
			streamedLights.Clear();
        }

		public void RemoveAllNodes()
		{
			foreach(Teleport_Streamable streamableComponent in streamedHierarchies)
			{
				Client_RemoveNodeByID(session.GetClientID(), streamableComponent.GetUid());
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
				Client_AddNode(session.GetClientID(), nodeID, avs.Transform.FromLocalUnityTransform(node.transform));
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
					StartStreaming(streamable,4);
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
						if (streamable != null)
						{
							StopStreaming(streamable,4);
						}
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
				Teleport_Streamable streamable = part.GetComponent<Teleport_Streamable>();
				StartStreaming(streamable,2);
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
						Teleport_Streamable streamable = collider.gameObject.GetComponent<Teleport_Streamable>();
						StartStreaming(streamable, 1);
					}
				}

				foreach(Collider collider in lostColliders)
				{
					Teleport_Streamable streamable = collider.gameObject.GetComponent<Teleport_Streamable>();
					StopStreaming(streamable, 1);
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
		// Start streaming the given streamable gameObject and its hierarchy.
		private bool StartStreaming(Teleport_Streamable streamable, UInt32 streaming_reason)
		{
			GameObject gameObject= streamable.gameObject;
			if (failedGameObjects.Contains(gameObject))
			{
				return false;
			}

			if (streamedGameObjects.Contains(gameObject))
			{
				if((streamable.streaming_reason & streaming_reason) != 0)
				{ 
					Debug.LogError($"StartStreaming called on {gameObject.name} for reason {streaming_reason}, but this was already known.");
			}
				else
			{
					streamable.streaming_reason |= streaming_reason;
				}
				return false;
			}
			streamable.streaming_reason |= streaming_reason;
			streamedHierarchies.Add(streamable);

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

		public bool StopStreaming(Teleport_Streamable streamable, UInt32 streaming_reason)
		{
			GameObject gameObject = streamable.gameObject;
			streamable.streaming_reason &= (~streaming_reason);
			if(streamable.streaming_reason!=0)
				return false;
			GeometrySource geometrySource = GeometrySource.GetGeometrySource();
			uid gameObjectID = geometrySource.FindResourceID(gameObject);

			streamedGameObjects.Remove(gameObject);
			Client_RemoveNodeByID(session.GetClientID(), gameObjectID);
			Client_NodeLeftBounds(session.GetClientID(), gameObjectID);

				streamable.RemoveStreamingClient(session);
				streamedHierarchies.Remove(streamable);

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
			return true;
		}

		private void DetectInvalidStreamables()
		{
			for(int i = streamedColliders.Count - 1; i >= 0; i--)
			{
				Collider collider = streamedColliders[i];
				if(!collider.CompareTag(teleportSettings.TagToStream))
				{
					Teleport_Streamable streamable = collider.gameObject.GetComponent<Teleport_Streamable>();
					StopStreaming(streamable,1);
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

			List<avs.MovementUpdate> updates = new List<avs.MovementUpdate>();
			foreach(Teleport_Streamable hierarchy in streamedHierarchies)
			{
				updates.AddRange(hierarchy.GetMovementUpdates(session.GetClientID()));
			}

			Client_UpdateNodeMovement(session.GetClientID(), updates.ToArray(), updates.Count);
		}
	}
}