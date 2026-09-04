using System.IO;
using UnityEngine;

/// <summary>
/// Runtime paths for NASA LROC geographic PNG (4K vs 8K dev toggle).
/// </summary>
public static class MoonGeographicMapPaths
{
    public const int StandardMaxLoadWidth = 4096;
    public const int HighMaxLoadWidth = 8192;

    public static bool UseHighResolutionMap => DevSettings.MoonGeographicMapHigh8K;

    public static string ActivePngFileName =>
        UseHighResolutionMap ?
            MoonSurfaceConstants.GeographicPng8kFileName :
            MoonSurfaceConstants.GeographicPngFileName;

    public static string ActivePngPath =>
        Path.Combine(PlanetDataPaths.MapModeFolder, ActivePngFileName);

    public static string StandardPngPath =>
        Path.Combine(PlanetDataPaths.MapModeFolder, MoonSurfaceConstants.GeographicPngFileName);

    public static string HighPngPath =>
        Path.Combine(PlanetDataPaths.MapModeFolder, MoonSurfaceConstants.GeographicPng8kFileName);

    public static string NasaLroc8kPngPath =>
        Path.Combine(PlanetDataPaths.MapModeFolder, "lroc_color_16bit_srgb_8k.png");

    /// <summary>
    /// First existing file wins. 8K prefers <c>moon_geographic_8k.png</c>, then NASA LROC 8K name, then 4K.
    /// </summary>
    public static string[] ResolveLoadCandidatePaths()
    {
        if (UseHighResolutionMap)
        {
            return new[]
            {
                HighPngPath,
                NasaLroc8kPngPath,
                StandardPngPath,
            };
        }

        return new[]
        {
            StandardPngPath,
            HighPngPath,
        };
    }

    public static int ActiveMaxLoadWidth
    {
        get
        {
            int requested = UseHighResolutionMap ? HighMaxLoadWidth : StandardMaxLoadWidth;
            int gpuCap = SystemInfo.maxTextureSize;

            if (gpuCap > 0 && requested > gpuCap)
            {
                Debug.LogWarning(
                    "MoonGeographicMapPaths: clamping load width " + requested +
                    " to GPU maxTextureSize " + gpuCap + "."
                );
                return gpuCap;
            }

            return requested;
        }
    }
}
