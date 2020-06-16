using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

using uid = System.UInt64;
using teleport;

namespace teleport
{
	public class TeleportRenderPipeline : RenderPipeline
	{
		bool useDynamicBatching = false;
		bool useGPUInstancing = false;

		public const string CUBEMAP_CAM_PREFIX = "TeleportCubemapCam";

		Dictionary<Camera,TeleportCameraRenderer> renderers = new Dictionary<Camera, TeleportCameraRenderer>();
		TeleportRenderSettings renderSettings = null;
		public TeleportRenderPipeline(TeleportRenderSettings renderSettings, bool useDynamicBatching = true, bool useGPUInstancing = true, bool useSRPBatcher = true)
		{
			this.renderSettings = renderSettings;
			this.useDynamicBatching = useDynamicBatching;
			this.useGPUInstancing = useGPUInstancing;
			GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
			GraphicsSettings.lightsUseLinearIntensity = true;
		}
		TeleportCameraRenderer AddRenderer(Camera c)
		{
			TeleportCameraRenderer renderer = new TeleportCameraRenderer();
			renderers.Add(c, renderer);
			renderer = renderers[c];
			renderer.renderSettings = renderSettings;
			return renderer;
		}
		TeleportCameraRenderer GetTeleportCameraRenderer(Camera c)
		{
			TeleportCameraRenderer renderer = null;
			if (renderers.TryGetValue(c,out renderer))
			{
				return renderer;
			}
			return AddRenderer(c);
		}
		public struct LightingOrder
		{
			public int MainLightIndex;
			public int SecondLightIndex;
			public int NumUnimportantLights;
		};

		// Main Light is always a directional light
		static public LightingOrder GetLightingOrder(NativeArray<VisibleLight> visibleLights)
		{
			LightingOrder lightingOrder = new LightingOrder();
			lightingOrder.MainLightIndex = -1;
			lightingOrder.SecondLightIndex = -1;
			lightingOrder.NumUnimportantLights = 0;
			int totalVisibleLights = visibleLights.Length;

			if (totalVisibleLights == 0)
				return lightingOrder;

			Light sunLight = RenderSettings.sun;
			int brightestDirectionalLightIndex = -1;
			int secondBrightestDirectionalLightIndex = -1;
			float brightestLightIntensity = 0.0f;
			for (int i = 0; i < totalVisibleLights; ++i)
			{
				VisibleLight currVisibleLight = visibleLights[i];
				Light currLight = currVisibleLight.light;

				// Particle system lights have the light property as null. We sort lights so all particles lights
				// come last. Therefore, if first light is particle light then all lights are particle lights.
				// In this case we either have no main light or already found it.
				if (currLight == null)
				{
					break;
				}
				if (currLight == sunLight)
				{
					brightestDirectionalLightIndex = i;
				}

				// In case no shadow light is present we will return the brightest directional light
				if (currVisibleLight.lightType == LightType.Directional && currLight.intensity > brightestLightIntensity)
				{
					brightestLightIntensity = currLight.intensity;
					brightestDirectionalLightIndex = i;
				}
			}
			float secondBrightestLightIntensity = 0.0f;

			for (int i = 0; i < totalVisibleLights; ++i)
			{
				if (i == brightestDirectionalLightIndex)
					continue;
				VisibleLight currVisibleLight = visibleLights[i];
				Light currLight = currVisibleLight.light;
				if (currLight == null)
				{
					break;
				}
				// In case no shadow light is present we will return the brightest directional light
				if (currLight.intensity > secondBrightestLightIntensity)
				{
					secondBrightestLightIntensity = currLight.intensity;
					secondBrightestDirectionalLightIndex = i;
				}
			}

			lightingOrder.NumUnimportantLights = totalVisibleLights - (brightestDirectionalLightIndex >= 0 ? 1 : 0) - (secondBrightestDirectionalLightIndex >= 0 ? 1 : 0);
			if (lightingOrder.NumUnimportantLights > 4)
				lightingOrder.NumUnimportantLights = 4;
			lightingOrder.MainLightIndex = brightestDirectionalLightIndex;
			lightingOrder.SecondLightIndex = secondBrightestDirectionalLightIndex;
			return lightingOrder;
		}
		protected override void Render(ScriptableRenderContext context, Camera[] cameras)
		{
			foreach (var camera in cameras)
			{
				Render(context, camera);
			}
		}
		Matrix4x4 viewmat = new Matrix4x4();
		void Render(ScriptableRenderContext context, Camera camera)
		{
			TeleportCameraRenderer renderer = GetTeleportCameraRenderer(camera);
			var sc = camera.gameObject.GetComponent<Teleport_SceneCaptureComponent>();
			if (sc != null)
			{
				renderer.RenderToSceneCapture(context, camera);
			}
			else
			{
				renderer.Render(context, camera);
				viewmat = camera.worldToCameraMatrix;
			}
		}
	}
}