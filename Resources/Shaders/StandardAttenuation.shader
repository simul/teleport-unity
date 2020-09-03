// Reproduces untiyAttenuation 1024x1 texture.
Shader "Teleport/StandardAttenuation" {
Properties {
    _ShadowMapTexture ("", any) = "" {}
    _ODSWorldTexture("", 2D) = "" {}
}

CGINCLUDE

#pragma enable_d3d11_debug_symbols

#include "UnityCG.cginc"

// Configuration



struct appdata {
    float4 vertex : POSITION;
    float2 texcoord : TEXCOORD0;
};

struct v2f {
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
};

v2f vert (appdata v)
{
    v2f o;
    o.pos =  UnityObjectToClipPos(v.vertex);
    o.uv = v.texcoord;
    return o;
}

/**
 *  Hard shadow
 */
float4 frag (v2f i) : SV_Target
{
    float U_CONST=100.0;
    float c=i.uv.x*U_CONST+1.0;
    float4 res =float4(saturate(1.0/c),0,0,1.0);//0.4/c*c,0,0,0);
	return res;
}

ENDCG


// ----------------------------------------------------------------------------------------
// Subshader for hard shadows:
// Just collect shadows into the buffer. Used on pre-SM3 GPUs and when hard shadows are picked.

SubShader
{
    Pass
    {
        ZWrite Off ZTest Always Cull Off

        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag

        ENDCG
    }
}

Fallback Off
}
