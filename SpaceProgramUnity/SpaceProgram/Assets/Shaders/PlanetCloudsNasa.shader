Shader "SpaceProgram/PlanetCloudsNasa"
{
    Properties
    {
        _CloudMap ("Cloud Map", 2D) = "black" {}
        _CloudColor ("Cloud Color", Color) = (0.94, 0.96, 0.99, 1)
        _TwilightColor ("Twilight Color", Color) = (0.95, 0.42, 0.18, 1)
        _NightCloudColor ("Night Cloud Color", Color) = (0.055, 0.058, 0.068, 1)
        _Coverage ("Opacity", Range(0, 1)) = 1
        _Softness ("Softness", Range(0.01, 0.8)) = 0.08
        _Density ("Density", Range(0, 2)) = 1.05
        _NightBrightness ("Night Brightness", Range(0, 1)) = 0.45
        _TwilightStrength ("Twilight Strength", Range(0, 2)) = 1.0
        _VolumeStrength ("Thickness Contrast", Range(0, 3)) = 1.2
        _WrapAmount ("Terminator Wrap", Range(0, 1)) = 0.28
        _CloudDetail ("Cloud Detail", Range(0, 1)) = 0.72
        _PlanetSpinLongitudeRadians ("Planet Spin Longitude", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "CloudShellNasa"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_CloudMap);
            SAMPLER(sampler_CloudMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _CloudMap_ST;
                float4 _CloudColor;
                float4 _TwilightColor;
                float4 _NightCloudColor;
                float _Coverage;
                float _Softness;
                float _Density;
                float _NightBrightness;
                float _TwilightStrength;
                float _VolumeStrength;
                float _WrapAmount;
                float _CloudDetail;
                float _PlanetSpinLongitudeRadians;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                float3 planetOriginWS : TEXCOORD2;
            };

            // work_terrain: UV from sim (west = -X), texU = geographicU, never 1-U.
            // Mesh Frame positions are x-flipped; unflip before atan2.
            // Per-pixel UV — interpolating vertex UV across lon ±180 smears the date line.
            float3 SimUnitFromMeshFrame(float3 positionOS)
            {
                float3 meshUnit = normalize(positionOS);
                return normalize(float3(-meshUnit.x, meshUnit.y, meshUnit.z));
            }

            float2 GeographicUvFromSimUnit(float3 simUnit)
            {
                float ring = length(simUnit.xz);
                float lon = ring > 1e-5 ? atan2(simUnit.x, simUnit.z) : 0.0;
                float lat = asin(clamp(simUnit.y, -1.0, 1.0));
                float u = lon * (0.5 / 3.141592653589793) + 0.5;
                float v = lat * (1.0 / 3.141592653589793) + 0.5;
                u = frac(u + _PlanetSpinLongitudeRadians * 0.0 + 1.0);
                v = clamp(v, 0.003, 0.997);
                return float2(u, v);
            }

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

            float SampleCloudCoverage(float3 simUnit)
            {
                float2 uv = GeographicUvFromSimUnit(simUnit);
                float2 dx;
                float2 dy;
                ComputeCorrectedGradients(uv, dx, dy);
                float ring = max(length(simUnit.xz), 1e-5);
                float lonAtten = saturate(ring * 8.0);
                dx.x *= lonAtten;
                dy.x *= lonAtten;

                float polar = saturate((abs(simUnit.y) - 0.92) / 0.07);
                float sharp = SAMPLE_TEXTURE2D_GRAD(_CloudMap, sampler_CloudMap, uv, dx, dy).r;
                float soft = SAMPLE_TEXTURE2D_LOD(
                    _CloudMap,
                    sampler_CloudMap,
                    uv,
                    polar * polar * 3.5
                ).r;
                return lerp(sharp, soft, polar);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.planetOriginWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                return output;
            }

            half4 Frag(Varyings input, float faceSign : VFACE) : SV_Target
            {
                float3 planetOriginWS = input.planetOriginWS;
                float3 outward = normalize(input.positionWS - planetOriginWS);
                float cameraRadius = length(_WorldSpaceCameraPos - planetOriginWS);
                float layerRadius = length(input.positionWS - planetOriginWS);
                bool inside = cameraRadius < layerRadius - 40.0;
                bool frontFace = faceSign > 0.0;
                if (inside)
                {
                    if (frontFace)
                    {
                        return half4(0, 0, 0, 0);
                    }
                }
                else if (!frontFace)
                {
                    return half4(0, 0, 0, 0);
                }

                float3 simUnit = SimUnitFromMeshFrame(input.positionOS);
                float coverage = SampleCloudCoverage(simUnit);
                float mask = saturate(coverage);
                mask = mask * mask * (3.0 - 2.0 * mask);

                float density = saturate(mask * _Density * saturate(_Coverage));
                if (density <= 0.004)
                {
                    return half4(0, 0, 0, 0);
                }

                Light mainLight = GetMainLight();
                float3 sunDir = mainLight.direction;
                float sunDot = dot(outward, sunDir);
                float ndotl = saturate(sunDot);
                float detail = saturate(_CloudDetail);
                float wrapNdotL = saturate((ndotl + _WrapAmount * detail) / (1.0 + _WrapAmount * detail));
                float twilightBand = saturate(1.0 - abs(sunDot) * 4.2) * saturate(sunDot);
                float dayShade = lerp(ndotl, wrapNdotL, twilightBand * lerp(0.35, 0.65, detail));
                float internalShadow = smoothstep(-0.05, 0.90, mask);
                dayShade = lerp(0.62, 1.04, saturate(dayShade));
                float3 albedo = _CloudColor.rgb * lerp(
                    float3(0.42, 0.48, 0.58),
                    float3(1.02, 1.02, 1.03),
                    lerp(0.55, 1.0, internalShadow)
                );
                albedo *= lerp(1.0, lerp(0.92, 1.06, internalShadow), saturate(_VolumeStrength * 0.35));

                float twilight = saturate(1.0 - abs(sunDot) * 3.0) * saturate(sunDot);
                float3 dayLit = albedo * mainLight.color * dayShade;
                float3 twilightLit = lerp(
                    dayLit,
                    _TwilightColor.rgb * mainLight.color * dayShade,
                    twilight * _TwilightStrength * 0.75
                );

                float3 nightLit = _NightCloudColor.rgb;
                float lightIntensity = smoothstep(0.02, 0.28, sunDot);
                float night = 1.0 - lightIntensity;
                float3 lit = lerp(nightLit, twilightLit, lightIntensity);

                float alpha = saturate(density);
                float nightAlpha = saturate(
                    pow(max(density, 1e-4), 0.50) * (1.0 + _NightBrightness)
                );
                alpha = saturate(lerp(alpha, nightAlpha, night));
                return half4(lit, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
