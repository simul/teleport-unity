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
		public static Dictionary<uid, VideoEncoder> videoEncoders = new Dictionary<uid, VideoEncoder>();

		public uid clientID = 0; // This needs to be set by a session component instance after start
		public RenderTexture sceneCaptureTexture = null;
		public Camera cam = null;
		CasterMonitor monitor; //Cached reference to the caster monitor.   
		TeleportSettings teleportSettings=null;
		private static Teleport_SceneCaptureComponent renderingSceneCapture = null;

		void Start()
		{
			monitor = CasterMonitor.GetCasterMonitor();
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
			sceneCaptureTexture=null;
		}

		void LateUpdate()
		{
			// for now just get latest client
			uid id = Teleport_SessionComponent.GetClientID();
	 
			if (id != clientID)
			{
				if (videoEncoders.ContainsKey(clientID))
				{
					videoEncoders.Remove(clientID);
				}

				if (videoEncoders.ContainsKey(id))
				{
					videoEncoders.Remove(id);
				}

				if (id != 0)
				{
					clientID = id;

					videoEncoders.Add(clientID, new VideoEncoder(clientID));
				}
				else
				{
					clientID = 0;
				}
			}

			if (cam && sceneCaptureTexture)
			{
				RenderToTexture();
			}
		}

		void Initialize()
		{
			GameObject obj = gameObject;// new GameObject(TeleportRenderPipeline.CUBEMAP_CAM_PREFIX + clientID, typeof(Camera));
			//obj.hideFlags = HideFlags.DontSave; //If you use HideFlags.DontSave, you need to delete the object manually. Alternatively, use DontDestroyOnLoad.
			obj.transform.position = transform.position;
			obj.transform.rotation = Quaternion.identity;
			cam = gameObject.GetComponent<Camera>();
			if (!cam)
			{
				cam=gameObject.AddComponent<Camera>();
			}
			cam.nearClipPlane = 0.05f;
			cam.farClipPlane = 1000;
			cam.fieldOfView = 90;
			cam.aspect = 1;
			cam.depthTextureMode |= DepthTextureMode.Depth;

			cam.enabled = false;

			int size = (int)teleportSettings.casterSettings.captureCubeTextureSize * 3;

			RenderTextureFormat format;
			if (teleportSettings.casterSettings.use10BitEncoding)
			{
				format = RenderTextureFormat.ARGB64;
			}
			else
			{
				format = RenderTextureFormat.ARGB32;
			}

			sceneCaptureTexture = new RenderTexture(size, size, 0, format, RenderTextureReadWrite.Default);
			sceneCaptureTexture.name = "Scene Capture Texture";
			//sceneCaptureTexture.hideFlags = HideFlags.DontSave; //If you use HideFlags.DontSave, you need to delete the object manually. Alternatively, use DontDestroyOnLoad.
			sceneCaptureTexture.dimension = TextureDimension.Tex2D;
			sceneCaptureTexture.useMipMap = false;
			sceneCaptureTexture.autoGenerateMips = false;
			sceneCaptureTexture.enableRandomWrite = true;
			sceneCaptureTexture.Create();
		}

		void RenderToTexture()
		{
			cam.transform.position = transform.position;

			// Update name in case client ID changed
			cam.name = TeleportRenderPipeline.CUBEMAP_CAM_PREFIX + clientID;
			renderingSceneCapture = this;
			cam.Render();
		}

		public static Teleport_SceneCaptureComponent GetRenderingSceneCapture()
		{
			return renderingSceneCapture;
		}
	}
}
