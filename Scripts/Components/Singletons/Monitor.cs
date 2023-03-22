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
	[HelpURL("https://docs.teleportvr.io/unity.html")]
#endif
	/// <summary>
	/// A singleton component which stores per-server-session state.
	/// </summary>
	[ExecuteInEditMode]
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
		delegate bool OnClientStoppedRenderingNode(uid clientID, uid nodeID);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate bool OnClientStartedRenderingNode(uid clientID, uid nodeID);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnSetHeadPose(uid clientID, in avs.Pose newHeadPose);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnSetControllerPose(uid clientID, uid index, in avs.PoseDynamic newHeadPose);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnNewInput(uid clientID, in avs.InputState inputState, in IntPtr binaryStatesPtr, in IntPtr analogueStateasPtr, in IntPtr binaryEventsPtr, in IntPtr analogueEventsPtr, in IntPtr motionEventsPtr);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnDisconnect(uid clientID);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public delegate void OnMessageHandler(avs.LogSeverity Severity, string Msg, in IntPtr userData); 

		 [UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void ReportHandshakeFn(uid clientID, in avs.Handshake handshake);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnAudioInputReceived(uid clientID, in IntPtr dataPtr, UInt64 dataSize);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate Int64 GetUnixTimestampFn();

		#endregion
		public static OnMessageHandler editorMessageHandler=null;
		#region DLLImport

		struct InitialiseState
		{
			public string clientIP;
			public string httpMountDirectory;
			public string certPath;
			public string privateKeyPath;
			public uint DISCOVERY_PORT;
			public uint SERVICE_PORT;

			public OnClientStoppedRenderingNode clientStoppedRenderingNode;
			public OnClientStartedRenderingNode clientStartedRenderingNode;
			public OnSetHeadPose headPoseSetter;
			public OnSetControllerPose controllerPoseSetter;
			public OnNewInput newInputProcessing;
			public OnDisconnect disconnect;
			public OnMessageHandler messageHandler;
			public ReportHandshakeFn reportHandshake;
			public OnAudioInputReceived audioInputReceived;
			public GetUnixTimestampFn getUnixTimestamp;
		};

		[DllImport(TeleportServerDll.name)]
		static extern void SetMessageHandlerDelegate(OnMessageHandler m);
		
		[DllImport(TeleportServerDll.name)]
		public static extern UInt64 SizeOf(string name);
		[DllImport(TeleportServerDll.name)]
		private static extern bool Teleport_Initialize(InitialiseState initialiseState);
		[DllImport(TeleportServerDll.name)]
		private static extern void SetConnectionTimeout(Int32 timeout);
		[DllImport(TeleportServerDll.name)]
		private static extern void UpdateServerSettings(teleport.ServerSettings newSettings);
		
		[DllImport(TeleportServerDll.name)]
		private static extern void SetClientPosition(uid clientID, Vector3 pos);
		[DllImport(TeleportServerDll.name)]
		private static extern void Tick(float deltaTime);
		[DllImport(TeleportServerDll.name)]
		private static extern void EditorTick();
		[DllImport(TeleportServerDll.name)]
		private static extern void Shutdown();
		[DllImport(TeleportServerDll.name)]
		private static extern uid GetUnlinkedClientID();

		// Really basic "send it again" function. Sends to all relevant clients. Must improve!
		[DllImport(TeleportServerDll.name)]
		private static extern void ResendNode(uid id);
		#endregion


		[Header("Background")]
		public BackgroundMode backgroundMode = BackgroundMode.COLOUR;
		[MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.R4)]
		public Color BackgroundColour;

		public void onSpecularRenderTextureChange()
        {
			
        }
		[Tooltip("Choose a cubemap rendertarget. This will be generated from the 'Environment Cubemap' and used for lighting dynamic objects.")]
		//! This will point to a saved asset texture.
		public RenderTexture specularRenderTexture;
		[Tooltip("Choose a cubemap rendertarget. This will be generated from the 'Environment Cubemap' and used for lighting dynamic objects.")]
		//! This will point to a saved asset texture.
		public RenderTexture diffuseRenderTexture;
#if UNITY_EDITOR
		//! For generating static cubemaps in the Editor.
		RenderTexture dummyRenderTexture;
		[HideInInspector]
		public Camera dummyCam = null;
		public int envMapSize=64;
		[HideInInspector]
		public bool generateEnvMaps=false;
#endif
		//! Create a new session, e.g. when a client connects.
		public delegate Teleport_SessionComponent CreateSession();

		public CreateSession createSessionCallback = DefaultCreateSession;

		[Tooltip("Choose the prefab to be used when a player connects, to represent that player's position and shape.")]
		public GameObject defaultPlayerPrefab;

		private GUIStyle overlayFont = new GUIStyle();
		private GUIStyle clientFont = new GUIStyle();

		private string title = "Teleport";

		[HideInInspector]
		public Teleport_AudioCaptureComponent audioCapture = null;

		public Cubemap environmentCubemap=null;
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

		public static Int64 GetUnixTimestampNow()
		{
			return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		}

		///MONOBEHAVIOUR FUNCTIONS

		private void Awake()
		{
			var g = GeometrySource.GetGeometrySource();
			if (g == null)
				return;
#if UNITY_EDITOR
			if (!UnityEditor.EditorApplication.isPlaying)
				return;
#endif
			// Make sure we have a Teleport Render Pipeline, or we won't get a video stream.
			if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null || UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.GetType() != typeof(TeleportRenderPipelineAsset))
			{
#if UNITY_EDITOR
				UnityEditor.EditorUtility.DisplayDialog("Warning", "Current rendering pipeline is not TeleportRenderPipeline.", "OK");
				UnityEditor.EditorApplication.isPlaying = false;
				return;
#else
				title += ": Current rendering pipeline is not TeleportRenderPipeline!";
				Debug.LogError(title);
#endif
			}
			if (g.CheckForErrors() == false)
			{
#if UNITY_EDITOR
				Debug.LogError("GeometrySource.CheckForErrors() failed. Run will not proceed.");
				UnityEditor.EditorUtility.DisplayDialog("Warning", "This scene has errors.", "OK");
				UnityEditor.EditorApplication.isPlaying = false;
				return;
#else
				Debug.LogError("GeometrySource.CheckForErrors() failed. Please check the log.");
#endif
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
			
			//We need to add the animation events on play, so we can detect when an animation starts.
			GeometrySource.GetGeometrySource().AddAnimationEventHooks();
		}

		private void OnEnable()
		{
			ulong unmanagedSize = SizeOf("ServerSettings");
			ulong managedSize = (ulong)Marshal.SizeOf(typeof(teleport.ServerSettings));
		
			if (managedSize != unmanagedSize)
			{
				Debug.LogError($"teleport.Monitor failed to initialise! {nameof(teleport.ServerSettings)} struct size mismatch between unmanaged code({unmanagedSize}) and managed code({managedSize})!\n"
					+$"This usually means that your TeleportServer.dll (or .so) is out of sync with the Unity plugin C# code.\n" +
					$"One or both of these needs to be updated.");
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
			if (!Application.isPlaying)
				return;

			TeleportSettings teleportSettings = TeleportSettings.GetOrCreateSettings();
			UpdateServerSettings(teleportSettings.serverSettings);
			InitialiseState initialiseState = new InitialiseState
			{
				clientStoppedRenderingNode = ClientStoppedRenderingNode,
				clientStartedRenderingNode = ClientStartedRenderingNode,
				headPoseSetter = Teleport_SessionComponent.StaticSetHeadPose,
				controllerPoseSetter = Teleport_SessionComponent.StaticSetControllerPose,
				newInputProcessing = Teleport_SessionComponent.StaticProcessInput,
				disconnect = Teleport_SessionComponent.StaticDisconnect,
				messageHandler = teleportSettings.serverSettings.pipeDllOutputToUnity ? LogMessageHandler : (OnMessageHandler)null,
				SERVICE_PORT = teleportSettings.listenPort,
				DISCOVERY_PORT = teleportSettings.discoveryPort,
				reportHandshake = ReportHandshake,
				audioInputReceived = Teleport_SessionComponent.StaticProcessAudioInput,
				getUnixTimestamp = GetUnixTimestampNow,
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

			TeleportSettings settings = TeleportSettings.GetOrCreateSettings();
			// Create audio component
			var audioCaptures = FindObjectsOfType<Teleport_AudioCaptureComponent>();
			if (audioCaptures.Length > 0)
			{
				audioCapture = audioCaptures[0];
			}
			else
			{
				GameObject go = new GameObject("TeleportAudioCapture");
				go.AddComponent<Teleport_AudioCaptureComponent>();
				audioCapture = go.GetComponent<Teleport_AudioCaptureComponent>();
			}

			if(!settings.serverSettings.isStreamingAudio)
			{
				audioCapture.gameObject.SetActive(false);
				// Setting active to false on game obect does not disable audio listener or capture component
				// so they must be disabled directly.
				audioCapture.enabled = false;
				audioCapture.GetComponent<AudioListener>().enabled = false;
			}
		}

		private void OnDisable()
		{
			SetMessageHandlerDelegate(null);
#if UNITY_EDITOR
			if (dummyCam)
			{
				//DestroyImmediate(dummyCam);
			}
#endif
			SceneManager.sceneLoaded -= OnSceneLoaded;
			Shutdown();
		}
		
		static public void OverrideRenderingLayerMask(GameObject gameObject, uint mask,bool recursive=false)
		{
			Renderer[] renderers;
			if(recursive)
				renderers= gameObject.GetComponentsInChildren<Renderer>(true);
			else
				renderers = gameObject.GetComponents<Renderer>();
			foreach (Renderer renderer in renderers)
			{
				renderer.renderingLayerMask= mask;
			}
		}

		static public void SetRenderingLayerMask(GameObject gameObject, uint mask, bool recursive = false)
		{
			Renderer[] renderers;
			if (recursive)
				renderers = gameObject.GetComponentsInChildren<Renderer>(true);
			else
				renderers = gameObject.GetComponents<Renderer>();
			foreach (Renderer renderer in renderers)
			{
				renderer.renderingLayerMask |= mask;
			}
		}

		static public void UnsetRenderingLayerMask(GameObject gameObject, uint mask, bool recursive = false)
		{
			uint inverse_mask=~mask;
			Renderer[] renderers;
			if (recursive)
				renderers = gameObject.GetComponentsInChildren<Renderer>(true);
			else
				renderers = gameObject.GetComponents<Renderer>();
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
				SetRenderingLayerMask(gameObject, invStreamedMask);
			}

			//Add the Teleport_Streamable component to all root streamable objects.
			// Don't add to objects that have a streamable parent!
			List<GameObject> teleportStreamableObjects = GeometrySource.GetGeometrySource().GetStreamableObjects();
			foreach(GameObject gameObject in teleportStreamableObjects)
			{
				//Objects with collision will have a Teleport_Streamable component added, as they can be streamed as root objects.
				if(gameObject.GetComponent<Collider>() != null && gameObject.GetComponentInParent<Teleport_Streamable>() == null)
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
			if(initialised&&Application.isPlaying)
			{
				Tick(Time.deltaTime);
				CheckForClients();
			}
#if UNITY_EDITOR
			if (generateEnvMaps)
			{
				GenerateEnvMaps();
				generateEnvMaps=false;
				dummyCam.enabled = true;
			}
#endif
		}
#if UNITY_EDITOR
		private void GenerateEnvMaps()
		{
			// we will render this source cubemap into a target that has mips for roughness, and also into a diffuse cubemap.
			// We will save those two cubemaps to disk, and store them as the client dynamic lighting textures.

			if (dummyRenderTexture == null)
			{
				dummyRenderTexture = new RenderTexture(8, 8
					, 24, UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_UNorm, 1);
			}
			if (dummyCam == null)
			{
				GameObject monitorObject = gameObject;
				monitorObject.TryGetComponent<Camera>(out dummyCam);
				if (dummyCam == null)
					dummyCam=monitorObject.AddComponent<Camera>();
				dummyCam.targetTexture= dummyRenderTexture;
				dummyCam.enabled = true;
			}
			int mips = 21;
			while (mips > 1 && ((1 << mips) > envMapSize))
			{
				mips--;
			}
			string scenePath = SceneManager.GetActiveScene().path;
			if (specularRenderTexture)
			{
				string specularRenderTexturePath = UnityEditor.AssetDatabase.GetAssetPath(specularRenderTexture);
				if(specularRenderTexturePath=="")
					specularRenderTexture = null;
			}
			// If specular rendertexture is unassigned or not the same size as the env cubemap, recreate it as a saved asset.
			if (specularRenderTexture == null || specularRenderTexture.width != envMapSize ||
				specularRenderTexture.mipmapCount != mips)
			{
				specularRenderTexture = new RenderTexture(envMapSize, envMapSize
					, 24, UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_UNorm, mips);
				specularRenderTexture.dimension = UnityEngine.Rendering.TextureDimension.Cube;
				specularRenderTexture.useMipMap = true;
				// We will generate the mips with shaders in the render call.
				specularRenderTexture.autoGenerateMips = false;
				string assetPath = scenePath.Replace(".unity", "/specularRenderTexture.renderTexture");
				string assetDirectory = scenePath.Replace(".unity", "");
				string parentDirectory=System.IO.Path.GetDirectoryName(scenePath);
				string subDirectory = System.IO.Path.GetFileNameWithoutExtension(scenePath);
				UnityEditor.AssetDatabase.CreateFolder(parentDirectory,subDirectory);
				UnityEditor.AssetDatabase.CreateAsset(specularRenderTexture, assetPath);
			}
			if (diffuseRenderTexture)
			{
				string diffuseRenderTexturePath = UnityEditor.AssetDatabase.GetAssetPath(diffuseRenderTexture);
				if (diffuseRenderTexturePath == "")
					diffuseRenderTexture = null;
			}
			// If diffuse rendertexture is unassigned or not the same size as the env cubemap, recreate it as a saved asset.
			if (diffuseRenderTexture == null || diffuseRenderTexture.width != envMapSize ||
				diffuseRenderTexture.mipmapCount != mips)
			{
				diffuseRenderTexture = new RenderTexture(envMapSize, envMapSize
					, 24, UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_UNorm, mips);
				diffuseRenderTexture.dimension = UnityEngine.Rendering.TextureDimension.Cube;
				diffuseRenderTexture.useMipMap = true;
				// We will generate the mips with shaders in the render call.
				diffuseRenderTexture.autoGenerateMips = true;
				string assetPath = scenePath.Replace(".unity", "/diffuseRenderTexture.renderTexture");
				UnityEditor.AssetDatabase.CreateAsset(diffuseRenderTexture, assetPath);
			}
			dummyCam.Render();
		}
#endif
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
				UpdateServerSettings(teleportSettings.serverSettings);
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
				Debug.LogError($"Error setting up SessionComponent for Client {id}. There is already a registered session for that client!");
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
			// We want to use an existing session in the scene if it doesn't have a client.
			// This is useful if the session is placed in the scene instead of spawned.
			var currentSessions = FindObjectsOfType<Teleport_SessionComponent>();
			foreach(var s in currentSessions)
			{
				if (!s.Spawned && s.GetClientID() == 0)
				{
					AddMainCamToSession(s);
					return s;
				}
			}

			Teleport_SessionComponent session = null;
			if (Instance.defaultPlayerPrefab == null)
			{
				Instance.defaultPlayerPrefab = Resources.Load("Prefabs/TeleportVR") as GameObject;
			}
			if (Instance.defaultPlayerPrefab != null)
			{
				var childComponents = Instance.defaultPlayerPrefab.GetComponentsInChildren<Teleport_SessionComponent>();

				if (childComponents.Length != 1)
				{
					Debug.LogError($"Exactly <b>one</b> {typeof(Teleport_SessionComponent).Name} child should exist, but <color=red><b>{childComponents.Length}</b></color> were found for \"{Instance.defaultPlayerPrefab}\"!");
					return null;
				}

				if (childComponents.Length == 0)
				{
					return null;
				}
				Vector3 SpawnPosition = new Vector3(0, 0, 0);
				Quaternion SpawnRotation=Quaternion.identity;
				var spawner=Instance.gameObject.GetComponent<teleport.Spawner>();
				if(spawner)
                {
					// If the spawner fails, we can't initialize a session.
					if(!spawner.Spawn(out SpawnPosition, out SpawnRotation))
					{
						Debug.LogError($"spawner.Spawn failed.");
						return null;
					}
                }
				GameObject player = Instantiate(Instance.defaultPlayerPrefab, SpawnPosition, SpawnRotation);
				player.name = "TeleportVR_" +Instance.defaultPlayerPrefab.name+"_"+ Teleport_SessionComponent.sessions.Count + 1;

				session = player.GetComponentsInChildren<Teleport_SessionComponent>()[0];
				session.Spawned = true;

				AddMainCamToSession(session);
			}

			return session;
		}

		static void AddMainCamToSession(Teleport_SessionComponent session)
		{
			if (session.head != null && Camera.main != null && Teleport_SessionComponent.sessions.Count == 0)
			{
				Camera.main.transform.parent = session.head.transform;
				Camera.main.transform.localRotation = Quaternion.identity;
				Camera.main.transform.localPosition = Vector3.zero;
			}
		}

		///DLL CALLBACK FUNCTIONS
		[return: MarshalAs(UnmanagedType.U1)]
		private static bool ClientStoppedRenderingNode(uid clientID, uid nodeID)
		{
			UnityEngine.Object obj = GeometrySource.GetGeometrySource().FindResource(nodeID);
			if(!obj)
			{
				// Possibly node was already deleted.
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
		private static bool ClientStartedRenderingNode(uid clientID, uid nodeID)
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
			if(editorMessageHandler!=null)
				editorMessageHandler(Severity,Msg,userData);
#if UNITY_EDITOR
#else
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
#endif
		}
		public void ReparentNode(GameObject child, GameObject newParent, Vector3 relativePos, Quaternion relativeRot,bool keepWorldPos)
		{
			GameObject oldParent = child.transform.parent != null ? child.transform.parent.gameObject : null;
			if (newParent != null)
				child.transform.SetParent(newParent.transform, keepWorldPos);
			else
				child.transform.SetParent(null, keepWorldPos);
			if(!keepWorldPos)
			{
				child.transform.localPosition = relativePos;
				child.transform.localRotation= relativeRot;
			}
			Teleport_Streamable teleport_Streamable = child.GetComponent<Teleport_Streamable>();
			// Is the new parent owned by a client? If so inform clients of this change:
			Teleport_SessionComponent oldSession=null;
			Teleport_SessionComponent newSession = null;
			Teleport_Streamable newParentStreamable=null;
			Teleport_Streamable oldParentStreamable=null;
			if (newParent != null)
			{
				// Is the new parent owned by a client? If so inform clients of this change:
				newParentStreamable = newParent.GetComponentInParent<Teleport_Streamable>();
				if (newParentStreamable != null && newParentStreamable.OwnerClient != 0)
				{
					newSession = Teleport_SessionComponent.GetSessionComponent(newParentStreamable.OwnerClient);
				}
			}
			if (newSession)
			{
				teleport_Streamable.OwnerClient = newSession.GetClientID();
			}
			else
				teleport_Streamable.OwnerClient = 0;
			if (oldParent != null)
			{
				oldParentStreamable = oldParent.GetComponentInParent<Teleport_Streamable>();
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
			teleport_Streamable.stageSpaceVelocity=new Vector3(0,0,0);
			teleport_Streamable.stageSpaceAngularVelocity = new Vector3(0, 0, 0);
		}

		public void ComponentChanged(MonoBehaviour component)
		{
			if (!Application.isPlaying)
				return;
			Teleport_Streamable streamable=component.gameObject.GetComponentInParent<Teleport_Streamable>();
			if(streamable)
			{
				uid u=streamable.GetUid();
				if(u!=0)
					ResendNode(u);
			}
		}
	}
}