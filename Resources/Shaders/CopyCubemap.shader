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
	float4x4 TargetToSourceAxes;
	sampler2D _SourceTexture;
	samplerCUBE _SourceCubemapTexture;
	samplerCUBE _DiffuseSourceCubemapTexture;
// due to unfortunate failings in Unity's CommandBuffer API, we have to duplicate both the source inputs AND the shader.
	samplerCUBE _EncodeSourceCubemapTexture0;
	samplerCUBE _EncodeSourceCubemapTexture1;
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

	fixed4 plain_copy (v2f i) : SV_Target
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

	// due to unfortunate failings in Unity's CommandBuffer API, we have to duplicate both the source inputs AND the shader.
	fixed4 encode_face_frag0 (v2f i) : SV_Target
	{
		vec3 view   =CubeFaceAndTexCoordsToView(Face,i.uv);
		vec4 res	=vec4(view,0);
		//view		=view.zxy;
		//view		=float3(-view.y,view.z,-view.x);
		res			=CubeSampleLevel (_EncodeSourceCubemapTexture0, view, (float)MipIndex) ;
	//res.rgb+=saturate(view);
		//res.r = 1.0;
		return res;
	}
	fixed4 encode_face_frag1 (v2f i) : SV_Target
	{
		vec3 view   =CubeFaceAndTexCoordsToView(Face,i.uv);
		vec4 res	=vec4(view,0);
		//view		=view.zxy;
		//view		=float3(-view.y,view.z,-view.x);
		res			=CubeSampleLevel (_EncodeSourceCubemapTexture1, view, (float)MipIndex) ;
	//res.rgb+=saturate(view);
		//res.r = 1.0;
		return res;
	}
	
	fixed4 mip_frag (v2f i) : SV_Target
	{
		vec3 view   =CubeFaceAndTexCoordsToView(Face,i.uv);
		vec4 res	=vec4(view,0);
		view		=mul(TargetToSourceAxes,vec4(view.xyz,0)).xyz;
		//view.y		*=-1.0;
		//float roughness=GetroughnessFromMip(float(MipIndex),float(NumMips),1.2);
		res			=RoughnessMip(_SourceCubemapTexture, view, NumMips, 1.0, Roughness,false);
	//res.rgb+=saturate(view);
		//res.g = 1.0;
		return res;
	}
	fixed4 ambient_diffuse_frag (v2f i) : SV_Target
	{
		vec3 view   =CubeFaceAndTexCoordsToView(Face,i.uv);
		vec4 res	=vec4(view,0);
		view		=mul(TargetToSourceAxes,vec4(view.xyz,0)).xyz;
	
		res			=Multiplier*AmbientDiffuse(_DiffuseSourceCubemapTexture, view);
		//res.r = 1.0;
		return res;
	}
	fixed4 directional_diffuse_frag (v2f i) : SV_Target
	{
		float3 view	=CubeFaceAndTexCoordsToView(Face,i.uv);
		vec4 res	=vec4(view,0);
		view		=mul(TargetToSourceAxes,vec4(view.xyz,0)).xyz;
		//view		=view.zxy;
		//view.y		*=-1.0;
		res			=Multiplier*float4(Colour*max(0.0,dot(Direction,view)),1.0);
	//res.rgb+=view;
		//res.b = 1.0;
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
			#pragma fragment plain_copy
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
			#pragma fragment encode_face_frag0
			ENDCG
		}
		Pass
		{
			ZWrite Off ZTest Always Cull Off

			CGPROGRAM
			#pragma vertex vert_from_id
			#pragma fragment encode_face_frag1
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
