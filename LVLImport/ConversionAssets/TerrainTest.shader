Shader "ConversionAssets/TerrainTest"
{
    Properties {
        _Layer0Tex ("Texture", 2D) = "black" {}
        _Layer1Tex ("Texture", 2D) = "black" {}
        _Layer2Tex ("Texture", 2D) = "black" {}
        _Layer3Tex ("Texture", 2D) = "black" {}
        _BlendMap  ("Texture", 2D) = "red" {}

        _XBound ("X Bound", Float) = 1024.0
        _ZBound ("Z Bound", Float) = 1024.0
    }
    SubShader {
        Tags { "RenderType" = "Opaque" }
        CGPROGRAM
        #pragma surface surf Lambert

        struct Input {
            float3 worldPos;
        };

        sampler2D _Layer0Tex;
        sampler2D _Layer1Tex;
        sampler2D _Layer2Tex;
        sampler2D _Layer3Tex;
        sampler2D _BlendMap;

        float _XBound;
        float _ZBound;

        void surf (Input IN, inout SurfaceOutput o) {
            float2 modulo = float2(32.0,32.0);
            float2 uvTex = (IN.worldPos.xz % modulo) / modulo;
            //o.Albedo = tex2D(_Layer0Tex, uvTex).rgb;

            float3 lay0Diff = tex2D(_Layer0Tex, uvTex).rgb;
            float3 lay1Diff = tex2D(_Layer1Tex, uvTex).rgb;
            float3 lay2Diff = tex2D(_Layer2Tex, uvTex).rgb;
            float3 lay3Diff = tex2D(_Layer3Tex, uvTex).rgb;

            float2 bounds = float2(_XBound, _ZBound);
            float4 blend = tex2D(_BlendMap, (IN.worldPos.xz + bounds) / (2.0 * bounds));
            
            //o.Albedo.rgb = blend.xyz;
            o.Albedo.rgb = lay0Diff * blend.r + lay1Diff * blend.g + lay2Diff * blend.b + lay3Diff * blend.a;
        }  
        ENDCG
    } 
    Fallback "Diffuse"
}