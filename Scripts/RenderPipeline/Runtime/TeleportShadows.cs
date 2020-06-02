using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class TeleportShadows
{
	public TeleportRenderSettings renderSettings = null;
	const string bufferName = "Shadows";
	//static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");
	public RenderTexture shadowAtlasTexture = null;
	const int maxShadowedDirectionalLightCount = 4;
	int ShadowedDirectionalLightCount = 0;
	int tileSize = 0;
	int split = 0;
	struct ShadowedDirectionalLight
	{
		public int visibleLightIndex;
	}

	ShadowedDirectionalLight[] ShadowedDirectionalLights =	new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];

	public void ReserveDirectionalShadows(CullingResults cullingResults,Light light, int visibleLightIndex)
	{
		if (	ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount &&
				light.shadows != LightShadows.None && light.shadowStrength > 0f &&
				cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
		{
			ShadowedDirectionalLights[ShadowedDirectionalLightCount++] =
				new ShadowedDirectionalLight
				{
					visibleLightIndex = visibleLightIndex
				};
		}
	}

	CommandBuffer buffer = new CommandBuffer
	{
		name = bufferName
	};

	public void Setup(ScriptableRenderContext context, CullingResults cullingResults	)
	{
		ShadowedDirectionalLightCount = 0;
	}
	public void Cleanup(ScriptableRenderContext context)
	{
		if (ShadowedDirectionalLightCount > 0)
		{
		//	buffer.ReleaseTemporaryRT(dirShadowAtlasId);
			ExecuteBuffer(context);
		}
	}
	void ExecuteBuffer(ScriptableRenderContext context)
	{
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}
	Rect GetTileViewport(int index, int tileSize, int split)
	{
		Vector2 offset = new Vector2(index % split, index / split);
		return new Rect(
			offset.x * tileSize, offset.y * tileSize, tileSize, tileSize
		);
	}
	public void RenderDirectionalShadows(ScriptableRenderContext context, CullingResults cullingResults, Light light, int cascadeCount, int index)
	{
		if (ShadowedDirectionalLightCount == 0)
			return;
		if (index >= 4)
			return;
		ShadowedDirectionalLight sdl = ShadowedDirectionalLights[0];


		var shadowSettings =new ShadowDrawingSettings(cullingResults, sdl.visibleLightIndex);

		Vector3 ratios = new Vector3(1.0f,1.0f,1.0f);

	/*	float[] shadowCascadeRatios = new float[3] { 0.138f, 0.251f, 0.369f };
		for (int i = 0; i<3; i++)
			ratios[i] = shadowCascadeRatios[i];*/

		/*if (cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
			light.visibleLightIndex, 0, 1, Vector3.zero, tileSize, 0f,
			out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
			out ShadowSplitData splitData
			))*/
			if (cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
			sdl.visibleLightIndex, index, 4, ratios, tileSize, 0f,
			out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
			out ShadowSplitData splitData
			))
		{
			Rect vp = GetTileViewport(index, tileSize, split);
			shadowSettings.splitData = splitData;
			buffer.SetViewport(vp);
			buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
			ExecuteBuffer(context);
			context.DrawShadows(ref shadowSettings);
			
		}
	}
	public void RenderDirectionalShadows(ScriptableRenderContext context, CullingResults cullingResults,Light light,int cascadeCount)
	{
		int atlasSize = (int)renderSettings.directional.atlasSize;
		split = cascadeCount <= 1 ? 1 : 2;
		tileSize = atlasSize / split;
		if (shadowAtlasTexture == null || shadowAtlasTexture.width != atlasSize)
		{
			shadowAtlasTexture = new RenderTexture(atlasSize, atlasSize, 1, RenderTextureFormat.Shadowmap);
		}
		//buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize,32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
		//_WorldSpaceLightPos0 = Shader.PropertyToID("_WorldSpaceLightPos0"); // dir or pos
		buffer.SetRenderTarget(shadowAtlasTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
		buffer.ClearRenderTarget(true, false, Color.clear);
		buffer.BeginSample(bufferName);
		ExecuteBuffer(context);
		for(int i=0;i<4;i++)
		{
			RenderDirectionalShadows(context, cullingResults, light, cascadeCount, i);
		}
		buffer.EndSample(bufferName);
		ExecuteBuffer(context);
	}
}