/// <summary>
/// Bake-time / runtime policy for horizontal layout of on-disk Earth maps vs canonical
/// <see cref="PlanetGeographicCoords"/>. One flag per asset type — applied in the single
/// sampler for that asset (CPU + GPU together), never split across loader/shader/heuristics.
/// </summary>
public struct PlanetMapLayoutPolicy
{
    /// <summary>Sample color at texture U = 1 - geographicU (E/W mirrored on-disk asset).</summary>
    public bool ColorMapRequiresHorizontalFlip;

    /// <summary>
    /// Sample color at texture V = 1 - geographicV. Only for on-disk assets that were NOT
    /// canonical-baked (e.g. raw RSS row-0-north PNG loaded without row remap).
    /// </summary>
    public bool ColorMapRequiresVerticalFlip;

    public bool ElevationMapRequiresHorizontalFlip;

    public static PlanetMapLayoutPolicy CleanCanon =>
        new PlanetMapLayoutPolicy
        {
            ColorMapRequiresHorizontalFlip = false,
            ColorMapRequiresVerticalFlip = false,
            ElevationMapRequiresHorizontalFlip = false,
        };

    /// <summary>
    /// earth_geographic.png from NASA TIFF bake — canonical U and V after
    /// <see cref="GeotiffRgbReader"/> row remap + Unity LoadImage. No runtime flips.
    /// </summary>
    public static PlanetMapLayoutPolicy NasaGeographicPng => CleanCanon;

    public float MapColorGeographicU(float geographicU)
    {
        return ColorMapRequiresHorizontalFlip ? (1f - geographicU) : geographicU;
    }

    public float MapColorGeographicV(float geographicV)
    {
        return ColorMapRequiresVerticalFlip ? (1f - geographicV) : geographicV;
    }

    public double MapElevationGeographicU(double geographicU)
    {
        return ElevationMapRequiresHorizontalFlip ? (1.0 - geographicU) : geographicU;
    }

    public override string ToString()
    {
        return
            "PlanetMapLayoutPolicy(colorFlipU=" +
            ColorMapRequiresHorizontalFlip.ToString().ToLowerInvariant() +
            ", colorFlipV=" +
            ColorMapRequiresVerticalFlip.ToString().ToLowerInvariant() +
            ", elevationFlipU=" +
            ElevationMapRequiresHorizontalFlip.ToString().ToLowerInvariant() +
            ")";
    }
}
