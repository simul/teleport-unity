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
        public static extern void StopSession(uid clientID);
        
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

        #region StaticCallbacks
        private static Quaternion latestRotation = new Quaternion();
        private static Vector3 latestPosition = new Vector3();

        public static bool StaticDoesSessionExist(uid clientID)
        {
            if(!sessions.ContainsKey(clientID))
            {
                Debug.LogError("No session component found for client with ID: " + clientID);
                return false;
            }

            return true;
        }

        public static void StaticDisconnect(uid clientID)
        {
            sessions[clientID].Disconnect();
        }

        public static void StaticSetHeadPose(uid clientID, in avs.HeadPose newHeadPose)
        {
            if(!StaticDoesSessionExist(clientID)) return;

            latestRotation.Set(newHeadPose.orientation.x, newHeadPose.orientation.y, newHeadPose.orientation.z, newHeadPose.orientation.w);
            latestPosition.Set(newHeadPose.position.x, newHeadPose.position.y, newHeadPose.position.z);
            sessions[clientID].SetHeadPose(latestRotation, latestPosition);
        }

        public static void StaticSetControllerPose(uid clientID, int index, in avs.HeadPose newPose)
        {
            if(!StaticDoesSessionExist(clientID)) return;

            latestRotation.Set(newPose.orientation.x, newPose.orientation.y, newPose.orientation.z, newPose.orientation.w);
            latestPosition.Set(newPose.position.x, newPose.position.y, newPose.position.z);
            sessions[clientID].SetControllerPose(index, latestRotation, latestPosition);
        }

        public static void StaticProcessInput(uid clientID, in avs.InputState newInput)
        {
        }
        #endregion

        private static Dictionary<uid, Teleport_SessionComponent> sessions = new Dictionary<uid, Teleport_SessionComponent>();

        private CasterMonitor casterMonitor; //Cached reference to the caster monitor.

        private uid clientID = 0;

        private Teleport_Head head = null;
        private Dictionary<int, Teleport_Controller> controllers = new Dictionary<int, Teleport_Controller>();

        private GeometryStreamingService geometryStreamingService = new GeometryStreamingService();
        private List<Collider> streamedObjects = new List<Collider>();

        public void Disconnect()
        {
            sessions.Remove(clientID);

            geometryStreamingService.RemoveAllActors(clientID);
            streamedObjects.Clear();

            clientID = 0;
        }

        public void SetHeadPose(Quaternion newRotation, Vector3 newPosition)
        {
            if(!head)
            {
                Teleport_Head[] heads = GetComponentsInChildren<Teleport_Head>();
                if(heads.Length != 1)
                {
                    Debug.LogError("Precisely ONE Teleport_Head should be found.");
                    return;
                }
                head = heads[0];
            }

            head.transform.rotation = newRotation;
            head.transform.localPosition = newPosition;
        }
        public void SetControllerPose(int index, Quaternion newRotation, Vector3 newPosition)
        {
            if(!controllers.ContainsKey(index))
            {
                Teleport_Controller[] controller_components = GetComponentsInChildren<Teleport_Controller>();
                foreach(var c in controller_components)
                {
                    if(c.Index == index)
                        controllers[index] = c;
                }
                if(!controllers.ContainsKey(index))
                    return;
            }
            var controller = controllers[index];
            
            controller.transform.rotation = newRotation;
            controller.transform.localPosition = newPosition;
        }

        private void OnDisable()
        {
            if(sessions.ContainsKey(clientID))
            {
                sessions.Remove(clientID);
            }
        }

        private void Start()
        {
            casterMonitor = CasterMonitor.GetCasterMonitor();
        }

        private void LateUpdate()
        {
            if(clientID == 0)
            {
                clientID = GetUnlinkedClientID();
                if(clientID == 0) return;

                if(sessions.ContainsKey(clientID))
                {
                    Debug.LogError("Session duplicate key!");
                }
                sessions[clientID] = this;
            }

            if(Client_IsConnected(clientID))
            {
                if(!Client_HasOrigin(clientID))
                {
                    Client_SetOrigin(clientID, transform.position);
                }
            }

            if(casterMonitor.casterSettings.isStreamingGeometry)
            {
                UpdateGeometryStreaming();
            }
        }

        private void OnDestroy()
        {
            if(clientID != 0) StopSession(clientID);
        }

        private void UpdateGeometryStreaming()
        {
            int layerMask = casterMonitor.layerMask;

            if(layerMask != 0)
            {
                layerMask = 1 << 8;
                List<Collider> innerSphereCollisions = new List<Collider>(Physics.OverlapSphere(transform.position, casterMonitor.casterSettings.detectionSphereRadius, layerMask));
                List<Collider> outerSphereCollisions = new List<Collider>(Physics.OverlapSphere(transform.position, casterMonitor.casterSettings.detectionSphereRadius + casterMonitor.casterSettings.detectionSphereBufferDistance, layerMask));

                List<Collider> gainedColliders = new List<Collider>(innerSphereCollisions.Except(streamedObjects));
                List<Collider> lostColliders = new List<Collider>(streamedObjects.Except(outerSphereCollisions));

                foreach(Collider collider in gainedColliders)
                {
                    //Skip game objects without the streaming tag.
                    if(collider.tag != casterMonitor.tagToStream) continue;

                    uid actorID = geometryStreamingService.AddActor(clientID, collider.gameObject);

                    if(actorID != 0)
                    {
                        streamedObjects.Add(collider);
                        ActorEnteredBounds(clientID, actorID);
                    }
                    else
                    {
                        Debug.LogWarning("Failed to add game object to stream: " + collider.gameObject.name);
                    }
                }

                foreach(Collider collider in lostColliders)
                {
                    streamedObjects.Remove(collider);

                    uid actorID = geometryStreamingService.RemoveActor(clientID, collider.gameObject);
                    if(actorID != 0)
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
                Debug.LogError("Teleport geometry streaming physics layer is not defined! Please assign layer masks under \"Layers To Stream\".");
            }
        }
    }
}