using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

using uid = System.UInt64;

namespace teleport
{
    public class Teleport_SceneCaptureComponent : MonoBehaviour
    {
        private Camera cam;
        public Cubemap cubemap;
        //public RenderTexture cubemap;
        public RenderTexture encInputTexArray;
        public RenderTexture encOutputTexture;
        private CasterMonitor monitor; //Cached reference to the caster monitor.
        private VideoEncoder encoder = new VideoEncoder();
       

        void Start()
        {
            monitor = CasterMonitor.GetCasterMonitor();
            Initialize();
        }

        void OnDisable()
        {
            encoder.Shutdown();
            DestroyImmediate(cam);
            DestroyImmediate(cubemap);
            DestroyImmediate(encInputTexArray);
            DestroyImmediate(encOutputTexture);
        }

        void LateUpdate()
        {
            if (encInputTexArray)
            {
                RenderToTexture();
                encoder.PrepareFrame(cam.transform);
                encoder.EncodeFrame(false);
            }
        }

        void Initialize()
        {
            GameObject obj = new GameObject("CubemapCamera", typeof(Camera));
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

            cubemap = new Cubemap(size, TextureFormat.ARGB32, false);
            cubemap.name = "Cubemap";
            cubemap.hideFlags = HideFlags.DontSave;
            cubemap.filterMode = FilterMode.Point;

            //cubemap = new RenderTexture(size, size, 0, format, RenderTextureReadWrite.Default);
            //cubemap.name = "Cubemap";
            //cubemap.hideFlags = HideFlags.DontSave;
            //cubemap.dimension = UnityEngine.Rendering.TextureDimension.Cube;
            //cubemap.filterMode = FilterMode.Point;
            //cubemap.useMipMap = false;
            //cubemap.autoGenerateMips = false;
            //cubemap.Create();

            encInputTexArray = new RenderTexture(size, size, 0, format, RenderTextureReadWrite.Default);
            encInputTexArray.volumeDepth = 6;
            encInputTexArray.enableRandomWrite = true;
            encInputTexArray.name = "Cubemap Texture Array";
            encInputTexArray.hideFlags = HideFlags.DontSave;
            encInputTexArray.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
            encInputTexArray.filterMode = FilterMode.Point;
            encInputTexArray.useMipMap = false;
            encInputTexArray.autoGenerateMips = false;
            encInputTexArray.Create();

            encOutputTexture = new RenderTexture(size * 3, size * 3, 0, format, RenderTextureReadWrite.Default);
            encOutputTexture.enableRandomWrite = true;
            encOutputTexture.name = "Encoder Input Texture";
            encOutputTexture.hideFlags = HideFlags.DontSave;
            encOutputTexture.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
            encOutputTexture.filterMode = FilterMode.Point;
            encOutputTexture.useMipMap = false;
            encOutputTexture.autoGenerateMips = false;
            encOutputTexture.Create();

            encoder.Initialize(0, encInputTexArray, encOutputTexture);
        }

        void RenderToTexture()
        {
            //var faceToRender = Time.frameCount % 6;
            //var faceMask = 1 << faceToRender;
            int faceMask = 63;
            cam.transform.position = transform.position;
            cam.transform.forward = transform.forward;

            if (!cam.RenderToCubemap(cubemap, faceMask))
            {
                Debug.LogError("Error occured rendering the cubemap");
                return;
            }

            // Copy here because we cannot bind to a RWTexture2DArray otherwise
            for (int i = 0; i < 6; ++i)
            {
                Graphics.CopyTexture(cubemap, i, encInputTexArray, i);
            }   
        }
    }
}
