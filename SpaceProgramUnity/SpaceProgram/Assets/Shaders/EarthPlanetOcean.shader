Shader "SpaceProgram/EarthPlanetOcean"
{
    Properties
    {
        _PlanetColorMap ("Planet Color", 2D) = "white" {}
        _DeepColor ("Deep Color", Color) = (0.02, 0.10, 0.22, 0.82)
        _ShallowColor ("Shallow Color", Color) = (0.08, 0.28, 0.42, 0.72)
        _Specular ("Specular", Color) = (0.55, 0.75, 0.90, 1)
        _WaveScale ("Wave Scale", Float) = 140.0
        _WaveSpeed ("Wave Speed", Float) = 0.035
        _ColorMapFlipLongitude ("Flip Color U", Float) = 0
        _ColorMapFlipLatitude ("Flip Color V", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "PlanetSpinCommon.hlsl"

            TEXTURE2D(_PlanetColorMap);
            SAMPLER(sampler_PlanetColorMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _DeepColor;
                float4 _ShallowColor;
                float4 _Specular;
                float _WaveScale;
                float _WaveSpeed;
                float _ColorMapFlipLongitude;
                float _ColorMapFlipLatitude;
                float _PlanetSpinLongitudeRadians;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 latLongUv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 latLongUv : TEXCOORD2;
            };

            float2 ApplyColorMapLayoutUv(float2 uv)
            {
                uv.x = lerp(uv.x, 1.0 - uv.x, _ColorMapFlipLongitude);
                uv.y = lerp(uv.y, 1.0 - uv.y, _ColorMapFlipLatitude);
                return uv;
            }

            float OceanLikeness(half3 macroColor)
            {
                float3 c = saturate((float3)macroColor);
                float luma = dot(c, float3(0.299, 0.587, 0.114));
                float blueExcess = saturate(c.b - max(c.r, c.g) * 0.95);
                return saturate(blueExcess * 5.0) * saturate((0.40 - luma) * 4.0);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalize(TransformObjectToWorldDir(input.normalOS));
                output.latLongUv = input.latLongUv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 splatUv = ApplyColorMapLayoutUv(
                    ApplyPlanetSpinToWrappedGeographicUv(
                        input.latLongUv,
                        _PlanetSpinLongitudeRadians
                    )
                );
                half3 planetMacro = SAMPLE_TEXTURE2D(
                    _PlanetColorMap,
                    sampler_PlanetColorMap,
                    splatUv
                ).rgb;
                float waterMask = OceanLikeness(planetMacro);
                clip(waterMask - 0.12);

                float time = _Time.y * _WaveSpeed;
                float2 waveUv = splatUv * _WaveScale;
                float waveA = sin(waveUv.x * 6.2831853 + time * 2.1) *
                    sin(waveUv.y * 6.2831853 - time * 1.4);
                float waveB = sin((waveUv.x * 1.7 + waveUv.y) * 6.2831853 - time * 2.8);
                float wave = waveA * 0.55 + waveB * 0.45;

                float3 normalWS = normalize(input.normalWS);
                float3 tangent = normalize(cross(normalWS, float3(0.0, 1.0, 0.0)));
                if (dot(tangent, tangent) < 1e-4)
                {
                    tangent = float3(1.0, 0.0, 0.0);
                }
                float3 bitangent = normalize(cross(normalWS, tangent));
                normalWS = normalize(normalWS + tangent * wave * 0.18 + bitangent * waveB * 0.12);

                float3 viewDir = normalize(_WorldSpaceCameraPos - input.positionWS);
                float fresnel = pow(saturate(1.0 - dot(normalWS, viewDir)), 3.0);

                Light mainLight = GetMainLight();
                float ndotl = saturate(dot(normalWS, mainLight.direction));
                float spec = pow(saturate(dot(
                    reflect(-mainLight.direction, normalWS),
                    viewDir
                )), 48.0);

                half3 albedo = lerp(_DeepColor.rgb, _ShallowColor.rgb, saturate(wave * 0.35 + 0.5));
                albedo = lerp(albedo, planetMacro, 0.22 * waterMask);
                half3 nightAmbient = half3(0.035, 0.045, 0.065);
                half3 lit = albedo * (mainLight.color * (0.35 + ndotl * 0.65) + nightAmbient);
                lit += _Specular.rgb * spec * 0.65;
                lit += albedo * fresnel * 0.25;

                float alpha = lerp(_DeepColor.a, _ShallowColor.a, fresnel) * saturate(waterMask * 1.4);
                return half4(saturate(lit), saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
