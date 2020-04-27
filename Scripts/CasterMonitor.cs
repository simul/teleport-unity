using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

using uid = System.UInt64;

namespace teleport
{
    public class CasterMonitor : MonoBehaviour
    {
        public SCServer.CasterSettings casterSettings = new SCServer.CasterSettings();

        public GeometrySource geometrySource;

        [Header("Geometry Selection")]
        public LayerMask layersToStream; //Mask of the physics layers the user can choose to stream.
        public string tagToStream; //Objects with this tag will be streamed; leaving it blank will cause it to just use the layer mask.

        [Header("Connections")]
        public uint listenPort = 10500u;
        public uint discoveryPort = 10607u;
        public int connectionTimeout = 5; //How many seconds to wait before automatically disconnecting from the client.

        private static CasterMonitor instance; //There should only be one CasterMonitor instance at a time.

        //StringBuilders used for constructing log messages from libavstream.
        private static StringBuilder logInfo = new StringBuilder();
        private static StringBuilder logWarning = new StringBuilder();
        private static StringBuilder logError = new StringBuilder();
        private static StringBuilder logCritical = new StringBuilder();

        #region DLLDelegates
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void OnShowActor(in IntPtr actorPtr);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void OnHideActor(in IntPtr actorPtr);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void OnSetHeadPose(uid clientID, in avs.HeadPose newHeadPose);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void OnSetControllerPose(uid clientID, int index, in avs.HeadPose newHeadPose);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void OnNewInput(uid clientID, in avs.InputState newInput);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void OnDisconnect(uid clientID);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void OnMessageHandler(avs.LogSeverity Severity, string Msg, in IntPtr userData);
        #endregion

        #region DLLImport

        struct InitializeState
        {
            public OnShowActor showActor;
            public OnHideActor hideActor;
            public OnSetHeadPose headPoseSetter;
            public OnSetControllerPose controllerPoseSetter;
            public OnNewInput newInputProcessing;
            public OnDisconnect disconnect;
            public OnMessageHandler messageHandler;
            public uint DISCOVERY_PORT;
            public uint SERVICE_PORT;
        };
        [DllImport("SimulCasterServer")]
        private static extern void Initialise(InitializeState initializeState);
        [DllImport("SimulCasterServer")]
        private static extern void UpdateCasterSettings(SCServer.CasterSettings newSettings);

        [DllImport("SimulCasterServer")]
        private static extern void SetClientPosition(uid clientID, Vector3 pos);
        [DllImport("SimulCasterServer")]
        private static extern void Tick(float deltaTime);
        [DllImport("SimulCasterServer")]
        private static extern void Shutdown();
        #endregion

        public static CasterMonitor GetCasterMonitor()
        {
            if(Application.isPlaying) return instance;
            else return FindObjectOfType<CasterMonitor>();
        }

#if UNITY_EDITOR
        //We can only use the Unity AssetDatabase while in the editor.
        private void Awake()
        {
            //If the geometry source is not assigned; find an existing one, or create one.
            if(geometrySource == null)
            {
                Debug.LogWarning("<b>" + name + "</b>'s Geometry Source is not set. Please assign in Editor, or your application may crash in standalone.");

                geometrySource = GeometrySource.GetGeometrySource();
            }
        }
#endif

        private void OnEnable()
        {
            //We only want one instance, so delete duplicates.
            if(instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;
            InitializeState initializeState=new InitializeState();
            initializeState.showActor = CasterMonitor.ShowActor;
            initializeState.hideActor = HideActor;
            initializeState.headPoseSetter = Teleport_SessionComponent.StaticSetHeadPose;
            initializeState.controllerPoseSetter = Teleport_SessionComponent.StaticSetControllerPose;
            initializeState.newInputProcessing = Teleport_SessionComponent.StaticProcessInput;
            initializeState.disconnect = Teleport_SessionComponent.StaticDisconnect;
            initializeState.messageHandler=LogMessageHandler;
            initializeState.SERVICE_PORT = listenPort;
            initializeState.DISCOVERY_PORT = discoveryPort;
            Initialise(initializeState);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnValidate()
        {
            if(Application.isPlaying)
            {
                UpdateCasterSettings(casterSettings);
            }
        }

        private void OnDisable()
        {
            Shutdown();
        }

        private static void ShowActor(in IntPtr actorPtr)
        {
            GameObject actor = (GameObject)GCHandle.FromIntPtr(actorPtr).Target;

            Renderer actorRenderer = actor.GetComponent<Renderer>();
            actorRenderer.enabled = true;
        }

        public static void HideActor(in IntPtr actorPtr)
        {
            GameObject actor = (GameObject)GCHandle.FromIntPtr(actorPtr).Target;

            Renderer actorRenderer = actor.GetComponent<Renderer>();
            actorRenderer.enabled = false;
        }

        private static void LogMessageHandler(avs.LogSeverity Severity, string Msg, in IntPtr userData)
        {
            switch(Severity)
            {
                case avs.LogSeverity.Debug:
                case avs.LogSeverity.Info:
                    logInfo.Append(Msg);

                    if(logInfo[logInfo.Length - 1] == '\n')
                    {
                        Debug.Log(logInfo.ToString());
                        logInfo.Clear();
                    }

                    break;
                case avs.LogSeverity.Warning:
                    logWarning.Append(Msg);

                    if(logWarning[logWarning.Length - 1] == '\n')
                    {
                        Debug.LogWarning(logWarning.ToString());
                        logWarning.Clear();
                    }

                    break;
                case avs.LogSeverity.Error:
                    logError.Append(Msg);

                    if(logError[logError.Length - 1] == '\n')
                    {
                        Debug.LogError(logError.ToString());
                        logError.Clear();
                    }

                    break;
                case avs.LogSeverity.Critical:
                    logCritical.Append(Msg);

                    if(logCritical[logCritical.Length - 1] == '\n')
                    {
                        Debug.LogAssertion(logCritical.ToString());
                        logCritical.Clear();
                    }

                    break;
            }
        }
    }
}