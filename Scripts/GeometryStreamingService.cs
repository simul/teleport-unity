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
		#if WIN32
		const string dllName="TeleportServer";
		#else
		const string dllName="TeleportServer.so";
		#endif
		[DllImport(TeleportServerDll.name)]
		private static extern UInt64 Client_AddNode(uid clientID, uid nodeID);
		[DllImport(TeleportServerDll.name)]
		private static extern void Client_RemoveNodeByID(uid clientID, uid nodeID);
		[DllImport(TeleportServerDll.name)]
		private static extern void Client_AddGenericTexture(uid clientID, uid textureID);
		[DllImport(TeleportServerDll.name)]
		public static extern void Client_SetGlobalIlluminationTextures(uid clientID, UInt64 num, uid[] textureIDs);
		[DllImport(TeleportServerDll.name)]
		public static extern void Client_NodeEnteredBounds(uid clientID, uid nodeID);
		[DllImport(TeleportServerDll.name)]
		public static extern void Client_NodeLeftBounds(uid clientID, uid nodeID);
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
			if(textureIDs!=null)
				Client_SetGlobalIlluminationTextures(session.GetClientID(), (UInt64)textureIDs.Length, textureIDs);
		}
		public void StreamPlayerBody()
		{
			List<GameObject> bodyParts = session.GetPlayerBodyParts();
			foreach(GameObject part in bodyParts)
			{
				Teleport_Streamable streamable = part.GetComponent<Teleport_Streamable>();
				streamable.OwnerClient=session.GetClientID();
				StartStreaming(streamable, 2);
			}
			Teleport_Streamable session_streamable = session.GetComponent<Teleport_Streamable>();
			StartStreaming(session_streamable, 2);
		}

		public void SendAnimationState()
		{
			foreach(Teleport_Streamable streamable in streamedHierarchies)
			{
				streamable.SendAnimationState(session.GetClientID());
			}
		}
		/// <summary>
		/// Tell this client to highlight the specified node.
		/// </summary>
		/// <param name="gameObject">The object to highlight</param>
		/// <param name="isHighlighted">Whether to activate or deactivate highlighting.</param>
		public void SetNodeHighlighted(GameObject gameObject, bool isHighlighted)
		{
			uid nodeID = GeometrySource.GetGeometrySource().FindResourceID(gameObject);
			if(nodeID != 0)
			{
				Client_SetNodeHighlighted(session.GetClientID(), nodeID, isHighlighted);
			}
		}

		public void SetNodePosePath(GameObject gameObject, string regexPosePath)
		{
			uid nodeID = GeometrySource.GetGeometrySource().FindResourceID(gameObject);
			if (nodeID == 0)
			{
				Debug.LogError("Node id not found for "+gameObject.name);
			}
			else
			{
				var clID= session.GetClientID();
				// for now we assume that these types have no parent.
				Client_SetNodePosePath(clID, nodeID, regexPosePath);
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
		HashSet<Teleport_Streamable> innerStreamables = new HashSet<Teleport_Streamable>();
		HashSet<Teleport_Streamable> outerStreamables = new HashSet<Teleport_Streamable>();
		int inner_overlap_count=0;
		int outer_overlap_count = 0;
		public void UpdateGeometryStreaming()
		{
			if(!session.IsConnected())
				return;
			if(session.GetClientID()==0)
			{
				TeleportLog.LogErrorOnce("Client ID is zero.");
				return;
			}
			var layersToStream=teleportSettings.LayersToStream;
			//if(layersToStream==0)
			//	layersToStream=0xFFFFFFFF;
			//Detect changes in geometry that needs to be streamed to the client.

			// each client session component should maintain a list of the TeleportStreamable (root) objects it is
			// tracking. Perhaps count how many colliders it is impinging for each root.
			// The session can use OnTriggerEnter and OnTriggerExit to update this list.
			if (layersToStream!= 0)
			{ 
				Vector3 position= session.head.transform.position;
				float R0= teleportSettings.serverSettings.detectionSphereRadius;
				float R1= R0+teleportSettings.serverSettings.detectionSphereBufferDistance;
				inner_overlap_count= Physics.OverlapSphereNonAlloc( position,  R0, innerOverlappingColliders, teleportSettings.LayersToStream);
				if(inner_overlap_count> innerOverlappingColliders.Length)
                {
					innerOverlappingColliders = new Collider[inner_overlap_count*2];
					Physics.OverlapSphereNonAlloc(position, R0, innerOverlappingColliders, teleportSettings.LayersToStream);
				}
				outer_overlap_count = Physics.OverlapSphereNonAlloc(position, R0, outerOverlappingColliders, teleportSettings.LayersToStream);
				if (outer_overlap_count > outerOverlappingColliders.Length)
				{
					outerOverlappingColliders = new Collider[outer_overlap_count * 2];
					Physics.OverlapSphereNonAlloc(position, R0, outerOverlappingColliders, teleportSettings.LayersToStream);
				}
				List<Teleport_Streamable> gainedStreamables=new List<Teleport_Streamable>();
				for (int i = 0; i < inner_overlap_count; i++)
                {
					GameObject g=innerOverlappingColliders[i].gameObject;
					if(!g)
						continue;
					if (!innerOverlappingColliders[i].enabled)
						continue;
					var streamable=g.GetComponentInParent<Teleport_Streamable>();
					if(!streamable)
						continue;
					if(innerStreamables.Contains(streamable))
						continue;
					innerStreamables.Add(streamable);
					gainedStreamables.Add(streamable);
				}
				List<Teleport_Streamable> lostStreamables = new List<Teleport_Streamable>();
				HashSet<Teleport_Streamable> keptOuterStreamables = new HashSet<Teleport_Streamable>();
				for (int i = 0; i < outer_overlap_count; i++)
				{
					GameObject g = outerOverlappingColliders[i].gameObject;
					if (!g)
						continue;
					if (!outerOverlappingColliders[i].enabled)
						continue;
					var streamable = g.GetComponentInParent<Teleport_Streamable>();
					if (!streamable)
						continue;
					if (outerStreamables.Contains(streamable))
						continue;
					keptOuterStreamables.Add(streamable);
				}
				foreach(var s in outerStreamables)
				{
					if(!keptOuterStreamables.Contains(s))
                    {
						outerStreamables.Remove(s);
						lostStreamables.Add(s);
                    }
				}
				foreach (var streamable in gainedStreamables)
				{
					StartStreaming(streamable, 1);
				}

				foreach (var streamable in lostStreamables)
				{
					StopStreaming(streamable, 1);
				}
				/*	List<Collider> innerSphereCollisions = new List<Collider>(Physics.OverlapSphere(position, R0, teleportSettings.LayersToStream));
					List<Collider> outerSphereCollisions = new List<Collider>(Physics.OverlapSphere(position, R1, teleportSettings.LayersToStream));
					List<Collider> gainedColliders = new List<Collider>(innerSphereCollisions.Except(streamedColliders));
					List<Collider> lostColliders = new List<Collider>(streamedColliders.Except(outerSphereCollisions));

					foreach (Collider collider in gainedColliders)
					{			
						if(!collider.enabled)
							continue;
						//Skip game objects without the streaming tag.
						var props= collider.GetComponent < StreamableProperties >();
						if((teleportSettings.TagToStream.Length == 0 || collider.CompareTag(teleportSettings.TagToStream))
							&&(props== null ||!props.streamOnlyWithParent))
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
						if (collider.enabled)
						{
							Teleport_Streamable streamable = collider.gameObject.GetComponent<Teleport_Streamable>();
							StopStreaming(streamable, 1);
						}
					}*/
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
		class ClientStreamableTracking
		{
			// Track the reasons why we're streaming this. A set of bit flags, when it goes to zero you can stop streaming it.
			// TODO: this should obviously be per-client.
			public UInt32 streaming_reason = 0;
		}

		Dictionary<Teleport_Streamable,ClientStreamableTracking> clientStreamableTracking=new Dictionary<Teleport_Streamable, ClientStreamableTracking>();
		// Start streaming the given streamable gameObject and its hierarchy.
		private bool StartStreaming(Teleport_Streamable streamable, UInt32 streaming_reason)
		{
			GameObject gameObject = streamable.gameObject;
			ClientStreamableTracking tracking= GetTracking(streamable);
			if (streamedGameObjects.Contains(gameObject))
			{
				if((tracking.streaming_reason & streaming_reason) != 0)
				{
					Debug.LogWarning($"StartStreaming called on {gameObject.name} for reason {streaming_reason}, but this was already known.");
				}
				else
				{
					tracking.streaming_reason |= streaming_reason;
				}
				return false;
			}
			tracking.streaming_reason |= streaming_reason;
			streamable.AddStreamingClient(session);
			streamedHierarchies.Add(streamable);

			//Stream Teleport_Streamable's hierarchy.
			foreach(StreamedNode streamedNode in streamable.streamedHierarchy)
			{
				if(streamedGameObjects.Contains(streamedNode.gameObject))
				{
					continue;
				}

				int num_nodes_streamed=(int)Client_AddNode(session.GetClientID(), streamedNode.nodeID);

				Client_NodeEnteredBounds(session.GetClientID(), streamedNode.nodeID);
				if (num_nodes_streamed != streamedGameObjects.Count+1)
                {
					Debug.LogError("Object Node count mismatch between dll and C# after adding " + streamedNode.gameObject.name);
					return false;
				}
				streamedGameObjects.Add(streamedNode);
			}

			Collider[] colliders = gameObject.GetComponents<Collider>();
			foreach(Collider collider in colliders)
			{
				streamedColliders.Add(collider);
			}

			return true;
		}
		private ClientStreamableTracking GetTracking(Teleport_Streamable streamable)
		{
			ClientStreamableTracking outTracking;
			if (clientStreamableTracking.TryGetValue(streamable,out outTracking))
				return outTracking;
			var t=new ClientStreamableTracking();
			clientStreamableTracking.Add(streamable,t);
			return t;
		}
		public bool StopStreaming(Teleport_Streamable streamable, UInt32 streaming_reason)
		{
			ClientStreamableTracking tracking = GetTracking(streamable);
			tracking.streaming_reason &= ~streaming_reason;
			if(tracking.streaming_reason != 0)
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
			var geometrySource=GeometrySource.GetGeometrySource();
			for (int i = streamedHierarchies.Count - 1; i >= 0; i--)
			{
				Teleport_Streamable streamable = streamedHierarchies[i];
				if (!geometrySource.IsGameObjectMarkedForStreaming(streamable.gameObject))
				{
					ClientStreamableTracking tracking = GetTracking(streamable);
					if ((tracking.streaming_reason & 1) != 0)
					{
						StopStreaming(streamable, 1);
					}
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