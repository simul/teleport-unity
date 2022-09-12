Shader "Teleport/CopyCubemap"
{
	CGINCLUDE

	#pragma enable_d3d11_debug_symbols
	float Roughness;
	int MipIndex;
	int NumMips;
	uint Face;
	float Multiplier;
	float3 Direction;
	float3 Colour;
	sampler2D _SourceTexture;
	samplerCUBE _SourceCubemapTexture;

	#define CPP_GLSL

	#include "UnityCG.cginc"
	#include "Common.cginc"
	#include "Cubemap.cginc"

	// Configuration

	struct appdata
	{
		float4 vertex   : POSITION;
		float2 uv	   : TEXCOORD0;
	};

	struct v2f
	{
		float4 pos : SV_POSITION;
		float2 uv : TEXCOORD0;
	};

	v2f vert (appdata v)
	{
		v2f o;
		float4 clipPos;
		clipPos	= float4(v.vertex.xy*2.0-float2(1.0,1.0),1.0,1.0);
		o.pos	= clipPos;
		o.uv	= v.uv;

		return o;
	}

	fixed4 frag (v2f i) : SV_Target
	{
		float4 res = tex2D(_SourceTexture, i.uv);
		return res;
	}
	
	v2f vert_from_id(appdata_img v, uint vid : SV_VertexID)
	{
		v2f o;
		o.pos	=float4(GetOutPosFromVertexId(vid), 1.0);
		o.uv	=GetUvFromVertexId(vid);
		return o;
	}

	fixed4 encode_face_frag (v2f i) : SV_Target
	{
		vec3 view   =CubeFaceAndTexCoordsToView(Face,i.uv);
		view		=float3(-view.z,view.x,view.y);
		vec4 res	=CubeSampleLevel (_SourceCubemapTexture, view, (float)MipIndex) ;
		return res;
	}
	
	fixed4 mip_frag (v2f i) : SV_Target
	{
		vec3 view   =CubeFaceAndTexCoordsToView(Face,i.uv);
		//float roughness=GetroughnessFromMip(float(MipIndex),float(NumMips),1.2);
		vec4 res	=RoughnessMip(_SourceCubemapTexture, view, NumMips, 1.0, Roughness,false);
		return res;
	}
	fixed4 ambient_diffuse_frag (v2f i) : SV_Target
	{
		vec3 view   =CubeFaceAndTexCoordsToView(Face,i.uv);
		vec4 res	=Multiplier*AmbientDiffuse(_SourceCubemapTexture, view);
		return res;
	}
	fixed4 directional_diffuse_frag (v2f i) : SV_Target
	{
		float3 view	=CubeFaceAndTexCoordsToView(Face,i.uv);
		float4 res	=Multiplier*float4(Colour*max(0.0,dot(Direction,view)),1.0);
		//res.rgb=view;
		return res;
	}
		

	ENDCG
	SubShader
	{
		Tags{}
		Pass
		{
			ZWrite Off ZTest Always Cull Off

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			ENDCG
		}
		Pass
		{
			ZWrite Off ZTest Always Cull Off

			CGPROGRAM
			#pragma vertex vert_from_id
			#pragma fragment mip_frag
			ENDCG
		}
		Pass
		{
			ZWrite Off ZTest Always Cull Off

			CGPROGRAM
			#pragma vertex vert_from_id
			#pragma fragment encode_face_frag
			ENDCG
		}
		Pass
		{
			ZWrite Off ZTest Always Cull Off

			CGPROGRAM
			#pragma vertex vert_from_id
			#pragma fragment ambient_diffuse_frag
			ENDCG
		}
		Pass
		{
			ZWrite Off ZTest Always Cull Off
			Blend One One
			CGPROGRAM
			#pragma vertex vert_from_id
			#pragma fragment directional_diffuse_frag
			ENDCG
		}
	}
	Fallback Off
}
