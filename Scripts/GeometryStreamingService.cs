using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using uid = System.UInt64;

namespace teleport
{
	public enum StreamingReason: uint
	{
		NEARBY=1,
		CLIENT_SELF=2,
		LIGHT=4,
		ALL= 0xFFFFFFFF
	}

	// One of these per-session.
	public class GeometryStreamingService
	{
		#region DLLImports
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
		const string dllName="TeleportServer";
#else
		const string dllName="TeleportServer.so";
#endif

		[DllImport(TeleportServerDll.name)]
		private static extern UInt64 Client_GetNumNodesToStream(uid clientID);
		[DllImport(TeleportServerDll.name)]
		private static extern UInt64 Client_GetNumNodesCurrentlyStreaming(uid clientID);

		[DllImport(TeleportServerDll.name)]
		private static extern void Client_AddGenericTexture(uid clientID, uid textureID);
		[DllImport(TeleportServerDll.name)]
		public static extern void Client_SetGlobalIlluminationTextures(uid clientID, UInt64 num, uid[] textureIDs);
		[DllImport(TeleportServerDll.name)]
		public static extern bool Client_StreamNode(uid clientID, uid nodeID);
		[DllImport(TeleportServerDll.name)]
		public static extern bool Client_UnstreamNode(uid clientID, uid nodeID);
		[DllImport(TeleportServerDll.name)]
		private static extern bool Client_IsStreamingNodeID(uid clientID, uid nodeID);
		[DllImport(TeleportServerDll.name)]
		public static extern bool Client_IsClientRenderingNodeID(uid clientID, uid nodeID);
		[DllImport(TeleportServerDll.name)]
		public static extern bool Client_HasResource(uid clientID, uid resourceID);
		[DllImport(TeleportServerDll.name)]
		private static extern void Client_UpdateNodeMovement(uid clientID, avs.MovementUpdate[] updates, int updateAmount);


		[DllImport(TeleportServerDll.name)]
		private static extern void Client_UpdateNodeEnabledState(uid clientID, avs.NodeUpdateEnabledState[] updates, int updateAmount);
		[DllImport(TeleportServerDll.name)]
		private static extern void Client_SetNodeHighlighted(uid clientID, uid nodeID, bool isHighlighted);
		[DllImport(TeleportServerDll.name)]
		private static extern void Client_ReparentNode(uid clientID, uid nodeID, uid newParentNodeID,avs.Pose localPose);

		[DllImport(TeleportServerDll.name)]
		private static extern void Client_SetNodePosePath(uid clientID, uid nodeID,  string regexPosePath);

		
		#endregion

		private readonly Teleport_SessionComponent session = null;
		private readonly TeleportSettings teleportSettings = null;

		public IStreamedGeometryManagement streamedGeometryManagement = null;

		private List<Collider> streamedColliders = new List<Collider>();
		private List<GameObject> streamedGameObjects = new List<GameObject>();
		private List<teleport.StreamableRoot> streamedHierarchies = new List<teleport.StreamableRoot>();
		/// <summary>
		///  Lights that are streamed to the client.
		/// </summary>
		private Dictionary<uid,Light> streamedLights = new Dictionary<uid, Light>();
		/// <summary>
		/// Lights that are baked in realtime into the 
		/// </summary>
		private HashSet< Light> bakedLights = new HashSet< Light>();

		private float timeSincePositionUpdate = 0;
		private float timeSinceEnabledStateUpdate=0;
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
			timeSincePositionUpdate = 1.0F / teleportSettings.moveUpdatesPerSecond;
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
			foreach(teleport.StreamableRoot streamableComponent in streamedHierarchies)
			{
				streamableComponent.RemoveStreamingClient(session);
				Client_UnstreamNode(session.GetClientID(), streamableComponent.GetUid());
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
					var streamable = light.gameObject.GetComponentInParent<teleport.StreamableRoot>();
					if (!streamable)
					{
						streamable=light.gameObject.AddComponent<teleport.StreamableRoot>();
					}
					if (!streamedGameObjects.Contains(streamable.gameObject))
					{
						StartStreaming(streamable, StreamingReason.LIGHT);
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
							var streamable = u.Value.GetComponentInParent<teleport.StreamableRoot>();
							if (streamable != null)
							{
								StopStreaming(streamable, StreamingReason.LIGHT);
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
			if(textureIDs!=null)
				Client_SetGlobalIlluminationTextures(session.GetClientID(), (UInt64)textureIDs.Length, textureIDs);
			if(session.clientSettings.backgroundMode==BackgroundMode.TEXTURE&& session.clientSettings.backgroundTexture!=0)
				Client_AddGenericTexture(session.GetClientID(), session.clientSettings.backgroundTexture);
		}
		public void StreamPlayerBody()
		{
			List<GameObject> bodyParts = session.GetPlayerBodyParts();
			foreach(GameObject part in bodyParts)
			{
				teleport.StreamableRoot streamable = part.GetComponent<teleport.StreamableRoot>();
				streamable.OwnerClient=session.GetClientID();
				StartStreaming(streamable, StreamingReason.CLIENT_SELF);
			}
			teleport.StreamableRoot session_streamable = session.GetComponentInChildren<Teleport_ClientspaceRoot>().GetComponent<teleport.StreamableRoot>();
			StartStreaming(session_streamable, StreamingReason.CLIENT_SELF);
		}

		public void SendAnimationState()
		{
			foreach(teleport.StreamableRoot streamable in streamedHierarchies)
			{
				streamable.SendAnimationState(session.GetClientID());
			}
		}
		/// <summary>sss
		/// Tell this client to highlight the specified node.
		/// </summary>
		/// <param name="gameObject">The object to highlight</param>
		/// <param name="isHighlighted">Whether to activate or deactivate highlighting.</param>
		public void SetNodeHighlighted(GameObject gameObject, bool isHighlighted)
		{
			uid nodeID = GeometrySource.GetGeometrySource().FindResourceID(gameObject);
			if(nodeID != 0)
			{
			//	Debug.Log("Highlight " + gameObject.name + " " + isHighlighted);
				Client_SetNodeHighlighted(session.GetClientID(), nodeID, isHighlighted);
			}
		}

		public void SetNodePosePath(GameObject gameObject, string regexPosePath)
		{
			GeometrySource geometrySource=GeometrySource.GetGeometrySource();
			uid nodeID = geometrySource.FindOrAddNodeID(gameObject);
			if (nodeID == 0)
			{
				Debug.LogError("Node id not found for "+gameObject.name);
			}
			else
			{
				var clID= session.GetClientID();
				// for now we assume that these types have no parent.
				if (clID>0)
					Client_SetNodePosePath(clID, nodeID, regexPosePath);
			}
		}
		public void ChangeNodeVisibility(GameObject gameObject, bool enabled)
		{
			var streamableNode = gameObject.GetComponent<StreamableNode>();
			if(streamableNode != null)
			{
				avs.NodeUpdateEnabledState [] states=new avs.NodeUpdateEnabledState[1];
				states[0].nodeID=streamableNode.nodeID;
				states[0].enabled=enabled;
				Client_UpdateNodeEnabledState(streamableNode.nodeID, states,1);
			}
		}
		public void ReparentNode(GameObject child, GameObject newParent, Vector3 relativePos, Quaternion relativeRot)
		{
			uid childNodeID = GeometrySource.GetGeometrySource().FindResourceID(child);
			if (childNodeID != 0)
			{
				avs.Pose relativePose;
				relativePose.position=relativePos;
				relativePose.orientation=relativeRot;
				uid newParentNodeID = GeometrySource.GetGeometrySource().FindResourceID(newParent);
				if (newParentNodeID != 0)
				{
					if(session.ownedNodes.Contains(newParentNodeID))
						session.ownedNodes.Add(childNodeID);
					Client_ReparentNode(session.GetClientID(), childNodeID,  newParentNodeID,  relativePose);
				}
				else
				{
					if (session.ownedNodes.Contains(childNodeID))
						session.ownedNodes.Remove(childNodeID);
					Client_ReparentNode(session.GetClientID(), childNodeID, newParentNodeID,  relativePose);
				}
			}
		}
		Collider[] innerOverlappingColliders=new Collider[10];
		Collider[] outerOverlappingColliders = new Collider[10];
		HashSet<teleport.StreamableRoot> outerStreamables = new HashSet<teleport.StreamableRoot>();
		int inner_overlap_count=0;
		int outer_overlap_count = 0;
		public void UpdateGeometryStreaming()
		{
			List<teleport.StreamableRoot> gainedStreamables = new List<teleport.StreamableRoot>();
			List<teleport.StreamableRoot> lostStreamables = new List<teleport.StreamableRoot>();
			if (streamedGeometryManagement!=null)
				streamedGeometryManagement.UpdateStreamedGeometry(session,ref gainedStreamables,ref lostStreamables,  streamedHierarchies);
			else
			{
				Debug.LogError("Please assign a Streamed Geometry Manager in the Monitor.");
				return;
			}
			foreach (var streamable in gainedStreamables)
			{
				if(StartStreaming(streamable, StreamingReason.NEARBY))
				{

				}
			}

			foreach (var streamable in lostStreamables)
			{
				StopStreaming(streamable, StreamingReason.NEARBY);
			}

			if(session.IsConnected() )
			{
				//tagHandler.StopStreamingUntaggedStreamables();
				SendPositionUpdates();
				SendEnabledStateUpdates();
			}
		}
		class ClientStreamableTracking
		{
			// Track the reasons why we're streaming this. A set of bit flags, when it goes to zero you can stop streaming it.
			public UInt32 streaming_reason = 0;
		}

		Dictionary<teleport.StreamableRoot,ClientStreamableTracking> clientStreamableTracking=new Dictionary<teleport.StreamableRoot, ClientStreamableTracking>();
		public void StreamableHasChanged(teleport.StreamableRoot streamable)
		{
			if (!streamedGameObjects.Contains(streamable.gameObject))
				return;
			SendHierarchyToClient(streamable);
		}
        // Start streaming the given hierarchy to this Client.
        private bool StartStreaming(teleport.StreamableRoot streamable, StreamingReason reason)
        {
			if(!streamable)
				return false;
			if(streamable.priority<teleportSettings.defaultMinimumNodePriority)
				return false;
			uint streaming_reason= (uint)reason;
			GameObject gameObject = streamable.gameObject;

			// Only report the more unusual reasons:
			//if(reason!=StreamingReason.NEARBY)
			//	Debug.Log($"StartStreaming called on {gameObject.name} for reason {reason}.");

			ClientStreamableTracking tracking = GetTracking(streamable);
            if (streamedGameObjects.Contains(gameObject))
            {
                if ((tracking.streaming_reason & streaming_reason) != 0)
                {
                    Debug.LogWarning($"StartStreaming called on {gameObject.name} for reason {reason}, but this was already known.");
                }
                else
                {
                    tracking.streaming_reason |= streaming_reason;
                }
                return false;
            }
            tracking.streaming_reason |= streaming_reason;
            if(!SendHierarchyToClient(streamable))
				return false;
            streamable.AddStreamingClient(session);
			streamedHierarchies.Add(streamable);
			return true;
		}
		bool SendHierarchyToClient(teleport.StreamableRoot streamable)
		{
			//Stream teleport.StreamableRoot's hierarchy.
			var streamableNodes = streamable.GetStreamableNodes();
			foreach (teleport.StreamableNode streamedNode in streamableNodes)
			{
				if(streamedNode.nodeID==0)
				{
					GeometrySource.GetGeometrySource().AddNode(streamedNode.gameObject);
				}
				if(streamedNode.nodeID== 0)
				{
					UnityEngine.Debug.LogError("Unable to assign node uid for "+streamedNode.gameObject.name);
					GeometrySource.GetGeometrySource().AddNode(streamedNode.gameObject);
					continue;
				}
				if(streamedGameObjects.Contains(streamedNode.gameObject))
				{
					continue;
				}
				ulong num_nodes_streamed_before = Client_GetNumNodesToStream(session.GetClientID());
				if (num_nodes_streamed_before != (ulong)streamedGameObjects.Count)
				{
					Debug.LogWarning($"Object Node count mismatch between dll ({num_nodes_streamed_before}) and C# ({(streamedGameObjects.Count + 1)}) before adding " + streamedNode.gameObject.name);
					//return false;
				}
				if (!Client_StreamNode(session.GetClientID(), streamedNode.nodeID))
				{
					UnityEngine.Debug.LogError("Failed to stream " + streamedNode.gameObject.name+" with uid "+ streamedNode.nodeID);
				}
				ulong num_nodes_streamed= Client_GetNumNodesToStream(session.GetClientID());
				if (num_nodes_streamed != (ulong)streamedGameObjects.Count+1)
                {
					Debug.LogWarning($"Object Node count mismatch between dll ({num_nodes_streamed}) and C# ({(streamedGameObjects.Count + 1)}) after adding " + streamedNode.gameObject.name);
					//return false;
				}
				streamedGameObjects.Add(streamedNode);
			}

			Collider[] colliders = streamable.gameObject.GetComponents<Collider>();
			foreach(Collider collider in colliders)
			{
				streamedColliders.Add(collider);
			}

			return true;
		}
		private ClientStreamableTracking GetTracking(teleport.StreamableRoot streamable)
		{
			ClientStreamableTracking outTracking;
			if (clientStreamableTracking.TryGetValue(streamable,out outTracking))
				return outTracking;
			var t=new ClientStreamableTracking();
			clientStreamableTracking.Add(streamable,t);
			return t;
		}
		public bool StopStreaming(teleport.StreamableRoot streamable, StreamingReason streaming_reason)
		{
			ClientStreamableTracking tracking = GetTracking(streamable);
			tracking.streaming_reason &= ~((uint)streaming_reason);
			if(tracking.streaming_reason != 0)
			{
				return false;
			}

			streamable.RemoveStreamingClient(session);
			streamedHierarchies.Remove(streamable);

			//Stop streaming hierarchy.
			foreach(teleport.StreamableNode streamedNode in streamable.GetStreamableNodes())
			{
				streamedGameObjects.Remove(streamedNode);
				Client_UnstreamNode(session.GetClientID(), streamedNode.nodeID);
			}

			//Remove GameObject's colliders from list.
			Collider[] colliders = streamable.GetComponents<Collider>();
			foreach(Collider collider in colliders)
			{
				streamedColliders.Remove(collider);
			}

			return true;
		}

        HashSet<uid> got_uids=new HashSet<uid>();

        private void SendPositionUpdates()
		{
			//Send position updates, if enough time has elapsed.
			timeSincePositionUpdate += Time.deltaTime;
			if (timeSincePositionUpdate < 1.0f / teleportSettings.moveUpdatesPerSecond)
				return;
			timeSincePositionUpdate = 0;
			List<avs.MovementUpdate> updates = new List<avs.MovementUpdate>();
			got_uids.Clear();

            foreach (teleport.StreamableRoot streamable in streamedHierarchies)
			{
				var add_list = streamable.GetMovementUpdates(session.GetClientID());
				foreach (var add in add_list)
				{
					if (got_uids.Contains(add.nodeID))
						continue;
					updates.Add(add);
					got_uids.Add(add.nodeID);
				}
			}

			Client_UpdateNodeMovement(session.GetClientID(), updates.ToArray(), updates.Count);
		}
		/// <summary>
		/// Per-client data for streamed root nodes.
		/// </summary>
		class StreamedRootPerClient
		{
			public double lastEnabledCheckTime=0.0;
		};
		Dictionary<teleport.StreamableRoot,StreamedRootPerClient> streamedRoots=new Dictionary<teleport.StreamableRoot, StreamedRootPerClient>();

		private void SendEnabledStateUpdates()
		{
			timeSinceEnabledStateUpdate+=Time.deltaTime;
			if (timeSinceEnabledStateUpdate <1.0f / teleportSettings.moveUpdatesPerSecond)
				return;
			timeSinceEnabledStateUpdate=0;
			List<avs.NodeUpdateEnabledState> updates = new List<avs.NodeUpdateEnabledState>();
			foreach(teleport.StreamableRoot streamable in streamedHierarchies)
			{
				List<avs.NodeUpdateEnabledState> streamable_states = new List<avs.NodeUpdateEnabledState>();
				streamable.GetEnabledStateUpdates(0, ref streamable_states);
				updates.AddRange(streamable_states);
			}

			//Don't send an update command, if there were no updates.
			if(updates.Count == 0)
			{
				return;
			}

			Client_UpdateNodeEnabledState(session.GetClientID(), updates.ToArray(), updates.Count);
		}

		public List<teleport.StreamableRoot> GetCurrentStreamables()
		{
			return streamedHierarchies;
		}
	}
}