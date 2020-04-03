﻿// (c) 2018 Simul.co

#include "UnityCG.cginc"

#define mat4 float4x4
#define mat3 float3x3
#define vec2 float2
#define vec3 float3
#define vec4 float4
#define THREADGROUP_SIZEX 32
#define THREADGROUP_SIZEY 32

static const float PI = 3.141592;

struct FScreenVertexOutput
{
	float2 UV : TEXCOORD0;
	float4 Position : SV_POSITION;
};


// TODO: Expose those parameters to the capture component.
static const float TonemapGamma = 2.2;
static const float TonemapExposure = 1.0;
static const float TonemapPureWhite = 1.0;

// Reinhard tonemapping operator with gamma correction.
// TODO: Switch to filmic tonemapping ala Uncharted 2.
float3 PrepareTonemap(float4 SceneColor)
{
	const float PureWhiteSq = TonemapPureWhite * TonemapPureWhite;
	float Luminance = dot(SceneColor.rgb, float3(0.2126, 0.7152, 0.0722));

	float MappedLuminance = (Luminance * (1.0 + Luminance / PureWhiteSq)) / (1.0 + Luminance);
	float3 MappedColor = (MappedLuminance / Luminance) * SceneColor.rgb;
	return float3(MappedColor.rgb);
}

float4 Prepare(float4 SceneColour)
{
	float4 Colour = 0.5 * SceneColour;
	return Colour;
}

float3 CubeFaceIndexToView(uint3 idx, uint2 dims)
{
	float4x4 cubeInvViewProj[6] = {
		{ {0,0,-1,0}	,{0,-1,0,0}	,{1,0,0,0}	,{0,0,0,1}	}
		, { {0,0,1,0}	,{0,-1,0,0}	,{-1,0,0,0}	,{0,0,0,1} }
		,{ {-1,0,0,0}	,{0,0,-1,0}	,{0,1,0,0}	,{0,0,0,1}	}
		,{ {-1,0,0,0}	,{0,0,1,0}	,{0,-1,0,0},{0,0,0,1}	}
		,{ {-1,0,0,0}	,{0,-1,0,0}	,{0,0,-1,0}	,{0,0,0,1}	}
		,{ {1,0,0,0}	,{0,-1,0,0}	,{0,0,1,0}	,{0,0,0,1}	}
	};
	float2 texCoords = (float2(idx.xy) + float2(0.5, 0.5)) / float2(dims);
	float4 clip_pos = float4(-1.0, 1.0, 1.0, 1.0);
	clip_pos.x += 2.0 * texCoords.x;
	clip_pos.y -= 2.0 * texCoords.y;
	float3 view = -normalize(mul(cubeInvViewProj[idx.z], clip_pos).xyz);
	return view;
}

// GGX Trowbridge-Reitz function (Walter et al 2007, Microfacet Models for Refraction through Rough Surfaces")
float D_GGX(float roughness, float NoH)
{
	float a = roughness * roughness;
	float a2 = a * a;
	float d = (NoH * a2 - NoH) * NoH + 1;	// 2 mad
	return a2 / (PI * d * d);					// 4 mul, 1 rcp
}