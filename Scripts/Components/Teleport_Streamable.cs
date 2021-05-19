using System;
using System.Collections.Generic;
using UnityEngine;

using uid = System.UInt64;

namespace teleport
{
	//This component is automatically added to streamable GameObjects and their children.
	[DisallowMultipleComponent]
	public class Teleport_StreamableTracker : MonoBehaviour
	{
	}

	//This component is automatically added to GameObjects that meet the criteria for geometry streaming.
	[DisallowMultipleComponent]
	public class Teleport_Streamable : MonoBehaviour
	{
		// SerializeField so that it will be visible on the inspector. 
		[SerializeField]
		private uid uid = 0;
		// Track the reasons why we're streaming this. A set of bit flags, when it goes to zero you can stop streaming it.
		public UInt32 streaming_reason=0;
		//The child objects that have no collision, so are streamed automatically with the GameObject the component is attached to.
		public List<GameObject> childHierarchy = new List<GameObject>();

		public bool sendMovementUpdates = true;

		
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
				uid = GeometrySource.GetGeometrySource().FindResourceID(this);
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
			///TODO: Cache result every <frame/movement update tick>, so we don't calculate it per client.
			List<avs.MovementUpdate> updates = new List<avs.MovementUpdate>();
			
			//Return an empty update list, if we're not sending movement updates.
			if(!sendMovementUpdates)
			{
				return updates;
			}

			//Add movement updates for GameObject and GameObjects in child hierarchy to the list.
			updates.Add(GetNodeMovementUpdate(gameObject, clientID));
			foreach(GameObject node in childHierarchy)
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
			GeometrySource.GetGeometrySource().AddNode(gameObject, GeometrySource.ForceExtractionMask.FORCE_NODES_AND_HIERARCHIES);
			CreateChildHierarchy();
		}

		private void OnDestroy()
		{
			OnDisable();
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

		private void CreateChildHierarchy()
		{
			childHierarchy.Clear();

			List<GameObject> exploredGameObjects = new List<GameObject>();
			exploredGameObjects.Add(gameObject);

			//Mark all children that will be streamed separately as explored; i.e. is marked for streaming.
			List<Collider> childColliders = new List<Collider>(GetComponentsInChildren<Collider>());
			foreach(Collider childCollider in childColliders)
			{
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

			//Find all desired components in children.
			List<Component> childComponents = new List<Component>();
			childComponents.AddRange(GetComponentsInChildren<MeshFilter>());
			childComponents.AddRange(GetComponentsInChildren<SkinnedMeshRenderer>());
			childComponents.AddRange(GetComponentsInChildren<Light>());

			foreach(Component component in childComponents)
			{
				AddToHierarchy(component.transform, exploredGameObjects);
			}

			//Add animator trackers to nodes in streamed hierarchy with animator components.
			//We don't want to use GetComponentInChildren(...), as we need to restrict the search to the streamed hierarchy rather than the entire hierarchy.
			AddAnimatorTracker(gameObject);
			foreach(GameObject child in childHierarchy)
			{
				AddAnimatorTracker(child);
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
					childHierarchy.Add(gameObject);

					AddToHierarchy(transform.parent, exploredGameObjects);
				}
			}
		}

		//Adds the Teleport_AnimatorTracker component to the passed node, if it has an Animator component.
		//	node : Node in the hierarchy we will add the Teleport_AnimatorTracker to, if it has an Animator component.
		private void AddAnimatorTracker(GameObject node)
		{
			if(node.TryGetComponent(out Animator _))
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

		private avs.MovementUpdate GetNodeMovementUpdate(GameObject node, uid clientID)
		{
			//Node should already have been added; but AddNode(...) will do the equivalent of FindResourceID(...), but with a fallback.
			uid nodeID = GeometrySource.GetGeometrySource().AddNode(node);

			avs.MovementUpdate update = new avs.MovementUpdate();
			update.timestamp = CasterMonitor.GetUnixTimestamp();
			update.nodeID = nodeID;

			if(GeometryStreamingService.IsClientRenderingParent(clientID, node))
			{
				update.isGlobal = (byte)0;
				update.position = node.transform.localPosition;
				update.rotation = node.transform.localRotation;
				update.scale = node.transform.localScale;
			}
			else
			{
				update.isGlobal = (byte)1;
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
				if(previousMovement.isGlobal!=0)
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
			foreach (var child in childHierarchy)
			{
				AddClientMask(child);
			}		
		}

		void AddClientMask(GameObject gameObject)
		{
			Renderer nodeRenderer = gameObject.GetComponent<Renderer>();
			if (nodeRenderer)
			{
				nodeRenderer.renderingLayerMask &= invStreamedMask;
				nodeRenderer.renderingLayerMask |= clientMask;
			}
		}

		public void HideHierarchy()
		{
			foreach (var child in childHierarchy)
			{
				RemoveClientMask(child);
			}
		}

		void RemoveClientMask(GameObject gameObject)
		{
			Renderer nodeRenderer = gameObject.GetComponent<Renderer>();
			if (nodeRenderer)
			{
				nodeRenderer.renderingLayerMask &= invClientMask;
				nodeRenderer.renderingLayerMask |= streamedClientMask;	
			}
		}
	}
}