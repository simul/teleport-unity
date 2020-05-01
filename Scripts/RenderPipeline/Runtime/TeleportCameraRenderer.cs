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

	// For culling only
	//static Quaternion frontQuat = Quaternion.identity;
	//static Quaternion backQuat = Quaternion.Euler(0, 180, 0);
	//static Quaternion rightQuat = Quaternion.Euler(0, 90, 0);
	//static Quaternion leftQuat = Quaternion.Euler(0, -90, 0);
	//static Quaternion upQuat = Quaternion.Euler(90, 0, 0);
	//static Quaternion downQuat = Quaternion.Euler(-90, 0, 0);

	//static Quaternion[] faceQuats = new Quaternion[] { frontQuat, backQuat, rightQuat, leftQuat, upQuat, downQuat };

	// To align with Unreal Engine
	static Quaternion frontFaceRot = Quaternion.Euler(0, 0, 0);
	static Quaternion backFaceRot = Quaternion.Euler(0, 0, 0);
	static Quaternion rightFaceRot = Quaternion.identity;
	static Quaternion leftFaceRot = Quaternion. Euler(0, 0, 180);
	static Quaternion upFaceRot = Quaternion.Euler(0, 0, -90);
	static Quaternion downFaceRot = Quaternion.Euler(0, 0, 90); 

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

	RenderTexture cubemapTexture;
	static Shader depthShader = null;
	static Material depthMaterial = null;

	ComputeShader computeShader = null;
	int encodeCamKernel;
	int quantizationKernel;
	int encodeDepthKernel;

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
				camera.clearFlags == CameraClearFlags.Color, camera.backgroundColor, 1.0f);
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
		teleportLighting.Setup(context, cullingResults);
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
		buffer.name = "Custom Depth CB";
		buffer.SetRenderTarget(Teleport_SceneCaptureComponent.GetRenderingSceneCapture().sceneCaptureTexture);
		buffer.SetGlobalTexture("DepthTexture", cubemapTexture.depthBuffer);
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
			int faceSize = (int)monitor.casterSettings.captureCubeTextureSize;

			cubemapTexture = new RenderTexture(faceSize, faceSize, 24, sceneCaptureTexture.format, RenderTextureReadWrite.Default);
			cubemapTexture.name = "Video Encoder Texture";
			cubemapTexture.hideFlags = HideFlags.DontSave;
			cubemapTexture.dimension = TextureDimension.Tex2D;
			cubemapTexture.useMipMap = false;
			cubemapTexture.autoGenerateMips = false;
			cubemapTexture.enableRandomWrite = false;
			cubemapTexture.Create();
		}

		camera.SetTargetBuffers(cubemapTexture.colorBuffer, cubemapTexture.depthBuffer);

		for (int i = 0; i < NumFaces; ++i)
		{
			DrawCubemapFace(context, camera, i);
		}

		FinalizeSceneCaptureTexture(context, camera);

		if (clientID != 0 && monitor && monitor.casterSettings.isStreamingVideo)
		{
			Teleport_SceneCaptureComponent.videoEncoders[clientID].CreateEncodeCommands(context, camera);
		}
	}

	void DrawCubemapFace(ScriptableRenderContext context, Camera camera, int face)
	{
		CasterMonitor monitor = CasterMonitor.GetCasterMonitor();
		int faceSize = (int)monitor.casterSettings.captureCubeTextureSize;
		int halfFaceSize = faceSize / 2;

		Color [] direction_colours = { new Color(.01F,0.0F,0.0F), new Color(.01F, 0.0F, 0.0F), new Color(0.0F, .005F, 0.0F), new Color(0.0F, .005F, 0.0F), new Color(0.0F, 0.0F, 0.01F), new Color(0.0F, 0.0F, 0.01F) };
		int offsetX = faceOffsets[face, 0];
		int offsetY = faceOffsets[face, 1];
		
		//camera.pixelRect = new Rect(offsetX * faceSize, offsetY * faceSize, faceSize, faceSize);

		var depthViewport = new Rect(offsetX * halfFaceSize, (faceSize * 2) + (offsetY * halfFaceSize), halfFaceSize, halfFaceSize);

		CamView view = faceCamViews[face];
		Vector3 pos=camera.transform.position;
		Vector3 from = pos;
		from.Set(0, 0, 0);
		Vector3 to = from + view.forward * 10;

		camera.worldToCameraMatrix = Matrix4x4.Rotate(faceRotations[face]) * Matrix4x4.LookAt(from, to, view.up)* Matrix4x4.Translate(-pos);
		Matrix4x4 proj=camera.projectionMatrix;
		BeginCamera(context, camera);

		string samplename = camera.gameObject.name + " Face " + face;
		StartSample(context, samplename);

		PrepareForSceneWindow(context, camera);
		Clear(context, direction_colours[face]);
		DrawOpaqueGeometry(context, camera);
		DrawTransparentGeometry(context, camera);
		DrawDepth(context, camera, depthViewport, face);
		DrawUnsupportedShaders(context, camera);
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
		int faceSize = (int)monitor.casterSettings.captureCubeTextureSize;
		//int size = faceSize * 3;

		const int THREADGROUP_SIZE = 32;
		int numThreadGroupsX = faceSize / THREADGROUP_SIZE;
		int numThreadGroupsY = faceSize / THREADGROUP_SIZE;

		var outputTexture = Teleport_SceneCaptureComponent.GetRenderingSceneCapture().sceneCaptureTexture;
		computeShader.SetTexture(quantizationKernel, "InputColorTexture", cubemapTexture);
		computeShader.SetTexture(quantizationKernel, "RWOutputColorTexture", outputTexture);
		computeShader.SetInt("Face", face);

		var buffer = new CommandBuffer();
		//buffer.SetGlobalTexture("RWOutputColorTexture", Graphics.activeColorBuffer);
		buffer.name = "Quantize Colour";
		buffer.DispatchCompute(computeShader, quantizationKernel, numThreadGroupsX, numThreadGroupsY, 1);
		context.ExecuteCommandBuffer(buffer);
		buffer.Release();
	}

	void EncodeDepth(ScriptableRenderContext context, Camera camera)
	{
		var monitor = CasterMonitor.GetCasterMonitor();
		int faceSize = (int)monitor.casterSettings.captureCubeTextureSize;

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

		int faceSize = (int)monitor.casterSettings.captureCubeTextureSize;
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
