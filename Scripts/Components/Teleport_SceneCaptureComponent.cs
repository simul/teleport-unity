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
		public uid clientID = 0; // This needs to be set by a session component instance after start
		public RenderTexture videoTexture = null;
		public RenderTexture rendererTexture = null;
		public RenderTexture DiffuseCubeTexture = null;
		public RenderTexture UnfilteredCubeTexture = null; 
		public RenderTexture SpecularCubeTexture = null; 
		public Camera cam = null;
		public WebCamTexture webcamTexture = null;

		TeleportSettings teleportSettings=null;

		List<double> bandwidths = new List<double>();
		float settingsDuration = 0.0f;

		public VideoEncoder VideoEncoder
		{
			get; private set;
		}

		public UInt32 CurrentTagID
		{
			get; set;
		}
		/// <summary>
		/// NOTE: Remove this. Can't have this as a singleton because it precludes multi-client.
		/// </summary>
		public static Teleport_SceneCaptureComponent RenderingSceneCapture
		{
			get; private set;
		}
		Teleport_SceneCaptureComponent()
		{

		}
		void Start()
		{
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
			videoTexture = null;
			rendererTexture = null;
			DiffuseCubeTexture = null;
			SpecularCubeTexture = null;
			UnfilteredCubeTexture = null;
			webcamTexture = null;
			VideoEncoder = null;
		}

		void LateUpdate()
		{
			// for now just get latest client
			uid id = Teleport_SessionComponent.GetLastClientID();
	 
			if (id != clientID)
			{
				clientID = id;
				CurrentTagID = 0;
				if (clientID != 0)
				{
					VideoEncoder = new VideoEncoder(clientID);
				}
				else
				{
					VideoEncoder = null;
				}
				bandwidths.Clear();
				settingsDuration = 0.0f;
			}

			if (cam && videoTexture)
			{
				if(clientID != 0)
				{
					ManagePerformance();
				}
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
			teleportSettings = TeleportSettings.GetOrCreateSettings();
			if (teleportSettings.casterSettings.usePerspectiveRendering)
			{
				cam.fieldOfView = teleportSettings.casterSettings.perspectiveFOV;
				cam.aspect = teleportSettings.casterSettings.perspectiveWidth  / (teleportSettings.casterSettings.perspectiveHeight);
			}
			else
			{
				cam.fieldOfView = 90;
				cam.aspect = 1;
			}
			cam.depthTextureMode |= DepthTextureMode.Depth;

			cam.enabled = false;

			CreateResources();

			if (teleportSettings.casterSettings.isStreamingWebcam && !teleportSettings.casterSettings.usePerspectiveRendering)
			{
				Application.RequestUserAuthorization(UserAuthorization.WebCam);
				if (Application.HasUserAuthorization(UserAuthorization.WebCam))
				{
					CreateWebTexture();				
				}
			}
		}

		void ManagePerformance()
		{
			var settings = teleportSettings.casterSettings;

			if (!settings.useDynamicQuality)
			{
				return;
			}

			settingsDuration += Time.deltaTime * 1000;

			var session = Teleport_SessionComponent.sessions[clientID];
			if (!session)
			{
				return;
			}
			var stats = session.GetNetworkStats();
			bandwidths.Add(stats.bandwidth);

			if (settingsDuration >= teleportSettings.casterSettings.bandwidthCalculationInterval)
			{
				double bandwidth = GetMedianBandwidth();
				bandwidths.Clear();
				settingsDuration = 0.0f;

				if (settings.usePerspectiveRendering)
				{
					int prevWidth = settings.perspectiveWidth;
					int prevHeight = settings.perspectiveHeight;

					// Add 4 pixels to height for tag id
					if (bandwidth > 50)
					{
						settings.perspectiveWidth = 1024;
						settings.perspectiveHeight = 1024;
					}
					else if (bandwidth > 25)
					{
						settings.perspectiveWidth = 1024;
						settings.perspectiveHeight = 1024;
					}
					else
					{
						settings.perspectiveWidth = 768;
						settings.perspectiveHeight = 768;
					}

					if (prevWidth != settings.perspectiveWidth || prevHeight != settings.perspectiveHeight)
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

		void ChangeQuality(double bandwidth)
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

		double GetMedianBandwidth()
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
			{
				int captureHeight = (int)(settings.perspectiveHeight * 1.5f);
				CreateTextures(settings.perspectiveWidth, captureHeight, settings.perspectiveWidth, settings.perspectiveHeight, 24);
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

			videoTexture = new RenderTexture(captureTexWidth, captureTexHeight, 0, format, RenderTextureReadWrite.Default);
			videoTexture.name = "Video Texture";
			videoTexture.dimension = TextureDimension.Tex2D;
			videoTexture.useMipMap = false;
			videoTexture.autoGenerateMips = false;
			videoTexture.enableRandomWrite = true;
			videoTexture.Create();

			rendererTexture = new RenderTexture(rendererTexWidth, rendererTexHeight, rendererTexDepth, format, RenderTextureReadWrite.Default);
			rendererTexture.name = "Scene Capture Renderer Texture";
			rendererTexture.dimension = TextureDimension.Tex2D;
			rendererTexture.useMipMap = false;
			rendererTexture.autoGenerateMips = false;
			rendererTexture.enableRandomWrite = false;
			rendererTexture.Create();

			UnfilteredCubeTexture = new RenderTexture(teleportSettings.casterSettings.defaultSpecularCubemapSize, teleportSettings.casterSettings.defaultSpecularCubemapSize, 1, format, RenderTextureReadWrite.Default);
			UnfilteredCubeTexture.name = "Unfiltered Cube";
			UnfilteredCubeTexture.dimension = TextureDimension.Cube;
			UnfilteredCubeTexture.useMipMap = false;
			UnfilteredCubeTexture.autoGenerateMips = false;
			UnfilteredCubeTexture.enableRandomWrite = true;
			UnfilteredCubeTexture.Create();

			DiffuseCubeTexture = new RenderTexture(teleportSettings.casterSettings.defaultDiffuseCubemapSize, teleportSettings.casterSettings.defaultDiffuseCubemapSize, 1, format, RenderTextureReadWrite.Default);
			DiffuseCubeTexture.name = "Diffuse Cube";
			DiffuseCubeTexture.dimension = TextureDimension.Cube;
			DiffuseCubeTexture.useMipMap = true;
			DiffuseCubeTexture.autoGenerateMips = false;
			DiffuseCubeTexture.enableRandomWrite = true;
			DiffuseCubeTexture.Create();
			
			SpecularCubeTexture = new RenderTexture(teleportSettings.casterSettings.defaultSpecularCubemapSize, teleportSettings.casterSettings.defaultSpecularCubemapSize, 1, format, RenderTextureReadWrite.Default);
			SpecularCubeTexture.name = "Specular Cube";
			SpecularCubeTexture.dimension = TextureDimension.Cube;
			SpecularCubeTexture.useMipMap = true;
			SpecularCubeTexture.autoGenerateMips = false;
			SpecularCubeTexture.enableRandomWrite = true;
			SpecularCubeTexture.Create();
		}

		void CreateWebTexture()
		{
			if (teleportSettings.webcam != "")
			{
			webcamTexture = new WebCamTexture();
				webcamTexture.deviceName = teleportSettings.webcam;
				webcamTexture.Play();
			}
			}
			webcamTexture.deviceName = devices[0].name;
			webcamTexture.Play();
		}
	}
