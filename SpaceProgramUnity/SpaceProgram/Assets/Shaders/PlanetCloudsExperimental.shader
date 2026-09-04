Shader "SpaceProgram/PlanetCloudsExperimental"
{
    Properties
    {
        _CloudColor ("Cloud Color", Color) = (0.94, 0.96, 0.99, 1)
        _TwilightColor ("Twilight Color", Color) = (0.95, 0.42, 0.18, 1)
        _NightCloudColor ("Night Cloud Color", Color) = (0.055, 0.058, 0.068, 1)
        _NoiseScale ("Puff Scale", Float) = 28
        _WeatherScale ("Weather Scale", Float) = 5.4
        _Coverage ("Coverage", Range(0, 1)) = 0.36
        _Softness ("Softness", Range(0.01, 0.8)) = 0.16
        _Density ("Density", Range(0, 2)) = 1.12
        _WindLon ("Wind Radians Per Second", Float) = 0.000238
        _WindLat ("Wind Phase", Float) = 0
        _CloudTime ("Cloud Time Seconds", Float) = 0
        _TerminatorSoftness ("Terminator Softness", Float) = 0.22
        _NightBrightness ("Night Brightness", Range(0, 1)) = 0.45
        _TwilightStrength ("Twilight Strength", Range(0, 2)) = 1.0
        _VolumeStrength ("Fake Volume Strength", Range(0, 3)) = 1.2
        _WrapAmount ("Terminator Wrap", Range(0, 1)) = 0.28
        _StormIntensity ("Unused", Range(0, 2)) = 0.55
        _RimFadeEnd ("Limb Fade End", Range(0.05, 0.35)) = 0.14
        _CloudDetail ("Cloud Detail", Range(0, 1)) = 0.72
        _Cluster ("Unused Cluster", Range(0, 1)) = 0
        _FlowAmount ("Flow Amount", Range(0, 1)) = 0.24
        _VortexAmount ("Vortex Amount", Range(0, 2)) = 1.05
        _CellularAmount ("Cellular Amount", Range(0, 1)) = 0.58
        _LayerKind ("Layer Kind", Range(0, 1)) = 0
        _OrbitView ("Orbit View", Range(0, 1)) = 0
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
            Name "CloudShellExperimental"
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
                float _FlowAmount;
                float _VortexAmount;
                float _CellularAmount;
                float _LayerKind;
                float _OrbitView;
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
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
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

            float ValueNoiseRotated(float3 x)
            {
                return ValueNoise(x) * 0.62 + ValueNoise(x.yzx * 1.17 + 19.13) * 0.38;
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
                float sum = PerlinNoise(p) * 0.62;
                p = p * 2.03 + 13.7;
                sum += ValueNoise(p) * 0.38;
                return saturate(sum);
            }

            float Ridged(float3 p)
            {
                return 1.0 - abs(ValueNoiseRotated(p) * 2.0 - 1.0);
            }

            float Billow(float3 p)
            {
                return abs(PerlinNoise(p) * 2.0 - 1.0);
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

            float3 StormCenter(int stormIndex)
            {
                float fi = (float)stormIndex + 1.0;
                float lon = frac(fi * 0.2735 + 0.17) * 6.2831853;
                float y = lerp(0.38, 0.66, frac(fi * 0.6180339));
                if (stormIndex >= 3)
                {
                    y = -y;
                }

                float xz = sqrt(max(1.0 - y * y, 1e-4));
                return float3(xz * cos(lon), y, xz * sin(lon));
            }

            float StormSpiralField(float3 dir, float vortexAmount)
            {
                float amount = saturate(vortexAmount);
                if (amount <= 0.001)
                {
                    return 1.0;
                }

                float combined = 1.0;
                float radius = lerp(0.28, 0.26, saturate(_OrbitView));
                [unroll]
                for (int stormIndex = 0; stormIndex < 6; stormIndex++)
                {
                    float3 storm = StormCenter(stormIndex);
                    float d = 1.0 - saturate(dot(dir, storm));
                    float w = saturate(1.0 - d / radius);
                    w = w * w * (3.0 - 2.0 * w);
                    float3 refAxis = abs(storm.y) > 0.92 ? float3(1, 0, 0) : float3(0, 1, 0);
                    float3 xAxis = normalize(cross(refAxis, storm));
                    float3 yAxis = cross(storm, xAxis);
                    float2 p = float2(dot(dir, xAxis), dot(dir, yAxis));
                    float r = length(p);
                    float phi = atan2(p.y, p.x);
                    float arms = sin(phi * 2.0 * sign(storm.y) + r * 7.5) * 0.5 + 0.5;
                    float eye = saturate(1.0 - r / 0.048);
                    float band = lerp(0.80, 1.16, arms) * lerp(1.0, 0.32, eye);
                    combined = lerp(combined, band, w * amount);
                }

                return combined;
            }

            float JetBand(float3 dir, float3 sampleDir, float flow, float layer)
            {
                float meander = (ValueNoiseRotated(sampleDir * 3.2 + 8.7) - 0.5) * 0.10;
                float absLat = abs(dir.y + meander);
                float jetLat = smoothstep(0.18, 0.32, absLat) * (1.0 - smoothstep(0.50, 0.68, absLat));
                float ridge = Ridged(sampleDir * 8.4 + 5.5);
                return ridge * jetLat * saturate(flow) * lerp(0.22, 0.55, layer);
            }

            float SampleExperimental(float3 dir, float detail)
            {
                float layer = saturate(_LayerKind);
                float orbit = saturate(_OrbitView);
                float3 sampleDir = NoiseDir(dir);
                float3 pWeather = sampleDir * max(_WeatherScale, 0.5);
                float flowAmt = saturate(_FlowAmount);
                float warpA = ValueNoise(pWeather * 0.50 + 2.7);
                float warpB = ValueNoise(pWeather * 0.44 + 11.3);
                pWeather += float3(warpA - 0.5, warpB - 0.5, warpA - 0.5) * flowAmt * 0.32;

                float synoptic = PerlinNoise(pWeather * 0.46 + 1.7);
                float meso = FbmLow(pWeather + 4.1);
                float blob = synoptic * 0.44 + meso * 0.56;

                float coverT = saturate((_Coverage - 0.10) / 0.62);
                float envelope = smoothstep(
                    lerp(0.48, 0.30, coverT),
                    lerp(0.68, 0.50, coverT),
                    blob
                );
                float spine = smoothstep(
                    lerp(0.72, 0.58, coverT),
                    lerp(0.90, 0.78, coverT),
                    blob
                );

                float meander = (ValueNoiseRotated(sampleDir * 3.4 + 8.7) - 0.5) * 0.08;
                float absLat = abs(dir.y + meander);
                float dry = smoothstep(0.22, 0.34, absLat) * (1.0 - smoothstep(0.42, 0.58, absLat));
                envelope *= 1.0 - dry * 0.18;
                spine *= 1.0 - dry * 0.12;

                float lanes = PerlinNoise(pWeather * 2.18 + 7.3);
                lanes = lerp(lanes, ValueNoiseRotated(pWeather * 3.05 + 12.8), 0.42);
                float open = smoothstep(0.22, 0.62, lanes);
                float large = envelope * lerp(0.30, 1.0, open);
                large = max(large, spine * 0.72);

                float bodyScale = max(_NoiseScale, 1.0) * lerp(0.92, 1.18, detail) * lerp(1.0, 1.08, orbit);
                float3 pBody = sampleDir * bodyScale;
                float puff = Billow(pBody);
                puff = lerp(puff, ValueNoiseRotated(pBody * 2.11 + 6.2), 0.32);
                large *= lerp(0.78, 1.06, puff);

                float fringe = saturate(envelope * (1.0 - envelope) * 4.0);
                float erosion = Ridged(pBody * lerp(2.4, 2.9, orbit) + 21.0);
                large = saturate(large - erosion * fringe * lerp(0.18, 0.38, detail));

                float filaments = Ridged(pBody * 0.38 + 8.4);
                large = lerp(large, max(large, filaments * envelope * 0.55), layer * 0.72);
                large = saturate(large + JetBand(dir, sampleDir, flowAmt, layer) * envelope * 0.32);
                large *= lerp(1.0, 0.78, layer);

                float grainAmt = saturate(_CellularAmount);
                float3 pSmall = sampleDir * lerp(14.0, 18.0, orbit) + 41.7;
                float speck = Billow(pSmall) * lerp(0.65, 1.0, Billow(pSmall * 0.52 + 13.0));
                float grain = speck * fringe * grainAmt * lerp(0.85, 1.05, layer);

                float shaped = max(large, grain);
                shaped *= StormSpiralField(dir, _VortexAmount * lerp(1.0, 0.55, layer));
                return saturate(shaped);
            }

            float CloudAlphaFromNoise(
                float noiseValue,
                float coverage,
                float softness,
                float detail
            )
            {
                float edge = max(softness, 0.05);
                float coverT = saturate((coverage - 0.10) / 0.62);
                float tail = smoothstep(0.06, 0.20 + edge * lerp(1.35, 1.85, coverT), noiseValue);
                tail = pow(max(tail, 1e-4), 0.58);
                float body = smoothstep(0.20, 0.20 + edge * 1.05, noiseValue);
                float core = smoothstep(0.40, lerp(0.72, 0.62, detail), noiseValue);
                return saturate(max(tail * 0.70, body * 0.82 + core * 0.28));
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
                float noiseCurrent = SampleExperimental(dir, detail);
                float alphaField = CloudAlphaFromNoise(
                    noiseCurrent,
                    _Coverage,
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
                if (detail > 0.12 && _VolumeStrength > 0.12)
                {
                    float3 nd = NoiseDir(dir);
                    float weatherScale = max(_WeatherScale, 0.5) * 1.7;
                    slope = PerlinNoise(nd * weatherScale + 3.1) - 0.5;
                }

                float wrapNdotL = saturate((ndotl + _WrapAmount * detail) / (1.0 + _WrapAmount * detail));
                float twilightBand = saturate(1.0 - abs(sunDot) * 4.2) * saturate(sunDot);
                float dayShade = lerp(ndotl, wrapNdotL, twilightBand * lerp(0.35, 0.65, detail));
                float internalShadow = smoothstep(-0.08, 0.88, noiseCurrent);
                dayShade = saturate(dayShade + slope * _VolumeStrength * 0.40 * internalShadow);
                dayShade = lerp(0.62, 1.05, dayShade);
                float3 albedo = _CloudColor.rgb * lerp(
                    float3(0.40, 0.46, 0.56),
                    float3(1.03, 1.03, 1.04),
                    internalShadow
                );

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
