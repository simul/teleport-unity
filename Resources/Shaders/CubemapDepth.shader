Shader "Custom/DepthShader"
{
	CGINCLUDE
	#pragma target 3.0
	#pragma enable_d3d11_debug_symbols
	#include "Common.cginc"

	Texture2D<float4> DepthTexture;
	SamplerState samplerDepthTexture;

	struct v2f
	{
		float2 uv : TEXCOORD0;
	};

	float PosToDistanceMultiplier(int2 pos, int w)
	{
		float h = (w + 1) / 2.0;
		vec2 diff = (vec2(pos) - vec2(h, h)) * 2.0 / vec2(w, w);
		return sqrt(1.0 + dot(diff, diff));
	}

	float GetDepth(int2 pos, int w)
	{
		float m = PosToDistanceMultiplier(pos, w);
		float d = LinearEyeDepth(DepthTexture[pos].r)/20.0;
		d *= m;
		return d;
	}

	v2f vert(appdata_img v, uint vid : SV_VertexID, out float4 outpos : SV_POSITION)
	{
		v2f o;
		outpos = float4(GetOutPosFromVertexId(vid), 1.0);
		o.uv = GetUvFromVertexId(vid);
		return o;
	}

	float4 frag_depth(v2f i, UNITY_VPOS_TYPE screenPos : VPOS) : SV_Target
	{
		int w, h;
		DepthTexture.GetDimensions(w, h);

		int2 pos = int2(i.uv * float(w)) - 1;
		
		float d00 = GetDepth(pos, w);
		float d01 = GetDepth(pos + int2(1, 0), w);
		float d10 = GetDepth(pos + int2(0, 1), w);

		float4 depth = float4(d00, d01, d10, 1.0);

		return depth;
	}
	float4 frag_colour(v2f i, UNITY_VPOS_TYPE screenPos : VPOS) : SV_Target
	{
		float4 result=DepthTexture.Sample(samplerDepthTexture,i.uv) ;
		//result.rb=1.0;
		return result;
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
			#pragma fragment frag_depth
			ENDCG
		}
		Pass
		{
			Blend Off

			ZTest Always Cull Off ZWrite Off
			Fog { Mode off }

			CGPROGRAM
			#pragma fragmentoption ARB_precision_hint_nicest
			#pragma vertex vert
			#pragma fragment frag_colour
			ENDCG
		}
	}
	Fallback off
}