/// <summary>
/// Shared Earth surface scale constants for SimulationCore and UnityAdapter.
/// Simulation uses real GEBCO meters 1:1. Unity flight mesh applies <see cref="UnityVisualHeightScale"/>.
/// </summary>
public static class PlanetSurfaceConstants
{
    /// <summary>Simulation planet radius (1:10 world scale, meters).</summary>
    public const double EarthRadiusMeters = CelestialBody.Earth.Radius;

    /// <summary>True Earth radius. Horizontal tiling and camera-distance fades scale by
    /// <see cref="UnityFlightPlanetRadiusMeters"/> / this, so 1:10 now and 1:1 later keep
    /// the same real-world meters per texture tile.</summary>
    public const float RealEarthRadiusMeters = 6371000f;

    /// <summary>Unity flight planet radius in meters (637100 sim-m = 6371 km real at 1:10).</summary>
    public const float UnityFlightPlanetRadiusMeters = 637100f;

    /// <summary>GEBCO elevation meters → Unity vertex offset (visual exaggeration).</summary>
    public const float UnityVisualHeightScale = 0.4f;

    /// <summary>Sea level for ocean sphere and atmosphere (real meters).</summary>
    public const double SeaLevelMeters = 0.0;

    public const string ElevationBinaryFileName = "earth_elevation_5400x2700.bin";

    public const string ElevationRawFileName = PlanetElevationRawFormat.DefaultFileName;

    public const string ElevationCoarseRawFileName = PlanetElevationLodRawFormat.CoarseFileName;

    public const string ElevationMidRawFileName = PlanetElevationLodRawFormat.MidFileName;

    public const string ElevationHighMidRawFileName = PlanetElevationLodRawFormat.HighMidFileName;

    public const string ColorTiffHighFileName = "Earth21600x10800_geo.tif";

    public const string ColorTiffStandardFileName = "Earth5400x2700_geo.tif";

    /// <summary>Legacy alias for high-res NASA color TIFF.</summary>
    public const string ColorTiffFileName = ColorTiffHighFileName;

    public const string GeographicPng2kFileName = "earth_geographic.png";

    public const string GeographicPng8kFileName = "earth_geographic_8k.png";

    public const string CloudsPngFileName = "clouds-alpha.png";

    public const string SourceElevationTiff21600FileName = "Earthrev_elev_21600x10800.tif";

    public const string SourceElevationTiff5400FileName = "Earthrev_elev_5400x2700.tif";

    /// <summary>
    /// Earthrev 8-bit UInt TIFF: 0 = sea level, 255 ≈ +10.7 km (no bathymetry in source).
    /// </summary>
    public const float EarthrevUInt8MetersPerSample = 10710f / 255f;
}
