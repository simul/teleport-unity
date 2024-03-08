using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using UnityEditor.PackageManager;
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
		avs.MovementUpdate previousLocalUpdate;
		bool previousMovementValid=false;
		public void ResetPreviousMovement()
		{
			previousMovementValid = false;
		}
		// Update the velocity, angular velocity for smoothed motion.
		public void CalcMovementUpdate()
		{
			localUpdate.nodeID = nodeID;
			localUpdate.time_since_server_start_us = teleport.Monitor.GetSessionTimestampNowUs();
			Int64 delta_time_us = localUpdate.time_since_server_start_us - previousLocalUpdate.time_since_server_start_us;
			if (delta_time_us == 0)
				return;
			if (!transform.hasChanged)
			{
				localUpdate.velocity = Vector3.zero;
				localUpdate.angularVelocityAngle = 0.0F;
				return;
			}
			transform.hasChanged = false;
			StreamableRoot streamableRoot= gameObject.GetComponent<StreamableRoot>();
			if(streamableRoot!=null)
			{
				localUpdate.isGlobal = true;
				localUpdate.position = gameObject.transform.position;
				localUpdate.rotation = gameObject.transform.rotation;
				localUpdate.scale = gameObject.transform.lossyScale;
			}
			else
			{
				localUpdate.isGlobal = false;
				localUpdate.position = gameObject.transform.localPosition;
				localUpdate.rotation = gameObject.transform.localRotation;
				localUpdate.scale = gameObject.transform.localScale;
			}
			bool do_smoothing = false;
			StreamableProperties streamableProperties = gameObject.GetComponent<StreamableProperties>();
			if (streamableProperties)
				do_smoothing = streamableProperties.smoothMotionAtClient;
			if (do_smoothing && previousMovementValid)
			{
				TeleportSettings teleportSettings = TeleportSettings.GetOrCreateSettings();
				Vector3 position;
				Quaternion rotation;

				position = gameObject.transform.localPosition;
				rotation = gameObject.transform.localRotation;
				double delta_time_s = Time.deltaTime;//delta_time_us * 0.000001;

				//We cast to the unity engine types to take advantage of the existing vector subtraction operators.
				//We multiply by the amount of move updates per second to get the movement per second, rather than per update.
				Rigidbody r = GetComponent<Rigidbody>();
				if (r && !r.isKinematic)
				{
					localUpdate.velocity = r.velocity;
					if (r.angularVelocity.sqrMagnitude > 0.000001)
					{
						localUpdate.angularVelocityAxis = r.angularVelocity.normalized;
						localUpdate.angularVelocityAngle = r.angularVelocity.magnitude;
					}
					else
					{
						localUpdate.angularVelocityAxis = Vector3.zero;
						localUpdate.angularVelocityAngle = 0;
					}
				}
				else
				{
					localUpdate.velocity = (position - previousLocalUpdate.position) / (float)delta_time_s;
					(rotation * Quaternion.Inverse(previousLocalUpdate.rotation)).ToAngleAxis(out localUpdate.angularVelocityAngle, out Vector3 angularVelocityAxis);
					if (localUpdate.angularVelocityAngle != 0)
					{
						//Angle needs to be inverted, for some reason.
						localUpdate.angularVelocityAngle /= (float)delta_time_s;
						localUpdate.angularVelocityAngle *= -Mathf.Deg2Rad;
					}
					float smoothingFactor=0.8F;
					if (previousMovementValid)
					{
						UnityEngine.Vector3 v0= previousLocalUpdate.velocity;
						UnityEngine.Vector3 v1= localUpdate.velocity;
						localUpdate.velocity=(smoothingFactor*v0)+(1.0F- smoothingFactor)*v1;
						UnityEngine.Vector3 a0 = previousLocalUpdate.angularVelocityAxis;
						UnityEngine.Vector3 a1 = localUpdate.angularVelocityAxis;
						UnityEngine.Vector3 a = (smoothingFactor * a0) + (1.0F - smoothingFactor) * a1;
						a.Normalize();
						localUpdate.angularVelocityAxis=a;
						localUpdate.angularVelocityAngle = (smoothingFactor * previousLocalUpdate.angularVelocityAngle) + (1.0F - smoothingFactor) * localUpdate.angularVelocityAngle;
			
					}
					localUpdate.angularVelocityAxis = angularVelocityAxis;
				}
			}
			else
			{
				localUpdate.velocity = Vector3.zero;
				localUpdate.angularVelocityAngle = 0.0F;
			}

			previousLocalUpdate = localUpdate;
			previousMovementValid = true;
		}
		public avs.MovementUpdate GetMovementUpdate(uid clientID)
		{
			return localUpdate;
		}
	}
}