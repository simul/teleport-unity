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
	/// <summary>
	/// A class to handle generating the lighting and reflection cubemaps.
	/// </summary>
	public class VideoEncoding
	{
		public static Shader cubemapShader = null;
		public static Material createLightingCubemapMaterial = null;
		public static Material encodeLightingCubemapMaterial = null; 
		static Mesh fullscreenMesh = null;
		static Shader depthShader = null;
		static Material depthMaterial = null;
		public ComputeShader computeShader = null;
		public int encodeTagIdKernel = -1;
		public int encodeColorKernel = -1;
		public int encodeCubemapFaceKernel = -1;
		public int encodeWebcamKernel = -1;
		//int downCopyFaceKernel = 0;
		const int THREADGROUP_SIZE = 32;

		public static int NumFaces = 6;
		public static int[,] faceOffsets = new int[6, 2] { { 0, 0 }, { 1, 0 }, { 2, 0 }, { 0, 1 }, { 1, 1 }, { 2, 1 } };

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
			encodeCubemapFaceKernel = computeShader.FindKernel("EncodeCubemapFaceCS");
			encodeWebcamKernel = computeShader.FindKernel("EncodeWebcamCS");

		//downCopyFaceKernel = computeShader.FindKernel("DownCopyFaceCS");
	}
		public void EnsureMaterial(ref Material m,ref Shader s,string shaderName)
		{
			if (m == null)
			{
				s = Shader.Find("Teleport/CopyCubemap");
				if (s != null)
				{
					m = new Material(s);
				}
				else
				{
					Debug.LogError("CopyCubemap.shader resource not found!");
					return;
				}
			}
		}
		/// <summary>
		/// Copy from the full size texture to the cubemaps, starting with the reflection cube.
		/// </summary>
		public void DrawCubemaps(ScriptableRenderContext context, RenderTexture sourceTexture, RenderTexture outputTexture, int face)
		{
			// Ordinarily, we would use a compute shader to downcopy from the full-size cubemap face to the smaller specular cubemaps.
			// But Unity can't handle treating a cubemap as a 2D Texture array, so we'll have to make do with a vertex/pixel shader copy.
			// Copying from the render texture to the current face of the specular cube texture.
			if (fullscreenMesh == null)
			{
				teleport.RenderingUtils.FullScreenMeshStruct fullScreenMeshStruct = new teleport.RenderingUtils.FullScreenMeshStruct();
				fullScreenMeshStruct.horizontal_fov_degrees = 90.0F;
				fullScreenMeshStruct.vertical_fov_degrees = 90.0F;
				fullScreenMeshStruct.far_plane_distance = 1000.0F;
				fullscreenMesh = teleport.RenderingUtils.CreateFullscreenMesh(fullScreenMeshStruct);
			}
			EnsureMaterial(ref encodeLightingCubemapMaterial,ref cubemapShader, "Teleport/CopyCubemap");
			EnsureMaterial(ref createLightingCubemapMaterial,ref cubemapShader, "Teleport/CopyCubemap");

			var buffer = new CommandBuffer();
			buffer.name = "Copy Cubemap Face";
			buffer.SetRenderTarget(outputTexture, 0, (CubemapFace)face);
			createLightingCubemapMaterial.SetTexture("_SourceTexture", sourceTexture);
			buffer.DrawMesh(fullscreenMesh, Matrix4x4.identity, createLightingCubemapMaterial, 0, 0);
			context.ExecuteCommandBuffer(buffer);
			buffer.Release();
		}
		static float RoughnessFromMip(float mip, float numMips)
		{
			double roughness_mip_scale = 1.2;
			double C = (3.0  + mip - numMips) / roughness_mip_scale;
			return (float)Math.Pow(Math.Exp(C), 2.0);
			//return (float)Math.Pow(Math.Exp((3.0 + mip - numMips) / roughness_mip_scale), 2.0);
		}

		/// <summary>
		/// For a specular cubemap, we render the lower-detail mipmaps to represent reflections for rougher materials.
		/// We SHOULD here be rendering down from higher to lower detail mips of the same cubemap.
		/// But Unity is missing the necessary API functions to render with one mip of input and one as output of the same texture.
		/// So instead we will render directly to the video texture.
		/// </summary>
		public void SpecularRoughnessMip(CommandBuffer buffer, RenderTexture SourceCubeTexture, RenderTexture SpecularCubeTexture, int face, int MipIndex, int mipOffset)
		{
			float roughness =  RoughnessFromMip((float)( MipIndex+ mipOffset), (float)( SpecularCubeTexture.mipmapCount));
			int w = SpecularCubeTexture.width << MipIndex;
			// Render to mip MipIndex...
			buffer.SetRenderTarget(SpecularCubeTexture, MipIndex, (CubemapFace)face);

			// We WOULD be sending roughness this way...
			// but Unity can't cope with changing the value between Draw calls...
			//createLightingCubemapMaterial.SetFloat("Roughness", roughness);

			// Instead we must user buffer.SetGlobalFloat etc.
			buffer.SetGlobalFloat("Roughness", roughness);
			buffer.SetGlobalInt("MipIndex", MipIndex);
			buffer.SetGlobalInt("NumMips", SpecularCubeTexture.mipmapCount);
			buffer.SetGlobalInt("Face", face);
			// But SetGlobalTexture() doesn't work - we must instead put it in the material, here:
			createLightingCubemapMaterial.SetTexture("_SourceCubemapTexture", SourceCubeTexture);
			// 
			buffer.DrawProcedural(Matrix4x4.identity, createLightingCubemapMaterial, 1, MeshTopology.Triangles,6);
		}
		public void GenerateSpecularMips(ScriptableRenderContext context, RenderTexture SourceCubeTexture, RenderTexture SpecularCubeTexture, int face,int mip_offset)
		{
			var buffer = new CommandBuffer();
			buffer.name = "Generate Specular Mips";

			// For perspective rendering
			EnsureMaterial(ref createLightingCubemapMaterial, ref cubemapShader, "Teleport/CopyCubemap");
			EnsureMaterial(ref encodeLightingCubemapMaterial, ref cubemapShader, "Teleport/CopyCubemap");

			// Only do 3 mips.
			for (int i = 0; i < SpecularCubeTexture.mipmapCount; i++)
			{
				SpecularRoughnessMip(buffer, SourceCubeTexture, SpecularCubeTexture, face,i,mip_offset);
			}
			context.ExecuteCommandBuffer(buffer);
			buffer.Release();
		}
		public void GenerateDiffuseCubemap(ScriptableRenderContext context, RenderTexture SourceCubeTexture, RenderTexture DiffuseCubeTexture, int face)
		{
			var buffer = new CommandBuffer();
			buffer.name = "Diffuse";

			buffer.SetRenderTarget(DiffuseCubeTexture, 0, (CubemapFace)face);
			buffer.SetGlobalInt("MipIndex", 0);
			buffer.SetGlobalInt("NumMips", 1);
			buffer.SetGlobalInt("Face", face);
			buffer.SetGlobalFloat("Roughness", 1.0F);
			buffer.SetGlobalTexture("_SourceCubemapTexture", SourceCubeTexture);
			// 
			buffer.DrawProcedural(Matrix4x4.identity, createLightingCubemapMaterial,3, MeshTopology.Triangles, 6);

			context.ExecuteCommandBuffer(buffer);
			buffer.Release();
		}
		public void EncodeColor(ScriptableRenderContext context, Camera camera, int face)
		{
			if (!computeShader)
				InitShaders();
			var outputTexture = Teleport_SceneCaptureComponent.RenderingSceneCapture.videoTexture;
			var captureTexture = Teleport_SceneCaptureComponent.RenderingSceneCapture.rendererTexture;

			int numThreadGroupsX = captureTexture.width / THREADGROUP_SIZE; 
			int numThreadGroupsY = captureTexture.height / THREADGROUP_SIZE;

			computeShader.SetTexture(encodeColorKernel, "InputColorTexture", captureTexture);
			computeShader.SetTexture(encodeColorKernel, "RWOutputColorTexture", outputTexture);
			computeShader.SetInt("Face", face);
			int[] offset = { 0,0 };
			computeShader.SetInts("Offset", offset);

			var buffer = new CommandBuffer();
			buffer.name = "Encode Color";
			buffer.DispatchCompute(computeShader, encodeColorKernel, numThreadGroupsX, numThreadGroupsY, 1);
			context.ExecuteCommandBuffer(buffer);
			buffer.Release();
		}

		public void EncodeDepth(ScriptableRenderContext context, Camera camera, Rect viewport)
		{
			if (!computeShader)
				InitShaders();
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

			var captureTexture = Teleport_SceneCaptureComponent.RenderingSceneCapture.rendererTexture;

			var buffer = new CommandBuffer();
			depthMaterial.SetTexture("DepthTexture", captureTexture, RenderTextureSubElement.Depth);
			buffer.name = "Custom Depth CB";
			buffer.SetRenderTarget(Teleport_SceneCaptureComponent.RenderingSceneCapture.videoTexture);
			buffer.SetViewport(viewport);
			buffer.BeginSample(buffer.name);
			buffer.DrawProcedural(Matrix4x4.identity, depthMaterial, 0, MeshTopology.Triangles, 6);
			buffer.EndSample(buffer.name);
			context.ExecuteCommandBuffer(buffer);
			buffer.Release();
		}
		// Encodes the id of the video tag data in 4x4 blocks of monochrome colour.
		public void EncodeTagID(ScriptableRenderContext context, Camera camera)
		{
			if (!computeShader)
				InitShaders();
			var tagDataID = Teleport_SceneCaptureComponent.RenderingSceneCapture.CurrentTagID;

			var outputTexture = Teleport_SceneCaptureComponent.RenderingSceneCapture.videoTexture;
			computeShader.SetTexture(encodeTagIdKernel, "RWOutputColorTexture", outputTexture);
			computeShader.SetInts("TagDataIdOffset", new Int32[2] { outputTexture.width - (32 * 4), outputTexture.height - 4 });
			computeShader.SetInt("TagDataId", (int)tagDataID);
			var buffer = new CommandBuffer();
			buffer.name = "Encode Camera Position";
			buffer.DispatchCompute(computeShader, encodeTagIdKernel, 4, 1, 1);
			context.ExecuteCommandBuffer(buffer);
			buffer.Release();
		}

		public void EncodeWebcam(ScriptableRenderContext context, Camera camera, Vector2Int offset)
		{
			if (!computeShader)
				InitShaders();
			
			var inputTexture = Teleport_SceneCaptureComponent.RenderingSceneCapture.webcamTexture;

			// Will be null if not streaming webcam.
			if (!inputTexture)
			{
				return;
			}

			var outputTexture = Teleport_SceneCaptureComponent.RenderingSceneCapture.videoTexture;

			TeleportSettings teleportSettings = TeleportSettings.GetOrCreateSettings();

			int faceSize = (int)teleportSettings.casterSettings.captureCubeTextureSize;
			int halfFaceSize = faceSize / 2;
			int[] webcamCubemapSize = { halfFaceSize, halfFaceSize };

			int numThreadGroupsX = webcamCubemapSize[0] / THREADGROUP_SIZE;
			int numThreadGroupsY = webcamCubemapSize[1] / THREADGROUP_SIZE;

			computeShader.SetTexture(encodeWebcamKernel, "InputColorTexture", inputTexture);
			computeShader.SetTexture(encodeWebcamKernel, "RWOutputColorTexture", outputTexture);
			
			computeShader.SetInts("Offset", offset.x,offset.y);
			computeShader.SetInts("WebcamCubemapSize", webcamCubemapSize);

			var buffer = new CommandBuffer();
			buffer.name = "Encode Webcam";
			buffer.DispatchCompute(computeShader, encodeWebcamKernel, numThreadGroupsX, numThreadGroupsY, 1);
			context.ExecuteCommandBuffer(buffer);
			buffer.Release();
		}

		/// <summary>
		/// Write the specified cubemap to the video texture.
		/// </summary>
		void Decompose(CommandBuffer buffer, RenderTexture cubeTexture, RenderTexture videoTexture, Vector2Int StartOffset, int face, int mips)
		{
			if (!computeShader)
				InitShaders();
			// Once again, Unity's limited rendering API has let us down. We can't access a cubemap as a texture array, so we can't use it as a source in compute shaders.
			// and we are left with using vertex/pixel shaders to blit the cube faces to 
			Vector2Int Offset = StartOffset;
			int w = cubeTexture.width;
			Rect pixelRect = new Rect(0, 0, 0, 0);
			for (int m = 0; m < mips; m++)
			{
				buffer.SetRenderTarget(videoTexture);
				pixelRect.x= (float)Offset.x+ faceOffsets[face,0]*w;
				pixelRect.y= (float)Offset.y+ faceOffsets[face,1]*w;
				pixelRect.width=pixelRect.height=w;
				buffer.SetViewport(pixelRect);
				buffer.SetGlobalTexture("_SourceCubemapTexture", cubeTexture);
				buffer.SetGlobalInt("MipIndex", m);
				buffer.SetGlobalInt("Face", face);
				buffer.DrawProcedural(Matrix4x4.identity, encodeLightingCubemapMaterial, 2, MeshTopology.Triangles,6);
				Offset.x+=w*3;
				w /= 2;
			}
		}
		public void EncodeLightingCubemaps(ScriptableRenderContext context, Teleport_SceneCaptureComponent sceneCaptureComponent, Vector2Int StartOffset, int face)
		{
			if (!computeShader)
				InitShaders();
			var buffer = new CommandBuffer();
			buffer.name = "EncodeLightingCubemaps";
			// 3 mips each of specular and rough-specular texture.
			Decompose(buffer, sceneCaptureComponent.SpecularCubeTexture, sceneCaptureComponent.videoTexture, StartOffset + sceneCaptureComponent.specularOffset, face,Math.Min(6,sceneCaptureComponent.SpecularCubeTexture.mipmapCount));

			context.ExecuteCommandBuffer(buffer);
			buffer.Release();
			buffer = new CommandBuffer();
			Decompose(buffer, sceneCaptureComponent.DiffuseCubeTexture, sceneCaptureComponent.videoTexture, StartOffset + sceneCaptureComponent.diffuseOffset, face,1);
			//Decompose(context, sceneCaptureComponent.LightingCubeTexture, sceneCaptureComponent.videoTexture, StartOffset + sceneCaptureComponent.lightOffset, face);

			context.ExecuteCommandBuffer(buffer);
			buffer.Release();
		}
		public void EncodeShadowmaps(ScriptableRenderContext context, Camera camera,CullingResults cullingResults, Teleport_SceneCaptureComponent sceneCaptureComponent, TeleportRenderPipeline.LightingOrder lightingOrder, TeleportLighting teleportLighting, Vector2Int StartOffset)
		{
			// For each shadowcasting light, write the shadowmap to the video.
			Vector2Int CurrentOffset= StartOffset;
			if (lightingOrder.MainLightIndex > -1 && lightingOrder.MainLightIndex < lightingOrder.visibleLights.Length)
			{
				var l= lightingOrder.visibleLights[lightingOrder.MainLightIndex].light;
				if (TeleportLighting.perFrameLightProperties.ContainsKey(l))
				{
					var perFrame = TeleportLighting.perFrameLightProperties[l];
					perFrame.sizeOnTexture = 256;
					var viewport = new Rect(CurrentOffset.x, CurrentOffset.y, perFrame.sizeOnTexture, perFrame.sizeOnTexture);
					var buffer = new CommandBuffer();
					int cascadeCount = l.type == LightType.Directional ? 4 : 1;
					int split = cascadeCount <= 1 ? 1 : 2;
					buffer.name = "Shadowmap to Video";
					buffer.BeginSample(buffer.name);
					Vector4 CascadeOffsetScale = new Vector4();
					CascadeOffsetScale.x = 0;
					CascadeOffsetScale.y = 0;
					CascadeOffsetScale.z = 1.0F/(float)split;
					CascadeOffsetScale.w = 1.0F/(float)split;
					depthMaterial.SetTexture("DepthTexture", perFrame.shadowAtlasTexture);
					depthMaterial.SetTexture("FilteredDepthTexture", perFrame.filteredShadowTexture);

					// vsm filter
					buffer.SetGlobalVector("CascadeOffsetScale", CascadeOffsetScale);
					buffer.SetRenderTarget(perFrame.filteredShadowTexture);
					buffer.DrawProcedural(Matrix4x4.identity, depthMaterial, 2, MeshTopology.Triangles, 6);

					// To video
					buffer.SetGlobalVector("CascadeOffsetScale", CascadeOffsetScale);
					buffer.SetRenderTarget(Teleport_SceneCaptureComponent.RenderingSceneCapture.videoTexture);
					buffer.SetViewport(viewport);
					buffer.DrawProcedural(Matrix4x4.identity, depthMaterial, 1, MeshTopology.Triangles, 6);


					buffer.EndSample(buffer.name);
					context.ExecuteCommandBuffer(buffer);
					buffer.Release();
					perFrame.texturePosition = CurrentOffset;
				}
			}
		}
	}

	public partial class TeleportCameraRenderer
	{
		public TeleportRenderSettings renderSettings = null;
		// Start cubemap members
		struct CamView
		{
			public Vector3 forward, up;
			public CamView(Vector3 forward, Vector3 up) => (this.forward, this.up) = (forward, up);
		}

		public RenderTexture depthTexture = null;

		// Switch back and front because Unity view matrices have -z for forward
		static CamView frontCamView = new CamView(Vector3.forward, Vector3.right);
		static CamView backCamView = new CamView(Vector3.back, Vector3.right);
		static CamView rightCamView = new CamView(Vector3.right, Vector3.down);
		static CamView leftCamView = new CamView(Vector3.left, Vector3.up);
		static CamView upCamView = new CamView(Vector3.up, Vector3.right);
		static CamView downCamView = new CamView(Vector3.down, Vector3.right);
		static CamView[] faceCamViews = new CamView[] { frontCamView, backCamView, rightCamView, leftCamView, upCamView, downCamView };
		static Matrix4x4[] faceViewMatrices = new Matrix4x4[6];
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

		VideoEncoding videoEncoding = new VideoEncoding();

		public TeleportSettings teleportSettings = null;

		// Culling objects that are geometry-streamed.
		//CullingGroup cullingGroup = new CullingGroup();
		BoundingSphere[] boundingSpheres = new BoundingSphere[2];

		public TeleportCameraRenderer()
		{
			teleportSettings = TeleportSettings.GetOrCreateSettings();
		}

		void ExecuteBuffer(ScriptableRenderContext context, CommandBuffer buffer)
		{
			context.ExecuteCommandBuffer(buffer);
			buffer.Clear();
		}
		static public void CullingEvent(CullingGroupEvent cullingGroupEvent)
		{
		}
		/// <summary>Clear the background based on the clearFlags.</summary>
		void Clear(ScriptableRenderContext context, Camera camera)
		{
		/*	cullingGroup.targetCamera = camera;
			boundingSpheres[0].position = camera.transform.position;
			boundingSpheres[0].radius = 10.0F;
			cullingGroup.SetBoundingSpheres(boundingSpheres);
			cullingGroup.SetBoundingSphereCount(1);
			cullingGroup.onStateChanged = CullingEvent;*/
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

		bool Cull(ScriptableRenderContext context, Camera camera, out CullingResults cullingResults,bool for_streaming=false)
		{
			if (for_streaming)
			{
				camera.fieldOfView = 180.0F;
				camera.aspect = 1.0F;
			}
			bool result;
			if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
			{
				p.shadowDistance = QualitySettings.shadowDistance;//renderSettings.maxShadowDistance;
				p.shadowDistance = Mathf.Min(p.shadowDistance, camera.farClipPlane);
				cullingResults = context.Cull(ref p);
				result= true;
			}
			else
			{
				cullingResults = new CullingResults();
				result = false;
			}
			if (for_streaming)
			{
				camera.fieldOfView = 90.0F;
				camera.aspect = 1.0F;
			}
			return result;
		}
		// A ShaderTagId is ACTUALLY the Tag "LightMode" in the .shader file.
		// It allows us to render all shaders that have a specific Pass defined with that tag.
		static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
		static ShaderTagId[] legacyShaderTagIds = {
			new ShaderTagId("Always"),
			new ShaderTagId("Forward"),
			new ShaderTagId("ForwardOnly"),
			new ShaderTagId("ForwardBase"),
			//new ShaderTagId("PrepassBase"),
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
		void DrawDepthPass(ScriptableRenderContext context, Camera camera,int layerMask,uint renderingMask,CullingResults cullingResults)
		{
			context.SetupCameraProperties(camera);
			CommandBuffer buffer = CommandBufferPool.Get("Depth Pass");

			if (depthTexture == null || depthTexture.width != camera.pixelWidth || depthTexture.height != camera.pixelHeight)
			{
				depthTexture = new RenderTexture(camera.pixelWidth, camera.pixelHeight, 1, RenderTextureFormat.Depth);
			}
			//buffer.GetTemporaryRT(_CameraDepthTexture, camera.pixelWidth, camera.pixelHeight, 32, FilterMode.Trilinear, RenderTextureFormat.Depth);
			buffer.SetRenderTarget(depthTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
			buffer.ClearRenderTarget(true, true, Color.clear);

			var sortingSettings = new SortingSettings(camera)
			{
				criteria = SortingCriteria.CommonOpaque
			};
			var filteringSettings = new FilteringSettings(RenderQueueRange.opaque, layerMask, renderingMask, 0);
			buffer.BeginSample("Depth");
			var drawingSettings = new DrawingSettings(depthShaderTagIds[0], sortingSettings);
			drawingSettings.enableDynamicBatching = true;
			drawingSettings.enableInstancing = true;
			TeleportRenderPipeline.LightingOrder lightingOrder = TeleportRenderPipeline.GetLightingOrder(cullingResults);
		
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
			CommandBufferPool.Release(buffer);
			context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
		}

		static Material highlightMaterial=null;
		void DrawOpaqueGeometry(ScriptableRenderContext context, Camera camera, int layerMask, uint renderingMask, TeleportRenderPipeline.LightingOrder lightingOrder
			,bool highlight=false, RenderTexture renderTarget = null, int face = -1)
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
			CullingResults cullingResults;
			if (!Cull(context, camera, out cullingResults))
			{
				return;
			}
			teleportLighting.renderSettings = renderSettings;

			var drawingSettings = new DrawingSettings(legacyShaderTagIds[0], sortingSettings)
			{
				enableDynamicBatching = true,
				enableInstancing = true,
				perObjectData =
					PerObjectData.ReflectionProbes |
					PerObjectData.Lightmaps | PerObjectData.ShadowMask |
					PerObjectData.LightProbe | PerObjectData.OcclusionProbe |
					PerObjectData.LightProbeProxyVolume |
												PerObjectData.LightData |
					PerObjectData.OcclusionProbeProxyVolume
			};
			drawingSettings.mainLightIndex = lightingOrder.MainLightIndex;

			if (highlight)
			{
				if (highlightMaterial == null)
				{
					highlightMaterial = new Material(Shader.Find("Teleport/HighlightShader"));
					//highlightMaterial.renderQueue
				}
				drawingSettings.overrideMaterial = highlightMaterial;
			}

			/*	drawingSettings.perObjectData |= PerObjectData.LightProbe
												| PerObjectData.ReflectionProbes
												| PerObjectData.LightProbeProxyVolume
												| PerObjectData.Lightmaps
												| PerObjectData.LightData
												| PerObjectData.MotionVectors
												| PerObjectData.LightIndices
												| PerObjectData.ReflectionProbeData
												| PerObjectData.OcclusionProbe
												| PerObjectData.OcclusionProbeProxyVolume              
												| PerObjectData.ShadowMask
												;*/
			teleportLighting.SetupForwardBasePass(context, camera, cullingResults, lightingOrder, renderTarget, face);
			for (int i = 1; i < legacyShaderTagIds.Length; i++)
			{
				drawingSettings.SetShaderPassName(i, legacyShaderTagIds[i]);
			}
			var filteringSettings = new FilteringSettings(RenderQueueRange.opaque, layerMask, renderingMask, 0);
			context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
			if(lightingOrder.AdditionalLightIndices!=null)
			for(int i=0;i< lightingOrder.AdditionalLightIndices.Length;i++)
			{
				int visibleLightIndex = lightingOrder.AdditionalLightIndices[i];
				var drawingSettings2 = new DrawingSettings(addLightShaderTagIds[0], sortingSettings);
				drawingSettings2.perObjectData = drawingSettings.perObjectData;                                                                          
				drawingSettings2.mainLightIndex = 0;
				drawingSettings2.enableDynamicBatching = true;
				drawingSettings2.enableInstancing = true;
				teleportLighting.SetupForwardAddPass(context, camera, cullingResults, lightingOrder, visibleLightIndex, renderTarget, face);
				for (int j = 0;j < addLightShaderTagIds.Length; j++)
				{
					drawingSettings2.SetShaderPassName(j, addLightShaderTagIds[j]);
				}
				context.DrawRenderers(cullingResults, ref drawingSettings2, ref filteringSettings);
			}
			teleportLighting.Cleanup(context,buffer);
		}
		void DrawTransparentGeometry(ScriptableRenderContext context, Camera camera, int layerMask, uint renderingMask)
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
			var filteringSettings = new FilteringSettings(RenderQueueRange.transparent, layerMask, renderingMask, 0);
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
		void Clear(ScriptableRenderContext context, Color color)
		{
			var buffer = new CommandBuffer();
			buffer.ClearRenderTarget(true, true, color);
			buffer.SetInvertCulling(false);
			context.ExecuteCommandBuffer(buffer);
			buffer.Release();
		}
		static public void SetStreamableHighlightMaskOnObjects()
		{
			EditorMask editorMask = EditorMask.GetInstance();
		}

		public void Render(ScriptableRenderContext context, Camera camera, int layerMask, uint renderingMask)
		{
			CullingResults cullingResultsAll;
			if (!Cull(context, camera, out cullingResultsAll))
			{
				return;
			}
			string samplename = camera.gameObject.name + " sample";
			StartSample(context, samplename);
			TeleportRenderPipeline.LightingOrder lightingOrder = TeleportRenderPipeline.GetLightingOrder(cullingResultsAll);
			teleportLighting.renderSettings = renderSettings;
			teleportLighting.RenderShadows(context, camera, cullingResultsAll, lightingOrder);
			DrawDepthPass(context, camera,  layerMask,  renderingMask, cullingResultsAll);
			teleportLighting.RenderScreenspaceShadows(context, camera, lightingOrder, cullingResultsAll, depthTexture);
			context.SetupCameraProperties(camera);
			Clear(context, camera);
			PrepareForSceneWindow(context, camera);
			// We draw everything first:
			DrawOpaqueGeometry(context, camera, layerMask, renderingMask,lightingOrder);
			DrawTransparentGeometry(context, camera, layerMask, renderingMask);
			// Now we highlight the streamed objects:
			if (Application.isPlaying)
			{
				renderingMask = (uint)(1 << 26);   // Render ONLY the items that are streamed to this client.
			}
			else
			{
				SetStreamableHighlightMaskOnObjects();
				renderingMask = (uint)1 << 31;   // When not playing, only streamables have this bit set.
			}
			DrawOpaqueGeometry(context, camera, layerMask, renderingMask, lightingOrder, true);
			
#if UNITY_EDITOR
			DrawUnsupportedShaders(context, camera);
#endif
			DrawGizmos(context, camera);
			EndSample(context, samplename);
			EndCamera(context, camera);
		}
		int m = 0;
	
		public uid ClientID
		{
			get
			{
				if (!Teleport_SceneCaptureComponent.RenderingSceneCapture)
					return 0;
				return Teleport_SceneCaptureComponent.RenderingSceneCapture.clientID;
			}
			set
			{
			}
		}
		public Teleport_SessionComponent SessionComponent
		{
			get
			{
				if(ClientID!=0 && Teleport_SessionComponent.sessions.ContainsKey(ClientID))
					return Teleport_SessionComponent.sessions[ClientID];
				return null;
			} 
		}
		public void RenderToSceneCapture(ScriptableRenderContext context, Camera camera)
		{
			if (!Teleport_SceneCaptureComponent.RenderingSceneCapture)
				return;
			RenderTexture videoTexture = Teleport_SceneCaptureComponent.RenderingSceneCapture.videoTexture;

			if (!videoTexture)
			{
				Debug.LogError("The video encoder texture must not be null");
				return;
			}
			if (!Teleport_SessionComponent.sessions.ContainsKey(ClientID))
				return;
			
			var session = Teleport_SessionComponent.sessions[ClientID];
			camera.transform.position = session.head.transform.position;
			camera.transform.rotation = session.head.transform.rotation;

			var oldPos = camera.transform.position;
			var oldRot = camera.transform.rotation;

			camera.targetTexture = Teleport_SceneCaptureComponent.RenderingSceneCapture.rendererTexture;
			if (ClientID != 0)
				UpdateStreamables(context, ClientID, camera);

			CullingResults cullingResultsAll;
			float oldNearClip = camera.nearClipPlane;
			if (teleportSettings.casterSettings.isStreamingGeometry)
			{
				camera.nearClipPlane = teleportSettings.casterSettings.detectionSphereRadius * 0.5f;
			}
			
			if (Cull(context, camera, out cullingResultsAll, true))
			{
				TeleportRenderPipeline.LightingOrder lightingOrder = TeleportRenderPipeline.GetLightingOrder(cullingResultsAll);

				teleportLighting.renderSettings = renderSettings;
				teleportLighting.RenderShadows(context, camera, cullingResultsAll, lightingOrder);

				Vector2Int shadowmapOffset;

				if (teleportSettings.casterSettings.usePerspectiveRendering)
				{
					// Draw scene
					camera.fieldOfView = teleportSettings.casterSettings.perspectiveFOV;
					DrawPerspective(context, camera, lightingOrder);

					// Draw cubemaps
					camera.fieldOfView = 90;
					for (int i = 0; i < VideoEncoding.NumFaces; ++i)
					{
						DrawCubemapFace(context, camera, lightingOrder, i);
					}

					int perspectiveWidth = teleportSettings.casterSettings.perspectiveWidth;
					int perspectiveHeight = teleportSettings.casterSettings.perspectiveHeight;
					shadowmapOffset = new Vector2Int(perspectiveWidth / 2, perspectiveHeight + 2 * Teleport_SceneCaptureComponent.RenderingSceneCapture.DiffuseCubeTexture.width) + Teleport_SceneCaptureComponent.RenderingSceneCapture.diffuseOffset;
				}
				else
				{
					for (int i = 0; i < VideoEncoding.NumFaces; ++i)
					{
						DrawCubemapFace(context, camera, lightingOrder, i);
					}

					int faceSize = (int)teleportSettings.casterSettings.captureCubeTextureSize;
					shadowmapOffset = new Vector2Int(3 * faceSize / 2, 2 * faceSize + 2 * Teleport_SceneCaptureComponent.RenderingSceneCapture.DiffuseCubeTexture.width) + Teleport_SceneCaptureComponent.RenderingSceneCapture.diffuseOffset;
				}

				videoEncoding.EncodeShadowmaps(context, camera, cullingResultsAll, Teleport_SceneCaptureComponent.RenderingSceneCapture, lightingOrder, teleportLighting, shadowmapOffset);

				videoEncoding.EncodeWebcam(context, camera, Teleport_SceneCaptureComponent.RenderingSceneCapture.webcamOffset);	
				videoEncoding.EncodeTagID(context, camera);
				context.Submit();

				camera.nearClipPlane = oldNearClip;
				camera.transform.position = oldPos;
				camera.transform.rotation = oldRot;

				var videoEncoder = Teleport_SceneCaptureComponent.RenderingSceneCapture.VideoEncoder;
				if (ClientID != 0 && teleportSettings.casterSettings.isStreamingVideo && videoEncoder != null)
				{
					var tagDataID = Teleport_SceneCaptureComponent.RenderingSceneCapture.CurrentTagID;
					videoEncoder.CreateEncodeCommands(context, camera, tagDataID);
				}
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
				Teleport_SessionComponent.sessions[clientID].SetStreamedLights(lights);
			}
			camera.fieldOfView = 90.0F;
			camera.farClipPlane = farClipPlane;
			camera.aspect = 1.0F;
		}
		void DrawCubemapFace(ScriptableRenderContext context, Camera camera, TeleportRenderPipeline.LightingOrder lightingOrder, int face)
		{
			CullingResults cullingResultsAll;
			if (!Cull(context, camera, out cullingResultsAll))
			{
				return;
			}

			CamView camView = faceCamViews[face];
			Vector3 to = camView.forward;
			Vector3 pos = camera.transform.position;

			camera.transform.LookAt(pos + to, camView.up);
			
			Matrix4x4 view = camera.transform.localToWorldMatrix;
			view = Matrix4x4.Inverse(view);
			view.m20 *= -1f;
			view.m21 *= -1f;
			view.m22 *= -1f;
			view.m23 *= -1f;
			faceViewMatrices[face] = view;
			int layerMask = 0x7FFFFFFF;
			uint renderingMask = 0x7FFFFFFF;
			//(uint)((1 << 25)|0x7);	// canvasrenderers hard coded to have mask 0x7..!
			camera.worldToCameraMatrix = view;
			string sampleName = camera.gameObject.name + " Face " + face;

			StartSample(context, sampleName);
			{
				if (teleportSettings.casterSettings.usePerspectiveRendering)
				{
					context.SetupCameraProperties(camera);
					Clear(context, camera);
					PrepareForSceneWindow(context, camera);

					DrawOpaqueGeometry(context, camera, layerMask, renderingMask, lightingOrder, false, Teleport_SceneCaptureComponent.RenderingSceneCapture.UnfilteredCubeTexture, face);
					DrawTransparentGeometry(context, camera, layerMask, renderingMask);
		
					videoEncoding.GenerateSpecularMips(context, Teleport_SceneCaptureComponent.RenderingSceneCapture.UnfilteredCubeTexture, Teleport_SceneCaptureComponent.RenderingSceneCapture.SpecularCubeTexture, face, 0);
					videoEncoding.GenerateDiffuseCubemap(context, Teleport_SceneCaptureComponent.RenderingSceneCapture.SpecularCubeTexture, Teleport_SceneCaptureComponent.RenderingSceneCapture.DiffuseCubeTexture, face);
					videoEncoding.EncodeLightingCubemaps(context, Teleport_SceneCaptureComponent.RenderingSceneCapture, new Vector2Int(teleportSettings.casterSettings.perspectiveWidth / 2, teleportSettings.casterSettings.perspectiveHeight), face);
				}
				else
				{
					int faceSize = (int)teleportSettings.casterSettings.captureCubeTextureSize;
					int halfFaceSize = faceSize / 2;
					int offsetX = VideoEncoding.faceOffsets[face, 0];
					int offsetY = VideoEncoding.faceOffsets[face, 1];
					var depthViewport = new Rect(offsetX * halfFaceSize, (faceSize * 2) + (offsetY * halfFaceSize), halfFaceSize, halfFaceSize);

					DrawDepthPass(context, camera, layerMask, renderingMask, cullingResultsAll);
					teleportLighting.RenderScreenspaceShadows(context, camera, lightingOrder, cullingResultsAll, depthTexture);
					context.SetupCameraProperties(camera);
					Clear(context, camera);
					PrepareForSceneWindow(context, camera);

					DrawOpaqueGeometry(context, camera, layerMask, renderingMask, lightingOrder);
					DrawTransparentGeometry(context, camera, layerMask, renderingMask);
					videoEncoding.DrawCubemaps(context, Teleport_SceneCaptureComponent.RenderingSceneCapture.rendererTexture, Teleport_SceneCaptureComponent.RenderingSceneCapture.UnfilteredCubeTexture, face);
					videoEncoding.GenerateSpecularMips(context, Teleport_SceneCaptureComponent.RenderingSceneCapture.UnfilteredCubeTexture, Teleport_SceneCaptureComponent.RenderingSceneCapture.SpecularCubeTexture, face, 0);
					videoEncoding.GenerateDiffuseCubemap(context, Teleport_SceneCaptureComponent.RenderingSceneCapture.SpecularCubeTexture, Teleport_SceneCaptureComponent.RenderingSceneCapture.DiffuseCubeTexture, face);
					videoEncoding.EncodeColor(context, camera, face);
					videoEncoding.EncodeDepth(context, camera, depthViewport);
					videoEncoding.EncodeLightingCubemaps(context, Teleport_SceneCaptureComponent.RenderingSceneCapture, new Vector2Int(3 * (int)depthViewport.width, 2 * faceSize), face);
#if UNITY_EDITOR
					DrawUnsupportedShaders(context, camera);
#endif
				}
			}
			EndSample(context, sampleName);
			EndCamera(context, camera);
			camera.ResetWorldToCameraMatrix();
		}

		void DrawPerspective(ScriptableRenderContext context, Camera camera, TeleportRenderPipeline.LightingOrder lightingOrder)
		{
			CullingResults cullingResultsAll;
			if (!Cull(context, camera, out cullingResultsAll))
			{
				return;
			}
			CasterMonitor monitor = CasterMonitor.GetCasterMonitor();
			int perspectiveWidth = teleportSettings.casterSettings.perspectiveWidth;
			int perspectiveHeight = teleportSettings.casterSettings.perspectiveHeight;

			var depthViewport = new Rect(0, perspectiveHeight, perspectiveWidth / 2, perspectiveHeight / 2);

			int layerMask = 0x7FFFFFFF;
			uint renderingMask = 0x7FFFFFFF;
			string sampleName = camera.gameObject.name + " Perspective";	

			StartSample(context, sampleName);
			{
				DrawDepthPass(context, camera, layerMask, renderingMask, cullingResultsAll);
				teleportLighting.RenderScreenspaceShadows(context, camera, lightingOrder, cullingResultsAll, depthTexture);
				context.SetupCameraProperties(camera);
				Clear(context, camera);
				PrepareForSceneWindow(context, camera);

				DrawOpaqueGeometry(context, camera, layerMask, renderingMask, lightingOrder);
				DrawTransparentGeometry(context, camera, layerMask, renderingMask);
				videoEncoding.EncodeColor(context, camera, 0);
				videoEncoding.EncodeDepth(context, camera, depthViewport);
#if UNITY_EDITOR
				DrawUnsupportedShaders(context, camera);
#endif
			}
			EndSample(context, sampleName);
			EndCamera(context, camera);
		}
	}
}