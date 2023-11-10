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
		public bool nodeEnabled;            //Whether the node is nodeEnabled on the server.

		//Cached references to components.
		public MeshRenderer meshRenderer;
		public SkinnedMeshRenderer skinnedMeshRenderer;
		public Light light;
		public avs.MovementUpdate localUpdate = new avs.MovementUpdate();
		public avs.MovementUpdate globalUpdate = new avs.MovementUpdate();
		// These can be received from 
		public Vector3 stageSpaceVelocity = new Vector3(0, 0, 0);
		public Vector3 stageSpaceAngularVelocity = new Vector3(0, 0, 0);
		public StreamableNode()
		{
			nodeID = GeometrySource.GetGeometrySource().FindResourceID(gameObject);
			nodeEnabled = true;

			meshRenderer = default;
			skinnedMeshRenderer = default;
			light = default;

			if (nodeID == 0)
			{
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

		//COVERSION OPERATORS

		//StreamableNode is basically a meta-wrapper for a GameObject, so we should be able to convert to the GameObject we are wrapping.
		public static implicit operator GameObject(StreamableNode streamedNode)
		{
			return streamedNode.gameObject;
		}
	}
}