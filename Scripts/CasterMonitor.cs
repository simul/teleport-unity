using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using System.Collections.Generic;
using uid = System.UInt64;
using UnityEngine.SceneManagement;

namespace teleport
{
#if UNITY_EDITOR
	[UnityEditor.InitializeOnLoad]
#endif
	public class CasterMonitor : MonoBehaviour
	{
		public Vector3 bodyOffsetFromHead = default;

		[SerializeField]
		private GameObject body = default, leftHand = default, rightHand = default;

		private static bool initialised = false;
		private static CasterMonitor instance; //There should only be one CasterMonitor instance at a time.

		//StringBuilders used for constructing log messages from libavstream.
		private static StringBuilder logInfo = new StringBuilder();
		private static StringBuilder logWarning = new StringBuilder();
		private static StringBuilder logError = new StringBuilder();
		private static StringBuilder logCritical = new StringBuilder();

		#region DLLDelegates

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate bool OnShowNode(uid clientID, uid nodeID);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate bool OnHideNode(uid clientID, uid nodeID);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnSetHeadPose(uid clientID, in avs.Pose newHeadPose);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnSetOriginFromClient(uid clientID, UInt64 validCounter, in avs.Pose newHeadPose);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnSetControllerPose(uid clientID, int index, in avs.Pose newHeadPose);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnNewInput(uid clientID, in avs.InputState inputState, in IntPtr binaryEventsPtr, in IntPtr analogueEventsPtr, in IntPtr motionEventsPtr);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnDisconnect(uid clientID);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnMessageHandler(avs.LogSeverity Severity, string Msg, in IntPtr userData);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void ReportHandshakeFn(uid clientID, in avs.Handshake handshake);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnAudioInputReceived(uid clientID, in IntPtr dataPtr, UInt64 dataSize);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate Int64 GetUnixTimestampFn();

		#endregion

		#region DLLImport

		struct InitialiseState
		{
			public string clientIP;
			public uint DISCOVERY_PORT;
			public uint SERVICE_PORT;

			public OnShowNode showNode;
			public OnHideNode hideNode;
			public OnSetHeadPose headPoseSetter;
			public OnSetOriginFromClient setOriginFromCLientFn;
			public OnSetControllerPose controllerPoseSetter;
			public OnNewInput newInputProcessing;
			public OnDisconnect disconnect;
			public OnMessageHandler messageHandler;
			public ReportHandshakeFn reportHandshake;
			public OnAudioInputReceived audioInputReceived;
			public GetUnixTimestampFn getUnixTimestamp;

			public avs.Vector3 bodyOffsetFromHead;
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
		private static extern void EditorTick();
		[DllImport("SimulCasterServer")]
		private static extern void Shutdown();
		#endregion

		private GUIStyle overlayFont = new GUIStyle();
		private GUIStyle clientFont = new GUIStyle();

		private string title = "Teleport";

#if UNITY_EDITOR
		static CasterMonitor()
		{
			UnityEditor.EditorApplication.update += EditorTick;
		}
#endif

		public static CasterMonitor GetCasterMonitor()
		{
			// We only want one instance, so delete duplicates.
			if (instance == null)
			{
				for (int i = 0; i < SceneManager.sceneCount; i++)
				{
					var objs=SceneManager.GetSceneAt(i).GetRootGameObjects();
					foreach(var o in objs)
					{
						var m=o.GetComponentInChildren<CasterMonitor>();
						if(m)
						{
							instance=m;
							return instance;
						}
					}
				}
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

		//Returns list of body part hierarchy root GameObjects. 
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

		//Returns which body part the GameObject is.
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

		public static Int64 GetUnixTimestamp()
		{
			return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		}

		///MONOBEHAVIOUR FUNCTIONS

		private void Awake()
		{
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

			//Add Teleport_Streamable component to all player body parts.
			List<GameObject> playerBodyParts = GetPlayerBodyParts();
			foreach(GameObject bodyPart in playerBodyParts)
			{
				Teleport_Streamable streamableComponent = bodyPart.GetComponent<Teleport_Streamable>();
				if(!streamableComponent)
				{
					streamableComponent = bodyPart.AddComponent<Teleport_Streamable>();
				}

				//We want the client to control the client-side transform of the body parts for reduced latency.
				streamableComponent.sendMovementUpdates = false;
			}
			
			//We need to add the animation events on play, so we can detect when an animation starts.
			GeometrySource.GetGeometrySource().AddAnimationEventHooks();
		}

		private void OnEnable()
		{
			ulong unmanagedSize = SizeOf("CasterSettings");
			ulong managedSize = (ulong)Marshal.SizeOf(typeof(SCServer.CasterSettings));
		
			if (managedSize != unmanagedSize)
			{
				Debug.LogError($"CasterMonitor failed to initialise! {nameof(SCServer.CasterSettings)} struct size mismatch between unmanaged code({unmanagedSize}) and managed code({managedSize})!");
				return;
			}

			SceneManager.sceneLoaded += OnSceneLoaded;

			TeleportSettings teleportSettings = TeleportSettings.GetOrCreateSettings();
			InitialiseState initialiseState = new InitialiseState
			{
				showNode = ShowNode,
				hideNode = HideNode,
				headPoseSetter = Teleport_SessionComponent.StaticSetHeadPose,
				setOriginFromCLientFn = Teleport_SessionComponent.StaticSetOriginFromClient,
				controllerPoseSetter = Teleport_SessionComponent.StaticSetControllerPose,
				newInputProcessing = Teleport_SessionComponent.StaticProcessInput,
				disconnect = Teleport_SessionComponent.StaticDisconnect,
				messageHandler = teleportSettings.casterSettings.pipeDllOutputToUnity ? LogMessageHandler : (OnMessageHandler)null,
				SERVICE_PORT = teleportSettings.listenPort,
				DISCOVERY_PORT = teleportSettings.discoveryPort,
				reportHandshake = ReportHandshake,
				audioInputReceived = Teleport_SessionComponent.StaticProcessAudioInput,
				getUnixTimestamp = GetUnixTimestamp,
				bodyOffsetFromHead = bodyOffsetFromHead,
				clientIP = teleportSettings.clientIP
			};

			initialised = Initialise(initialiseState);

			// Sets connection timeouts for peers (milliseconds)
			SetConnectionTimeout(teleportSettings.connectionTimeout);
			
		}

		private void OnDisable()
		{
			SceneManager.sceneLoaded -= OnSceneLoaded;
			Shutdown();
		}

		static public void SetMaskRecursive(GameObject gameObject,uint mask)
		{
			Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>();
			foreach (Renderer renderer in renderers)
			{
				renderer.renderingLayerMask = mask;
			}
		}

		static public void UnsetMaskRecursive(GameObject gameObject, uint mask)
		{
			uint inverse_mask=~mask;
			Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>();
			foreach (Renderer renderer in renderers)
			{
				renderer.renderingLayerMask = renderer.renderingLayerMask & inverse_mask;
			}
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
				SetMaskRecursive(gameObject, invStreamedMask);
			}

			//Add the Teleport_Streamable component to all streamable objects.
			List<GameObject> teleportStreamableObjects = GeometrySource.GetGeometrySource().GetStreamableObjects();
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
			if(initialised)
			{
				Tick(Time.deltaTime);
			}
		}

		private void OnGUI()
		{
			if ( Application.isPlaying)
			{
				int x = 10;
				int y = 0;
				GUI.Label(new Rect(x, y, 100, 20), title, overlayFont);
		
				GUI.Label(new Rect(x,y+=14, 100, 20), string.Format("Discovering on port {0}", TeleportSettings.GetOrCreateSettings().discoveryPort), overlayFont);
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
				TeleportSettings teleportSettings = TeleportSettings.GetOrCreateSettings();
				UpdateCasterSettings(teleportSettings.casterSettings);
			}
		}

		///DLL CALLBACK FUNCTIONS

		[return: MarshalAs(UnmanagedType.U1)]
		private static bool ShowNode(uid clientID, uid nodeID)
		{
			UnityEngine.Object obj = GeometrySource.GetGeometrySource().FindResource(nodeID);
			if(!obj)
			{
				Debug.LogWarning($"Failed to show node! Could not find a resource with ID {nodeID}!");
				return false;
			}

			if(obj.GetType() != typeof(GameObject))
			{
				Debug.LogWarning($"Failed to show node! Resource found for ID {nodeID} was of type {obj.GetType().Name}, when we require a {nameof(GameObject)}!");
				return false;
			}

			GameObject gameObject = (GameObject)obj;

			if(!gameObject.TryGetComponent(out Teleport_Streamable streamable))
			{
				/*
				Debug.LogWarning($"Failed to show node! \"{gameObject}\" does not have a {nameof(Teleport_Streamable)} component!");
				return false;
				*/

				//We still succeeded in ensuring the GameObject was in the correct state; the hierarchy root will show the node.
				return true;
			}

			streamable.ShowHierarchy();

			return true;
		}

		[return: MarshalAs(UnmanagedType.U1)]
		private static bool HideNode(uid clientID, uid nodeID)
		{
			UnityEngine.Object obj = GeometrySource.GetGeometrySource().FindResource(nodeID);
			if(!obj)
			{
				Debug.LogWarning($"Failed to hide node! Could not find a resource with ID {nodeID}!");
				return false;
			}

			if(obj.GetType() != typeof(GameObject))
			{
				Debug.LogWarning($"Failed to hide node! Resource found for ID {nodeID} was of type {obj.GetType().Name}, when we require a {nameof(GameObject)}!");
				return false;
			}

			GameObject gameObject = (GameObject)obj;

			if(!gameObject.TryGetComponent(out Teleport_Streamable streamable))
			{
				/*
				Debug.LogWarning($"Failed to hide node! \"{gameObject}\" does not have a {nameof(Teleport_Streamable)} component!");
				return false;
				*/

				//We still succeeded in ensuring the GameObject was in the correct state; the hierarchy root will hide the node.
				return true;
			}

			streamable.HideHierarchy();

			return true;
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