using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

using uid = System.UInt64;

namespace teleport
{
	[HelpURL("https://docs.teleportvr.io/unity.html")]
	public class Teleport_SceneCaptureComponent : MonoBehaviour
	{
		private uid clientID = 0; 
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
		bool initialized = false;

		public VideoEncoder VideoEncoder
		{
			get; private set;
		}

		public UInt32 CurrentTagID
		{
			get; set;
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

		public void SetClientID(uid id)
		{
			clientID = id;
			CurrentTagID = 0;
			if (clientID != 0)
			{
				Initialize();
				CreateResources();
				VideoEncoder = new VideoEncoder(this);
			}
			else
			{
				VideoEncoder = null;
			}
			bandwidths.Clear();
			settingsDuration = 0.0f;
		}

		public uid GetClientID()
		{
			return clientID;
		}

		void LateUpdate()
		{
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
			if (initialized)
			{
				return;
			}

			CurrentTagID = 0;

			GameObject obj = gameObject;
			obj.transform.position = transform.position;
			obj.transform.rotation = Quaternion.identity;
			cam = gameObject.GetComponent<Camera>();
			if (!cam)
			{
				cam = gameObject.AddComponent<Camera>();
			}
		
			cam.nearClipPlane = 0.05f;
			cam.farClipPlane = 1000;
			teleportSettings = TeleportSettings.GetOrCreateSettings();
			if (teleportSettings.serverSettings.usePerspectiveRendering)
			{
				cam.fieldOfView = teleportSettings.serverSettings.perspectiveFOV;
				cam.aspect = teleportSettings.serverSettings.perspectiveWidth  / (teleportSettings.serverSettings.perspectiveHeight);
			}
			else
			{
				cam.fieldOfView = 90;
				cam.aspect = 1;
			}
			cam.depthTextureMode |= DepthTextureMode.Depth;

			cam.enabled = false;

			if (teleportSettings.serverSettings.StreamWebcam)
			{
				Application.RequestUserAuthorization(UserAuthorization.WebCam);
				if (Application.HasUserAuthorization(UserAuthorization.WebCam))
				{
					CreateWebTexture();				
				}
			}

			initialized = true;
		}

		void ManagePerformance()
		{
			var settings = teleportSettings.serverSettings;

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

			if (settingsDuration >= teleportSettings.serverSettings.bandwidthCalculationInterval)
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
					int prevSize = session.clientSettings.captureCubeTextureSize;

					if (bandwidth > 50)
					{
						session.clientSettings.captureCubeTextureSize = 1024;
					}
					else if (bandwidth > 35)
					{
						session.clientSettings.captureCubeTextureSize = 512;
					}
					else if (bandwidth > 25)
					{
						session.clientSettings.captureCubeTextureSize = 256;
					}
					else
					{
						session.clientSettings.captureCubeTextureSize = 128;
					}

					if (prevSize != session.clientSettings.captureCubeTextureSize)
					{
						ChangeQuality(bandwidth);
					}
				}			
			}		
		}

		void ChangeQuality(double bandwidth)
		{
			var settings = teleportSettings.serverSettings;

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
			cam.Render();

			CurrentTagID = (CurrentTagID + 1) % 32;
		}

		void CreateResources()
		{
			var session = Teleport_SessionComponent.GetSessionComponent(clientID);
			if (session)
			{
				var vidSize = session.clientSettings.videoTextureSize;
				var settings = teleportSettings.serverSettings;

				if (settings.usePerspectiveRendering)
				{
					CreateTextures(vidSize.x, vidSize.y, settings.perspectiveWidth, settings.perspectiveHeight, 24);
				}
				else
				{
					CreateTextures(vidSize.x, vidSize.y, session.clientSettings.captureCubeTextureSize, session.clientSettings.captureCubeTextureSize, 24);
				}		
			}	
		}

		void CreateTextures(int captureTexWidth, int captureTexHeight, int rendererTexWidth, int rendererTexHeight, int rendererTexDepth)
		{
			RenderTextureFormat format;
			if (teleportSettings.serverSettings.use10BitEncoding)
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

			UnfilteredCubeTexture = new RenderTexture(teleportSettings.serverSettings.defaultSpecularCubemapSize, teleportSettings.serverSettings.defaultSpecularCubemapSize, 1, format, RenderTextureReadWrite.Default);
			UnfilteredCubeTexture.name = "Unfiltered Cube";
			UnfilteredCubeTexture.dimension = TextureDimension.Cube;
			UnfilteredCubeTexture.useMipMap = false;
			UnfilteredCubeTexture.autoGenerateMips = false;
			UnfilteredCubeTexture.enableRandomWrite = true;
			UnfilteredCubeTexture.Create();

			DiffuseCubeTexture = new RenderTexture(teleportSettings.serverSettings.defaultDiffuseCubemapSize, teleportSettings.serverSettings.defaultDiffuseCubemapSize, 1, format, RenderTextureReadWrite.Default);
			DiffuseCubeTexture.name = "Diffuse Cube";
			DiffuseCubeTexture.dimension = TextureDimension.Cube;
			DiffuseCubeTexture.useMipMap = true;
			DiffuseCubeTexture.autoGenerateMips = false;
			DiffuseCubeTexture.enableRandomWrite = true;
			DiffuseCubeTexture.Create();
			
			SpecularCubeTexture = new RenderTexture(teleportSettings.serverSettings.defaultSpecularCubemapSize, teleportSettings.serverSettings.defaultSpecularCubemapSize, 1, format, RenderTextureReadWrite.Default);
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
	}
}