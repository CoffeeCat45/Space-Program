/// <summary>
/// Layout for offline LOD elevation grids (Int16 meters, row-major, no header).
/// Same geographic layout as <see cref="PlanetElevationRawFormat"/>:
/// row 0 = north pole (+90°), Greenwich at column Width/2.
/// </summary>
public static class PlanetElevationLodRawFormat
{
    public const int CoarseWidth = 4096;

    public const int CoarseHeight = 2048;

    public const string CoarseFileName = "earth_elevation_coarse_4096.raw";

    public const long CoarseByteCount = (long)CoarseWidth * CoarseHeight * PlanetElevationRawFormat.BytesPerSample;

    public const int MidWidth = 8192;

    public const int MidHeight = 4096;

    public const string MidFileName = "earth_elevation_mid_8192.raw";

    public const long MidByteCount = (long)MidWidth * MidHeight * PlanetElevationRawFormat.BytesPerSample;

    public const int HighMidWidth = 16384;

    public const int HighMidHeight = 8192;

    public const string HighMidFileName = "earth_elevation_highmid_16384.raw";

    public const long HighMidByteCount = (long)HighMidWidth * HighMidHeight * PlanetElevationRawFormat.BytesPerSample;
}
