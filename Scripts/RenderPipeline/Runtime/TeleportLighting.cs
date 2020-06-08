using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Collections.Generic;

public struct PerFrameShadowCascadeProperties
{
	public Matrix4x4 viewMatrix;
	public Matrix4x4 projectionMatrix;
	public ShadowSplitData splitData;
}

public class PerFrameLightProperties
{
	public VisibleLight visibleLight;
	// One of these for each cascade.
	public PerFrameShadowCascadeProperties[] cascades=new PerFrameShadowCascadeProperties[4];
};
public class TeleportLighting
{
	public TeleportRenderSettings renderSettings=null;
	public TeleportShadows shadows = new TeleportShadows();
	static Dictionary<VisibleLight, PerFrameLightProperties> perFrameLightProperties=new Dictionary<VisibleLight, PerFrameLightProperties>();
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
		unity_WorldToLight = Shader.PropertyToID("unity_WorldToLight");

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
	public void Cleanup(ScriptableRenderContext context)
	{
		shadows.Cleanup( context);
	}
	public void RenderShadows(ScriptableRenderContext context, CullingResults cullingResults, TeleportRenderPipeline.LightingOrder lightingOrder)
	{
		CommandBuffer buffer = CommandBufferPool.Get(k_SetupShadowConstants);
		shadows.renderSettings = renderSettings;
		shadows.Setup(context, cullingResults);
		NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;

		if (lightingOrder.MainLightIndex >= 0)
		{
			for (int i = 0; i < visibleLights.Length; i++)
			{
				VisibleLight light = visibleLights[i];
				if (lightingOrder.MainLightIndex == i || lightingOrder.SecondLightIndex == i)
				{
					shadows.ReserveDirectionalShadows(cullingResults, light.light, i);
				}
				if (lightingOrder.MainLightIndex == i)
				{
					SetupMainLight(buffer, visibleLights[i]);
				}
				else
				{
					SetupAddLight(buffer, visibleLights[i]);
				}
				if (!perFrameLightProperties.ContainsKey(light))
				{
					perFrameLightProperties.Add(light, new PerFrameLightProperties());
					perFrameLightProperties[light].visibleLight = light;
				}
				PerFrameLightProperties perFrame =perFrameLightProperties[light];
				shadows.RenderDirectionalShadows(context, cullingResults, ref perFrame, 4);
			}
		}
	}
	public void RenderScreenspaceShadows(ScriptableRenderContext context, Camera camera,CullingResults cullingResults)
	{
		TeleportRenderPipeline.LightingOrder lightingOrder = TeleportRenderPipeline.GetLightingOrder(cullingResults.visibleLights);
		if (lightingOrder.MainLightIndex >= 0)
		{
			var visibleLight= cullingResults.visibleLights[lightingOrder.MainLightIndex];
			shadows.RenderScreenspaceShadows(context, camera, cullingResults, perFrameLightProperties[visibleLight], 4);
		}
	}
	public void SetupForwardBasePass(ScriptableRenderContext context,CullingResults cullingResults, TeleportRenderPipeline.LightingOrder lightingOrder)
	{
		CommandBuffer buffer = CommandBufferPool.Get(k_SetupLightConstants);
		buffer.BeginSample(bufferName);
		NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
		if (lightingOrder.MainLightIndex >= 0)
		{
			SetupMainLight(buffer, visibleLights[lightingOrder.MainLightIndex]);
		}
		nonImportantX = Vector4.zero;
		nonImportantY = Vector4.zero;
		nonImportantZ = Vector4.zero;
		nonImportantAtten = Vector4.zero;
		int n = 0;
		for (int i = 0; i < visibleLights.Length; i++)
		{
			if (i != lightingOrder.MainLightIndex&& i != lightingOrder.SecondLightIndex)
			{ 
				VisibleLight visibleLight = visibleLights[i];
				SetupUnimportantLight(buffer, n, visibleLight);
				n++;
			}
		}
		for (int i= lightingOrder.NumUnimportantLights; i<4;i++)
		{
			unimportant_LightColours[i] = Vector3.zero;
		}
		// We want to use keywords to choose the correct shader variants.
		CoreUtils.SetKeyword(buffer, "DIRECTIONAL", true);
		CoreUtils.SetKeyword(buffer, "LIGHTPROBE_SH",true);
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
		//buffer.SetGlobalVector(unity_OcclusionMaskSelector, unityVector);
		//
		buffer.EndSample(bufferName);
		context.ExecuteCommandBuffer(buffer);
		CommandBufferPool.Release(buffer);
	}
	public bool SetupForwardAddPass(ScriptableRenderContext context, CullingResults cullingResults, TeleportRenderPipeline.LightingOrder lightingOrder)
	{
		CommandBuffer buffer = CommandBufferPool.Get(k_SetupLightConstants);
		buffer.BeginSample(bufferName);
		NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
		if ( lightingOrder.SecondLightIndex<0)
			return false;
		VisibleLight visibleLight = visibleLights[lightingOrder.SecondLightIndex];
		SetupAddLight(buffer, visibleLight);
		
		CoreUtils.SetKeyword(buffer, "POINT", visibleLight.lightType==LightType.Point);
		CoreUtils.SetKeyword(buffer, "SPOT", visibleLight.lightType == LightType.Spot);
		CoreUtils.SetKeyword(buffer, "SHADOWS_DEPTH", visibleLight.light.shadows==LightShadows.Hard);

		//buffer.SetGlobalVectorArray(_VisibleLightColors, visibleLightColors);
		//buffer.SetGlobalVectorArray(_VisibleLightDirectionsOrPositions, visibleLightDirectionsOrPositions);

		buffer.EndSample(bufferName);
		context.ExecuteCommandBuffer(buffer);
		CommandBufferPool.Release(buffer);
		return true;
	}
	void SetupUnimportantLight(CommandBuffer buffer,int index, VisibleLight light)
	{
		//LightRenderMode mode =light.light.renderMode;
		Vector3 lightPos = light.localToWorldMatrix.GetColumn(3);
		if (light.lightType == LightType.Directional)
		{
			unimportant_LightPositions[index] = -light.localToWorldMatrix.GetColumn(2);
		}
		else if(index< maxUnimportantLights)
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

		unimportant_LightColours8[index] = light.finalColor*diff;
		if (index<4)
		{
			unimportant_LightColours[index] = light.finalColor*diff;
			nonImportantX[index] = lightPos.x;
			nonImportantY[index] = lightPos.y;
			nonImportantZ[index] = lightPos.z;
			nonImportantAtten[index ] = atten;
		}
	}
	void SetupAddLight(CommandBuffer buffer, VisibleLight light)
	{
		SetupMainLight(buffer, light);
	}
	void SetupMainLight(CommandBuffer buffer, VisibleLight light)
	{
		//LightRenderMode mode =light.light.renderMode;
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
			buffer.SetGlobalTexture(_LightTexture0, light.light.cookie);

		Matrix4x4 worldToShadow = teleport.ShadowUtils.CalcShadowMatrix( light);
		if (light.lightType == LightType.Spot)
			buffer.SetGlobalMatrix(unity_WorldToLight, worldToShadow);
		else
			buffer.SetGlobalMatrix(unity_WorldToLight, worldToShadow);


		Vector4 lightPos = light.localToWorldMatrix.GetColumn(3);
		lightPos.w = 1.0F;
		Vector4 lightDir = -light.localToWorldMatrix.GetColumn(2);
		lightDir.w = 0.0F;
		buffer.SetGlobalVector(_LightColor0, light.finalColor);
		if (light.lightType == LightType.Directional)
			buffer.SetGlobalVector(_WorldSpaceLightPos0, lightDir);
		else
			buffer.SetGlobalVector(_WorldSpaceLightPos0, lightPos);
		buffer.SetGlobalMatrix(_LightMatrix0, light.localToWorldMatrix);
		if (light.lightType == LightType.Spot)
		{
			buffer.SetGlobalTexture(_LightTexture0, light.light.cookie);
		}
	}
}
//FOG_LINEAR SHADOWS_DEPTH SPOT _METALLICGLOSSMAP _NORMALMAP