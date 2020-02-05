using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

using uid = System.UInt64;
namespace teleport
{
    public class Teleport_SessionComponent : MonoBehaviour
    {
        #region DLLImports
        [DllImport("SimulCasterServer")]
        public static extern void ActorEnteredBounds(uid clientID, uid actorID);
        [DllImport("SimulCasterServer")]
        public static extern void ActorLeftBounds(uid clientID, uid actorID);
        [DllImport("SimulCasterServer")]
        public static extern bool HasHost(uid clientID);
        [DllImport("SimulCasterServer")]
        public static extern bool HasPeer(uid clientID);
        [DllImport("SimulCasterServer")]
        public static extern string GetClientIP(uid clientID);
        [DllImport("SimulCasterServer")]
        public static extern System.UInt16 GetClientPort(uid clientID);
        [DllImport("SimulCasterServer")]
        public static extern System.UInt16 GetServerPort(uid clientID);
        [DllImport("SimulCasterServer")]
        private static extern uid GetUnlinkedClientID();
        [DllImport("SimulCasterServer")]

        private static extern void Client_SetOrigin(uid clientID, Vector3 pos);
        [DllImport("SimulCasterServer")]
        private static extern bool Client_IsConnected(uid clientID);
        [DllImport("SimulCasterServer")]
        private static extern bool Client_HasOrigin(uid clientID);
        #endregion
        #region Callbacks
        public static void StaticSetHeadPose(uid ClientID, in avs.HeadPose newHeadPose)
        {
            if (!sessions.ContainsKey(ClientID))
            {
                Debug.LogError("No Session Component found for "+ClientID);
                return;
            }
            sessions[ClientID].SetHeadPose(newHeadPose);
        }
        Quaternion q = new Quaternion();
        void SetHeadPose( avs.HeadPose newHeadPose)
        {
            if (!head)
            {
                Teleport_Head[] heads = GetComponentsInChildren<Teleport_Head>();
                if (heads.Length != 1)
                {
                    Debug.LogError("Precisely ONE Teleport_Head should be found.");
                    return;
                }
                head = heads[0];
            }
            q.Set(-newHeadPose.orientation.x
                ,-newHeadPose.orientation.z,-newHeadPose.orientation.y, newHeadPose.orientation.w);
            head.transform.rotation = q;
        }
        Teleport_Head head = null;
        public static void StaticProcessInput(uid ClientID, in avs.InputState newInput)
        {
        }
        #endregion
        Teleport_SessionComponent()
        {
        }
        ~Teleport_SessionComponent()
        {
            if (sessions.ContainsKey(clientID))
            {
                sessions.Remove(clientID);
            }
        }
        private static Dictionary<uid, Teleport_SessionComponent> sessions = new Dictionary<uid, Teleport_SessionComponent>();
        private uid clientID = 0;
        private GeometryStreamingService geometryStreamingService = new GeometryStreamingService();

        private List<Collider> streamedObjects = new List<Collider>();

        private void LateUpdate()
        {
            if (clientID == 0)
            {
                Debug.LogWarning("Session component is not connected to any client!");

                clientID = GetUnlinkedClientID();
                if (clientID == 0)
                    return;
                if (sessions.ContainsKey(clientID))
                {
                    Debug.LogError("Session duplicate key!");
                }
                sessions[clientID] = this;
            }
            if (Client_IsConnected(clientID))
            {
                if (!Client_HasOrigin(clientID))
                {
                    Client_SetOrigin(clientID, transform.position);
                }
            }
            return;
            int layerMask = LayerMask.NameToLayer(CasterMonitor.GEOMETRY_LAYER_NAME);

            if (layerMask != -1)
            {
                layerMask = 1 << 8;
                List<Collider> innerSphereCollisions = new List<Collider>(Physics.OverlapSphere(transform.position, CasterMonitor.GetCasterSettings().detectionSphereRadius, layerMask));
                List<Collider> outerSphereCollisions = new List<Collider>(Physics.OverlapSphere(transform.position, CasterMonitor.GetCasterSettings().detectionSphereRadius + CasterMonitor.GetCasterSettings().detectionSphereBufferDistance, layerMask));

                List<Collider> gainedColliders = new List<Collider>(innerSphereCollisions.Except(streamedObjects));
                List<Collider> lostColliders = new List<Collider>(streamedObjects.Except(outerSphereCollisions));

                foreach (Collider collider in gainedColliders)
                {
                    uid actorID = geometryStreamingService.AddActor(clientID, collider.gameObject);

                    if (actorID != 0)
                    {
                        streamedObjects.Add(collider);
                        ActorEnteredBounds(clientID, actorID);
                    }
                    else
                    {
                        Debug.LogWarning("Failed to add actor to stream: " + collider.gameObject.name);
                    }
                }

                foreach (Collider collider in lostColliders)
                {
                    streamedObjects.Remove(collider);

                    uid actorID = geometryStreamingService.RemoveActor(clientID, collider.gameObject);
                    if (actorID != 0)
                    {
                        ActorLeftBounds(clientID, actorID);
                    }
                    else
                    {
                        Debug.LogWarning("Attempted to remove actor that was not being streamed: " + collider.gameObject.name);
                    }
                }
            }
            else
            {
                Debug.LogError("\"" + CasterMonitor.GEOMETRY_LAYER_NAME + "\" physics layer is not defined! Please create this layer mask, then assign it to the geometry you want to be streamed.");
            }
        }
    }

}