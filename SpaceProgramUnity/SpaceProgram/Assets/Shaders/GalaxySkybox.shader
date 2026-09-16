Shader "SpaceProgram/GalaxySkybox"
{
    Properties
    {
        _Tint ("Tint Color", Color) = (0.5, 0.5, 0.5, 0.5)
        [Gamma] _Exposure ("Exposure", Range(0, 8)) = 1.0
        _SkyRotation ("Sky Rotation Quat", Vector) = (0, 0, 0, 1)
        [NoScaleOffset] _FrontTex ("Front [+Z]", 2D) = "grey" {}
        [NoScaleOffset] _BackTex ("Back [-Z]", 2D) = "grey" {}
        [NoScaleOffset] _LeftTex ("Left [+X]", 2D) = "grey" {}
        [NoScaleOffset] _RightTex ("Right [-X]", 2D) = "grey" {}
        [NoScaleOffset] _UpTex ("Up [+Y]", 2D) = "grey" {}
        [NoScaleOffset] _DownTex ("Down [-Y]", 2D) = "grey" {}
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _Tint;
            float _Exposure;
            float4 _SkyRotation;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        float3 RotateByQuat(float3 dir, float4 q)
        {
            float3 t = 2.0 * cross(q.xyz, dir);
            return dir + q.w * t + cross(q.xyz, t);
        }

        Varyings Vert(Attributes input)
        {
            Varyings output;
            float3 rotated = RotateByQuat(input.positionOS.xyz, _SkyRotation);
            output.positionCS = TransformObjectToHClip(rotated);
            output.uv = input.uv;
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "Front"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            TEXTURE2D(_FrontTex);
            SAMPLER(sampler_FrontTex);
            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_FrontTex, sampler_FrontTex, input.uv);
                return half4(tex.rgb * _Tint.rgb * _Exposure, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Back"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            TEXTURE2D(_BackTex);
            SAMPLER(sampler_BackTex);
            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BackTex, sampler_BackTex, input.uv);
                return half4(tex.rgb * _Tint.rgb * _Exposure, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Left"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            TEXTURE2D(_LeftTex);
            SAMPLER(sampler_LeftTex);
            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_LeftTex, sampler_LeftTex, input.uv);
                return half4(tex.rgb * _Tint.rgb * _Exposure, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Right"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            TEXTURE2D(_RightTex);
            SAMPLER(sampler_RightTex);
            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_RightTex, sampler_RightTex, input.uv);
                return half4(tex.rgb * _Tint.rgb * _Exposure, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Up"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            TEXTURE2D(_UpTex);
            SAMPLER(sampler_UpTex);
            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_UpTex, sampler_UpTex, input.uv);
                return half4(tex.rgb * _Tint.rgb * _Exposure, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Down"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            TEXTURE2D(_DownTex);
            SAMPLER(sampler_DownTex);
            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_DownTex, sampler_DownTex, input.uv);
                return half4(tex.rgb * _Tint.rgb * _Exposure, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
