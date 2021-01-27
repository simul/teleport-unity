using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;

namespace teleport
{
	public struct PerFrameShadowCascadeProperties
	{
		public Matrix4x4 viewMatrix;
		public Matrix4x4 projectionMatrix;
		public ShadowSplitData splitData;
	}
	public class PerFramePerCameraLightProperties
	{
		// The texture that we render shadows into for this light and camera.
		public RenderTexture screenspaceShadowTexture = null;
		// One of these for each cascade.
		public PerFrameShadowCascadeProperties[] cascades = new PerFrameShadowCascadeProperties[4];
		public Vector4 shadowFadeCenterAndType = new Vector4();
		public Matrix4x4[] WorldToShadow = new Matrix4x4[4];
	};
	public class PerFrameLightProperties
	{
		public VisibleLight visibleLight;
		public RenderTexture shadowAtlasTexture = null;
		public RenderTexture filteredShadowTexture = null; 
		public Vector2Int texturePosition = new Vector2Int();
		public int sizeOnTexture = 0;
		public Matrix4x4 worldToLightMatrix = new Matrix4x4();
		public bool AnyShadows = false;			// Any shadows this frame?
		// One of these for each cascade.
		public Dictionary<Camera, PerFramePerCameraLightProperties> perFramePerCameraLightProperties = new Dictionary<Camera, PerFramePerCameraLightProperties>();
	};
	public class TeleportLighting
	{
		public TeleportRenderSettings renderSettings = null;
		public TeleportShadows shadows = new TeleportShadows();
		public static Dictionary<Light, PerFrameLightProperties> perFrameLightProperties = new Dictionary<Light, PerFrameLightProperties>();
		const int maxUnimportantLights = 4;
		static int
			_LightColor0 = Shader.PropertyToID("_LightColor0"),
			_WorldSpaceLightPos0 = Shader.PropertyToID("_WorldSpaceLightPos0"), // dir or pos
			_LightMatrix0 = Shader.PropertyToID("_LightMatrix0"),
			_LightTexture0 = Shader.PropertyToID("_LightTexture0"),
			_LightTextureB0 = Shader.PropertyToID("_LightTextureB0"),
			unity_ProbeVolumeSH = Shader.PropertyToID("unity_ProbeVolumeSH"),
			unity_LightAtten4 = Shader.PropertyToID("unity_LightAtten0"),
			unity_LightColor0 = Shader.PropertyToID("unity_LightColor0"),
			unity_LightColor1 = Shader.PropertyToID("unity_LightColor1"),
			unity_LightColor2 = Shader.PropertyToID("unity_LightColor2"),
			unity_LightColor3 = Shader.PropertyToID("unity_LightColor3"),
			unity_LightColor = Shader.PropertyToID("unity_LightColor"),
			unity_4LightPosX0 = Shader.PropertyToID("unity_4LightPosX0"),
			unity_4LightPosY0 = Shader.PropertyToID("unity_4LightPosY0"),
			unity_4LightPosZ0 = Shader.PropertyToID("unity_4LightPosZ0"),
			//world space positions of first four non-important point lights.
			unity_4LightAtten0 = Shader.PropertyToID("unity_4LightAtten0"),//float4(ForwardBase pass only) attenuation factors of first four non-important point lights.

			// Non-important.
			//unimportant_LightColor = Shader.PropertyToID("unity_LightColor"),// half4[4]    (ForwardBase pass only) colors of of first four non-important point lights.
			unimportant_LightPosition = Shader.PropertyToID("unity_LightPosition"),
			unimportant_LightAtten = Shader.PropertyToID("unity_LightAtten"),
			unimportant_SpotDirection = Shader.PropertyToID("unity_SpotDirection"),

			unity_AmbientSky = Shader.PropertyToID("unity_AmbientSky"),
			unity_AmbientEquator = Shader.PropertyToID("unity_AmbientEquator"),
			unity_AmbientGround = Shader.PropertyToID("unity_AmbientGround"),
			UNITY_LIGHTMODEL_AMBIENT = Shader.PropertyToID("UNITY_LIGHTMODEL_AMBIENT"),
			unity_FogColor = Shader.PropertyToID("unity_FogColor"),
			unity_FogParams = Shader.PropertyToID("unity_FogParams"),
			unity_OcclusionMaskSelector = Shader.PropertyToID("unity_OcclusionMaskSelector"),
			unity_WorldToLight = Shader.PropertyToID("unity_WorldToLight"),
			
			_ShadowMapTexture = Shader.PropertyToID("_ShadowMapTexture"),
			_LightShadowData = Shader.PropertyToID("_LightShadowData"),
			unity_ShadowFadeCenterAndType = Shader.PropertyToID("unity_ShadowFadeCenterAndType"),

			unity_SpecCube0 = Shader.PropertyToID("unity_SpecCube0"),
			unity_SpecCube1 = Shader.PropertyToID("unity_SpecCube1");

		static Vector4[] unimportant_LightColours = new Vector4[maxUnimportantLights];
		static Vector4[] unimportant_LightColours8 = new Vector4[8];
		static Vector4[] unimportant_LightPositions = new Vector4[maxUnimportantLights];
		static Vector4[] unimportant_lightAttenuations = new Vector4[maxUnimportantLights];
		static Vector4[] unimportant_SpotDirections = new Vector4[maxUnimportantLights];

		static Vector4 nonImportantX = Vector4.zero;
		static Vector4 nonImportantY = Vector4.zero;
		static Vector4 nonImportantZ = Vector4.zero;
		static Vector4 nonImportantAtten = Vector4.one;
		const string k_SetupLightConstants = "Setup Light Constants";
		const string k_SetupShadowConstants = "Setup Shadow Constants";

		const string bufferName = "Lighting";
		public void Cleanup(ScriptableRenderContext context,CommandBuffer buffer)
		{
			shadows.Cleanup(context,buffer);
		}
		public void RenderShadows(ScriptableRenderContext context, Camera camera, CullingResults cullingResults, TeleportRenderPipeline.LightingOrder lightingOrder)
		{
			CommandBuffer buffer = CommandBufferPool.Get(k_SetupShadowConstants);
			shadows.renderSettings = renderSettings;
			shadows.Setup(context, cullingResults);
			NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
			foreach (var visibleLight in visibleLights)
			{
				Light light = visibleLight.light;
				if (light == null)
				{
					continue;
				}

				if (!perFrameLightProperties.ContainsKey(light))
				{
					perFrameLightProperties.Add(light, new PerFrameLightProperties());
					perFrameLightProperties[light].visibleLight = visibleLight;
				}
				PerFrameLightProperties perFrame = perFrameLightProperties[light];
				perFrame.worldToLightMatrix = teleport.ShadowUtils.CalcWorldToLightMatrix(visibleLight);
			}
			Bounds bounds = new Bounds();
			
			if (lightingOrder.MainLightIndex >= 0|| (lightingOrder.AdditionalLightIndices != null&&lightingOrder.AdditionalLightIndices.Length>0))
			{
				for (int i = 0; i < visibleLights.Length; i++)
				{
					VisibleLight visibleLight = visibleLights[i];
					Light light = visibleLight.light;
					if (light == null)
					{
						continue;
					}
					int cascadeCount = light.type==LightType.Directional?4:1;
					if (lightingOrder.MainLightIndex == i )
					{
						shadows.ReserveDirectionalShadows(cullingResults, light, i);
					}
					PerFrameLightProperties perFrame = perFrameLightProperties[light];
					if (!cullingResults.GetShadowCasterBounds(i, out bounds))
					{
						perFrame.AnyShadows = false;
						continue;
					}
					perFrame.AnyShadows = true;
					perFrame.worldToLightMatrix= teleport.ShadowUtils.CalcWorldToLightMatrix(visibleLight);
					if (lightingOrder.MainLightIndex == i)
					{
						SetupMainLight(buffer, visibleLights[i]);
					}
					else
					{
						SetupAddLight(buffer, visibleLights[i]);
					}
					if (!perFrame.perFramePerCameraLightProperties.ContainsKey(camera))
					{
						perFrame.perFramePerCameraLightProperties.Add(camera, new PerFramePerCameraLightProperties());
					}
					if (lightingOrder.MainLightIndex == i )//|| lightingOrder.SecondLightIndex == i)
					{
						PerFramePerCameraLightProperties perFramePerCamera = perFrame.perFramePerCameraLightProperties[camera];
						shadows.RenderShadowsForLight(context, cullingResults, light, i, camera, ref perFrame, ref perFramePerCamera, cascadeCount);
					}
				}
				if (lightingOrder.AdditionalLightIndices != null)
				{
					for (int i = 0; i < lightingOrder.AdditionalLightIndices.Length; i++)
					{
						var visibleLightIndex = lightingOrder.AdditionalLightIndices[i];
						VisibleLight visibleLight = visibleLights[visibleLightIndex];
						Light light = visibleLight.light;
						if (light == null)
						{
							continue;
						}
						PerFrameLightProperties perFrame = perFrameLightProperties[light];
						if (!cullingResults.GetShadowCasterBounds(visibleLightIndex, out bounds))
						{
							perFrame.AnyShadows = false;
							continue;
						}
						perFrame.AnyShadows = true;
						PerFramePerCameraLightProperties perFramePerCamera = perFrame.perFramePerCameraLightProperties[camera];
						int cascadeCount = light.type == LightType.Directional ? 4 : 1;
						if (light.shadows != LightShadows.None)
							shadows.RenderShadowsForLight(context, cullingResults, light, visibleLightIndex, camera, ref perFrame, ref perFramePerCamera, cascadeCount);
					}
				}
			}
		}
		/// <summary>
		/// Render in screenspace the shadowing value from 0 to 1 for each pixel, for a given light.
		/// </summary>
		public void RenderScreenspaceShadows(ScriptableRenderContext context, Camera camera, TeleportRenderPipeline.LightingOrder lightingOrder, CullingResults cullingResults,RenderTexture depthTexture)
		{
			if (lightingOrder.MainLightIndex >= 0 && lightingOrder.MainLightIndex<cullingResults.visibleLights.Count())
			{
				var visibleLight = cullingResults.visibleLights[lightingOrder.MainLightIndex];
				var light = visibleLight.light;
				if (perFrameLightProperties.ContainsKey(light) && perFrameLightProperties[light].AnyShadows)
					shadows.RenderScreenspaceShadows(context, camera, cullingResults, perFrameLightProperties[light], 4, depthTexture);
			}
			if(lightingOrder.AdditionalLightIndices!=null)
			for(int i=0;i<lightingOrder.AdditionalLightIndices.Length;i++)
			{
				int vbIndex = lightingOrder.AdditionalLightIndices[i];
				if (cullingResults.visibleLights.Length <= vbIndex)
				{
					continue;
				}
				var visibleLight = cullingResults.visibleLights[vbIndex];
				var light = visibleLight.light;
				if (light.shadows != LightShadows.None&&perFrameLightProperties.ContainsKey(light)&&perFrameLightProperties[light].AnyShadows)
					shadows.RenderScreenspaceShadows(context, camera, cullingResults, perFrameLightProperties[light], 1, depthTexture);
			}
		}
		public void SetupForwardBasePass(ScriptableRenderContext context, Camera camera,CullingResults cullingResults, TeleportRenderPipeline.LightingOrder lightingOrder)
		{
			CommandBuffer buffer = CommandBufferPool.Get(k_SetupLightConstants);
			buffer.BeginSample(bufferName);
			NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
			RenderTexture screenspaceShadowTexture = null;
			if (lightingOrder.MainLightIndex >= 0)
			{
				var visibleLight = visibleLights[lightingOrder.MainLightIndex];
				var light = visibleLight.light;
				SetupMainLight(buffer, visibleLight);
				// Which light do we want the shadows for?
				if (perFrameLightProperties.ContainsKey(light)) 
				{
					var perFrame = perFrameLightProperties[light];
					if (perFrame.AnyShadows)
					{
						screenspaceShadowTexture = perFrame.perFramePerCameraLightProperties[camera].screenspaceShadowTexture;
						buffer.SetGlobalTexture(_ShadowMapTexture, screenspaceShadowTexture, RenderTextureSubElement.Color);
						shadows.ApplyShadowConstants(context, buffer, camera, cullingResults, perFrame);
					}
				}
			}
			else
			{
				ClearMainLight(buffer);
			}
			if(screenspaceShadowTexture!=null)
				buffer.SetGlobalTexture(_ShadowMapTexture, screenspaceShadowTexture, RenderTextureSubElement.Color);
			else
				buffer.SetGlobalTexture(_ShadowMapTexture, Texture2D.whiteTexture);
			nonImportantX = Vector4.zero;
			nonImportantY = Vector4.zero;
			nonImportantZ = Vector4.zero;
			nonImportantAtten = Vector4.zero;
			int n = 0;
			for (int i = 0; i < visibleLights.Length; i++)
			{
				if (i != lightingOrder.MainLightIndex && !lightingOrder.AdditionalLightIndices.Contains(i))
				{
					VisibleLight visibleLight = visibleLights[i];
					SetupUnimportantLight(buffer, n, visibleLight);
					n++;
				}
			}
			for (int i = lightingOrder.NumUnimportantLights; i < 4; i++)
			{
				unimportant_LightColours[i] = Vector3.zero;
			}
			// We want to use keywords to choose the correct shader variants.
			CoreUtils.SetKeyword(buffer, "DIRECTIONAL", true);
			CoreUtils.SetKeyword(buffer, "LIGHTPROBE_SH", true);
			CoreUtils.SetKeyword(buffer, "SHADOWS_SCREEN", true);
			CoreUtils.SetKeyword(buffer, "VERTEXLIGHT_ON", lightingOrder.NumUnimportantLights > 0);
			CoreUtils.SetKeyword(buffer, "_ALPHATEST_ON", false);
			CoreUtils.SetKeyword(buffer, "_NORMALMAP", true);


			//DIRECTIONAL LIGHTPROBE_SH SHADOWS_SCREEN VERTEXLIGHT_ON _METALLICGLOSSMAP _NORMALMAP _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
			//	CoreUtils.SetKeyword(buffer, "LIGHTMAP_ON", numUnimportantLights == 0);
			//	CoreUtils.SetKeyword(buffer, "UNITY_SHOULD_SAMPLE_SH", true);
			//Standard, SubShader #0
			//DIRECTIONAL LIGHTPROBE_SH VERTEXLIGHT_ON _NORMALMAP
			//buffer.SetGlobalVectorArray(unimportant_LightColor, unimportant_LightColours);

			// Use the skybox as the DEFAULT reflection cube. This SHOULD get overriden per-object later.
			//buffer.SetGlobalTexture(unity_SpecCube0, RenderSettings.skybox.GetTexture(""));

			buffer.SetGlobalVectorArray(unimportant_LightPosition, unimportant_LightPositions);
			buffer.SetGlobalVectorArray(unimportant_LightAtten, unimportant_lightAttenuations);
			buffer.SetGlobalVectorArray(unimportant_SpotDirection, unimportant_SpotDirections);

			Vector3 unityVector = new Vector3(1.0F, 1.0F, 1.0F);
			buffer.SetGlobalVector(unity_4LightPosX0, nonImportantX);
			buffer.SetGlobalVector(unity_4LightPosY0, nonImportantY);
			buffer.SetGlobalVector(unity_4LightPosZ0, nonImportantZ);

			buffer.SetGlobalVector(unity_4LightAtten0, nonImportantAtten);
			buffer.SetGlobalVectorArray(unity_LightAtten4, unimportant_lightAttenuations);
			buffer.SetGlobalVector(unity_LightColor0, unimportant_LightColours[0]);
			buffer.SetGlobalVector(unity_LightColor1, unimportant_LightColours[1]);
			buffer.SetGlobalVector(unity_LightColor2, unimportant_LightColours[2]);
			buffer.SetGlobalVector(unity_LightColor3, unimportant_LightColours[3]);
			//buffer.SetGlobalVector(unity_LightColor0+1, unimportant_LightColours[1]);
			buffer.SetGlobalVectorArray(unity_LightColor, unimportant_LightColours);

			//buffer.SetGlobalVector(unity_AmbientSky, unityVector);
			//buffer.SetGlobalVector(unity_AmbientEquator, unityVector);
			//buffer.SetGlobalVector(unity_AmbientGround, unityVector);
			//buffer.SetGlobalVector(UNITY_LIGHTMODEL_AMBIENT, unityVector);
			//buffer.SetGlobalVector(unity_FogColor, unityVector);
			//buffer.SetGlobalVector(unity_FogParams, unityVector);

			// TODO: contains a vector for selecting the channel for the light that's currently being rendered.
			buffer.SetGlobalVector(unity_OcclusionMaskSelector, new Vector3(1.0F,0.0F,0.0F));
			//
			buffer.EndSample(bufferName);
			context.ExecuteCommandBuffer(buffer);
			CommandBufferPool.Release(buffer);
		}
		public bool SetupForwardAddPass(ScriptableRenderContext context, Camera camera,CullingResults cullingResults, TeleportRenderPipeline.LightingOrder lightingOrder,int additionalLightIndex)
		{
			CommandBuffer buffer = CommandBufferPool.Get(k_SetupLightConstants);
			NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
			if (additionalLightIndex < 0|| additionalLightIndex>= visibleLights.Length)
				return false;
			
			VisibleLight visibleLight = visibleLights[additionalLightIndex];
			Light light = visibleLight.light;
			if (light == null)
				return false;
			buffer.BeginSample(bufferName);
			SetupAddLight(buffer, visibleLight);
			bool has_shadows = false;
			if (perFrameLightProperties.ContainsKey(light)&&perFrameLightProperties[light].AnyShadows)
			{
				var perFrame = perFrameLightProperties[light];
				if (!perFrame.perFramePerCameraLightProperties.ContainsKey(camera))
				{
					perFrame.perFramePerCameraLightProperties.Add(camera, new PerFramePerCameraLightProperties());
				}
				PerFramePerCameraLightProperties perFramePerCamera = perFrame.perFramePerCameraLightProperties[camera];
				has_shadows = true;
				buffer.SetGlobalTexture(_ShadowMapTexture, perFrame.shadowAtlasTexture, RenderTextureSubElement.Depth);
				Vector4 lightShadowData = new Vector4(0.0F, 6.66666F, 0.033333333F, -2.66667F);
				buffer.SetGlobalVector(_LightShadowData, lightShadowData);
				shadows.ApplyShadowConstants(context, buffer, camera, cullingResults, perFrame);
				buffer.SetGlobalVector(unity_ShadowFadeCenterAndType, perFramePerCamera.shadowFadeCenterAndType);
			}

			CoreUtils.SetKeyword(buffer, "POINT", visibleLight.lightType == LightType.Point);
			CoreUtils.SetKeyword(buffer, "DIRECTIONAL", visibleLight.lightType == LightType.Directional);
			CoreUtils.SetKeyword(buffer, "SPOT", visibleLight.lightType == LightType.Spot);
			CoreUtils.SetKeyword(buffer, "SHADOWS_DEPTH", has_shadows);
			CoreUtils.SetKeyword(buffer, "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A", true);

			//buffer.SetGlobalVectorArray(_VisibleLightColors, visibleLightColors);
			//buffer.SetGlobalVectorArray(_VisibleLightDirectionsOrPositions, visibleLightDirectionsOrPositions);

			buffer.EndSample(bufferName);
			context.ExecuteCommandBuffer(buffer);
			CommandBufferPool.Release(buffer);
			return true;
		}
		void SetupUnimportantLight(CommandBuffer buffer, int index, VisibleLight light)
		{
			//LightRenderMode mode =light.light.renderMode;
			Vector3 lightPos = light.localToWorldMatrix.GetColumn(3);
			if (light.lightType == LightType.Directional)
			{
				unimportant_LightPositions[index] = -light.localToWorldMatrix.GetColumn(2);
			}
			else if (index < maxUnimportantLights)
			{
				unimportant_LightPositions[index] = light.localToWorldMatrix.GetColumn(3);
				if (light.lightType == LightType.Spot)
				{
					unimportant_lightAttenuations[index].x = Mathf.Cos(light.spotAngle / 2.0f);
					unimportant_lightAttenuations[index].y = 1.0f / Mathf.Cos(light.spotAngle / 4.0f);
					unimportant_lightAttenuations[index].z = 1.0f;
					unimportant_lightAttenuations[index].w = Mathf.Pow(light.range, 2.0f);
					//Light attenuation factors.x is cos(spotAngle / 2) or –1 for non - spot lights;
					// y is 1 / cos(spotAngle / 4) or 1 for non - spot lights;
					// z is quadratic attenuation;
					//w is squared light range.
				}
			}

			//float4 corr = rsqrt(lengthSq);
			//ndotl = max(float4(0, 0, 0, 0), ndotl * corr);
			// attenuation
			float atten = 25.0F / Mathf.Pow(light.range, 2.0f);


			//ightColor0 = final_col * (1.0 + lengthSq * atten); ;


			//float corr = 1.0F/(light.range);
			// attenuation
			//float atten = 1.0F / (1.0F + 25.0F);
			float diff = (1.0F + atten);

			unimportant_LightColours8[index] = light.finalColor * diff;
			// Fill in xyzw of these variables: e.g. index 0 is x etc.
			if (index < 4)
			{
				unimportant_LightColours[index] = light.finalColor * diff;
				nonImportantX[index] = lightPos.x;
				nonImportantY[index] = lightPos.y;
				nonImportantZ[index] = lightPos.z;
				nonImportantAtten[index] = atten;
			}
			// so the "attenuation" is 25/(range^2) - call this "a^2", so a = 5/range.
			// Now in UnityCG.cginc shader function Shade4PointLights we have
			// radiance=lightColour * (n.l) * 1/(1+r^2*a^2)
			// What does this mean?
			// radiance=lightColour * n.l * 1/(1+(r*5/range)^2)
			//								1/(1+((5*r)/range)^2)
			// e.g. if range was very large, it would be 1/((5r)^2).
			// i.e. proper inv square law.

			// At what range is radiance==lightColour??
			// that would be where
			//									1/(1+((5*r)/range)^2)=1
			//									1+25(r^2/range^2))=1
			// i.e. r/range -> 0 - at the light position itself, we have radiance=lightColour.
			// nonsense of course.
			// The real eqn that we go to at infinity would be:
			//	radiance=lightColour*n.l / (5*r/range)^2
			// which would mean radiance=lightColour where 5r==range, or r=range/5.
			// The proper equation should be to treat the light as a sphere of radius range/5,
			// and consider lightColour to be the irradiance at that surface.
		}
		void ClearMainLight(CommandBuffer buffer)
		{
			buffer.SetGlobalVector(_LightColor0, Vector4.zero);
			buffer.SetGlobalVector(_WorldSpaceLightPos0, Vector3.zero);
		}
		void SetupAddLight(CommandBuffer buffer, VisibleLight light)
		{
			SetupMainLight(buffer, light);
		}
		void SetupMainLight(CommandBuffer buffer, VisibleLight light)
		{
			Vector4 atten = new Vector4();
			if (light.lightType != LightType.Directional)
			{
				if (light.lightType == LightType.Spot)
				{
					atten.x = Mathf.Cos(light.spotAngle / 2.0f);
					atten.y = 1.0f / Mathf.Cos(light.spotAngle / 4.0f);
					atten.z = 1.0f;
					atten.w = Mathf.Pow(light.range, 2.0f);
					//Light attenuation factors.x is cos(spotAngle / 2) or –1 for non - spot lights;
					// y is 1 / cos(spotAngle / 4) or 1 for non - spot lights;
					// z is quadratic attenuation;
					//w is squared light range.
				}
			}
			if (light.lightType == LightType.Spot)
			{
				if (light.light.cookie != null)
					buffer.SetGlobalTexture(_LightTexture0, light.light.cookie);
				else
					buffer.SetGlobalTexture(_LightTexture0, GeneratedTexture.unitySoftTexture.renderTexture);
			}
			else if (light.lightType == LightType.Point)
				buffer.SetGlobalTexture(_LightTexture0, GeneratedTexture.unityAttenuationTexture.renderTexture);
			// TODO: we already did this for the PerFramePerLight properties:
			Matrix4x4 worldToLight = teleport.ShadowUtils.CalcWorldToLightMatrix(light);
			buffer.SetGlobalMatrix(unity_WorldToLight, worldToLight);

			// Supposedly:
			//_LightShadowData.y = Appears to be unused
			//_LightShadowData.z = 1.0 / shadow far distance
			//_LightShadowData.w = shadow near 
			// But actually:
			Vector4 lightShadowData = new Vector4(1.0F - light.light.shadowStrength, 20.0F/3.0F, 1.0F/60.0F, -8.0F/3.0F);
			
			buffer.SetGlobalVector(_LightShadowData, lightShadowData);

			Vector4 lightPos = light.localToWorldMatrix.GetColumn(3);
			lightPos.w = 1.0F;
			Vector4 lightDir = -light.localToWorldMatrix.GetColumn(2);
			lightDir.w = 0.0F;
			buffer.SetGlobalVector(_LightColor0, light.finalColor);
			if (light.lightType == LightType.Directional)
				buffer.SetGlobalVector(_WorldSpaceLightPos0, lightDir);
			else
				buffer.SetGlobalVector(_WorldSpaceLightPos0, lightPos);
			if(_LightMatrix0!= unity_WorldToLight)
				buffer.SetGlobalMatrix(_LightMatrix0, light.localToWorldMatrix);
		}
	}
	//FOG_LINEAR SHADOWS_DEPTH SPOT _METALLICGLOSSMAP _NORMALMAP
}