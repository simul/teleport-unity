using System;
using System.Collections.Generic;
using UnityEngine;

using uid = System.UInt64;

namespace teleport
{
	[Serializable]
	//Class for storing state and cached components of a node that is streamed in a Teleport_Streamable's hierarchy.
	public class StreamedNode
	{
		public GameObject gameObject; //GameObject that is being streamed.
		public uid nodeID; //ID of the node.
		public bool enabled; //Whether the node is enabled on the server.

		//Cached references to components.
		public MeshRenderer meshRenderer;
		public SkinnedMeshRenderer skinnedMeshRenderer;
		public Light light;

		public StreamedNode(GameObject node)
		{
			gameObject = node;
			nodeID = GeometrySource.GetGeometrySource().FindResourceID(node);
			enabled = true;

			meshRenderer = default;
			skinnedMeshRenderer = default;
			light = default;

			if(nodeID == 0)
			{
				Debug.LogWarning($"We have created an invalid {nameof(StreamedNode)}! Node ID is {nodeID}!");
			}
		}

		//Updates enabled state by checking cached references, and returns whether the enabled state changed.
		//WARNING: We currently do not support streaming multiple rendering components on the same GameObject; this will put precedence on MeshRenderers then SkinnedMeshRenderers then Lights.
		public bool UpdatedEnabledState()
		{
			bool wasEnabled = enabled;

			//StreamedNode is not enabled if it is not active in the hierarchy.
			if(!gameObject.activeInHierarchy)
			{
				enabled = false;
				return wasEnabled != enabled;
			}
			enabled = true;

			if(meshRenderer)
			{
				enabled = meshRenderer.enabled;
				return wasEnabled != enabled;
			}

			if(skinnedMeshRenderer)
			{
				enabled = skinnedMeshRenderer.enabled;
				return wasEnabled != enabled;
			}

			if(light)
			{
				enabled = light.enabled;
				return wasEnabled != enabled;
			}

			return wasEnabled != enabled;
		}

		//OVERLOADED ADD_COMPONENT(...)

		//We are using dynamic conversion to assign the component to the correct cached reference.
		public void AddComponent(Component component)
		{
			Debug.LogWarning($"Unable to add component of type {component.GetType().Name} to streamed node for {gameObject}!");
		}

		public void AddComponent(MeshRenderer component)
		{
			meshRenderer = component;
		}

		public void AddComponent(SkinnedMeshRenderer component)
		{
			skinnedMeshRenderer = component;
		}

		public void AddComponent(Light component)
		{
			light = component;
		}

		//COVERSION OPERATORS

		//StreamedNode is basically a meta-wrapper for a GameObject, so we should be able to convert to the GameObject we are wrapping.
		public static implicit operator GameObject(StreamedNode streamedNode)
		{
			return streamedNode.gameObject;
		}
	}

	//This component is automatically added to streamable GameObjects and their children.
	[DisallowMultipleComponent]
	public class Teleport_StreamableTracker : MonoBehaviour
	{
	}

	//This component is automatically added to GameObjects that meet the criteria for geometry streaming.
	[DisallowMultipleComponent]
	public class Teleport_Streamable : MonoBehaviour
	{
		// Track the reasons why we're streaming this. A set of bit flags, when it goes to zero you can stop streaming it.
		public UInt32 streaming_reason = 0;

		//All GameObjects objects in the streamed hierarchy this Teleport_Streamable represents.
		public List<StreamedNode> streamedHierarchy = new List<StreamedNode>();

		public bool sendMovementUpdates = true;
		public bool sendEnabledStateUpdates = true;
		public bool pollCurrentAnimation = false;

		[SerializeField]
		private uid uid = 0;

		private HashSet<Teleport_SessionComponent> sessions = new HashSet<Teleport_SessionComponent>();

		//Animator trackers in this TeleportStreamable's hierarchy.
		private List<Teleport_AnimatorTracker> animatorTrackers = new List<Teleport_AnimatorTracker>();

		private Dictionary<uid, avs.MovementUpdate> previousMovements = new Dictionary<uid, avs.MovementUpdate>();

		static int clientLayer = 25;
		// Add the 0x7 because that's used to show canvases, so we must remove it also from the inverse mask.
		// clear clientLayer and set (clientLayer+1)
		static uint clientMask = (uint)(((int)1) << clientLayer) | (uint)0x7;
		static uint invClientMask = ~clientMask;
		static uint streamedClientMask = (uint)(((int)1) << (clientLayer + 1));
		static uint invStreamedMask = ~streamedClientMask;

		public void SetUid(uid u)
		{
			if(uid != 0 && u != uid)
			{
				Debug.LogError($"GameObject <i>\"{name}\"</i> already has ID {uid}, but we are overriding it with uid {u}!");
			}
			uid = u;
		}

		public uid GetUid()
		{
			if(uid == 0)
			{
				uid = GeometrySource.GetGeometrySource().FindResourceID(gameObject);
			}

			return uid;
		}

		public void AddStreamingClient(Teleport_SessionComponent sessionComponent)
		{
			sessions.Add(sessionComponent);
		}

		public void RemoveStreamingClient(Teleport_SessionComponent sessionComponent)
		{
			sessions.Remove(sessionComponent);
		}

		//Returns hashset of sessions this hierarchy is in.
		public HashSet<Teleport_SessionComponent> GetActiveSessions()
		{
			return sessions;
		}

		public List<avs.MovementUpdate> GetMovementUpdates(uid clientID)
		{
			//Return an empty update list, if we're not sending movement updates.
			if(!sendMovementUpdates)
			{
				return new List<avs.MovementUpdate>();
			}

			//TODO: Cache result every tick, so we don't calculate it per client, or send it to every client on the C++/Unmanaged side.
			List<avs.MovementUpdate> updates = new List<avs.MovementUpdate>();
			
			//Add movement updates for GameObjects in streamed hierarchy to the list.
			foreach(GameObject node in streamedHierarchy)
			{
				if(!node)
				{
					Debug.LogWarning($"Failed to update movement of node! Null node in {nameof(Teleport_Streamable)}.childHierarchy of \"{name}\"!");
					continue;
				}

				updates.Add(GetNodeMovementUpdate(node, clientID));
			}

			return updates;
		}

		//Tells the client what nodes have changed their enabled state and should be hidden from view.
		public List<avs.NodeUpdateEnabledState> GetEnabledStateUpdates()
		{
			//Return an empty update list, if we're not sending enabled state updates.
			if(!sendEnabledStateUpdates)
			{
				return new List<avs.NodeUpdateEnabledState>();
			}

			//TODO: Support multiple clients, or send it to every client on the C++/Unmanaged side.
			List<avs.NodeUpdateEnabledState> updates = new List<avs.NodeUpdateEnabledState>();

			foreach(StreamedNode streamedNode in streamedHierarchy)
			{
				//Send message if enabled state has changed.
				if(streamedNode.UpdatedEnabledState())
				{
					updates.Add(new avs.NodeUpdateEnabledState{nodeID = streamedNode.nodeID, enabled = streamedNode.enabled});
				}
			}

			return updates;
		}

		//Send the playing animations on this hierarchy to the client.
		public void SendAnimationState(uid clientID)
		{
			foreach(Teleport_AnimatorTracker tracker in animatorTrackers)
			{
				tracker.SendPlayingAnimation(clientID);
			}
		}

		private void OnEnable()
		{
			uid = GeometrySource.GetGeometrySource().AddNode(gameObject, GeometrySource.ForceExtractionMask.FORCE_NODES_AND_HIERARCHIES);
			CreateStreamedHierarchy();
		}

		private void OnDisable()
		{
			//Remove GameObject from sessions.
			List<Teleport_SessionComponent> copiedSessions = new List<Teleport_SessionComponent>(sessions);
			foreach(Teleport_SessionComponent session in copiedSessions)
			{
				if(session.GeometryStreamingService != null)
				{
					// stop streaming no matter what the reason.
					session.GeometryStreamingService.StopStreaming(this,0xFFFFFFFF);
				}
			}
		}

		private void OnDestroy()
		{
			OnDisable();
		}

		private void CreateStreamedHierarchy()
		{
			streamedHierarchy.Clear();

			List<GameObject> exploredGameObjects = new List<GameObject>();
			//We need to stop once we reach this node, so we add it to the explored list, but that means we now have to explicitly add it to the streamed hierarchy.
			exploredGameObjects.Add(gameObject);
			streamedHierarchy.Add(new StreamedNode(gameObject));

			//Mark all children that will be streamed separately as explored; i.e. is marked for streaming.
			List<Collider> childColliders = new List<Collider>(GetComponentsInChildren<Collider>());
			foreach(Collider childCollider in childColliders)
			{
				//GetComponentsInChildren(...) also grabs components from node we are on, but we don't want to exclude the children of the node we are on.
				if(childCollider.gameObject != gameObject && GeometrySource.GetGeometrySource().IsGameObjectMarkedForStreaming(childCollider.gameObject))
				{
					//Mark child's children as explored, as they are part of a different streaming hierarchy.
					Transform[] childTransforms = childCollider.GetComponentsInChildren<Transform>();
					foreach(Transform transform in childTransforms)
					{
						exploredGameObjects.Add(transform.gameObject);
					}
				}
			}

			//Add components to hierarchy.
			AddComponentTypeToHierarchy<MeshRenderer>(exploredGameObjects);
			AddComponentTypeToHierarchy<SkinnedMeshRenderer>(exploredGameObjects);
			AddComponentTypeToHierarchy<Light>(exploredGameObjects);

			//Add animator trackers to nodes in streamed hierarchy with animator components.
			//We use streamedHierarchy, rather than GetComponentInChildren(...), as we need to restrict the search to the streamed hierarchy rather than the entire Transform hierarchy.
			foreach(GameObject gameObject in streamedHierarchy)
			{
				AddAnimatorTracker(gameObject);
			}
		}

		//Adds the GameObject of the passed transform to the hierarchy, if it has not already been explored in a depth-first search.
		//	transform : Transform of the GameObject we will add the the hierarchy, if unexplored.
		//	exploredGameObjects : GameObjects that have already been explored by the depth-first search, or we just don't want added to the hierarchy.
		private void AddToHierarchy(Transform transform, List<GameObject> exploredGameObjects)
		{
			if(transform)
			{
				GameObject gameObject = transform.gameObject;
				if(!exploredGameObjects.Contains(gameObject))
				{
					exploredGameObjects.Add(gameObject);
					streamedHierarchy.Add(new StreamedNode(gameObject));

					AddToHierarchy(transform.parent, exploredGameObjects);
				}
			}
		}

		//Adds the GameObjects with a component of the generic type belonging to the Teleport_Streamable's Transform hierarchy to the streamable hierarchy,
		//if they do not appear in the explored list and are not already a part of the streamable hierarchy.
		//	exploredGameObjects : GameObjects that have already been explored by the depth-first search, or we just don't want added to the hierarchy.
		private void AddComponentTypeToHierarchy<T>(List<GameObject> exploredGameObjects) where T : Component
		{
			T[] components = GetComponentsInChildren<T>();

			foreach(T component in components)
			{
				AddToHierarchy(component.transform, exploredGameObjects);

				//Cache the reference on the node in the streamed hierarchy.
				foreach(StreamedNode streamedNode in streamedHierarchy)
				{
					if(streamedNode == component.gameObject)
					{
						streamedNode.AddComponent((dynamic)component);
						break;
					}
				}
			}
		}

		//Adds the Teleport_AnimatorTracker component to the passed node, if it has an Animator component and its Transform hierarchy has a SkinnedMeshRenderer.
		//	node : Node in the hierarchy we will attempt to add the Teleport_AnimatorTracker to.
		private void AddAnimatorTracker(GameObject node)
		{
			if(node.TryGetComponent(out Animator _))
			{
				//AnimatorTracker currently needs a SkinnedMeshRenderer component to operate.
				if(node.GetComponentInChildren<SkinnedMeshRenderer>() != null)
				{
					//A hot-reload will cause this to be called again, so we need to check the component hasn't already been added.
					if(!node.TryGetComponent(out Teleport_AnimatorTracker tracker))
					{
						tracker = node.AddComponent<Teleport_AnimatorTracker>();
						tracker.hierarchyRoot = this;

						animatorTrackers.Add(tracker);
					}
				}
			}
		}

		private avs.MovementUpdate GetNodeMovementUpdate(GameObject node, uid clientID)
		{
			//Node should already have been added; but AddNode(...) will do the equivalent of FindResourceID(...), but with a fallback.
			uid nodeID = GeometrySource.GetGeometrySource().AddNode(node);

			avs.MovementUpdate update = new avs.MovementUpdate();
			update.timestamp = CasterMonitor.GetUnixTimestamp();
			update.nodeID = nodeID;

			if(GeometryStreamingService.IsClientRenderingParent(clientID, node))
			{
				update.isGlobal = false;
				update.position = node.transform.localPosition;
				update.rotation = node.transform.localRotation;
				update.scale = node.transform.localScale;
			}
			else
			{
				update.isGlobal = true;
				update.position = node.transform.position;
				update.rotation = node.transform.rotation;
				update.scale	= node.transform.lossyScale;
			}

			if(previousMovements.TryGetValue(nodeID, out avs.MovementUpdate previousMovement))
			{
				TeleportSettings teleportSettings = TeleportSettings.GetOrCreateSettings();
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
				update.velocity = (position - previousMovement.position) * teleportSettings.moveUpdatesPerSecond;

				(rotation * Quaternion.Inverse(previousMovement.rotation)).ToAngleAxis(out update.angularVelocityAngle, out Vector3 angularVelocityAxis);
				update.angularVelocityAxis = angularVelocityAxis;
				//Angle needs to be inverted, for some reason.
				update.angularVelocityAngle *= teleportSettings.moveUpdatesPerSecond * -Mathf.Deg2Rad;
			}

			previousMovements[nodeID] = update;
			return update;
		}

		public void ShowHierarchy()
		{
			foreach(StreamedNode streamedNode in streamedHierarchy)
			{
				AddClientMask(streamedNode);
			}
		}

		void AddClientMask(StreamedNode streamedNode)
		{
			if(streamedNode.meshRenderer)
			{
				streamedNode.meshRenderer.renderingLayerMask &= invStreamedMask;
				streamedNode.meshRenderer.renderingLayerMask |= clientMask;
			}

			if(streamedNode.skinnedMeshRenderer)
			{
				streamedNode.skinnedMeshRenderer.renderingLayerMask &= invStreamedMask;
				streamedNode.skinnedMeshRenderer.renderingLayerMask |= clientMask;
			}
		}

		public void HideHierarchy()
		{
			foreach(StreamedNode streamedNode in streamedHierarchy)
			{
				AddClientMask(streamedNode);
			}
		}

		void RemoveClientMask(StreamedNode streamedNode)
		{
			if(streamedNode.meshRenderer)
			{
				streamedNode.meshRenderer.renderingLayerMask &= invClientMask;
				streamedNode.meshRenderer.renderingLayerMask |= streamedClientMask;
			}

			if(streamedNode.skinnedMeshRenderer)
			{
				streamedNode.skinnedMeshRenderer.renderingLayerMask &= invClientMask;
				streamedNode.skinnedMeshRenderer.renderingLayerMask |= streamedClientMask;
			}
		}
	}
}