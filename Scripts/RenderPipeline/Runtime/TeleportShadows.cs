using UnityEngine;
using UnityEngine.Rendering;

namespace teleport
{
	public class TeleportShadows
	{
		public TeleportRenderSettings renderSettings = null;
		public RenderTexture shadowAtlasTexture = null;
		public RenderTexture screenspaceShadowTexture = null;
		const int maxShadowedDirectionalLightCount = 4;
		int ShadowedDirectionalLightCount = 0;
		int tileSize = 0;
		int split = 0;
		Matrix4x4[] WorldToShadow = new Matrix4x4[4];
		struct ShadowedDirectionalLight
		{
			public int visibleLightIndex;
		}
		static int _ShadowMapTexture = Shader.PropertyToID("_ShadowMapTexture"),
					_CameraDepthTexture=Shader.PropertyToID("_CameraDepthTexture"),
					unity_ShadowSplitSpheres0 = Shader.PropertyToID("unity_ShadowSplitSpheres0"),
					unity_ShadowSplitSpheres1 = Shader.PropertyToID("unity_ShadowSplitSpheres1"),
					unity_ShadowSplitSpheres2 = Shader.PropertyToID("unity_ShadowSplitSpheres2"),
					unity_ShadowSplitSpheres3 = Shader.PropertyToID("unity_ShadowSplitSpheres3"),
					unity_ShadowSplitSqRadii = Shader.PropertyToID("unity_ShadowSplitSqRadii"),
					unity_LightShadowBias = Shader.PropertyToID("unity_LightShadowBias"),
					_LightSplitsNear = Shader.PropertyToID("_LightSplitsNear"),
					_LightSplitsFar = Shader.PropertyToID("_LightSplitsFar"),
					unity_WorldToShadow0 = Shader.PropertyToID("unity_WorldToShadow0"),
					unity_WorldToShadow1 = Shader.PropertyToID("unity_WorldToShadow1"),
					unity_WorldToShadow2 = Shader.PropertyToID("unity_WorldToShadow2"),
					unity_WorldToShadow3 = Shader.PropertyToID("unity_WorldToShadow3"),
					_LightShadowData = Shader.PropertyToID("_LightShadowData"),
					unity_ShadowFadeCenterAndType = Shader.PropertyToID("unity_ShadowFadeCenterAndType"),
					something_nonexistent = Shader.PropertyToID("something_nonexistent");
		ShadowedDirectionalLight[] ShadowedDirectionalLights = new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];
		Mesh fullscreenMesh = null;
		public void ReserveDirectionalShadows(CullingResults cullingResults, Light light, int visibleLightIndex)
		{
			if (ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount &&
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
			name = "Shadows"
		};

		public void Setup(ScriptableRenderContext context, CullingResults cullingResults)
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
		Vector4 shadowFadeCenterAndType = new Vector4();
		public void RenderDirectionalShadows(ScriptableRenderContext context, CullingResults cullingResults, ref PerFramePerCameraLightProperties perFrameLightProperties, int cascadeCount, int index)
		{
			if (ShadowedDirectionalLightCount == 0)
				return;
			if (index >= 4)
				return;
			ShadowedDirectionalLight sdl = ShadowedDirectionalLights[0];

			var shadowSettings = new ShadowDrawingSettings(cullingResults, sdl.visibleLightIndex);

			Vector3 ratios = QualitySettings.shadowCascade4Split;
			if (cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
				sdl.visibleLightIndex, index, 4, ratios, tileSize, QualitySettings.shadowNearPlaneOffset,//+perFrameLightProperties.visibleLight.light.shadowNearPlane
				out perFrameLightProperties.cascades[index].viewMatrix, out perFrameLightProperties.cascades[index].projectionMatrix,
				out perFrameLightProperties.cascades[index].splitData
				))
			{
				VisibleLight visibleLight = cullingResults.visibleLights[sdl.visibleLightIndex];
				Rect vp = GetTileViewport(index, tileSize, split);
				Vector4 sph = perFrameLightProperties.cascades[index].splitData.cullingSphere;
				sph.w *= sph.w;
				perFrameLightProperties.cascades[index].splitData.cullingSphere = sph;
				shadowSettings.splitData = perFrameLightProperties.cascades[index].splitData;
				buffer.SetViewport(vp);
				buffer.SetViewProjectionMatrices(perFrameLightProperties.cascades[index].viewMatrix, perFrameLightProperties.cascades[index].projectionMatrix);
				Vector4 bias4 = new Vector4(visibleLight.light.shadowBias, visibleLight.light.shadowNormalBias, 0.0f, 0.0f);
				//			new Vector4(visibleLight.light.shadowBias, visibleLight.light.shadowBias, visibleLight.light.shadowBias, visibleLight.light.shadowBias);

				Vector4 shadowBias = new Vector4(0, 0, 0, 0);
				shadowBias = teleport.ShadowUtils.GetShadowBias(ref visibleLight, sdl.visibleLightIndex, bias4, visibleLight.light.shadows == LightShadows.Soft, perFrameLightProperties.cascades[index].projectionMatrix, tileSize);

				//Vector4 lightShadowBias = new Vector4(-0.001484548F, 1.0F, 0.017146875F, 0F);
				buffer.SetGlobalVector(unity_LightShadowBias, shadowBias);
				ExecuteBuffer(context);
				context.DrawShadows(ref shadowSettings);

			}
		}
		public void RenderDirectionalShadows(ScriptableRenderContext context, CullingResults cullingResults, ref PerFramePerCameraLightProperties perFrameLightProperties, int cascadeCount)
		{
			buffer.BeginSample("Shadows");
			int atlasSize = (int)renderSettings.directional.atlasSize;
			split = cascadeCount <= 1 ? 1 : 2;
			tileSize = atlasSize / split;
			if (shadowAtlasTexture == null || shadowAtlasTexture.width != atlasSize)
			{
				shadowAtlasTexture = new RenderTexture(atlasSize, atlasSize, 1, RenderTextureFormat.Shadowmap);
			}
			buffer.SetRenderTarget(shadowAtlasTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
			buffer.ClearRenderTarget(true, true, Color.clear);
			ExecuteBuffer(context);
			shadowFadeCenterAndType.x = shadowFadeCenterAndType.y = shadowFadeCenterAndType.z = 0;
			for (int i = 0; i < 4; i++)
			{
				RenderDirectionalShadows(context, cullingResults, ref perFrameLightProperties, cascadeCount, i);
				shadowFadeCenterAndType.x += 0.25F * perFrameLightProperties.cascades[i].splitData.cullingSphere.x;
				shadowFadeCenterAndType.y += 0.25F * perFrameLightProperties.cascades[i].splitData.cullingSphere.y;
				shadowFadeCenterAndType.z += 0.25F * perFrameLightProperties.cascades[i].splitData.cullingSphere.z;
			}
			buffer.EndSample("Shadows");
			ExecuteBuffer(context);
		}
		static Shader shadowShader = null;
		static Material shadowMaterial = null;
		Vector4 lightShadowData = new Vector4();
		public static void GetShadowTransforms(ref Matrix4x4[] WorldToShadow, PerFramePerCameraLightProperties perFrameLightProperties, int atlasSize, int numCascades)
		{
			for (int i = 0; i < 4; i++)
			{
				PerFrameShadowCascadeProperties cascade = perFrameLightProperties.cascades[i];
				WorldToShadow[i] = teleport.ShadowUtils.GetShadowTransform(cascade.projectionMatrix, cascade.viewMatrix);
				teleport.ShadowUtils.ApplySliceTransform(ref WorldToShadow[i], i, atlasSize, 2, atlasSize / 2);
			}
		}
		teleport.RenderingUtils.FullScreenMeshStruct fullScreenMeshStruct = new teleport.RenderingUtils.FullScreenMeshStruct();
		teleport.RenderingUtils.FullScreenMeshStruct newMeshStruct = new teleport.RenderingUtils.FullScreenMeshStruct();
		public void RenderScreenspaceShadows(ScriptableRenderContext context, Camera camera, CullingResults cullingResults, PerFrameLightProperties perFrameLightProperties, int cascadeCount
				, RenderTexture depthTexture)
		{
			PerFramePerCameraLightProperties perFramePerCameraLightProperties = perFrameLightProperties.perFramePerCameraLightProperties[camera];
			CommandBuffer buffer = CommandBufferPool.Get("RenderScreenspaceShadows");
			buffer.BeginSample("Shadow");
			context.SetupCameraProperties(camera);
			if (screenspaceShadowTexture == null || screenspaceShadowTexture.width != camera.pixelWidth || screenspaceShadowTexture.height != camera.pixelHeight)
			{
				screenspaceShadowTexture = new RenderTexture(camera.pixelWidth, camera.pixelHeight,1, RenderTextureFormat.BGRA32);
			}
			buffer.SetGlobalTexture(_CameraDepthTexture, depthTexture);
			buffer.SetRenderTarget(screenspaceShadowTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store); 
			//buffer.GetTemporaryRT(_ShadowMapTexture, camera.pixelWidth, camera.pixelHeight, 32, FilterMode.Bilinear, RenderTextureFormat.BGRA32);
			//buffer.SetRenderTarget(_ShadowMapTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
			buffer.ClearRenderTarget(false, true, Color.clear);
			buffer.SetViewMatrix(Matrix4x4.identity);
			// Unclear what Unity's logic is about needing this proj matrix:
			//Matrix4x4.Perspective(60, 1, 1, 99)
			Matrix4x4 proj = new Matrix4x4(new Vector4(2.0F, 0, 0, 0)
											, new Vector4(0, 2.0F, 0, 0)
											, new Vector4(0, 0, -0.0198F, 0)
											, new Vector4(-1.0F, -1.0F, -0.98F, 1.0F));
			buffer.SetProjectionMatrix(proj);
			//buffer.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
			//buffer.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, m_ScreenSpaceShadowsMaterial);

			if (shadowMaterial == null)
			{
				//shadowShader = Shader.Find("Hidden/Internal-ScreenSpaceShadows");
				shadowShader = Shader.Find("Teleport/ScreenSpaceShadows");
				//shadowShader = Resources.Load("Hidden/Internal-ScreenSpaceShadows", typeof(Shader)) as Shader;
				if (shadowShader != null)
				{
					shadowMaterial = new Material(shadowShader);
				}
				else
				{
					Debug.LogError("shadowMaterial.shader resource not found!");
					return;
				}
			}
			shadowMaterial.SetTexture("_ShadowMapTexture", shadowAtlasTexture, RenderTextureSubElement.Depth);

			GetShadowTransforms(ref WorldToShadow, perFramePerCameraLightProperties, (int)renderSettings.directional.atlasSize, 4);

			buffer.SetGlobalVector(unity_ShadowSplitSpheres0, perFramePerCameraLightProperties.cascades[0].splitData.cullingSphere);
			buffer.SetGlobalVector(unity_ShadowSplitSpheres1, perFramePerCameraLightProperties.cascades[1].splitData.cullingSphere);
			buffer.SetGlobalVector(unity_ShadowSplitSpheres2, perFramePerCameraLightProperties.cascades[2].splitData.cullingSphere);
			buffer.SetGlobalVector(unity_ShadowSplitSpheres3, perFramePerCameraLightProperties.cascades[3].splitData.cullingSphere);
			Vector4 sqr_rad = new Vector4(perFramePerCameraLightProperties.cascades[0].splitData.cullingSphere.w
										, perFramePerCameraLightProperties.cascades[1].splitData.cullingSphere.w
										, perFramePerCameraLightProperties.cascades[2].splitData.cullingSphere.w
										, perFramePerCameraLightProperties.cascades[3].splitData.cullingSphere.w);
			buffer.SetGlobalVector(unity_ShadowSplitSqRadii, sqr_rad);
			//buffer.SetGlobalVector(unity_LightShadowBias, new Vector4(perFrameLightProperties.visibleLight.light.shadowBias,0,0,0));
			//buffer.SetGlobalMatrixArray(unity_WorldToShadow, WorldToShadow);
			// But guess what? SetGlobalMatrixArray doesn't work. At all. So we do this nonsense instead:
			buffer.SetGlobalMatrix(unity_WorldToShadow0, WorldToShadow[0]);
			buffer.SetGlobalMatrix(unity_WorldToShadow1, WorldToShadow[1]);
			buffer.SetGlobalMatrix(unity_WorldToShadow2, WorldToShadow[2]);
			buffer.SetGlobalMatrix(unity_WorldToShadow3, WorldToShadow[3]);
			lightShadowData = new Vector4(0.0F, 66.66666F, 0.33333333F, -3.0F);
			buffer.SetGlobalVector(_LightShadowData, lightShadowData);
			CoreUtils.SetKeyword(buffer, "SHADOWS_SPLIT_SPHERES", true);
			newMeshStruct.horizontal_fov_degrees = camera.fieldOfView * camera.aspect;
			newMeshStruct.vertical_fov_degrees = camera.fieldOfView;
			newMeshStruct.far_plane_distance = camera.farClipPlane;
			if (fullscreenMesh == null || !fullScreenMeshStruct.Equals(newMeshStruct))
			{
				fullScreenMeshStruct = newMeshStruct;
				fullscreenMesh = teleport.RenderingUtils.CreateFullscreenMesh(fullScreenMeshStruct);
			}

			string smf = shadowMaterial.GetTag("ShadowMapFilter", false);
			int shaderPass = 0;
			if (perFrameLightProperties.visibleLight.light.shadows == LightShadows.Soft)
				shaderPass = 1;
			buffer.DrawMesh(fullscreenMesh, Matrix4x4.identity, shadowMaterial, 0, shaderPass);

			//buffer.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1, properties);
			buffer.EndSample("Shadow");
			// for next step:
			shadowFadeCenterAndType.w = 1;
			buffer.SetGlobalVector(unity_ShadowFadeCenterAndType, shadowFadeCenterAndType);
			context.ExecuteCommandBuffer(buffer);
			CommandBufferPool.Release(buffer);
		}
	}
}