using System;
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
		[DllImport("SimulCasterServer")]
		private static extern void AddLight(uid clientID, IntPtr newLight, uid lightID);
		[DllImport("SimulCasterServer")]
		private static extern void AddActor(uid clientID, IntPtr newActor, uid actorID, avs.Transform currentTransform);
		[DllImport("SimulCasterServer")]
		private static extern uid RemoveActor(uid clientID, IntPtr oldActor);
		[DllImport("SimulCasterServer")]
		private static extern uid GetActorID(uid clientID, IntPtr actor);

		[DllImport("SimulCasterServer")]
		public static extern void ActorEnteredBounds(uid clientID, uid actorID);
		[DllImport("SimulCasterServer")]
		public static extern void ActorLeftBounds(uid clientID, uid actorID);
		[DllImport("SimulCasterServer")]
		private static extern bool IsStreamingActor(uid clientID, IntPtr actor);

		[DllImport("SimulCasterServer")]
		public static extern void ShowActor(uid clientID, uid actorID);
		[DllImport("SimulCasterServer")]
		public static extern void HideActor(uid clientID, uid actorID);
		[DllImport("SimulCasterServer")]
		public static extern void SetActorVisible(uid clientID, uid actorID, bool isVisible);
		[DllImport("SimulCasterServer")]
		public static extern bool IsClientRenderingActorID(uid clientID, uid actorID);
		[DllImport("SimulCasterServer")]
		public static extern bool IsClientRenderingActorPtr(uid clientID, IntPtr actorPtr);

		[DllImport("SimulCasterServer")]
		public static extern bool HasResource(uid clientID, uid resourceID);

		[DllImport("SimulCasterServer")]
		private static extern void UpdateActorMovement(uid clientID, avs.MovementUpdate[] updates, int updateAmount);
		#endregion

		private readonly Teleport_SessionComponent session;
		private readonly TeleportSettings teleportSettings;

		private List<Collider> streamedObjects = new List<Collider>();
		private List<Light> streamedLights = new List<Light>();

		//Stores handles to game objects, so the garbage collector doesn't move/delete the objects while they're being referenced by the native plug-in.
		private Dictionary<GameObject, GCHandle> gameObjectHandles = new Dictionary<GameObject, GCHandle>();

		private Dictionary<uid, avs.MovementUpdate> previousMovements = new Dictionary<uid, avs.MovementUpdate>();
		private float timeSincePositionUpdate = 0;

		public GeometryStreamingService(Teleport_SessionComponent parentComponent)
		{
			session = parentComponent;

			teleportSettings = TeleportSettings.GetOrCreateSettings();
			timeSincePositionUpdate = 1 / teleportSettings.moveUpdatesPerSecond;
		}

		public void Clear()
        {
			streamedObjects.Clear();
			streamedLights.Clear();

			RemoveAllActors();

			previousMovements.Clear();
        }

		public void RemoveAllActors()
		{
			foreach(GCHandle handle in gameObjectHandles.Values)
			{
				RemoveActor(session.GetClientID(), GCHandle.ToIntPtr(handle));
			}

			gameObjectHandles.Clear();
		}

		public uid AddActor(GameObject actor)
		{
			GeometrySource geometrySource = GeometrySource.GetGeometrySource();
			uid actorID = geometrySource.AddNode(actor);

			if(actorID != 0 && !gameObjectHandles.ContainsKey(actor))
			{
				GCHandle actorHandle = GCHandle.Alloc(actor, GCHandleType.Pinned);

				AddActor(session.GetClientID(), GCHandle.ToIntPtr(actorHandle), actorID, (avs.Transform)actor.transform);
				gameObjectHandles.Add(actor, actorHandle);
			}

			return actorID;
		}

		public uid RemoveActor(GameObject actor)
		{
			uid actorID = RemoveActor(session.GetClientID(), GCHandle.ToIntPtr(gameObjectHandles[actor]));
			gameObjectHandles.Remove(actor);

			return actorID;
		}

		public uid GetActorID(GameObject actor)
		{
			return GetActorID(session.GetClientID(), GCHandle.ToIntPtr(gameObjectHandles[actor]));
		}

		public bool IsStreamingActor(GameObject actor)
		{
			return IsStreamingActor(session.GetClientID(), GCHandle.ToIntPtr(gameObjectHandles[actor]));
		}

		public void SetVisibleLights(Light[] lights)
		{
			foreach(var l in lights)
			{
				uid actorID = AddActor(l.gameObject);
				if(!streamedLights.Contains(l))
					streamedLights.Add(l);
			}
		}

		public int GetStreamedObjectCount()
        {
			return streamedObjects.Count;
        }

		public int GetStreamedLightCount()
        {
			return streamedLights.Count;
        }

		public void UpdateGeometryStreaming()
		{
			//Detect changes in geometry that needs to be streamed to the client.
			if(teleportSettings.LayersToStream != 0)
			{
				List<Collider> innerSphereCollisions = new List<Collider>(Physics.OverlapSphere(session.transform.position, teleportSettings.casterSettings.detectionSphereRadius, teleportSettings.LayersToStream));
				List<Collider> outerSphereCollisions = new List<Collider>(Physics.OverlapSphere(session.transform.position, teleportSettings.casterSettings.detectionSphereRadius + teleportSettings.casterSettings.detectionSphereBufferDistance, teleportSettings.LayersToStream));

				List<Collider> gainedColliders = new List<Collider>(innerSphereCollisions.Except(streamedObjects));
				List<Collider> lostColliders = new List<Collider>(streamedObjects.Except(outerSphereCollisions));

				foreach(Collider collider in gainedColliders)
				{
					//Skip game objects without the streaming tag.
					if(teleportSettings.TagToStream.Length>0 && !collider.CompareTag(teleportSettings.TagToStream))
						continue;
					// Add to the list without first checking if it can be added. So it won't spam failure messages.
					streamedObjects.Add(collider);
					uid actorID = AddActor(collider.gameObject);
					if(actorID != 0)
					{
						ActorEnteredBounds(session.GetClientID(), actorID);
					}
					else
					{
						Debug.LogWarning("Failed to add game object to stream: " + collider.gameObject.name);
					}
				}

				foreach(Collider collider in lostColliders)
				{
					streamedObjects.Remove(collider);

					uid actorID = RemoveActor(collider.gameObject);
					if(actorID != 0)
					{
						ActorLeftBounds(session.GetClientID(), actorID);
					}
					else
					{
						Debug.LogWarning("Attempted to remove actor that was not being streamed: " + collider.gameObject.name);
					}
				}
			}
			else
			{
				teleport.TeleportLog.LogErrorOnce("Teleport geometry streaming layer is not defined! Please assign layer masks under \"Layers To Stream\".");
			}

			//Send position updates, if enough time has elapsed.
			timeSincePositionUpdate += Time.deltaTime;
			if(session.IsConnected() && timeSincePositionUpdate >= 1.0f / teleportSettings.moveUpdatesPerSecond)
			{
				SendPositionUpdates();
				timeSincePositionUpdate = 0;
			}
		}

		public void SendPositionUpdates()
        {
			TeleportSettings teleportSettings = TeleportSettings.GetOrCreateSettings();
			if(teleportSettings == null) return;

			avs.MovementUpdate[] updates = new avs.MovementUpdate[gameObjectHandles.Count];
			long timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

			int i = 0;
			foreach(GameObject node in gameObjectHandles.Keys)
            {
				GeometrySource geometrySource = GeometrySource.GetGeometrySource();
				uid nodeID = geometrySource.FindNode(node);

				if(nodeID == 0) return;

				updates[i].timestamp = timestamp;
				updates[i].nodeID = nodeID;

                if(IsClientRenderingParent(node))
                {
					updates[i].isGlobal = false;
					updates[i].position = node.transform.localPosition;
                    updates[i].rotation = node.transform.localRotation;
				}
                else
				{
					updates[i].isGlobal = true;
					updates[i].position = node.transform.position;
					updates[i].rotation = node.transform.rotation;
				}

				if(previousMovements.TryGetValue(nodeID, out avs.MovementUpdate previousMovement))
				{
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
					updates[i].velocity = (position - previousMovement.position) * teleportSettings.moveUpdatesPerSecond;

					(rotation * Quaternion.Inverse(previousMovement.rotation)).ToAngleAxis(out updates[i].angularVelocityAngle, out Vector3 angularVelocityAxis);
					updates[i].angularVelocityAxis = angularVelocityAxis;
					//Angle needs to be inverted, for some reason.
					updates[i].angularVelocityAngle *= teleportSettings.moveUpdatesPerSecond * -Mathf.Deg2Rad;
				}

				previousMovements[nodeID] = updates[i];

				++i;
            }

			UpdateActorMovement(session.GetClientID(), updates, updates.Length);
        }

		private bool IsClientRenderingParent(GameObject child)
        {
			if(child.transform.parent)
			{
				gameObjectHandles.TryGetValue(child.transform.parent.gameObject, out GCHandle parentHandle);
				if(parentHandle.IsAllocated)
				{
					return IsClientRenderingActorPtr(session.GetClientID(), GCHandle.ToIntPtr(parentHandle));
				}
			}

			return false;
		}
	}
}