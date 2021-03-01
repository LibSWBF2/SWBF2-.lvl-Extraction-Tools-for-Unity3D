Shader "ConversionAssets/SWBFTerrain"
{
    Properties {
        _Layer0Tex ("Layer 0 Texture", 2D) = "black" {}
        _Layer1Tex ("Layer 1 Texture", 2D) = "black" {}
        _Layer2Tex ("Layer 2 Texture", 2D) = "black" {}
        _Layer3Tex ("Layer 3 Texture", 2D) = "black" {}

        _Layer4Tex ("Layer 4 Texture", 2D) = "black" {}
        _Layer5Tex ("Layer 5 Texture", 2D) = "black" {}
        _Layer6Tex ("Layer 6 Texture", 2D) = "black" {}
        _Layer7Tex ("Layer 7 Texture", 2D) = "black" {}

        _Layer8Tex ("Layer 8 Texture", 2D) = "black" {}
        _Layer9Tex ("Layer 9 Texture", 2D) = "black" {}
        _Layer10Tex ("Layer 10 Texture", 2D) = "black" {}
        _Layer11Tex ("Layer 11 Texture", 2D) = "black" {}

        _Layer12Tex ("Layer 12 Texture", 2D) = "black" {}
        _Layer13Tex ("Layer 13 Texture", 2D) = "black" {}
        _Layer14Tex ("Layer 14 Texture", 2D) = "black" {}
        _Layer15Tex ("Layer 15 Texture", 2D) = "black" {}

        _BlendMap0  ("Blend Map Layers 0-3", 2D) = "red" {}
        _BlendMap1  ("Blend Map Layers 4-7", 2D) = "red" {}
        _BlendMap2  ("Blend Map Layers 8-11", 2D) = "red" {}
        _BlendMap3  ("Blend Map Layers 12-15", 2D) = "red" {}

        _XBound ("X Bound", Float) = 1024.0
        _ZBound ("Z Bound", Float) = 1024.0
    }
    SubShader {

        Pass {
            ZWrite On
        }
        
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
        sampler2D _BlendMap0;

        float _XBound;
        float _ZBound;

        void surf (Input IN, inout SurfaceOutput o) {
            float2 modulo = float2(32.0,32.0);
            float2 gridLoc = IN.worldPos.xz;
            float2 uvTex = (gridLoc % modulo) / modulo;
            //o.Albedo = tex2D(_Layer0Tex, uvTex).rgb;

            float3 lay0Diff = tex2D(_Layer0Tex, uvTex).rgb;
            float3 lay1Diff = tex2D(_Layer1Tex, uvTex).rgb;
            float3 lay2Diff = tex2D(_Layer2Tex, uvTex).rgb;
            float3 lay3Diff = tex2D(_Layer3Tex, uvTex).rgb;

            float2 bounds = float2(_XBound / 2.0, _ZBound / 2.0);

            //float4 blend = tex2D(_BlendMap0, ((IN.worldPos.xz + bounds) / bounds));
            float4 blend = tex2D(_BlendMap0, (IN.worldPos.xz + bounds) / (2.0 * bounds));
            
            //o.Albedo.rgb = blend.xyz;
            o.Albedo.rgb = lay0Diff * blend.r + lay1Diff * blend.g + lay2Diff * blend.b + lay3Diff * blend.a;

            //o.Albedo.rgb = float4(.5,.5,.5,1.0);
        }  
        ENDCG


        ZWrite Off
        Blend One One

        CGPROGRAM
        #pragma surface surf Lambert

        struct Input {
            float3 worldPos;
        };

        sampler2D _Layer4Tex;
        sampler2D _Layer5Tex;
        sampler2D _Layer6Tex;
        sampler2D _Layer7Tex;
        sampler2D _BlendMap1;

        float _XBound;
        float _ZBound;

        void surf (Input IN, inout SurfaceOutput o) {
            float2 modulo = float2(32.0,32.0);
            float2 gridLoc = IN.worldPos.xz;
            float2 uvTex = (gridLoc % modulo) / modulo;
            //o.Albedo = tex2D(_Layer0Tex, uvTex).rgb;

            float3 lay0Diff = tex2D(_Layer4Tex, uvTex).rgb;
            float3 lay1Diff = tex2D(_Layer5Tex, uvTex).rgb;
            float3 lay2Diff = tex2D(_Layer6Tex, uvTex).rgb;
            float3 lay3Diff = tex2D(_Layer7Tex, uvTex).rgb;

            float2 bounds = float2(_XBound / 2.0, _ZBound / 2.0);

            //float4 blend = tex2D(_BlendMap1, ((IN.worldPos.xz + bounds) / bounds));
            float4 blend = tex2D(_BlendMap1, (IN.worldPos.xz + bounds) / (2.0 * bounds));
            
            //o.Albedo.rgb = blend.xyz;
            o.Albedo.rgb = lay0Diff * blend.r + lay1Diff * blend.g + lay2Diff * blend.b + lay3Diff * blend.a;

            //o.Albedo.rgb = float4(.5,.5,.5,1.0);
        }  
        ENDCG

        ZWrite Off
        Blend One One

        CGPROGRAM
        #pragma surface surf Lambert

        struct Input {
            float3 worldPos;
        };

        sampler2D _Layer8Tex;
        sampler2D _Layer9Tex;
        sampler2D _Layer10Tex;
        sampler2D _Layer11Tex;
        sampler2D _BlendMap2;

        float _XBound;
        float _ZBound;

        void surf (Input IN, inout SurfaceOutput o) {
            float2 modulo = float2(32.0,32.0);
            float2 gridLoc = IN.worldPos.xz;
            float2 uvTex = (gridLoc % modulo) / modulo;
            //o.Albedo = tex2D(_Layer0Tex, uvTex).rgb;

            float3 lay0Diff = tex2D(_Layer8Tex, uvTex).rgb;
            float3 lay1Diff = tex2D(_Layer9Tex, uvTex).rgb;
            float3 lay2Diff = tex2D(_Layer10Tex, uvTex).rgb;
            float3 lay3Diff = tex2D(_Layer11Tex, uvTex).rgb;

            float2 bounds = float2(_XBound / 2.0, _ZBound / 2.0);

            //float4 blend = tex2D(_BlendMap2, ((IN.worldPos.xz + bounds) / bounds));
            float4 blend = tex2D(_BlendMap2, (IN.worldPos.xz + bounds) / (2.0 * bounds));
            
            //o.Albedo.rgb = blend.xyz;
            o.Albedo.rgb = lay0Diff * blend.r + lay1Diff * blend.g + lay2Diff * blend.b + lay3Diff * blend.a;

            //o.Albedo.rgb = float4(.5,.5,.5,1.0);
        }  
        ENDCG

        ZWrite Off
        Blend One One

        CGPROGRAM
        #pragma surface surf Lambert

        struct Input {
            float3 worldPos;
        };

        sampler2D _Layer12Tex;
        sampler2D _Layer13Tex;
        sampler2D _Layer14Tex;
        sampler2D _Layer15Tex;
        sampler2D _BlendMap3;

        float _XBound;
        float _ZBound;

        void surf (Input IN, inout SurfaceOutput o) {
            float2 modulo = float2(32.0,32.0);
            float2 gridLoc = IN.worldPos.xz;
            float2 uvTex = (gridLoc % modulo) / modulo;
            //o.Albedo = tex2D(_Layer0Tex, uvTex).rgb;

            float3 lay0Diff = tex2D(_Layer12Tex, uvTex).rgb;
            float3 lay1Diff = tex2D(_Layer13Tex, uvTex).rgb;
            float3 lay2Diff = tex2D(_Layer14Tex, uvTex).rgb;
            float3 lay3Diff = tex2D(_Layer15Tex, uvTex).rgb;

            float2 bounds = float2(_XBound / 2.0, _ZBound / 2.0);

            //float4 blend = tex2D(_BlendMap3, ((IN.worldPos.xz + bounds) / bounds));
            float4 blend = tex2D(_BlendMap3, (IN.worldPos.xz + bounds) / (2.0 * bounds));
            
            //o.Albedo.rgb = blend.xyz;
            o.Albedo.rgb = lay0Diff * blend.r + lay1Diff * blend.g + lay2Diff * blend.b + lay3Diff * blend.a;

            //o.Albedo.rgb = float4(.5,.5,.5,1.0);
        }  
        ENDCG
    } 
    Fallback "Diffuse"
}