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
		public RenderTexture videoTexture = null;
		public RenderTexture rendererTexture = null;
		public RenderTexture DiffuseCubeTexture = null;
		public RenderTexture UnfilteredCubeTexture = null; 
		public RenderTexture SpecularCubeTexture = null; 
		public RenderTexture RoughSpecularCubeTexture = null;
		public Camera cam = null;
		public Vector2Int specularOffset;
		public Vector2Int diffuseOffset;
		public Vector2Int roughOffset;
		public Vector2Int lightOffset;

		TeleportSettings teleportSettings=null;

		List<float> bandwidths = new List<float>();
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

		void Start()
		{
			teleportSettings = TeleportSettings.GetOrCreateSettings();
			specularOffset = new Vector2Int(0, 0);
			diffuseOffset = specularOffset + new Vector2Int(0, teleportSettings.casterSettings.specularCubemapSize * 2);
			roughOffset = new Vector2Int((3 * teleportSettings.casterSettings.specularCubemapSize*7)/4, 0);
			lightOffset = diffuseOffset + new Vector2Int(teleportSettings.casterSettings.specularCubemapSize * 3 / 2, teleportSettings.casterSettings.specularCubemapSize * 2);
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
			RoughSpecularCubeTexture = null;
			UnfilteredCubeTexture = null;
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
				cam.aspect = (float)teleportSettings.casterSettings.sceneCaptureWidth  / (float)(teleportSettings.casterSettings.sceneCaptureHeight - 4);
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

					// Add 4 pixels to height for tag id
					if (bandwidth > 50)
					{
						settings.sceneCaptureWidth = 3840;
						settings.sceneCaptureHeight = 2164;
					}
					else if (bandwidth > 25)
					{
						settings.sceneCaptureWidth = 1920;
						settings.sceneCaptureHeight = 1084;
					}
					else
					{
						settings.sceneCaptureWidth = 1280;
						settings.sceneCaptureHeight = 724;
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
			{	// Minus 4 used for tag data id from renderer texture height
				CreateTextures(settings.sceneCaptureWidth, settings.sceneCaptureHeight, settings.sceneCaptureWidth, settings.sceneCaptureHeight - 4, 24);
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

			UnfilteredCubeTexture = new RenderTexture(teleportSettings.casterSettings.specularCubemapSize, teleportSettings.casterSettings.specularCubemapSize, 1, format, RenderTextureReadWrite.Default);
			UnfilteredCubeTexture.name = "Unfiltered Cube";
			UnfilteredCubeTexture.dimension = TextureDimension.Cube;
			UnfilteredCubeTexture.useMipMap = false;
			UnfilteredCubeTexture.autoGenerateMips = false;
			UnfilteredCubeTexture.enableRandomWrite = true;
			UnfilteredCubeTexture.Create();

			DiffuseCubeTexture = new RenderTexture(teleportSettings.casterSettings.diffuseCubemapSize, teleportSettings.casterSettings.diffuseCubemapSize, 1, format, RenderTextureReadWrite.Default);
			DiffuseCubeTexture.name = "Diffuse Cube";
			DiffuseCubeTexture.dimension = TextureDimension.Cube;
			DiffuseCubeTexture.useMipMap = true;
			DiffuseCubeTexture.autoGenerateMips = false;
			DiffuseCubeTexture.enableRandomWrite = true;
			DiffuseCubeTexture.Create();

			SpecularCubeTexture = new RenderTexture(teleportSettings.casterSettings.specularCubemapSize, teleportSettings.casterSettings.specularCubemapSize, 1, format, RenderTextureReadWrite.Default);
			SpecularCubeTexture.name = "Specular Cube";
			SpecularCubeTexture.dimension = TextureDimension.Cube;
			SpecularCubeTexture.useMipMap = true;
			SpecularCubeTexture.autoGenerateMips = false;
			SpecularCubeTexture.enableRandomWrite = true;
			SpecularCubeTexture.Create();

			RoughSpecularCubeTexture = new RenderTexture(teleportSettings.casterSettings.specularCubemapSize, teleportSettings.casterSettings.specularCubemapSize, 1, format, RenderTextureReadWrite.Default);
			RoughSpecularCubeTexture.name = "Rough Specular Cube";
			RoughSpecularCubeTexture.dimension = TextureDimension.Cube;
			RoughSpecularCubeTexture.useMipMap = true;
			RoughSpecularCubeTexture.autoGenerateMips = false;
			RoughSpecularCubeTexture.enableRandomWrite = true;
			RoughSpecularCubeTexture.Create();
		}
	}
}
