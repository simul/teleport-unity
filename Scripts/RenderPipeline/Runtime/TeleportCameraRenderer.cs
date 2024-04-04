using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

using uid = System.UInt64;

namespace teleport
{
	/// A class to render in a scriptable pipeline using Unity's default materials and shaders.
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
		// These are the views for  a GL-style cubemap with Y vertical.
		static CamView frontCamViewGL = new CamView(Vector3.forward, Vector3.up);
		static CamView backCamViewGL = new CamView(Vector3.back, Vector3.up);
		static CamView rightCamViewGL = new CamView(Vector3.right, Vector3.up);
		static CamView leftCamViewGL = new CamView(Vector3.left, Vector3.up);
		static CamView upCamViewGL = new CamView(Vector3.up, Vector3.right);
		static CamView downCamViewGL = new CamView(Vector3.down, Vector3.right);
		static CamView[] faceCamViewsGL = new CamView[] { backCamViewGL, frontCamViewGL,  downCamViewGL, upCamViewGL, leftCamViewGL, rightCamViewGL };
		// These are the views for an engineering style cubemap with Z up.
		static CamView backCamView = new CamView(Vector3.forward, Vector3.up);
		static CamView frontCamView= new CamView(Vector3.back, Vector3.down);
		static CamView rightCamView = new CamView(Vector3.right, Vector3.forward);
		static CamView leftCamView = new CamView(Vector3.left, Vector3.forward);
		static CamView upCamView = new CamView(Vector3.up, Vector3.forward);
		static CamView downCamView = new CamView(Vector3.down, Vector3.forward);
		static CamView[] faceCamViewsEngineering = new CamView[] { leftCamView, rightCamView, frontCamView, backCamView, downCamView, upCamView };

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

		bool Cull(ScriptableRenderContext context, Camera camera, out CullingResults cullingResults,bool for_streaming_cubemap=false)
		{
			if (for_streaming_cubemap)
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
			if (for_streaming_cubemap)
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
		void DrawOpaqueGeometry(ScriptableRenderContext context, Camera camera, bool flipCulling,CullingResults cullingResults, int layerMask, uint renderingMask, TeleportRenderPipeline.LightingOrder lightingOrder
			,bool highlight, Color highlightColour, RenderTexture renderTarget = null, int face = -1)
		{
			// The generic textures accessible from all default shaders...
			var buffer = new CommandBuffer();
			buffer.SetInvertCulling(flipCulling);
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

			buffer.SetGlobalVector("highlightColour", highlightColour);
			context.ExecuteCommandBuffer(buffer);
			buffer.Release();

			var sortingSettings = new SortingSettings(camera)
			{
				criteria = SortingCriteria.CommonOpaque
			};
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
					Shader sh= Shader.Find("Teleport/HighlightShader");
					if(sh!= null)
						highlightMaterial = new Material(sh);
				}
				drawingSettings.overrideMaterial = highlightMaterial;
			}
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
		void DrawTransparentGeometry(ScriptableRenderContext context, Camera camera,bool flipCulling, CullingResults cullingResults, int layerMask, uint renderingMask)
		{
			var buffer = new CommandBuffer();
			buffer.SetInvertCulling(flipCulling);
			context.ExecuteCommandBuffer(buffer);
			buffer.Release();
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
#if UNITY_EDITOR
		public void GenerateEnvMaps(ScriptableRenderContext context)
		{
			for (int face = 0; face < 6; face++)
			{
				VideoEncoding.GenerateSpecularMips(context, Monitor.Instance.environmentCubemap, Monitor.Instance.environmentRenderTexture, 1.0f, face, 0, avs.AxesStandard.EngineeringStyle);
				VideoEncoding.GenerateSpecularMips(context, Monitor.Instance.environmentCubemap, Monitor.Instance.specularRenderTexture,Monitor.Instance.specularMultiplier, face, 0, avs.AxesStandard.EngineeringStyle);
				HashSet<Light> bakedLights =new HashSet<Light> ();
				Light[] lights = UnityEngine.Object.FindObjectsOfType<Light>();
				// include only baked lights in range.
				foreach(Light l in lights)
				{
					if(l.lightmapBakeType!=LightmapBakeType.Realtime)
					{
						bakedLights.Add(l);
					}
				}
				VideoEncoding.GenerateDiffuseCubemap(context, Monitor.Instance.environmentCubemap, bakedLights, Monitor.Instance.diffuseRenderTexture, face, Monitor.Instance.specularMultiplier, avs.AxesStandard.EngineeringStyle);
			}
		}
#endif
		public void Render(ScriptableRenderContext context, Camera camera, int layerMask, uint renderingMask)
		{
			bool is_reflection=(camera.cameraType==CameraType.Reflection);
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
			DrawDepthPass(context, camera, layerMask, renderingMask, cullingResultsAll);
			teleportLighting.RenderScreenspaceShadows(context, camera, lightingOrder, cullingResultsAll, depthTexture);
			context.SetupCameraProperties(camera);
			Clear(context, camera);
			PrepareForSceneWindow(context, camera);
			// We draw everything first:
			DrawOpaqueGeometry(context, camera, is_reflection, cullingResultsAll,layerMask, renderingMask,lightingOrder,false, teleportSettings.highlightStreamableColour);
			DrawTransparentGeometry(context, camera, is_reflection, cullingResultsAll,layerMask, renderingMask);
			// Now we highlight the streamed objects:
			if (teleportSettings.highlightStreamables)
			{
				if (Application.isPlaying)
				{
					renderingMask = (uint)(1 << 26);   // Render ONLY the items that are streamed to this client.
				}
				else
				{
					renderingMask = (uint)1 << 31;   // When not playing, only streamables have this bit set.
				}
				DrawOpaqueGeometry(context, camera, is_reflection, cullingResultsAll,layerMask, renderingMask, lightingOrder, true,teleportSettings.highlightStreamableColour);
			}
			if (teleportSettings.highlightNonStreamables)
			{
				renderingMask = (uint)1 << 30;   // When not playing, only non-streamables have this bit set.
				DrawOpaqueGeometry(context, camera, is_reflection, cullingResultsAll, layerMask, renderingMask, lightingOrder, true,teleportSettings.highlightNonStreamableColour);
			}
#if UNITY_EDITOR
			DrawUnsupportedShaders(context, camera);
#endif
			DrawGizmos(context, camera);
			EndSample(context, samplename);
			EndCamera(context, camera);
		}
		//int m = 0;

		public uid ClientID = 0;
		
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
			if (!Teleport_SessionComponent.sessions.ContainsKey(ClientID))
				return;

			var session = Teleport_SessionComponent.sessions[ClientID];

			var sceneCapture = session.sceneCaptureComponent;

			if (!sceneCapture.videoTexture)
			{
				Debug.LogError("The video encoder texture must not be null");
				return;
			}
			
			camera.transform.position = session.head.transform.position;
			camera.transform.rotation = session.head.transform.rotation;

			var oldPos = camera.transform.position;
			var oldRot = camera.transform.rotation;

			var camDir = camera.transform.forward;
			camDir.Normalize();

			camera.targetTexture = sceneCapture.rendererTexture;
			if (ClientID != 0)
				UpdateStreamables(context, ClientID, camera);

			CullingResults cullingResultsAll;
			float oldNearClip = camera.nearClipPlane;
			if (teleportSettings.serverSettings.isStreamingGeometry)
			{
				camera.nearClipPlane = teleportSettings.serverSettings.detectionSphereRadius * 0.5f;
			}
			
			if (Cull(context, camera, out cullingResultsAll, !teleportSettings.serverSettings.usePerspectiveRendering))
			{
				TeleportRenderPipeline.LightingOrder lightingOrder = TeleportRenderPipeline.GetLightingOrder(cullingResultsAll);

				teleportLighting.renderSettings = renderSettings;
				teleportLighting.RenderShadows(context, camera, cullingResultsAll, lightingOrder);

				// We downscale the whole diffuse so that it peaks at the maximum colour value.
				float max_light = 1.0f;
				var bakedLights=SessionComponent.GeometryStreamingService.GetBakedLights();
				foreach (Light light in bakedLights)
				{
					if(light.type!=LightType.Directional)
						continue;
					var clr = light.intensity * light.color.linear;
					max_light = Math.Max(Math.Max(Math.Max(max_light, clr.r), clr.g), clr.b);
				}
				float diffuseAmbientScale=1.0F/max_light;
				if (teleportSettings.serverSettings.usePerspectiveRendering)
				{
					// Draw scene
					camera.fieldOfView = teleportSettings.serverSettings.perspectiveFOV;
					DrawPerspective(context, camera, cullingResultsAll, lightingOrder);
					// Draw cubemaps
					camera.fieldOfView = 90.0F;
					for (int i = 0; i < VideoEncoding.NumFaces; ++i)
					{
						DrawCubemapFace(context, camera, camDir, cullingResultsAll, lightingOrder, i, diffuseAmbientScale);
					}
				}
				else
				{
					for (int i = 0; i < VideoEncoding.NumFaces; ++i)
					{
						DrawCubemapFace(context, camera, camDir, cullingResultsAll, lightingOrder, i, diffuseAmbientScale);
					}
				}
				if (teleportSettings.serverSettings.StreamWebcam)
                {
					videoEncoding.EncodeWebcam(context, camera, SessionComponent.clientSettings.webcamPos, SessionComponent.clientSettings.webcamSize, sceneCapture);	
				}
				videoEncoding.EncodeTagID(context, camera, sceneCapture);
				context.Submit();

				camera.nearClipPlane = oldNearClip;
				camera.transform.position = oldPos;
				camera.transform.rotation = oldRot;

				var videoEncoder = sceneCapture.VideoEncoder;
				if (ClientID != 0 && teleportSettings.serverSettings.StreamVideo && videoEncoder != null)
				{
					var tagDataID = sceneCapture.CurrentTagID;
					videoEncoder.CreateEncodeCommands(context, camera, tagDataID, max_light);
				}
			}
		}

		// This function leverages the Unity rendering pipeline functionality to get information about what lights etc should be visible to the client.
		void UpdateStreamables(ScriptableRenderContext context, uid clientID, Camera camera)
		{
			float fov = camera.fieldOfView;
			float aspect = camera.aspect;
			float farClipPlane = camera.farClipPlane;

			camera.fieldOfView = 180.0F;
			camera.aspect = 2.0F;
			camera.farClipPlane = 10.0F;

			ScriptableCullingParameters p;
			if (camera.TryGetCullingParameters(out p))
			{
				teleport.Monitor monitor = teleport.Monitor.Instance;
				CullingResults cullingResults = context.Cull(ref p);
				Light[] lights = new Light[cullingResults.visibleLights.Length];
				for (int i = 0; i < lights.Length; i++)
				{
					lights[i] = cullingResults.visibleLights[i].light;
				}
				Teleport_SessionComponent.sessions[clientID].SetStreamedLights(lights);
			}
			camera.fieldOfView = fov;
			camera.aspect = aspect;
			camera.farClipPlane = farClipPlane;
		}
		void DrawCubemapFace(ScriptableRenderContext context, Camera camera, Vector3 camDir, CullingResults cullingResultsAll, TeleportRenderPipeline.LightingOrder lightingOrder, int face,float diffuseAmbientScale)
		{
			bool GL=false;
			CamView camView;
			if(GL)
				camView=faceCamViewsGL[face];
			else
				camView=faceCamViewsEngineering[face];
			Vector3 to = camView.forward;

			// Don't render faces that are completely invisible to the camera.
			if (Vector3.Dot(camDir, camView.forward) < 0.0f)
			{
				//return;
			}

			Vector3 pos = camera.transform.position;

			camera.transform.LookAt(pos + to, camView.up);
			
			Matrix4x4 view = camera.transform.localToWorldMatrix;
			view = Matrix4x4.Inverse(view);
			/*if(GL)
			{
				view.m20 *= -1f;
				view.m21 *= -1f;
				view.m22 *= -1f;
				view.m23 *= -1f;
			}*/
			int layerMask = 0x7FFFFFFF;
			uint renderingMask = (uint)((1 << 25)|0x7);	// canvasrenderers hard coded to have mask 0x7..!
			camera.worldToCameraMatrix = view;
			string sampleName = camera.gameObject.name + " Face " + face;

			var sceneCapture = SessionComponent.sceneCaptureComponent;
			StartSample(context, sampleName);
			
			if (teleportSettings.serverSettings.usePerspectiveRendering)
			{
				context.SetupCameraProperties(camera);
				Clear(context, camera);
				PrepareForSceneWindow(context, camera);

				DrawOpaqueGeometry(context, camera, false, cullingResultsAll, layerMask, renderingMask, lightingOrder, false, teleportSettings.highlightStreamableColour,sceneCapture.UnfilteredCubeTexture, face);
				DrawTransparentGeometry(context, camera, false, cullingResultsAll, layerMask, renderingMask);
		
				VideoEncoding.GenerateSpecularMips(context, sceneCapture.UnfilteredCubeTexture, sceneCapture.SpecularCubeTexture, 1.0F,face, 0, avs.AxesStandard.EngineeringStyle);
				VideoEncoding.GenerateDiffuseCubemap(context, sceneCapture.SpecularCubeTexture, SessionComponent.GeometryStreamingService.GetBakedLights(), sceneCapture.DiffuseCubeTexture, face,diffuseAmbientScale, avs.AxesStandard.GlStyle);
				videoEncoding.EncodeLightingCubemaps(context, sceneCapture, SessionComponent,face);
			}
			else
			{
				DrawDepthPass(context, camera, layerMask, renderingMask, cullingResultsAll);
				teleportLighting.RenderScreenspaceShadows(context, camera, lightingOrder, cullingResultsAll, depthTexture);
				context.SetupCameraProperties(camera);
				Clear(context, camera);
				PrepareForSceneWindow(context, camera);

				DrawOpaqueGeometry(context, camera, !GL,cullingResultsAll, layerMask, renderingMask, lightingOrder,false, teleportSettings.highlightStreamableColour);
				DrawTransparentGeometry(context, camera, !GL,cullingResultsAll, layerMask, renderingMask);
				
				// The unfiltered (reflection cube) should render close objects (though maybe only static ones).
				float oldNearClip		= camera.nearClipPlane;
				camera.nearClipPlane	= 5.0f;
				videoEncoding.CopyOneFace(context, SessionComponent.sceneCaptureComponent.rendererTexture, sceneCapture.UnfilteredCubeTexture, face);
				camera.nearClipPlane	= oldNearClip;
				VideoEncoding.GenerateSpecularMips(context, SessionComponent.sceneCaptureComponent.UnfilteredCubeTexture, sceneCapture.SpecularCubeTexture, 1.0F,face, 0, avs.AxesStandard.GlStyle);
				VideoEncoding.GenerateDiffuseCubemap(context, SessionComponent.sceneCaptureComponent.SpecularCubeTexture, SessionComponent.GeometryStreamingService.GetBakedLights(), sceneCapture.DiffuseCubeTexture, face, diffuseAmbientScale, avs.AxesStandard.GlStyle);
				if(SessionComponent.clientSettings.captureCubeTextureSize>0&& SessionComponent.clientSettings.backgroundMode==BackgroundMode.VIDEO)
				{ 
					videoEncoding.EncodeColor(context, camera, face, sceneCapture);
					if (!teleportSettings.serverSettings.useAlphaLayerEncoding)
					{
						int faceSize		= SessionComponent.clientSettings.captureCubeTextureSize;
						int halfFaceSize	= faceSize / 2;
						int offsetX			= VideoEncoding.faceOffsets[face, 0];
						int offsetY			= VideoEncoding.faceOffsets[face, 1];
						var depthViewport	= new Rect(offsetX * halfFaceSize, (faceSize * 2) + (offsetY * halfFaceSize), halfFaceSize, halfFaceSize);
						videoEncoding.EncodeDepth(context, camera, depthViewport, sceneCapture);
					}
				}
				videoEncoding.EncodeLightingCubemaps(context, sceneCapture, SessionComponent, face);
#if UNITY_EDITOR
				DrawUnsupportedShaders(context, camera);
#endif
			}
			
			EndSample(context, sampleName);
			EndCamera(context, camera);
			camera.ResetWorldToCameraMatrix();
		}

		void DrawPerspective(ScriptableRenderContext context, Camera camera, CullingResults cullingResultsAll, TeleportRenderPipeline.LightingOrder lightingOrder)
		{
			//CullingResults cullingResultsAll;
			//if (!Cull(context, camera, out cullingResultsAll))
			//{
			//	return;
			//}
			teleport.Monitor monitor = teleport.Monitor.Instance;

			int layerMask = 0x7FFFFFFF;
			uint renderingMask = 0x7FFFFFFF;
			string sampleName = camera.gameObject.name + " Perspective";

			var sceneCapture = SessionComponent.sceneCaptureComponent;

			StartSample(context, sampleName);
			{
				DrawDepthPass(context, camera, layerMask, renderingMask, cullingResultsAll);
				teleportLighting.RenderScreenspaceShadows(context, camera, lightingOrder, cullingResultsAll, depthTexture);
				context.SetupCameraProperties(camera);
				Clear(context, camera);
				PrepareForSceneWindow(context, camera);

				DrawOpaqueGeometry(context, camera, false,cullingResultsAll, layerMask, renderingMask, lightingOrder,false, teleportSettings.highlightStreamableColour);
				DrawTransparentGeometry(context, camera, false,cullingResultsAll, layerMask, renderingMask);
				if ( teleportSettings.serverSettings.perspectiveWidth > 0)
				{ 
					videoEncoding.EncodeColor(context, camera, 0, sceneCapture);
					if (!teleportSettings.serverSettings.useAlphaLayerEncoding)
					{
						int perspectiveWidth = teleportSettings.serverSettings.perspectiveWidth;
						int perspectiveHeight = teleportSettings.serverSettings.perspectiveHeight;
						var depthViewport = new Rect(0, perspectiveHeight, perspectiveWidth / 2, perspectiveHeight / 2);
						videoEncoding.EncodeDepth(context, camera, depthViewport, sceneCapture);
					}
				}
#if UNITY_EDITOR
				DrawUnsupportedShaders(context, camera);
#endif
			}
			EndSample(context, sampleName);
			EndCamera(context, camera);
		}
	}
}