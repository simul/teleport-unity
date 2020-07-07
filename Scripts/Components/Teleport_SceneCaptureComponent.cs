using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

using uid = System.UInt64;

namespace teleport
{
	public class Teleport_SceneCaptureComponent : MonoBehaviour
	{
		#region DLLImports
		[DllImport("SimulCasterServer")]
		public static extern float GetBandwidthInKbps(uid clientID);
		#endregion

		public uid clientID = 0; // This needs to be set by a session component instance after start
		public RenderTexture sceneCaptureTexture = null;
		public Camera cam = null;

		TeleportSettings teleportSettings=null;

		List<float> bandwidths = new List<float>();
		float settingsDuration = 0.0f;

		public RenderTexture RendererTexture 
		{
			get; private set;
		}

		public VideoEncoder VideoEncoder
		{
			get; private set;
		}

		public UInt32 CurrentTagID
		{
			get; set;
		}

		public static Teleport_SceneCaptureComponent RenderingSceneCapture
		{
			get; private set;
		}

		void Start()
		{
			teleportSettings = TeleportSettings.GetOrCreateSettings();
			Initialize();
		}

		private void OnEnable()
		{
		   
		}

		void OnDisable()
		{
			ReleaseResources();
		}

		void ReleaseResources()
		{
			cam = null;
			sceneCaptureTexture = null;
			RendererTexture = null;
			VideoEncoder = null;
		}

		void LateUpdate()
		{
			// for now just get latest client
			uid id = Teleport_SessionComponent.GetLastClientID();
	 
			if (id != clientID)
			{
				clientID = id;
				if (clientID != 0)
				{
					CurrentTagID = 0;
					VideoEncoder = new VideoEncoder(clientID);
				}
				else
				{
					VideoEncoder = null;
				}
				bandwidths.Clear();
				settingsDuration = 0.0f;
			}

			if (cam && sceneCaptureTexture)
			{
				ManagePerformance();
				RenderToTexture();
			}
		}

		void Initialize()
		{
			CurrentTagID = 0;

			GameObject obj = gameObject;
			obj.transform.position = transform.position;
			obj.transform.rotation = Quaternion.identity;
			cam = gameObject.GetComponent<Camera>();
			if (!cam)
			{
				cam=gameObject.AddComponent<Camera>();
			}
			cam.nearClipPlane = 0.05f;
			cam.farClipPlane = 1000;
			if (teleportSettings.casterSettings.usePerspectiveRendering)
			{
				cam.fieldOfView = teleportSettings.casterSettings.perspectiveFOV;
				cam.aspect = (float)teleportSettings.casterSettings.sceneCaptureWidth / (float)teleportSettings.casterSettings.sceneCaptureHeight;
			}
			else
			{
				cam.fieldOfView = 90;
				cam.aspect = 1;
			}
			cam.depthTextureMode |= DepthTextureMode.Depth;

			cam.enabled = false;

			CreateResources();
		}

		void ManagePerformance()
		{
			var settings = teleportSettings.casterSettings;

			if (!settings.useDynamicQuality)
			{
				return;
			}

			settingsDuration += Time.deltaTime * 1000;

			bandwidths.Add(GetBandwidthInKbps(clientID) / 1000.0f);
			if (settingsDuration >= teleportSettings.casterSettings.bandwidthCalculationInterval)
			{
				float bandwidth = GetMedianBandwidth();
				bandwidths.Clear();
				settingsDuration = 0.0f;

				if (settings.usePerspectiveRendering)
				{
					int prevWidth = settings.sceneCaptureWidth;
					int prevHeight = settings.sceneCaptureHeight;

					if (bandwidth > 50)
					{
						settings.sceneCaptureWidth = 3840;
						settings.sceneCaptureHeight = 2160;
					}
					else if (bandwidth > 25)
					{
						settings.sceneCaptureWidth = 1920;
						settings.sceneCaptureHeight = 1080;
					}
					else
					{
						settings.sceneCaptureWidth = 1280;
						settings.sceneCaptureHeight = 720;
					}

					if (prevWidth != settings.sceneCaptureWidth || prevHeight != settings.sceneCaptureHeight)
					{
						ChangeQuality(bandwidth);
					}
				}
				else
				{
					float prevSize = settings.captureCubeTextureSize;

					if (bandwidth > 50)
					{
						settings.captureCubeTextureSize = 1024;
					}
					else if (bandwidth > 35)
					{
						settings.captureCubeTextureSize = 512;
					}
					else if (bandwidth > 25)
					{
						settings.captureCubeTextureSize = 256;
					}
					else
					{
						settings.captureCubeTextureSize = 128;
					}

					if (prevSize != settings.captureCubeTextureSize)
					{
						ChangeQuality(bandwidth);
					}
				}			
			}		
		}

		void ChangeQuality(float bandwidth)
		{
			var settings = teleportSettings.casterSettings;

			settings.averageBitrate = (int)bandwidth * 1000000;
			settings.maxBitrate = settings.averageBitrate * 2;

			CreateResources();
			if (VideoEncoder != null)
			{
				VideoEncoder.Reconfigure = true;
			}
		}

		float GetMedianBandwidth()
		{
			var size = bandwidths.Count;

			if (size < 1)
			{
				return 0;
			}

			bandwidths.Sort();

			if (size % 2 == 0)
			{
				int index = size / 2;
				return (bandwidths[index] + bandwidths[index - 1]) / 2.0f;
			}

			return bandwidths[(size - 1) / 2];
		}

		void RenderToTexture()
		{
			cam.transform.position = transform.position;

			if (teleportSettings.casterSettings.usePerspectiveRendering)
			{
				cam.transform.rotation = transform.rotation;
			}

			// Update name in case client ID changed
			cam.name = TeleportRenderPipeline.CUBEMAP_CAM_PREFIX + clientID;
			RenderingSceneCapture = this;
			cam.Render();

			CurrentTagID = (CurrentTagID + 1) % 32;
		}

		void CreateResources()
		{
			var settings = teleportSettings.casterSettings;
			if (settings.usePerspectiveRendering)
			{	// Add 4 to the scene capture texture height for the tag data id
				CreateTextures(settings.sceneCaptureWidth, settings.sceneCaptureHeight + 4, settings.sceneCaptureWidth, settings.sceneCaptureHeight, 0);
			}
			else
			{
				int size = (int)teleportSettings.casterSettings.captureCubeTextureSize;
				CreateTextures(size * 3, size * 3, size, size, 24);
			}
		}

		void CreateTextures(int captureTexWidth, int captureTexHeight, int rendererTexWidth, int rendererTexHeight, int rendererTexDepth)
		{
			RenderTextureFormat format;
			if (teleportSettings.casterSettings.use10BitEncoding)
			{
				format = RenderTextureFormat.ARGB64;
			}
			else
			{
				format = RenderTextureFormat.ARGB32;
			}

			sceneCaptureTexture = new RenderTexture(captureTexWidth, captureTexHeight, 0, format, RenderTextureReadWrite.Default);
			sceneCaptureTexture.name = "Scene Capture Texture";
			sceneCaptureTexture.dimension = TextureDimension.Tex2D;
			sceneCaptureTexture.useMipMap = false;
			sceneCaptureTexture.autoGenerateMips = false;
			sceneCaptureTexture.enableRandomWrite = true;
			sceneCaptureTexture.Create();

			RendererTexture = new RenderTexture(rendererTexWidth, rendererTexHeight, rendererTexDepth, format, RenderTextureReadWrite.Default);
			RendererTexture.name = "Scene Capture Renderer Texture";
			RendererTexture.dimension = TextureDimension.Tex2D;
			RendererTexture.useMipMap = false;
			RendererTexture.autoGenerateMips = false;
			RendererTexture.enableRandomWrite = false;
			RendererTexture.Create();
		}
	}
}
