using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using UnityEngine;
using uid = System.UInt64;

namespace teleport
{
	//This component is automatically added to streamable GameObjects and their children.
	[DisallowMultipleComponent]
	public class StreamableNode: MonoBehaviour
	{
		public uid nodeID;              //ID of the node.
		bool nodeEnabled;               //Whether the node is enabled on the server.
		public bool allowStreaming =true;		// If false, blocks streaming of this node, AND its children.

		//Cached references to components.
		public MeshRenderer meshRenderer;
		public SkinnedMeshRenderer skinnedMeshRenderer;
		public Light light;
		public avs.MovementUpdate localUpdate = new avs.MovementUpdate();
		public avs.MovementUpdate globalUpdate = new avs.MovementUpdate();
		// These can be received from 
		public Vector3 stageSpaceVelocity = new Vector3(0, 0, 0);
		public Vector3 stageSpaceAngularVelocity = new Vector3(0, 0, 0);
		public void Awake()
		{
			nodeID = GeometrySource.GetGeometrySource().FindResourceID(gameObject);
			nodeEnabled = true;

			meshRenderer = default;
			skinnedMeshRenderer = default;
			light = default;

			if (nodeID == 0)
			{
				nodeID= GeometrySource.GetGeometrySource().AddNode(gameObject);
				if(nodeID==0)
					UnityEngine.Debug.LogWarning($"We have created an invalid {nameof(StreamableNode)}! Node ID is {nodeID}!");
			}
		} 

		//Updates nodeEnabled state by checking cached references, and returns whether the nodeEnabled state changed.
		//WARNING: We currently do not support streaming multiple rendering components on the same GameObject; this will put precedence on MeshRenderers then SkinnedMeshRenderers then Lights.
		public bool UpdatedEnabledState()
		{
			bool wasEnabled = nodeEnabled;

			//StreamableNode is not nodeEnabled if it is not active in the hierarchy.
			if (!gameObject.activeInHierarchy)
			{
				nodeEnabled = false;
				return wasEnabled != nodeEnabled;
			}
			nodeEnabled = true;

			if (meshRenderer)
			{
				nodeEnabled = meshRenderer.enabled;
				return wasEnabled != nodeEnabled;
			}

			if (skinnedMeshRenderer)
			{
				nodeEnabled = skinnedMeshRenderer.enabled;
				return wasEnabled != nodeEnabled;
			}

			if (light)
			{
				nodeEnabled = light.enabled;
				return wasEnabled != nodeEnabled;
			}

			return wasEnabled != nodeEnabled;
		}

		public void AddComponent<T>(T component)
		{
			if (typeof(T) == typeof(MeshRenderer))
			{
				meshRenderer = GetComponent<MeshRenderer>();
			}
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

		//CONVERSION OPERATORS
		//Auto convert to GameObject.
		public static implicit operator GameObject(StreamableNode streamedNode)
		{
			return streamedNode.gameObject;
		}
		avs.MovementUpdate previousMovement;
		bool previousMovementValid=false;
		public void ResetPreviousMovement()
		{
			previousMovementValid = false;
		}
		public avs.MovementUpdate GetMovementUpdate(uid clientID)
		{
			if (!transform.hasChanged)
			{
				localUpdate.nodeID=0;
				return localUpdate;
			}
			transform.hasChanged = false;
			GameObject node = gameObject;
			//Node should already have been added; but AddNode(...) will do the equivalent of FindResourceID(...), but with a fallback.
			uid nodeID = GeometrySource.GetGeometrySource().AddNode(node);
			uid prevID = nodeID;
			ref avs.MovementUpdate update = ref localUpdate;
			bool has_parent = GeometryStreamingService.IsClientRenderingParent(clientID, node);
			if (!has_parent)
			{
				update = ref globalUpdate;
				prevID = nodeID + 1000000000;
			}
			if (update.time_since_server_start_us == teleport.Monitor.GetSessionTimestampNowUs())
				return update;
			update.time_since_server_start_us = teleport.Monitor.GetSessionTimestampNowUs();
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
				update.scale = node.transform.lossyScale;
			}
			bool do_smoothing = true;
			StreamableProperties streamableProperties = node.GetComponent<StreamableProperties>();
			if (streamableProperties)
				do_smoothing = streamableProperties.smoothMotionAtClient;
			if (do_smoothing && previousMovementValid)
			{
				TeleportSettings teleportSettings = TeleportSettings.GetOrCreateSettings();
				Vector3 position;
				Quaternion rotation;

				//Velocity and angular velocity must be calculated in the same basis as the previous movement.
				if (previousMovement.isGlobal)
				{
					position = node.transform.position;
					rotation = node.transform.rotation;
				}
				else
				{
					position = node.transform.localPosition;
					rotation = node.transform.localRotation;
				}
				Int64 delta_time_ns = update.time_since_server_start_us - previousMovement.time_since_server_start_us;
				if (delta_time_ns == 0)
				{
					update.nodeID = 0;
					return update;
				}
				if (delta_time_ns != 0)
				{
					double delta_time_s = delta_time_ns * 0.000000001;

					//We cast to the unity engine types to take advantage of the existing vector subtraction operators.
					//We multiply by the amount of move updates per second to get the movement per second, rather than per update.
					Rigidbody r = GetComponent<Rigidbody>();
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
				update.velocity = Vector3.zero;
				update.angularVelocityAngle = 0.0F;
			}

			previousMovement=update;
			previousMovementValid=true;
			return update;
		}
	}
}