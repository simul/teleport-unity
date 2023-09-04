using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

using uid = System.UInt64;

namespace teleport
{
	// declaring an interface
	public interface SessionSubcomponent
	{
		void OnSessionStart();
	}
	[HelpURL("https://docs.teleportvr.io/unity.html")]
	/// <summary>
	/// This component manages a client session, so there is one Teleport_SessionComponent per session,
	/// and this typically is attached to the root GameObject of each client.
	/// </summary>
	public class Teleport_SessionComponent : MonoBehaviour
	{
		#region DLLImports

		[DllImport(TeleportServerDll.name)]
		private static extern bool Client_IsConnected(uid clientID);
		[DllImport(TeleportServerDll.name)]
		public static extern bool Client_HasHost(uid clientID);
		[DllImport(TeleportServerDll.name)]
		public static extern bool Client_HasPeer(uid clientID);

		[DllImport(TeleportServerDll.name)]
		public static extern void Client_StopSession(uid clientID);
		[DllImport(TeleportServerDll.name)]
		public static extern void Client_StopStreaming(uid clientID);


		[DllImport(TeleportServerDll.name)]
		public static extern bool Client_GetNetworkState(uid clientID, ref avs.ClientNetworkState st);

		[DllImport(TeleportServerDll.name)]
		public static extern uint Client_GetClientIP(uid clientID, uint bufferLength, StringBuilder buffer);

		[DllImport(TeleportServerDll.name)]
		public static extern bool Client_GetClientNetworkStats(uid clientID, ref avs.NetworkStats stats);

		[DllImport(TeleportServerDll.name)]
		public static extern bool Client_GetClientDisplayInfo(uid clientID, ref avs.DisplayInfo displayInfo);
		[DllImport(TeleportServerDll.name)]
		public static extern bool Client_GetClientVideoEncoderStats(uid clientID, ref avs.VideoEncoderStats stats);

		// Set the client-specific settings, e.g. video layout.
		[DllImport(TeleportServerDll.name)]
		private static extern void Client_SetClientSettings(uid clientID, teleport.ClientSettings clientSettings);
		// Set the client-specific lighting, e.g. video or texture lighting.
		[DllImport(TeleportServerDll.name)]
		private static extern void Client_SetClientDynamicLighting(uid clientID, teleport.ClientDynamicLighting clientDynamicLighting);

		[DllImport(TeleportServerDll.name)]
		private static extern void Client_SetClientInputDefinitions(uid clientID, int numControls, string[] controlPaths, avs.InputDefinitionInterop[] inputDefinitions);

		[DllImport(TeleportServerDll.name)]
		private static extern bool Client_SetOrigin(uid clientID, uid originNodeID);
		[DllImport(TeleportServerDll.name)]
		private static extern bool Client_HasOrigin(uid clientID);

		[DllImport(TeleportServerDll.name)]
		private static extern void Client_SetNodeAnimationSpeed(uid clientID, uid nodeID, uid animationID, float speed);

		#endregion

		#region StaticCallbacks

		public static bool HasSessionComponent(uid clientID)
		{
			if (!sessions.ContainsKey(clientID))
			{
				TeleportLog.LogErrorOnce($"No session component found for Client_{clientID}!");
				return false;
			}

			return true;
		}

		public static Teleport_SessionComponent GetSessionComponent(uid clientID)
		{
			if (!sessions.TryGetValue(clientID, out Teleport_SessionComponent sessionComponent))
			{
				return null;
			}

			return sessionComponent;
		}

		public static void StaticDisconnect(uid clientID)
		{
			if (sessions.ContainsKey(clientID))
			{
				var session = sessions[clientID];

				bool nestedMainCam = false;
				// Change main camera to be child of another session.
				if (Camera.main != null)
				{
					var cams = session.GetComponentsInChildren<Camera>();
					foreach (var cam in cams)
					{
						if (Camera.main == cam)
						{
							Camera.main.transform.parent = null;
							nestedMainCam = true;
							break;
						}
					}
				}

				session.Disconnect();
				sessions.Remove(clientID);

				// Don't destory sessions placed in the scene.
				if (session.Spawned)
				{
					Destroy(session.gameObject);
				}

				if (nestedMainCam && sessions.Count > 0)
				{
					// Nest main camera in any other session.
					session = sessions.First().Value;
					if (session._head != null)
					{
						Camera.main.transform.parent = session._head.transform;
					}
				}
			}

			// This MUST be called for connection / reconnection to work properly.
			Client_StopStreaming(clientID);
		}


		public static void StaticSetHeadPose(uid clientID, in avs.Pose newHeadPose)
		{
			Teleport_SessionComponent sessionComponent = GetSessionComponent(clientID);
			if (!sessionComponent)
			{
				return;
			}

			Quaternion rotation = new Quaternion(newHeadPose.orientation.x, newHeadPose.orientation.y, newHeadPose.orientation.z, newHeadPose.orientation.w);
			Vector3 position = new Vector3(newHeadPose.position.x, newHeadPose.position.y, newHeadPose.position.z);
			sessionComponent.SetHeadPose(rotation, position);
		}

		public static void StaticSetControllerPose(uid clientID, uid id, in avs.PoseDynamic newPose)
		{
			Teleport_SessionComponent sessionComponent = GetSessionComponent(clientID);
			if (!sessionComponent)
			{
				return;
			}

			Quaternion latestRotation = new Quaternion(newPose.orientation.x, newPose.orientation.y, newPose.orientation.z, newPose.orientation.w);
			Vector3 latestPosition = new Vector3(newPose.position.x, newPose.position.y, newPose.position.z);
			Vector3 velocity = new Vector3(newPose.velocity.x, newPose.velocity.y, newPose.velocity.z);
			Vector3 angularVelocity= new Vector3(newPose.angularVelocity.x, newPose.angularVelocity.y, newPose.angularVelocity.z);
			sessionComponent.SetControllerPose(id, latestRotation, latestPosition, velocity, angularVelocity);
		}

		public static void StaticProcessInputState(uid clientID, in avs.InputState inputState, in IntPtr binaryStatesPtr, in IntPtr analogueStatesPtr)
		{
			Teleport_SessionComponent sessionComponent = GetSessionComponent(clientID);
			if (!sessionComponent)
			{
				return;
			}

			float[] floatStates=new float[inputState.numAnalogueStates];
			if (inputState.numAnalogueStates != 0)
			{
				int analogueStateSize = Marshal.SizeOf<float>();
				IntPtr positionPtr = analogueStatesPtr;
				for (int i = 0; i < inputState.numAnalogueStates; i++)
				{
					floatStates[i] = Marshal.PtrToStructure<float>(positionPtr);
					positionPtr += analogueStateSize;
				}
			}
			byte[] boolStates = new byte[inputState.numBinaryStates];
			if (inputState.numBinaryStates != 0)
			{
				int binaryStateSize = Marshal.SizeOf<byte>();
				IntPtr positionPtr = binaryStatesPtr;
				for (int i = 0; i < inputState.numBinaryStates; i++)
				{
					boolStates[i] = Marshal.PtrToStructure<byte>(positionPtr);
					positionPtr += binaryStateSize;
				}
			}
			sessionComponent.ProcessInputState(boolStates, floatStates);
		}

		public static void StaticProcessInputEvents(uid clientID, UInt16 numBinaryEvents,UInt16 numAnalogueEvents, UInt16 numMotionEvents
			,  in IntPtr binaryEventsPtr, in IntPtr analogueEventsPtr, in IntPtr motionEventsPtr)
		{
			Teleport_SessionComponent sessionComponent = GetSessionComponent(clientID);
			if (!sessionComponent)
			{
				return;
			}

			avs.InputEventBinary[] binaryEvents = new avs.InputEventBinary[numBinaryEvents];
			if (numBinaryEvents != 0)
			{
				int binaryEventSize = Marshal.SizeOf<avs.InputEventBinary>();

				//Convert the pointer array into a struct array.
				IntPtr positionPtr = binaryEventsPtr;
				for (int i = 0; i < numBinaryEvents; i++)
				{
					binaryEvents[i] = Marshal.PtrToStructure<avs.InputEventBinary>(positionPtr);
					positionPtr += binaryEventSize;
					//Debug.Log("inputState binary event " + binaryEvents[i].inputID + " " + binaryEvents[i].activated);
				}
			}

			avs.InputEventAnalogue[] analogueEvents = new avs.InputEventAnalogue[numAnalogueEvents];
			if (numAnalogueEvents != 0)
			{
				int analogueEventSize = Marshal.SizeOf<avs.InputEventAnalogue>();

				//Convert the pointer array into a struct array.
				IntPtr positionPtr = analogueEventsPtr;
				for (int i = 0; i < numAnalogueEvents; i++)
				{
					analogueEvents[i] = Marshal.PtrToStructure<avs.InputEventAnalogue>(positionPtr);
					positionPtr += analogueEventSize;
					//Debug.Log("inputState analogue event "+ analogueEvents[i].inputID+" "+ analogueEvents[i].value);
				}
			}

			avs.InputEventMotion[] motionEvents = new avs.InputEventMotion[numMotionEvents];
			if (numMotionEvents != 0)
			{
				int motionEventSize = Marshal.SizeOf<avs.InputEventMotion>();

				//Convert the pointer array into a struct array.
				IntPtr positionPtr = motionEventsPtr;
				for (int i = 0; i < numMotionEvents; i++)
				{
					motionEvents[i] = Marshal.PtrToStructure<avs.InputEventMotion>(positionPtr);
					positionPtr += motionEventSize;
				}
			}

			sessionComponent.ProcessInputEvents( binaryEvents, analogueEvents, motionEvents);
		}
		public static void StaticProcessAudioInput(uid clientID, in IntPtr dataPtr, UInt64 dataSize)
		{
			Teleport_SessionComponent sessionComponent = GetSessionComponent(clientID);
			if (!sessionComponent)
			{
				return;
			}

			float[] data = new float[dataSize / sizeof(float)];
			Marshal.Copy(dataPtr, data, 0, data.Length);
			sessionComponent.ProcessAudioInput(data);
		}

		#endregion

		//PROPERTIES
		public List<uid> ownedNodes;
		//One per session, as we stream geometry on a per-client basis.
		private GeometryStreamingService geometryStreamingService = null;
		public GeometryStreamingService GeometryStreamingService
		{
			get
			{
				return geometryStreamingService;
			}
		}

		//Handshake we have received from the client.
		private avs.Handshake handshake = default;
		public avs.Handshake Handshake
		{
			get
			{
				return handshake;
			}
		}

		public avs.AxesStandard AxesStandard
		{
			get
			{
				return handshake.axesStandard;
			}
		}

		//PUBLIC MEMBER VARIABLES

		// Was the gameobject the session belongs placed in the level or spawned at runtime.
		public bool Spawned { get; set; } = false;

		Teleport_Head _head = null;
		public Teleport_Head head
		{
			get
			{
				return _head;
			}
		}
		Teleport_ClientspaceRoot _clientspaceRoot = null;
		public Teleport_ClientspaceRoot clientspaceRoot
		{
			get
			{
				return _clientspaceRoot;
			}
		}
		Teleport_SceneCaptureComponent _sceneCaptureComponent = null;
		public Teleport_SceneCaptureComponent sceneCaptureComponent
		{
			get
			{
				return _sceneCaptureComponent;
			}
		}
		public AudioSource inputAudioSource = null;

		public teleport.ClientSettings clientSettings = new teleport.ClientSettings();
		public teleport.ClientDynamicLighting clientDynamicLighting =new teleport.ClientDynamicLighting();
		//public teleport.InputDefinition[] inputDefinitions = new teleport.InputDefinition[0];

		//PUBLIC STATIC MEMBER VARIABLES

		public static Dictionary<uid, Teleport_SessionComponent> sessions = new Dictionary<uid, Teleport_SessionComponent>();

		//PRIVATE MEMBER VARIABLES

		private TeleportSettings teleportSettings = null;
		private Input _input=null;
		public Input input
		{
			get
			{
				return _input;
			}
		}

		private uid clientID = 0;

		private HashSet< Teleport_Controller> mappedNodes = new HashSet< Teleport_Controller>();

		private bool resetOrigin = true;

		private Vector3 last_sent_origin = new Vector3(0, 0, 0);
		private Vector3 last_received_origin = new Vector3(0, 0, 0);
		private Vector3 last_received_headPos = new Vector3(0, 0, 0);

		private avs.NetworkStats networkStats;
		private avs.VideoEncoderStats videoEncoderStats;

		private void Awake()
		{
			// Stream the clientspace root, but player does NOT own this:

			if (_clientspaceRoot)
			{
				Teleport_Streamable streamableComponent = _clientspaceRoot.gameObject.GetComponent<Teleport_Streamable>();
				if (!streamableComponent)
				{
					streamableComponent = _clientspaceRoot.gameObject.AddComponent<Teleport_Streamable>();
				}
				streamableComponent.sendMovementUpdates = true;
			}
			//Add Teleport_Streamable component to all player body parts.
			List<GameObject> playerBodyParts = GetPlayerBodyParts();
			foreach (GameObject bodyPart in playerBodyParts)
			{
				if(_clientspaceRoot!=null&&bodyPart == _clientspaceRoot.gameObject)
					continue;
				Teleport_Streamable streamableComponent = bodyPart.GetComponent<Teleport_Streamable>();
				if (!streamableComponent)
				{
					streamableComponent = bodyPart.AddComponent<Teleport_Streamable>();
				}

				//We want the client to control the client-side transform of the body parts for reduced latency.
				streamableComponent.sendMovementUpdates = false;
				streamableComponent.sendEnabledStateUpdates = true;
				streamableComponent.pollCurrentAnimation = true;
			}
		}

		//PUBLIC FUNCTIONS

		public avs.NetworkStats GetNetworkStats()
		{
			return networkStats;
		}

		public avs.VideoEncoderStats GetVideoEncoderStats()
		{
			return videoEncoderStats;
		}

		public void Disconnect()
		{
			if (geometryStreamingService != null)
			{
				geometryStreamingService.Clear();
			}
			foreach(var uid in ownedNodes)
			{
				GameObject gameObject= (GameObject)GeometrySource.GetGeometrySource().FindResource(uid);
				if (gameObject != null)
				{
					Teleport_Streamable teleport_Streamable = gameObject.GetComponent<Teleport_Streamable>();
					teleport_Streamable.OwnerClient=0;
				}
			}
			clientID = 0;
			if (_sceneCaptureComponent != null)
            {
				_sceneCaptureComponent.SetClientID(0);
			}
		}

		public bool IsConnected()
		{
			return Client_IsConnected(clientID);
		}

		public uid GetClientID()
		{
			return clientID;
		}

		public bool HasClient()
		{
			return GetClientID() != 0;
		}

		public void ResetOrigin()
		{
			resetOrigin = true;
		}

		public void SetStreamedLights(Light[] lights)
		{
			geometryStreamingService.SetStreamedLights(lights);
		}

		public void ReportHandshake(avs.Handshake receivedHandshake)
		{
			handshake = receivedHandshake;

			if (teleportSettings.serverSettings.isStreamingGeometry)
			{
				GeometrySource geometrySource = GeometrySource.GetGeometrySource();

				//Send initial animation state on receiving the handshake, as the connection is now ready for commands.
				if(geometryStreamingService!=null)
					geometryStreamingService.SendAnimationState();
			}
		}

		public void SetHeadPose(Quaternion newRotation, Vector3 newPosition)
		{
			if (!_head)
			{
				return;
			}

			if (_head.movementEnabled)
			{
				_head.transform.localPosition = newPosition;
				_head.transform.localRotation = newRotation;
			}
			last_received_headPos = newPosition;
		}

		public void ProcessInputState(byte[] booleanStates, float[] floatStates)
		{
			_input.ProcessInputStates(booleanStates, floatStates);
		}

		public void ProcessInputEvents( avs.InputEventBinary[] binaryEvents, avs.InputEventAnalogue[] analogueEvents, avs.InputEventMotion[] motionEvents)
		{
			_input.ProcessInputEvents(binaryEvents, analogueEvents, motionEvents);
		}

		public void SetControllerPose(uid id, Quaternion newRotation, Vector3 newPosition, Vector3 velocity, Vector3 angularVelocity)
		{
			GeometrySource geometrySource = GeometrySource.GetGeometrySource();
			if (!geometrySource.GetSessionNodes().TryGetValue(id, out GameObject controlledGameObject))
			{
				return;
			}
			if(!controlledGameObject)
				return;
			var streamable= controlledGameObject.GetComponentInParent<Teleport_Streamable>();
			if (!streamable)
			{
				Debug.LogError("Trying to set pose of controlled object "+id+", "+ controlledGameObject.name+" that has no Teleport_Streamable.");
				return;
			}
			if (streamable.OwnerClient != clientID)
			{
				Debug.LogError("Trying to set pose of controlled object " + id + ", " + controlledGameObject.name + " whose owner client " + streamable.OwnerClient+" is not the client ID "+clientID.ToString()+".");
				return;
			}
			controlledGameObject.transform.localPosition=newPosition;
			controlledGameObject.transform.localRotation=newRotation;
			StreamedNode node=streamable.GetStreamedNode(controlledGameObject);
			node.stageSpaceVelocity=velocity;
			node.stageSpaceAngularVelocity=angularVelocity;
		}

		public void ProcessAudioInput(float[] data)
		{
			int numFrames = data.Length / (sizeof(float) * 2);
			inputAudioSource.clip = AudioClip.Create("Input", numFrames, 2, UnityEngine.AudioSettings.outputSampleRate, false);
			inputAudioSource.clip.SetData(data, 0);
			inputAudioSource.Play();
		}

		public string GetClientIP()
		{
			StringBuilder ipAddress = new StringBuilder("", 20);
			try
			{
				uint newlen = Client_GetClientIP(clientID, 16, ipAddress);
				if (newlen > 0)
				{
					ipAddress = new StringBuilder("", (int)newlen + 2);
					Client_GetClientIP(clientID, newlen + 1, ipAddress);
				}
			}
			catch (Exception exc)
			{
				Debug.Log(exc.ToString());
			}
			return ipAddress.ToString();
		}
		avs.DisplayInfo displayInfo;
		public avs.DisplayInfo GetDisplayInfo()
		{
			if(Client_GetClientDisplayInfo(clientID, ref displayInfo))
				return displayInfo;
			displayInfo.framerate=0;
			return displayInfo;
		}
		public void ShowOverlay(int x, int y, GUIStyle font)
		{
			Vector3 headPosition = _head ? _head.transform.position : default;
			Vector3 originPosition = _clientspaceRoot ? _clientspaceRoot.transform.position : default;

			int lineHeight = 14;
			avs.ClientNetworkState clientNetworkState = new avs.ClientNetworkState();
			Teleport_SessionComponent.Client_GetNetworkState(clientID, ref clientNetworkState);
			GUI.Label(new Rect(x, y += lineHeight, 300, 20), string.Format("Client uid {0} {1}", clientID, GetClientIP()), font);
			GUI.Label(new Rect(x, y += lineHeight, 300, 20), string.Format("Signal: {0}", clientNetworkState.signalingState), font);
            GUI.Label(new Rect(x, y += lineHeight, 300, 20), string.Format("Stream: {0}", clientNetworkState.streamingState), font);

        }

		//UNITY MESSAGES

		private void Start()
		{
			Debug.Log("Session Start(): clientID="+GetClientID());
			teleportSettings = TeleportSettings.GetOrCreateSettings();
			if(geometryStreamingService==null)
				geometryStreamingService = new GeometryStreamingService(this);
			networkStats = new avs.NetworkStats();
			videoEncoderStats = new avs.VideoEncoderStats();
			inputAudioSource = new AudioSource();

			// Bypass effects added by the scene's AudioListener
			if (inputAudioSource)
			{
				inputAudioSource.bypassEffects = true;
			}
			_input = GetComponent<Input>();
			if(_input == null)
				_input = gameObject.AddComponent<Input>();
			_head = GetSingleComponentFromChildren<Teleport_Head>();
			_clientspaceRoot = GetSingleComponentFromChildren<Teleport_ClientspaceRoot>();
			// We must have a root node for the player's client space.
			if(_clientspaceRoot!=null)
				geometryStreamingService.SetNodePosePath(_clientspaceRoot.gameObject, "root");
			_sceneCaptureComponent = GetSingleComponentFromChildren<Teleport_SceneCaptureComponent>();
			if(!teleportSettings.serverSettings.StreamVideo)
				_sceneCaptureComponent.enabled=false;
			// Now we've initialized the session, we can initialize any subcomponents that depend on this component.
			var subComponents=GetComponentsInChildren<SessionSubcomponent> ();
			foreach(var s in subComponents)
			{
				s.OnSessionStart();
			}
		}

		private void OnEnable()
		{
			if (clientID != 0)
			{
				sessions[clientID] = this;
			}
			//Place controllers at their assigned index in the lookup.
			Teleport_Controller[] controllers = GetComponentsInChildren<Teleport_Controller>();
			foreach (Teleport_Controller controller in controllers)
			{
				controller.SetSession( this);
				mappedNodes.Add(controller);
			}
		}

		private void OnDisable()
		{
			if (sessions.ContainsKey(clientID))
			{
				sessions.Remove(clientID);
			}
		}

		private void OnDestroy()
		{
			if (clientID != 0)
			{
				Client_StopSession(clientID);
			}
		}

		private void LateUpdate()
		{
			if (Client_IsConnected(clientID))
			{
				SendOriginUpdates();
				Client_GetClientNetworkStats(clientID, ref networkStats);
				Client_GetClientVideoEncoderStats(clientID, ref videoEncoderStats);
			}

			if (teleportSettings.serverSettings.isStreamingGeometry)
			{
				if (geometryStreamingService != null)
				{
					geometryStreamingService.UpdateGeometryStreaming();
				}
			}
		}

		//PRIVATE FUNCTIONS

		public void StartSession(uid connectedID)
		{
			if (connectedID == 0)
            {
				return;
            }
			clientID = connectedID;
			Debug.Log("Started session: clientID="+ clientID);
			sessions[clientID] = this;

			UpdateClientSettings();
			if (geometryStreamingService == null)
				geometryStreamingService = new GeometryStreamingService(this);
			if (geometryStreamingService!=null&&teleportSettings.serverSettings.isStreamingGeometry)
			{
				geometryStreamingService.StreamGlobals();
			}
			foreach (Teleport_Controller controller in mappedNodes)
			{
				if (controller.poseRegexPath.Length>0)
				{
					geometryStreamingService.SetNodePosePath(controller.gameObject,controller.poseRegexPath);
				}
				Teleport_Streamable[] streamables = controller.gameObject.GetComponentsInChildren<Teleport_Streamable>();
				foreach (Teleport_Streamable streamable in streamables)
				{
					streamable.sendMovementUpdates = false;
				}
			}
			if (_sceneCaptureComponent != null)
				_sceneCaptureComponent.SetClientID(clientID);
		}
		private void UpdateClientDynamicLighting(Vector2Int cubeMapsOffset)
		{
			clientDynamicLighting.specularCubemapSize = teleportSettings.serverSettings.defaultSpecularCubemapSize;
			clientDynamicLighting.specularMips = teleportSettings.serverSettings.defaultSpecularMips;
			clientDynamicLighting.diffuseCubemapSize = teleportSettings.serverSettings.defaultDiffuseCubemapSize;
			clientDynamicLighting.lightCubemapSize = teleportSettings.serverSettings.defaultLightCubemapSize;
			// Depth is stored in color's alpha channel if alpha layer encoding is enabled.
			if (teleportSettings.serverSettings.useAlphaLayerEncoding)
			{
				cubeMapsOffset.x = 0;
				const int MIPS_WIDTH = 378;
				clientDynamicLighting.specularPos = new Vector2Int(cubeMapsOffset.x, cubeMapsOffset.y);
				clientDynamicLighting.diffusePos = clientDynamicLighting.specularPos + new Vector2Int(MIPS_WIDTH, 0);
			}
			else
			{
				clientDynamicLighting.specularPos = new Vector2Int(cubeMapsOffset.x, cubeMapsOffset.y);
				clientDynamicLighting.diffusePos = clientDynamicLighting.specularPos + new Vector2Int(0, clientDynamicLighting.specularCubemapSize * 2);
				clientDynamicLighting.lightPos = clientDynamicLighting.diffusePos + new Vector2Int(clientDynamicLighting.specularCubemapSize * 3 / 2, clientDynamicLighting.specularCubemapSize * 2);
			}
			clientDynamicLighting.diffuseCubemapTexture= 0;
			clientDynamicLighting.specularCubemapTexture = 0;
			if (Monitor.Instance.diffuseRenderTexture)
			{
				clientDynamicLighting.diffuseCubemapTexture=GeometrySource.GetGeometrySource().AddTexture(Monitor.Instance.diffuseRenderTexture);
			}
			if (Monitor.Instance.specularRenderTexture)
			{
				clientDynamicLighting.specularCubemapTexture = GeometrySource.GetGeometrySource().AddTexture(Monitor.Instance.specularRenderTexture);
			}
			clientDynamicLighting.lightingMode=Monitor.Instance.lightingMode;
		}
		private void UpdateClientSettings()
		{
			teleportSettings = TeleportSettings.GetOrCreateSettings();
			var settings = teleportSettings.serverSettings;
			clientSettings.shadowmapSize = teleportSettings.serverSettings.defaultShadowmapSize;

			clientSettings.captureCubeTextureSize=teleportSettings.serverSettings.defaultCaptureCubeTextureSize;
			clientSettings.backgroundMode = Monitor.Instance.backgroundMode;
			clientSettings.backgroundColour = Monitor.Instance.BackgroundColour;
			clientSettings.drawDistance=teleportSettings.serverSettings.detectionSphereRadius;
			int faceSize = clientSettings.captureCubeTextureSize;
			int doubleFaceSize = faceSize * 2;
			int halfFaceSize = (int)(faceSize * 0.5);

			int perspectiveWidth = teleportSettings.serverSettings.perspectiveWidth;
			int perspectiveHeight = teleportSettings.serverSettings.perspectiveHeight;

			Vector2Int cubeMapsOffset = new Vector2Int(0, 0);
			// Offsets to lighting cubemaps in video texture
			if (clientSettings.backgroundMode == BackgroundMode.VIDEO)
            {
				if (teleportSettings.serverSettings.usePerspectiveRendering)
				{
					cubeMapsOffset.x = perspectiveWidth / 2;
					cubeMapsOffset.y = perspectiveHeight;
				}
				else
				{ 
					cubeMapsOffset.x = halfFaceSize * 3;
					cubeMapsOffset.y = doubleFaceSize;
				}
			}
			UpdateClientDynamicLighting(cubeMapsOffset);
			// Depth is stored in color's alpha channel if alpha layer encoding is enabled.
			if (teleportSettings.serverSettings.useAlphaLayerEncoding)
			{
				// We don't currently encode shadows or use light cubemap
				//clientSettings.lightPos = clientSettings.diffusePos + new Vector2Int(clientSettings.diffuseCubemapSize * 3, 0);
				//clientSettings.shadowmapPos = clientSettings.lightPos + new Vector2Int(MIPS_WIDTH, 0);
				clientSettings.webcamPos = clientDynamicLighting.diffusePos + new Vector2Int(clientDynamicLighting.diffuseCubemapSize * 3, 0);
			}
			else
			{
				clientSettings.shadowmapPos = clientDynamicLighting.diffusePos + new Vector2Int(0, 2 * clientDynamicLighting.diffuseCubemapSize);
				clientSettings.webcamPos = cubeMapsOffset + new Vector2Int(clientDynamicLighting.specularCubemapSize * 3, clientDynamicLighting.specularCubemapSize * 2);
			}
			clientSettings.webcamSize = new Vector2Int(settings.webcamWidth, settings.webcamHeight);
			// find the size of the video texture.
			if (clientSettings.backgroundMode == BackgroundMode.VIDEO)
			{ 
				clientSettings.videoTextureSize.x= clientSettings.videoTextureSize.y=0;
				if (teleportSettings.serverSettings.usePerspectiveRendering)
				{
					clientSettings.videoTextureSize.x = Math.Max(clientSettings.videoTextureSize.x, teleportSettings.serverSettings.perspectiveWidth);
					clientSettings.videoTextureSize.y = Math.Max(clientSettings.videoTextureSize.y,settings.perspectiveHeight);
				}
				else
				{
					clientSettings.videoTextureSize.x = Math.Max(clientSettings.videoTextureSize.x, faceSize * 3);
					clientSettings.videoTextureSize.y = Math.Max(clientSettings.videoTextureSize.y, faceSize * 2);
				}
				// Is depth separate?
				if (!teleportSettings.serverSettings.useAlphaLayerEncoding)
				{
					clientSettings.videoTextureSize.y = Math.Max(clientSettings.videoTextureSize.y, clientSettings.videoTextureSize.y+ clientSettings.videoTextureSize.y/2);
				}
			}
			clientSettings.videoTextureSize.x = Math.Max(clientSettings.videoTextureSize.x, clientDynamicLighting.diffusePos.x+ clientDynamicLighting.diffuseCubemapSize * 3);
			clientSettings.videoTextureSize.y = Math.Max(clientSettings.videoTextureSize.y, clientDynamicLighting.diffusePos.y + clientDynamicLighting.diffuseCubemapSize *2);
			if (teleportSettings.serverSettings.StreamWebcam)
            {
				clientSettings.videoTextureSize.x = Math.Max(clientSettings.videoTextureSize.x, clientSettings.webcamPos.x + clientSettings.webcamSize.x);
				clientSettings.videoTextureSize.y = Math.Max(clientSettings.videoTextureSize.y, clientSettings.webcamPos.y + clientSettings.webcamSize.y);
			}

			avs.VideoEncodeCapabilities videoEncodeCapabilities = VideoEncoder.GetEncodeCapabilities();
			if (clientSettings.videoTextureSize.x < videoEncodeCapabilities.minWidth || clientSettings.videoTextureSize.x > videoEncodeCapabilities.maxWidth
				|| clientSettings.videoTextureSize.y < videoEncodeCapabilities.minHeight || clientSettings.videoTextureSize.y > videoEncodeCapabilities.maxHeight)
			{
				Debug.LogError("The video encoder does not support the video texture dimensions.");
			}
			//Debug.Log("ClientSettings.videoTextureSize: "+clientSettings.videoTextureSize.x+", "+clientSettings.videoTextureSize.y);
			//Debug.Log("ClientSettings.drawDistance: "+clientSettings.drawDistance);
			Client_SetClientSettings(clientID, clientSettings);
			Client_SetClientDynamicLighting(clientID, clientDynamicLighting); 
			// Just use one set of input defs for now.
			string [] controlPaths=new string[teleportSettings.inputDefinitions.Count];
			avs.InputDefinitionInterop[] inputDefsInterop=new avs.InputDefinitionInterop[teleportSettings.inputDefinitions.Count];
			for (int i = 0; i < teleportSettings.inputDefinitions.Count; i++)
            {
				var def= teleportSettings.inputDefinitions[i];

				inputDefsInterop[i].inputID=(System.UInt16)i;
				inputDefsInterop[i].inputType=def.inputType;
				controlPaths[i]=def.controlPath;
			}
			Client_SetClientInputDefinitions(clientID, inputDefsInterop.Length,controlPaths,inputDefsInterop);
		}

		private void SendOriginUpdates()
		{
			uid origin_uid =0;
			if (_clientspaceRoot != null)
			{
				Teleport_Streamable streamable =_clientspaceRoot.gameObject.GetComponent<Teleport_Streamable>();
				if(streamable!=null)
				{
					origin_uid = streamable.GetUid();
					if(!streamable.sendMovementUpdates)
						streamable.sendMovementUpdates=true;
				}
			}
			if (teleportSettings.serverSettings.controlModel == teleport.ControlModel.SERVER_ORIGIN_CLIENT_LOCAL)
			{
				if (_head != null && _clientspaceRoot != null)
				{
					if (!Client_HasOrigin(clientID) || resetOrigin || _clientspaceRoot.transform.hasChanged)
					{
						if (Client_SetOrigin(clientID,  origin_uid))
						{
							last_sent_origin = _clientspaceRoot.transform.position;
							_clientspaceRoot.transform.hasChanged = false;
							resetOrigin = false;
						}
					}
				}
			}
		}

		private string FormatVectorString(Vector3 v)
		{
			return string.Format("({0:F3}, {1:F3}, {2:F3})", v.x, v.y, v.z);
		}

		private T GetSingleComponentFromChildren<T>()
		{
			T[] childComponents = GetComponentsInChildren<T>();

			if (childComponents.Length != 1)
			{
				Debug.LogError($"Exactly <b>one</b> {typeof(T).Name} child should exist, but <color=red><b>{childComponents.Length}</b></color> were found for \"{gameObject}\"!");
			}

			return childComponents.Length != 0 ? childComponents[0] : default;
		}

		//Returns list of streamable GameObjects in this session's hierarchy that are controlled by
		// the client: i.e. they have a Teleport_Controller.. 
		public List<GameObject> GetPlayerBodyParts()
		{
			List<GameObject> bodyParts = new List<GameObject>();
			var c=GetComponentsInChildren<Teleport_Controller>();
			foreach (var o in c)
			{
				if(o.gameObject.GetComponent<Teleport_Streamable>()!=null)
					bodyParts.Add(o.gameObject);
			}
			return bodyParts;
		}
	}
}