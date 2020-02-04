using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

using uid = System.UInt64;

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

    Dictionary<GameObject, GCHandle> gameObjectHandles = new Dictionary<GameObject, GCHandle>();

    public void AddActor(uid clientID, GameObject actor)
    {
        uid actorID = CasterMonitor.GetGeometrySource().AddNode(actor);
        if(actorID != 0)
        {
            GCHandle actorHandle = GCHandle.Alloc(actor, GCHandleType.Pinned);

            AddActor(clientID, actorHandle.AddrOfPinnedObject(), actorID);
            gameObjectHandles.Add(actor, actorHandle);
        }
    }

    public uid RemoveActor(uid clientID, GameObject actor)
    {
        uid actorID = RemoveActor(clientID, gameObjectHandles[actor].AddrOfPinnedObject());
        gameObjectHandles.Remove(actor);

        return actorID;
    }

    public uid GetActorID(uid clientID, GameObject actor)
    {
        return GetActorID(clientID, gameObjectHandles[actor].AddrOfPinnedObject());
    }

    public bool IsStreamingActor(uid clientID, GameObject actor)
    {
        return IsStreamingActor(clientID, gameObjectHandles[actor].AddrOfPinnedObject());
    }
}
