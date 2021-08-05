﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

using uid = System.UInt64;

namespace teleport
{
	public class Teleport_SessionComponent : MonoBehaviour
	{
		#region DLLImports

		[DllImport("SimulCasterServer")]
		private static extern bool Client_IsConnected(uid clientID);
		[DllImport("SimulCasterServer")]
		public static extern bool Client_HasHost(uid clientID);
		[DllImport("SimulCasterServer")]
		public static extern bool Client_HasPeer(uid clientID);

		[DllImport("SimulCasterServer")]
		public static extern void Client_StopSession(uid clientID);
		[DllImport("SimulCasterServer")]
		public static extern void Client_StopStreaming(uid clientID);

		[DllImport("SimulCasterServer")]
		public static extern uint Client_GetClientIP(uid clientID, uint bufferLength, StringBuilder buffer);
		[DllImport("SimulCasterServer")]
		public static extern UInt16 Client_GetClientPort(uid clientID);
		[DllImport("SimulCasterServer")]
		public static extern UInt16 Client_GetServerPort(uid clientID);

		[DllImport("SimulCasterServer")]
		public static extern bool Client_GetClientNetworkStats(uid clientID, ref avs.NetworkStats stats);

		// Set the client-specific settings, e.g. video layout.
		[DllImport("SimulCasterServer")]
		private static extern void Client_SetClientSettings(uid clientID, SCServer.ClientSettings clientSettings);

		[DllImport("SimulCasterServer")]
		private static extern bool Client_SetOrigin(uid clientID, UInt64 validCounter, Vector3 pos, [MarshalAs(UnmanagedType.U1)] bool set_rel, Vector3 rel_pos, Quaternion orientation);
		[DllImport("SimulCasterServer")]
		private static extern bool Client_HasOrigin(uid clientID);

		[DllImport("SimulCasterServer")]
		private static extern uid GetUnlinkedClientID();

		[DllImport("SimulCasterServer")]
		private static extern void Client_UpdateNodeAnimationControl(uid clientID, avs.NodeUpdateAnimationControl update);
		[DllImport("SimulCasterServer")]
		private static extern void Client_SetNodeAnimationSpeed(uid clientID, uid nodeID, uid animationID, float speed);

		#endregion

		#region StaticCallbacks

		// Aidan: This is temporary for the capture component
		//TODO: Replace this with something that is not temporary.
		public static uid GetLastClientID()
		{
			if (sessions.Count > 0)
			{
				return sessions.Last().Key;
			}
			else
			{
				return 0;
			}
		}

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
				TeleportLog.LogErrorOnce($"No session component found for Client_{clientID}!");
			}

			return sessionComponent;
		}

		public static void StaticDisconnect(uid clientID)
		{
			if (sessions.ContainsKey(clientID))
			{
				sessions[clientID].Disconnect();
				sessions.Remove(clientID);
			}

			// This MUST be called for connection / reconnection to work properly.
			Client_StopStreaming(clientID);
		}

		public static void StaticSetOriginFromClient(uid clientID, UInt64 validCounter, in avs.Pose newPose)
		{
			Teleport_SessionComponent sessionComponent = GetSessionComponent(clientID);
			if (!sessionComponent)
			{
				return;
			}

			Quaternion rotation = new Quaternion(newPose.orientation.x, newPose.orientation.y, newPose.orientation.z, newPose.orientation.w);
			Vector3 position = new Vector3(newPose.position.x, newPose.position.y, newPose.position.z);
			sessionComponent.SetOriginFromClient(validCounter, rotation, position);
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

		public static void StaticSetControllerPose(uid clientID, int index, in avs.Pose newPose)
		{
			Teleport_SessionComponent sessionComponent = GetSessionComponent(clientID);
			if (!sessionComponent)
			{
				return;
			}

			Quaternion latestRotation = new Quaternion(newPose.orientation.x, newPose.orientation.y, newPose.orientation.z, newPose.orientation.w);
			Vector3 latestPosition = new Vector3(newPose.position.x, newPose.position.y, newPose.position.z);
			sessionComponent.SetControllerPose(index, latestRotation, latestPosition);
		}

		public static void StaticProcessInput(uid clientID, in avs.InputState inputState, in IntPtr binaryEventsPtr, in IntPtr analogueEventsPtr, in IntPtr motionEventsPtr)
		{
			Teleport_SessionComponent sessionComponent = GetSessionComponent(clientID);
			if (!sessionComponent)
			{
				return;
			}

			sessionComponent.SetControllerInput(inputState.controllerID, inputState.buttonsDown, inputState.joystickAxisX, inputState.joystickAxisY);

			avs.InputEventBinary[] binaryEvents = new avs.InputEventBinary[inputState.binaryEventAmount];
			if (inputState.binaryEventAmount != 0)
			{
				int binaryEventSize = Marshal.SizeOf<avs.InputEventBinary>();

				//Convert the pointer array into a struct array.
				IntPtr positionPtr = binaryEventsPtr;
				for (int i = 0; i < inputState.binaryEventAmount; i++)
				{
					binaryEvents[i] = Marshal.PtrToStructure<avs.InputEventBinary>(positionPtr);
					positionPtr += binaryEventSize;
				}
			}

			avs.InputEventAnalogue[] analogueEvents = new avs.InputEventAnalogue[inputState.analogueEventAmount];
			if (inputState.analogueEventAmount != 0)
			{
				int analogueEventSize = Marshal.SizeOf<avs.InputEventAnalogue>();

				//Convert the pointer array into a struct array.
				IntPtr positionPtr = analogueEventsPtr;
				for (int i = 0; i < inputState.analogueEventAmount; i++)
				{
					analogueEvents[i] = Marshal.PtrToStructure<avs.InputEventAnalogue>(positionPtr);
					positionPtr += analogueEventSize;
				}
			}

			avs.InputEventMotion[] motionEvents = new avs.InputEventMotion[inputState.motionEventAmount];
			if (inputState.motionEventAmount != 0)
			{
				int motionEventSize = Marshal.SizeOf<avs.InputEventMotion>();

				//Convert the pointer array into a struct array.
				IntPtr positionPtr = motionEventsPtr;
				for (int i = 0; i < inputState.motionEventAmount; i++)
				{
					motionEvents[i] = Marshal.PtrToStructure<avs.InputEventMotion>(positionPtr);
					positionPtr += motionEventSize;
				}
			}

			sessionComponent.ProcessControllerEvents(inputState.controllerID, binaryEvents, analogueEvents, motionEvents);
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

		//One per session, as we stream geometry on a per-client basis.
		private GeometryStreamingService geometryStreamingService = default;
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

		public int maxNodesOnOverlay = 10; //Amount of nodes to show on the overlay before breaking.
		public int maxLightsOnOverlay = 5; //Amount of lights to show on the overlay before breaking.

		public Teleport_Head head = null;
		public Teleport_ClientspaceRoot clientspaceRoot = null;
		public Teleport_CollisionRoot collisionRoot = null;
		public Teleport_SceneCaptureComponent sceneCaptureComponent = null;
		public Teleport_AudioCaptureComponent audioCaptureComponent = null;
		public AudioSource inputAudioSource = null;

		public List<Teleport_Controller> controllers;

		public SCServer.ClientSettings clientSettings = new SCServer.ClientSettings();

		//PUBLIC STATIC MEMBER VARIABLES

		public static Dictionary<uid, Teleport_SessionComponent> sessions = new Dictionary<uid, Teleport_SessionComponent>();

		//PRIVATE MEMBER VARIABLES

		private TeleportSettings teleportSettings = null;

		private uid clientID = 0;

		private Dictionary<int, Teleport_Controller> controllerLookup = new Dictionary<int, Teleport_Controller>();

		private bool resetOrigin = true;
		private UInt64 originValidCounter = 1;

		private Vector3 last_sent_origin = new Vector3(0, 0, 0);
		private Vector3 last_received_origin = new Vector3(0, 0, 0);
		private Vector3 last_received_headPos = new Vector3(0, 0, 0);

		private avs.NetworkStats networkStats;

		//PUBLIC FUNCTIONS

		public avs.NetworkStats GetNetworkStats()
		{
			return networkStats;
		}

		public void Disconnect()
		{
			if (geometryStreamingService != null)
			{
				geometryStreamingService.Clear();
			}

			clientID = 0;
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


			if (teleportSettings.casterSettings.isStreamingGeometry)
			{
				GeometrySource geometrySource = GeometrySource.GetGeometrySource();

				//Send initial animation state on receiving the handshake, as the connection is now ready for commands.
				geometryStreamingService.SendAnimationState();

				//Send animation control updates for the grip animation of the controllers.
				foreach (Teleport_Controller controller in controllerLookup.Values)
				{
					SkinnedMeshRenderer skinnedMeshRenderer = controller.controllerModel.GetComponentInChildren<SkinnedMeshRenderer>();
					if (!skinnedMeshRenderer)
					{
						continue;
					}

					//We need the ID of the node the animations actually occur on.
					uid animatedNodeID = geometrySource.FindResourceID(skinnedMeshRenderer.gameObject);

					//Set time override for controller press animation.
					avs.NodeUpdateAnimationControl animationControlUpdate = new avs.NodeUpdateAnimationControl
					{
						nodeID = animatedNodeID,
						animationID = geometrySource.FindResourceID(controller.triggerPressAnimation),
						timeControl = controller.pressAnimationTimeOverride
					};

					Client_UpdateNodeAnimationControl(clientID, animationControlUpdate);

					//Set speed of controller animations.
					Animator animator = controller.controllerModel.GetComponentInChildren<Animator>();
					if(animator)
					{
						UnityEditor.Animations.AnimatorController animatorController = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
						UnityEditor.Animations.AnimatorStateMachine stateMachine = animatorController.layers[0].stateMachine;

						foreach(UnityEditor.Animations.ChildAnimatorState stateWrapper in stateMachine.states)
						{
							UnityEditor.Animations.AnimatorState state = stateWrapper.state;

							switch(state.motion)
							{
								case AnimationClip clip:
									uid animationID = geometrySource.FindResourceID(clip);
									if(animationID != 0)
									{
										Client_SetNodeAnimationSpeed(clientID, animatedNodeID, animationID, state.speed);
									}
									else
									{
										Debug.LogWarning($"Animation \"{clip.name}\" not extracted! Can't set speed of animation for {controller.controllerModel.name}.");
									}

									break;
								case UnityEditor.Animations.BlendTree blendTree:
									Debug.LogWarning($"Teleport currently does not support BlendTrees in AnimatorControllers. BlendTree \"{blendTree.name}\" from AnimatorState \"{state.name}\" on GameObject \"{animator.name}\".");
									break;
								default:
									Debug.LogWarning($"Unrecognised Motion \"{state.motion.name}\" from AnimatorState \"{state.name}\" on GameObject \"{animator.name}\".");
									break;
							}
						}
					}
				}
			}
		}

		public void SetOriginFromClient(UInt64 validCounter, Quaternion newRotation, Vector3 newPosition)
		{
			if (clientspaceRoot != null && validCounter == originValidCounter)
			{
				clientspaceRoot.transform.SetPositionAndRotation(newPosition, newRotation);
				last_received_origin = newPosition;
			}
		}

		public void SetHeadPose(Quaternion newRotation, Vector3 newPosition)
		{
			if (!head)
			{
				return;
			}

			if (head.movementEnabled)
			{
				head.transform.SetPositionAndRotation(newPosition, newRotation);
			}
			last_received_headPos = newPosition;
		}

		public void SetControllerInput(int controllerIndex, UInt32 buttons, float stickX, float stickY)
		{
			if (!controllerLookup.TryGetValue(controllerIndex, out Teleport_Controller controller))
			{
				return;
			}

			controller.SetButtons(buttons);
			controller.SetJoystick(stickX, stickY);
		}

		public void ProcessControllerEvents(int controllerIndex, avs.InputEventBinary[] binaryEvents, avs.InputEventAnalogue[] analogueEvents, avs.InputEventMotion[] motionEvents)
		{
			if (!controllerLookup.TryGetValue(controllerIndex, out Teleport_Controller controller))
			{
				return;
			}

			controller.ProcessInputEvents(binaryEvents, analogueEvents, motionEvents);
		}

		public void SetControllerPose(int controllerIndex, Quaternion newRotation, Vector3 newPosition)
		{
			if (!controllerLookup.TryGetValue(controllerIndex, out Teleport_Controller controller))
			{
				return;
			}

			controller.transform.SetPositionAndRotation(newPosition, newRotation);
		}

		public void ProcessAudioInput(float[] data)
		{
			int numFrames = data.Length / (sizeof(float) * 2);
			inputAudioSource.clip = AudioClip.Create("Input", numFrames, 2, AudioSettings.outputSampleRate, false);
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

		public void ShowOverlay(int x, int y, GUIStyle font)
		{
			Vector3 headPosition = head ? head.transform.position : default;
			Vector3 originPosition = clientspaceRoot ? clientspaceRoot.transform.position : default;

			int lineHeight = 14;
			GUI.Label(new Rect(x, y += lineHeight, 300, 20), string.Format("Client_{0} {1}", clientID, GetClientIP()), font);

			//Add a break for readability.
			y += lineHeight;

			GUI.Label(new Rect(x, y += lineHeight, 300, 20), string.Format("available bandwidth\t{0:F3} mb/s", networkStats.bandwidth), font);
			GUI.Label(new Rect(x, y += lineHeight, 300, 20), string.Format("avg bandwidth used\t{0:F3} mb/s", networkStats.avgBandwidthUsed), font);
			GUI.Label(new Rect(x, y += lineHeight, 300, 20), string.Format("max bandwidth used\t{0:F3} mb/s", networkStats.maxBandwidthUsed), font);

			//Add a break for readability.
			y += lineHeight;

			GUI.Label(new Rect(x, y += lineHeight, 300, 20), string.Format("origin pos\t{0}", FormatVectorString(originPosition)), font);
			GUI.Label(new Rect(x, y += lineHeight, 300, 20), string.Format("sent origin\t{0}", FormatVectorString(last_sent_origin)), font);
			GUI.Label(new Rect(x, y += lineHeight, 300, 20), string.Format("received origin\t{0}", FormatVectorString(last_received_origin)), font);
			GUI.Label(new Rect(x, y += lineHeight, 300, 20), string.Format("received head\t{0}", FormatVectorString(last_received_headPos)), font);
			GUI.Label(new Rect(x, y += lineHeight, 300, 20), string.Format("head position\t{0}", FormatVectorString(headPosition)), font);

			//Add a break for readability.
			y += lineHeight;

			for (int i = 0; i < controllers.Count; i++)
			{
				Teleport_Controller controller = controllers[i];
				if (!controller)
				{
					continue;
				}

				GUI.Label(new Rect(x, y += lineHeight, 300, 20), string.Format("Controller {0}, {1}", i, FormatVectorString(controller.transform.position)));
				GUI.Label(new Rect(x, y += lineHeight, 300, 20), string.Format("\tbtns:{0} stick:{1:F3},{2:F3}", controller.buttons, controller.joystick.x, controller.joystick.y));
			}

			if (geometryStreamingService != null)
			{
				GeometrySource geometrySource = GeometrySource.GetGeometrySource();

				//Add a break for readability.
				y += lineHeight;

				//Display amount of nodes.
				int nodeAmount = geometryStreamingService.GetStreamedObjectCount();
				GUI.Label(new Rect(x, y += lineHeight, 300, 20), string.Format("Nodes {0}", nodeAmount));

				List<GameObject> streamedGameObjects = geometryStreamingService.GetStreamedObjects();
				//List nodes to the maximum.
				for (int i = 0; i < nodeAmount && i < maxNodesOnOverlay; i++)
				{
					GameObject node = streamedGameObjects[i];
					uid nodeID = geometrySource.FindResourceID(node);
					GUI.Label(new Rect(x, y += lineHeight, 500, 20), string.Format("\t{0} {1}", nodeID, node.name));
				}

				//Display an ellipsis if there are more than the maximum nodes to display.
				if (nodeAmount > maxNodesOnOverlay)
				{
					GUI.Label(new Rect(x, y += lineHeight, 500, 20), "\t...");
				}

				//Add a break for readability.
				y += lineHeight;

				//Display amount of lights.
				int lightAmount = geometryStreamingService.GetStreamedLightCount();
				GUI.Label(new Rect(x, y += lineHeight, 300, 20), string.Format("Lights {0}", lightAmount));

				int validLightIndex = 0;
				foreach (var lightPair in geometryStreamingService.GetStreamedLights())
				{
					Light light = lightPair.Value;
					if (light != null)
					{
						GUI.Label(new Rect(x, y += lineHeight, 500, 20), string.Format("\t{0} {1}: ({2}, {3}, {4})", lightPair.Key, light.name, light.transform.forward.x, light.transform.forward.y, light.transform.forward.z));
						if (sceneCaptureComponent.VideoEncoder != null && validLightIndex < sceneCaptureComponent.VideoEncoder.cubeTagDataWrapper.data.lightCount)
						{
							avs.Vector3 shadowPosition = sceneCaptureComponent.VideoEncoder.cubeTagDataWrapper.data.lights[validLightIndex].position;
							GUI.Label(new Rect(x, y += lineHeight, 500, 20), string.Format("\t\tshadow orig ({0}, {1}, {2})", shadowPosition.x, shadowPosition.y, shadowPosition.z));
						}
						validLightIndex++;
					}

					//Break if we have displayed the maximum amount of lights.
					if (validLightIndex >= maxLightsOnOverlay)
					{
						GUI.Label(new Rect(x, y += lineHeight, 500, 20), "\t...");
						break;
					}
				}
			}
		}

		//UNITY MESSAGES

		private void Start()
		{
			teleportSettings = TeleportSettings.GetOrCreateSettings();
			geometryStreamingService = new GeometryStreamingService(this);
			networkStats = new avs.NetworkStats();
			inputAudioSource = new AudioSource();

			// Bypass effects added by the scene's AudioListener
			if (inputAudioSource)
			{
				inputAudioSource.bypassEffects = true;
			}

			head = GetSingleComponentFromChildren<Teleport_Head>();
			clientspaceRoot = GetSingleComponentFromChildren<Teleport_ClientspaceRoot>();
			collisionRoot = GetSingleComponentFromChildren<Teleport_CollisionRoot>();
			sceneCaptureComponent = GetSingleComponentFromChildren<Teleport_SceneCaptureComponent>();
			audioCaptureComponent = GetSingleComponentFromChildren<Teleport_AudioCaptureComponent>();
		}

		private void OnEnable()
		{
			if (clientID != 0)
			{
				sessions[clientID] = this;
			}

			//Place controllers at their assigned index in the lookup.
			foreach (Teleport_Controller controller in controllers)
			{
				controller.session = this;
				controllerLookup[(int)controller.Index] = controller;
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
			if (clientID == 0)
			{
				uid id = GetUnlinkedClientID();
				if (id == 0)
				{
					return;
				}

				if (sessions.ContainsKey(id))
				{
					Debug.LogError($"Error setting up SessionComponent for Client_{id}. There is already a registered session for that client!");
					return;
				}

				StartSession(id);
			}

			if (Client_IsConnected(clientID))
			{
				SendOriginUpdates();
				Client_GetClientNetworkStats(clientID, ref networkStats);
			}

			if (teleportSettings.casterSettings.isStreamingGeometry)
			{
				if (geometryStreamingService != null)
				{
					geometryStreamingService.UpdateGeometryStreaming();
				}
			}
		}

		//PRIVATE FUNCTIONS

		private void StartSession(uid connectedID)
		{
			clientID = connectedID;
			sessions[clientID] = this;
			UpdateClientSettings();

			if(geometryStreamingService!=null&&teleportSettings.casterSettings.isStreamingGeometry)
			{
				geometryStreamingService.StreamGlobals();
			}
			foreach (Teleport_Controller controller in controllers)
			{
				Teleport_Streamable[] streamables =controller.gameObject.GetComponentsInChildren<Teleport_Streamable>();
				foreach (Teleport_Streamable streamable in streamables)
				{
					streamable.sendMovementUpdates = false;
				}
			}
		}
		private void UpdateClientSettings()
		{
			teleportSettings = TeleportSettings.GetOrCreateSettings();
			var settings = teleportSettings.casterSettings;
			clientSettings.specularCubemapSize = teleportSettings.casterSettings.defaultSpecularCubemapSize;
			clientSettings.specularMips = teleportSettings.casterSettings.defaultSpecularMips;
			clientSettings.diffuseCubemapSize = teleportSettings.casterSettings.defaultDiffuseCubemapSize;
			clientSettings.lightCubemapSize = teleportSettings.casterSettings.defaultLightCubemapSize;
			clientSettings.shadowmapSize = teleportSettings.casterSettings.defaultShadowmapSize;

			int faceSize = teleportSettings.casterSettings.captureCubeTextureSize;
			int doubleFaceSize = faceSize * 2;
			int halfFaceSize = (int)(faceSize * 0.5);

			int perspectiveWidth = teleportSettings.casterSettings.perspectiveWidth;
			int perspectiveHeight = teleportSettings.casterSettings.perspectiveHeight;

			// Offsets to lighting cubemaps in video texture
			Vector2Int cubeMapsOffset = new Vector2Int(0, 0);

			if (teleportSettings.casterSettings.usePerspectiveRendering)
			{
				clientSettings.videoTextureSize.x = teleportSettings.casterSettings.perspectiveWidth;
				if (teleportSettings.casterSettings.useAlphaLayerEncoding)
				{
					clientSettings.videoTextureSize.y = settings.perspectiveHeight + (teleportSettings.casterSettings.defaultSpecularCubemapSize * 2);
				}
				else
				{
					clientSettings.videoTextureSize.y = (int)(settings.perspectiveHeight * 1.5f);
				}
				cubeMapsOffset.x = perspectiveWidth / 2;
				cubeMapsOffset.y = perspectiveHeight;
			}
			else
			{ 
				clientSettings.videoTextureSize.x = faceSize * 3;
				if (teleportSettings.casterSettings.useAlphaLayerEncoding)
				{
					clientSettings.videoTextureSize.y = faceSize * 2 + (teleportSettings.casterSettings.defaultSpecularCubemapSize * 2);
				}
				else
				{
					clientSettings.videoTextureSize.y = faceSize * 3;
				}
				cubeMapsOffset.x = halfFaceSize * 3;
				cubeMapsOffset.y = doubleFaceSize;
			}

			// Depth is stored in color's alpha channel if alpha layer encoding is enabled.
			if (teleportSettings.casterSettings.useAlphaLayerEncoding)
			{
				cubeMapsOffset.x = 0;
				const int MIPS_WIDTH = 378;
				clientSettings.specularPos = new Vector2Int(cubeMapsOffset.x, cubeMapsOffset.y);
				clientSettings.diffusePos = clientSettings.specularPos + new Vector2Int(MIPS_WIDTH, 0);
				// We don't currently encode shadows or use light cubemap
				//clientSettings.lightPos = clientSettings.diffusePos + new Vector2Int(clientSettings.diffuseCubemapSize * 3, 0);
				//clientSettings.shadowmapPos = clientSettings.lightPos + new Vector2Int(MIPS_WIDTH, 0);
				clientSettings.webcamPos = clientSettings.diffusePos + new Vector2Int(clientSettings.diffuseCubemapSize * 3, 0);
				clientSettings.webcamSize = new Vector2Int(settings.webcamWidth, settings.webcamHeight);
			}
			else
			{
				clientSettings.specularPos = new Vector2Int(cubeMapsOffset.x, cubeMapsOffset.y);
				clientSettings.diffusePos = clientSettings.specularPos + new Vector2Int(0, clientSettings.specularCubemapSize * 2);
				clientSettings.lightPos = clientSettings.diffusePos + new Vector2Int(clientSettings.specularCubemapSize * 3 / 2, clientSettings.specularCubemapSize * 2);
				clientSettings.shadowmapPos = clientSettings.diffusePos + new Vector2Int(0, 2 * clientSettings.diffuseCubemapSize);

				clientSettings.webcamPos = cubeMapsOffset + new Vector2Int(clientSettings.specularCubemapSize * 3, clientSettings.specularCubemapSize * 2);
				clientSettings.webcamSize = new Vector2Int(settings.webcamWidth, settings.webcamHeight);
			}
			Client_SetClientSettings(clientID, clientSettings);
		}

		private void SendOriginUpdates()
		{
			if (teleportSettings.casterSettings.controlModel == SCServer.ControlModel.CLIENT_ORIGIN_SERVER_GRAVITY)
			{
				if (head != null && clientspaceRoot != null)
				{
					if (!Client_HasOrigin(clientID) || resetOrigin)
					{
						originValidCounter++;
						if (Client_SetOrigin(clientID, originValidCounter, clientspaceRoot.transform.position, false, head.transform.position - clientspaceRoot.transform.position, clientspaceRoot.transform.rotation))
						{
							last_sent_origin = clientspaceRoot.transform.position;
							clientspaceRoot.transform.hasChanged = false;
							resetOrigin = false;
						}
					}
					else if (clientspaceRoot.transform.hasChanged)
					{
						Vector3 diff = clientspaceRoot.transform.position - last_received_origin;
						if (diff.magnitude > 5.0F)
						{
							originValidCounter++;
						}
						// Otherwise just a "suggestion" update. ValidCounter is not altered. The client will use the vertical only.
						if (Client_SetOrigin(clientID, originValidCounter, clientspaceRoot.transform.position, false, head.transform.position - clientspaceRoot.transform.position, clientspaceRoot.transform.rotation))
						{
							last_sent_origin = clientspaceRoot.transform.position;
							clientspaceRoot.transform.hasChanged = false;
						}
					}
				}

				if (collisionRoot != null && collisionRoot.transform.hasChanged)
				{
					collisionRoot.transform.hasChanged = false;
				}
			}
			else if (teleportSettings.casterSettings.controlModel == SCServer.ControlModel.SERVER_ORIGIN_CLIENT_LOCAL)
			{
				if (head != null && clientspaceRoot != null)
				{
					if (!Client_HasOrigin(clientID) || resetOrigin || clientspaceRoot.transform.hasChanged)
					{
						originValidCounter++;
						if (Client_SetOrigin(clientID, originValidCounter, clientspaceRoot.transform.position, false, head.transform.position - clientspaceRoot.transform.position, clientspaceRoot.transform.rotation))
						{
							last_sent_origin = clientspaceRoot.transform.position;
							clientspaceRoot.transform.hasChanged = false;
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
	}
}