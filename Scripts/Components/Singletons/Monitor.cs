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
	public class Monitor : MonoBehaviour
	{
		private static bool initialised = false;
		private static teleport.Monitor instance; //There should only be one teleport.Monitor instance at a time.

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
			public string httpMountDirectory;
			public string certPath;
			public string privateKeyPath;
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
		};

		[DllImport("TeleportServer")]
		public static extern UInt64 SizeOf(string name);
		[DllImport("TeleportServer")]
		private static extern bool Teleport_Initialize(InitialiseState initialiseState);
		[DllImport("TeleportServer")]
		private static extern void SetConnectionTimeout(Int32 timeout);
		[DllImport("TeleportServer")]
		private static extern void UpdateCasterSettings(SCServer.CasterSettings newSettings);
		
		[DllImport("TeleportServer")]
		private static extern void SetClientPosition(uid clientID, Vector3 pos);
		[DllImport("TeleportServer")]
		private static extern void Tick(float deltaTime);
		[DllImport("TeleportServer")]
		private static extern void EditorTick();
		[DllImport("TeleportServer")]
		private static extern void Shutdown();
		[DllImport("TeleportServer")]
		private static extern uid GetUnlinkedClientID();
		#endregion

		public delegate Teleport_SessionComponent CreateSession();

		public CreateSession createSessionCallback = DefaultCreateSession;

		private static GameObject defaultPlayerPrefab = null;

		private GUIStyle overlayFont = new GUIStyle();
		private GUIStyle clientFont = new GUIStyle();

		private string title = "Teleport";

#if UNITY_EDITOR
		static Monitor()
		{
			UnityEditor.EditorApplication.update += EditorTick;
		}
#endif

		public void SetCreateSessionCallback(CreateSession callback)
		{
			createSessionCallback = callback;
		}

		public static Monitor Instance
		{
			get
			{
				// We only want one instance, so delete duplicates.
				if (instance == null)
				{
					for (int i = 0; i < SceneManager.sceneCount; i++)
					{
						var objs=SceneManager.GetSceneAt(i).GetRootGameObjects();
						foreach(var o in objs)
						{
							var m=o.GetComponentInChildren<teleport.Monitor>();
							if(m)
							{
								instance=m;
								return instance;
							}
						}
					}
					instance = FindObjectOfType<teleport.Monitor>();
					if(instance==null)
					{
						var tempObject= new GameObject("Monitor");
						//Add Components
						tempObject.AddComponent<teleport.Monitor>();
						instance = tempObject.GetComponent<teleport.Monitor>();
					}
				}
				return instance;
			}
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
			
			//We need to add the animation events on play, so we can detect when an animation starts.
			GeometrySource.GetGeometrySource().AddAnimationEventHooks();
		}

		private void OnEnable()
		{
			ulong unmanagedSize = SizeOf("CasterSettings");
			ulong managedSize = (ulong)Marshal.SizeOf(typeof(SCServer.CasterSettings));
		
			if (managedSize != unmanagedSize)
			{
				Debug.LogError($"teleport.Monitor failed to initialise! {nameof(SCServer.CasterSettings)} struct size mismatch between unmanaged code({unmanagedSize}) and managed code({managedSize})!");
				return;
			}
			if (instance == null)
				instance = this;
			if (instance != this)
			{
				Debug.LogError($"More than one instance of singleton teleport.Monitor.");
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
				httpMountDirectory = teleportSettings.cachePath,
				clientIP = teleportSettings.clientIP,
				certPath = teleportSettings.certPath,
				privateKeyPath = teleportSettings.privateKeyPath
			};

			initialised = Teleport_Initialize(initialiseState);
			if(!initialised)
			{
				Debug.LogError($"Teleport_Initialize failed, so server cannot start.");
			}
			// Sets connection timeouts for peers (milliseconds)
			SetConnectionTimeout(teleportSettings.connectionTimeout);
		}

		private void OnDisable()
		{
			SceneManager.sceneLoaded -= OnSceneLoaded;
			Shutdown();
		}
		
		static public void OverrideMaskRecursive(GameObject gameObject, uint mask)
		{
			Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>();
			foreach (Renderer renderer in renderers)
			{
				renderer.renderingLayerMask= mask;
			}
		}

		static public void SetMaskRecursive(GameObject gameObject,uint mask)
		{
			Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>();
			foreach (Renderer renderer in renderers)
			{
				renderer.renderingLayerMask |= mask;
			}
		}

		static public void UnsetMaskRecursive(GameObject gameObject, uint mask)
		{
			uint inverse_mask=~mask;
			Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>();
			foreach (Renderer renderer in renderers)
			{
				// Previously we &'d with the existing mask, but that causes bad behaviour if the mask is left in the wrong state and the object is saved.
				renderer.renderingLayerMask &=  inverse_mask;
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
				CheckForClients();
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

		private void CheckForClients()
		{
			uid id = GetUnlinkedClientID();
			if (id == 0)
			{
				return;
			}

			if (Teleport_SessionComponent.sessions.ContainsKey(id))
			{
				Debug.LogError($"Error setting up SessionComponent for Client_{id}. There is already a registered session for that client!");
				return;
			}

			var session = createSessionCallback();
			if (session != null)
			{
				session.StartSession(id);
			}
		}

		public static Teleport_SessionComponent DefaultCreateSession()
		{
			Teleport_SessionComponent session = null;

			// We want to use an existing session in the scene if it doesn't have a client.
			// This is useful if the session is placed in the scene instead of spawned.
			var currentSessions = FindObjectsOfType<Teleport_SessionComponent>();
			foreach(var s in currentSessions)
			{
				if (s.GetClientID() == 0)
				{
					return s;
				}
			}

			if (defaultPlayerPrefab == null)
			{
				defaultPlayerPrefab = Resources.Load("Prefabs/TeleportVR") as GameObject;
			}
			if (defaultPlayerPrefab != null)
			{
				var childComponents = defaultPlayerPrefab.GetComponentsInChildren<Teleport_SessionComponent>();

				if (childComponents.Length != 1)
				{
					Debug.LogError($"Exactly <b>one</b> {typeof(Teleport_SessionComponent).Name} child should exist, but <color=red><b>{childComponents.Length}</b></color> were found for \"{defaultPlayerPrefab}\"!");
					return null;
				}

				if (childComponents.Length == 0)
				{
					return null;
				}

				GameObject player = Instantiate(defaultPlayerPrefab, new Vector3(0, 0, 0), Quaternion.identity);
				player.name = "TeleportVR_" + Teleport_SessionComponent.sessions.Count;

				session = player.GetComponentsInChildren<Teleport_SessionComponent>()[0];

				if (session.head != null && Camera.main != null && Teleport_SessionComponent.sessions.Count == 0)
				{
					Camera.main.transform.parent = session.head.transform;
				}
			}
			return session;
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
				//We still succeeded in ensuring the GameObject was in the correct state; the hierarchy root will show the node.
				/*
				Debug.LogWarning($"Failed to show node! \"{gameObject}\" does not have a {nameof(Teleport_Streamable)} component!");
				return false;
				*/

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
				//We still succeeded in ensuring the GameObject was in the correct state; the hierarchy root will hide the node.
				/*
				Debug.LogWarning($"Failed to hide node! \"{gameObject}\" does not have a {nameof(Teleport_Streamable)} component!");
				return false;
				*/

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
		public void ReparentNode(GameObject child, GameObject newParent, Vector3 relativePos, Quaternion relativeRot)
		{
			GameObject oldParent = child.transform.parent != null ? child.transform.parent.gameObject : null;
			if (newParent != null)
				child.transform.SetParent(newParent.transform, false);
			else
				child.transform.SetParent(null, false);
			child.transform.localPosition = relativePos;
			child.transform.localRotation= relativeRot;
			Teleport_Streamable teleport_Streamable = child.GetComponent<Teleport_Streamable>();
			// Is the new parent owned by a client? If so inform clients of this change:
			Teleport_SessionComponent oldSession=null;
			Teleport_SessionComponent newSession = null;
			Teleport_Streamable newParentStreamable=null;
			Teleport_Streamable oldParentStreamable=null;
			if (newParent != null)
			{
				// Is the new parent owned by a client? If so inform clients of this change:
				newParentStreamable = newParent.GetComponent<Teleport_Streamable>();
				if (newParentStreamable != null && newParentStreamable.OwnerClient != 0)
				{
					newSession = Teleport_SessionComponent.GetSessionComponent(newParentStreamable.OwnerClient);
				}
			}
			if (newSession)
			{
				teleport_Streamable.OwnerClient = newSession.GetClientID();
			}
			if (oldParent != null)
			{
				oldParentStreamable = oldParent.GetComponent<Teleport_Streamable>();
				if (oldParentStreamable != null&&oldParentStreamable.OwnerClient != 0)
				{
					oldSession = Teleport_SessionComponent.GetSessionComponent(oldParentStreamable.OwnerClient);
				}
			}
			if (oldSession)
			{
				oldSession.GeometryStreamingService.ReparentNode(child, newParent, relativePos, relativeRot);
			}
			if (newSession)
			{
				newSession.GeometryStreamingService.ReparentNode(child, newParent, relativePos, relativeRot);
			}
		}
	}
}