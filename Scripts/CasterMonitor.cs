using System;
using System.Runtime.InteropServices;
using UnityEngine;
using uid = System.UInt64;

namespace teleport
{
    public class CasterMonitor : MonoBehaviour
    {
        public static readonly string GEOMETRY_LAYER_NAME = "Caster Geometry";

        //Unity can't serialise static variables, and seeing as this will be a global single-instance,
        //I would rather have static access than lots of references to the CasterMonitor.
        private static SCServer.CasterSettings _casterSettings;
        public SCServer.CasterSettings casterSettings = new SCServer.CasterSettings();

        private static GeometrySource geometrySource = new GeometrySource();

        public CasterContext casterContext;

        public int listenPort = 10500;
        public int discoveryPort = 10607;
        public int connectionTimeout = 5; //How many seconds to wait before automatically disconnecting from the client.

        #region DLLDelegates
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void OnShowActor(IntPtr actorPtr);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void OnHideActor(IntPtr actorPtr);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void OnSetHeadPose( uid clientID, in avs.HeadPose newHeadPose);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void OnNewInput( uid clientID, in avs.InputState newInput);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void OnDisconnect();
        #endregion

        #region DLLImport
        [DllImport("SimulCasterServer")]
        private static extern void Initialise(OnShowActor showActor, OnHideActor hideActor,
            OnSetHeadPose headPoseSetter, OnNewInput newInputProcessing, OnDisconnect disconnect);
        [DllImport("SimulCasterServer")]
        private static extern void UpdateCasterSettings(SCServer.CasterSettings newSettings);

        [DllImport("SimulCasterServer")]
        private static extern void SetClientPosition(uid clientID, Vector3 pos);
        [DllImport("SimulCasterServer")]
        private static extern void Tick(float deltaTime);
        [DllImport("SimulCasterServer")]
        private static extern void Shutdown();
        #endregion

        public static SCServer.CasterSettings GetCasterSettings()
        {
            return _casterSettings;
        }

        public static GeometrySource GetGeometrySource()
        {
            return geometrySource;
        }

        private void OnEnable()
        {
            _casterSettings = casterSettings;

            Initialise(ShowActor, HideActor, Teleport_SessionComponent.StaticSetHeadPose, Teleport_SessionComponent.StaticProcessInput, Disconnect);
        }

        private void Start()
        {
            ///TODO: Implement geometrySource.AddMesh
            //GameObject[] objects = FindObjectsOfType<GameObject>();
            //foreach(GameObject actor in objects)
            //{
            //    if(actor.layer == LayerMask.NameToLayer(GEOMETRY_LAYER_NAME))
            //    {
            //        geometrySource.AddNode(actor);
            //    }
            //}
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnValidate()
        {
            UpdateCasterSettings(casterSettings);
        }

        private void OnDisable()
        {
            Shutdown();
        }

        private void ShowActor(IntPtr actorPtr)
        {
            GameObject actor = Marshal.PtrToStructure<GameObject>(actorPtr);

            Renderer actorRenderer = actor.GetComponent<Renderer>();
            actorRenderer.enabled = true;
        }

        private void HideActor(IntPtr actorPtr)
        {
            GameObject actor = Marshal.PtrToStructure<GameObject>(actorPtr);

            Renderer actorRenderer = actor.GetComponent<Renderer>();
            actorRenderer.enabled = false;
        }

        private void Disconnect()
        {
            Debug.LogWarning("Disconnect() not implemented.");
        }
    }

}