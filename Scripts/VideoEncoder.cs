using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

using uid = System.UInt64;

namespace teleport
{
	public class VideoEncoder
	{
		#region DLLImports
		[DllImport("SimulCasterServer")]
		static extern System.IntPtr GetRenderEventWithDataCallback();
		#endregion

		[StructLayout(LayoutKind.Sequential)]
		struct EncodeVideoParamsWrapper
		{
			public uid clientID;
			public SCServer.VideoEncodeParams videoEncodeParams;
		};

		[StructLayout(LayoutKind.Sequential)]
		public struct SceneCapture2DTagDataWrapper
		{
			public uid clientID;
			public UInt64 dataSize;
			public avs.SceneCapture2DTagData data;
		};

		[StructLayout(LayoutKind.Sequential)]
		public struct SceneCaptureCubeTagDataWrapper
		{
			public uid clientID;
			public UInt64 dataSize;
			public avs.SceneCaptureCubeTagData data;
		};

		//Stores handles to game objects, so the garbage collector doesn't move/delete the objects while they're being referenced by the native plug-in.
		Dictionary<GameObject, GCHandle> gameObjectHandles = new Dictionary<GameObject, GCHandle>();

		uid clientID;

		CasterMonitor monitor;


		CommandBuffer commandBuffer = null;

		bool initalized = false;
		bool _reconfigure = false;


		public bool Reconfigure
		{
			set; get;
		}

		public VideoEncoder(uid clientID)
		{
			this.clientID = clientID;

			monitor = CasterMonitor.GetCasterMonitor();
		}

		public void CreateEncodeCommands(ScriptableRenderContext context, Camera camera, UInt32 tagDataID = 0)
		{
			commandBuffer = new CommandBuffer();
			commandBuffer.name = "Video Encoder " + clientID;

			ConfigureEncoder(camera);

			CreateEncodeCommand(camera, tagDataID);

			context.ExecuteCommandBuffer(commandBuffer);
			ReleaseCommandbuffer(camera);
			context.Submit();
		}

		void ConfigureEncoder(Camera camera)
		{
			if (initalized && !_reconfigure)
			{
				return;
			}

			var paramsWrapper = new EncodeVideoParamsWrapper();
			paramsWrapper.clientID = clientID;
			paramsWrapper.videoEncodeParams = new SCServer.VideoEncodeParams();
			var teleportSettings = TeleportSettings.GetOrCreateSettings();
			if (teleportSettings.casterSettings.usePerspectiveRendering)
			{
				paramsWrapper.videoEncodeParams.encodeWidth = teleportSettings.casterSettings.sceneCaptureWidth;
				paramsWrapper.videoEncodeParams.encodeHeight = teleportSettings.casterSettings.sceneCaptureHeight;
			}
			else
			{
				paramsWrapper.videoEncodeParams.encodeWidth = paramsWrapper.videoEncodeParams.encodeHeight = (int)teleportSettings.casterSettings.captureCubeTextureSize * 3;
			}

			switch (SystemInfo.graphicsDeviceType)
			{
				case (GraphicsDeviceType.Direct3D11):
					paramsWrapper.videoEncodeParams.deviceType = SCServer.GraphicsDeviceType.Direct3D11;
					break;
				case (GraphicsDeviceType.Direct3D12):
					paramsWrapper.videoEncodeParams.deviceType = SCServer.GraphicsDeviceType.Direct3D12; // May not work if device not created with shared heap flag in Unity source
					break;
				case (GraphicsDeviceType.OpenGLCore):
					paramsWrapper.videoEncodeParams.deviceType = SCServer.GraphicsDeviceType.OpenGL; // Needs to be supported
					break;
				case (GraphicsDeviceType.Vulkan):
					paramsWrapper.videoEncodeParams.deviceType = SCServer.GraphicsDeviceType.Vulkan; // Needs to be supported
					break;
				default:
					Debug.Log("Graphics api not supported");
					return;
			}

			var encoderTexture = Teleport_SceneCaptureComponent.RenderingSceneCapture.videoTexture;
			// deviceHandle set in dll
			paramsWrapper.videoEncodeParams.inputSurfaceResource = encoderTexture.GetNativeTexturePtr();
			IntPtr paramsWrapperPtr = Marshal.AllocHGlobal(Marshal.SizeOf(new EncodeVideoParamsWrapper()));
			Marshal.StructureToPtr(paramsWrapper, paramsWrapperPtr, true);

			if (!initalized)
			{
				commandBuffer.IssuePluginEventAndData(GetRenderEventWithDataCallback(), 0, paramsWrapperPtr);
				initalized = true;
			}
			else
			{
				commandBuffer.IssuePluginEventAndData(GetRenderEventWithDataCallback(), 1, paramsWrapperPtr);
			}
			_reconfigure = false;
		}

		void CreateEncodeCommand(Camera camera, UInt32 tagDataID)
		{
			if (!initalized || _reconfigure)
			{
				return;
			}

			IntPtr ptr;
			CreateTagDataWarapper(camera, tagDataID, out ptr);
			commandBuffer.IssuePluginEventAndData(GetRenderEventWithDataCallback(), 2, ptr);
		}

		void CreateTagDataWarapper(Camera camera, UInt32 tagDataID, out IntPtr dataPtr)
		{
			var teleportSettings = TeleportSettings.GetOrCreateSettings();
			if (teleportSettings.casterSettings.usePerspectiveRendering)
			{
				var w = new SceneCapture2DTagDataWrapper();
				w.clientID = clientID;
				w.dataSize = (UInt64)Marshal.SizeOf(typeof(avs.SceneCapture2DTagData));
				w.data = new avs.SceneCapture2DTagData();
				w.data.id = tagDataID;
				w.data.cameraTransform = new avs.Transform();
				w.data.cameraTransform.position = camera.transform.position;
				w.data.cameraTransform.rotation = camera.transform.parent.rotation;
				w.data.cameraTransform.scale = new avs.Vector3(1, 1, 1);

				dataPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SceneCapture2DTagDataWrapper)));
				Marshal.StructureToPtr(w, dataPtr, true);
			}
			else
			{
				List<avs.LightData> lightDataList;
				CreateLightingData(camera, out lightDataList);
				var lightSizeInBytes = Marshal.SizeOf(typeof(avs.LightData)) * lightDataList.Count;

				var w = new SceneCaptureCubeTagDataWrapper();
				w.clientID = clientID;
				w.dataSize = (UInt64)(Marshal.SizeOf(typeof(avs.SceneCaptureCubeTagData)) + lightSizeInBytes);
				w.data = new avs.SceneCaptureCubeTagData();
				w.data.id = tagDataID;
				w.data.cameraTransform = new avs.Transform();
				w.data.cameraTransform.position = camera.transform.position;
				w.data.cameraTransform.rotation = camera.transform.rotation;
				w.data.cameraTransform.scale = new avs.Vector3(1, 1, 1);
				w.data.lightCount = (uint)lightDataList.Count;
				w.data.lights = lightDataList.ToArray();

				var wrapperSize = Marshal.SizeOf(typeof(SceneCaptureCubeTagDataWrapper)) + lightSizeInBytes;
				dataPtr = Marshal.AllocHGlobal(wrapperSize);
				Marshal.StructureToPtr(w, dataPtr, true);
			}
		}

		void CreateLightingData(Camera camera, out List<avs.LightData> lightDataList)
		{
			lightDataList = new List<avs.LightData>();
			foreach (var keyVal in TeleportLighting.perFrameLightProperties)
			{
				var p = keyVal.Value;
				PerFramePerCameraLightProperties clp;

				// check if this light belongs to this camera
				if (!p.perFramePerCameraLightProperties.TryGetValue(camera, out clp))
				{
					continue;
				}

				var lightData = new avs.LightData();
				ref var l = ref p.visibleLight;
				lightData.worldTransform = l.localToWorldMatrix;
				lightData.color = new avs.Vector4(l.light.color.r, l.light.color.g, l.light.color.b, l.light.color.a);
				lightData.range = l.range;
				lightData.spotAngle = l.spotAngle;
				lightData.lightType = l.lightType;
				lightData.shadowViewMatrix = clp.cascades[0].viewMatrix;
				lightData.shadowProjectionMatrix = clp.cascades[0].projectionMatrix;

				lightDataList.Add(lightData);
			}
		}

		void ReleaseCommandbuffer(Camera camera)
		{
			if (commandBuffer != null)
			{
				commandBuffer.Release();
			}
		}

		public void Shutdown()
		{

		}

	}
}
