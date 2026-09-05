Shader "SpaceProgram/EarthPlanetSurface"
{
    Properties
    {
        _ColorMap ("Color Map (RSS Earth)", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _FallbackColor ("Fallback Color", Color) = (0.2, 0.6, 1.0, 1.0)
        _ColorTint ("Color Tint", Color) = (1.15, 1.15, 1.15, 1.0)
        _NormalStrength ("Normal Strength", Range(0, 2)) = 0.35
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1
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
            ZWrite [_ZWrite]
            ZTest LEqual
            Offset 1, 2

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "PlanetSpinCommon.hlsl"

            TEXTURE2D(_ColorMap);
            SAMPLER(sampler_ColorMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _FallbackColor;
                float4 _ColorTint;
                float _NormalStrength;
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
                float3 positionOS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float2 geographicUv : TEXCOORD2;
            };

            float2 DirectionToLatLongUv(float3 direction)
            {
                direction = normalize(direction);

                if (abs(direction.x) < 0.0001 && abs(direction.z) < 0.0001)
                {
                    return float2(0.5, direction.y > 0.0 ? 0.0 : 1.0);
                }

                float latitudeRadians = asin(clamp(direction.y, -1.0, 1.0));
                float longitudeRadians = atan2(direction.x, direction.z);
                float geographicU = longitudeRadians * (0.5 / 3.14159265) + 0.5;
                float geographicV = latitudeRadians * (1.0 / 3.14159265) + 0.5;
                return float2(geographicU, geographicV);
            }

            float3 UnpackNormalDxt5nm(float4 packedNormal)
            {
                float2 normalXY = packedNormal.ag * 2.0 - 1.0;
                float normalZ = sqrt(1.0 - saturate(dot(normalXY, normalXY)));
                return float3(normalXY.x, normalXY.y, normalZ);
            }

            // Unwrap longitude derivatives across the u=0/1 seam so mip selection stays continuous.
            void ComputeCorrectedGradients(float2 uv, out float2 dx, out float2 dy)
            {
                dx = ddx(uv);
                dy = ddy(uv);

                if (dx.x > 0.5)
                {
                    dx.x -= 1.0;
                }
                else if (dx.x < -0.5)
                {
                    dx.x += 1.0;
                }

                if (dy.x > 0.5)
                {
                    dy.x -= 1.0;
                }
                else if (dy.x < -0.5)
                {
                    dy.x += 1.0;
                }
            }

            half3 SamplePlanetAlbedo(float2 uv, float2 dx, float2 dy)
            {
                half4 colorSample = SAMPLE_TEXTURE2D_GRAD(
                    _ColorMap,
                    sampler_ColorMap,
                    uv,
                    dx,
                    dy
                );
                return length(colorSample.rgb) <= 0.001h ?
                    _FallbackColor.rgb :
                    colorSample.rgb * _ColorTint.rgb;
            }

            float3 SamplePlanetTangentNormal(float2 uv, float2 dx, float2 dy)
            {
                float4 packedNormal = SAMPLE_TEXTURE2D_GRAD(
                    _NormalMap,
                    sampler_NormalMap,
                    uv,
                    dx,
                    dy
                );
                return UnpackNormalDxt5nm(packedNormal);
            }

            float3 BuildSphereTangentFrame(float3 normalWS, out float3 tangentWS, out float3 bitangentWS)
            {
                float3 referenceUp = abs(normalWS.y) > 0.95 ?
                    float3(1.0, 0.0, 0.0) :
                    float3(0.0, 1.0, 0.0);
                tangentWS = normalize(cross(referenceUp, normalWS));
                bitangentWS = normalize(cross(normalWS, tangentWS));
                return normalWS;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionOS = input.positionOS.xyz;
                output.positionWS = positionInputs.positionWS;
                output.geographicUv = input.geographicUv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 directionOS = normalize(input.positionOS);
                float3 normalWS = normalize(TransformObjectToWorldDir(directionOS));
                float2 uv = ApplyPlanetSpinToWrappedGeographicUv(
                    input.geographicUv,
                    _PlanetSpinLongitudeRadians
                );

                float2 dx;
                float2 dy;
                ComputeCorrectedGradients(uv, dx, dy);

                half3 albedo = SamplePlanetAlbedo(uv, dx, dy);
                float poleLatitude = abs(directionOS.y);
                float poleSoftening = smoothstep(0.88, 0.995, poleLatitude);

                float3 tangentWS;
                float3 bitangentWS;
                BuildSphereTangentFrame(normalWS, tangentWS, bitangentWS);

                float3 tangentNormal = SamplePlanetTangentNormal(uv, dx, dy);
                float normalBlend = _NormalStrength * (1.0 - poleSoftening * 0.85);
                tangentNormal = normalize(lerp(float3(0, 0, 1), tangentNormal, normalBlend));

                normalWS = normalize(
                    tangentWS * tangentNormal.x +
                    bitangentWS * tangentNormal.y +
                    normalWS * tangentNormal.z
                );

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 ambient = SampleSH(normalWS);
                ambient = max(ambient, half3(0.06, 0.07, 0.09));
                half3 lit = albedo * (mainLight.color * ndotl + ambient);
                lit = max(lit, albedo * 0.08h);
                lit = lerp(lit, lit * 0.82h, poleSoftening);
                lit = min(lit, half3(1.15, 1.15, 1.15));
                return half4(lit, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
