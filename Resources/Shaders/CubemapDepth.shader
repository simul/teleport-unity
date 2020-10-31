Shader "Custom/DepthShader"
{
	CGINCLUDE
	#pragma target 3.0
	#pragma enable_d3d11_debug_symbols
	#include "Common.cginc"

	Texture2D<float4> DepthTexture;
	SamplerState samplerDepthTexture;
	Texture2D<float4> FilteredDepthTexture;
	SamplerState samplerFilteredDepthTexture;
	float4 CascadeOffsetScale;

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
	
	float2 ComputeMoments(float Depth)
	{
		float2 Moments;
		Moments.x = Depth; 
		float dx = ddx(Depth);   
		float dy = ddy(Depth);  
		Moments.y = Depth*Depth + 0.25*(dx*dx + dy*dy);
		return Moments;
	} 

	float4 frag_colour(v2f i, UNITY_VPOS_TYPE screenPos : VPOS) : SV_Target
	{
		float2 uv=i.uv;
		vec4 res=pow(FilteredDepthTexture.Sample(samplerFilteredDepthTexture,uv),2.0);
		return res;
	/*
		float2 uv=.5*i.uv;
		float depth=1.0-DepthTexture.Sample(samplerDepthTexture,uv).x ;
	//	result.g=result*result;
		vec4 result=vec4(depth,depth,depth,depth);
		return result;*/
	}
	float4 filter_vsm(v2f i, UNITY_VPOS_TYPE screenPos : VPOS) : SV_Target
	{
		float2 uv=.5*vec2(i.uv.x,1.0-i.uv.y);
		vec4 result=vec4(0,0,0,0);
		vec2 offset[5]={{0,0},{-1,0},{1,0},{0,1},{0,-1}};
		float sum=0.0;
		for(int i=-2;i<3;i++)
		{
			for(int j=-2;j<3;j++)
			{
				vec2 off=vec2(i,j);
				float weight=exp(-dot(off,off));
				float depth=DepthTexture.Sample(samplerDepthTexture,uv+0.00*off).x;
				float z=1.0-depth;//0.4243;
				float2 moments=ComputeMoments(z);
				// Square for variance shadow map.
				result+=weight*vec4(moments.xyy,0);
				sum+=weight;
			}
		}
		return result/sum;
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
		Pass
		{
			Blend Off

			ZTest Always Cull Off ZWrite Off
			Fog { Mode off }

			CGPROGRAM
			#pragma fragmentoption ARB_precision_hint_nicest
			#pragma vertex vert
			#pragma fragment filter_vsm
			ENDCG
		}
	}
	Fallback off
}