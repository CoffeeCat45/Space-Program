Shader "SpaceProgram/PlanetClouds"
{
    Properties
    {
        _CloudColor ("Cloud Color", Color) = (0.93, 0.95, 0.98, 0.9)
        _TwilightColor ("Twilight Color", Color) = (0.95, 0.42, 0.18, 1)
        _NightCloudColor ("Night Cloud Color", Color) = (0.028, 0.030, 0.036, 1)
        _NoiseScale ("Puff Scale", Float) = 72
        _WeatherScale ("Weather Scale", Float) = 7.5
        _Coverage ("Coverage", Range(0, 1)) = 0.32
        _Softness ("Softness", Range(0.01, 0.8)) = 0.18
        _Density ("Density", Range(0, 2)) = 1.1
        _WindLon ("Wind Radians Per Second", Float) = 0.0009
        _WindLat ("Wind Phase", Float) = 0
        _CloudTime ("Cloud Time Seconds", Float) = 0
        _TerminatorSoftness ("Terminator Softness", Float) = 0.22
        _NightBrightness ("Night Brightness", Range(0, 1)) = 0.55
        _TwilightStrength ("Twilight Strength", Range(0, 2)) = 1.0
        _VolumeStrength ("Fake Volume Strength", Range(0, 3)) = 1.35
        _WrapAmount ("Terminator Wrap", Range(0, 1)) = 0.32
        _StormIntensity ("Coriolis Storm Spin", Range(0, 2)) = 0.55
        _RimFadeEnd ("Limb Fade End", Range(0.05, 0.35)) = 0.14
        _CloudDetail ("Cloud Detail", Range(0, 1)) = 0.72
        _Cluster ("Cloud Clusters", Range(0, 1)) = 0
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
            Name "CloudShell"
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

            CBUFFER_START(UnityPerMaterial)
                float4 _CloudColor;
                float4 _TwilightColor;
                float4 _NightCloudColor;
                float _NoiseScale;
                float _WeatherScale;
                float _Coverage;
                float _Softness;
                float _Density;
                float _WindLon;
                float _WindLat;
                float _CloudTime;
                float _TerminatorSoftness;
                float _NightBrightness;
                float _TwilightStrength;
                float _VolumeStrength;
                float _WrapAmount;
                float _StormIntensity;
                float _RimFadeEnd;
                float _CloudDetail;
                float _Cluster;
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

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float3 HashGrad31(float3 p)
            {
                float n = Hash31(p);
                float3 g = frac(float3(n, n * 17.17, n * 27.13)) * 2.0 - 1.0;
                return g * rsqrt(max(dot(g, g), 1e-4));
            }

            float ValueNoise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash31(i);
                float b = Hash31(i + float3(1, 0, 0));
                float c = Hash31(i + float3(0, 1, 0));
                float d = Hash31(i + float3(1, 1, 0));
                float e = Hash31(i + float3(0, 0, 1));
                float f1 = Hash31(i + float3(1, 0, 1));
                float g = Hash31(i + float3(0, 1, 1));
                float h = Hash31(i + float3(1, 1, 1));
                float nx0 = lerp(a, b, f.x);
                float nx1 = lerp(c, d, f.x);
                float nx2 = lerp(e, f1, f.x);
                float nx3 = lerp(g, h, f.x);
                float nxy0 = lerp(nx0, nx1, f.y);
                float nxy1 = lerp(nx2, nx3, f.y);
                return lerp(nxy0, nxy1, f.z);
            }

            float PerlinNoise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                float3 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);

                float n000 = dot(HashGrad31(i), f);
                float n100 = dot(HashGrad31(i + float3(1, 0, 0)), f - float3(1, 0, 0));
                float n010 = dot(HashGrad31(i + float3(0, 1, 0)), f - float3(0, 1, 0));
                float n110 = dot(HashGrad31(i + float3(1, 1, 0)), f - float3(1, 1, 0));
                float n001 = dot(HashGrad31(i + float3(0, 0, 1)), f - float3(0, 0, 1));
                float n101 = dot(HashGrad31(i + float3(1, 0, 1)), f - float3(1, 0, 1));
                float n011 = dot(HashGrad31(i + float3(0, 1, 1)), f - float3(0, 1, 1));
                float n111 = dot(HashGrad31(i + float3(1, 1, 1)), f - float3(1, 1, 1));

                float nx00 = lerp(n000, n100, u.x);
                float nx10 = lerp(n010, n110, u.x);
                float nx01 = lerp(n001, n101, u.x);
                float nx11 = lerp(n011, n111, u.x);
                float nxy0 = lerp(nx00, nx10, u.y);
                float nxy1 = lerp(nx01, nx11, u.y);
                return lerp(nxy0, nxy1, u.z) * 0.5 + 0.5;
            }

            float FbmLow(float3 p)
            {
                float sum = ValueNoise(p) * 0.5;
                p = p * 2.05 + 17.1;
                sum += ValueNoise(p) * 0.5;
                return saturate(sum);
            }

            float FbmMixed(float3 p, float detail)
            {
                float sum = PerlinNoise(p) * 0.5;
                p = p * 2.05 + 17.1;
                sum += ValueNoise(p) * 0.28;
                if (detail >= 0.45)
                {
                    p = p * 2.03 + 9.7;
                    sum += ValueNoise(p) * 0.22;
                }
                return saturate(sum);
            }

            float3 RotateY(float3 direction, float radians)
            {
                float s;
                float c;
                sincos(radians, s, c);
                return float3(
                    direction.x * c - direction.z * s,
                    direction.y,
                    direction.x * s + direction.z * c
                );
            }

            float3 NoiseDir(float3 d)
            {
                return float3(d.y, d.z, d.x);
            }

            float ClimateCoverageMask(float3 dir)
            {
                float meander = (ValueNoise(NoiseDir(dir) * 3.4 + 8.7) - 0.5) * 0.08;
                float absLat = abs(dir.y + meander);
                float subtropical =
                    smoothstep(0.22, 0.34, absLat) *
                    (1.0 - smoothstep(0.40, 0.56, absLat));
                return 1.0 - subtropical * 0.16;
            }

            float3 ApplyCoriolisWarp(float3 outward, float detail)
            {
                if (detail <= 0.08)
                {
                    return outward;
                }

                float3 n = NoiseDir(outward);
                float3 offset = float3(
                    ValueNoise(n * 3.1 + 2.7),
                    ValueNoise(n * 3.1 + 9.4),
                    ValueNoise(n * 3.1 + 15.8)
                ) - 0.5;
                return normalize(outward + offset * _StormIntensity * 0.10 * detail);
            }

            float SampleCloudNoise(float3 dir, float noiseScale, float weatherScale, float detail)
            {
                float cluster = saturate(_Cluster);
                float3 sampleDir = NoiseDir(ApplyCoriolisWarp(dir, detail));
                float3 p = sampleDir * max(noiseScale, 1.0);
                float warpA = ValueNoise(p * 0.45 + 2.7);
                float warpB = ValueNoise(p * 0.51 + 11.3);
                float warpC = ValueNoise(p * 0.38 + 23.9);
                float warpAmt = lerp(0.35, 0.70, detail);
                float3 warped = p + float3(warpA, warpB, warpC) * warpAmt;
                float n = FbmMixed(warped, detail);
                if (detail > 0.35)
                {
                    n = lerp(n, ValueNoise(warped + n * 1.1), 0.28);
                }
                float3 weatherWarp = float3(warpA, warpB, warpC) * lerp(0.70, 1.15, detail);
                float weatherFreq = max(weatherScale, 0.5) * lerp(1.0, 0.58, cluster);
                float weather = FbmLow(sampleDir * weatherFreq + weatherWarp + 4.1);
                weather = saturate(weather * 0.78 + warpA * 0.22);
                float climate = ClimateCoverageMask(dir);
                weather = saturate(weather * lerp(0.88, 1.0, climate));
                float weatherLo = lerp(0.18, 0.30, cluster);
                float weatherHi = lerp(0.78, 0.55, cluster);
                float weatherFloor = lerp(0.22, 0.04, cluster);
                float weatherMask = smoothstep(weatherLo, weatherHi, weather);
                n *= lerp(weatherFloor, 1.0, weatherMask);
                n = lerp(n, saturate(n * lerp(0.12, 1.18, weatherMask)), cluster);
                return saturate(n);
            }

            float CloudAlphaFromNoise(
                float noiseValue,
                float coverage,
                float softness,
                float detail
            )
            {
                float edge = max(softness, 0.08);
                float fringe = smoothstep(coverage - 0.16, coverage + 0.06, noiseValue);
                float body = smoothstep(coverage - 0.02, coverage + edge * 1.10, noiseValue);
                float core = smoothstep(coverage + edge * 0.28, coverage + edge * 1.25, noiseValue);
                float packed = fringe * 0.18 + body * lerp(0.48, 0.62, detail) + core * 0.34;
                return saturate(packed);
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

                float3 unit = normalize(input.positionOS);
                float3 dir = RotateY(
                    unit,
                    _PlanetSpinLongitudeRadians + _CloudTime * _WindLon + _WindLat
                );

                float detail = saturate(_CloudDetail);
                float climateMask = ClimateCoverageMask(dir);
                float localCoverage = lerp(_Coverage, saturate(_Coverage * climateMask), 0.4);
                float noiseScale = _NoiseScale * lerp(0.72, 1.0, detail);
                float noiseCurrent = SampleCloudNoise(dir, noiseScale, _WeatherScale, detail);
                float alphaField = CloudAlphaFromNoise(
                    noiseCurrent,
                    localCoverage,
                    _Softness,
                    detail
                );
                float density = saturate(alphaField * _Density);

                if (density <= 0.004)
                {
                    return half4(0, 0, 0, 0);
                }

                Light mainLight = GetMainLight();
                float3 sunDir = mainLight.direction;
                float sunDot = dot(outward, sunDir);
                float ndotl = saturate(sunDot);

                float slope = 0.0;
                if (detail > 0.18 && _VolumeStrength > 0.15)
                {
                    float3 sunOffsetDir = normalize(dir + sunDir * lerp(0.004, 0.008, detail));
                    float cheapScale = noiseScale * 0.72;
                    slope = ValueNoise(dir * cheapScale) - ValueNoise(sunOffsetDir * cheapScale);
                }

                float wrapNdotL = saturate((ndotl + _WrapAmount * detail) / (1.0 + _WrapAmount * detail));
                float twilightBand = saturate(1.0 - abs(sunDot) * 4.2) * saturate(sunDot);
                float dayShade = lerp(ndotl, wrapNdotL, twilightBand * lerp(0.35, 0.65, detail));
                dayShade = saturate(dayShade + slope * _VolumeStrength * detail);
                dayShade = lerp(0.78, 1.0, dayShade);

                float twilight = saturate(1.0 - abs(sunDot) * 3.0) * saturate(sunDot);
                float3 dayLit = _CloudColor.rgb * mainLight.color * dayShade;
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
