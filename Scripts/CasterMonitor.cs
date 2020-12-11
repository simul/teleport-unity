using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using System.Collections.Generic;
using uid = System.UInt64;
using UnityEngine.SceneManagement;

namespace teleport
{
	[ExecuteInEditMode]
	public class CasterMonitor : MonoBehaviour
	{
		public GameObject leftHand = null;
		public GameObject rightHand = null;

		// Reference to the global (per-project) TeleportSettings asset.
		private TeleportSettings teleportSettings= null;

		private GeometrySource geometrySource = null;

		private List<GameObject> streamableObjects = new List<GameObject>();

		private static bool ok = false;
		private static CasterMonitor instance; //There should only be one CasterMonitor instance at a time.

		//StringBuilders used for constructing log messages from libavstream.
		private static StringBuilder logInfo = new StringBuilder();
		private static StringBuilder logWarning = new StringBuilder();
		private static StringBuilder logError = new StringBuilder();
		private static StringBuilder logCritical = new StringBuilder();

		#region DLLDelegates
		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate byte OnShowActor(uid clientID, uid nodeID);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate byte OnHideActor(uid clientID, uid nodeID);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnSetHeadPose(uid clientID, in avs.HeadPose newHeadPose);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnSetControllerPose(uid clientID, int index, in avs.HeadPose newHeadPose);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnNewInput(uid clientID, in avs.InputState newInput, in IntPtr newInputEvents);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnDisconnect(uid clientID);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnMessageHandler(avs.LogSeverity Severity, string Msg, in IntPtr userData);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void ReportHandshakeFn(uid clientID, in avs.Handshake handshake);
#endregion
		
		#region DLLImport

		struct InitialiseState
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
			public ReportHandshakeFn reportHandshake;
		};
		[DllImport("SimulCasterServer")]
		private static extern bool Initialise(InitialiseState initialiseState);
		[DllImport("SimulCasterServer")]
		private static extern void SetConnectionTimeout(Int32 timeout);
		[DllImport("SimulCasterServer")]
		private static extern void UpdateCasterSettings(SCServer.CasterSettings newSettings);

		[DllImport("SimulCasterServer")]
		private static extern void SetClientPosition(uid clientID, Vector3 pos);
		[DllImport("SimulCasterServer")]
		private static extern void Tick(float deltaTime);
		[DllImport("SimulCasterServer")]
		/// Tock executes when not playing e.g in Edit Mode.
		private static extern void Tock();
		[DllImport("SimulCasterServer")]
		private static extern void Shutdown();
		#endregion

		private GUIStyle overlayFont = new GUIStyle();
		private GUIStyle clientFont = new GUIStyle();

		private string title = "Teleport";

		public static CasterMonitor GetCasterMonitor()
		{
			// We only want one instance, so delete duplicates.
			if (instance == null)
			{
				instance = FindObjectOfType<CasterMonitor>();
				if(instance==null)
				{
					var tempObject= new GameObject("Monitor");
					//Add Components
					tempObject.AddComponent<CasterMonitor>();
					instance = tempObject.GetComponent<CasterMonitor>();
				}
			}
			return instance;
		}
		//We can only use the Unity AssetDatabase while in the editor.
		private void Awake()
		{
			teleportSettings=TeleportSettings.GetOrCreateSettings();
			//If the geometry source is not assigned; find an existing one, or create one.
			if (geometrySource == null)
			{
				geometrySource = GeometrySource.GetGeometrySource();
			}

			// Add hands
			if (leftHand)
			{
				geometrySource.AddLeftHandID(leftHand.GetInstanceID());
			}
			if (rightHand)
			{
				geometrySource.AddRightHandID(rightHand.GetInstanceID());
			}

			overlayFont.normal.textColor = Color.yellow;
			overlayFont.fontSize = 14;
			clientFont.fontSize = 14;
			clientFont.normal.textColor = Color.white;

			// Make sure we have a Teleport Render Pipeline, or we won't get a video stream.
			if(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null || UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.GetType() != typeof(TeleportRenderPipelineAsset))
			{
				title += ": Current rendering pipeline is not TeleportRenderPipeline!";
				Debug.LogError(title);
			}

			//Force streamable objects to be updated, if they were changed but extract was not pressed in the Resource Window.
			geometrySource.UpdateStreamableObjects();
		}

		private void OnEnable()
		{
			InitialiseState initialiseState = new InitialiseState();
			initialiseState.showActor = ShowActor;
			initialiseState.hideActor = HideActor;
			initialiseState.headPoseSetter = Teleport_SessionComponent.StaticSetHeadPose;
			initialiseState.controllerPoseSetter = Teleport_SessionComponent.StaticSetControllerPose;
			initialiseState.newInputProcessing = Teleport_SessionComponent.StaticProcessInput;
			initialiseState.disconnect = Teleport_SessionComponent.StaticDisconnect;
			initialiseState.messageHandler = LogMessageHandler; 
			initialiseState.SERVICE_PORT = teleportSettings.listenPort;
			initialiseState.DISCOVERY_PORT = teleportSettings.discoveryPort;
			initialiseState.reportHandshake = ReportHandshake; 
			ok = Initialise(initialiseState);

			// Sets connection timeouts for peers (milliseconds)
			SetConnectionTimeout(teleportSettings.connectionTimeout);
			SceneManager.sceneLoaded += OnSceneLoaded;
			SceneManager.sceneUnloaded += OnSceneUnloaded;
		}

		void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			GameObject[] rootGameObjects = scene.GetRootGameObjects();
			foreach(GameObject gameObject in rootGameObjects)
			{
				Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>();
				foreach(Renderer renderer in renderers)
				{
					renderer.renderingLayerMask = 0xFFFFFFFF;
				}

				SkinnedMeshRenderer[] skinnedMeshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
				foreach(SkinnedMeshRenderer renderer in skinnedMeshRenderers)
				{
					renderer.renderingLayerMask = 0xFFFFFFFF;
				}

				//CanvasRenderer[] canvasRenderers = gameObject.GetComponentsInChildren<CanvasRenderer>();
				//foreach(CanvasRenderer renderer in canvasRenderers)
				//{
				//	renderer.renderingLayerMask = 0xFFFFFFFF;
				//}
			}

			//Add the Teleport_Streamable component to all streamable objects.
			GameObject[] teleportStreamableObjects = geometrySource.GetStreamableObjects();
			foreach(var gameObject in teleportStreamableObjects)
			{
				if(gameObject.GetComponent<Teleport_Streamable>() == null)
				{
					gameObject.AddComponent<Teleport_Streamable>();
				}
			}

			Teleport_Streamable[] streamables = FindObjectsOfType<Teleport_Streamable>();
			foreach(Teleport_Streamable streamable in streamables)
			{
				streamableObjects.Add(streamable.gameObject);
			}

			// Reset the "origin" for all sessions on the assumption we have changed level.
			foreach(Teleport_SessionComponent sessionComponent in Teleport_SessionComponent.sessions.Values)
			{
				sessionComponent.ResetOrigin();
			}
		}

		void OnSceneUnloaded(Scene scene)
		{
			GameObject[] rootGameObjects = scene.GetRootGameObjects();

			foreach(GameObject gameObject in rootGameObjects)
			{
				streamableObjects.Remove(gameObject);

				Teleport_Streamable[] streamableChildren = gameObject.GetComponentsInChildren<Teleport_Streamable>();
				foreach(Teleport_Streamable child in streamableChildren)
				{
					streamableObjects.Remove(child.gameObject);
				}
			}
		}

		private void Update()
		{
			if(ok && Application.isPlaying)
			{
				Tick(Time.deltaTime);
			}
			else
			{
				Tock();
			}
		}
		void OnGUI()
		{
			if ( Application.isPlaying)
			{
				int x = 10;
				int y = 20;
				GUI.Label(new Rect(x, y += 14, 100, 20), title, overlayFont);
		
				GUI.Label(new Rect(x,y+=14, 100, 20), string.Format("Discovering on port {0}", teleportSettings.discoveryPort), overlayFont);
				foreach(var s in Teleport_SessionComponent.sessions)
				{
					s.Value.ShowOverlay(x,y, clientFont);
				}
			}
		}

		private void OnValidate()
		{
			if(Application.isPlaying)
			{
				teleportSettings = TeleportSettings.GetOrCreateSettings();
				UpdateCasterSettings(teleportSettings.casterSettings);
			}
		}

		private void OnDisable()
		{
			SceneManager.sceneLoaded += OnSceneLoaded;
			SceneManager.sceneUnloaded -= OnSceneUnloaded;
			Shutdown();
		}

		//Called from DLL.
		private static byte ShowActor(uid clientID, uid nodeID)
		{
			UnityEngine.Object obj = GeometrySource.GetGeometrySource().FindResource(nodeID);
			if(!obj || obj.GetType() != typeof(GameObject))
			{
				return (byte)0;
			}

			GameObject gameObject = (GameObject)obj;
			int clientLayer = 25;
			uint clientMask = (uint)(((int)1) << clientLayer) | (uint)0x7;
			Renderer actorRenderer = gameObject.GetComponent<Renderer>();
			if (actorRenderer)
			{
				actorRenderer.renderingLayerMask |= clientMask;
			}
			else
			{
				SkinnedMeshRenderer skinnedMeshRenderer = gameObject.GetComponentInChildren<SkinnedMeshRenderer>();
				skinnedMeshRenderer.renderingLayerMask |= clientMask;
			}
			return (byte)1;

		}

		//Called from DLL.
		public static byte HideActor(uid clientID, uid nodeID)
		{
			UnityEngine.Object obj = GeometrySource.GetGeometrySource().FindResource(nodeID);
			if(!obj || obj.GetType() != typeof(GameObject))
			{
				return (byte)0;
			}

			GameObject gameObject = (GameObject)obj;
			int clientLayer = 25;
			// Add the 0x7 because that's used to show canvases, so we must remove it also from the inverse mask.
			uint clientMask = (uint)(((int)1) << clientLayer)|(uint)0x7;
			Renderer actorRenderer = gameObject.GetComponent<Renderer>();
			uint invClientMask = ~clientMask;
			if (actorRenderer)
				actorRenderer.renderingLayerMask &= invClientMask;
			else
			{
				SkinnedMeshRenderer skinnedMeshRenderer = gameObject.GetComponentInChildren<SkinnedMeshRenderer>();
				skinnedMeshRenderer.renderingLayerMask &= invClientMask;
			}
			return (byte)1;
		}

		private static void ReportHandshake(uid clientID, in avs.Handshake handshake)
		{
			var session = Teleport_SessionComponent.sessions[clientID];
			if(session != null)
			{
				session.ReportHandshake(handshake);
			}
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