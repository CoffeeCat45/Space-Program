Shader "SpaceProgram/EarthAtmosphere"
{
    Properties
    {
        _DayColor ("Day Color", Color) = (0.40, 0.66, 0.98, 1)
        _HorizonColor ("Horizon Color", Color) = (0.80, 0.88, 0.96, 1)
        _SunsetColor ("Sunset Color", Color) = (0.95, 0.42, 0.18, 1)
        _PlanetRadius ("Planet Radius", Float) = 637100
        _AtmosphereHeight ("Atmosphere Height", Float) = 115000
        _ScaleHeight ("Dense Scale Height", Float) = 1200
        _LimbHeight ("Limb Scale Height", Float) = 8000
        _SkyOpacity ("Sky Opacity", Float) = 0.985
        _SkyExtinction ("Sky Extinction", Float) = 6.4
        _LimbStrength ("Limb Strength", Float) = 0.000028
        _ExteriorAlphaBehindScale ("Exterior Alpha Behind Planet", Range(0, 1)) = 0.18
        _ExteriorAlphaBoost ("Exterior Alpha Boost", Float) = 1
        _TerminatorSoftness ("Terminator Softness", Float) = 0.22
        _CameraAltitude ("Camera Altitude", Float) = 0
        _BlackZenithAltStart ("Black Zenith Alt Start", Float) = 8000
        _BlackZenithAltEnd ("Black Zenith Alt End", Float) = 28000
        _StarAppearAltStart ("Star Appear Alt Start", Float) = 32000
        _StarAppearAltEnd ("Star Appear Alt End", Float) = 48000
        _ShellFadeStartAlt ("Shell Fade Start Alt", Float) = 58000
        _ShellFadeEndAlt ("Shell Fade End Alt", Float) = 105000
        _MapMode ("Map Mode", Float) = 0
        _Opacity ("Opacity", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "AtmosphereScatter"
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
                float4 _DayColor;
                float4 _HorizonColor;
                float4 _SunsetColor;
                float _PlanetRadius;
                float _AtmosphereHeight;
                float _ScaleHeight;
                float _LimbHeight;
                float _SkyOpacity;
                float _SkyExtinction;
                float _LimbStrength;
                float _ExteriorAlphaBehindScale;
                float _ExteriorAlphaBoost;
                float _TerminatorSoftness;
                float _CameraAltitude;
                float _BlackZenithAltStart;
                float _BlackZenithAltEnd;
                float _StarAppearAltStart;
                float _StarAppearAltEnd;
                float _ShellFadeStartAlt;
                float _ShellFadeEndAlt;
                float _MapMode;
                float _Opacity;
            CBUFFER_END

            float AltSmooth(float startAlt, float endAlt, float altitude)
            {
                float t = saturate((altitude - startAlt) / max(endAlt - startAlt, 1.0));
                return t * t * (3.0 - 2.0 * t);
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                // Inflate so rasterization covers the true sphere; the fragment
                // ray-sphere test restores a round limb instead of icosphere edges.
                float3 inflatedOS = input.positionOS.xyz * 1.08;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(inflatedOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                return output;
            }

            bool RaySphere(float3 origin, float3 dir, float radius, out float tNear, out float tFar)
            {
                float b = dot(origin, dir);
                float c = dot(origin, origin) - radius * radius;
                float disc = b * b - c;
                if (disc < 0.0)
                {
                    tNear = -1.0;
                    tFar = -1.0;
                    return false;
                }

                float s = sqrt(disc);
                tNear = -b - s;
                tFar = -b + s;
                return tFar > 0.0;
            }

            float WorldRadiusFromObject(float objectRadius)
            {
                float3 origin = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float3 rim = TransformObjectToWorld(float3(objectRadius, 0.0, 0.0));
                return length(rim - origin);
            }

            half4 Frag(Varyings input, float faceSign : VFACE) : SV_Target
            {
                float3 planetOriginWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float worldOuterRadius = WorldRadiusFromObject(_PlanetRadius + _AtmosphereHeight);
                float worldPlanetRadius = WorldRadiusFromObject(_PlanetRadius);
                float3 camRel = _WorldSpaceCameraPos - planetOriginWS;
                float cameraRadius = length(camRel);
                bool inside = _MapMode < 0.5 && cameraRadius < worldOuterRadius - 1.0;
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

                float3 rayDir = normalize(input.positionWS - _WorldSpaceCameraPos);
                // Object units may be meters (LOD) or 0.5 (distant unit globe).
                // Do not floor the denominator at 1 — that explodes H on the unit globe.
                float objectOuterRadius = max(_PlanetRadius + _AtmosphereHeight, 1e-8);
                float scaleHeight = max(_ScaleHeight * (worldOuterRadius / objectOuterRadius), 1e-8);
                float limbHeight = max(_LimbHeight * (worldOuterRadius / objectOuterRadius), 1e-8);
                float3 camUp = camRel / max(cameraRadius, 1.0);

                Light mainLight = GetMainLight();
                float3 sunDir = mainLight.direction;
                float camSun = dot(camUp, sunDir);

                if (inside)
                {
                    float camHeight = max(0.0, cameraRadius - worldPlanetRadius);
                    float terminator = smoothstep(-_TerminatorSoftness, _TerminatorSoftness + 0.18, camSun);
                    float mu = saturate(dot(camUp, rayDir));
                    float horizon = 1.0 - mu;
                    float column = exp(-camHeight / scaleHeight);
                    float haze = exp(-camHeight / limbHeight);
                    float chapman = 2.0 * scaleHeight / max(worldPlanetRadius, 1.0);
                    float airmass = 1.0 / sqrt(mu * mu + chapman);
                    float opticalDepth = (column + 0.08 * haze) * airmass;
                    float altStarFade = AltSmooth(_StarAppearAltStart, _StarAppearAltEnd, _CameraAltitude);
                    float rayStarLeak = exp(-_SkyExtinction * opticalDepth);
                    float rayStarVisible = smoothstep(0.04, 0.52, rayStarLeak);
                    float starVisible = rayStarVisible * altStarFade;
                    float shellFade = 1.0 - AltSmooth(_ShellFadeStartAlt, _ShellFadeEndAlt, camHeight);
                    float skyAlpha = saturate((1.0 - starVisible) * _SkyOpacity * shellFade);
                    float altDarken = pow(
                        AltSmooth(_BlackZenithAltStart, _BlackZenithAltEnd, _CameraAltitude),
                        0.72
                    );
                    float zenithInner = lerp(0.90, 0.38, altDarken);
                    float zenithOuter = lerp(0.98, 0.58, altDarken);
                    float zenithMask = smoothstep(zenithInner, zenithOuter, mu);
                    float zenithDark = saturate(zenithMask * altDarken * 1.18) * (1.0 - starVisible);
                    float3 spaceTint = half3(0.004, 0.006, 0.014);

                    float sunset = saturate(1.0 - abs(camSun) * 3.2) * saturate(camSun + 0.32);
                    float3 color = lerp(_DayColor.rgb, _HorizonColor.rgb, saturate(horizon * 0.85 + 0.1));
                    color = lerp(color, _SunsetColor.rgb, sunset * 0.7);
                    color = lerp(color, spaceTint, zenithDark);
                    color *= mainLight.color;
                    color *= lerp(0.05, 1.0, terminator);
                    color *= lerp(0.22, 1.0, skyAlpha);
                    float alpha = skyAlpha * terminator * saturate(_Opacity);
                    return half4(color, saturate(alpha));
                }

                float tAtmNear;
                float tAtmFar;
                if (!RaySphere(camRel, rayDir, worldOuterRadius, tAtmNear, tAtmFar))
                {
                    return half4(0, 0, 0, 0);
                }

                float tStart = max(tAtmNear, 0.0);
                float tEnd = tAtmFar;
                float tPlanetNear;
                float tPlanetFar;
                bool planetBehind = RaySphere(
                    camRel,
                    rayDir,
                    worldPlanetRadius,
                    tPlanetNear,
                    tPlanetFar
                ) && tPlanetNear > 0.0;
                if (planetBehind)
                {
                    tEnd = min(tEnd, tPlanetNear);
                }

                float tClosest = clamp(-dot(camRel, rayDir), tStart, tEnd);
                float hMin = max(0.0, length(camRel + rayDir * tClosest) - worldPlanetRadius);
                float radiusAtMin = max(worldPlanetRadius + hMin, 1.0);
                float opticalDepth =
                    exp(-hMin / scaleHeight) * sqrt(6.2832 * radiusAtMin * scaleHeight) +
                    0.1 * exp(-hMin / limbHeight) * sqrt(6.2832 * radiusAtMin * limbHeight);
                if (planetBehind)
                {
                    opticalDepth *= 0.22;
                }

                float3 shadePos = planetBehind
                    ? (camRel + rayDir * tPlanetNear)
                    : (camRel + rayDir * tClosest);
                float3 shadeOut = normalize(shadePos);
                float pixelSun = dot(shadeOut, sunDir);
                float viewSun = dot(camUp, sunDir);
                float scatter = saturate(1.0 - exp(-_LimbStrength * opticalDepth));
                float terminator = smoothstep(-0.02, 0.28, pixelSun);
                float sunset = saturate(1.0 - abs(pixelSun) * 3.2) * saturate(pixelSun);
                float3 color = lerp(_DayColor.rgb, _SunsetColor.rgb, sunset * 0.8);
                color *= mainLight.color;
                color *= terminator;
                float viewNight = smoothstep(0.10, -0.22, viewSun);
                float dayCrescent = smoothstep(0.02, 0.38, pixelSun);
                float nightViewKill = viewNight * (1.0 - dayCrescent);
                float alpha = scatter * terminator * (1.0 - nightViewKill * 0.94) * _ExteriorAlphaBoost;
                if (planetBehind)
                {
                    alpha *= _ExteriorAlphaBehindScale;
                }

                return half4(color, saturate(alpha * saturate(_Opacity)));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
