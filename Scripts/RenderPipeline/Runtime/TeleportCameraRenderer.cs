using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Conditional = System.Diagnostics.ConditionalAttribute;

using teleport;

using uid = System.UInt64;

public partial class TeleportCameraRenderer
{
	// Start cubemap members
	struct CamView
	{
		public Vector3 forward, up;

		public CamView(Vector3 forward, Vector3 up) => (this.forward, this.up) = (forward, up);
	}

	static int NumFaces = 6;
	static int[,] faceOffsets = new int[6, 2] { { 0, 0 }, { 1, 0 }, { 2, 0 }, { 0, 1 }, { 1, 1 }, { 2, 1 } };

	// To align with Unreal Engine
	static Quaternion frontFaceRot = Quaternion.Euler(0, 0, 0);
	static Quaternion backFaceRot = Quaternion.Euler(0, 0, 0);
	static Quaternion rightFaceRot = Quaternion.identity;
	static Quaternion leftFaceRot = Quaternion. Euler(0, 0, 180);
	static Quaternion upFaceRot = Quaternion.Euler(0, 0, -90);
	static Quaternion downFaceRot = Quaternion.Euler(0, 0, -90); 

	static Quaternion[] faceRotations = new Quaternion[] { frontFaceRot, backFaceRot, rightFaceRot, leftFaceRot, upFaceRot, downFaceRot };

	// Switch back and front because Unity view matrices have -z for forward
	static CamView frontCamView = new CamView(Vector3.back, Vector3.left);
	static CamView backCamView = new CamView(Vector3.forward, Vector3.right);
	static CamView rightCamView = new CamView(Vector3.right, Vector3.up);
	static CamView leftCamView = new CamView(Vector3.left, Vector3.up);
	static CamView upCamView = new CamView(Vector3.up, Vector3.back);
	static CamView downCamView = new CamView(Vector3.down, Vector3.forward);

	static CamView[] faceCamViews = new CamView[] { frontCamView, backCamView, rightCamView, leftCamView, upCamView, downCamView };

	// End cubemap members


	TeleportLighting teleportLighting = new TeleportLighting();


	static Shader depthShader = null;
	static Material depthMaterial = null;

	public TeleportCameraRenderer()
	{

	}

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
				camera.clearFlags == CameraClearFlags.Color, camera.backgroundColor.linear, 1.0f);
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
	//static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
	static ShaderTagId[] legacyShaderTagIds = {
		//new ShaderTagId("Always"),
		//new ShaderTagId("Forward"),
		//new ShaderTagId("ForwardOnly"),
		new ShaderTagId("ForwardBase"),
		//new ShaderTagId("PrepassBase"),
		//new ShaderTagId("Vertex"),
		//new ShaderTagId("VertexLMRGBM"),
		//new ShaderTagId("VertexLM"),
		//new ShaderTagId("SRPDefaultLit"),
		//new ShaderTagId("SRPDefaultUnlit")
};
	static ShaderTagId[] addLightShaderTagIds = {
		new ShaderTagId("ForwardAdd"),
};
	void SetupLighting(ScriptableRenderContext context, CullingResults cullingResults, ref DrawingSettings drawingSettings)
	{
	}
	void DrawOpaqueGeometry(ScriptableRenderContext context, Camera camera)
	{
		var sortingSettings = new SortingSettings(camera)
		{
			criteria = SortingCriteria.CommonOpaque
		};
		var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
		CullingResults cullingResults;
		if (!Cull(context, camera, out cullingResults))
		{
			return;
		}
		TeleportRenderPipeline.LightingOrder lightingOrder = TeleportRenderPipeline.GetMainLightIndex(cullingResults.visibleLights);
		{
			var drawingSettings = new DrawingSettings(legacyShaderTagIds[0], sortingSettings);
			drawingSettings.mainLightIndex = lightingOrder.MainLightIndex;

			//drawingSettings.enableDynamicBatching = true;
			//drawingSettings.enableInstancing= true;
			if (cullingResults.visibleLights.Length > 0)
			{
				drawingSettings.perObjectData |= PerObjectData.LightProbe
						| PerObjectData.ReflectionProbes
						| PerObjectData.LightProbeProxyVolume
						| PerObjectData.Lightmaps
						| PerObjectData.LightData
						| PerObjectData.MotionVectors
						| PerObjectData.LightIndices
						| PerObjectData.ReflectionProbeData
						| PerObjectData.OcclusionProbe
						| PerObjectData.OcclusionProbeProxyVolume
						| PerObjectData.ShadowMask;
			}
			teleportLighting.SetupForwardBasePass(context, cullingResults, lightingOrder);
		for (int i = 1; i < legacyShaderTagIds.Length; i++)
		{
			drawingSettings.SetShaderPassName(i, legacyShaderTagIds[i]);
		}
			context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
	}
		if(cullingResults.visibleLights.Length>1)
		{
			var drawingSettings = new DrawingSettings(addLightShaderTagIds[0], sortingSettings);
			drawingSettings.mainLightIndex = 0;
			drawingSettings.enableDynamicBatching = true;
			drawingSettings.enableInstancing= true;
			teleportLighting.SetupForwardAddPass(context, cullingResults, lightingOrder);
			for (int i = 0; i < addLightShaderTagIds.Length; i++)
			{
				drawingSettings.SetShaderPassName(i, addLightShaderTagIds[i]);
			}
			context.DrawRenderers(	cullingResults, ref drawingSettings, ref filteringSettings);
		}

		//
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
	void DrawDepth(ScriptableRenderContext context, Camera camera, Rect viewport, int face)
	{
		if (depthMaterial == null)
		{
			depthShader = Resources.Load("Shaders/CubemapDepth", typeof(Shader)) as Shader;
			if (depthShader != null)
			{
				depthMaterial = new Material(depthShader);
			}
			else
			{
				Debug.LogError("ComputeDepth.shader resource not found!");
				return;
			}
		}

		depthMaterial.SetInt("Face", face);

		var buffer = new CommandBuffer();
		buffer.BeginSample("Custom Depth CB");
		buffer.name = "Custom Depth CB";
		buffer.SetRenderTarget(camera.targetTexture.colorBuffer);
		buffer.SetViewport(viewport);
		buffer.SetGlobalTexture("DepthTexture", camera.targetTexture.depthBuffer);
		buffer.DrawProcedural(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, 6);
		buffer.EndSample("Custom Depth CB");
		context.ExecuteCommandBuffer(buffer);
		buffer.Release();
	}
	
	void EndCamera(ScriptableRenderContext context, Camera camera)
	{
		context.Submit();
	}
	void Clear(ScriptableRenderContext context,Color color)
	{
		var buffer = new CommandBuffer();
		buffer.ClearRenderTarget(true, true, color);
		context.ExecuteCommandBuffer(buffer);
		buffer.Release();
	}

	public void Render(ScriptableRenderContext context, Camera camera)
	{
		BeginCamera(context, camera);
		
		string samplename = camera.gameObject.name + " sample";
		StartSample(context, samplename);

		PrepareForSceneWindow(context, camera);
		DrawOpaqueGeometry(context, camera);
		DrawTransparentGeometry(context, camera);
		DrawUnsupportedShaders(context, camera);
		DrawGizmos(context, camera);
		EndSample(context, samplename);
		EndCamera(context, camera);
	}

	public void RenderToCubemap(ScriptableRenderContext context, Camera camera, uid clientID)
	{
		if (!camera.targetTexture)
		{
		 	Debug.LogError("The camera needs a target texture for rendering the cubemap");
			return;
		}

		for (int i = 0; i < NumFaces; ++i)
		{
			DrawCubemapFace(context, camera, i);
		}

		CasterMonitor monitor = CasterMonitor.GetCasterMonitor();
		if (clientID != 0 && monitor && monitor.casterSettings.isStreamingVideo)
		{
			Teleport_SceneCaptureComponent.videoEncoders[clientID].CreateEncodeCommands(context, camera);
		}
	}

	void DrawCubemapFace(ScriptableRenderContext context, Camera camera, int face)
	{
		int faceSize = camera.targetTexture.width / 3;
		int halfFaceSize = faceSize / 2;

		Color [] direction_colours = { new Color(.01F,0.0F,0.0F), new Color(.01F, 0.0F, 0.0F), new Color(0.0F, .005F, 0.0F), new Color(0.0F, .005F, 0.0F), new Color(0.0F, 0.0F, 0.01F), new Color(0.0F, 0.0F, 0.01F) };
		int offsetX = faceOffsets[face, 0];
		int offsetY = faceOffsets[face, 1];
		
		camera.pixelRect = new Rect(offsetX * faceSize, offsetY * faceSize, faceSize, faceSize);

		var depthViewport = new Rect(offsetX * halfFaceSize, (faceSize * 2) + (offsetY * halfFaceSize), halfFaceSize, halfFaceSize);

		CamView view = faceCamViews[face];
		Vector3 to = view.forward;
		Vector3 pos=camera.transform.position;

		camera.worldToCameraMatrix = Matrix4x4.Rotate(faceRotations[face]) * Matrix4x4.LookAt(Vector3.zero, to, view.up)* Matrix4x4.Translate(-pos);
		Matrix4x4 proj=camera.projectionMatrix;
		BeginCamera(context, camera);

		string samplename = camera.gameObject.name + " Face " + face;
		StartSample(context, samplename);

		PrepareForSceneWindow(context, camera);
		Clear(context,0*direction_colours[face]);
		DrawOpaqueGeometry(context, camera);
		DrawTransparentGeometry(context, camera);
		DrawDepth(context, camera, depthViewport, face);
		DrawUnsupportedShaders(context, camera);
		EndSample(context, samplename);
		EndCamera(context, camera);
	}

}
