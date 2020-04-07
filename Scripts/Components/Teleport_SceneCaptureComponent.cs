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
        public RenderTexture sceneCaptureTexture;
        Camera cam;
        CasterMonitor monitor; //Cached reference to the caster monitor.   
        bool invertCulling = false;

        void Start()
        {
            monitor = CasterMonitor.GetCasterMonitor();
            Initialize();
        }

        void OnDisable()
        {
            ReleaseResources();
        }

        void ReleaseResources()
        {
            DestroyImmediate(cam);
            DestroyImmediate(sceneCaptureTexture);
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

            if (clientID > 0 && cam.targetTexture)
            {
                RenderToTexture();
            }
        }

        void Initialize()
        {
            GameObject obj = new GameObject(TeleportRenderPipeline.CUBEMAP_CAM_PREFIX + clientID, typeof(Camera));
            obj.hideFlags = HideFlags.DontSave;
            obj.transform.position = transform.position;
            obj.transform.rotation = transform.rotation;
            cam = obj.GetComponent<Camera>();
            cam.farClipPlane = 1000;
            cam.fieldOfView = 90;
            cam.aspect = 1;
            cam.depthTextureMode |= DepthTextureMode.Depth;

            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.OpenGLCore && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Vulkan)
            {
                invertCulling = true;
                Matrix4x4 proj = cam.projectionMatrix;
                proj.m11 = -proj.m11;
                proj.m13 = -proj.m13;
                cam.projectionMatrix = proj;
            }
          
            cam.enabled = false;

            int size = (int)monitor.casterSettings.captureCubeTextureSize * 3;

            RenderTextureFormat format;
            if (monitor.casterSettings.use10BitEncoding)
            {
                format = RenderTextureFormat.ARGB64;
            }
            else
            {
                format = RenderTextureFormat.ARGB32;
            }

            sceneCaptureTexture = new RenderTexture(size, size, 24, format, RenderTextureReadWrite.Default);
            sceneCaptureTexture.name = "Scene Capture Texture";
            sceneCaptureTexture.hideFlags = HideFlags.DontSave;
            sceneCaptureTexture.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
            sceneCaptureTexture.useMipMap = false;
            sceneCaptureTexture.autoGenerateMips = false;
            sceneCaptureTexture.enableRandomWrite = true;
            sceneCaptureTexture.Create();

            cam.targetTexture = sceneCaptureTexture;
        }

        void RenderToTexture()
        {
            cam.transform.position = transform.position;
            cam.transform.rotation = transform.rotation;

            // Update name in case client ID changed
            cam.name = TeleportRenderPipeline.CUBEMAP_CAM_PREFIX + clientID;

            bool originalInvertCulling = GL.invertCulling;

            // Cull opposite winding order to prevent vertices from being culled after inverting
            if (invertCulling)
            {
                GL.invertCulling = true;
            }
           
            cam.Render();
            
            GL.invertCulling = originalInvertCulling;
        }
    }
}
