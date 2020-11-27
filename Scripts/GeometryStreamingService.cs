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
		private static extern void Client_AddActor(uid clientID, IntPtr newActor, uid actorID, avs.Transform currentTransform);
		[DllImport("SimulCasterServer")]
		private static extern uid Client_RemoveActor(uid clientID, IntPtr oldActor);
		[DllImport("SimulCasterServer")]
		private static extern uid Client_GetActorID(uid clientID, IntPtr actor);

		[DllImport("SimulCasterServer")]
		public static extern void Client_ActorEnteredBounds(uid clientID, uid actorID);
		[DllImport("SimulCasterServer")]
		public static extern void Client_ActorLeftBounds(uid clientID, uid actorID);
		[DllImport("SimulCasterServer")]
		private static extern bool Client_IsStreamingActor(uid clientID, IntPtr actor);

		[DllImport("SimulCasterServer")]
		public static extern void Client_ShowActor(uid clientID, uid actorID);
		[DllImport("SimulCasterServer")]
		public static extern void Client_HideActor(uid clientID, uid actorID);
		[DllImport("SimulCasterServer")]
		public static extern void Client_SetActorVisible(uid clientID, uid actorID, bool isVisible);
		[DllImport("SimulCasterServer")]
		public static extern bool Client_IsClientRenderingActorID(uid clientID, uid actorID);
		[DllImport("SimulCasterServer")]
		public static extern bool Client_IsClientRenderingActorPtr(uid clientID, IntPtr actorPtr);

		[DllImport("SimulCasterServer")]
		public static extern bool Client_HasResource(uid clientID, uid resourceID);

		[DllImport("SimulCasterServer")]
		private static extern void Client_UpdateActorMovement(uid clientID, avs.MovementUpdate[] updates, int updateAmount);
		#endregion

		private readonly Teleport_SessionComponent session;
		private readonly TeleportSettings teleportSettings;

		private List<Collider> streamedColliders = new List<Collider>();
		private List<GameObject> streamedGameObjects = new List<GameObject>();
		private Dictionary<uid,Light> streamedLights = new Dictionary<uid, Light>();

		//Stores handles to game objects, so the garbage collector doesn't move/delete the objects while they're being referenced by the native plug-in.
		// Roderick's note: this does not appear to work. Errors refer to accessing deleted objects.
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
			streamedColliders.Clear();
			streamedGameObjects.Clear();
			streamedLights.Clear();

			RemoveAllActors();

			previousMovements.Clear();
        }

		public void RemoveAllActors()
		{
			foreach(GCHandle handle in gameObjectHandles.Values)
			{
				Client_RemoveActor(session.GetClientID(), GCHandle.ToIntPtr(handle));
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

				Client_AddActor(session.GetClientID(), GCHandle.ToIntPtr(actorHandle), actorID, avs.Transform.FromGlobalUnityTransform(actor.transform));
				gameObjectHandles.Add(actor, actorHandle);
			}

			return actorID;
		}

		public uid RemoveActor(GameObject gameObject)
		{
			uid actorID = Client_RemoveActor(session.GetClientID(), GCHandle.ToIntPtr(gameObjectHandles[gameObject]));
			gameObjectHandles.Remove(gameObject);

			return actorID;
		}

		public uid GetActorID(GameObject actor)
		{
			if(!gameObjectHandles.ContainsKey(actor))
			{
				return 0;
			}

			return Client_GetActorID(session.GetClientID(), GCHandle.ToIntPtr(gameObjectHandles[actor]));
		}

		public bool IsStreamingActor(GameObject actor)
		{
			return Client_IsStreamingActor(session.GetClientID(), GCHandle.ToIntPtr(gameObjectHandles[actor]));
		}

		public void SetVisibleLights(Light[] lights)
		{
			foreach(Light light in lights)
			{
				uid actorID = AddActor(light.gameObject);
				if(!streamedLights.ContainsKey(actorID))
				{
					streamedLights[actorID] = light;
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
				uid actorID = geometrySource.FindResourceID(gameObject);
				if(actorID != 0)
				{
					uids.Add(actorID);
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

		public void UpdateGeometryStreaming()
		{
			//Detect changes in geometry that needs to be streamed to the client.
			if(teleportSettings.LayersToStream != 0)
			{
				List<Collider> innerSphereCollisions = new List<Collider>(Physics.OverlapSphere(session.head.transform.position, teleportSettings.casterSettings.detectionSphereRadius, teleportSettings.LayersToStream));
				List<Collider> outerSphereCollisions = new List<Collider>(Physics.OverlapSphere(session.head.transform.position, teleportSettings.casterSettings.detectionSphereRadius + teleportSettings.casterSettings.detectionSphereBufferDistance, teleportSettings.LayersToStream));

				List<Collider> gainedColliders = new List<Collider>(innerSphereCollisions.Except(streamedColliders));
				List<Collider> lostColliders = new List<Collider>(streamedColliders.Except(outerSphereCollisions));

				foreach(Collider collider in gainedColliders)
				{
					//Skip game objects without the streaming tag.
					if(teleportSettings.TagToStream.Length == 0 || collider.CompareTag(teleportSettings.TagToStream))
					{
						if(!StartStreamingGameObject(collider.gameObject))
						{
							// Why would this fail?
							for(int i=0;i<streamedColliders.Count;i++)
							{
								var c = streamedColliders[i];
								if(c==collider)
								{
									Debug.LogError("collider matches.");
								}
							}
						}
					}
				}

				foreach(Collider collider in lostColliders)
				{
					StopStreamingGameObject(collider.gameObject);
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
				timeSincePositionUpdate = 0;

				DetectInvalidStreamables();
				SendPositionUpdates();
			}
		}

		private bool StartStreamingGameObject(GameObject gameObject)
		{
			if(streamedGameObjects.Contains(gameObject))
			{
				Debug.LogError("StartStreamingGameObject called on " + gameObject.name + " which is already streamed.");
				return false;
			}
			streamedGameObjects.Add(gameObject);
			// Add to the list without first checking if it can be added. So it won't spam failure messages.
			var colliders  = gameObject.GetComponents<Collider>();
			foreach(var c in colliders)
				streamedColliders.Add(c);
			uid actorID = AddActor(gameObject);
			if(actorID != 0)
			{
				Client_ActorEnteredBounds(session.GetClientID(), actorID);
			}
			else
			{
				Debug.LogWarning("Failed to add game object to stream: " + gameObject.name);
			}
			var streamable = gameObject.GetComponent<Teleport_Streamable>();
			if(streamable==null)
			{
				Debug.LogError("Game object "+gameObject.name+" has no Teleport_Streamable component.");
			}
			else
			{
				streamable.AddStreamingClient(session);
			}
			
			return true;
		}

		public void StopStreamingGameObject(GameObject gameObject)
		{
			var colliders = gameObject.GetComponents<Collider>();
			foreach (var c in colliders)
				streamedColliders.Remove(c);
			streamedGameObjects.Remove(gameObject);

			uid actorID = RemoveActor(gameObject);
			if(actorID != 0)
			{
				Client_ActorLeftBounds(session.GetClientID(), actorID);
			}
			else
			{
				Debug.LogWarning("Attempted to remove actor that was not being streamed: " + gameObject.name);
			}
			var streamable = gameObject.GetComponent<Teleport_Streamable>();
			if (streamable == null)
			{
				Debug.LogError("Game object " + gameObject.name + " has no Teleport_Streamable component.");
			}
			else
			{
				streamable.RemoveStreamingClient(session);
			}	
		}

		private void DetectInvalidStreamables()
		{
			for(int i = streamedGameObjects.Count - 1; i >= 0; i--)
			{
				GameObject gameObject= streamedGameObjects[i];
				if(!gameObject.CompareTag(teleportSettings.TagToStream))
				{
					StopStreamingGameObject(gameObject);
				}
			}
		}

		private void SendPositionUpdates()
        {
			TeleportSettings teleportSettings = TeleportSettings.GetOrCreateSettings();
			if(teleportSettings == null) return;

			avs.MovementUpdate[] updates = new avs.MovementUpdate[gameObjectHandles.Count];
			long timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

			int i = 0;
			foreach(GameObject node in gameObjectHandles.Keys)
            {
				GeometrySource geometrySource = GeometrySource.GetGeometrySource();
				uid nodeID = geometrySource.FindResourceID(node);

				if(nodeID == 0)
				{
					Debug.LogWarning($"Attempted to update movement of \"{node.name}\", but it has an ID of zero.");
					continue;
				}

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

			Client_UpdateActorMovement(session.GetClientID(), updates, updates.Length);
        }

		private bool IsClientRenderingParent(GameObject child)
        {
			if(child.transform.parent)
			{
				gameObjectHandles.TryGetValue(child.transform.parent.gameObject, out GCHandle parentHandle);
				if(parentHandle.IsAllocated)
				{
					return Client_IsClientRenderingActorPtr(session.GetClientID(), GCHandle.ToIntPtr(parentHandle));
				}
			}

			return false;
		}
	}
}