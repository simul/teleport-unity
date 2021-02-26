using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEditor;

namespace SCServer
{
	[StructLayout(LayoutKind.Sequential), Serializable]
    public class CasterSettings
    {
		[Header("SRT")]
		public Int32 requiredLatencyMs = 30;

		[Header("General")]
		[MarshalAs(UnmanagedType.LPWStr)] public string sessionName = "Unity";
		[MarshalAs(UnmanagedType.LPWStr)] public string clientIP = "127.0.0.1";
		public Int32 detectionSphereRadius = 15;
		public Int32 detectionSphereBufferDistance = 5;
		public Int32 expectedLag = 0;
		public Int64 throttleKpS = 0;

		[Header("Geometry")]
		[MarshalAs(UnmanagedType.U1)] public bool isStreamingGeometry = true;
		public byte geometryTicksPerSecond = 2;
		public Int32 geometryBufferCutoffSize = 1048576; //Byte amount we stop encoding nodes at.
		public float confirmationWaitTime = 15; //Seconds to wait before resending a resource.

		[Header("Video")]
		[MarshalAs(UnmanagedType.U1)] public bool isStreamingVideo = true;
		[MarshalAs(UnmanagedType.U1)] public bool isStreamingWebcam = false;
		public float captureCubeTextureSize = 512; 
		public Int32 videoEncodeFrequency = 2;
		[MarshalAs(UnmanagedType.U1)] public bool isDeferringOutput = false;
		[MarshalAs(UnmanagedType.U1)] public bool isCullingCubemaps = false;
		public Int32 blocksPerCubeFaceAcross = 2; // The number of blocks per cube face will be this value squared
		public Int32 cullQuadIndex = -1; // This culls a quad at the index. For debugging only
		public Int32 targetFPS = 60;
		public Int32 idrInterval = 0;
		public avs.VideoCodec videoCodec = avs.VideoCodec.HEVC;
		public avs.RateControlMode rateControlMode = avs.RateControlMode.RC_CBR_LOWDELAY_HQ; 
		public Int32 averageBitrate = 40000000;
		public Int32 maxBitrate = 80000000;
		[MarshalAs(UnmanagedType.U1)] public bool useAutoBitRate = false;
		public Int32 vbvBufferSizeInFrames = 3;
		[MarshalAs(UnmanagedType.U1)] public bool useAsyncEncoding = true;
		[MarshalAs(UnmanagedType.U1)] public bool use10BitEncoding = false;
		[MarshalAs(UnmanagedType.U1)] public bool useYUV444Decoding = false;
		[MarshalAs(UnmanagedType.U1)] public bool usePerspectiveRendering = false;
		public Int32 sceneCaptureWidth = 1920;
		public Int32 sceneCaptureHeight = 1084; // 4 pixels Added for tag id
		public float perspectiveFOV = 110;
		[MarshalAs(UnmanagedType.U1)] public bool useDynamicQuality = false;
		public Int32 bandwidthCalculationInterval = 5000;

		[Header("Audio")]
		[MarshalAs(UnmanagedType.U1)] public bool isStreamingAudio = true;
		[MarshalAs(UnmanagedType.U1)] public bool isReceivingAudio = false;

		[Header("Debugging")]
		public Int32 debugStream = 0;
		[MarshalAs(UnmanagedType.U1)] public bool debugNetworkPackets = false;
		[MarshalAs(UnmanagedType.U1)] public bool debugControlPackets = false;
		[MarshalAs(UnmanagedType.U1)] public bool calculateChecksums = false;
		[MarshalAs(UnmanagedType.U1)] private bool willCacheReset = false;
		[MarshalAs(UnmanagedType.U1)] public bool pipeDllOutputToUnity = false;
		public byte estimatedDecodingFrequency = 10; //An estimate of how frequently the client will decode the packets sent to it; used by throttling.
		

		[Header("Compression")]
		[MarshalAs(UnmanagedType.U1)] public bool useCompressedTextures = true;
		public byte qualityLevel = 1;
		public byte compressionLevel = 1;

		[Header("Camera")]
		[MarshalAs(UnmanagedType.U1)] public bool willDisableMainCamera = false;
		[NonSerialized]
		[MarshalAs(UnmanagedType.U8)] public byte axesStandard = 64|2|4;

		[Header("Lighting")]
		public Int32 specularCubemapSize = 64;
		public Int32 roughCubemapSize = 64;
		public Int32 diffuseCubemapSize = 64;
		public Int32 lightCubemapSize = 64;

		[Header("Input")]
		public bool lockPlayerHeight = true;
		public ControlModel controlModel=ControlModel.SERVER_ORIGIN_CLIENT_LOCAL;
	}
}