using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEditor.PackageManager;
using UnityEngine;

using uid = System.UInt64;

namespace teleport
{
	public class GeometryStreamingService
	{
		#region DLLImports
		[DllImport("SimulCasterServer")]
		private static extern void AddActor(uid clientID, IntPtr newActor, uid actorID);
		[DllImport("SimulCasterServer")]
		private static extern uid RemoveActor(uid clientID, IntPtr oldActor);
		[DllImport("SimulCasterServer")]
		private static extern uid GetActorID(uid clientID, IntPtr actor);
		[DllImport("SimulCasterServer")]
		private static extern bool IsStreamingActor(uid clientID, IntPtr actor);
		[DllImport("SimulCasterServer")]
		public static extern void ShowActor(uid clientID, uid actorID);
		[DllImport("SimulCasterServer")]
		public static extern void HideActor(uid clientID, uid actorID);
		[DllImport("SimulCasterServer")]
		public static extern void SetActorVisible(uid clientID, uid actorID, bool isVisible);
		[DllImport("SimulCasterServer")]
		public static extern bool HasResource(uid clientID, uid resourceID);
		[DllImport("SimulCasterServer")]
		private static extern void AddLight(uid clientID, IntPtr newLight, uid lightID);

		[DllImport("SimulCasterServer")]
		private static extern void UpdateActorMovement(uid clientID, avs.MovementUpdate[] updates, int updateAmount);
		#endregion

		//Stores handles to game objects, so the garbage collector doesn't move/delete the objects while they're being referenced by the native plug-in.
		Dictionary<GameObject, GCHandle> gameObjectHandles = new Dictionary<GameObject, GCHandle>();

		Dictionary<uid, avs.MovementUpdate> previousMovements = new Dictionary<uid, avs.MovementUpdate>();

		public void RemoveAllActors(uid clientID)
		{
			foreach(GCHandle handle in gameObjectHandles.Values)
			{
				RemoveActor(clientID, GCHandle.ToIntPtr(handle));
			}

			gameObjectHandles.Clear();
		}

		public uid AddActor(uid clientID, GameObject actor)
		{
			GeometrySource geometrySource = GeometrySource.GetGeometrySource();
			uid actorID = geometrySource.AddNode(actor);

			if(actorID != 0 && !gameObjectHandles.ContainsKey(actor))
			{
				GCHandle actorHandle = GCHandle.Alloc(actor, GCHandleType.Pinned);

				AddActor(clientID, GCHandle.ToIntPtr(actorHandle), actorID);
				gameObjectHandles.Add(actor, actorHandle);
			}

			return actorID;
		}

		public uid RemoveActor(uid clientID, GameObject actor)
		{
			uid actorID = RemoveActor(clientID, GCHandle.ToIntPtr(gameObjectHandles[actor]));
			gameObjectHandles.Remove(actor);

			return actorID;
		}

		public uid GetActorID(uid clientID, GameObject actor)
		{
			return GetActorID(clientID, GCHandle.ToIntPtr(gameObjectHandles[actor]));
		}

		public bool IsStreamingActor(uid clientID, GameObject actor)
		{
			return IsStreamingActor(clientID, GCHandle.ToIntPtr(gameObjectHandles[actor]));
		}

		public void SendPositionUpdates(uid clientID)
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
				updates[i].position = node.transform.localPosition;
				updates[i].rotation = node.transform.localRotation;

				if(previousMovements.TryGetValue(nodeID, out avs.MovementUpdate previousMovement))
                {
					Quaternion oldRotation = previousMovement.rotation;

					//We cast to the unity engine types to take advantage of the existing vector subtraction operators.
					//We multiply by the amount of move updates per second to get the movement per second, rather than per update.
					updates[i].velocity = ((Vector3)updates[i].position - previousMovement.position) * teleportSettings.moveUpdatesPerSecond;

					(node.transform.localRotation * Quaternion.Inverse(oldRotation)).ToAngleAxis(out updates[i].angularVelocityAngle, out Vector3 angularVelocityAxis);
					updates[i].angularVelocityAxis = angularVelocityAxis;
					//Angle needs to be inverted, for some reason.
					updates[i].angularVelocityAngle *= teleportSettings.moveUpdatesPerSecond * -Mathf.Deg2Rad;
				}

				previousMovements[nodeID] = updates[i];

				++i;
            }

			UpdateActorMovement(clientID, updates, updates.Length);
        }
	}
}