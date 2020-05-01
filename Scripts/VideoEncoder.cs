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

        uid clientID;
        CasterMonitor monitor;

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
        }

        public void CreateEncodeCommands(ScriptableRenderContext context, Camera camera)
        {     
            commandBuffer = new CommandBuffer();
            commandBuffer.name = "Video Encoder";

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

            context.ExecuteCommandBuffer(commandBuffer);
            ReleaseCommandbuffer(camera);
            context.Submit();
        }

        public void ReleaseCommandbuffer(Camera camera)
        {
            if (commandBuffer != null)
            {
                commandBuffer.Release();
            }
        }

        void InitEncoder(Camera camera)
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

        public void Shutdown()
        {

        }

    }
}
