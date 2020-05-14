﻿using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using System.Collections.Generic;
using uid = System.UInt64;

namespace teleport
{
	public class CasterMonitor : MonoBehaviour
	{
		public SCServer.CasterSettings casterSettings = new SCServer.CasterSettings();
		
		// Reference to the global (per-project) TeleportSettings asset.
		private TeleportSettings teleportSettings= null;

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
		delegate void OnShowActor(uid clientID, in IntPtr actorPtr);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnHideActor(uid clientID, in IntPtr actorPtr);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnSetHeadPose(uid clientID, in avs.HeadPose newHeadPose);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnSetControllerPose(uid clientID, int index, in avs.HeadPose newHeadPose);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnNewInput(uid clientID, in avs.InputState newInput);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnDisconnect(uid clientID);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnMessageHandler(avs.LogSeverity Severity, string Msg, in IntPtr userData);
		#endregion

		#region DLLImport

		struct InitializeState
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
		};
		[DllImport("SimulCasterServer")]
		private static extern bool Initialise(InitializeState initializeState);
		[DllImport("SimulCasterServer")]
		private static extern void UpdateCasterSettings(SCServer.CasterSettings newSettings);

		[DllImport("SimulCasterServer")]
		private static extern void SetClientPosition(uid clientID, Vector3 pos);
		[DllImport("SimulCasterServer")]
		private static extern void Tick(float deltaTime);
		[DllImport("SimulCasterServer")]
		private static extern void Shutdown();
		#endregion

		private GUIStyle overlayFont = new GUIStyle();
		public static CasterMonitor GetCasterMonitor()
		{
			// We only want one instance, so delete duplicates.
			if (instance == null)
			{
				instance = FindObjectOfType<CasterMonitor>();
			}
			if (Application.isPlaying)
				return instance;
			else
				return null;
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
			overlayFont.normal.textColor = Color.yellow;
			overlayFont.fontSize = 14;
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
			InitializeState initializeState=new InitializeState();
			initializeState.showActor = CasterMonitor.ShowActor;
			initializeState.hideActor = HideActor;
			initializeState.headPoseSetter = Teleport_SessionComponent.StaticSetHeadPose;
			initializeState.controllerPoseSetter = Teleport_SessionComponent.StaticSetControllerPose;
			initializeState.newInputProcessing = Teleport_SessionComponent.StaticProcessInput;
			initializeState.disconnect = Teleport_SessionComponent.StaticDisconnect;
			initializeState.messageHandler=LogMessageHandler;
			initializeState.SERVICE_PORT = teleportSettings.listenPort;
			initializeState.DISCOVERY_PORT = teleportSettings.discoveryPort;
			ok=Initialise(initializeState);
		}
		List<Teleport_SessionComponent> sessions=new List<Teleport_SessionComponent>();
		public void AddSession(Teleport_SessionComponent s)
		{
			if(sessions.Contains(s))
				return;
			sessions.Add(s);
		}
		private void Update()
		{
			if(ok)
				Tick(Time.deltaTime);
		}
		void OnGUI()
		{
			GUI.Label(new Rect(10, 10, 100, 20), "Teleport", overlayFont);
			int x = 10;
			int y = 20;
			foreach(var s in sessions)
			{
				s.ShowOverlay(x,y, overlayFont);
			}
		}


		private void OnValidate()
		{
			if(Application.isPlaying)
			{
				UpdateCasterSettings(casterSettings);
			}
		}

		private void OnDisable()
		{
			sessions.Clear();
			Shutdown();
		}

		private static void ShowActor(uid clientID, in IntPtr actorPtr)
		{
			GameObject actor = (GameObject)GCHandle.FromIntPtr(actorPtr).Target;

			Renderer actorRenderer = actor.GetComponent<Renderer>();
			actorRenderer.enabled = true;
		}

		public static void HideActor(uid clientID,in IntPtr actorPtr)
		{
			GameObject actor = (GameObject)GCHandle.FromIntPtr(actorPtr).Target;

			Renderer actorRenderer = actor.GetComponent<Renderer>();
			//actorRenderer.enabled = false;
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