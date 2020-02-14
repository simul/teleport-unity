using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

using uid = System.UInt64;

namespace teleport
{
    public class Teleport_SceneCaptureComponent3D : MonoBehaviour
    {
        int cubemapSize = 128;
        bool oneFacePerFrame = false;
        Camera cam;
        RenderTexture renderTexture;


        void Start()
        {
            // render all six faces at startup
            UpdateCubemap(63);
        }

        void OnDisable()
        {
            DestroyImmediate(cam);
            DestroyImmediate(renderTexture);
        }

        void LateUpdate()
        {
            if (oneFacePerFrame)
            {
                var faceToRender = Time.frameCount % 6;
                var faceMask = 1 << faceToRender;
                UpdateCubemap(faceMask);
            }
            else
            {
                UpdateCubemap(63); // all six faces
            }
        }

        void UpdateCubemap(int faceMask)
        {
            if (!cam)
            {
                GameObject obj = new GameObject("CubemapCamera", typeof(Camera));
                obj.hideFlags = HideFlags.HideAndDontSave;
                obj.transform.position = transform.position;
                obj.transform.rotation = Quaternion.identity;
                cam = obj.GetComponent<Camera>();
                cam.farClipPlane = 100; // don't render very far into cubemap
                cam.enabled = false;
            }

            if (!renderTexture)
            {
                renderTexture = new RenderTexture(cubemapSize, cubemapSize, 16);
                renderTexture.dimension = UnityEngine.Rendering.TextureDimension.Cube;
                renderTexture.hideFlags = HideFlags.HideAndDontSave;
                GetComponent<Renderer>().sharedMaterial.SetTexture("_Cube", renderTexture);
            }

            cam.transform.position = transform.position;
            cam.RenderToCubemap(renderTexture, faceMask);
        }
    }
}
