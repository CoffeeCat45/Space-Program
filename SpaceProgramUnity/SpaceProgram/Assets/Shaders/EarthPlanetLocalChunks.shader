Shader "SpaceProgram/EarthPlanetLocalChunks"
{
    Properties
    {
        _PlanetColorMap ("Planet Color (RSS macro)", 2D) = "white" {}
        _SplatMap ("Local ground-noise mask (R=sand G=grass B=snow, tile-scale)", 2D) = "black" {}
        _LowAlbedo ("Low Albedo (sand)", 2D) = "white" {}
        _LowNormal ("Low Normal", 2D) = "bump" {}
        _MidAlbedo ("Mid Albedo (grass)", 2D) = "white" {}
        _MidNormal ("Mid Normal", 2D) = "bump" {}
        _HighAlbedo ("High Albedo (snow)", 2D) = "white" {}
        _HighNormal ("High Normal", 2D) = "bump" {}
        _SteepAlbedo ("Steep Albedo (rock)", 2D) = "white" {}
        _SteepNormal ("Steep Normal", 2D) = "bump" {}
        _PlanetRadius ("Planet Radius", Float) = 637100
        _RealEarthRadius ("Real Earth Radius", Float) = 6371000
        _VisualHeightScale ("Visual Height Scale", Float) = 0.4
        _DetailMetersPerTile ("Detail Meters Per Tile", Float) = 200.0
        _DetailFarMetersPerTile ("Detail Far Meters Per Tile", Float) = 800.0
        _DetailNearFadeMeters ("Detail Near Fade Meters", Float) = 300.0
        _DetailFarFadeMeters ("Detail Far Fade Meters", Float) = 4200.0
        _SurfaceTextureScale ("Surface Texture Scale", Float) = 1.0
        _LowAltitudeStart ("Low Altitude Start", Float) = 0.0
        _LowAltitudeEnd ("Low Altitude End", Float) = 80.0
        _HighAltitudeStart ("High Altitude Start", Float) = 2200.0
        _HighAltitudeEnd ("High Altitude End", Float) = 2800.0
        _SteepPower ("Steep Power", Float) = 4.0
        _SteepContrast ("Steep Contrast", Float) = 3.5
        _SteepMidpoint ("Steep Midpoint", Float) = 0.55
        _SteepSlopeThreshold ("Steep Slope Threshold", Range(0, 1)) = 0.48
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1.0
        _DetailActive ("Detail Active", Float) = 1.0
        _SurfaceBlendMode ("Surface Blend Mode (0=Earth biome 1=height-slope)", Float) = 0.0
        _MacroBlendStart ("Macro Blend Start", Float) = 22000.0
        _MacroBlendEnd ("Macro Blend End", Float) = 100000.0
        _MacroBlendMax ("Macro Blend Max", Float) = 0.16
        _GroundAmbientFloor ("Ground Ambient Floor", Float) = 0.08
        _SeaLevelHeightMeters ("Sea Level Height Offset", Float) = 0.0
        _ShallowWaterDepthMeters ("Shallow Water Depth", Float) = 12.0
        _WaveNormal ("Wave Normal", 2D) = "bump" {}
        _WaveMetersPerTile ("Wave Meters Per Tile", Float) = 520.0
        _WaveSpeed ("Wave Speed", Float) = 0.62
        _WaveStrength ("Wave Strength", Range(0, 2)) = 1.0
        _WaveHeightMeters ("Wave Height Meters", Float) = 12.0
        _HasOcean ("Has Ocean (0=airless, skip swell)", Float) = 1
        _OceanColor ("Ocean Color", Color) = (0.02, 0.10, 0.20, 1)
        _OceanSatelliteBlend ("Ocean Satellite Blend", Range(0, 1)) = 0.78
        _OceanSpecular ("Ocean Specular", Range(0, 2)) = 0.38
        _OceanDepthPackMeters ("Ocean Depth Pack Meters", Float) = 2500.0
        _FallbackColor ("Fallback Color", Color) = (0.28, 0.48, 0.22, 1.0)
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
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "PlanetSpinCommon.hlsl"

            TEXTURE2D(_PlanetColorMap);
            SAMPLER(sampler_PlanetColorMap);
            TEXTURE2D(_SplatMap);
            SAMPLER(sampler_SplatMap);
            TEXTURE2D(_LowAlbedo);
            SAMPLER(sampler_LowAlbedo);
            TEXTURE2D(_LowNormal);
            SAMPLER(sampler_LowNormal);
            TEXTURE2D(_MidAlbedo);
            SAMPLER(sampler_MidAlbedo);
            TEXTURE2D(_MidNormal);
            SAMPLER(sampler_MidNormal);
            TEXTURE2D(_HighAlbedo);
            SAMPLER(sampler_HighAlbedo);
            TEXTURE2D(_HighNormal);
            SAMPLER(sampler_HighNormal);
            TEXTURE2D(_SteepAlbedo);
            SAMPLER(sampler_SteepAlbedo);
            TEXTURE2D(_SteepNormal);
            SAMPLER(sampler_SteepNormal);
            TEXTURE2D(_WaveNormal);
            SAMPLER(sampler_WaveNormal);

            CBUFFER_START(UnityPerMaterial)
                float4 _FallbackColor;
                float _PlanetRadius;
                float _RealEarthRadius;
                float _VisualHeightScale;
                float _DetailMetersPerTile;
                float _DetailFarMetersPerTile;
                float _DetailNearFadeMeters;
                float _DetailFarFadeMeters;
                float _SurfaceTextureScale;
                float _LowAltitudeStart;
                float _LowAltitudeEnd;
                float _HighAltitudeStart;
                float _HighAltitudeEnd;
                float _SteepPower;
                float _SteepContrast;
                float _SteepMidpoint;
                float _SteepSlopeThreshold;
                float _NormalStrength;
                float _DetailActive;
                float _SurfaceBlendMode;
                float _MacroBlendStart;
                float _MacroBlendEnd;
                float _MacroBlendMax;
                float _GroundAmbientFloor;
                float _SeaLevelHeightMeters;
                float _ShallowWaterDepthMeters;
                float _WaveMetersPerTile;
                float _WaveSpeed;
                float _WaveStrength;
                float _WaveHeightMeters;
                float _HasOcean;
                float4 _OceanColor;
                float _OceanSatelliteBlend;
                float _OceanSpecular;
                float _OceanDepthPackMeters;
                float _ColorMapFlipLongitude;
                float _ColorMapFlipLatitude;
                float _PlanetSpinLongitudeRadians;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 latLongUv : TEXCOORD0;
                // Real surface meters (lon*radius*cosLat, lat*radius), wrapped on the CPU
                // in double precision so this stays small enough for float32 to keep
                // sub-centimeter precision. See PlanetFlightDetailUv.ComputeWrappedDetailMetersUv.
                float2 localMetersUv : TEXCOORD1;
                float4 vertexColor : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 latLongUv : TEXCOORD2;
                float2 localMetersUv : TEXCOORD3;
                float terrainHeightMeters : TEXCOORD4;
                float4 vertexColor : COLOR;
            };

            void ComputeCorrectedGradients(float2 uv, out float2 dx, out float2 dy)
            {
                dx = ddx(uv);
                dy = ddy(uv);

                if (dx.x > 0.5) dx.x -= 1.0;
                else if (dx.x < -0.5) dx.x += 1.0;

                if (dy.x > 0.5) dy.x -= 1.0;
                else if (dy.x < -0.5) dy.x += 1.0;
            }

            float3 UnpackNormalDxt5nm(float4 packedNormal)
            {
                float2 normalXY = packedNormal.ag * 2.0 - 1.0;
                float normalZ = sqrt(1.0 - saturate(dot(normalXY, normalXY)));
                return float3(normalXY.x, normalXY.y, normalZ);
            }

            half3 SampleEquirectAlbedo(TEXTURE2D_PARAM(textureMap, samplerState), float2 uv)
            {
                float2 dx;
                float2 dy;
                ComputeCorrectedGradients(uv, dx, dy);
                return SAMPLE_TEXTURE2D_GRAD(textureMap, samplerState, uv, dx, dy).rgb;
            }

            // Earth_Influence.dds is a small tileable ground-noise mask (same authoring scale
            // as the Low/Mid/High/Steep detail albedos, painted with grass/twig-litter detail),
            // NOT a world biome atlas. Sampling it with the global lat/long UV stretched one
            // tiny tile across the whole planet, so the GPU picked the texture's lowest mip
            // (its flat average color) almost everywhere — a uniform, washed-out blend that
            // looked like "the same texture everywhere". Sample it at the same local tile UV
            // as the albedo maps so it actually varies from patch to patch on the ground.
            float4 SampleDetailSplat(float2 detailUv, float2 dx, float2 dy)
            {
                return SAMPLE_TEXTURE2D_GRAD(_SplatMap, sampler_SplatMap, detailUv, dx, dy);
            }

            // Distances in PlanetBiomeTerrainConfig are real Earth meters. Scale by
            // UnityRadius / RealEarth so 1:10 now and 1:1 later keep the same geographic
            // tile size (RSS PQS ≈ 200 real-m/tile).
            float WorldHorizontalScale()
            {
                return max(_PlanetRadius, 1.0) / max(_RealEarthRadius, 1.0);
            }

            float2 BuildDetailTileUv(float2 localMetersUv)
            {
                float metersPerTile = max(_DetailMetersPerTile, 0.001) * WorldHorizontalScale();
                float scale = max(_SurfaceTextureScale, 0.05);
                return localMetersUv / metersPerTile / scale;
            }

            void CorrectPeriodicUvGradients(float wrapPeriod, inout float2 dx, inout float2 dy)
            {
                float halfWrap = wrapPeriod * 0.5;

                if (dx.x > halfWrap) dx.x -= wrapPeriod;
                else if (dx.x < -halfWrap) dx.x += wrapPeriod;
                if (dx.y > halfWrap) dx.y -= wrapPeriod;
                else if (dx.y < -halfWrap) dx.y += wrapPeriod;

                if (dy.x > halfWrap) dy.x -= wrapPeriod;
                else if (dy.x < -halfWrap) dy.x += wrapPeriod;
                if (dy.y > halfWrap) dy.y -= wrapPeriod;
                else if (dy.y < -halfWrap) dy.y += wrapPeriod;
            }

            float DetailUvWrapPeriod()
            {
                float metersPerTile = max(_DetailMetersPerTile, 0.001) * WorldHorizontalScale();
                float scale = max(_SurfaceTextureScale, 0.05);
                return 16384.0 / max(metersPerTile * scale, 0.001);
            }

            float3 ComputeHeightSlopeLayerWeights(float realElevationMeters)
            {
                float lowEnd = max(_LowAltitudeEnd, _LowAltitudeStart + 0.001);
                float highEnd = max(_HighAltitudeEnd, _HighAltitudeStart + 0.001);
                float lowWeight = 1.0 - smoothstep(_LowAltitudeStart, lowEnd, realElevationMeters);
                float highWeight = smoothstep(_HighAltitudeStart, highEnd, realElevationMeters);
                float midWeight = saturate(1.0 - lowWeight - highWeight);
                float3 weights = float3(lowWeight, midWeight, highWeight);
                float weightSum = max(weights.x + weights.y + weights.z, 0.001);
                return weights / weightSum;
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

            float2 HashCell(float2 cell)
            {
                float h = frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453123);
                return float2(h, frac(h * 91.7));
            }

            float2 RotateAroundCenter(float2 uv, float angleRadians)
            {
                float s = sin(angleRadians);
                float c = cos(angleRadians);
                float2x2 rotation = float2x2(c, -s, s, c);
                return mul(rotation, uv - 0.5) + 0.5;
            }

            /// <summary>
            /// Breaks up visible long-range tiling ("basket weave" stripes). One rotated
            /// sample still leaves a square cell grid (obvious on flat snow). Two scales
            /// blended by a slower noise field hide the seams and the repeating motif.
            /// </summary>
            float2 StochasticTileUv(float2 uv)
            {
                float2 cell = floor(uv);
                float2 cellUv = frac(uv);
                float2 hashValue = HashCell(cell);
                float angle = hashValue.x * 6.2831853;
                cellUv = RotateAroundCenter(cellUv, angle);
                return cell + frac(cellUv + hashValue.y * 7.0);
            }

            void BuildDecorrelatedTileUvs(
                float2 uv,
                out float2 uvA,
                out float2 uvB,
                out float blend,
                out float uvBScale
            )
            {
                // Lon/lat meter UV is a square lattice; rotate it so tiles are not
                // in screen-aligned rows (very visible on flat snow like Tibet).
                float2x2 latticeRot = float2x2(0.8775826, -0.4794255, 0.4794255, 0.8775826);
                float2 rotated = mul(latticeRot, uv);
                uvBScale = 1.31;
                uvA = StochasticTileUv(rotated);
                uvB = StochasticTileUv(rotated * uvBScale + float2(17.8, 9.1));

                float2 noiseUv = rotated * 0.37;
                float2 noiseCell = floor(noiseUv);
                float2 f = smoothstep(0.0, 1.0, frac(noiseUv));
                float h00 = HashCell(noiseCell).x;
                float h10 = HashCell(noiseCell + float2(1.0, 0.0)).x;
                float h01 = HashCell(noiseCell + float2(0.0, 1.0)).x;
                float h11 = HashCell(noiseCell + float2(1.0, 1.0)).x;
                blend = lerp(lerp(h00, h10, f.x), lerp(h01, h11, f.x), f.y);
            }

            half3 SampleDetailAlbedo(
                TEXTURE2D_PARAM(textureMap, samplerState),
                float2 detailUv,
                float2 dx,
                float2 dy
            )
            {
                float2 uvA;
                float2 uvB;
                float blend;
                float uvBScale;
                BuildDecorrelatedTileUvs(detailUv, uvA, uvB, blend, uvBScale);
                half3 sampleA = SAMPLE_TEXTURE2D_GRAD(
                    textureMap,
                    samplerState,
                    uvA,
                    dx,
                    dy
                ).rgb;
                half3 sampleB = SAMPLE_TEXTURE2D_GRAD(
                    textureMap,
                    samplerState,
                    uvB,
                    dx * uvBScale,
                    dy * uvBScale
                ).rgb;
                return lerp(sampleA, sampleB, blend);
            }

            float3 SampleDetailNormal(
                TEXTURE2D_PARAM(textureMap, samplerState),
                float2 detailUv,
                float2 dx,
                float2 dy
            )
            {
                float2 uvA;
                float2 uvB;
                float blend;
                float uvBScale;
                BuildDecorrelatedTileUvs(detailUv, uvA, uvB, blend, uvBScale);
                float3 sampleA = UnpackNormalDxt5nm(
                    SAMPLE_TEXTURE2D_GRAD(textureMap, samplerState, uvA, dx, dy)
                );
                float3 sampleB = UnpackNormalDxt5nm(
                    SAMPLE_TEXTURE2D_GRAD(
                        textureMap,
                        samplerState,
                        uvB,
                        dx * uvBScale,
                        dy * uvBScale
                    )
                );
                return normalize(lerp(sampleA, sampleB, blend));
            }

            /// <summary>
            /// NASA photo → sand / grass. Snow from color only for ice (cold + desaturated).
            /// Deserts are warm tan (R ≥ G, not green). Bright Sahara must not become snow.
            /// </summary>
            float3 ComputeMacroBiomeWeights(half3 macroColor)
            {
                float3 c = saturate((float3)macroColor);
                float luma = dot(c, float3(0.299, 0.587, 0.114));
                float maxC = max(c.r, max(c.g, c.b));
                float minC = min(c.r, min(c.g, c.b));
                float saturation = maxC > 1e-4 ? (maxC - minC) / maxC : 0.0;

                float grass = saturate((c.g - c.r) * 6.0 + 0.28);
                grass *= saturate((c.g - c.b) * 2.2 + 0.40);

                float warm = saturate((c.r - c.g) * 4.5 + 0.12);
                float dryBright = saturate((luma - 0.30) * 2.4) * saturate(0.58 - c.g);
                float sand = saturate(warm + dryBright * saturate(1.0 - grass));
                sand *= saturate(1.0 - grass);

                float snow = saturate(luma - 0.72) *
                    saturate(0.22 - saturation) *
                    saturate(c.b - c.r + 0.08);
                snow *= saturate(1.0 - sand);

                float total = sand + grass + snow;
                if (total < 0.12)
                {
                    sand = saturate((c.r - c.g) * 8.0 + 0.28);
                    grass = 1.0 - sand;
                    snow = 0.0;
                    total = 1.0;
                }

                return float3(sand, grass, snow) / max(total, 0.001);
            }

            float OceanLikeness(half3 macroColor)
            {
                float3 c = saturate((float3)macroColor);
                float luma = dot(c, float3(0.299, 0.587, 0.114));
                float blueExcess = saturate(c.b - max(c.r, c.g) * 0.95);
                return saturate(blueExcess * 5.0) * saturate((0.40 - luma) * 4.0);
            }

            /// <summary>
            /// Land next to a sea/ocean. Distant UV probes (~90 km) miss ponds and most lakes.
            /// A coarse mip is extra signal when the color map has mipmaps.
            /// </summary>
            float OceanProximity(float2 splatUv, half3 localMacro)
            {
                const float oceanProbeUv = 0.0024;
                half3 wide = SAMPLE_TEXTURE2D_LOD(
                    _PlanetColorMap,
                    sampler_PlanetColorMap,
                    splatUv,
                    5.0
                ).rgb;
                half3 north = SAMPLE_TEXTURE2D_LOD(
                    _PlanetColorMap,
                    sampler_PlanetColorMap,
                    splatUv + float2(0.0, oceanProbeUv),
                    3.0
                ).rgb;
                half3 south = SAMPLE_TEXTURE2D_LOD(
                    _PlanetColorMap,
                    sampler_PlanetColorMap,
                    splatUv + float2(0.0, -oceanProbeUv),
                    3.0
                ).rgb;
                half3 east = SAMPLE_TEXTURE2D_LOD(
                    _PlanetColorMap,
                    sampler_PlanetColorMap,
                    splatUv + float2(oceanProbeUv, 0.0),
                    3.0
                ).rgb;
                half3 west = SAMPLE_TEXTURE2D_LOD(
                    _PlanetColorMap,
                    sampler_PlanetColorMap,
                    splatUv + float2(-oceanProbeUv, 0.0),
                    3.0
                ).rgb;
                float wideOcean = max(
                    OceanLikeness(wide),
                    (
                        OceanLikeness(north) +
                        OceanLikeness(south) +
                        OceanLikeness(east) +
                        OceanLikeness(west)
                    ) * 0.25
                );
                float localOcean = OceanLikeness(localMacro);
                return saturate(wideOcean * 2.2) * saturate(1.0 - localOcean * 1.5);
            }

            void BuildSphereTangentFrameWS(
                float3 normalWS,
                out float3 tangentWS,
                out float3 bitangentWS
            )
            {
                float3 referenceUp = abs(normalWS.y) > 0.95 ?
                    float3(1.0, 0.0, 0.0) :
                    float3(0.0, 1.0, 0.0);
                tangentWS = normalize(cross(referenceUp, normalWS));
                bitangentWS = normalize(cross(normalWS, tangentWS));
            }

            float CloudLikeness(half3 color)
            {
                float luma = dot((float3)color, float3(0.299, 0.587, 0.114));
                float saturation = max(color.r, max(color.g, color.b)) -
                    min(color.r, min(color.g, color.b));
                return saturate((luma - 0.42) * 4.2) * saturate(1.0 - saturation * 3.4);
            }

            half3 SampleOceanAlbedo(
                float2 splatUv,
                half3 planetMacro,
                float viewerAltitude,
                float depthMeters
            )
            {
                half3 coarse = SAMPLE_TEXTURE2D_LOD(
                    _PlanetColorMap,
                    sampler_PlanetColorMap,
                    splatUv,
                    4.25
                ).rgb;
                float cloudLocal = CloudLikeness(planetMacro);
                half3 photo = lerp(planetMacro, coarse, cloudLocal);
                photo = lerp(photo, _OceanColor.rgb, CloudLikeness(photo));

                float luma = dot((float3)photo, float3(0.299, 0.587, 0.114));
                half3 hue = photo / max(luma, 0.03);
                half3 nasaWater = hue * lerp(0.03, 0.20, saturate(luma * 2.15));
                nasaWater = lerp(_OceanColor.rgb, nasaWater, _OceanSatelliteBlend);

                float shelf = 1.0 - smoothstep(5.0, 55.0, depthMeters);
                float slope = 1.0 - smoothstep(55.0, 280.0, depthMeters);
                half3 turquoise = lerp(
                    half3(0.06, 0.26, 0.26),
                    nasaWater * half3(0.80, 1.18, 1.08),
                    0.5
                );
                half3 waterAlbedo = lerp(
                    _OceanColor.rgb,
                    nasaWater,
                    lerp(0.28, 1.0, slope)
                );
                waterAlbedo = lerp(waterAlbedo, turquoise, shelf);

                float realAltitude = viewerAltitude / max(WorldHorizontalScale(), 0.01);
                float orbitBlend = smoothstep(16000.0, 85000.0, realAltitude);
                half3 orbitAlbedo = lerp(waterAlbedo, photo * 0.52, 0.72);
                return lerp(waterAlbedo, orbitAlbedo, orbitBlend);
            }

            float2 Rotate2(float2 value, float radians)
            {
                float s;
                float c;
                sincos(radians, s, c);
                return float2(c * value.x - s * value.y, s * value.x + c * value.y);
            }

            float2 OceanWindDir(float2 splatUv)
            {
                float lat = (splatUv.y - 0.5) * 3.14159265;
                float lon = splatUv.x * 6.2831853;
                float absLat = abs(lat);
                float westerly = smoothstep(0.28, 0.72, absLat);
                float angle = lerp(3.02, 0.20, westerly);
                angle += sin(lon * 2.0 + lat * 1.7) * 0.42;
                angle += sin(lon * 5.0 - lat * 0.8) * 0.14;
                float2 dir = float2(cos(angle), sin(angle));
                dir.y *= lat < 0.0 ? -1.0 : 1.0;
                return normalize(dir);
            }

            float OceanDistanceFade(float3 positionWS)
            {
                float dist = length(_WorldSpaceCameraPos - positionWS);
                return 1.0 - smoothstep(350.0, 2600.0, dist);
            }

            void EvalOceanSwell(
                float2 localMetersUv,
                float2 splatUv,
                float depthMeters,
                float fade,
                out float height,
                out float3 tangentNormal
            )
            {
                float worldScale = WorldHorizontalScale();
                float absLat = abs((splatUv.y - 0.5) * 3.14159265);
                float deep = saturate(depthMeters / 380.0);
                float lambda = max(_WaveMetersPerTile, 120.0) * worldScale *
                    lerp(0.85, 1.40, saturate(absLat / 0.85)) *
                    lerp(0.80, 1.12, deep);
                float2 wind = OceanWindDir(splatUv);
                float2 crossWind = Rotate2(wind, 0.30);
                float time = _Time.y * _WaveSpeed;
                float k1 = 6.28318530718 / lambda;
                float k2 = 6.28318530718 / (lambda * 1.75);
                float phase1 = dot(wind, localMetersUv) * k1 - time * 0.82;
                float phase2 = dot(crossWind, localMetersUv) * k2 - time * 0.54;
                float latAmp = lerp(0.50, 1.0, smoothstep(0.22, 0.78, absLat));
                float depthAmp = lerp(0.48, 1.0, deep);
                float amplitude = _WaveHeightMeters * worldScale * _WaveStrength * latAmp * depthAmp * fade;
                height = (sin(phase1) * 0.88 + sin(phase2) * 0.12) * amplitude;
                float normalAmp = _WaveStrength * fade * latAmp * depthAmp;
                tangentNormal = float3(0.0, 0.0, 1.0);
                tangentNormal.xy -= wind * (0.22 * normalAmp * cos(phase1));
                tangentNormal.xy -= crossWind * (0.07 * normalAmp * cos(phase2));
                tangentNormal = normalize(tangentNormal);
            }

            float3 DisplaceOceanVertex(
                float3 positionOS,
                float2 localMetersUv,
                float2 splatUv,
                float waterMask,
                float depthMeters
            )
            {
                float3 radial = normalize(positionOS);
                float3 positionWS = TransformObjectToWorld(positionOS);
                float fade = OceanDistanceFade(positionWS);
                float height;
                float3 tangentNormal;
                EvalOceanSwell(
                    localMetersUv,
                    splatUv,
                    depthMeters,
                    fade * waterMask,
                    height,
                    tangentNormal
                );
                return positionOS + radial * height;
            }

            float3 SamplePlanetOceanNormal(
                float2 localMetersUv,
                float2 splatUv,
                float3 positionWS,
                float depthMeters
            )
            {
                float worldScale = WorldHorizontalScale();
                float lambda = max(_WaveMetersPerTile, 120.0) * worldScale;
                float pixelMeters = max(fwidth(localMetersUv).x, fwidth(localMetersUv).y);
                float lodFade = 1.0 - smoothstep(lambda * 0.14, lambda * 0.62, pixelMeters);
                float fade = OceanDistanceFade(positionWS) * lodFade;
                if (fade < 0.01)
                {
                    return float3(0.0, 0.0, 1.0);
                }

                float height;
                float3 tangentNormal;
                EvalOceanSwell(
                    localMetersUv,
                    splatUv,
                    depthMeters,
                    fade,
                    height,
                    tangentNormal
                );
                return tangentNormal;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 originalOS = input.positionOS.xyz;
                float3 displacedOS = originalOS;
                if (_HasOcean > 0.5)
                {
                    float oceanDepthMeters = input.vertexColor.a *
                        input.vertexColor.g *
                        _OceanDepthPackMeters;
                    displacedOS = DisplaceOceanVertex(
                        originalOS,
                        input.localMetersUv,
                        input.latLongUv,
                        input.vertexColor.a,
                        oceanDepthMeters
                    );
                }
                VertexPositionInputs positionInputs = GetVertexPositionInputs(displacedOS);
                output.positionCS = positionInputs.positionCS;
                output.positionOS = displacedOS;
                output.normalWS = normalize(TransformObjectToWorldDir(input.normalOS));
                output.latLongUv = input.latLongUv;
                output.localMetersUv = input.localMetersUv;
                output.terrainHeightMeters = length(originalOS) - _PlanetRadius - _SeaLevelHeightMeters;
                output.vertexColor = input.vertexColor;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 radialOS = normalize(input.positionOS);
                float3 normalWS = normalize(input.normalWS);
                float terrainHeight = input.terrainHeightMeters;
                float2 splatUv = ApplyPlanetSpinToWrappedGeographicUv(
                    input.latLongUv,
                    _PlanetSpinLongitudeRadians
                );
                float altitudeAboveSurface = max(0.0, terrainHeight);
                float realElevationMeters = terrainHeight / max(_VisualHeightScale, 0.05);
                float gebcoMeters = altitudeAboveSurface / max(_VisualHeightScale, 0.05);

                float3 planetOriginWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float viewerAltitude = max(
                    0.0,
                    length(_WorldSpaceCameraPos - planetOriginWS) - _PlanetRadius
                );
                bool heightSlopeSplat = _SurfaceBlendMode > 0.5;
                float worldScale = WorldHorizontalScale();
                // Hardcoded — do not add a UnityPerMaterial field (Editor crash / white screen).
                const float moonMacroUnifyAltitudeSimMeters = 8000.0;
                float macroDriver = viewerAltitude;
                if (heightSlopeSplat)
                {
                    // Pad: fragment distance so the horizon is LROC, not gray splat.
                    // In flight an iso-distance mix is a growing disk under the ship.
                    if (viewerAltitude < moonMacroUnifyAltitudeSimMeters)
                    {
                        float3 fragmentWS = TransformObjectToWorld(input.positionOS);
                        macroDriver = length(_WorldSpaceCameraPos - fragmentWS);
                    }
                }

                float macroBlend = smoothstep(
                    _MacroBlendStart * worldScale,
                    _MacroBlendEnd * worldScale,
                    macroDriver
                ) * _MacroBlendMax;
                // Moon limb / high hop: LROC already dominates. Skip dual-sample
                // splat (16+ taps) so the 32–60 km chunk disk does not tank GPU.
                bool skipHeavyDetail = heightSlopeSplat &&
                    (macroBlend > 0.55 || viewerAltitude > 15000.0);

                half3 planetMacro = SampleEquirectAlbedo(
                    TEXTURE2D_ARGS(_PlanetColorMap, sampler_PlanetColorMap),
                    splatUv
                );
                half3 planetMacroBiome = planetMacro;
                if (!heightSlopeSplat)
                {
                    // Biome from a coarser mip so NASA texels (~2 km) do not show as squares
                    // on the ground. Overlay color above still uses the sharp sample.
                    planetMacroBiome = SAMPLE_TEXTURE2D_LOD(
                        _PlanetColorMap,
                        sampler_PlanetColorMap,
                        splatUv,
                        2.5
                    ).rgb;
                }

                float2 detailUv = BuildDetailTileUv(input.localMetersUv);
                float2 detailDx = ddx(detailUv);
                float2 detailDy = ddy(detailUv);
                CorrectPeriodicUvGradients(DetailUvWrapPeriod(), detailDx, detailDy);
                float3 layerWeights = float3(0.0, 1.0, 0.0);
                float rockWeight = 0.0;
                half3 detailAlbedo = planetMacro;
                float waterMask = 0.0;

                if (!skipHeavyDetail)
                {
                half3 lowDetail = SampleDetailAlbedo(
                    TEXTURE2D_ARGS(_LowAlbedo, sampler_LowAlbedo),
                    detailUv,
                    detailDx,
                    detailDy
                );
                half3 midDetail = SampleDetailAlbedo(
                    TEXTURE2D_ARGS(_MidAlbedo, sampler_MidAlbedo),
                    detailUv,
                    detailDx,
                    detailDy
                );
                half3 highDetail = SampleDetailAlbedo(
                    TEXTURE2D_ARGS(_HighAlbedo, sampler_HighAlbedo),
                    detailUv,
                    detailDx,
                    detailDy
                );
                half3 rockDetail = SampleDetailAlbedo(
                    TEXTURE2D_ARGS(_SteepAlbedo, sampler_SteepAlbedo),
                    detailUv,
                    detailDx,
                    detailDy
                );

                float4 splat = SampleDetailSplat(detailUv, detailDx, detailDy);
                float splatSum = splat.r + splat.g + splat.b;
                if (splatSum <= 0.001)
                {
                    splat.rgb = heightSlopeSplat ? float3(0.0, 1.0, 0.0) : float3(0.2, 0.7, 0.1);
                    splatSum = 1.0;
                }

                splat.rgb /= splatSum;

                float surfaceUpDot = dot(normalWS, TransformObjectToWorldDir(radialOS));
                float slope = 1.0 - saturate(surfaceUpDot);
                float steepSignal = pow(saturate(slope), max(_SteepPower, 0.001));
                steepSignal = saturate((steepSignal - _SteepMidpoint) * _SteepContrast + 0.5);
                float steepWeight = smoothstep(
                    _SteepSlopeThreshold - 0.12,
                    _SteepSlopeThreshold + 0.08,
                    steepSignal
                );

                if (heightSlopeSplat)
                {
                    layerWeights = ComputeHeightSlopeLayerWeights(realElevationMeters);
                    if (splatSum > 0.05)
                    {
                        layerWeights = lerp(layerWeights, splat.rgb, 0.08);
                        float heightSlopeSum = max(
                            layerWeights.x + layerWeights.y + layerWeights.z,
                            0.001
                        );
                        layerWeights /= heightSlopeSum;
                    }

                    detailAlbedo = lowDetail * layerWeights.x +
                        midDetail * layerWeights.y +
                        highDetail * layerWeights.z;
                    rockWeight = steepWeight;
                    detailAlbedo = lerp(detailAlbedo, rockDetail, rockWeight);
                }
                else
                {
                    float3 macroWeights = ComputeMacroBiomeWeights(planetMacroBiome);
                    float gebcoJitter = (HashCell(floor(splatUv * 2048.0)).x - 0.5) * 180.0;
                    float gebcoBlend = max(0.0, gebcoMeters + gebcoJitter);
                    float latRadians = (splatUv.y - 0.5) * 3.14159265;
                    float absLat = abs(latRadians);
                    float polar = saturate((absLat - 1.047) / 0.40);
                    float cosLat = cos(absLat);
                    float snowlineMeters = 400.0 + 4600.0 * cosLat * cosLat;
                    snowlineMeters = lerp(snowlineMeters, 40.0, polar);

                    float mountainShape = saturate((slope - 0.035) * 10.0) *
                        saturate((0.45 - slope) * 3.5);
                    float aboveSnowline = smoothstep(
                        snowlineMeters - 200.0,
                        snowlineMeters + 500.0,
                        gebcoBlend
                    );
                    float peakSnow = aboveSnowline * mountainShape;
                    float iceCap = smoothstep(
                        snowlineMeters + 1100.0,
                        snowlineMeters + 2100.0,
                        gebcoBlend
                    );

                    float desert = macroWeights.x;
                    float snow = max(max(peakSnow, iceCap), polar * 0.9);
                    snow = max(snow, macroWeights.z * saturate(polar + peakSnow + iceCap));
                    snow *= saturate(1.0 - desert * (1.0 - polar));
                    snow = saturate(snow);

                    float oceanNear = OceanProximity(splatUv, planetMacro);
                    float coastHeight = 1.0 - smoothstep(20.0, 90.0, gebcoBlend);
                    float coastSand = coastHeight * oceanNear * saturate(1.0 - polar);
                    float inlandSand = desert * (1.0 - smoothstep(2500.0, 4000.0, gebcoBlend));
                    float sand = saturate(max(inlandSand, coastSand) * (1.0 - snow));

                    float highland = smoothstep(700.0, 2000.0, gebcoBlend);
                    float grass = macroWeights.y * (1.0 - snow) * (1.0 - sand * 0.7);
                    grass *= lerp(1.0, 0.22, highland * (1.0 - snow) * (1.0 - sand));

                    layerWeights = float3(sand, grass, snow);
                    layerWeights = lerp(layerWeights, splat.rgb, 0.06);
                    layerWeights = max(layerWeights, 0.0);
                    float layerSum = max(layerWeights.x + layerWeights.y + layerWeights.z, 0.001);
                    layerWeights /= layerSum;

                    detailAlbedo = lowDetail * layerWeights.x +
                        midDetail * layerWeights.y +
                        highDetail * layerWeights.z;

                    float highlandRock = highland *
                        saturate(1.0 - layerWeights.z) *
                        saturate(1.0 - sand) *
                        lerp(0.38, 1.0, steepWeight);
                    rockWeight = saturate(steepWeight + highlandRock);
                    detailAlbedo = lerp(detailAlbedo, rockDetail, rockWeight);

                    float shoreNoise = HashCell(floor(detailUv * 3.0)).x - 0.5;
                    float shoreJitterMeters = shoreNoise * _ShallowWaterDepthMeters * 0.35;
                    float shoreBlend = smoothstep(
                        -_ShallowWaterDepthMeters * 0.5,
                        _ShallowWaterDepthMeters * 0.65,
                        terrainHeight + shoreJitterMeters
                    );
                    half3 wetShoreAlbedo = lerp(
                        detailAlbedo * half3(0.58, 0.68, 0.78),
                        detailAlbedo,
                        shoreBlend
                    );
                    detailAlbedo = lerp(detailAlbedo, wetShoreAlbedo, saturate(1.0 - shoreBlend + 0.15));

                    float lakeWater = OceanLikeness(planetMacro) *
                        saturate((gebcoMeters + 80.0) / 90.0);
                    waterMask = _HasOcean > 0.5 ?
                        saturate(max(input.vertexColor.a, lakeWater)) :
                        0.0;

                    float macroOcean = OceanLikeness(planetMacro);
                    float coastFixBlend = smoothstep(
                        22000.0 * worldScale,
                        42000.0 * worldScale,
                        viewerAltitude
                    );
                    waterMask = saturate(max(waterMask, macroOcean * coastFixBlend));
                }
                }

                half3 landAlbedo = lerp(
                    detailAlbedo,
                    planetMacro,
                    macroBlend * (1.0 - waterMask)
                );

                if (dot(landAlbedo, landAlbedo) <= 0.0001)
                {
                    landAlbedo = _FallbackColor.rgb;
                }

                landAlbedo *= lerp(input.vertexColor.rrr, half3(1.0, 1.0, 1.0), waterMask);

                float oceanDepthMeters = input.vertexColor.a > 0.5 ?
                    input.vertexColor.g * _OceanDepthPackMeters :
                    0.0;
                half3 finalAlbedo = landAlbedo;
                if (waterMask > 0.001)
                {
                    half3 oceanAlbedo = SampleOceanAlbedo(
                        splatUv,
                        planetMacro,
                        viewerAltitude,
                        oceanDepthMeters
                    );
                    finalAlbedo = lerp(landAlbedo, oceanAlbedo, waterMask);
                }

                float3 tangentNormal = float3(0.0, 0.0, 1.0);

                if (!skipHeavyDetail && _NormalStrength > 0.001)
                {
                    float3 lowNormal = SampleDetailNormal(
                        TEXTURE2D_ARGS(_LowNormal, sampler_LowNormal),
                        detailUv,
                        detailDx,
                        detailDy
                    );
                    float3 midNormal = SampleDetailNormal(
                        TEXTURE2D_ARGS(_MidNormal, sampler_MidNormal),
                        detailUv,
                        detailDx,
                        detailDy
                    );
                    float3 highNormal = SampleDetailNormal(
                        TEXTURE2D_ARGS(_HighNormal, sampler_HighNormal),
                        detailUv,
                        detailDx,
                        detailDy
                    );
                    float3 rockNormal = SampleDetailNormal(
                        TEXTURE2D_ARGS(_SteepNormal, sampler_SteepNormal),
                        detailUv,
                        detailDx,
                        detailDy
                    );

                    float3 biomeNormal = normalize(
                        lowNormal * layerWeights.x +
                        midNormal * layerWeights.y +
                        highNormal * layerWeights.z
                    );
                    tangentNormal = biomeNormal;
                    tangentNormal = normalize(lerp(tangentNormal, rockNormal, rockWeight));
                    tangentNormal = normalize(
                        lerp(float3(0.0, 0.0, 1.0), tangentNormal, _NormalStrength)
                    );
                }

                if (waterMask > 0.001)
                {
                    tangentNormal = normalize(
                        lerp(
                            tangentNormal,
                            SamplePlanetOceanNormal(
                                input.localMetersUv,
                                splatUv,
                                TransformObjectToWorld(input.positionOS),
                                oceanDepthMeters
                            ),
                            waterMask
                        )
                    );
                }

                float3 tangentWS;
                float3 bitangentWS;
                BuildSphereTangentFrameWS(normalWS, tangentWS, bitangentWS);
                normalWS = normalize(
                    tangentWS * tangentNormal.x +
                    bitangentWS * tangentNormal.y +
                    normalWS * tangentNormal.z
                );

                float3 positionWS = TransformObjectToWorld(input.positionOS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));
                float ndotl = saturate(dot(normalWS, mainLight.direction));
                ndotl = lerp(ndotl, ndotl * 0.78 + 0.12, waterMask);
                half3 ambient = max(SampleSH(normalWS), half3(_GroundAmbientFloor, _GroundAmbientFloor, _GroundAmbientFloor));
                half3 lit = finalAlbedo * (mainLight.color * ndotl + ambient);
                lit = max(lit, finalAlbedo * _GroundAmbientFloor);

                float3 viewDir = normalize(_WorldSpaceCameraPos - positionWS);
                float spec = pow(saturate(dot(reflect(-mainLight.direction, normalWS), viewDir)), 18.0);
                float fresnel = pow(saturate(1.0 - dot(normalWS, viewDir)), 5.0);
                lit += mainLight.color * spec * _OceanSpecular * waterMask;
                lit += (mainLight.color * 0.25 + half3(0.10, 0.18, 0.32)) * fresnel * 0.12 * waterMask;
                return half4(saturate(lit), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _FallbackColor;
                float _PlanetRadius;
                float _RealEarthRadius;
                float _VisualHeightScale;
                float _DetailMetersPerTile;
                float _DetailFarMetersPerTile;
                float _DetailNearFadeMeters;
                float _DetailFarFadeMeters;
                float _SurfaceTextureScale;
                float _LowAltitudeStart;
                float _LowAltitudeEnd;
                float _HighAltitudeStart;
                float _HighAltitudeEnd;
                float _SteepPower;
                float _SteepContrast;
                float _SteepMidpoint;
                float _SteepSlopeThreshold;
                float _NormalStrength;
                float _DetailActive;
                float _SurfaceBlendMode;
                float _MacroBlendStart;
                float _MacroBlendEnd;
                float _MacroBlendMax;
                float _GroundAmbientFloor;
                float _SeaLevelHeightMeters;
                float _ShallowWaterDepthMeters;
                float _WaveMetersPerTile;
                float _WaveSpeed;
                float _WaveStrength;
                float _WaveHeightMeters;
                float _HasOcean;
                float4 _OceanColor;
                float _OceanSatelliteBlend;
                float _OceanSpecular;
                float _OceanDepthPackMeters;
                float _ColorMapFlipLongitude;
                float _ColorMapFlipLatitude;
                float _PlanetSpinLongitudeRadians;
            CBUFFER_END

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                float2 latLongUv : TEXCOORD0;
                float2 localMetersUv : TEXCOORD1;
                float4 vertexColor : COLOR;
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            float DepthWorldHorizontalScale()
            {
                return max(_PlanetRadius, 1.0) / max(_RealEarthRadius, 1.0);
            }

            float2 DepthOceanWindDir(float2 splatUv)
            {
                float lat = (splatUv.y - 0.5) * 3.14159265;
                float lon = splatUv.x * 6.2831853;
                float absLat = abs(lat);
                float westerly = smoothstep(0.28, 0.72, absLat);
                float angle = lerp(3.02, 0.20, westerly);
                angle += sin(lon * 2.0 + lat * 1.7) * 0.42;
                angle += sin(lon * 5.0 - lat * 0.8) * 0.14;
                float2 dir = float2(cos(angle), sin(angle));
                dir.y *= lat < 0.0 ? -1.0 : 1.0;
                return normalize(dir);
            }

            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings output;
                float3 positionOS = input.positionOS.xyz;
                float3 positionWS = TransformObjectToWorld(positionOS);
                float fade = 1.0 - smoothstep(
                    350.0,
                    2600.0,
                    length(_WorldSpaceCameraPos - positionWS)
                );
                float worldScale = DepthWorldHorizontalScale();
                float absLat = abs((input.latLongUv.y - 0.5) * 3.14159265);
                float depthMeters = input.vertexColor.a * input.vertexColor.g * _OceanDepthPackMeters;
                float deep = saturate(depthMeters / 380.0);
                float lambda = max(_WaveMetersPerTile, 120.0) * worldScale *
                    lerp(0.85, 1.40, saturate(absLat / 0.85)) *
                    lerp(0.80, 1.12, deep);
                float2 wind = DepthOceanWindDir(input.latLongUv);
                float2 crossWind = float2(
                    wind.x * 0.955 - wind.y * 0.296,
                    wind.x * 0.296 + wind.y * 0.955
                );
                float time = _Time.y * _WaveSpeed;
                float k1 = 6.28318530718 / lambda;
                float k2 = 6.28318530718 / (lambda * 1.75);
                float phase1 = dot(wind, input.localMetersUv) * k1 - time * 0.82;
                float phase2 = dot(crossWind, input.localMetersUv) * k2 - time * 0.54;
                float latAmp = lerp(0.50, 1.0, smoothstep(0.22, 0.78, absLat));
                float depthAmp = lerp(0.48, 1.0, deep);
                float amplitude = _HasOcean > 0.5 ?
                    _WaveHeightMeters * worldScale * _WaveStrength *
                    latAmp * depthAmp * fade * input.vertexColor.a :
                    0.0;
                float height = (sin(phase1) * 0.88 + sin(phase2) * 0.12) * amplitude;
                positionOS += normalize(positionOS) * height;
                output.positionCS = TransformObjectToHClip(positionOS);
                return output;
            }

            half DepthFrag(DepthVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
