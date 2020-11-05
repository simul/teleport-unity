using UnityEngine;
using UnityEngine.Rendering;

namespace teleport
{
	public class TeleportShadows
	{
		public TeleportRenderSettings renderSettings = null;
		const int maxShadowedDirectionalLightCount = 4;
		int ShadowedDirectionalLightCount = 0;
		int tileSize = 0;
		static int _ShadowMapTexture			= Shader.PropertyToID("_ShadowMapTexture"),
					_CameraDepthTexture			= Shader.PropertyToID("_CameraDepthTexture"),
					unity_ShadowSplitSpheres0	= Shader.PropertyToID("unity_ShadowSplitSpheres0"),
					unity_ShadowSplitSpheres1	= Shader.PropertyToID("unity_ShadowSplitSpheres1"),
					unity_ShadowSplitSpheres2	= Shader.PropertyToID("unity_ShadowSplitSpheres2"),
					unity_ShadowSplitSpheres3	= Shader.PropertyToID("unity_ShadowSplitSpheres3"),
					unity_ShadowSplitSqRadii	= Shader.PropertyToID("unity_ShadowSplitSqRadii"),
					unity_LightShadowBias		= Shader.PropertyToID("unity_LightShadowBias"),
					_LightSplitsNear			= Shader.PropertyToID("_LightSplitsNear"),
					_LightSplitsFar				= Shader.PropertyToID("_LightSplitsFar"),
					unity_WorldToShadow0		= Shader.PropertyToID("unity_WorldToShadow0"),
					unity_WorldToShadow1		= Shader.PropertyToID("unity_WorldToShadow1"),
					unity_WorldToShadow2		= Shader.PropertyToID("unity_WorldToShadow2"),
					unity_WorldToShadow3		= Shader.PropertyToID("unity_WorldToShadow3"),
					_LightShadowData			= Shader.PropertyToID("_LightShadowData"),
					unity_ShadowFadeCenterAndType = Shader.PropertyToID("unity_ShadowFadeCenterAndType"),
					something_nonexistent		= Shader.PropertyToID("something_nonexistent");
		Mesh fullscreenMesh = null;
		public void ReserveDirectionalShadows(CullingResults cullingResults, Light light, int visibleLightIndex)
		{
			if (ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount &&
					light.shadows != LightShadows.None && light.shadowStrength > 0f &&
					cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
			{
			}
		}

		public void Setup(ScriptableRenderContext context, CullingResults cullingResults)
		{
			ShadowedDirectionalLightCount = 0;
		}
		public void Cleanup(ScriptableRenderContext context, CommandBuffer buffer)
		{
			if (ShadowedDirectionalLightCount > 0)
			{
				//	buffer.ReleaseTemporaryRT(dirShadowAtlasId);
				ExecuteBuffer(context,buffer);
			}
		}
		void ExecuteBuffer(ScriptableRenderContext context,CommandBuffer buffer)
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
		public void RenderShadowCascadeForLight(ScriptableRenderContext context,CommandBuffer buffer,CullingResults cullingResults, Light light, int visibleLightIndex, ref PerFramePerCameraLightProperties perFramePerCamera, int cascadeCount, int index)
		{
			if (index >= cascadeCount)
				return;
			int split = cascadeCount <= 1 ? 1 : 2;

			var shadowSettings = new ShadowDrawingSettings(cullingResults, visibleLightIndex);

			Vector3 ratios = QualitySettings.shadowCascade4Split;
			bool result = false;
			if (light.type == LightType.Directional)
			{
				result = cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
					visibleLightIndex, index, cascadeCount, ratios, tileSize, QualitySettings.shadowNearPlaneOffset,//+perFrameLightProperties.visibleLight.light.shadowNearPlane
					out perFramePerCamera.cascades[index].viewMatrix, out perFramePerCamera.cascades[index].projectionMatrix,
					out perFramePerCamera.cascades[index].splitData
				);
			}
			else if (light.type == LightType.Spot)
			{
				Debug.Assert(index == 0);
				result = cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(
					visibleLightIndex, out perFramePerCamera.cascades[index].viewMatrix, out perFramePerCamera.cascades[index].projectionMatrix,
					out perFramePerCamera.cascades[index].splitData
				);
			}
			else if (light.type == LightType.Point)
			{
				Debug.Assert(index == 0);
				result = cullingResults.ComputePointShadowMatricesAndCullingPrimitives(
					visibleLightIndex, CubemapFace.Unknown,0.0F,out perFramePerCamera.cascades[index].viewMatrix, out perFramePerCamera.cascades[index].projectionMatrix,
					out perFramePerCamera.cascades[index].splitData
				);
			}
			if(result)
			{
				VisibleLight visibleLight = cullingResults.visibleLights[visibleLightIndex];
				Rect vp = GetTileViewport(index, tileSize, split);
				Vector4 sph = perFramePerCamera.cascades[index].splitData.cullingSphere;
				sph.w *= sph.w;
				perFramePerCamera.cascades[index].splitData.cullingSphere = sph;
				shadowSettings.splitData = perFramePerCamera.cascades[index].splitData;
				buffer.SetViewport(vp);
				buffer.SetViewProjectionMatrices(perFramePerCamera.cascades[index].viewMatrix, perFramePerCamera.cascades[index].projectionMatrix);
				Matrix4x4 viewproj  = ShadowUtils.GetShadowTransformForRender(perFramePerCamera.cascades[index].projectionMatrix, perFramePerCamera.cascades[index].viewMatrix);
				Vector4 bias4 = new Vector4(visibleLight.light.shadowBias, visibleLight.light.shadowNormalBias, 0.0f, 0.0f);
				//			new Vector4(visibleLight.light.shadowBias, visibleLight.light.shadowBias, visibleLight.light.shadowBias, visibleLight.light.shadowBias);

				Vector4 shadowBias = new Vector4(0, 0, 0, 0);
				shadowBias = teleport.ShadowUtils.GetShadowBias(ref visibleLight, visibleLightIndex, bias4, visibleLight.light.shadows == LightShadows.Soft, perFramePerCamera.cascades[index].projectionMatrix, tileSize);

				//Vector4 lightShadowBias = new Vector4(-0.001484548F, 1.0F, 0.017146875F, 0F);
				buffer.SetGlobalVector(unity_LightShadowBias, shadowBias);
				ExecuteBuffer(context,buffer);
				context.DrawShadows(ref shadowSettings);
			}
		}
		public void RenderShadowsForLight(ScriptableRenderContext context, CullingResults cullingResults, Light light, int visibleLightIndex, Camera camera, ref PerFrameLightProperties perFrameLightProperties, ref PerFramePerCameraLightProperties perFramePerCameraLightProperties, int cascadeCount)
		{
			string sampleName = "RenderShadowsForLight " +light.name+ "("+light.type.ToString()+")";
			CommandBuffer buffer = CommandBufferPool.Get(sampleName);
			int atlasSize = (int)renderSettings.directional.atlasSize;
			int split = cascadeCount <= 1 ? 1 : 2;
			tileSize = atlasSize / split;
			if (perFrameLightProperties.shadowAtlasTexture == null || perFrameLightProperties.shadowAtlasTexture.width != atlasSize)
			{
				perFrameLightProperties.shadowAtlasTexture = new RenderTexture(atlasSize, atlasSize, 1, RenderTextureFormat.Shadowmap);
				perFrameLightProperties.filteredShadowTexture = new RenderTexture(tileSize, tileSize, 1, RenderTextureFormat.RGB111110Float); 
			}
			buffer.SetRenderTarget(perFrameLightProperties.shadowAtlasTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
			buffer.ClearRenderTarget(true, true, Color.clear);
			ExecuteBuffer(context, buffer);
			float invCount = 1.0F / (float)cascadeCount;
			perFramePerCameraLightProperties.shadowFadeCenterAndType.x = perFramePerCameraLightProperties.shadowFadeCenterAndType.y = perFramePerCameraLightProperties.shadowFadeCenterAndType.z = 0;
			for (int i = 0; i <cascadeCount; i++)
			{
				RenderShadowCascadeForLight(context, buffer,cullingResults, light, visibleLightIndex, ref perFramePerCameraLightProperties, cascadeCount, i);
				perFramePerCameraLightProperties.shadowFadeCenterAndType.x += invCount * perFramePerCameraLightProperties.cascades[i].splitData.cullingSphere.x;
				perFramePerCameraLightProperties.shadowFadeCenterAndType.y += invCount * perFramePerCameraLightProperties.cascades[i].splitData.cullingSphere.y;
				perFramePerCameraLightProperties.shadowFadeCenterAndType.z += invCount * perFramePerCameraLightProperties.cascades[i].splitData.cullingSphere.z;
			}
			ExecuteBuffer(context,buffer);
			perFramePerCameraLightProperties.shadowFadeCenterAndType.w = 1;
			if(light.type==LightType.Spot)
			{
				perFramePerCameraLightProperties.shadowFadeCenterAndType = camera.transform.position;
				perFramePerCameraLightProperties.shadowFadeCenterAndType += 4.0F*(Vector4)(camera.transform.forward);
				perFramePerCameraLightProperties.shadowFadeCenterAndType.w = 1.0F;
			}
			buffer.SetGlobalVector(unity_ShadowFadeCenterAndType, perFramePerCameraLightProperties.shadowFadeCenterAndType);
			ExecuteBuffer(context, buffer);
		}
		static Shader shadowShader = null;
		static Material shadowMaterial = null;
		Vector4 lightShadowData = new Vector4();
		public static void GetShadowTransforms(ref PerFramePerCameraLightProperties perFrameLightProperties, int atlasSize, int cascadeCount)
		{
			int split = cascadeCount <= 1 ? 1 : 2;
			for (int i = 0; i < cascadeCount; i++)
			{
				PerFrameShadowCascadeProperties cascade = perFrameLightProperties.cascades[i];
				perFrameLightProperties.WorldToShadow[i] = teleport.ShadowUtils.GetShadowTransformForShader(cascade.projectionMatrix, cascade.viewMatrix);
				teleport.ShadowUtils.ApplySliceTransform(ref perFrameLightProperties.WorldToShadow[i], i, atlasSize, split, atlasSize / split);
			}
		}
		teleport.RenderingUtils.FullScreenMeshStruct fullScreenMeshStruct = new teleport.RenderingUtils.FullScreenMeshStruct();
		teleport.RenderingUtils.FullScreenMeshStruct newMeshStruct = new teleport.RenderingUtils.FullScreenMeshStruct();
		/// <summary>
		/// For a given camera, render the shadows into screenspace 
		/// </summary>
		public void ApplyShadowConstants(ScriptableRenderContext context, CommandBuffer buffer, Camera camera, CullingResults cullingResults, PerFrameLightProperties perFrameLightProperties)
		{
			PerFramePerCameraLightProperties perFramePerCameraLightProperties = perFrameLightProperties.perFramePerCameraLightProperties[camera];
			//buffer.SetGlobalVector(unity_LightShadowBias, new Vector4(perFrameLightProperties.visibleLight.light.shadowBias,0,0,0));
			//buffer.SetGlobalMatrixArray(unity_WorldToShadow, WorldToShadow);
			// But guess what? SetGlobalMatrixArray doesn't work. At all. So we do this nonsense instead:
			buffer.SetGlobalMatrix(unity_WorldToShadow0, perFramePerCameraLightProperties.WorldToShadow[0]);
			buffer.SetGlobalMatrix(unity_WorldToShadow1, perFramePerCameraLightProperties.WorldToShadow[1]);
			buffer.SetGlobalMatrix(unity_WorldToShadow2, perFramePerCameraLightProperties.WorldToShadow[2]);
			buffer.SetGlobalMatrix(unity_WorldToShadow3, perFramePerCameraLightProperties.WorldToShadow[3]);
		}
		/// <summary>
		/// For a given camera, render the shadows into screenspace 
		/// </summary>
		public void RenderScreenspaceShadows(ScriptableRenderContext context, Camera camera, CullingResults cullingResults, PerFrameLightProperties perFrameLightProperties, int cascadeCount
				, RenderTexture depthTexture)
		{
			if (!perFrameLightProperties.perFramePerCameraLightProperties.ContainsKey(camera))
			{
				return;
			}
			PerFramePerCameraLightProperties perFramePerCameraLightProperties = perFrameLightProperties.perFramePerCameraLightProperties[camera];
			CommandBuffer buffer = CommandBufferPool.Get("RenderScreenspaceShadows");
			buffer.BeginSample("Shadow");
			context.SetupCameraProperties(camera);
			if (perFramePerCameraLightProperties.screenspaceShadowTexture == null || perFramePerCameraLightProperties.screenspaceShadowTexture.width != camera.pixelWidth || perFramePerCameraLightProperties.screenspaceShadowTexture.height != camera.pixelHeight)
			{
				perFramePerCameraLightProperties.screenspaceShadowTexture = new RenderTexture(camera.pixelWidth, camera.pixelHeight,1, RenderTextureFormat.BGRA32);
			}
			buffer.SetGlobalTexture(_CameraDepthTexture, depthTexture);
			buffer.SetRenderTarget(perFramePerCameraLightProperties.screenspaceShadowTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store); 
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
					buffer.EndSample("Shadow");
					return;
				}
			}
			shadowMaterial.SetTexture("_ShadowMapTexture", perFrameLightProperties.shadowAtlasTexture, RenderTextureSubElement.Depth);

			GetShadowTransforms(ref perFramePerCameraLightProperties, (int)renderSettings.directional.atlasSize, cascadeCount);
			buffer.SetGlobalVector(unity_ShadowSplitSpheres0, perFramePerCameraLightProperties.cascades[0].splitData.cullingSphere);
			buffer.SetGlobalVector(unity_ShadowSplitSpheres1, perFramePerCameraLightProperties.cascades[1].splitData.cullingSphere);
			buffer.SetGlobalVector(unity_ShadowSplitSpheres2, perFramePerCameraLightProperties.cascades[2].splitData.cullingSphere);
			buffer.SetGlobalVector(unity_ShadowSplitSpheres3, perFramePerCameraLightProperties.cascades[3].splitData.cullingSphere);
			Vector4 sqr_rad = new Vector4(perFramePerCameraLightProperties.cascades[0].splitData.cullingSphere.w
										, perFramePerCameraLightProperties.cascades[1].splitData.cullingSphere.w
										, perFramePerCameraLightProperties.cascades[2].splitData.cullingSphere.w
										, perFramePerCameraLightProperties.cascades[3].splitData.cullingSphere.w);
			buffer.SetGlobalVector(unity_ShadowSplitSqRadii, sqr_rad);
			ApplyShadowConstants(context, buffer, camera, cullingResults, perFrameLightProperties);
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
			context.ExecuteCommandBuffer(buffer);
			CommandBufferPool.Release(buffer);
		}
	}
}