using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace teleport
{
	/// A class to encode rendered and generated images into a video texture for streaming.
	public class VideoEncoding
	{
		public static Shader cubemapShader = null;
		public static Material copyCubemapMaterial = null;
		static Mesh fullscreenMesh = null;
		static Shader depthShader = null;
		static Material depthMaterial = null;
		public ComputeShader computeShader = null;
		public int encodeTagIdKernel = -1;
		public int encodeColorKernel = -1;
		public int encodeWebcamKernel = -1;
		static int CopyCubemap_plain_copy=0;
		static int CopyCubemap_mip_frag = 1;
		static int CopyCubemap_encode_face_frag0 = 2;
		static int CopyCubemap_encode_face_frag1 = 3;
		static int CopyCubemap_ambient_diffuse_frag = 4;
		static int CopyCubemap_directional_diffuse_frag = 5; 
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
			encodeWebcamKernel = computeShader.FindKernel("EncodeWebcamCS");
		}

		static private void InitDepthShader()
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
				}
			}
		}

		static public void EnsureMaterial(ref Material m,ref Shader s,string shaderName)
		{
			if (m == null)
			{
				s = Shader.Find(shaderName);
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
		public void CopyOneFace(ScriptableRenderContext context, RenderTexture sourceTexture, RenderTexture outputTexture, int face)
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
			EnsureMaterial(ref copyCubemapMaterial,ref cubemapShader, "Teleport/CopyCubemap");

			var buffer = new CommandBuffer();
			buffer.name = "Copy Cubemap Face";
			buffer.SetRenderTarget(outputTexture, 0, (CubemapFace)face);
			copyCubemapMaterial.SetTexture("_SourceTexture", sourceTexture);
			buffer.DrawMesh(fullscreenMesh, Matrix4x4.identity, copyCubemapMaterial, 0, CopyCubemap_plain_copy);
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
		static Matrix4x4 GetTargetToSourceTransform(avs.AxesStandard targetAxesStandard)
		{
			Matrix4x4 transformTargetToSource = Matrix4x4.identity;
			if (targetAxesStandard == avs.AxesStandard.GlStyle)
			{

			}
			else if (targetAxesStandard == avs.AxesStandard.EngineeringStyle)
			{
				// transform from eng to gl to precede cubemap lookup.
				//view_gl = view_eng.zxy;
				transformTargetToSource.SetColumn(0, new Vector4(0, 0, 1.0F, 0));
				transformTargetToSource.SetColumn(1, new Vector4(1.0F, 0, 0, 0));
				transformTargetToSource.SetColumn(2, new Vector4(0, -1.0F, 0, 0));
			}
			else
				Debug.LogError("Unsupported axes standard for this function.");
			return transformTargetToSource;
		}
		/// <summary>
		/// For a specular cubemap, we render the lower-detail mipmaps to represent reflections for rougher materials.
		/// We SHOULD here be rendering down from higher to lower detail mips of the same cubemap.
		/// But Unity is missing the necessary API functions to render with one mip of input and one as output of the same texture.
		/// So instead we will render directly to the video texture.
		/// </summary>
		static public void SpecularRoughnessMip(CommandBuffer buffer, Texture SourceCubeTexture, RenderTexture SpecularCubeTexture, int face, int MipIndex, int mipOffset,avs.AxesStandard targetAxesStandard)
		{
			float roughness =  RoughnessFromMip((float)( MipIndex+ mipOffset), (float)( SpecularCubeTexture.mipmapCount));
			int w = SpecularCubeTexture.width << MipIndex;
			// Render to mip MipIndex...
			buffer.SetRenderTarget(SpecularCubeTexture, MipIndex, (CubemapFace)face);

			// We WOULD be sending roughness this way...
			// but Unity can't cope with changing the value between Draw calls...
			//copyCubemapMaterial.SetFloat("Roughness", roughness);

			// Instead we must use buffer.SetGlobalFloat etc.
			buffer.SetGlobalMatrix("TargetToSourceAxes", GetTargetToSourceTransform(targetAxesStandard));
			buffer.SetGlobalFloat("Roughness", roughness);
			buffer.SetGlobalInt("MipIndex", MipIndex);
			buffer.SetGlobalInt("NumMips", SpecularCubeTexture.mipmapCount);
			buffer.SetGlobalInt("Face", face);
			// But SetGlobalTexture() doesn't work - we must instead put it in the material, here:
			copyCubemapMaterial.SetTexture("_SourceCubemapTexture", SourceCubeTexture);
			// 
			buffer.DrawProcedural(Matrix4x4.identity, copyCubemapMaterial, CopyCubemap_mip_frag, MeshTopology.Triangles,6);
		}
		//! Generate mipmaps into a target cubemap rendertexture. The source is considered to be in the Unity/OpenGL layout.
		//! The target layout is specified by avs.AxesStandard axesStandard.
		static public void GenerateSpecularMips(ScriptableRenderContext context, Texture SourceCubeTexture, RenderTexture SpecularCubeTexture, int face,int mip_offset, avs.AxesStandard axesStandard)
		{
			var buffer = new CommandBuffer();
			buffer.name = "Generate Specular Mips";

			// For perspective rendering
			EnsureMaterial(ref copyCubemapMaterial, ref cubemapShader, "Teleport/CopyCubemap");

			for (int i = 0; i < SpecularCubeTexture.mipmapCount; i++)
			{
				SpecularRoughnessMip(buffer, SourceCubeTexture, SpecularCubeTexture, face,i,mip_offset, axesStandard);
			}
			context.ExecuteCommandBuffer(buffer);
			buffer.Release();
		}
		//! Generate a target diffuse cubemap rendertexture. The source is considered to be in the Unity/OpenGL layout.
		//! The target layout is specified by avs.AxesStandard axesStandard.
		static public void GenerateDiffuseCubemap(ScriptableRenderContext context, Texture SourceCubeTexture, HashSet<Light> bakedLights, RenderTexture DiffuseCubeTexture, int face,float light_scale,avs.AxesStandard targetAxesStandard)
		{
			var buffer = new CommandBuffer();
			buffer.name = "Diffuse";

			buffer.SetRenderTarget(DiffuseCubeTexture, 0, (CubemapFace)face);
			Matrix4x4 transformTargetToSource = Matrix4x4.identity;
			buffer.SetGlobalMatrix("TargetToSourceAxes", GetTargetToSourceTransform(targetAxesStandard));
			buffer.SetGlobalInt("MipIndex", 0);
			buffer.SetGlobalInt("NumMips", 1);
			buffer.SetGlobalInt("Face", face);
			buffer.SetGlobalFloat("Roughness", 1.0F);
			buffer.SetGlobalTexture("_DiffuseSourceCubemapTexture", SourceCubeTexture);
			// But SetGlobalTexture() doesn't work - we must instead put it in the material, here:
			copyCubemapMaterial.SetTexture("_DiffuseSourceCubemapTexture", SourceCubeTexture);
			// We downscale the whole diffuse so that it peaks at the maximum colour value.
			buffer.SetGlobalFloat("Multiplier", light_scale);
			// Here we encode the contribution of the diffuse cubemap.
			buffer.DrawProcedural(Matrix4x4.identity, copyCubemapMaterial,CopyCubemap_ambient_diffuse_frag, MeshTopology.Triangles, 6);
			// Now we encode the contribution of the baked lights.
			foreach(Light light in bakedLights)
			{
				if (light.type != LightType.Directional)
					continue;
				var clr = light.intensity * light.color.linear;
				// light.transform.forward gives the direction the light is pointing.
				// We want the direction TO the light...
				var dir = -light.transform.forward;
				buffer.SetGlobalVector("Colour", new Vector4(clr.r, clr.g, clr.b, clr.a));
				buffer.SetGlobalVector("Direction", new Vector4(dir.x,dir.y, dir.z, 0.0F));
				buffer.DrawProcedural(Matrix4x4.identity, copyCubemapMaterial,CopyCubemap_directional_diffuse_frag, MeshTopology.Triangles, 6);
			}
			context.ExecuteCommandBuffer(buffer);
			buffer.Release();
		}
		public void EncodeColor(ScriptableRenderContext context, Camera camera, int face, Teleport_SceneCaptureComponent sceneCapture)
		{
			if (!computeShader)
				InitShaders();
			var outputTexture = sceneCapture.videoTexture;
			var captureTexture = sceneCapture.rendererTexture;

			int numThreadGroupsX = captureTexture.width / THREADGROUP_SIZE; 
			int numThreadGroupsY = captureTexture.height / THREADGROUP_SIZE;

			computeShader.SetTexture(encodeColorKernel, "InputColorTexture", captureTexture);
			computeShader.SetTexture(encodeColorKernel, "InputDepthTexture", captureTexture, 0, RenderTextureSubElement.Depth);
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

		public void EncodeDepth(ScriptableRenderContext context, Camera camera, Rect viewport, Teleport_SceneCaptureComponent sceneCapture)
		{
			if (!computeShader)
				InitShaders();

			InitDepthShader();

			var captureTexture = sceneCapture.rendererTexture;

			var buffer = new CommandBuffer();
			depthMaterial.SetTexture("DepthTexture", captureTexture, RenderTextureSubElement.Depth);
			buffer.name = "Custom Depth CB";
			buffer.SetRenderTarget(sceneCapture.videoTexture);
			buffer.SetViewport(viewport);
			buffer.BeginSample(buffer.name);

			buffer.DrawProcedural(Matrix4x4.identity, depthMaterial,CopyCubemap_plain_copy, MeshTopology.Triangles, 6);
			buffer.EndSample(buffer.name);
			context.ExecuteCommandBuffer(buffer);
			buffer.Release();
		}
		// Encodes the id of the video tag data in 4x4 blocks of monochrome colour.
		public void EncodeTagID(ScriptableRenderContext context, Camera camera, Teleport_SceneCaptureComponent sceneCapture)
		{
			if (!computeShader)
				InitShaders();
			var tagDataID = sceneCapture.CurrentTagID;

			var outputTexture = sceneCapture.videoTexture;
			computeShader.SetTexture(encodeTagIdKernel, "RWOutputColorTexture", outputTexture);
			computeShader.SetInts("TagDataIdOffset", new Int32[2] { outputTexture.width - (32 * 4), outputTexture.height - 4 });
			computeShader.SetInt("TagDataId", (int)tagDataID);
			var buffer = new CommandBuffer();
			buffer.name = "Encode Camera Position";
			buffer.DispatchCompute(computeShader, encodeTagIdKernel, 4, 1, 1);
			context.ExecuteCommandBuffer(buffer);
			buffer.Release();
		}

		public void EncodeWebcam(ScriptableRenderContext context, Camera camera, Vector2Int offset, Vector2Int webcamSize, Teleport_SceneCaptureComponent sceneCapture)
		{
			if (!computeShader)
				InitShaders();

			var inputTexture = sceneCapture.webcamTexture;

			// Will be null if not streaming webcam.
			if (!inputTexture)
			{
				return;
			}

			var outputTexture = sceneCapture.videoTexture;

			int numThreadGroupsX = webcamSize.x / THREADGROUP_SIZE;
			int numThreadGroupsY = webcamSize.y / THREADGROUP_SIZE;
			
			computeShader.SetTexture(encodeWebcamKernel, "InputColorTexture", inputTexture);
			computeShader.SetTexture(encodeWebcamKernel, "RWOutputColorTexture", outputTexture);
			
			computeShader.SetInts("Offset", offset.x, offset.y);
			computeShader.SetInts("WebcamSize", webcamSize.x, webcamSize.y);

			var buffer = new CommandBuffer();
			buffer.name = "Encode Webcam";
			buffer.DispatchCompute(computeShader, encodeWebcamKernel, numThreadGroupsX, numThreadGroupsY, 1);
			context.ExecuteCommandBuffer(buffer);
			buffer.Release();
		}

		/// <summary>
		/// Write the specified cubemap to the video texture.
		/// </summary>
		void DecomposeCubemapTo2D(CommandBuffer buffer, RenderTexture cubeTexture, RenderTexture videoTexture, Vector2Int StartOffset, int face, int mips,int shader_choice)
		{
			if (!computeShader)
				InitShaders();
			// Once again, Unity's limited rendering API has let us down. We can't access a cubemap as a texture array, so we can't use it as a source in compute shaders.
			// and we are left with using vertex/pixel shaders to blit the cube faces to 
			Vector2Int Offset = StartOffset;
			int w = cubeTexture.width;
			Rect pixelRect = new Rect(0, 0, 0, 0);
			int shaderIndex=0;
			if (shader_choice == 0)
			{
				shaderIndex= CopyCubemap_encode_face_frag0;
				buffer.SetGlobalTexture("_EncodeSourceCubemapTexture0", cubeTexture);
				// But SetGlobalTexture() doesn't work - we must instead put it in the material, here:
				copyCubemapMaterial.SetTexture("_EncodeSourceCubemapTexture0", cubeTexture);

			}
			if (shader_choice == 1)
			{
				shaderIndex = CopyCubemap_encode_face_frag1;
				buffer.SetGlobalTexture("_EncodeSourceCubemapTexture1", cubeTexture);
				// But SetGlobalTexture() doesn't work - we must instead put it in the material, here:
				copyCubemapMaterial.SetTexture("_EncodeSourceCubemapTexture1", cubeTexture);

			}
			for (int m = 0; m < mips; m++)
			{
				buffer.SetRenderTarget(videoTexture);
				pixelRect.x= (float)Offset.x+ faceOffsets[face,0]*w;
				pixelRect.y= (float)Offset.y+ faceOffsets[face,1]*w;
				pixelRect.width=pixelRect.height=w;
				buffer.SetViewport(pixelRect);
				buffer.SetGlobalInt("MipIndex", m);
				buffer.SetGlobalInt("Face", face);
				buffer.DrawProcedural(Matrix4x4.identity, copyCubemapMaterial, shaderIndex, MeshTopology.Triangles,6);
				Offset.x+=w*3;
				w /= 2;
			}
		}
		public void EncodeLightingCubemaps(ScriptableRenderContext context, Teleport_SceneCaptureComponent sceneCaptureComponent,Teleport_SessionComponent session, int face)
		{
			if (!computeShader)
				InitShaders();
			var buffer = new CommandBuffer();
			buffer.name = "EncodeLightingCubemaps";

			// 3 mips each of specular and rough-specular texture.
			DecomposeCubemapTo2D(buffer, sceneCaptureComponent.SpecularCubeTexture, sceneCaptureComponent.videoTexture, session.clientDynamicLighting.specularPos, face,sceneCaptureComponent.SpecularCubeTexture.mipmapCount,0);

			context.ExecuteCommandBuffer(buffer);
			buffer.Release();
			buffer = new CommandBuffer();
			DecomposeCubemapTo2D(buffer, sceneCaptureComponent.DiffuseCubeTexture, sceneCaptureComponent.videoTexture, session.clientDynamicLighting.diffusePos, face,1,1);
			//DecomposeCubemapTo2D(context, sceneCaptureComponent.LightingCubeTexture, sceneCaptureComponent.videoTexture, StartOffset + sceneCaptureComponent.lightOffset, face);

			context.ExecuteCommandBuffer(buffer);
			buffer.Release();
		}
		public void EncodeShadowmaps(ScriptableRenderContext context, Camera camera,CullingResults cullingResults, Teleport_SceneCaptureComponent sceneCaptureComponent, Teleport_SessionComponent session, TeleportRenderPipeline.LightingOrder lightingOrder, TeleportLighting teleportLighting)
		{
			InitDepthShader();
			// For each shadowcasting light, write the shadowmap to the video.
			Vector2Int CurrentOffset= session.clientSettings.shadowmapPos;
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
					buffer.SetRenderTarget(sceneCaptureComponent.videoTexture);
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

}