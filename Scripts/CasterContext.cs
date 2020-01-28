using System;

namespace SCServer
{
    public struct CasterContext
    {
        public IntPtr NetworkPipeline;
        public IntPtr ColorQueue;
        public IntPtr DepthQueue;
        public IntPtr GeometryQueue;
        public bool isCapturingDepth;
        public avs.AxesStandard axesStandard;
    }
}

public struct CasterContext
{
    public IntPtr EncodePipeline;

    public SCServer.CasterContext baseContext;
}
