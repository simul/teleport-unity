Shader "Custom/DepthShader"
{
	CGINCLUDE
	#pragma target 3.0
	#pragma enable_d3d11_debug_symbols
	#include "Common.cginc"

	static const int2 FaceOffsets[] = { {0,0},{1,0},{2,0},{0,1},{1,1},{2,1} };

	uniform sampler2D _CameraDepthTexture;
	uniform float4 _CameraDepthTexture_TexelSize;

	int Face;

	struct v2f
	{
		float2 uv : TEXCOORD0;
	};

	float PosToDistanceMultiplier(int2 pos, int w)
	{
		float h = (w + 1.0) / 2.0;
		vec2 diff = (vec2(pos) - vec2(h, h)) * 2.0 / vec2(w, w);
		return sqrt(1.0 + dot(diff, diff));
	}

	float GetDepth(int3 pos, int w)
	{
		float m = PosToDistanceMultiplier(pos.xy, w);
		int2 inPos = pos.xy + w * FaceOffsets[pos.z];
		float2 uv = float2(inPos.x, inPos.y);
		uv *= _CameraDepthTexture_TexelSize.xy;
		uv.y = 1.0 - uv.y;
		float d = tex2D(_CameraDepthTexture, uv).r;
		d *= m;
		return d;
	}

	float2 GetUv(uint id)
	{
		float2 uvs[6];
#if defined(USING_STEREO_MATRICES)
		if (unity_StereoEyeIndex == 0)
		{
			uvs[0] = float2(0.0, 0.0);
			uvs[1] = float2(0.5, 1.0);
			uvs[2] = float2(0.0, 1.0);

			uvs[3] = float2(0.0, 0.0);
			uvs[4] = float2(0.5, 0.0);
			uvs[5] = float2(0.5, 1.0);
		}
		else
		{
			uvs[0] = float2(0.5, 0.0);
			uvs[1] = float2(1.0, 1.0);
			uvs[2] = float2(0.5, 1.0);

			uvs[3] = float2(0.5, 0.0);
			uvs[4] = float2(1.0, 0.0);
			uvs[5] = float2(1.0, 1.0);
		}
#else
		uvs[0] = float2(0.0, 0.0);
		uvs[1] = float2(1.0, 1.0);
		uvs[2] = float2(0.0, 1.0);

		uvs[3] = float2(0.0, 0.0);
		uvs[4] = float2(1.0, 0.0);
		uvs[5] = float2(1.0, 1.0);
#endif
		return uvs[id];
	}

	v2f vert(appdata_img v, uint vid : SV_VertexID, out float4 outpos : SV_POSITION)
	{
		float3 vertices[6];
		vertices[0] = float3(-1.0, 1.0, 1.0);
		vertices[1] = float3(1.0, -1.0, 1.0);
		vertices[2] = float3(-1.0, -1.0, 1.0);
		vertices[3] = float3(-1.0, 1.0, 1.0);
		vertices[4] = float3(1.0, 1.0, 1.0);
		vertices[5] = float3(1.0, -1.0, 1.0);
		v2f o;
		outpos = float4(vertices[vid], 1.0);
		o.uv = GetUv(vid);
		o.uv.y = 1.0 - o.uv.y;
		return o;
	}

	float4 frag(v2f i, UNITY_VPOS_TYPE screenPos : VPOS) : SV_Target
	{
		int W = _CameraDepthTexture_TexelSize.z / 3;

	    int3 pos = int3(i.uv * W, Face);
		
		float d00 = GetDepth(pos, W);
		float d01 = GetDepth(pos + int3(1, 0, 0), W);
		float d10 = GetDepth(pos + int3(0, 1, 0), W);

		float4 depth = float4(d00, d01, d10, 1.0) / 100.0 / 20.0;

		return float4(depth.xyz, 1.0);
	}

	ENDCG

	SubShader
	{
		Pass
		{
			Blend Off

			ZTest Always Cull Off ZWrite Off
			Fog { Mode off }

			CGPROGRAM
			#pragma fragmentoption ARB_precision_hint_nicest
			#pragma vertex vert
			#pragma fragment frag
			ENDCG
		}
	}
	Fallback off
}