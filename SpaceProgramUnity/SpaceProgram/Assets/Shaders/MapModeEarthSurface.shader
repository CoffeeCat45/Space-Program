Shader "SpaceProgram/MapModeEarthSurface"
{
    Properties
    {
        _ColorMap ("Geographic Color Map", 2D) = "white" {}
        _FallbackColor ("Fallback Color", Color) = (0.1, 0.2, 0.45, 1.0)
        _ColorTint ("Color Tint", Color) = (1.1, 1.1, 1.1, 1.0)
        _ColorMapFlipLongitude ("Flip Color U", Float) = 0
        _ColorMapFlipLatitude ("Flip Color V", Float) = 0
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
            Offset 1, 2

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "PlanetSpinCommon.hlsl"

            TEXTURE2D(_ColorMap);
            SAMPLER(sampler_ColorMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorMap_ST;
                float4 _FallbackColor;
                float4 _ColorTint;
                float _ColorMapFlipLongitude;
                float _ColorMapFlipLatitude;
                float _PlanetSpinLongitudeRadians;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 geographicUv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 directionOS : TEXCOORD0;
                float2 geographicUv : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.directionOS = normalize(input.positionOS.xyz);
                output.geographicUv = input.geographicUv;
                return output;
            }

            float2 DirectionToLatLongUv(float3 direction)
            {
                direction = normalize(direction);

                if (abs(direction.x) < 0.0001 && abs(direction.z) < 0.0001)
                {
                    return float2(0.5, direction.y > 0.0 ? 1.0 : 0.0);
                }

                float latitudeRadians = asin(clamp(direction.y, -1.0, 1.0));
                float longitudeRadians = atan2(direction.x, direction.z);
                float geographicU = longitudeRadians * (0.5 / 3.14159265) + 0.5;
                float geographicV = latitudeRadians * (1.0 / 3.14159265) + 0.5;
                return float2(geographicU, geographicV);
            }

            float2 ApplyEarthColorMapLayoutUv(float2 geographicUv)
            {
                geographicUv.x = lerp(geographicUv.x, 1.0 - geographicUv.x, _ColorMapFlipLongitude);
                geographicUv.y = lerp(geographicUv.y, 1.0 - geographicUv.y, _ColorMapFlipLatitude);
                return geographicUv;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Vertex geographic UV (Variant A).
                float2 uv = ApplyPlanetSpinToWrappedGeographicUv(
                    input.geographicUv,
                    _PlanetSpinLongitudeRadians
                );

                half4 colorSample = SAMPLE_TEXTURE2D(
                    _ColorMap,
                    sampler_ColorMap,
                    TRANSFORM_TEX(uv, _ColorMap)
                );
                half3 albedo = length(colorSample.rgb) <= 0.001h ?
                    _FallbackColor.rgb :
                    colorSample.rgb * _ColorTint.rgb;

                float3 normalWS = normalize(TransformObjectToWorldDir(input.directionOS));
                Light mainLight = GetMainLight();
                float ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 ambient = max(SampleSH(normalWS), half3(0.06, 0.07, 0.09));
                half3 lit = albedo * (mainLight.color * ndotl + ambient);
                lit = max(lit, albedo * 0.08h);
                return half4(lit, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
