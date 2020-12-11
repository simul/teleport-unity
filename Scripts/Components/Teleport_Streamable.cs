using System.Collections;
using System.Collections.Generic;
using teleport;
using UnityEngine;

using uid = System.UInt64;

namespace teleport
{
	// This component is AUTOMATICALLY added to a gameObject that meets the criteria for geometry streaming.
	[DisallowMultipleComponent]
	public class Teleport_Streamable : MonoBehaviour
	{
		private uid uid = 0;
		//The GameObject itself; and the child objects that have no collision, so are streamed automatically with this one.
		public List<GameObject> streamedHierarchy = new List<GameObject>();

		private HashSet<Teleport_SessionComponent> sessions = new HashSet<Teleport_SessionComponent>();

		public uid GetUid()
		{
			if(uid == 0)
			{
				uid = GeometrySource.GetGeometrySource().FindResourceID(this);
			}

			return uid;
		}

		public void SetUid(uid u)
		{
			if(uid != 0 && u != uid)
			{
				Debug.LogError("Already have uid " + uid + " but overriding it with uid " + u);
			}
			uid = u;
		}

		public void AddStreamingClient(Teleport_SessionComponent sessionComponent)
		{
			sessions.Add(sessionComponent);
		}

		public void RemoveStreamingClient(Teleport_SessionComponent sessionComponent)
		{
			sessions.Remove(sessionComponent);
		}

		private void Start()
		{
			CreateStreamedHierarchy();
		}

		private void OnDestroy()
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

		private void CreateStreamedHierarchy()
		{
			streamedHierarchy.Clear();

			List<GameObject> exploredGameObjects = new List<GameObject>();
			exploredGameObjects.Add(gameObject);
			streamedHierarchy.Add(gameObject);

			//Mark all children that will be streamed separately as explored; i.e. is marked for streaming.
			List<Collider> childColliders = new List<Collider>(GetComponentsInChildren<Collider>());
			foreach(Collider childCollider in childColliders)
			{
				if(childCollider.gameObject != gameObject && GeometrySource.GetGeometrySource().IsGameObjectMarkedForStreaming(childCollider.gameObject))
				{
					//Mark all children as explored, as they are part of a different streaming hierarchy.
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
					streamedHierarchy.Add(gameObject);

					AddToHierarchy(transform.parent, exploredGameObjects);
				}
			}
		}
	}
}