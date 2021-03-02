using System;
using System.Collections.Generic;
using UnityEngine;

using uid = System.UInt64;

namespace teleport
{
	//This component is automatically added to GameObjects that meets the criteria for geometry streaming.
	[DisallowMultipleComponent]
	public class Teleport_Streamable : MonoBehaviour
	{
		//The child objects that have no collision, so are streamed automatically with the GameObject the component is attached to.
		public List<GameObject> childHierarchy = new List<GameObject>();

		public bool sendMovementUpdates = true;

		private uid uid = 0;
		private Dictionary<uid, avs.MovementUpdate> previousMovements = new Dictionary<uid, avs.MovementUpdate>();

		private HashSet<Teleport_SessionComponent> sessions = new HashSet<Teleport_SessionComponent>();

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

		private void OnEnable()
		{
			CreateChildHierarchy();
		}

		private void OnDisable()
		{
			//Remove GameObject from sessions.
			List<Teleport_SessionComponent> copiedSessions = new List<Teleport_SessionComponent>(sessions);
			foreach(Teleport_SessionComponent session in copiedSessions)
			{
				if(session.GeometryStreamingService != null)
				{
					session.GeometryStreamingService.StopStreamingGameObject(gameObject);
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
		}

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

		private avs.MovementUpdate GetNodeMovementUpdate(GameObject node, uid clientID)
		{
			//Node should already have been added; but AddNode(...) will do the equivalent of FindResourceID(...), but with a fallback.
			uid nodeID = GeometrySource.GetGeometrySource().AddNode(node, false);

			avs.MovementUpdate update = new avs.MovementUpdate();
			update.timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			update.nodeID = nodeID;

			if(GeometryStreamingService.IsClientRenderingParent(clientID, node))
			{
				update.isGlobal = false;
				update.position = node.transform.localPosition;
				update.rotation = node.transform.localRotation;
			}
			else
			{
				update.isGlobal = true;
				update.position = node.transform.position;
				update.rotation = node.transform.rotation;
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
	}
}