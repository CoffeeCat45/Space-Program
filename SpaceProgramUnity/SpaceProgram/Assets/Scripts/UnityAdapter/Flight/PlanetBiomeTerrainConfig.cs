/// <summary>
/// Earth biome terrain constants for flight chunks.
/// Distances are <b>real Earth meters</b>. The shader multiplies by
/// planetRadius / <see cref="PlanetSurfaceConstants.RealEarthRadiusMeters"/> so tiling
/// stays the same when the world goes from 1:10 to 1:1.
/// </summary>
public static class PlanetBiomeTerrainConfig
{
    /// <summary>
    /// RSS PQS Earth <c>lowTiling = 200000</c> ≈ 200 real-m/tile.
    /// Parallax <c>_SurfaceTextureScale = 0.55</c> is EVA grain (~2 real-m) and sparkles on coarse LODs.
    /// </summary>
    public const float DetailMetersPerTile = 200f;

    /// <summary>Far-chunk tile size (real meters). Kopernicus ZoomRotation far tiling.</summary>
    public const float DetailFarMetersPerTile = 800f;

    /// <summary>Camera distance where near tiling still applies (real meters).</summary>
    public const float DetailNearFadeMeters = 300f;

    /// <summary>Camera distance where far tiling is fully applied (real meters).</summary>
    public const float DetailFarFadeMeters = 4200f;

    public const float SurfaceTextureScale = 1f;

    public const float LowAltitudeStartMeters = 0f;
    public const float LowAltitudeEndMeters = 80f;
    public const float HighAltitudeStartMeters = 3500f;
    public const float HighAltitudeEndMeters = 4500f;

    /// <summary>
    /// Airless close-up (Moon): Low/Mid/High from signed elevation, not Earth biomes.
    /// LDEM maria are negative; highlands are a few km.
    /// </summary>
    public const float HeightSlopeLowStartMeters = -2800f;
    public const float HeightSlopeLowEndMeters = -150f;
    public const float HeightSlopeHighStartMeters = 900f;
    public const float HeightSlopeHighEndMeters = 2800f;

    public const float SteepPower = 4f;
    public const float SteepContrast = 3.5f;
    public const float SteepMidpoint = 0.55f;
    public const float SteepSlopeThreshold = 0.48f;

    public const float HeightSlopeSteepPower = 6f;
    public const float HeightSlopeSteepContrast = 4f;
    public const float HeightSlopeSteepMidpoint = 0.731f;

    public const float NormalStrength = 1f;

    /// <summary>
    /// NASA photo overlay vs camera altitude in real meters.
    /// Must not use fragment terrain height — that painted mountain peaks white on the pad.
    /// Starts after pad-lock band so macro PNG does not drift during ascent.
    /// </summary>
    public const float MacroSatelliteBlendStartMeters = 52000f;
    public const float MacroSatelliteBlendEndMeters = 70000f;
    public const float MacroSatelliteMaxBlend = 0.16f;

    /// <summary>
    /// Moon LROC overlay vs camera-to-fragment distance. Shader still multiplies by
    /// worldScale (R / 6371 km), so these real-meter values become ~12 km / ~38 km sim
    /// on the 1:10 Moon. Limb and high hops show the photo map, not gray splat.
    /// </summary>
    public const float MoonMacroBlendStartRealMeters = 440000f;
    public const float MoonMacroBlendEndRealMeters = 1394000f;
    public const float MoonMacroBlendMax = 0.9f;

    public const float GroundAmbientFloor = 0.08f;

    /// <summary>
    /// Dominant swell length in real meters. Open-ocean swell, not wind chop.
    /// </summary>
    public const float WaveMetersPerTile = 520f;

    /// <summary>Travel scale. Longer swell must stay slower than short chop.</summary>
    public const float WaveSpeed = 0.62f;

    public const float WaveStrength = 1f;

    /// <summary>Peak swell height in real meters. Shader scales by planetRadius / 6371000.</summary>
    public const float WaveHeightRealMeters = 12f;

    /// <summary>
    /// Moon chunks share this shader but have no vertex ocean mask. Unity then
    /// defaults COLOR.a to 1 and the whole globe swells (~1–2 m peak-to-peak).
    /// </summary>
    public static float WaveHeightMetersForBody(bool hasOcean)
    {
        return hasOcean ? WaveHeightRealMeters : 0f;
    }

    public const float OceanSpecular = 0.38f;
    public const float OceanDepthPackMeters = 2500f;

    /// <summary>
    /// How much NASA ocean hue tints the water. Clouds in the photo are rejected in-shader.
    /// </summary>
    public const float OceanSatelliteBlend = 0.78f;
}
