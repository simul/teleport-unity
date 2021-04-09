// (c) 2020 Simul.co
#ifndef CUBEMAP_CGINC
#define CUBEMAP_CGINC
#include "Common.cginc"

/*
uint ReverseBits32(uint bits) {
	bits = (bits << 16u) | (bits >> 16u);
	bits = ((bits & 0x55555555u) << 1u) | ((bits & 0xAAAAAAAAu) >> 1u);
	bits = ((bits & 0x33333333u) << 2u) | ((bits & 0xCCCCCCCCu) >> 2u);
	bits = ((bits & 0x0F0F0F0Fu) << 4u) | ((bits & 0xF0F0F0F0u) >> 4u);
	bits = ((bits & 0x00FF00FFu) << 8u) | ((bits & 0xFF00FF00u) >> 8u);
	return (bits);
}*/

vec2 Hammersley(uint index, uint NumSamples, uint2 random)
{
	float E1 = frac((float)index / NumSamples + float(random.x & 0xffff) / (1 << 16));
	// Radical inverse:
	float E2 = float(reversebits(index) ^ random.y) * 2.3283064365386963e-10;
	return vec2(E1, E2);
}

mat3 GetTangentBasis(vec3 Z_dir)
{
	vec3 UpVector = abs(Z_dir.z) < 0.999 ? vec3(0, 0, 1) : vec3(1, 0, 0);
	vec3 TangentX = normalize(cross(UpVector, Z_dir));
	vec3 TangentY = cross(Z_dir, TangentX);
	return mat3(TangentX, TangentY, Z_dir);
}

float3 TangentToWorld(vec3 Vec, vec3 Z_dir)
{
	return mul(Vec, GetTangentBasis(Z_dir));
}

float RoughnessFromMip(float Mip, float CubemapMaxMip)
{
	float LevelFrom1x1 = CubemapMaxMip - 1 - Mip;
	return exp2(( - LevelFrom1x1) / 1.2);
}

vec4 CosineSampleHemisphere(vec2 E)
{
	float Phi = 2 * PI * E.x;
	float cos_theta = sqrt(E.y);
	float sin_theta = sqrt(1 - cos_theta * cos_theta);

	vec3 H;
	H.x = sin_theta * cos(Phi);
	H.y = sin_theta * sin(Phi);
	H.z = cos_theta;

	float PDF = cos_theta / PI;

	return vec4(H, PDF);
}

vec4 ImportanceSampleGGX(vec2 E, float roughness)
{
	float m = roughness * roughness;
	float m2 = m * m;

	float Phi = 2 * PI * E.x;
	float cos_theta = sqrt((1 - E.y) / (1 + (m2 - 1) * E.y));
	float sin_theta = sqrt(1 - cos_theta * cos_theta);

	vec3 H;
	H.x = sin_theta * cos(Phi);
	H.y = sin_theta * sin(Phi);
	H.z = cos_theta;

	float d = (cos_theta * m2 - cos_theta) * cos_theta + 1;
	float D = m2 / (PI*d*d);
	float PDF = D * cos_theta;

	return vec4(H, PDF);
}


float GetroughnessFromMip(float mip, float num_mips,float roughness_mip_scale)
{
	// exp2 is pow(2.0,x)
	return saturate(exp2( ( 3 - num_mips + mip) / roughness_mip_scale ));
}

vec4 CubeSampleLevel(samplerCUBE cubemap,vec3 view,float m)
{
	return texCUBElod(cubemap,vec4(view,m));
}

vec4 RoughnessMip(samplerCUBE sourceCubemap,vec3 view,int numMips,float alpha,float roughness,bool rough) 
{
	vec4 outp;
	float r = saturate(roughness);//exp2( ( 3 - numMips + mipIndex) / 1.2 );;
	float CubeSize = 1 << ( numMips - 1 );
	const float SolidAngleTexel = 4.0 *PI / float(( 6.0 * CubeSize * CubeSize ) * 2.0);
	const uint NumSamples =   64;
	vec4 result = vec4(0,0,0,0);
	mat3 TangentToWorld = GetTangentBasis(view);
	float Weight = 0.0;
	for (uint i = 0; i < NumSamples; i++)
	{
		vec2 E = Hammersley(i, NumSamples, uint2(0x8FFF,0x3f7F));
		vec3 L;
		if(rough) // used for roughness > 0.99
		{
			// roughness=1, GGX is constant. Use cosine distribution instead
			L		= CosineSampleHemisphere(E).xyz;
			float NoL = L.z;
			if( NoL > 0 )
			{
				L			=mul( L, TangentToWorld );
				vec4 lookup	=100.0*saturate(0.01*CubeSampleLevel(sourceCubemap, L, 0));
				result		+= NoL*lookup;
				Weight		+= NoL;
			}
		//	result+=vec4(abs(L),1.0);a
		}
		else
		{
			E.y *= 0.995;
			vec3 H	= ImportanceSampleGGX(E, r).xyz;
			L		= 2*H.z*H - vec3(0,0,1.0);
	
			float NoL = L.z;
			if( NoL > 0 )
			{
				//float NoH = H.z;
				//float PDF = D_GGX( r, NoH ) * 0.25;
				//float SolidAngleSample = 1.0 / ( NumSamples * PDF );
				//float Mip = 0.5 * log2( SolidAngleSample / SolidAngleTexel );
				//L=vec3(0,0,1.0);
				L = mul( L, TangentToWorld );
				// Apply a limit to avoid numerical issues:
				//#ifdef CPP_GLSL
				//	vec4 lookup=100.0*saturate(0.01*CubeSampleLevel(sourceCubemap,vec3(-L.x, -L.y, L.z), 0));
				//#else
					vec4 lookup=100.0*saturate(0.01*CubeSampleLevel(sourceCubemap, L, 0));
				//a#endif
				result	+= NoL*lookup;
			
				Weight += NoL;
			}
		}
	}
	outp = result / Weight;
	return vec4(outp.rgb, alpha);
}

vec4 Diffuse(samplerCUBE sourceCubemap,vec3 view) 
{
	vec4 outp;
	const uint NumSamples = 256;
	vec4 result = vec4(0,0,0,0);
	mat3 TangentToWorld = GetTangentBasis(view);
	float Weight = 0.0;
	for (uint i = 0; i < NumSamples; i++)
	{
		vec2 E = Hammersley(i, NumSamples, uint2(253*i,i*5));
		vec3 L;
		// roughness=1, GGX is constant. Use cosine distribution instead
		L		= CosineSampleHemisphere(E).xyz;
		float NoL = L.z;
		L		=mul( L, TangentToWorld );
		vec4 lookup=100.0*saturate(0.01*CubeSampleLevel(sourceCubemap, L, 2));
		result	+= NoL*lookup;
		Weight	+= NoL;
	}



	outp = result / Weight;
	return vec4(outp.rgb, 1.0);
}

#endif