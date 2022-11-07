// (c) 2018 Simul.co
#ifndef COMMON_CGINC
#define COMMON_CGINC
#include "UnityCG.cginc"

#define mat4 float4x4
#define mat3 float3x3
#define vec2 float2
#define vec3 float3
#define vec4 float4
// Was 32, but 32*32= 1024, which is about 16 times too large for an efficient threadgroup.
#define THREADGROUP_SIZEX 32
#define THREADGROUP_SIZEY 32

#ifndef PI
#define PI (3.1415926)
#endif

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

float3 CubeFaceAndTexCoordsToView(uint face, vec2 texCoords)
{
	//GL-style
	float4x4 cubeInvViewProj[6] = {
		{ {0,0,-1,0}	,{0,-1,0,0}	,{1,0,0,0}	,{0,0,0,1}	}
		, { {0,0,1,0}	,{0,-1,0,0}	,{-1,0,0,0}	,{0,0,0,1} }
		,{ {-1,0,0,0}	,{0,0,-1,0}	,{0,1,0,0}	,{0,0,0,1}	}
		,{ {-1,0,0,0}	,{0,0,1,0}	,{0,-1,0,0} ,{0,0,0,1}	}
		,{ {-1,0,0,0}	,{0,-1,0,0}	,{0,0,-1,0}	,{0,0,0,1}	}
		,{ {1,0,0,0}	,{0,-1,0,0}	,{0,0,1,0}	,{0,0,0,1}	}
	};
	float4 clip_pos = float4(-1.0, -1.0, 1.0, 1.0);
	clip_pos.x += 2.0 * texCoords.x;
	clip_pos.y += 2.0 * texCoords.y;
	float3 view = -normalize(mul(cubeInvViewProj[face], clip_pos));
	return view;
}


float3 CubeFaceIndexToView(uint3 idx, uint2 dims)
{
	float2 texCoords = (float2(idx.xy) + float2(0.5, 0.5)) / float2(dims);
	return CubeFaceAndTexCoordsToView(idx.z,texCoords);
}

// GGX Trowbridge-Reitz function (Walter et al 2007, Microfacet Models for Refraction through Rough Surfaces")
float D_GGX(float roughness, float NoH)
{
	float a = roughness * roughness;
	float a2 = a * a;
	float d = (NoH * a2 - NoH) * NoH + 1;	// 2 mad
	return a2 / (PI * d * d);					// 4 mul, 1 rcp
}


float2 GetUvFromVertexId(uint id)
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
	float2 uv=uvs[id];
	uv.y = 1.0 - uv.y;
	return uv;
}

float3 GetOutPosFromVertexId(uint vid)
{
	float3 vertices[6];
	vertices[0] = float3(-1.0, 1.0, 1.0);
	vertices[1] = float3(1.0, -1.0, 1.0);
	vertices[2] = float3(-1.0, -1.0, 1.0);
	vertices[3] = float3(-1.0, 1.0, 1.0);
	vertices[4] = float3(1.0, 1.0, 1.0);
	vertices[5] = float3(1.0, -1.0, 1.0);
	return vertices[vid];
}
#endif