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
		private static extern uid Client_RemoveActorByID(uid clientID, uid actorID);

		[DllImport("SimulCasterServer")]
		public static extern void Client_ActorEnteredBounds(uid clientID, uid actorID);
		[DllImport("SimulCasterServer")]
		public static extern void Client_ActorLeftBounds(uid clientID, uid actorID);
		[DllImport("SimulCasterServer")]
		private static extern bool Client_IsStreamingActorID(uid clientID, uid actorID);

		[DllImport("SimulCasterServer")]
		public static extern void Client_ShowActor(uid clientID, uid actorID);
		[DllImport("SimulCasterServer")]
		public static extern void Client_HideActor(uid clientID, uid actorID);
		[DllImport("SimulCasterServer")]
		public static extern void Client_SetActorVisible(uid clientID, uid actorID, bool isVisible);
		[DllImport("SimulCasterServer")]
		public static extern bool Client_IsClientRenderingActorID(uid clientID, uid actorID);

		[DllImport("SimulCasterServer")]
		public static extern bool Client_HasResource(uid clientID, uid resourceID);

		[DllImport("SimulCasterServer")]
		private static extern void Client_UpdateActorMovement(uid clientID, avs.MovementUpdate[] updates, int updateAmount);
		#endregion

		private readonly Teleport_SessionComponent session;
		private readonly TeleportSettings teleportSettings;

		private List<Collider> streamedColliders = new List<Collider>();
		private List<GameObject> streamedGameObjects = new List<GameObject>();
		private List<GameObject> failedGameObjects = new List<GameObject>(); 
		private Dictionary<uid,Light> streamedLights = new Dictionary<uid, Light>();

		//Stores handles to game objects, so the garbage collector doesn't move/delete the objects while they're being referenced by the native plug-in.
		// Roderick's note: this does not appear to work. Instead, we get null pointers.
		//private Dictionary<GameObject, GCHandle> gameObjectHandles = new Dictionary<GameObject, GCHandle>();

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
			foreach(var gameObject in streamedGameObjects)
			{
				Client_RemoveActorByID(session.GetClientID(), gameObject.GetComponent<Teleport_Streamable>().GetUid());
			}
		}

		uid AddActor(GameObject actor)
		{
			GeometrySource geometrySource = GeometrySource.GetGeometrySource();
			uid actorID = geometrySource.AddNode(actor);
			if(actorID==0)
			{
				Debug.LogError("AddNode failed for "+actor.name+".");
				return actorID;
			}
			if(actorID != 0 )
			{
				Client_AddActor(session.GetClientID(), new IntPtr(0), actorID, avs.Transform.FromGlobalUnityTransform(actor.transform));
			}

			return actorID;
		}

		public uid GetActorID(GameObject gameObject)
		{
			var streamable = gameObject.GetComponent<Teleport_Streamable>();
			if (streamable == null)
				return 0;
			return streamable.GetUid();
		}

		public bool IsStreamingActor(GameObject gameObject)
		{
			var streamable= gameObject.GetComponent<Teleport_Streamable>();
			if (streamable==null)
				return false;
			return Client_IsStreamingActorID(session.GetClientID(), streamable.GetUid());
		}
		/// <summary>
		/// Set the lights to be streamed to this client.
		///		If they are streamable, this will:
		///			- ensure that their nodes are streamed.
		///			- tell the client that these are the active lights.
		///		If not streamable, the lights will not be sent.
		/// </summary>
		/// <param name="lights"></param>
		public void SetStreamedLights(Light[] lights)
		{
			HashSet<uid> streamedNow = new HashSet<uid>();
			foreach(Light light in lights)
			{
			// if the light is not streamable, don't stream.
				var streamable = light.gameObject.GetComponent<Teleport_Streamable>();
				if (!streamable)
					continue;
				if (!streamedGameObjects.Contains(light.gameObject))
				{
					StartStreamingGameObject(light.gameObject);
				}
				var uid = streamable.GetUid();
				if (uid == 0)
					continue;
				streamedNow.Add(uid);
				if (!streamedLights.ContainsKey(uid))
				{
					streamedLights[uid] = light;
				}
			}
			while(streamedLights.Count> streamedNow.Count)
			foreach(var u in streamedLights)
			{
				if(!streamedNow.Contains(u.Key))
				{
					streamedLights.Remove(u.Key);
					if(u.Value!=null)
						StopStreamingGameObject(u.Value.gameObject);
					break;
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
						StartStreamingGameObject(collider.gameObject);
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
			if (failedGameObjects.Contains(gameObject))
				return false;
			if (streamedGameObjects.Contains(gameObject))
			{
				Debug.LogError("StartStreamingGameObject called on " + gameObject.name + " which is already streamed.");
				return false;
			}
			var streamable = gameObject.GetComponent<Teleport_Streamable>();
			if (streamable == null)
			{
				Debug.LogError("Game object " + gameObject.name + " has no Teleport_Streamable component.");
				return false;
			}
			bool any = false;
			// include all collisionless children.
			foreach(var g in streamable.includedChildren)
			{
				if (failedGameObjects.Contains(g))
					continue;
				if (streamedGameObjects.Contains(g))
					continue;
				uid actorID = AddActor(g);
				if (actorID == 0)
				{
					Debug.LogWarning("Failed to add game object for streaming: " + g.name);
					failedGameObjects.Add(g);
				}
				else
				{
					any = true;
					Client_ActorEnteredBounds(session.GetClientID(), actorID);
					streamedGameObjects.Add(g);
					var s = g.GetComponent<Teleport_Streamable>();
					s.SetUid(actorID);
					s.AddStreamingClient(session);
				}
			}	
			// Add to the list without first checking if it can be added. So it won't spam failure messages.
			var colliders = gameObject.GetComponents<Collider>();
			foreach (var c in colliders)
				streamedColliders.Add(c);			

			return true;
		}

		public void StopStreamingGameObject(GameObject gameObject)
		{
			var colliders = gameObject.GetComponents<Collider>();
			foreach (var c in colliders)
				streamedColliders.Remove(c);
			streamedGameObjects.Remove(gameObject);
			var streamable = gameObject.GetComponent<Teleport_Streamable>();
			if (streamable == null)
			{
				Debug.LogError("Game object " + gameObject.name + " has no Teleport_Streamable component.");
			}

			// include all collisionless children.
			foreach (var g in streamable.includedChildren)
			{
				var s = g.GetComponent<Teleport_Streamable>();
				if (s == null)
				{
					continue;
				}
				uid actorID = Client_RemoveActorByID(session.GetClientID(), s.GetUid());
				if (actorID != 0)
				{
					Client_ActorLeftBounds(session.GetClientID(), actorID);
				}
				else
				{
					Debug.LogWarning("Attempted to remove actor that was not being streamed: " + g.name);
				}
				s.RemoveStreamingClient(session);
			}
		}

		private void DetectInvalidStreamables()
		{
			for (int i = streamedGameObjects.Count - 1; i >= 0; i--)
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
			if(teleportSettings == null)
				return;

			avs.MovementUpdate[] updates = new avs.MovementUpdate[streamedGameObjects.Count];
			long timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

			int i = 0;
			foreach(GameObject node in streamedGameObjects)
            {
				if (node == null)
				{
					continue;
				}
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
				if(child.transform.parent.gameObject)
				{
					var parent_Streamable = child.transform.parent.gameObject.GetComponent<Teleport_Streamable>();
					if (parent_Streamable == null)
						return false;
					return Client_IsClientRenderingActorID(session.GetClientID(), parent_Streamable.GetUid());
				}
			}

			return false;
		}
	}
}