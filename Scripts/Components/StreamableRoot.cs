using System;
using System.Collections.Generic;
using UnityEngine;
using VLB;
using uid = System.UInt64;

namespace teleport
{
	[HelpURL("https://docs.teleportvr.io/unity.html")]
	//This component is automatically added to GameObjects that meet the criteria for geometry streaming.
	[DisallowMultipleComponent]
	public class StreamableRoot : MonoBehaviour
	{
		//! The highest priority of StreamableProperties found in the streamable's hierarchy.
		public int priority = 0;

		//All GameObjects objects in the streamed hierarchy this teleport.StreamableRoot represents.
		HashSet<StreamableNode> streamedHierarchy = new HashSet<StreamableNode>();
		public HashSet<StreamableNode> GetStreamableNodes(){
			return streamedHierarchy;
		}

		[SerializeField]
		bool _sendMovementUpdates = true;
		public bool sendMovementUpdates
		{
			get {return _sendMovementUpdates; }
			set
			{
				if (_sendMovementUpdates != value)
				{
					_sendMovementUpdates = value;
					UpdateHierarchy();
				}
			}
		}
		[SerializeField]
		bool _sendEnabledStateUpdates = true;
		public bool sendEnabledStateUpdates
		{
			get { return _sendEnabledStateUpdates; }
			set
			{
				if (_sendEnabledStateUpdates != value)
				{
					_sendEnabledStateUpdates = value;
					UpdateHierarchy();
				}
			}
		}
		[SerializeField]
		bool _trackAllEnabledStates = false;
		public bool trackAllEnabledStates
		{
			get { return _trackAllEnabledStates; }
			set
			{
				if (_trackAllEnabledStates != value)
				{
					_trackAllEnabledStates = value;
					UpdateHierarchy();
				}
			}
		}
		
		public bool pollCurrentAnimation = false;

		private uid uid = 0;

		private uid owner_client_uid = 0;

		public uid OwnerClient
		{
			get
			{
				return owner_client_uid;
			}
			set
			{
				if (value != this.owner_client_uid)
				{
					owner_client_uid = value;
				}
			}
		}

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
			// Don't send updates to a client that owns the node.
			if(!_sendMovementUpdates||owner_client_uid==clientID)
			{
				return new List<avs.MovementUpdate>();
			}

			//TODO: Cache result every tick, so we don't calculate it per client, or send it to every client on the C++/Unmanaged side.
			List<avs.MovementUpdate> updates = new List<avs.MovementUpdate>();
			
			//Add movement updates for GameObjects in streamed hierarchy to the list.
			foreach(StreamableNode streamedNode in streamedHierarchy)
			{
				if(!streamedNode.gameObject)
				{
					Debug.LogWarning($"Failed to update movement of node! Null node in {nameof(teleport.StreamableRoot)}.childHierarchy of \"{name}\"!");
					continue;
				}
				var update=GetNodeMovementUpdate(streamedNode, clientID);
				if(update.nodeID!=0)
					updates.Add(update);
			}

			return updates;
		}
		UInt64 enabled_state_hash=0;
		//Tells the client what nodes have changed their enabled state and should be hidden or shown.
		public UInt64 GetEnabledStateUpdates(double time_last_checked,ref List<avs.NodeUpdateEnabledState> updates)
		{
			//Return an empty update list, if we're not sending enabled state updates.
			if(!_sendEnabledStateUpdates)
			{
				return 0;
			}

			//TODO: Support multiple clients, or send it to every client on the C++/Unmanaged side.
			updates.Clear();
			var activeNodeTrackers=GetComponentsInChildren< ActiveNodeTracker >();
			foreach (ActiveNodeTracker t in activeNodeTrackers)
			{
				//Send message if enabled state has changed.
				if(t.timeAtLastChange>time_last_checked)
				{
				//	updates.Add(new avs.NodeUpdateEnabledState{nodeID = streamedNode.nodeID, enabled = streamedNode.enabled});
				}
			}

			return enabled_state_hash;
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
			Teleport_SessionComponent sess=GetComponent< Teleport_SessionComponent >();
			if(sess)
			{ 
				CreateStreamedHierarchy();
			}
			else
				CreateStreamedHierarchy();
		}
		public void ForceInit()
        {
            OnEnable();

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
	
		HashSet<GameObject> exploredGameObjects = new HashSet<GameObject>();
		private void CreateStreamedHierarchy()
		{
			streamedHierarchy.Clear();
			priority=0;
			exploredGameObjects.Clear();
			//We need to stop once we reach this node, so we add it to the explored list, but that means we now have to explicitly add it to the streamed hierarchy.
			exploredGameObjects.Add(gameObject);
			streamedHierarchy.Add(gameObject.GetOrAddComponent<StreamableNode>());
			UpdateHierarchy();
		}
		public StreamableNode GetStreamedNode(GameObject go)
		{
			foreach(var n in streamedHierarchy)
			{
				if (n.gameObject==go)
					return n;
			}
			return null;
		}
		//! This will look in the children to see if any newly added objects should be in the streamed hierarchy.
		public void UpdateHierarchy()
		{
			// Check for nonstatic:
#if UNITY_EDITOR
			List<Transform> children = new List<Transform>(GetComponentsInChildren<Transform>(true));
			foreach (Transform t in children)
			{
				if (!t.gameObject.isStatic)
				{
					StreamableProperties streamableProperties=t.gameObject.GetComponent<StreamableProperties>();
					if(streamableProperties==null)
						streamableProperties=t.gameObject.AddComponent<StreamableProperties>();
					streamableProperties.isStationary = gameObject.isStatic;
				}
			}
#endif
			_sendMovementUpdates=false;
			// Get all the StreamableProperties instances in the hierarchy, and use them to find the maximum "priority" value.
			List<StreamableProperties> streamablePropertiesList = new List<StreamableProperties>(GetComponentsInChildren<StreamableProperties>(true));
			foreach (StreamableProperties streamableProperties in streamablePropertiesList)
			{
				priority=Math.Max(priority, streamableProperties.priority);
				_sendMovementUpdates|= !streamableProperties.isStationary;
			}
			//Mark all children that will be streamed separately as explored; i.e. marked for streaming.
			List<teleport.StreamableRoot> childRoots = new List<teleport.StreamableRoot>(GetComponentsInChildren<teleport.StreamableRoot>(true));
			foreach(teleport.StreamableRoot childRoot in childRoots)
			{
				StreamableProperties props= childRoot.GetComponent<StreamableProperties>();
				// Don't ignore if streamOnlyWithParent is set:
				if (props &&props.streamOnlyWithParent)
					continue;
				//GetComponentsInChildren(...) also grabs components from node we are on, but we don't want to exclude the children of the node we are on.
				if (childRoot.gameObject != gameObject && GeometrySource.GetGeometrySource().IsGameObjectMarkedForStreaming(childRoot.gameObject))
				{
					//Mark child's children as explored, as they are part of a different streaming hierarchy.
					Transform[] childTransforms = childRoot.GetComponentsInChildren<Transform>(true);
					foreach(Transform transform in childTransforms)
					{
						exploredGameObjects.Add(transform.gameObject);
					}
				}
			}
			// If this object has an Animator, grab the whole hierarchy.
			if(GetComponent<UnityEngine.Animator>()!=null)
			{
				AddComponentTypeToHierarchy<Transform>();
			}
			//Add components to hierarchy.
			AddComponentTypeToHierarchy<MeshRenderer>();
			AddComponentTypeToHierarchy<SkinnedMeshRenderer>();
			AddComponentTypeToHierarchy<Light>();

			//Add animator trackers to nodes in streamed hierarchy with animator components.
			//We use streamedHierarchy, rather than GetComponentInChildren(...),
			// as we need to restrict the search to the streamed hierarchy rather than the entire Transform hierarchy.
			foreach(GameObject gameObject in streamedHierarchy)
			{
				AddAnimatorTracker(gameObject);
			}
			foreach (var s in sessions)
            {
				s.GeometryStreamingService.StreamableHasChanged(this);
            }
		}

		protected void reportNodeEnabled(ActiveNodeTracker sender, bool enabled)
		{
			Debug.LogError("Node "+sender.name+(enabled?"Enabled":"Disabled"));
		}
		//Adds the GameObject of the passed transform to the hierarchy, if it has not already been explored in a depth-first search.
		//	transform : Transform of the GameObject we will add the the hierarchy, if unexplored.
		//	exploredGameObjects : GameObjects that have already been explored by the depth-first search, or we just don't want added to the hierarchy.
		private void AddToHierarchy(Transform transform)
		{
			if(transform)
			{
				GameObject gameObject = transform.gameObject;
				if(!exploredGameObjects.Contains(gameObject))
				{
					StreamableNode streamableNode=gameObject.GetOrAddComponent<teleport.StreamableNode>();
					exploredGameObjects.Add(gameObject);
					if(streamableNode!=null&&!streamedHierarchy.Contains(streamableNode))
						streamedHierarchy.Add(streamableNode);
					if(_trackAllEnabledStates)
					{
						gameObject.GetOrAddComponent<teleport.ActiveNodeTracker>();
					}
					var activeNodeTracker = gameObject.GetComponent<teleport.ActiveNodeTracker>();
					if (activeNodeTracker != null)
					{
						activeNodeTracker.report = reportNodeEnabled;
					}
					AddToHierarchy(transform.parent);
				}
			}
		}

		//Adds the GameObjects with a component of the generic type belonging to the teleport.StreamableRoot's Transform hierarchy to the streamable hierarchy,
		//if they do not appear in the explored list and are not already a part of the streamable hierarchy.
		//	exploredGameObjects : GameObjects that have already been explored by the depth-first search, or we just don't want added to the hierarchy.
		private void AddComponentTypeToHierarchy<T>() where T : Component
		{
			T[] components = GetComponentsInChildren<T>(true);

			foreach(T component in components)
			{
				AddToHierarchy(component.transform);

				//Cache the reference on the node in the streamed hierarchy.
				foreach(StreamableNode streamedNode in streamedHierarchy)
				{
					if(streamedNode == component.gameObject)
					{
						streamedNode.AddComponent(component);
						break;
					}
				}
			}
		}

		//Adds the Teleport_AnimatorTracker component to the passed node, if it has an Animator component and its Transform hierarchy has a SkinnedMeshRenderer.
		//	node : Node in the hierarchy we will attempt to add the Teleport_AnimatorTracker too.
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
		//! Make sure apparent motion is zero when sending the next movement update.
		public void ResetVelocityTracking(GameObject node)
		{
			uid nodeID = GeometrySource.GetGeometrySource().AddNode(node);
			previousMovements.Remove(nodeID);
			previousMovements.Remove(nodeID + 1000000000);
		}
		//! Make sure apparent motion is zero for the whole hierarchyu.
		public void ResetVelocityTracking()
		{
			previousMovements.Clear();
		}
		private avs.MovementUpdate GetNodeMovementUpdate(StreamableNode streamedNode, uid clientID)
		{
			GameObject node= streamedNode.gameObject;
			//Node should already have been added; but AddNode(...) will do the equivalent of FindResourceID(...), but with a fallback.
			uid nodeID = GeometrySource.GetGeometrySource().AddNode(node);
			uid prevID=nodeID;
			ref avs.MovementUpdate update= ref streamedNode.localUpdate;
			bool has_parent= GeometryStreamingService.IsClientRenderingParent(clientID, node);
			if (!has_parent)
			{
				update=ref streamedNode.globalUpdate;
				prevID=nodeID+1000000000;
			}
			if(update.time_since_server_start_ns == teleport.Monitor.GetServerTimeNs())
				return update;
			update.time_since_server_start_ns = teleport.Monitor.GetServerTimeNs();
			update.nodeID = nodeID;

			if (has_parent)
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
			bool do_smoothing=true;
			StreamableProperties streamableProperties=node.GetComponent<StreamableProperties>();
			if(streamableProperties)
				do_smoothing= streamableProperties.smoothMotionAtClient;
			if (do_smoothing&&previousMovements.TryGetValue(prevID, out avs.MovementUpdate previousMovement))
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
				Int64 delta_time_ns = update.time_since_server_start_ns - previousMovement.time_since_server_start_ns;
				if(delta_time_ns==0)
				{ 
					update.nodeID=0;
					return update;
				}
				if (delta_time_ns != 0)
                {
					double delta_time_s = delta_time_ns * 0.000000001;

					//We cast to the unity engine types to take advantage of the existing vector subtraction operators.
					//We multiply by the amount of move updates per second to get the movement per second, rather than per update.
					Rigidbody r=GetComponent<Rigidbody>();
                    if (r && !r.isKinematic)
                    {
                        update.velocity = r.velocity;
                        if (r.angularVelocity.sqrMagnitude > 0.000001)
                        {
                            update.angularVelocityAxis = r.angularVelocity.normalized;
                            update.angularVelocityAngle = r.angularVelocity.magnitude;
                        }
                        else
                        {
                            update.angularVelocityAxis = Vector3.zero;
                            update.angularVelocityAngle = 0;
                        }
                    }
                    else
                    {
                        update.velocity = (position - previousMovement.position) / (float)delta_time_s;
                        (rotation * Quaternion.Inverse(previousMovement.rotation)).ToAngleAxis(out update.angularVelocityAngle, out Vector3 angularVelocityAxis);
                        update.angularVelocityAxis = angularVelocityAxis;
                        if (update.angularVelocityAngle != 0)
                        {
                            //Angle needs to be inverted, for some reason.
                            update.angularVelocityAngle /= (float)delta_time_s;
                            update.angularVelocityAngle *= -Mathf.Deg2Rad;
                        }
                    }
				}
            }
			else
            {
				update.velocity=Vector3.zero;
				update.angularVelocityAngle=0.0F;
            }

            previousMovements[prevID] = update;
			return update;
		}

		public void ShowHierarchy()
		{
			foreach(StreamableNode streamedNode in streamedHierarchy)
			{
				AddClientMask(streamedNode);
			}
		}

		void AddClientMask(StreamableNode streamedNode)
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
			foreach(StreamableNode streamedNode in streamedHierarchy)
			{
				AddClientMask(streamedNode);
			}
		}

		void RemoveClientMask(StreamableNode streamedNode)
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
		Bounds bounds= new Bounds();
		bool boundsValid=false;
		public Bounds GetBounds()
		{
			if(streamedHierarchy.Count==0)
				CreateStreamedHierarchy();
			if(!boundsValid)
			{
				bounds.center=transform.position;
				bounds=new Bounds(transform.position,new Vector3(1.0F, 1.0F, 1.0F));
				foreach (var node in streamedHierarchy)
				{
					if(!node)
						continue;
					Collider coll = node.gameObject.GetComponent<Collider>();
					if (coll)
					{
						bounds.Encapsulate(coll.bounds);
					}
					else
					{
						MeshRenderer m = node.gameObject.GetComponent<MeshRenderer>();
						if (m)
						{
							bounds.Encapsulate(m.bounds);
						}
					}
				}
				boundsValid=true;
			}
			return bounds;
		}
	}
}