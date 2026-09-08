Shader "SpaceProgram/OrbitMarkerOverlay"
{
    Properties
    {
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)
        [MainTexture] _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Overlay"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "OrbitMarkerOverlay"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "OrbitOverlayOcclusion.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _MainTex_ST;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float occluded : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                float3 anchorWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                output.occluded = IsOccludedByPlanet(anchorWS) ? 1.0 : 0.0;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                if (input.occluded > 0.5)
                {
                    discard;
                }

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                return tex * _BaseColor;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
