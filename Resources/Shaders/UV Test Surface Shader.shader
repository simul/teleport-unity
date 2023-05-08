Shader "TeleportVR/UV Test Surface Shader"
{
    Properties
    {
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
		 _Cube ("Cubemap", CUBE) = "" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows addshadow
        #pragma enable_d3d11_debug_symbols 
        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _MainTex;
        samplerCUBE _Cube;

        struct Input
        {
            float2 uv_MainTex;
          float3 worldPos ;
        };

        fixed4 _Color;

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = fixed3(0,0,0);
            // Metallic and smoothness come from slider variables
            o.Alpha = c.a;
            float2 diff=abs(IN.uv_MainTex-float2(0.25,0.75));
            float dist=min(diff.x,diff.y);
			o.Emission =fixed3(0,0,0);
			o.Emission.rg=IN.uv_MainTex.xy;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
