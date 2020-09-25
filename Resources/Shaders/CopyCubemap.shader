Shader "Teleport/CopyCubemap"
{
    CGINCLUDE

    #pragma enable_d3d11_debug_symbols
    float Roughness;
    int MipIndex;
    int NumMips;
    uint Face;
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
        float2 uv       : TEXCOORD0;
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
        clipPos     = float4(v.vertex.xy*2.0-float2(1.0,1.0),1.0,1.0);
        o.pos       = clipPos;
        o.uv        = v.uv;

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
		o.pos = float4(GetOutPosFromVertexId(vid), 1.0);
		o.uv = GetUvFromVertexId(vid);
		return o;
	}
    
    
    fixed4 copy_face_frag (v2f i) : SV_Target
    {
        vec3 view   =CubeFaceAndTexCoordsToView(Face,i.uv);
        vec4 res    =CubeSampleLevel (_SourceCubemapTexture, view, (float)MipIndex) ;
	    return res;
    }
    fixed4 mip_frag (v2f i) : SV_Target
    {
        vec3 view   =CubeFaceAndTexCoordsToView(Face,i.uv);
        //float roughness=GetroughnessFromMip(float(MipIndex),float(NumMips),1.2);
        vec4 res    =RoughnessMip(_SourceCubemapTexture, view, NumMips, 1.0, Roughness,false);
	    return res;
    }
    fixed4 diffuse_frag (v2f i) : SV_Target
    {
        vec3 view   =CubeFaceAndTexCoordsToView(Face,i.uv);
       // vec4 res    =RoughnessMip(_SourceCubemapTexture, view, NumMips, 1.0, 1.0,true);
        vec4 res    =Diffuse(_SourceCubemapTexture, view);
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
            #pragma fragment copy_face_frag
            ENDCG
        }
        Pass
        {
            ZWrite Off ZTest Always Cull Off

            CGPROGRAM
            #pragma vertex vert_from_id
            #pragma fragment diffuse_frag
            ENDCG
        }
    }


    Fallback Off
}
