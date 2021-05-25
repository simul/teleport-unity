﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using avs;
using uid = System.UInt64;

namespace teleport
{
	public class VideoEncoder
	{
		#region DLLImports
		[DllImport("SimulCasterServer")]
		static extern System.IntPtr GetRenderEventWithDataCallback();
		#endregion
		[DllImport("SimulCasterServer")]
		public static extern void ConvertTransform(avs.AxesStandard fromStandard, avs.AxesStandard toStandard, ref avs.Transform transform);
		[DllImport("SimulCasterServer")]
		public static extern void ConvertRotation(avs.AxesStandard fromStandard, avs.AxesStandard toStandard, ref avs.Vector4 rotation);
		[DllImport("SimulCasterServer")]
		public static extern void ConvertPosition(avs.AxesStandard fromStandard, avs.AxesStandard toStandard, ref avs.Vector3 position);
		[DllImport("SimulCasterServer")]
		public static extern void ConvertScale(avs.AxesStandard fromStandard, avs.AxesStandard toStandard, ref avs.Vector3 scale);
		[DllImport("SimulCasterServer")]
		public static extern byte ConvertAxis(avs.AxesStandard fromStandard, avs.AxesStandard toStandard, ref byte axis);

		[StructLayout(LayoutKind.Sequential)]
		struct EncodeVideoParamsWrapper
		{
			public uid clientID;
			public SCServer.VideoEncodeParams videoEncodeParams;
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
		int lightWarnCount = 1;


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
				paramsWrapper.videoEncodeParams.encodeWidth = teleportSettings.casterSettings.perspectiveWidth;
				paramsWrapper.videoEncodeParams.encodeHeight = (int)(teleportSettings.casterSettings.perspectiveHeight * 1.5f);
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
			CreateTagDataWrapper(camera, tagDataID, out ptr);
			commandBuffer.IssuePluginEventAndData(GetRenderEventWithDataCallback(), 2, ptr);
		}

		public SceneCaptureCubeTagDataWrapper cubeTagDataWrapper = new SceneCaptureCubeTagDataWrapper();
		void CreateTagDataWrapper(Camera camera, UInt32 tagDataID, out IntPtr dataPtr)
		{
			var teleportSettings = TeleportSettings.GetOrCreateSettings();
			
			List<avs.LightTagData> lightDataList;
			CreateLightingData(camera, out lightDataList);
			var lightSizeInBytes = Marshal.SizeOf(typeof(avs.LightTagData)) * lightDataList.Count;

			cubeTagDataWrapper.clientID = clientID;
			cubeTagDataWrapper.dataSize = (UInt64)(Marshal.SizeOf(typeof(avs.SceneCaptureCubeTagData)) + lightSizeInBytes);
			cubeTagDataWrapper.data = new avs.SceneCaptureCubeTagData();
			cubeTagDataWrapper.data.timestamp = CasterMonitor.GetUnixTimestamp();
			cubeTagDataWrapper.data.id = tagDataID;
			cubeTagDataWrapper.data.cameraTransform = new avs.Transform();
			cubeTagDataWrapper.data.cameraTransform.position = camera.transform.position;
			cubeTagDataWrapper.data.cameraTransform.rotation = camera.transform.rotation;
			cubeTagDataWrapper.data.cameraTransform.scale = new avs.Vector3(1, 1, 1);
			cubeTagDataWrapper.data.lightCount = (uint)lightDataList.Count;

			// If this doesn't work, do Array.copy
			cubeTagDataWrapper.data.lights = lightDataList.ToArray();

			var wrapperSize = Marshal.SizeOf(typeof(SceneCaptureCubeTagDataWrapper)) + lightSizeInBytes;
			dataPtr = Marshal.AllocHGlobal(wrapperSize);
			Marshal.StructureToPtr(cubeTagDataWrapper, dataPtr, true);
		}

		void CreateLightingData(Camera camera, out List<avs.LightTagData> lightDataList)
		{
			var textureScaleAndBias = Matrix4x4.identity;
			textureScaleAndBias.m22 = 0.5f;
			textureScaleAndBias.m23 = 0.0f;

			lightDataList = new List<avs.LightTagData>();
			// which lights are streamed in this session?
			var session = Teleport_SessionComponent.sessions[clientID];
		
			var streamedLights=session.GeometryStreamingService.GetStreamedLights();
			if(streamedLights.Count>session.handshake.maxLightsSupported)
			{
				if(lightWarnCount > 0)
				{
					Debug.LogWarning($"Can't support all lights! The scene has {streamedLights.Count} lights, but the client only supports {session.handshake.maxLightsSupported}!");
					lightWarnCount--;
				}
			}
			foreach(var l in streamedLights)
			{
				var uid = l.Key;
				var light = l.Value;
				if (light == null)
				{
					continue;
				}
				if(lightDataList.Count>= session.handshake.maxLightsSupported)
					break;
				PerFrameLightProperties perFrameLightProperties = null;
				PerFramePerCameraLightProperties perFramePerCameraLightProperties=null;
				var lightData = new avs.LightTagData();
				if (TeleportLighting.perFrameLightProperties.TryGetValue(light, out perFrameLightProperties)&&perFrameLightProperties != null)
				{
					perFrameLightProperties.perFramePerCameraLightProperties.TryGetValue(camera, out perFramePerCameraLightProperties);
					lightData.worldTransform = perFrameLightProperties.visibleLight.localToWorldMatrix;
				}
				var clr=light.intensity* light.color.linear;
				lightData.color = new avs.Vector4(clr.r, clr.g, clr.b, clr.a);
				lightData.range = light.range;// visibleLight.range; 
				lightData.spotAngle = light.spotAngle;// visibleLight.spotAngle;
				lightData.lightType = DataTypes.UnityToTeleport(light.type);
				lightData.position = light.transform.position;
				// We want here the ORIGIN of the shadow matrix, not the light's "position", which is irrelevant for directional lights.
				if ( perFramePerCameraLightProperties != null)
				{
					if(lightData.lightType == avs.LightType.Directional)
						lightData.position = perFramePerCameraLightProperties.cascades[0].viewMatrix.inverse.GetPosition();
					var viewMatrix= perFramePerCameraLightProperties.cascades[0].viewMatrix;
					//DataTypes.ConvertViewMatrrix(session.axesStandard, ref viewMatrix);
					lightData.worldToShadowMatrix = ShadowUtils.GetShadowTransformForRender(
																			perFramePerCameraLightProperties.cascades[0].projectionMatrix
																			, viewMatrix);
				}
				else
				{
					if (perFrameLightProperties != null)
					{
						lightData.worldToShadowMatrix = perFrameLightProperties.worldToLightMatrix;
						//DataTypes.ConvertViewMatrrix(session.axesStandard, ref lightData.worldToShadowMatrix);
					}
				}
				// Unity lights shine in the z direction...
				// viewMatrix no good because Unity has view matrices that are not rotational!
				Quaternion rotation = light.transform.rotation;
				Quaternion Z_to_Y	= new Quaternion(0.707F, 0, 0, 0.707F);
				rotation =  rotation* Z_to_Y;
				rotation.Normalize();
				lightData.orientation = rotation;
				// Apply texture scale and offset to save a MAD in shader.
				if (perFramePerCameraLightProperties != null)
				{
					Matrix4x4 proj = textureScaleAndBias * perFramePerCameraLightProperties.cascades[0].projectionMatrix;
					lightData.shadowProjectionMatrix = proj.transpose;
				}
				DataTypes.ConvertViewProjectionMatrix(session.axesStandard,ref lightData.worldToShadowMatrix);
				if (perFrameLightProperties != null)
				{
					lightData.texturePosition = perFrameLightProperties.texturePosition;
					lightData.textureSize = perFrameLightProperties.sizeOnTexture;
				}
				lightData.worldTransform = light.transform.localToWorldMatrix;
				// doesn't matter, we don't use it.
				//ConvertTransform(avs.AxesStandard.UnityStyle, session.axesStandard, ref lightData.worldTransform);
				lightData.uid = uid;
				ConvertPosition(avs.AxesStandard.UnityStyle, session.axesStandard, ref lightData.position);
				ConvertRotation(avs.AxesStandard.UnityStyle, session.axesStandard, ref lightData.orientation);

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
