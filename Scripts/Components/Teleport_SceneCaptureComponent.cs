using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

using uid = System.UInt64;

namespace teleport
{
    public class Teleport_SceneCaptureComponent : MonoBehaviour
    {
        public uid clientID = 0; // This needs to be set by a session component instance after start
        //public Cubemap cubemap;
        //public RenderTexture cubemap;
        //public RenderTexture cubemapTexArray;
        public RenderTexture sceneCaptureTexture;
        Camera cam;
        CasterMonitor monitor; //Cached reference to the caster monitor.   

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
            //DestroyImmediate(cubemap);
            //DestroyImmediate(cubemapTexArray);
            DestroyImmediate(sceneCaptureTexture);
        }

        void LateUpdate()
        {
            // for now just get latest client
            clientID = Teleport_SessionComponent.GetClientID();
     
            if (clientID != 0 && cam.targetTexture)
            {
                RenderToTexture();
            }
        }

        void Initialize()
        {
            GameObject obj = new GameObject(TeleportRenderPipeline.CUBEMAP_CAM_PREFIX + clientID, typeof(Camera));
            obj.hideFlags = HideFlags.DontSave;
            obj.transform.position = transform.position;
            obj.transform.rotation = Quaternion.identity;
            cam = obj.GetComponent<Camera>();
            cam.farClipPlane = 1000;
            cam.enabled = false;

            int size = (int)monitor.casterSettings.captureCubeTextureSize;

            RenderTextureFormat format;
            if (monitor.casterSettings.use10BitEncoding)
            {
                format = RenderTextureFormat.ARGB64;
            }
            else
            {
                format = RenderTextureFormat.ARGB32;
            }

            //cubemap = new Cubemap(size, TextureFormat.ARGB32, false);
            //cubemap.name = "Cubemap";
            //cubemap.hideFlags = HideFlags.DontSave;
            //cubemap.filterMode = FilterMode.Point;

            //cubemap = new RenderTexture(size, size, 0, format, RenderTextureReadWrite.Default);
            //cubemap.name = "Cubemap";
            //cubemap.hideFlags = HideFlags.DontSave;
            //cubemap.dimension = UnityEngine.Rendering.TextureDimension.Cube;
            //cubemap.filterMode = FilterMode.Point;
            //cubemap.useMipMap = false;
            //cubemap.autoGenerateMips = false;
            //cubemap.Create();

            //cubemapTexArray = new RenderTexture(size, size, 0, format, RenderTextureReadWrite.Default);
            //cubemapTexArray.volumeDepth = 6;
            //cubemapTexArray.enableRandomWrite = true;
            //cubemapTexArray.name = "Cubemap Texture Array";
            //cubemapTexArray.hideFlags = HideFlags.DontSave;
            //cubemapTexArray.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
            //cubemapTexArray.filterMode = FilterMode.Point;
            //cubemapTexArray.useMipMap = false;
            //cubemapTexArray.autoGenerateMips = false;
            //cubemapTexArray.Create();

            sceneCaptureTexture = new RenderTexture(size * 3, size * 3, 0, format, RenderTextureReadWrite.Default);
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
            //cam.transform.position = transform.position;
            //cam.transform.forward = transform.forward;
           
            // Update name in case client ID changed
            cam.name = TeleportRenderPipeline.CUBEMAP_CAM_PREFIX + clientID;
            cam.Render();
        }
    }
}
