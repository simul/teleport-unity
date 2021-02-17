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
		[SerializeField]
		private GameObject body = default, leftHand = default, rightHand = default;

		// Reference to the global (per-project) TeleportSettings asset.
		private TeleportSettings teleportSettings = null;

		private GeometrySource geometrySource = null;

		private static bool ok = false;
		private static CasterMonitor instance; //There should only be one CasterMonitor instance at a time.

		//StringBuilders used for constructing log messages from libavstream.
		private static StringBuilder logInfo = new StringBuilder();
		private static StringBuilder logWarning = new StringBuilder();
		private static StringBuilder logError = new StringBuilder();
		private static StringBuilder logCritical = new StringBuilder();

		#region DLLDelegates
		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate byte OnShowNode(uid clientID, uid nodeID);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate byte OnHideNode(uid clientID, uid nodeID);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnSetHeadPose(uid clientID, in avs.Pose newHeadPose);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnSetOriginFromClient(uid clientID, UInt64 validCounter, in avs.Pose newHeadPose);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnSetControllerPose(uid clientID, int index, in avs.Pose newHeadPose);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnNewInput(uid clientID, in avs.InputState newInput, in IntPtr newInputEvents);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnDisconnect(uid clientID);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnMessageHandler(avs.LogSeverity Severity, string Msg, in IntPtr userData);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void ReportHandshakeFn(uid clientID, in avs.Handshake handshake);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnAudioInputReceived(uid clientID, in IntPtr dataPtr, UInt64 dataSize);

		#endregion

		#region DLLImport

		struct InitialiseState
		{
			public OnShowNode showNode;
			public OnHideNode hideNode;
			public OnSetHeadPose headPoseSetter;
			public OnSetOriginFromClient setOriginFromCLientFn;
			public OnSetControllerPose controllerPoseSetter;
			public OnNewInput newInputProcessing;
			public OnDisconnect disconnect;
			public OnMessageHandler messageHandler;
			public uint DISCOVERY_PORT;
			public uint SERVICE_PORT;
			public ReportHandshakeFn reportHandshake;
			public OnAudioInputReceived audioInputReceived;
		};
		[DllImport("SimulCasterServer")]
		public static extern UInt64 SizeOf(string name);
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

		public List<GameObject> GetPlayerBodyParts()
		{
			List<GameObject> bodyParts = new List<GameObject>();

			if(body)
			{
				bodyParts.Add(body);
			}

			if(leftHand)
			{
				bodyParts.Add(leftHand);
			}

			if(rightHand)
			{
				bodyParts.Add(rightHand);
			}

			return bodyParts;
		}

		public avs.NodeDataSubtype GetGameObjectBodyPart(GameObject gameObject)
		{
			if(gameObject == body)
			{
				return avs.NodeDataSubtype.Body;
			}
			else if(gameObject == leftHand)
			{
				return avs.NodeDataSubtype.LeftHand;
			}
			else if(gameObject == rightHand)
			{
				return avs.NodeDataSubtype.RightHand;
			}

			return avs.NodeDataSubtype.None;
		}

		///MONOBEHAVIOUR FUNCTIONS

		private void Awake()
		{
			teleportSettings=TeleportSettings.GetOrCreateSettings();
			//If the geometry source is not assigned; find an existing one, or create one.
			if (geometrySource == null)
			{
				geometrySource = GeometrySource.GetGeometrySource();
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

			//Add Teleport_Streamable component to all player body parts.
			List<GameObject> playerBodyParts = GetPlayerBodyParts();
			foreach(GameObject gameObject in playerBodyParts)
			{
				if(gameObject.GetComponent<Teleport_Streamable>() == null)
				{
					gameObject.AddComponent<Teleport_Streamable>();
				}
			}
		}

		private void OnEnable()
		{
			ulong dllSize=SizeOf("CasterSettings");
			ulong exeSize=(ulong)System.Runtime.InteropServices.Marshal.SizeOf(typeof(SCServer.CasterSettings));
			if(exeSize!=dllSize)
			{
				Debug.LogError("Struct size mismatch in dll vs C#.");
				return;
			}
			InitialiseState initialiseState = new InitialiseState();
			initialiseState.showNode = ShowNode;
			initialiseState.hideNode = HideNode;
			initialiseState.headPoseSetter = Teleport_SessionComponent.StaticSetHeadPose;
			initialiseState.setOriginFromCLientFn = Teleport_SessionComponent.StaticSetOriginFromClient;
			initialiseState.controllerPoseSetter = Teleport_SessionComponent.StaticSetControllerPose;
			initialiseState.newInputProcessing = Teleport_SessionComponent.StaticProcessInput;
			initialiseState.disconnect = Teleport_SessionComponent.StaticDisconnect;

			if(teleportSettings.casterSettings.pipeDllOutputToUnity)
				initialiseState.messageHandler = LogMessageHandler;
			else
				initialiseState.messageHandler = (OnMessageHandler)null; 
			initialiseState.SERVICE_PORT = teleportSettings.listenPort;
			initialiseState.DISCOVERY_PORT = teleportSettings.discoveryPort;
			initialiseState.reportHandshake = ReportHandshake;
			initialiseState.audioInputReceived = Teleport_SessionComponent.StaticProcessAudioInput;
			ok = Initialise(initialiseState);

			// Sets connection timeouts for peers (milliseconds)
			SetConnectionTimeout(teleportSettings.connectionTimeout);
			SceneManager.sceneLoaded += OnSceneLoaded;
		}

		private void OnDisable()
		{
			SceneManager.sceneLoaded -= OnSceneLoaded;
			Shutdown();
		}

		private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			// clear masks corresponding to streamed objects.
			int clientLayer = 25;
			uint streamedClientMask = (uint)(((int)1) << (clientLayer + 1));
			uint invStreamedMask = ~streamedClientMask;

			GameObject[] rootGameObjects = scene.GetRootGameObjects();
			foreach(GameObject gameObject in rootGameObjects)
			{
				Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>();
				foreach(Renderer renderer in renderers)
				{
					renderer.renderingLayerMask = invStreamedMask;
				}

				SkinnedMeshRenderer[] skinnedMeshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
				foreach(SkinnedMeshRenderer renderer in skinnedMeshRenderers)
				{
					renderer.renderingLayerMask = invStreamedMask;
				}
			}

			//Add the Teleport_Streamable component to all streamable objects.
			List<GameObject> teleportStreamableObjects = geometrySource.GetStreamableObjects();
			foreach(GameObject gameObject in teleportStreamableObjects)
			{
				//Objects with collision will have a Teleport_Streamable component added, as they can be streamed as root objects.
				if(gameObject.GetComponent<Collider>() != null && gameObject.GetComponent<Teleport_Streamable>() == null)
				{
					gameObject.AddComponent<Teleport_Streamable>();
				}
			}

			// Reset the "origin" for all sessions on the assumption we have changed level.
			foreach(Teleport_SessionComponent sessionComponent in Teleport_SessionComponent.sessions.Values)
			{
				sessionComponent.ResetOrigin();
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

		private void OnGUI()
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

		///DLL CALLBACK FUNCTIONS

		private static byte ShowNode(uid clientID, uid nodeID)
		{
			UnityEngine.Object obj = GeometrySource.GetGeometrySource().FindResource(nodeID);
			if(!obj || obj.GetType() != typeof(GameObject))
			{
				return (byte)0;
			}

			GameObject gameObject = (GameObject)obj;
			int clientLayer = 25;
			uint clientMask = (uint)(((int)1) << clientLayer) | (uint)0x7;
			uint streamedClientMask = (uint)(((int)1) << (clientLayer + 1));
			uint invStreamedMask = ~streamedClientMask;
			Renderer nodeRenderer = gameObject.GetComponent<Renderer>();
			if(nodeRenderer)
			{
				nodeRenderer.renderingLayerMask &= invStreamedMask;
				nodeRenderer.renderingLayerMask |= clientMask;
			}
			else
			{
				SkinnedMeshRenderer skinnedMeshRenderer = gameObject.GetComponentInChildren<SkinnedMeshRenderer>();
				skinnedMeshRenderer.renderingLayerMask &= invStreamedMask;
				skinnedMeshRenderer.renderingLayerMask |= clientMask;
			}
			return (byte)1;
		}

		private static byte HideNode(uid clientID, uid nodeID)
		{
			UnityEngine.Object obj = GeometrySource.GetGeometrySource().FindResource(nodeID);
			if (!obj || obj.GetType() != typeof(GameObject))
			{
				return (byte)0;
			}	

			GameObject gameObject = (GameObject)obj;
			int clientLayer = 25;
			// Add the 0x7 because that's used to show canvases, so we must remove it also from the inverse mask.
			// clear clientLayer and set (clientLayer+1)
			uint clientMask = (uint)(((int)1) << clientLayer)|(uint)0x7;
			uint invClientMask = ~clientMask;
			uint streamedClientMask = (uint)(((int)1) << (clientLayer + 1));

			Renderer nodeRenderer = gameObject.GetComponent<Renderer>();
			if (nodeRenderer)
			{
				nodeRenderer.renderingLayerMask &= invClientMask;
				nodeRenderer.renderingLayerMask |= streamedClientMask; 
			}
			else
			{
				SkinnedMeshRenderer skinnedMeshRenderer = gameObject.GetComponentInChildren<SkinnedMeshRenderer>();
				if(skinnedMeshRenderer)
				{
					skinnedMeshRenderer.renderingLayerMask &= invClientMask;
					skinnedMeshRenderer.renderingLayerMask |= streamedClientMask;
				}
			}
			return (byte)1;
		}

		private static void ReportHandshake(uid clientID,in avs.Handshake handshake)
		{
			var session=Teleport_SessionComponent.sessions[clientID];
			if(session!=null)
			{
				session.ReportHandshake(handshake);
			}
		}

		private static void LogMessageHandler(avs.LogSeverity Severity, string Msg, in IntPtr userData)
		{
			if(Msg.Length<1)
				return;
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