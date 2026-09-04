/// <summary>
/// Layout for full-resolution GEBCO Int16 raw elevation grid (no header).
/// Row-major Int16 meters; row 0 = north pole (+90°), Greenwich at column Width/2.
/// </summary>
public static class PlanetElevationRawFormat
{
    public const int Width = 86400;

    public const int Height = 43200;

    public const int BytesPerSample = 2;

    public const int GreenwichCenterColumn = Width / 2;

    public const string DefaultFileName = "earth_elevation.raw";

    public const long ExpectedByteCount = (long)Width * Height * BytesPerSample;
}
