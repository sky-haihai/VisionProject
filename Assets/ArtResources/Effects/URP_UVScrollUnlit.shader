Shader "ArtResources/URP/UVScrollUnlit"
{
    Properties
    {
        [MainTexture] _BaseMapA("Base Map A", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        _BaseScrollSpeedA("Base A Scroll Speed X", Float) = 0.5

        _MaskMapA("Opacity Mask A", 2D) = "white" {}
        _MaskScrollSpeedA("Mask A Scroll Speed X", Float) = 0.15

        _BaseMapB("Base Map B", 2D) = "white" {}
        _BaseScrollSpeedB("Base B Scroll Speed X", Float) = 0.35

        _MaskMapB("Opacity Mask B", 2D) = "white" {}
        _MaskScrollSpeedB("Mask B Scroll Speed X", Float) = 0.08
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMapA_ST;
                float4 _BaseMapB_ST;
                float4 _MaskMapA_ST;
                float4 _MaskMapB_ST;
                float _BaseScrollSpeedA;
                float _BaseScrollSpeedB;
                float _MaskScrollSpeedA;
                float _MaskScrollSpeedB;
            CBUFFER_END

            TEXTURE2D(_BaseMapA);
            SAMPLER(sampler_BaseMapA);
            TEXTURE2D(_BaseMapB);
            SAMPLER(sampler_BaseMapB);
            TEXTURE2D(_MaskMapA);
            SAMPLER(sampler_MaskMapA);
            TEXTURE2D(_MaskMapB);
            SAMPLER(sampler_MaskMapB);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uvA = TRANSFORM_TEX(input.uv, _BaseMapA);
                uvA.x += _Time.y * _BaseScrollSpeedA;

                float2 uvB = TRANSFORM_TEX(input.uv, _BaseMapB);
                uvB.x += _Time.y * _BaseScrollSpeedB;

                float2 maskAST = TRANSFORM_TEX(input.uv, _MaskMapA);
                float maskAU = maskAST.x + _Time.y * _MaskScrollSpeedA;
                float maskAV = maskAST.y;

                float2 maskBST = TRANSFORM_TEX(input.uv, _MaskMapB);
                float maskBU = maskBST.x + _Time.y * _MaskScrollSpeedB;
                float maskBV = maskBST.y;

                half maskA = SAMPLE_TEXTURE2D(_MaskMapA, sampler_MaskMapA, float2(maskAU, maskAV)).r;
                half maskB = SAMPLE_TEXTURE2D(_MaskMapB, sampler_MaskMapB, float2(maskBU, maskBV)).r;

                half4 layerA = SAMPLE_TEXTURE2D(_BaseMapA, sampler_BaseMapA, uvA) * _BaseColor;
                layerA.a *= maskA;

                half4 layerB = SAMPLE_TEXTURE2D(_BaseMapB, sampler_BaseMapB, uvB) * _BaseColor;
                layerB.a *= maskB;

                // Straight-alpha composite: B over A (B 在上层)
                half aOut = layerB.a + layerA.a * (1.0h - layerB.a);
                half3 rgbOut = (layerB.rgb * layerB.a + layerA.rgb * layerA.a * (1.0h - layerB.a)) / max(aOut, 0.0001h);

                return half4(rgbOut, aOut);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
