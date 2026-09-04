Shader "SpaceProgram/SandboxEarthGlobe"
{
    Properties
    {
        _BaseMap ("Geographic Color Map", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 geographicUv : TEXCOORD0;
            }

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalOS : TEXCOORD1;
                float2 geographicUv : TEXCOORD2;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalOS = normalize(input.normalOS);
                output.geographicUv = input.geographicUv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 colorSample = SAMPLE_TEXTURE2D(
                    _BaseMap,
                    sampler_BaseMap,
                    TRANSFORM_TEX(input.geographicUv, _BaseMap)
                );
                half3 albedo = colorSample.rgb * _BaseColor.rgb;

                float3 normalWS = normalize(TransformObjectToWorldDir(input.normalOS));
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 ambient = max(SampleSH(normalWS), half3(0.06, 0.07, 0.09));
                half3 lit = albedo * (mainLight.color * ndotl + ambient);
                lit = max(lit, albedo * 0.08h);
                return half4(lit, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
