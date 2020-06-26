using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Conditional = System.Diagnostics.ConditionalAttribute;

using teleport;

using uid = System.UInt64;
using System.Security.AccessControl;

namespace teleport
{
	public partial class TeleportCameraRenderer
	{
		public TeleportRenderSettings renderSettings = null;
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

		static int _CameraColorTexture = Shader.PropertyToID("_CameraColorTexture"),
					_CameraDepthAttachment = Shader.PropertyToID("_CameraDepthAttachment"),
					_CameraDepthTexture = Shader.PropertyToID("_CameraDepthTexture"),
					_CameraOpaqueTexture = Shader.PropertyToID("_CameraOpaqueTexture"),
					_AfterPostProcessTexture = Shader.PropertyToID("_AfterPostProcessTexture"),
					_InternalGradingLut = Shader.PropertyToID("_InternalGradingLut"),
					_GrabTexture = Shader.PropertyToID("_GrabTexture"),
					_GrabTexture_HDR = Shader.PropertyToID("_GrabTexture_HDR"),
					_GrabTexture_TexelSize = Shader.PropertyToID("_GrabTexture_TexelSize"),
					_GrabTexture_ST = Shader.PropertyToID("_GrabTexture_ST"),
					unity_LightShadowBias = Shader.PropertyToID("unity_LightShadowBias");
		static Shader depthShader = null;
		static Material depthMaterial = null;
		static Shader shadowShader = null;
		static Material shadowMaterial = null;

		ComputeShader computeShader = null;
		int encodeTagIdKernel;
		int encodeColorKernel;

		public TeleportSettings teleportSettings = null;

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
			if (camera.clearFlags != CameraClearFlags.Nothing)
			{
				var buffer = new CommandBuffer { name = camera.name + " Teleport BeginCamera" };
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
			if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
			{
				p.shadowDistance = QualitySettings.shadowDistance;//renderSettings.maxShadowDistance;
				p.shadowDistance = Mathf.Min(p.shadowDistance, camera.farClipPlane);
				cullingResults = context.Cull(ref p);
				return true;
			}
			cullingResults = new CullingResults();
			return false;
		}
		// A ShaderTagId is ACTUALLY the Tag "LightMode" in the .shader file.
		// It allows us to render all shaders that have a specific Pass defined with that tag.
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
		static ShaderTagId[] depthShaderTagIds = {
				new ShaderTagId("ShadowCaster"),
		};
		void DrawDepthPass(ScriptableRenderContext context, Camera camera)
		{
			context.SetupCameraProperties(camera);
			CommandBuffer buffer = CommandBufferPool.Get("Depth Pass");
			buffer.BeginSample("Depth");
			buffer.GetTemporaryRT(_CameraDepthTexture, camera.pixelWidth, camera.pixelHeight, 32, FilterMode.Trilinear, RenderTextureFormat.Depth);
			buffer.SetRenderTarget(_CameraDepthTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
			buffer.ClearRenderTarget(true, true, Color.clear);

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
			var drawingSettings = new DrawingSettings(depthShaderTagIds[0], sortingSettings);
			drawingSettings.enableDynamicBatching = true;
			drawingSettings.enableInstancing = true;
			TeleportRenderPipeline.LightingOrder lightingOrder = TeleportRenderPipeline.GetLightingOrder(cullingResults.visibleLights);
			//teleportLighting.SetupForwardBasePass(context, cullingResults, lightingOrder);
			CoreUtils.SetKeyword(buffer, "SHADOWS_DEPTH", true);
			Vector4 lightShadowBias = new Vector4(0F, 0F, 0F, 0F);
			//Vector4 lightShadowBias = new Vector4(-0.001553151F, 1.0F, 0.032755F, 0F);
			buffer.SetGlobalVector(unity_LightShadowBias, lightShadowBias);
			for (int i = 1; i < depthShaderTagIds.Length; i++)
			{
				drawingSettings.SetShaderPassName(i, depthShaderTagIds[i]);
			}
			buffer.EndSample("Depth");
			context.ExecuteCommandBuffer(buffer);
			context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

			CommandBufferPool.Release(buffer);
		}
		void DrawShadowPass(ScriptableRenderContext context, Camera camera, CullingResults cullingResults, TeleportRenderPipeline.LightingOrder lightingOrder)
		{
			teleportLighting.RenderScreenspaceShadows(context, camera, cullingResults);

		}

		void DrawOpaqueGeometry(ScriptableRenderContext context, Camera camera)
		{
			// The generic textures accessible from all default shaders...
			var buffer = new CommandBuffer();
			buffer.SetGlobalTexture(_CameraColorTexture, Texture2D.whiteTexture);
			buffer.SetGlobalTexture(_CameraDepthAttachment, Texture2D.whiteTexture);
			//buffer.SetGlobalTexture(_CameraDepthTexture, Texture2D.whiteTexture);
			buffer.SetGlobalTexture(_CameraOpaqueTexture, Texture2D.whiteTexture);
			buffer.SetGlobalTexture(_AfterPostProcessTexture, Texture2D.whiteTexture);
			buffer.SetGlobalTexture(_InternalGradingLut, Texture2D.whiteTexture);

			buffer.SetGlobalTexture(_GrabTexture, Texture2D.whiteTexture);
			//buffer.SetGlobalTexture(_GrabTexture_HDR, Texture2D.whiteTexture);
			//buffer.SetGlobalTexture(_GrabTexture_ST, Texture2D.whiteTexture);
			//_GrabTexture_TexelSize = Shader.PropertyToID("_GrabTexture_TexelSize"),
			context.ExecuteCommandBuffer(buffer);
			buffer.Release();

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
			teleportLighting.renderSettings = renderSettings;
			//nity.Collections.NativeArray<VisibleLight> visibleLights = renderingData.lightData.visibleLights;
			//or (int i = 0; i < visibleLights.Length; ++i)
			//
			//	// Skip main directional light as it is not packed into the shadow atlas
			//	if (i == renderingData.lightData.mainLightIndex)
			//	{
			//		continue;
			//	}
			//	renderingData.cullResults.GetShadowCasterBounds(i, out var bounds);
			//
			TeleportRenderPipeline.LightingOrder lightingOrder = TeleportRenderPipeline.GetLightingOrder(cullingResults.visibleLights);

			var drawingSettings = new DrawingSettings(legacyShaderTagIds[0], sortingSettings);
			drawingSettings.mainLightIndex = lightingOrder.MainLightIndex;

			drawingSettings.enableDynamicBatching = true;
			drawingSettings.enableInstancing = true;
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

			if (cullingResults.visibleLights.Length > 1 || lightingOrder.SecondLightIndex >= 0)
			{
				var drawingSettings2 = new DrawingSettings(addLightShaderTagIds[0], sortingSettings);
				drawingSettings2.mainLightIndex = 0;
				drawingSettings2.enableDynamicBatching = true;
				drawingSettings2.enableInstancing = true;
				teleportLighting.SetupForwardAddPass(context, cullingResults, lightingOrder);
				for (int i = 0; i < addLightShaderTagIds.Length; i++)
				{
					drawingSettings2.SetShaderPassName(i, addLightShaderTagIds[i]);
				}
				context.DrawRenderers(cullingResults, ref drawingSettings2, ref filteringSettings);
			}
			teleportLighting.Cleanup(context);
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
			drawingSettings.enableDynamicBatching = true;
			drawingSettings.enableInstancing = true;
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

			var captureTexture = Teleport_SceneCaptureComponent.RenderingSceneCapture.RendererTexture;
			depthMaterial.SetTexture("DepthTexture", captureTexture, RenderTextureSubElement.Depth);

			var buffer = new CommandBuffer();
			buffer.name = "Custom Depth CB";
			buffer.SetRenderTarget(Teleport_SceneCaptureComponent.RenderingSceneCapture.sceneCaptureTexture);
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
		void Clear(ScriptableRenderContext context, Color color)
		{
			var buffer = new CommandBuffer();
			buffer.ClearRenderTarget(true, true, color);
			buffer.SetInvertCulling(false);
			context.ExecuteCommandBuffer(buffer);
			buffer.Release();
		}

		void RenderShadows(ScriptableRenderContext context, Camera camera)
		{
			var filteringSettings = new FilteringSettings(RenderQueueRange.all);
			CullingResults cullingResults;
			if (!Cull(context, camera, out cullingResults))
			{
				return;
			}
			TeleportRenderPipeline.LightingOrder lightingOrder = TeleportRenderPipeline.GetLightingOrder(cullingResults.visibleLights);
			teleportLighting.renderSettings = renderSettings;
			teleportLighting.RenderShadows(context, cullingResults, lightingOrder);
			DrawShadowPass(context, camera, cullingResults, lightingOrder);
		}
		public void Render(ScriptableRenderContext context, Camera camera)
		{
			string samplename = camera.gameObject.name + " sample";
			StartSample(context, samplename);
			DrawDepthPass(context, camera);
			RenderShadows(context, camera);

			BeginCamera(context, camera);
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
		public void RenderToSceneCapture(ScriptableRenderContext context, Camera camera)
		{
			if (teleportSettings.casterSettings.usePerspectiveRendering)
			{
				RenderToSceneCapture2D(context, camera);
			}
			else
			{
				RenderToSceneCaptureCubemap(context, camera);
			}
		}

		public void RenderToSceneCapture2D(ScriptableRenderContext context, Camera camera)
		{
			RenderTexture sceneCaptureTexture = Teleport_SceneCaptureComponent.RenderingSceneCapture.sceneCaptureTexture;

			if (!sceneCaptureTexture)
			{
				Debug.LogError("The video encoder texture must not be null");
				return;
			}
			PrepareForRenderToSceneCapture();

			Render(context, camera);
			EncodeColor(context, camera, 0);
			context.Submit();

			var videoEncoder = Teleport_SceneCaptureComponent.RenderingSceneCapture.VideoEncoder;
			if (teleportSettings.casterSettings.isStreamingVideo && videoEncoder != null)
			{
				videoEncoder.CreateEncodeCommands(context, camera);
			}
		}
		public void RenderToSceneCaptureCubemap(ScriptableRenderContext context, Camera camera)
		{
			RenderTexture sceneCaptureTexture = Teleport_SceneCaptureComponent.RenderingSceneCapture.sceneCaptureTexture;

			if (!sceneCaptureTexture)
			{
				Debug.LogError("The video encoder texture must not be null");
				return;
			}

			PrepareForRenderToSceneCapture();

			camera.targetTexture = Teleport_SceneCaptureComponent.RenderingSceneCapture.RendererTexture;
			uid clientID = Teleport_SceneCaptureComponent.RenderingSceneCapture.clientID;
			if (clientID != 0)
				UpdateStreamables(context, clientID, camera);
			for (int i = 0; i < NumFaces; ++i)
			{
				DrawCubemapFace(context, camera, i);
			}

			FinalizeSceneCaptureTexture(context, camera);

			var videoEncoder = Teleport_SceneCaptureComponent.RenderingSceneCapture.VideoEncoder;
			if (teleportSettings.casterSettings.isStreamingVideo && videoEncoder != null)
			{
				videoEncoder.CreateEncodeCommands(context, camera);
			}
		}
		void PrepareForRenderToSceneCapture()
		{
			if (!computeShader)
			{
				InitShaders();
			}
		}
		// This function leverages the Unity rendering pipeline functionality to get information about what lights etc should be visible to the client.
		void UpdateStreamables(ScriptableRenderContext context, uid clientID, Camera camera)
		{
			camera.fieldOfView = 180.0F;
			camera.aspect = 2.0F;
			float farClipPlane = camera.farClipPlane;
			camera.farClipPlane = 10.0F;
			ScriptableCullingParameters p;
			if (camera.TryGetCullingParameters(out p))
			{
				CasterMonitor monitor = CasterMonitor.GetCasterMonitor();
				CullingResults cullingResults = context.Cull(ref p);
				Light[] lights = new Light[cullingResults.visibleLights.Length];
				for (int i = 0; i < lights.Length; i++)
				{
					lights[i] = cullingResults.visibleLights[i].light;
				}
				Teleport_SessionComponent.sessions[clientID].SetVisibleLights(lights);
			}
			camera.fieldOfView = 90.0F;
			camera.farClipPlane = farClipPlane;
			camera.aspect = 1.0F;
		}
		void DrawCubemapFace(ScriptableRenderContext context, Camera camera, int face)
		{
			CasterMonitor monitor = CasterMonitor.GetCasterMonitor();
			int faceSize = (int)teleportSettings.casterSettings.captureCubeTextureSize;
			int halfFaceSize = faceSize / 2;

			Color[] direction_colours = { new Color(.5F, 0.0F, 0.0F), new Color(.01F, 0.0F, 0.0F), new Color(0.0F, .5F, 0.0F), new Color(0.0F, .005F, 0.0F), new Color(0.0F, 0.0F, 0.5F), new Color(0.0F, 0.0F, 0.01F) };
			int offsetX = faceOffsets[face, 0];
			int offsetY = faceOffsets[face, 1];

			var depthViewport = new Rect(offsetX * halfFaceSize, (faceSize * 2) + (offsetY * halfFaceSize), halfFaceSize, halfFaceSize);

			CamView camView = faceCamViews[face];
			Vector3 to = camView.forward;
			Vector3 pos = camera.transform.position;

			camera.transform.position = pos;
			camera.transform.LookAt(pos + to, camView.up);
			Matrix4x4 view = camera.transform.localToWorldMatrix;// new Matrix4x4(camera.transform.position, camera.transform.rotation, Vector4.one); //no scale...
			view = Matrix4x4.Inverse(view);
			view.m20 *= -1f;
			view.m21 *= -1f;
			view.m22 *= -1f;
			view.m23 *= -1f;

			camera.worldToCameraMatrix = view;

			string samplename = camera.gameObject.name + " Face " + face;
			StartSample(context, samplename);
			{
				DrawDepthPass(context, camera);
				RenderShadows(context, camera);

				BeginCamera(context, camera);

				PrepareForSceneWindow(context, camera);
				Clear(context, 0 * direction_colours[face]);
				DrawOpaqueGeometry(context, camera);
				DrawTransparentGeometry(context, camera);
				EncodeColor(context, camera, face);
				EncodeDepth(context, camera, depthViewport, face);
#if UNITY_EDITOR
				DrawUnsupportedShaders(context, camera);
#endif
			}
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
			encodeTagIdKernel = computeShader.FindKernel("EncodeTagDataIdCS");
			encodeColorKernel = computeShader.FindKernel("EncodeColorCS");
		}

		void EncodeColor(ScriptableRenderContext context, Camera camera, int face)
		{
			var monitor = CasterMonitor.GetCasterMonitor();
			int faceSize = (int)teleportSettings.casterSettings.captureCubeTextureSize;

			const int THREADGROUP_SIZE = 32;
			int numThreadGroupsX = faceSize / THREADGROUP_SIZE;
			int numThreadGroupsY = faceSize / THREADGROUP_SIZE;

			var outputTexture = Teleport_SceneCaptureComponent.RenderingSceneCapture.sceneCaptureTexture;
			var captureTexture = Teleport_SceneCaptureComponent.RenderingSceneCapture.RendererTexture;
			computeShader.SetTexture(encodeColorKernel, "InputColorTexture", captureTexture);
			computeShader.SetTexture(encodeColorKernel, "RWOutputColorTexture", outputTexture);
			computeShader.SetInt("Face", face);

			var buffer = new CommandBuffer();
			buffer.name = "Encode Color";
			buffer.DispatchCompute(computeShader, encodeColorKernel, numThreadGroupsX, numThreadGroupsY, 1);
			context.ExecuteCommandBuffer(buffer);
			buffer.Release();
		}

		void EncodeDepth(ScriptableRenderContext context, Camera camera, Rect viewport, int face)
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

			var captureTexture = Teleport_SceneCaptureComponent.RenderingSceneCapture.RendererTexture;
			depthMaterial.SetTexture("DepthTexture", captureTexture, RenderTextureSubElement.Depth);

			var buffer = new CommandBuffer();
			buffer.name = "Custom Depth CB";
			buffer.SetRenderTarget(Teleport_SceneCaptureComponent.RenderingSceneCapture.sceneCaptureTexture);
			buffer.SetViewport(viewport);
			buffer.BeginSample(buffer.name);
			buffer.DrawProcedural(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, 6);
			buffer.EndSample(buffer.name);
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

			var outputTexture = Teleport_SceneCaptureComponent.RenderingSceneCapture.sceneCaptureTexture;
			computeShader.SetTexture(encodeTagIdKernel, "RWOutputColorTexture", outputTexture);
			computeShader.SetInts("CamPosOffset", new Int32[2] { size - (32 * 4), size - (3 * 8) });
			computeShader.SetFloats("CubemapCameraPositionMetres", camPos);
			var buffer = new CommandBuffer();
			buffer.name = "Encode Camera Position";
			buffer.DispatchCompute(computeShader, encodeTagIdKernel, 4, 1, 1);
			context.ExecuteCommandBuffer(buffer);
			buffer.Release();
		}

		// Encodes the id of the video tag data in 4x4 blocks of monochrome colour.
		void EncodeTagID(ScriptableRenderContext context, Camera camera)
		{
			var videoEncoder = Teleport_SceneCaptureComponent.RenderingSceneCapture.VideoEncoder;
			if (videoEncoder != null)
			{
				var monitor = CasterMonitor.GetCasterMonitor();

				int faceSize = (int)teleportSettings.casterSettings.captureCubeTextureSize;
				int size = faceSize * 3;


				var outputTexture = Teleport_SceneCaptureComponent.RenderingSceneCapture.sceneCaptureTexture;
				computeShader.SetTexture(encodeTagIdKernel, "RWOutputColorTexture", outputTexture);
				computeShader.SetInts("TagDataIdOffset", new Int32[2] { size - (32 * 4), size - 4 });
				computeShader.SetFloats("TagDataId", videoEncoder.CurrentTagID);
				var buffer = new CommandBuffer();
				buffer.name = "Encode Camera Position";
				buffer.DispatchCompute(computeShader, encodeTagIdKernel, 4, 1, 1);
				context.ExecuteCommandBuffer(buffer);
				buffer.Release();
			}		
		}
	}
}