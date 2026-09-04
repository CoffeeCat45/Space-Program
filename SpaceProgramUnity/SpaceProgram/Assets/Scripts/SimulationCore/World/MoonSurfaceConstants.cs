/// <summary>
/// Moon terrain files in StreamingAssets (same layout as Earth GEBCO / geographic PNG).
/// LDEM source TIF is kilometres; baked .raw is Int16 metres.
/// </summary>
public static class MoonSurfaceConstants
{
    public const string ElevationRawFileName = "moon_elevation.raw";
    public const string ElevationSizeFileName = "moon_elevation.size";
    public const string GeographicPngFileName = "moon_geographic.png";
    public const string GeographicPng8kFileName = "moon_geographic_8k.png";
    public const string SourceLdemTiffFileName = "ldem_64.tif";
    public const float LdemSourceToMeters = 1000f;
    /// <summary>NASA CGI Moon Kit 64 pixels/degree (23040×11520). Source TIF is float kilometres.</summary>
    public const int DefaultElevationWidth = 23040;
    public const int DefaultElevationHeight = 11520;
}
