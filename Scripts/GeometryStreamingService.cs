using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
        #endregion

        //Stores handles to game objects, so the garbage collector doesn't move/delete the objects while they're being referenced by the native plug-in.
        Dictionary<GameObject, GCHandle> gameObjectHandles = new Dictionary<GameObject, GCHandle>();

        public uid AddActor(uid clientID, GameObject actor)
        {
            uid actorID = teleport.CasterMonitor.GetGeometrySource().AddNode(actor);
            if(actorID != 0)
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
    }
}