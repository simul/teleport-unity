using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class TeleportLighting
{

	const string bufferName = "Lighting";

	CommandBuffer buffer = new CommandBuffer
	{
		name = bufferName
	};
	const int maxVisibleLights = 4;
	static int
		_LightColor0 = Shader.PropertyToID("_LightColor0"),
		_WorldSpaceLightPos0 = Shader.PropertyToID("_WorldSpaceLightPos0"), // dir or pos
		_LightMatrix0 = Shader.PropertyToID("_LightMatrix0"),
		unity_4LightPosX0 = Shader.PropertyToID("unity_4LightPosX0"),
		unity_4LightPosY0 = Shader.PropertyToID("unity_4LightPosY0"),
		unity_4LightPosZ0 = Shader.PropertyToID("unity_4LightPosZ0"),
		//world space positions of first four non-important point lights.
		unity_4LightAtten0 = Shader.PropertyToID("unity_4LightAtten0"),//float4(ForwardBase pass only) attenuation factors of first four non-important point lights.
		unity_LightColor = Shader.PropertyToID("unity_LightColor"),// half4[4]    (ForwardBase pass only) colors of of first four non-important point lights.
		unity_WorldToShadow = Shader.PropertyToID("unity_WorldToShadow"),
		_DirectionalLightCount = Shader.PropertyToID("_DirectionalLightCount"),
		_DirectionalLightColors = Shader.PropertyToID("_DirectionalLightColors"),
		_DirectionalLightDirections = Shader.PropertyToID("_DirectionalLightDirections"),
		unity_LightAtten = Shader.PropertyToID("unity_LightAtten"),
		_VisibleLightColors = Shader.PropertyToID("_VisibleLightColors"),
		_VisibleLightDirectionsOrPositions = Shader.PropertyToID("_VisibleLightDirectionsOrPositions"),
		unity_AmbientSky = Shader.PropertyToID("unity_AmbientSky"),
		unity_AmbientEquator = Shader.PropertyToID("unity_AmbientEquator"),
		unity_AmbientGround = Shader.PropertyToID("unity_AmbientGround"),
		UNITY_LIGHTMODEL_AMBIENT = Shader.PropertyToID("UNITY_LIGHTMODEL_AMBIENT"),
		unity_FogColor = Shader.PropertyToID("unity_FogColor"),
		unity_FogParams = Shader.PropertyToID("unity_FogParams"),
		unity_OcclusionMaskSelector = Shader.PropertyToID("unity_OcclusionMaskSelector");

	Vector4[] visibleLightColors = new Vector4[maxVisibleLights];
	Vector4[] visibleLightDirectionsOrPositions = new Vector4[maxVisibleLights];
	Vector4[] lightAttenuations = new Vector4[maxVisibleLights];
	public void Setup(ScriptableRenderContext context,CullingResults cullingResults)
	{
		buffer.BeginSample(bufferName);
		NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
		for (int i = 0; i < visibleLights.Length; i++)
		{
			VisibleLight visibleLight = visibleLights[i];
			SetupLight(i, visibleLight);
		}

		buffer.SetGlobalInt(_DirectionalLightCount, visibleLights.Length);
		buffer.SetGlobalVectorArray(_VisibleLightColors, visibleLightColors);
		buffer.SetGlobalVectorArray(_VisibleLightDirectionsOrPositions, visibleLightDirectionsOrPositions);

		Vector3 unityVector = new Vector3(1.0F, 1.0F, 1.0F);
		buffer.SetGlobalVector(unity_4LightPosX0, unityVector);
		buffer.SetGlobalVector(unity_4LightPosY0, unityVector);
		buffer.SetGlobalVector(unity_4LightPosZ0, unityVector);

		buffer.SetGlobalVector(unity_4LightAtten0, unityVector);
		buffer.SetGlobalVector(unity_LightColor, unityVector);

		buffer.SetGlobalVector(unity_AmbientSky, unityVector);
		buffer.SetGlobalVector(unity_AmbientEquator, unityVector);
		buffer.SetGlobalVector(unity_AmbientGround, unityVector);
		buffer.SetGlobalVector(UNITY_LIGHTMODEL_AMBIENT, unityVector);
		buffer.SetGlobalVector(unity_FogColor, unityVector);
		buffer.SetGlobalVector(unity_FogParams, unityVector);
		buffer.SetGlobalVector(unity_OcclusionMaskSelector, unityVector);
		
		buffer.EndSample(bufferName);
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}
	void SetupLight(int index, VisibleLight light)
	{
		if (light.lightType == LightType.Directional)
		{
			visibleLightDirectionsOrPositions[index] = -light.localToWorldMatrix.GetColumn(2);
		}
		else
		{
			visibleLightDirectionsOrPositions[index] =	light.localToWorldMatrix.GetColumn(3);
			if (light.lightType == LightType.Spot)
			{
				lightAttenuations[index].x=Mathf.Cos(light.spotAngle / 2.0f);
				lightAttenuations[index].y =1.0f / Mathf.Cos(light.spotAngle / 4.0f);
				lightAttenuations[index].z = 1.0f;
				lightAttenuations[index].w = Mathf.Pow(light.range,2.0f);
				//Light attenuation factors.x is cos(spotAngle / 2) or –1 for non - spot lights;
				// y is 1 / cos(spotAngle / 4) or 1 for non - spot lights;
				// z is quadratic attenuation;
				//w is squared light range.
			}
		}
		Matrix4x4 LightMatrix0=new Matrix4x4();
		Matrix4x4[] WorldToShadow = new Matrix4x4[4];
		WorldToShadow[0] = Matrix4x4.identity;
		LightMatrix0 = Matrix4x4.identity;
		visibleLightColors[index] = light.finalColor;
		buffer.SetGlobalVector(_LightColor0, light.finalColor);
		buffer.SetGlobalVector(_WorldSpaceLightPos0, visibleLightDirectionsOrPositions[index]);
		buffer.SetGlobalMatrix(_LightMatrix0, LightMatrix0);
		buffer.SetGlobalMatrixArray(unity_WorldToShadow, WorldToShadow); 
		buffer.SetGlobalVectorArray(unity_LightAtten, lightAttenuations);
	}
}