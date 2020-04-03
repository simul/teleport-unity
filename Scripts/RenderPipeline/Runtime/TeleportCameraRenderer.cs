using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

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
	static int[,] glFaceOffsets = new int[6, 2] { { 0, 2 }, { 1, 2 }, { 2, 2 }, { 0, 1 }, { 1, 1 }, { 2, 1 } };

	static int[,] nonGLFaceOffsets = new int[6, 2] { { 0, 0 }, { 1, 0 }, { 2, 0 }, { 0, 1 }, { 1, 1 }, { 2, 1 } };

	// For culling only
	static Quaternion frontQuat = Quaternion.identity;
	static Quaternion backQuat = Quaternion.Euler(0, 180, 0);
	static Quaternion rightQuat = Quaternion.Euler(0, 90, 0);
	static Quaternion leftQuat = Quaternion.Euler(0, -90, 0);
	static Quaternion upQuat = Quaternion.Euler(90, 0, 0);
	static Quaternion downQuat = Quaternion.Euler(-90, 0, 0);

	static Quaternion[] faceQuats = new Quaternion[] { frontQuat, backQuat, rightQuat, leftQuat, upQuat, downQuat };

	// To align with Unreal Engine
	static Quaternion frontFaceRot = Quaternion.Euler(0, 0, 90);
	static Quaternion backFaceRot = Quaternion.Euler(0, 0, -90);
	static Quaternion rightFaceRot = Quaternion.Euler(0, 0, 180);
	static Quaternion leftFaceRot = Quaternion.identity;
	static Quaternion upFaceRot = Quaternion.Euler(0, 0, -90);
	static Quaternion downFaceRot = Quaternion.Euler(0, 0, 90); 

	static Quaternion[] faceRotations = new Quaternion[] { frontFaceRot, backFaceRot, rightFaceRot, leftFaceRot, upFaceRot, downFaceRot };

	// Switch back and front because Unity view matrices have -z for forward
	static CamView frontCamView = new CamView(Vector3.back, Vector3.up);
	static CamView backCamView = new CamView(Vector3.forward, Vector3.up);
	static CamView rightCamView = new CamView(Vector3.right, Vector3.up);
	static CamView leftCamView = new CamView(Vector3.left, Vector3.up);
	static CamView upCamView = new CamView(Vector3.up, Vector3.back);
	static CamView downCamView = new CamView(Vector3.down, Vector3.forward);

	static CamView[] faceCamViews = new CamView[] { frontCamView, backCamView, rightCamView, leftCamView, upCamView, downCamView };

	// End cubemap members


	TeleportLighting lighting = new TeleportLighting();

	


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
		StartSample(context, samplename);

		PrepareForSceneWindow(context, camera);
		DrawOpaqueGeometry(context, camera);
		DrawTransparentGeometry(context, camera);
		DrawUnsupportedShaders(context, camera);
		DrawGizmos(context, camera);
		EndSample(context, samplename);
		EndCamera(context, camera);
	}

	public void RenderToCubemap(ScriptableRenderContext context, Camera camera, uid clientID, bool streamCubemap = true)
	{
		if (!camera.targetTexture)
		{
			Debug.LogError("The camera needs a target texture for rendering the cubemap");
			return;
		}

		for (int i = 0; i < NumFaces; ++i)
		{
			if (streamCubemap && i == NumFaces - 1)
			{
				Teleport_SceneCaptureComponent.videoEncoders[clientID].CreateEncodeCommands(camera);
			}
			DrawCubemapFace(context, camera, i);
		}
	}

	void DrawCubemapFace(ScriptableRenderContext context, Camera camera, int face)
	{
		int[,] faceOffsets;

		if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan)
		{
			faceOffsets = glFaceOffsets;
		}
		else
		{
			faceOffsets = nonGLFaceOffsets;
		}

		int offsetX = faceOffsets[face, 0];
		int offsetY = faceOffsets[face, 1];

		int faceSize = camera.targetTexture.width / 3;

		camera.pixelRect = new Rect(offsetX * faceSize, offsetY * faceSize, faceSize, faceSize);

		CamView view = faceCamViews[face];
		Vector3 to = camera.transform.position + view.forward * 10;

		camera.worldToCameraMatrix = Matrix4x4.Rotate(faceRotations[face]) * Matrix4x4.LookAt(camera.transform.position, to, view.up);

		BeginCamera(context, camera);
		PrepareForSceneWindow(context, camera);
		DrawOpaqueGeometry(context, camera);
		DrawTransparentGeometry(context, camera);
		EndCamera(context, camera);
	}
 
}
