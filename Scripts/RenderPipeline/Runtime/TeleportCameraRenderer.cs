using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public partial class TeleportCameraRenderer
{
	TeleportLighting lighting = new TeleportLighting();
	void ExecuteBuffer(ScriptableRenderContext context, CommandBuffer buffer)
	{
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}
	void BeginCamera(ScriptableRenderContext context, Camera camera)
	{
		context.SetupCameraProperties(camera);
		//buffer.BeginSample(buffer.name);
		if (camera.clearFlags != CameraClearFlags.Nothing )
		{
			var buffer = new CommandBuffer { name = camera.name + " TeleportCameraRenderer BeginCamera" };
			buffer.ClearRenderTarget(
				camera.clearFlags == CameraClearFlags.Depth || camera.clearFlags == CameraClearFlags.Color || camera.clearFlags == CameraClearFlags.Skybox,
				camera.clearFlags == CameraClearFlags.Color, camera.backgroundColor, 1.0f);
			//buffer.EndSample(buffer.name);
			ExecuteBuffer(context, buffer);
			buffer.Release();
			if (camera.clearFlags == CameraClearFlags.Skybox)
			{
				context.DrawSkybox(camera);
			}
		}
	}
	bool Cull(ScriptableRenderContext context, Camera camera, out CullingResults cullingResults)
	{
		//ScriptableCullingParameters p
		if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
		{
			cullingResults = context.Cull(ref p);
			return true;
		}
		cullingResults = new CullingResults();
		return false;
	}
	static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
	static ShaderTagId[] legacyShaderTagIds = {
		new ShaderTagId("Always"),
		new ShaderTagId("Forward"),
		new ShaderTagId("ForwardOnly"),
		new ShaderTagId("ForwardBase"),
		new ShaderTagId("PrepassBase"),
		new ShaderTagId("Vertex"),
		new ShaderTagId("VertexLMRGBM"),
		new ShaderTagId("VertexLM"),
		new ShaderTagId("SRPDefaultLit"),
		new ShaderTagId("SRPDefaultUnlit")
};
	void SetupLighting(ScriptableRenderContext context, CullingResults cullingResults, ref DrawingSettings drawingSettings)
	{
		lighting.Setup(context, cullingResults);
		NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
		for (int i = 0; i < visibleLights.Length; i++)
		{
			VisibleLight visibleLight = visibleLights[i];
			//SetupDirectionalLight(i, visibleLight);
		}
	}
	void DrawOpaqueGeometry(ScriptableRenderContext context, Camera camera)
	{
		var sortingSettings = new SortingSettings(camera)
		{
			criteria = SortingCriteria.CommonOpaque
		};
		var drawingSettings = new DrawingSettings(legacyShaderTagIds[0], sortingSettings);
		var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
		CullingResults cullingResults;
		if (!Cull(context, camera, out cullingResults))
		{
			return;
		}
		SetupLighting(context, cullingResults, ref drawingSettings);
		for (int i = 1; i < legacyShaderTagIds.Length; i++)
		{
			drawingSettings.SetShaderPassName(i, legacyShaderTagIds[i]);
		}
		context.DrawRenderers(
			cullingResults, ref drawingSettings, ref filteringSettings
		);
	}
	void DrawTransparentGeometry(ScriptableRenderContext context, Camera camera)
	{
		var sortingSettings = new SortingSettings(camera)
		{
			criteria = SortingCriteria.CommonTransparent
		};
		var drawingSettings = new DrawingSettings(
			legacyShaderTagIds[0], sortingSettings
		);
		var filteringSettings = new FilteringSettings(RenderQueueRange.transparent);
		CullingResults cullingResults;
		if (!Cull(context, camera, out cullingResults))
		{
			return;
		}
		for (int i = 1; i < legacyShaderTagIds.Length; i++)
		{
			drawingSettings.SetShaderPassName(i, legacyShaderTagIds[i]);
		}
		context.DrawRenderers(
			cullingResults, ref drawingSettings, ref filteringSettings
		);
	}

	void EndCamera(ScriptableRenderContext context, Camera camera)
	{
		context.Submit();
	}

	public void Render(ScriptableRenderContext context, Camera camera)
	{
		BeginCamera(context, camera);
		string samplename = camera.gameObject.name + " sample";
		StartSample(context,samplename );

		PrepareForSceneWindow(context, camera);
		DrawOpaqueGeometry(context, camera);
		DrawTransparentGeometry(context, camera);
		DrawUnsupportedShaders(context, camera);
		DrawGizmos(context, camera);
		EndSample(context, samplename);
		EndCamera(context, camera);
	}
}
