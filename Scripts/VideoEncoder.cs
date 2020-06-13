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

        //Stores handles to game objects, so the garbage collector doesn't move/delete the objects while they're being referenced by the native plug-in.
        Dictionary<GameObject, GCHandle> gameObjectHandles = new Dictionary<GameObject, GCHandle>();

        uid clientID;
        CasterMonitor monitor;

        CommandBuffer commandBuffer = null;

        bool initalized = false;
        bool _reconfigure = false;

     
        public bool Reconfigure
        {
            set; get;
        }

        public VideoEncoder(uid clientID)
        {
            this.clientID = clientID;

            monitor = CasterMonitor.GetCasterMonitor();    
        }

        public void CreateEncodeCommands(ScriptableRenderContext context, Camera camera)
        {     
            commandBuffer = new CommandBuffer();
            commandBuffer.name = "Video Encoder " + clientID;

            ConfigureEncoder(camera);

            var paramsWrapper = new TransformWrapper();
            paramsWrapper.clientID = clientID;
            paramsWrapper.transform = new avs.Transform();
            paramsWrapper.transform.position = camera.transform.position;
            paramsWrapper.transform.rotation = camera.transform.rotation;
            paramsWrapper.transform.scale = new avs.Vector3(1, 1, 1);

            IntPtr paramsWrapperPtr = Marshal.AllocHGlobal(Marshal.SizeOf(new TransformWrapper()));
            Marshal.StructureToPtr(paramsWrapper, paramsWrapperPtr, true);

            commandBuffer.IssuePluginEventAndData(GetRenderEventWithDataCallback(), 2, paramsWrapperPtr);

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

        void ConfigureEncoder(Camera camera)
        {
            if (initalized && !_reconfigure)
            {
                return;
            }

            var paramsWrapper = new EncodeVideoParamsWrapper();
            paramsWrapper.clientID = clientID;
            paramsWrapper.videoEncodeParams = new SCServer.VideoEncodeParams();
            var teleportSettings = TeleportSettings.GetOrCreateSettings();
            if (teleportSettings.casterSettings.usePerspectiveRendering)
            {
                paramsWrapper.videoEncodeParams.encodeWidth = teleportSettings.casterSettings.sceneCaptureWidth;
                paramsWrapper.videoEncodeParams.encodeHeight = teleportSettings.casterSettings.sceneCaptureHeight;
            }
            else
            {
                paramsWrapper.videoEncodeParams.encodeWidth = paramsWrapper.videoEncodeParams.encodeHeight = (int)teleportSettings.casterSettings.captureCubeTextureSize * 3;
            }
           
            switch (SystemInfo.graphicsDeviceType)
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

            var encoderTexture = Teleport_SceneCaptureComponent.RenderingSceneCapture.sceneCaptureTexture;
            // deviceHandle set in dll
            paramsWrapper.videoEncodeParams.inputSurfaceResource = encoderTexture.GetNativeTexturePtr();
            IntPtr paramsWrapperPtr = Marshal.AllocHGlobal(Marshal.SizeOf(new EncodeVideoParamsWrapper()));
            Marshal.StructureToPtr(paramsWrapper, paramsWrapperPtr, true);

            if (!initalized)
            {
                commandBuffer.IssuePluginEventAndData(GetRenderEventWithDataCallback(), 0, paramsWrapperPtr);
                initalized = true;
            }
            else
            {
                commandBuffer.IssuePluginEventAndData(GetRenderEventWithDataCallback(), 1, paramsWrapperPtr);
            }
            _reconfigure = false;
        }

        public void Shutdown()
        {

        }

    }
}
