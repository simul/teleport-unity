using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

using uid = System.UInt64;

namespace teleport
{
    public class CasterMonitor : MonoBehaviour
    {
        public static readonly string GEOMETRY_LAYER_NAME = "Teleport Geometry";

        //Unity can't serialise static variables, and seeing as this will be a global single-instance,
        //I would rather have static access than lots of references to the CasterMonitor.
        private static SCServer.CasterSettings _casterSettings;
        public SCServer.CasterSettings casterSettings = new SCServer.CasterSettings();

        private static GeometrySource geometrySource = new GeometrySource();

        public int listenPort = 10500;
        public int discoveryPort = 10607;
        public int connectionTimeout = 5; //How many seconds to wait before automatically disconnecting from the client.

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
        delegate void OnMessageHandler(avs.LogSeverity Severity, string Msg, in IntPtr userData);
        #endregion

        #region DLLImport
        [DllImport("SimulCasterServer")]
        private static extern void Initialise(OnShowActor showActor, OnHideActor hideActor,
            OnSetHeadPose headPoseSetter, OnSetControllerPose controllerPoseSetter, OnNewInput newInputProcessing, OnMessageHandler messageHandler);
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

            Initialise(ShowActor, HideActor, Teleport_SessionComponent.StaticSetHeadPose, Teleport_SessionComponent.StaticSetControllerPose, Teleport_SessionComponent.StaticProcessInput, LogMessageHandler);
        }

        private void Start()
        {
            GameObject[] objects = FindObjectsOfType<GameObject>();
            foreach(GameObject actor in objects)
            {
                if(actor.layer == LayerMask.NameToLayer(GEOMETRY_LAYER_NAME))
                {
                    geometrySource.AddNode(actor);
                }
            }
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