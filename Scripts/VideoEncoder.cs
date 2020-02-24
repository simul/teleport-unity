using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;


namespace teleport
{
    public class VideoEncoder
    {
        private const int THREADGROUP_SIZE = 32;

        private CasterMonitor monitor;

        private RenderTexture cubemapTexArray;
        private RenderTexture outputTexture;

        private static String shaderPath = "Shaders/ProjectCubemap";

        private ComputeShader shader;
        private int encodeCamKernel;
        private int decomposeKernel;
        private int decomposeDepthKernel;

        public void Initialize(RenderTexture inCubemapTexArray, RenderTexture inOutputTexture)
        {
            monitor = CasterMonitor.GetCasterMonitor();

            cubemapTexArray = inCubemapTexArray;
            outputTexture = inOutputTexture;
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

            // Aidan: Offsets differ from Unreal here because Unity usues OpenGL convention where 0,0 is bottom left instead of top left like D3D.
            

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
