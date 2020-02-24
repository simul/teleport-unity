using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

using uid = System.UInt64;

namespace teleport
{
    public class VideoEncoder
    {
        #region DLLImports
        [DllImport("SimulCasterServer")]
        private static extern void InitializeVideoEncoder(uid clientID, SCServer.VideoEncodeParams videoEncodeParams);
        [DllImport("SimulCasterServer")]
        private static extern void EncodeVideoFrame(uid clientID, avs.Transform cameraTransform);
        #endregion

        //Stores handles to game objects, so the garbage collector doesn't move/delete the objects while they're being referenced by the native plug-in.
        Dictionary<GameObject, GCHandle> gameObjectHandles = new Dictionary<GameObject, GCHandle>();

        private const int THREADGROUP_SIZE = 32;

        private uid clientID;
        private CasterMonitor monitor;

        private RenderTexture cubemapTexArray;
        private RenderTexture outputTexture;

        private static String shaderPath = "Shaders/ProjectCubemap";

        private ComputeShader shader;
        private int encodeCamKernel;
        private int decomposeKernel;
        private int decomposeDepthKernel;

        public void Initialize(uid inClientID, RenderTexture inCubemapTexArray, RenderTexture inOutputTexture)
        {
            clientID = inClientID;
            cubemapTexArray = inCubemapTexArray;
            outputTexture = inOutputTexture;

            monitor = CasterMonitor.GetCasterMonitor();

            InitEncoder();
            InitShaders();     
        }
         
        public void PrepareFrame(Transform cameraTransform)
        {
            DispatchShaders(cameraTransform);
        }

        public void EncodeFrame(bool forceIDR)
        {

        }

        public void Shutdown()
        {

        }

        private void InitEncoder()
        {
            var vidParams = new SCServer.VideoEncodeParams();
            vidParams.encodeWidth = vidParams.encodeHeight = (int)monitor.casterSettings.captureCubeTextureSize * 3;
            switch(SystemInfo.graphicsDeviceType)
            {
                case (GraphicsDeviceType.Direct3D11):
                    vidParams.deviceType = SCServer.GraphicsDeviceType.Direct3D11;
                    break;
                case (GraphicsDeviceType.Direct3D12):
                    vidParams.deviceType = SCServer.GraphicsDeviceType.Direct3D12;
                    break;
                case (GraphicsDeviceType.OpenGLCore):
                    vidParams.deviceType = SCServer.GraphicsDeviceType.OpenGL; // Needs to be supported
                    break;
                case (GraphicsDeviceType.Vulkan):
                    vidParams.deviceType = SCServer.GraphicsDeviceType.Vulkan; // Needs to be supported
                    break;
                default:
                    Debug.Log("Graphics api not supported");
                    return;
            }
            vidParams.deviceHandle = outputTexture.GetNativeTexturePtr();
            vidParams.inputSurfaceResource = outputTexture.GetNativeTexturePtr();
           // InitializeVideoEncoder(clientID, vidParams);
        }

        private void InitShaders()
        {
            // NB: Do not include file extension when loading a shader
            shader = Resources.Load<ComputeShader>(shaderPath);
            if (!shader)
            {
                Debug.Log("Shader not found at path " + shaderPath + ".compute");
            }
            encodeCamKernel = shader.FindKernel("EncodeCameraPositionCS");
            decomposeKernel = shader.FindKernel("DecomposeCS");
            decomposeDepthKernel = shader.FindKernel("DecomposeDepthCS");
        }

        private void DispatchShaders(Transform cameraTransform)
        {
            Int32 numThreadGroupsX = (int)monitor.casterSettings.captureCubeTextureSize / THREADGROUP_SIZE;
            Int32 numThreadGroupsY = (int)monitor.casterSettings.captureCubeTextureSize / THREADGROUP_SIZE;
            Int32 numThreadGroupsZ = 6;

            int faceSize = (int)monitor.casterSettings.captureCubeTextureSize;
            int size = faceSize * 3;

            int a = -1;

            int b = 6 + a;

            // Aidan: Offsets differ from Unreal here because Unity uses OpenGL convention where 0,0 is bottom left instead of top left like D3D.
            

            // Colour
            shader.SetTexture(decomposeKernel, "RWInputCubeAsArray", cubemapTexArray);
            shader.SetTexture(decomposeKernel, "RWOutputColorTexture", outputTexture);
            shader.SetInts("Offset", new Int32[2] { 0, faceSize });
            shader.Dispatch(decomposeKernel, numThreadGroupsX, numThreadGroupsY, numThreadGroupsZ);

            // Depth
            //shader.SetTexture(decomposeDepthKernel, "RWInputCubeAsArray", cubemapTexArray);
            //shader.SetTexture(decomposeDepthKernel, "RWOutputColorTexture", outputTexture);
            //shader.SetInt("Offset",  shader.SetInts("Offset", new Int32[2] { 0, 0 });
            //shader.Dispatch(decomposeDepthKernel, numThreadGroupsX, numThreadGroupsY, numThreadGroupsZ);

            // Camera Position 
            float[] camPos = new float[3] { cameraTransform.position.x, cameraTransform.position.y, cameraTransform.position.z };
            // flip y because Unity usues OpenGL convention where 0,0 is bottom left instead of top left like D3D.
            shader.SetInts("Offset", new Int32[2] { size - (32 * 4), 3 * 8});
            shader.SetFloats("CubemapCameraPositionMetres", camPos);
            shader.Dispatch(encodeCamKernel, 4, 1, 1);
        }

    }
}
