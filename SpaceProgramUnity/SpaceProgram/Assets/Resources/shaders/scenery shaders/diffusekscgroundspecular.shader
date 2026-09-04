Shader "KSP/Scenery/Diffuse Ground KSC Specular" {
	Properties {
		_NearGrassTexture ("Near Grass (RGB)", 2D) = "gray" {}
		_NearGrassNormal ("Near Grass Normal", 2D) = "gray" {}
		_NearGrassTiling ("Near Grass Tiling", Float) = 1
		_MidGrassTexture ("Mid Grass (RGB)", 2D) = "gray" {}
		_MidGrassNormal ("Mid Grass Normal", 2D) = "gray" {}
		_MidGrassTiling ("Mid Grass Tiling", Float) = 1
		_MidGrassBlendDistance ("Mid Grass Blend Distance", Float) = 5000
		_FarGrassTexture ("Far Grass (RGB)", 2D) = "gray" {}
		_FarGrassNormal ("Far Grass Normal", 2D) = "gray" {}
		_FarGrassTiling ("Far Grass Tiling", Float) = 1
		_FarGrassBlendDistance ("Far Grass Blend Distance", Float) = 10000
		_GrassColor ("Grass Color", Vector) = (1,1,1,1)
		_BlendMaskTexture ("Blend Mask (RGB)", 2D) = "gray" {}
		_TarmacTexture ("Tarmac (RGB)", 2D) = "gray" {}
		_TarmacNormal ("Tarmac Normal", 2D) = "gray" {}
		_TarmacColor ("Tarmac Color", Vector) = (1,1,1,1)
		_SpecularColor ("Specular Color", Vector) = (0.5,0.5,0.5,1)
		[PerRendererData] _Opacity ("_Opacity", Range(0, 1)) = 1
		[PerRendererData] _RimFalloff ("_RimFalloff", Range(0.01, 5)) = 0.1
		[PerRendererData] _RimColor ("_RimColor", Vector) = (0,0,0,0)
		[PerRendererData] _UnderwaterFogFactor ("Underwater Fog Factor", Range(0, 1)) = 0
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType" = "Opaque" }
		LOD 200

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;

			struct Vertex_Stage_Input
			{
				float4 pos : POSITION;
			};

			struct Vertex_Stage_Output
			{
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, input.pos));
				return output;
			}

			float4 frag(Vertex_Stage_Output input) : SV_TARGET
			{
				return float4(1.0, 1.0, 1.0, 1.0); // RGBA
			}

			ENDHLSL
		}
	}
	Fallback "Diffuse"
}