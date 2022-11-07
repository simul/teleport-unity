﻿using System;
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

		[DllImport("TeleportServer")]
		private static extern bool Client_IsConnected(uid clientID);
		[DllImport("TeleportServer")]
		public static extern bool Client_HasHost(uid clientID);
		[DllImport("TeleportServer")]
		public static extern bool Client_HasPeer(uid clientID);

		[DllImport("TeleportServer")]
		public static extern void Client_StopSession(uid clientID);
		[DllImport("TeleportServer")]
		public static extern void Client_StopStreaming(uid clientID);

		[DllImport("TeleportServer")]
		public static extern uint Client_GetClientIP(uid clientID, uint bufferLength, StringBuilder buffer);
		[DllImport("TeleportServer")]
		public static extern UInt16 Client_GetClientPort(uid clientID);
		[DllImport("TeleportServer")]
		public static extern UInt16 Client_GetServerPort(uid clientID);

		[DllImport("TeleportServer")]
		public static extern bool Client_GetClientNetworkStats(uid clientID, ref avs.NetworkStats stats);
		[DllImport("TeleportServer")]
		public static extern bool Client_GetClientVideoEncoderStats(uid clientID, ref avs.VideoEncoderStats stats);

		// Set the client-specific settings, e.g. video layout.
		[DllImport("TeleportServer")]
		private static extern void Client_SetClientSettings(uid clientID, teleport.ClientSettings clientSettings);
		// Set the client-specific lighting, e.g. video or texture lighting.
		[DllImport("TeleportServer")]
		private static extern void Client_SetClientDynamicLighting(uid clientID, teleport.ClientDynamicLighting clientDynamicLighting);

		[DllImport("TeleportServer")]
		private static extern void Client_SetClientInputDefinitions(uid clientID, int numControls, string[] controlPaths, avs.InputDefinitionInterop[] inputDefinitions);

		[DllImport("TeleportServer")]
		private static extern bool Client_SetOrigin(uid clientID, uid originNodeID, UInt64 validCounter, Vector3 pos, Quaternion orientation);
		[DllImport("TeleportServer")]
		private static extern bool Client_HasOrigin(uid clientID);

		[DllImport("TeleportServer")]
		private static extern void Client_UpdateNodeAnimationControl(uid clientID, avs.NodeUpdateAnimationControl update);
		[DllImport("TeleportServer")]
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
					if (session.head != null)
					{
						Camera.main.transform.parent = session.head.transform;
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

		public static void StaticProcessInput(uid clientID, in avs.InputState inputState
			, in IntPtr binaryStatesPtr, in IntPtr analogueStatesPtr, in IntPtr binaryEventsPtr, in IntPtr analogueEventsPtr, in IntPtr motionEventsPtr)
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
			avs.InputEventBinary[] binaryEvents = new avs.InputEventBinary[inputState.numBinaryEvents];
			if (inputState.numBinaryEvents != 0)
			{
				int binaryEventSize = Marshal.SizeOf<avs.InputEventBinary>();

				//Convert the pointer array into a struct array.
				IntPtr positionPtr = binaryEventsPtr;
				for (int i = 0; i < inputState.numBinaryEvents; i++)
				{
					binaryEvents[i] = Marshal.PtrToStructure<avs.InputEventBinary>(positionPtr);
					positionPtr += binaryEventSize;
					//Debug.Log("inputState binary event " + binaryEvents[i].inputID + " " + binaryEvents[i].activated);
				}
			}

			avs.InputEventAnalogue[] analogueEvents = new avs.InputEventAnalogue[inputState.numAnalogueEvents];
			if (inputState.numAnalogueEvents != 0)
			{
				int analogueEventSize = Marshal.SizeOf<avs.InputEventAnalogue>();

				//Convert the pointer array into a struct array.
				IntPtr positionPtr = analogueEventsPtr;
				for (int i = 0; i < inputState.numAnalogueEvents; i++)
				{
					analogueEvents[i] = Marshal.PtrToStructure<avs.InputEventAnalogue>(positionPtr);
					positionPtr += analogueEventSize;
					//Debug.Log("inputState analogue event "+ analogueEvents[i].inputID+" "+ analogueEvents[i].value);
				}
			}

			avs.InputEventMotion[] motionEvents = new avs.InputEventMotion[inputState.numMotionEvents];
			if (inputState.numMotionEvents != 0)
			{
				int motionEventSize = Marshal.SizeOf<avs.InputEventMotion>();

				//Convert the pointer array into a struct array.
				IntPtr positionPtr = motionEventsPtr;
				for (int i = 0; i < inputState.numMotionEvents; i++)
				{
					motionEvents[i] = Marshal.PtrToStructure<avs.InputEventMotion>(positionPtr);
					positionPtr += motionEventSize;
				}
			}

			sessionComponent.ProcessInput(boolStates, floatStates, binaryEvents, analogueEvents, motionEvents);
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

		public int maxNodesOnOverlay = 10; // Number of nodes to show on the overlay before breaking.
		public int maxLightsOnOverlay = 5; // Number of lights to show on the overlay before breaking.

		public Teleport_Head head = null;
		public Teleport_ClientspaceRoot clientspaceRoot = null;
		public Teleport_CollisionRoot collisionRoot = null;
		public Teleport_SceneCaptureComponent sceneCaptureComponent = null;
		public AudioSource inputAudioSource = null;
		public Vector3 bodyOffsetFromHead = default;

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

		private Dictionary<int, Teleport_Controller> controllerLookup = new Dictionary<int, Teleport_Controller>();

		private bool resetOrigin = true;
		private UInt64 originValidCounter = 1;

		private Vector3 last_sent_origin = new Vector3(0, 0, 0);
		private Vector3 last_received_origin = new Vector3(0, 0, 0);
		private Vector3 last_received_headPos = new Vector3(0, 0, 0);

		private avs.NetworkStats networkStats;
		private avs.VideoEncoderStats videoEncoderStats;

		private void Awake()
		{
			//Add Teleport_Streamable component to all player body parts.
			List<GameObject> playerBodyParts = GetPlayerBodyParts();
			foreach (GameObject bodyPart in playerBodyParts)
			{
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
			sceneCaptureComponent.SetClientID(clientID);
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

				//Send animation control updates for the grip animation of the controllers.
				foreach (Teleport_Controller controller in controllerLookup.Values)
				{
					if(!controller.controllerModel)
						continue;
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
					// TODO: Convert the following to work outside of editor.
					#if UNITY_EDITOR
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
						#endif
					}
				}
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

		public void ProcessInput(byte[] booleanStates,float[] floatStates, avs.InputEventBinary[] binaryEvents, avs.InputEventAnalogue[] analogueEvents, avs.InputEventMotion[] motionEvents)
		{
			_input.ProcessInputEvents(booleanStates,floatStates,binaryEvents, analogueEvents, motionEvents);
		}

		public void SetControllerPose(uid id, Quaternion newRotation, Vector3 newPosition, Vector3 velocity, Vector3 angularVelocity)
		{
			GeometrySource geometrySource = GeometrySource.GetGeometrySource();
			if (!geometrySource.GetSessionNodes().TryGetValue(id, out GameObject gameObject))
			{
				return;
			}
			var streamable= gameObject.GetComponent<Teleport_Streamable>();
			if (!streamable)
			{
				Debug.LogError("Trying to set pose of controlled object "+gameObject.name+" that has no Teleport_Streamable.");
				return;
			}
			if (streamable.OwnerClient != clientID)
			{
				Debug.LogError("Trying to set pose of controlled object " + gameObject.name + " whose owner client " + streamable.OwnerClient+" is not the client ID "+clientID.ToString()+".");
				return;
			}
			gameObject.transform.localPosition=newPosition;
			gameObject.transform.localRotation=newRotation;
			streamable.stageSpaceVelocity=velocity;
			streamable.stageSpaceAngularVelocity=angularVelocity;
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

		public void ShowOverlay(int x, int y, GUIStyle font)
		{
			Vector3 headPosition = head ? head.transform.position : default;
			Vector3 originPosition = clientspaceRoot ? clientspaceRoot.transform.position : default;

			int lineHeight = 14;
			GUI.Label(new Rect(x, y += lineHeight, 300, 20), string.Format("Client uid {0} {1}", clientID, GetClientIP()), font);

			//Add a break for readability.
			y += lineHeight;

			GUI.Label(new Rect(x, y += lineHeight, 300, 20), string.Format("available bandwidth\t{0:F3} mb/s", networkStats.bandwidth), font);
			GUI.Label(new Rect(x, y += lineHeight, 300, 20), string.Format("avg bandwidth used\t{0:F3} mb/s", networkStats.avgBandwidthUsed), font);
			GUI.Label(new Rect(x, y += lineHeight, 300, 20), string.Format("max bandwidth used\t{0:F3} mb/s", networkStats.maxBandwidthUsed), font);

			y += lineHeight;

			GUI.Label(new Rect(x, y += lineHeight, 300, 20), string.Format("video frames submitted per sec\t{0:F3}", videoEncoderStats.framesSubmittedPerSec), font);
			GUI.Label(new Rect(x, y += lineHeight, 300, 20), string.Format("video frames encoded per sec\t{0:F3}", videoEncoderStats.framesEncodedPerSec), font);

			//Add a break for readability.
			y += lineHeight;

			GUI.Label(new Rect(x, y += lineHeight, 300, 20), string.Format("origin pos\t{0}", FormatVectorString(originPosition)), font);
			GUI.Label(new Rect(x, y += lineHeight, 300, 20), string.Format("sent origin\t{0}", FormatVectorString(last_sent_origin)), font);
			GUI.Label(new Rect(x, y += lineHeight, 300, 20), string.Format("received origin\t{0}", FormatVectorString(last_received_origin)), font);
			GUI.Label(new Rect(x, y += lineHeight, 300, 20), string.Format("received head\t{0}", FormatVectorString(last_received_headPos)), font);
			GUI.Label(new Rect(x, y += lineHeight, 300, 20), string.Format("head position\t{0}", FormatVectorString(headPosition)), font);

			//Add a break for readability.
			y += lineHeight;

			foreach (Teleport_Controller controller in controllerLookup.Values)
			{
				GUI.Label(new Rect(x, y += lineHeight, 300, 20), string.Format("Controller {0}, {1}", controller.Index, FormatVectorString(controller.transform.position)));

		
			}

			if (geometryStreamingService != null)
			{
				GeometrySource geometrySource = GeometrySource.GetGeometrySource();

				//Add a break for readability.
				y += lineHeight;

				//Display Count of lights.
				int lightCount = geometryStreamingService.GetStreamedLightCount();
				GUI.Label(new Rect(x, y += lineHeight, 300, 20), string.Format("Lights {0}", lightCount));

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

					//Break if we have displayed the maximum Count of lights.
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
			head = GetSingleComponentFromChildren<Teleport_Head>();
			clientspaceRoot = GetSingleComponentFromChildren<Teleport_ClientspaceRoot>();
			collisionRoot = GetSingleComponentFromChildren<Teleport_CollisionRoot>();
			sceneCaptureComponent = GetSingleComponentFromChildren<Teleport_SceneCaptureComponent>();
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
				controller.session = this;
				if (controllerLookup.TryGetValue((int)controller.Index,out var value) )
				{
					Debug.LogError("Controllers have the same index");
				}
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
			sessions[clientID] = this;

			UpdateClientSettings();
			if (geometryStreamingService == null)
				geometryStreamingService = new GeometryStreamingService(this);
			if (geometryStreamingService!=null&&teleportSettings.serverSettings.isStreamingGeometry)
			{
				geometryStreamingService.StreamGlobals();
			}
			// We must have a root node for the player's client space.
			geometryStreamingService.SetNodePosePath(clientspaceRoot.gameObject, "root");
			foreach (Teleport_Controller controller in controllerLookup.Values)
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
			sceneCaptureComponent.SetClientID(clientID);
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
			clientDynamicLighting.lightingMode=LightingMode.TEXTURE;
		}
		private void UpdateClientSettings()
		{
			teleportSettings = TeleportSettings.GetOrCreateSettings();
			var settings = teleportSettings.serverSettings;
			clientSettings.shadowmapSize = teleportSettings.serverSettings.defaultShadowmapSize;

			clientSettings.captureCubeTextureSize=teleportSettings.serverSettings.defaultCaptureCubeTextureSize;
			clientSettings.backgroundMode = teleportSettings.serverSettings.backgroundMode;
			clientSettings.backgroundColour = teleportSettings.serverSettings.BackgroundColour;
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
			clientSettings.bodyOffsetFromHead= bodyOffsetFromHead;

			avs.VideoEncodeCapabilities videoEncodeCapabilities = VideoEncoder.GetEncodeCapabilities();
			if (clientSettings.videoTextureSize.x < videoEncodeCapabilities.minWidth || clientSettings.videoTextureSize.x > videoEncodeCapabilities.maxWidth
				|| clientSettings.videoTextureSize.y < videoEncodeCapabilities.minHeight || clientSettings.videoTextureSize.y > videoEncodeCapabilities.maxHeight)
			{
				Debug.LogError("The video encoder does not support the video texture dimensions.");
			}

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
			if (clientspaceRoot != null)
			{
				Teleport_Streamable streamable =clientspaceRoot.gameObject.GetComponent<Teleport_Streamable> ();
				if(streamable!=null)
					origin_uid=streamable.GetUid();
			}
			if (teleportSettings.serverSettings.controlModel == teleport.ControlModel.CLIENT_ORIGIN_SERVER_GRAVITY)
			{
				if (head != null && clientspaceRoot != null)
				{
					if (!Client_HasOrigin(clientID) || resetOrigin)
					{
						originValidCounter++;
						if (Client_SetOrigin(clientID, originValidCounter, origin_uid, clientspaceRoot.transform.position, clientspaceRoot.transform.rotation))
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
						if (Client_SetOrigin(clientID, originValidCounter, origin_uid, clientspaceRoot.transform.position, clientspaceRoot.transform.rotation))
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
			else if (teleportSettings.serverSettings.controlModel == teleport.ControlModel.SERVER_ORIGIN_CLIENT_LOCAL)
			{
				if (head != null && clientspaceRoot != null)
				{
					if (!Client_HasOrigin(clientID) || resetOrigin || clientspaceRoot.transform.hasChanged)
					{
						originValidCounter++;
						if (Client_SetOrigin(clientID, originValidCounter, origin_uid, clientspaceRoot.transform.position, clientspaceRoot.transform.rotation))
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

		//Returns list of body part hierarchy root GameObjects. 
		public List<GameObject> GetPlayerBodyParts()
		{
			List<GameObject> bodyParts = new List<GameObject>();

			var c=GetComponentsInChildren<Teleport_Streamable>();
			foreach (var o in c)
			{
				bodyParts.Add(o.gameObject);
			}
			return bodyParts;
		}
	}
}