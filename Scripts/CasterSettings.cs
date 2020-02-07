using System;
using System.Runtime.InteropServices;

namespace SCServer
{
	[StructLayout(LayoutKind.Sequential), Serializable]
    public class CasterSettings
    {
		public Int32 requiredLatencyMs = 30;

		[MarshalAs(UnmanagedType.LPWStr)] public string sessionName = "Unity";
		[MarshalAs(UnmanagedType.LPWStr)] public string clientIP = "127.0.0.1";
		public Int32 detectionSphereRadius = 1000;
		public Int32 detectionSphereBufferDistance = 200;
		public Int32 expectedLag = 0;
		public Int64 throttleKpS = 0;
		public IntPtr handActor;

		[MarshalAs(UnmanagedType.U1)] public bool enableGeometryStreaming = true;
		public byte geometryTicksPerSecond = 2;
		public Int32 geometryBufferCutoffSize = 1048576; //Byte amount we stop encoding nodes at.
		public float confirmationWaitTime = 15; //Seconds to wait before resending a resource.

		[MarshalAs(UnmanagedType.U1)] public bool willOverrideTextureTarget = false;
		public IntPtr sceneCaptureTextureTarget;
		public Int32 videoEncodeFrequency = 2;
		[MarshalAs(UnmanagedType.U1)] public bool enableDeferOutput = false;
		[MarshalAs(UnmanagedType.U1)] public bool enableCubemapCulling = false;
		public Int32 blocksPerCubeFaceAcross = 2; // The number of blocks per cube face will be this value squared
		public Int32 cullQuadIndex = -1; // This culls a quad at the index. For debugging only
		public Int32 targetFPS = 60;
		public Int32 idrInterval = 0;
		//EncoderRateControlMode rateControlMode;
		public Int32 averageBitrate = 40000000;
		public Int32 maxBitrate = 80000000;
		[MarshalAs(UnmanagedType.U1)] public bool enableAutoBitRate = false;
		public Int32 vbvBufferSizeInFrames = 3;
		[MarshalAs(UnmanagedType.U1)] public bool useAsyncEncoding = true;
		[MarshalAs(UnmanagedType.U1)] public bool use10BitEncoding = false;
		[MarshalAs(UnmanagedType.U1)] public bool useYUV444Decoding = false;

		public Int32 debugStream = 0;
		[MarshalAs(UnmanagedType.U1)] public bool enableDebugNetworkPackets = false;
		[MarshalAs(UnmanagedType.U1)] public bool enableDebugControlPackets = false;
		[MarshalAs(UnmanagedType.U1)] public bool enableChecksums = false;
		[MarshalAs(UnmanagedType.U1)] public bool willCacheReset = false;
		public byte estimatedDecodingFrequency = 10; //An estimate of how frequently the client will decode the packets sent to it; used by throttling.

		[MarshalAs(UnmanagedType.U1)] public bool useCompressedTextures = true;
		public byte qualityLevel = 1;
		public byte compressionLevel = 1;

		[MarshalAs(UnmanagedType.U1)] public bool willDisableMainCamera = false;

		public byte axesStandard = 64|2|4;
	}
}