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


	// Switch back and front because Unity view matrices have -z for forward
	static CamView frontCamView = new CamView(Vector3.forward, Vector3.right);
	static CamView backCamView = new CamView(Vector3.back, Vector3.right); 
	static CamView rightCamView = new CamView(Vector3.right, Vector3.down);
	static CamView leftCamView = new CamView(Vector3.left, Vector3.up);
	static CamView upCamView = new CamView(Vector3.up, Vector3.right);
	static CamView downCamView = new CamView(Vector3.down, Vector3.right);

	static CamView[] faceCamViews = new CamView[] { frontCamView, backCamView, rightCamView, leftCamView, upCamView, downCamView };

	// End cubemap members


	TeleportLighting teleportLighting = new TeleportLighting();

	RenderTexture cubemapTexture = null;
	static Shader depthShader = null;
	static Material depthMaterial = null;

	ComputeShader computeShader = null;
	int encodeCamKernel;
	int quantizationKernel;
	int encodeDepthKernel;
	TeleportSettings teleportSettings = null;

	public TeleportCameraRenderer()
	{
		teleportSettings = TeleportSettings.GetOrCreateSettings();
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
			//drawingSettings.enableInstancing = true;
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
		if(cullingResults.visibleLights.Length>1||lightingOrder.SecondLightIndex>=0)
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

		depthMaterial.SetTexture("DepthTexture", cubemapTexture, RenderTextureSubElement.Depth);

		var buffer = new CommandBuffer();
		buffer.name = "Custom Depth CB";
		buffer.SetRenderTarget(Teleport_SceneCaptureComponent.GetRenderingSceneCapture().sceneCaptureTexture);
		buffer.SetViewport(viewport);
		buffer.BeginSample(buffer.name);
		buffer.DrawProcedural(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, 6);
		buffer.EndSample(buffer.name);
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
		buffer.SetInvertCulling(false);
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
#if UNITY_EDITOR
		DrawUnsupportedShaders(context, camera);
#endif
		DrawGizmos(context, camera);
		EndSample(context, samplename);
		EndCamera(context, camera);
	}

	public void RenderToCubemap(ScriptableRenderContext context, Camera camera, uid clientID)
	{
		CasterMonitor monitor = CasterMonitor.GetCasterMonitor();
		RenderTexture sceneCaptureTexture = Teleport_SceneCaptureComponent.GetRenderingSceneCapture().sceneCaptureTexture;

		if (!sceneCaptureTexture)
		{
			Debug.LogError("The camera needs a target texture for rendering the cubemap");
			return;
		}

		if (!computeShader)
		{
			InitShaders();
		}

		if (!cubemapTexture)
		{
			int faceSize = (int)teleportSettings.casterSettings.captureCubeTextureSize;

			cubemapTexture = new RenderTexture(faceSize, faceSize, 24, sceneCaptureTexture.format, RenderTextureReadWrite.Default);
			cubemapTexture.name = "Video Encoder Texture";
			cubemapTexture.hideFlags = HideFlags.DontSave;
			cubemapTexture.dimension = TextureDimension.Tex2D;
			cubemapTexture.useMipMap = false;
			cubemapTexture.autoGenerateMips = false;
			cubemapTexture.enableRandomWrite = false;
			cubemapTexture.Create();
		}

		camera.targetTexture = cubemapTexture;

		for (int i = 0; i < NumFaces; ++i)
		{
			DrawCubemapFace(context, camera, i);
		}

		FinalizeSceneCaptureTexture(context, camera);

		if (clientID != 0 && monitor && teleportSettings.casterSettings.isStreamingVideo)
		{
			Teleport_SceneCaptureComponent.videoEncoders[clientID].CreateEncodeCommands(context, camera);
		}
	}

	void DrawCubemapFace(ScriptableRenderContext context, Camera camera, int face)
	{
		CasterMonitor monitor = CasterMonitor.GetCasterMonitor();
		int faceSize = (int)teleportSettings.casterSettings.captureCubeTextureSize;
		int halfFaceSize = faceSize / 2;

		Color [] direction_colours = { new Color(.5F,0.0F,0.0F), new Color(.01F, 0.0F, 0.0F), new Color(0.0F, .5F, 0.0F), new Color(0.0F, .005F, 0.0F), new Color(0.0F, 0.0F, 0.5F), new Color(0.0F, 0.0F, 0.01F) };
		int offsetX = faceOffsets[face, 0];
		int offsetY = faceOffsets[face, 1];

		var depthViewport = new Rect(offsetX * halfFaceSize, (faceSize * 2) + (offsetY * halfFaceSize), halfFaceSize, halfFaceSize);

		CamView camView = faceCamViews[face];
		Vector3 to = camView.forward;
		Vector3 pos=camera.transform.position;

		camera.transform.position = pos;
		camera.transform.LookAt(pos + to, camView.up);
		Matrix4x4 view = camera.transform.localToWorldMatrix;// new Matrix4x4(camera.transform.position, camera.transform.rotation, Vector4.one); //no scale...
		view = Matrix4x4.Inverse(view);
		view.m20 *= -1f;
		view.m21 *= -1f;
		view.m22 *= -1f;
		view.m23 *= -1f;

		camera.worldToCameraMatrix = view;
		//Render(context, camera);
		BeginCamera(context, camera);

		string samplename = camera.gameObject.name + " Face " + face;
		StartSample(context, samplename);

		PrepareForSceneWindow(context, camera);
		Clear(context,0*direction_colours[face]);
		DrawOpaqueGeometry(context, camera);
		DrawTransparentGeometry(context, camera);
		DrawDepth(context, camera, depthViewport, face);
#if UNITY_EDITOR
		DrawUnsupportedShaders(context, camera);
#endif
		QuantizeColor(context, camera, face);
		EndSample(context, samplename);
		EndCamera(context, camera);
	}

	private void FinalizeSceneCaptureTexture(ScriptableRenderContext context, Camera camera)
	{
		EncodeCameraPosition(context, camera);
		context.Submit();
	}

	private void InitShaders()
	{
		var shaderPath = "Shaders/ProjectCubemap";
		// NB: Do not include file extension when loading a shader
		computeShader = Resources.Load<ComputeShader>(shaderPath);
		if (!computeShader)
		{
			Debug.Log("Shader not found at path " + shaderPath + ".compute");
		}
		encodeCamKernel = computeShader.FindKernel("EncodeCameraPositionCS");
		quantizationKernel = computeShader.FindKernel("QuantizationCS");
		encodeDepthKernel = computeShader.FindKernel("EncodeDepthCS");
	}

	void QuantizeColor(ScriptableRenderContext context, Camera camera, int face)
	{
		var monitor = CasterMonitor.GetCasterMonitor();
		int faceSize = (int)teleportSettings.casterSettings.captureCubeTextureSize;

		const int THREADGROUP_SIZE = 32;
		int numThreadGroupsX = faceSize / THREADGROUP_SIZE;
		int numThreadGroupsY = faceSize / THREADGROUP_SIZE;

		var outputTexture = Teleport_SceneCaptureComponent.GetRenderingSceneCapture().sceneCaptureTexture;
		computeShader.SetTexture(quantizationKernel, "InputColorTexture", cubemapTexture);
		computeShader.SetTexture(quantizationKernel, "RWOutputColorTexture", outputTexture);
		computeShader.SetInt("Face", face);

		var buffer = new CommandBuffer();
		buffer.name = "Quantize Colour";
		buffer.DispatchCompute(computeShader, quantizationKernel, numThreadGroupsX, numThreadGroupsY, 1);
		context.ExecuteCommandBuffer(buffer);
		buffer.Release();
	}

	void EncodeDepth(ScriptableRenderContext context, Camera camera)
	{
		var monitor = CasterMonitor.GetCasterMonitor();
		int faceSize = (int)teleportSettings.casterSettings.captureCubeTextureSize;

		const int THREADGROUP_SIZE = 32;
		int numThreadGroupsX = (faceSize / 2) / THREADGROUP_SIZE;
		int numThreadGroupsY = (faceSize / 2) / THREADGROUP_SIZE;

		var buffer = new CommandBuffer();

		buffer.SetGlobalTexture("DepthTexture", camera.targetTexture.depthBuffer);

		computeShader.SetTexture(encodeDepthKernel, "RWOutputColorTexture", camera.targetTexture);
		//computeShader.SetTextureFromGlobal(encodeDepthKernel, "DepthTexture", "_CameraDepthTexture");
		
		computeShader.SetInts("DepthOffset", new Int32[2] { 0, faceSize * 2 });

		buffer.name = "Encode Depth";
		buffer.DispatchCompute(computeShader, encodeDepthKernel, numThreadGroupsX, numThreadGroupsY, 6);
		context.ExecuteCommandBuffer(buffer);
		buffer.Release();
	}

	void EncodeCameraPosition(ScriptableRenderContext context, Camera camera)
	{
		var monitor = CasterMonitor.GetCasterMonitor();

		int faceSize = (int)teleportSettings.casterSettings.captureCubeTextureSize;
		int size = faceSize * 3;

		var camTransform = camera.transform;
		float[] camPos = new float[3] { camTransform.position.x, camTransform.position.z, camTransform.position.y };

		var outputTexture = Teleport_SceneCaptureComponent.GetRenderingSceneCapture().sceneCaptureTexture;
		computeShader.SetTexture(encodeCamKernel, "RWOutputColorTexture", outputTexture);
		computeShader.SetInts("CamPosOffset", new Int32[2] { size - (32 * 4), size - (3 * 8) });
		computeShader.SetFloats("CubemapCameraPositionMetres", camPos);
		var buffer = new CommandBuffer();
		buffer.SetGlobalTexture("RWOutputColorTexture", Graphics.activeColorBuffer);
		buffer.name = "Encode Camera Position";
		buffer.DispatchCompute(computeShader, encodeCamKernel, 4, 1, 1);
		context.ExecuteCommandBuffer(buffer);
		buffer.Release();
	}

}
