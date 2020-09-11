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
			public int[] AdditionalLightIndices;
			public int NumUnimportantLights;
		};

		// Main Light is always a directional light
		static public LightingOrder GetLightingOrder(CullingResults cullingResults)
		{
			LightingOrder lightingOrder = new LightingOrder();
			lightingOrder.MainLightIndex = -1;
			List<int> AdditionalLightIndices = new List<int>();
			lightingOrder.NumUnimportantLights = 0;
			int totalVisibleLights = cullingResults.visibleLights.Length;

			if (totalVisibleLights == 0)
				return lightingOrder;

			Light sunLight = RenderSettings.sun;
			int brightestDirectionalLightIndex = -1;
			float brightestLightIntensity = 0.0f;
			for (int i = 0; i < totalVisibleLights; ++i)
			{
				VisibleLight currVisibleLight = cullingResults.visibleLights[i];
				Bounds outBounds = new Bounds();
				bool ok = cullingResults.GetShadowCasterBounds(i, out outBounds);
				if (!ok)
					continue;
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
			// If we have found NO directional light with shadows, we can use one without.
			for (int i = 0; i < totalVisibleLights; ++i)
			{
				VisibleLight currVisibleLight = cullingResults.visibleLights[i];
				Light currLight = currVisibleLight.light;
				if (currLight == null)
					break;
				// In case no shadow light is present we will return the brightest directional light
				if (currLight == sunLight||currVisibleLight.lightType == LightType.Directional && currLight.intensity > brightestLightIntensity)
				{
					brightestLightIntensity = currLight.intensity;
					brightestDirectionalLightIndex = i;
				}
			}
			for (int i = 0; i < totalVisibleLights; ++i)
			{
				if (i == brightestDirectionalLightIndex)
					continue;
				VisibleLight currVisibleLight = cullingResults.visibleLights[i];
				Bounds outBounds= new Bounds();
				bool ok=cullingResults.GetShadowCasterBounds(i, out outBounds);
				if (!ok)
					continue;
				Light currLight = currVisibleLight.light;
				if (currLight == null)
				{
					break;
				}
				AdditionalLightIndices.Add(i);
			}

			lightingOrder.NumUnimportantLights = totalVisibleLights - (brightestDirectionalLightIndex >= 0 ? 1 : 0) - AdditionalLightIndices.Count;
			if (lightingOrder.NumUnimportantLights > 4)
				lightingOrder.NumUnimportantLights = 4;
			lightingOrder.MainLightIndex = brightestDirectionalLightIndex;
			lightingOrder.AdditionalLightIndices = AdditionalLightIndices.ToArray();
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