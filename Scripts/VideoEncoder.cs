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
		[DllImport("SimulCasterServer")]
        static extern void ClearVideoTagData(uid clientID, uint tagDataID);
        [DllImport("SimulCasterServer")]
        static extern void AddVideoTagData(uid clientID, uint tagDataID, IntPtr data, UInt64 dataSize);
		#endregion

		[StructLayout(LayoutKind.Sequential)]
		struct EncodeVideoParamsWrapper
		{
			public uid clientID;
			public SCServer.VideoEncodeParams videoEncodeParams;
		};

		[StructLayout(LayoutKind.Sequential)]
		public struct ClientIDWrapper
		{
			public uid clientID;
            public uint tagDataID;
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

			// Send the data to c++ plugin to be cached there (will be sent to client with video frame)
			SendTagData(camera, tagDataID);

			var wrapper = new ClientIDWrapper();
			wrapper.clientID = clientID;
            wrapper.tagDataID = tagDataID;

			IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(new ClientIDWrapper()));
			Marshal.StructureToPtr(wrapper, ptr, true);

			commandBuffer.IssuePluginEventAndData(GetRenderEventWithDataCallback(), 2, ptr);
		}

		void SendTagData(Camera camera, UInt32 tagDataID)
		{
            ClearVideoTagData(clientID, tagDataID);

			var teleportSettings = TeleportSettings.GetOrCreateSettings();
			if (teleportSettings.casterSettings.usePerspectiveRendering)
			{
				var tagDataSize = (UInt64)Marshal.SizeOf(typeof(avs.SceneCapture2DTagData));
				avs.SceneCapture2DTagData tagData = new avs.SceneCapture2DTagData();
				tagData.id = tagDataID;
				tagData.cameraTransform = new avs.Transform();
				tagData.cameraTransform.position = camera.transform.position;
				tagData.cameraTransform.rotation = camera.transform.parent.rotation;
				tagData.cameraTransform.scale = new avs.Vector3(1, 1, 1);

				IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(new avs.SceneCapture2DTagData()));
				Marshal.StructureToPtr(tagData, ptr, true);

                AddVideoTagData(clientID, tagDataID, ptr, tagDataSize);

				Marshal.FreeHGlobal(ptr);
			}
			else
			{
				var tagData = new avs.SceneCaptureCubeTagData();
				tagData.id = tagDataID;
				tagData.cameraTransform = new avs.Transform();
				tagData.cameraTransform.position = camera.transform.position;
				tagData.cameraTransform.rotation = camera.transform.rotation;
				tagData.cameraTransform.scale = new avs.Vector3(1, 1, 1);

				List<avs.LightData> lightDataList;
				CreateLightingData(camera, out lightDataList);

				tagData.lightCount = (uint)lightDataList.Count;

				IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(new avs.SceneCaptureCubeTagData()));
				Marshal.StructureToPtr(tagData, ptr, true);

				var tagDataSize = (UInt64)Marshal.SizeOf(typeof(avs.SceneCaptureCubeTagData));

				// Send fixed size tag data
                AddVideoTagData(clientID, tagDataID, ptr, tagDataSize);

				Marshal.FreeHGlobal(ptr);

				tagDataSize = (UInt64)Marshal.SizeOf(typeof(avs.LightData));

				// Send light related tag data
				for (int i = 0; i < lightDataList.Count; ++i)
				{
					ptr = Marshal.AllocHGlobal(Marshal.SizeOf(new avs.LightData()));
					Marshal.StructureToPtr(lightDataList[i], ptr, true);

					// Send fixed size tag data
                    AddVideoTagData(clientID, tagDataID, ptr, tagDataSize);

					Marshal.FreeHGlobal(ptr);
				}

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
