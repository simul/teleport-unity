Shader "Custom/DepthShader"
{
	CGINCLUDE
	#pragma target 3.0
	#pragma enable_d3d11_debug_symbols
	#include "Common.cginc"

	Texture2D<float4> DepthTexture;

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

	float GetDepth(int2 pos, int w)
	{
		float m = PosToDistanceMultiplier(pos, w);
		float d = Linear01Depth(DepthTexture[pos].r);
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
		//o.uv.y = 1.0 - o.uv.y;
		return o;
	}

	float4 frag(v2f i, UNITY_VPOS_TYPE screenPos : VPOS) : SV_Target
	{
		int w, h;
		DepthTexture.GetDimensions(w, h);

		int2 pos = int2(i.uv * float(w));
		
		float d00 = GetDepth(pos, w);
		float d01 = GetDepth(pos + int2(1, 0), w);
		float d10 = GetDepth(pos + int2(0, 1), w);

		float4 depth = float4(d00, d01, d10, 1.0);

		return depth;
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