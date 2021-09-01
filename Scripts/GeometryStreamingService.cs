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
		private static extern void Client_AddGenericTexture(uid clientID, uid textureID);
		[DllImport("SimulCasterServer")]
		public static extern void Client_SetGlobalIlluminationTextures(uid clientID, UInt64 num, uid[] textureIDs);
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


		[DllImport("SimulCasterServer")]
		private static extern void Client_UpdateNodeEnabledState(uid clientID, avs.NodeUpdateEnabledState[] updates, int updateAmount);
		[DllImport("SimulCasterServer")]
		private static extern void Client_SetNodeHighlighted(uid clientID, uid nodeID, bool isHighlighted);

		#endregion

		private readonly Teleport_SessionComponent session = null;
		private readonly TeleportSettings teleportSettings = null;

		private List<Collider> streamedColliders = new List<Collider>();
		private List<GameObject> streamedGameObjects = new List<GameObject>();
		private List<Teleport_Streamable> streamedHierarchies = new List<Teleport_Streamable>();
		/// <summary>
		///  Lights that are streamed to the client.
		/// </summary>
		private Dictionary<uid,Light> streamedLights = new Dictionary<uid, Light>();
		/// <summary>
		/// Lights that are baked in realtime into the 
		/// </summary>
		private HashSet< Light> bakedLights = new HashSet< Light>();

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
			streamedLights.Clear();
			bakedLights.Clear();

		}

		public void RemoveAllNodes()
		{
			foreach(Teleport_Streamable streamableComponent in streamedHierarchies)
			{
				streamableComponent.RemoveStreamingClient(session);
				Client_RemoveNodeByID(session.GetClientID(), streamableComponent.GetUid());
			}
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
		bool ShouldBake(Light light)
		{
			return true;
		}
		/// <summary>
		/// Set the lights to be streamed to this client.
		///		If they are streamable, this will:
		///			- ensure that their nodes are streamed.
		///			- tell the client that these are the active lights.
		///		If not streamable, the lights will not be sent.
		///		The lights will either be streamed or baked.
		/// </summary>
		/// <param name="lights"></param>
		public void SetStreamedLights(Light[] lights)
		{
			// Let us assume that these lights are given in order of priority.
			HashSet<uid> streamedNow = new HashSet<uid>();
			HashSet<Light> bakedNow = new HashSet<Light>();
			foreach (Light light in lights)
			{
				// if the light is not streamable, make it so.
				if(ShouldBake(light))
				{
					bakedNow.Add(light);
					if (!bakedLights.Contains(light))
					{
						bakedLights.Add(light);
					}
				}
				else
				{
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
			}
			while (bakedLights.Count > bakedNow.Count)
			{
				foreach (var u in bakedLights)
				{
					if (!bakedNow.Contains(u))
					{
						bakedLights.Remove(u);
						break;
					}
				}
			}
			while (streamedLights.Count>streamedNow.Count)
			{
				foreach (var u in streamedLights)
				{
					if (!streamedNow.Contains(u.Key))
					{
						streamedLights.Remove(u.Key);
						if (u.Value != null)
						{
							var streamable = u.Value.GetComponentInParent<Teleport_Streamable>();
							if (streamable != null)
							{
								StopStreaming(streamable, 4);
							}
						}
						break;
					}
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

		public HashSet<Light> GetBakedLights()
		{
			return bakedLights;
		}

		public void StreamGlobals()
		{
			StreamGenericTextures();
			StreamPlayerBody();
		}
		public void StreamGenericTextures()
		{
			uid [] textureIDs= GeometrySource.GetGeometrySource().FindResourceIDs(GlobalIlluminationExtractor.GetTextures());
			Client_SetGlobalIlluminationTextures(session.GetClientID(), (UInt64)textureIDs.Length, textureIDs);
		}
		public void StreamPlayerBody()
		{
			CasterMonitor monitor = CasterMonitor.GetCasterMonitor();
			List<GameObject> bodyParts = monitor.GetPlayerBodyParts();
			foreach(GameObject part in bodyParts)
			{
				Teleport_Streamable streamable = part.GetComponent<Teleport_Streamable>();
				StartStreaming(streamable, 2);
			}
		}

		public void SendAnimationState()
		{
			foreach(Teleport_Streamable streamable in streamedHierarchies)
			{
				streamable.SendAnimationState(session.GetClientID());
			}
		}

		public void SetNodeHighlighted(GameObject gameObject, bool isHighlighted)
		{
			uid nodeID = GeometrySource.GetGeometrySource().FindResourceID(gameObject);
			if(nodeID != 0)
			{
				Client_SetNodeHighlighted(session.GetClientID(), nodeID, isHighlighted);
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
						//GameObject became streamable mid-session, and now needs a Teleport_Streamable component.
						if(!streamable)
						{
							streamable = collider.gameObject.AddComponent<Teleport_Streamable>();
						}
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

				StopStreamingUntaggedStreamables();
				SendPositionUpdates();
				SendEnabledStateUpdates();
			}
		}

		// Start streaming the given streamable gameObject and its hierarchy.
		private bool StartStreaming(Teleport_Streamable streamable, UInt32 streaming_reason)
		{
			GameObject gameObject = streamable.gameObject;

			if(streamedGameObjects.Contains(gameObject))
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
			streamable.AddStreamingClient(session);
			streamedHierarchies.Add(streamable);

			//Stream Teleport_Streamable's hierarchy.
			foreach(StreamedNode streamedNode in streamable.streamedHierarchy)
			{
				if(streamedGameObjects.Contains(streamedNode))
				{
					continue;
				}

				Client_AddNode(session.GetClientID(), streamedNode.nodeID, avs.Transform.FromLocalUnityTransform(streamedNode.gameObject.transform));
				
				Client_NodeEnteredBounds(session.GetClientID(), streamedNode.nodeID);
				streamedGameObjects.Add(streamedNode);
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
			streamable.streaming_reason &= ~streaming_reason;
			if(streamable.streaming_reason != 0)
			{
				return false;
			}

			streamable.RemoveStreamingClient(session);
			streamedHierarchies.Remove(streamable);

			//Stop streaming hierarchy.
			foreach(StreamedNode streamedNode in streamable.streamedHierarchy)
			{
				streamedGameObjects.Remove(streamedNode);
				Client_RemoveNodeByID(session.GetClientID(), streamedNode.nodeID);
				Client_NodeLeftBounds(session.GetClientID(), streamedNode.nodeID);
			}

			//Remove GameObject's colliders from list.
			Collider[] colliders = streamable.GetComponents<Collider>();
			foreach(Collider collider in colliders)
			{
				streamedColliders.Remove(collider);
			}

			return true;
		}

		private void StopStreamingUntaggedStreamables()
		{
			for(int i = streamedHierarchies.Count - 1; i >= 0; i--)
			{
				Teleport_Streamable streamable = streamedHierarchies[i];
				if(!GeometrySource.GetGeometrySource().IsGameObjectMarkedForStreaming(streamable.gameObject) && (streamable.streaming_reason & 1) != 0)
				{
					StopStreaming(streamable, 1);
				}
			}
		}

		private void SendPositionUpdates()
		{
			List<avs.MovementUpdate> updates = new List<avs.MovementUpdate>();
			foreach(Teleport_Streamable streamable in streamedHierarchies)
			{
				updates.AddRange(streamable.GetMovementUpdates(session.GetClientID()));
			}

			Client_UpdateNodeMovement(session.GetClientID(), updates.ToArray(), updates.Count);
		}

		private void SendEnabledStateUpdates()
		{
			List<avs.NodeUpdateEnabledState> updates = new List<avs.NodeUpdateEnabledState>();
			foreach(Teleport_Streamable streamable in streamedHierarchies)
			{
				updates.AddRange(streamable.GetEnabledStateUpdates());
			}

			//Don't send an update command, if there were no updates.
			if(updates.Count == 0)
			{
				return;
			}

			Client_UpdateNodeEnabledState(session.GetClientID(), updates.ToArray(), updates.Count);
		}
	}
}