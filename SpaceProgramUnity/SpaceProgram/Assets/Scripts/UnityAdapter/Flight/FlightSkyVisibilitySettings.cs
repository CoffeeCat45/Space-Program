/// <summary>
/// KSP-style sky / star fade altitudes (sim meters above planet radius).
/// </summary>
static class FlightSkyVisibilitySettings
{
    /// <summary>Below ~8 km the zenith stays normal atmospheric blue.</summary>
    public const float BlackZenithStartAltitudeMeters = 8000f;

    /// <summary>By ~25 km zenith darkening is already strong; cone keeps widening toward horizon.</summary>
    public const float BlackZenithEndAltitudeMeters = 28000f;

    /// <summary>First stars through the atmosphere shell (no Milky Way yet).</summary>
    public const float StarsAppearStartAltitudeMeters = 32000f;

    public const float StarsAppearEndAltitudeMeters = 48000f;

    public const float SkyboxStarsExposureStartAltitudeMeters = 33000f;

    public const float SkyboxStarsExposureEndAltitudeMeters = 52000f;

    /// <summary>Inner sky shell fades out before the mesh ends (prevents cliff at shell exit).</summary>
    public const float AtmosphereShellFadeStartAltitudeMeters = 58000f;

    public const float AtmosphereShellFadeEndAltitudeMeters = 105000f;

    /// <summary>Visual atmosphere mesh height (sim meters above surface).</summary>
    public const float AtmosphereShellHeightMeters = 115000f;

    /// <summary>Milky Way / galaxy wash on the cubemap — much higher than point stars.</summary>
    public const float SkyboxGalaxyExposureStartAltitudeMeters = 90000f;

    public const float SkyboxGalaxyExposureEndAltitudeMeters = 170000f;

    public const float StarsOnlySkyboxExposure = 0.28f;

    public const float FullGalaxySkyboxExposure = 1.0f;

    /// <summary>Map mode always shows full galaxy sky, independent of vessel altitude.</summary>
    public const float MapModeSkyboxExposure = FullGalaxySkyboxExposure;
}
