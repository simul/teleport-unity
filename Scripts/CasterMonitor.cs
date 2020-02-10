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

        public GeometrySource geometrySource = new GeometrySource();

        [Header("Geometry Selection")]
        [SerializeField]
        private LayerMask[] layersToStream = new LayerMask[0]; //Array of physics layers the user can choose to stream.
        public string tagToStream; //Objects with this tag will be streamed; leaving it blank will cause it to just use the layer mask.

        [Header("Connections")]
        public int listenPort = 10500;
        public int discoveryPort = 10607;
        public int connectionTimeout = 5; //How many seconds to wait before automatically disconnecting from the client.

        [NonSerialized]
        public LayerMask layerMask; //Layer mask generated from layersToStream array to determine streamed objects.

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

        public static CasterMonitor GetCasterMonitor()
        {
            return instance;
        }

        private void OnEnable()
        {
            //We only want one instance, so delete duplicates.
            if(instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;

            Initialise(ShowActor, HideActor, Teleport_SessionComponent.StaticSetHeadPose, Teleport_SessionComponent.StaticSetControllerPose, Teleport_SessionComponent.StaticProcessInput, LogMessageHandler);
        }

        private void Start()
        {
            //Grab every game object if no tag is defined, otherwise grab all of the game objects with the matching tag.
            GameObject[] objects = (tagToStream == "") ? FindObjectsOfType<GameObject>() : GameObject.FindGameObjectsWithTag(tagToStream);

            //Extract data from all game objects/actors that respond to the streaming layer mask.
            foreach(GameObject actor in objects)
            {
                if(layersToStream.Length == 0 || (1 << actor.layer) == layerMask)
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

			//Regenerate layer mask whenever a value changes; you can't serialise properties for the Unity Editor.
            layerMask = 0;
            foreach(LayerMask layer in layersToStream)
            {
                layerMask |= layer;
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