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
        static extern System.IntPtr GetRenderEventWithDataCallback();
        #endregion

        //Stores handles to game objects, so the garbage collector doesn't move/delete the objects while they're being referenced by the native plug-in.
        Dictionary<GameObject, GCHandle> gameObjectHandles = new Dictionary<GameObject, GCHandle>();

        const int THREADGROUP_SIZE = 32;

        uid clientID;
        CasterMonitor monitor;

        static String shaderPath = "Shaders/ProjectCubemap";

        ComputeShader shader;
        int encodeCamKernel;
        int quantizationKernel;

        CommandBuffer commandBuffer = null;

        bool initalized = false;

        [StructLayout(LayoutKind.Sequential)]
        struct EncodeVideoParamsWrapper
        {
            public uid clientID;
            public SCServer.VideoEncodeParams videoEncodeParams;
        };

        [StructLayout(LayoutKind.Sequential)]
        struct TransformWrapper
        {
            public uid clientID;
            public avs.Transform transform;
        };

        public VideoEncoder(uid clientID)
        {
            this.clientID = clientID;

            monitor = CasterMonitor.GetCasterMonitor();

            InitShaders();     
        }

        public void CreateEncodeCommands(Camera camera)
        {
            if (commandBuffer == null)
            {
                commandBuffer = new CommandBuffer();
                commandBuffer.name = "Video Encoder";
                camera.AddCommandBuffer(CameraEvent.AfterEverything, commandBuffer);
            }

            commandBuffer.Clear();

            AddComputeShaderCommands(camera);

            if (!initalized)
            {
                InitEncoder(camera);
                initalized = true;
            }

            var paramsWrapper = new TransformWrapper();
            paramsWrapper.clientID = clientID;
            paramsWrapper.transform = new avs.Transform();
            paramsWrapper.transform.position = camera.transform.position;
            paramsWrapper.transform.rotation = camera.transform.rotation;
            paramsWrapper.transform.scale = new avs.Vector3(1, 1, 1);

            IntPtr paramsWrapperPtr = Marshal.AllocHGlobal(Marshal.SizeOf(new TransformWrapper()));
            Marshal.StructureToPtr(paramsWrapper, paramsWrapperPtr, true);

            commandBuffer.IssuePluginEventAndData(GetRenderEventWithDataCallback(), 1, paramsWrapperPtr);
        }

        private void InitEncoder(Camera camera)
        {
            var paramsWrapper = new EncodeVideoParamsWrapper();
            paramsWrapper.clientID = clientID;
            paramsWrapper.videoEncodeParams = new SCServer.VideoEncodeParams();
            paramsWrapper.videoEncodeParams.encodeWidth = paramsWrapper.videoEncodeParams.encodeHeight = (int)monitor.casterSettings.captureCubeTextureSize * 3;

            switch(SystemInfo.graphicsDeviceType)
            {
                case (GraphicsDeviceType.Direct3D11):
                    paramsWrapper.videoEncodeParams.deviceType = SCServer.GraphicsDeviceType.Direct3D11;
                    break;
                case (GraphicsDeviceType.Direct3D12):
                    paramsWrapper.videoEncodeParams.deviceType = SCServer.GraphicsDeviceType.Direct3D12; // May not work if device not created with shared heap flag in Unity source
                    break;
                case (GraphicsDeviceType.OpenGLCore):
                    paramsWrapper.videoEncodeParams.deviceType = SCServer.GraphicsDeviceType.OpenGL; // Needs to be supported
                    break;
                case (GraphicsDeviceType.Vulkan):
                    paramsWrapper.videoEncodeParams.deviceType = SCServer.GraphicsDeviceType.Vulkan; // Needs to be supported
                    break;
                default:
                    Debug.Log("Graphics api not supported");
                    return;
            }

            // deviceHandle set in dll
            paramsWrapper.videoEncodeParams.inputSurfaceResource = camera.targetTexture.GetNativeTexturePtr();
            IntPtr paramsWrapperPtr = Marshal.AllocHGlobal(Marshal.SizeOf(new EncodeVideoParamsWrapper()));
            Marshal.StructureToPtr(paramsWrapper, paramsWrapperPtr, true);    

            commandBuffer.IssuePluginEventAndData(GetRenderEventWithDataCallback(), 0, paramsWrapperPtr);
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
            quantizationKernel = shader.FindKernel("QuantizationCS");
        }

        private void AddComputeShaderCommands(Camera camera)
        {
            int faceSize = (int)monitor.casterSettings.captureCubeTextureSize;
            int size = faceSize * 3;

            // Camera Position 
            var camTransform = camera.transform;
            float[] camPos = new float[3] { camTransform.position.x, camTransform.position.z, camTransform.position.y };

            shader.SetTexture(encodeCamKernel, "RWOutputColorTexture", camera.targetTexture);
            shader.SetInts("Offset", new Int32[2] { size - (32 * 4), size - (3 * 8)});
            shader.SetFloats("CubemapCameraPositionMetres", camPos);
            commandBuffer.DispatchCompute(shader, encodeCamKernel, 4, 1, 1);

            // Colour Quantization
            int numThreadGroupsX = size / THREADGROUP_SIZE;
            int numThreadGroupsY = (faceSize * 2) / THREADGROUP_SIZE;
            shader.SetTexture(quantizationKernel, "RWOutputColorTexture", camera.targetTexture);
            commandBuffer.DispatchCompute(shader, quantizationKernel, numThreadGroupsX, numThreadGroupsY, 1);
        }

        public void Shutdown()
        {

        }

    }
}
