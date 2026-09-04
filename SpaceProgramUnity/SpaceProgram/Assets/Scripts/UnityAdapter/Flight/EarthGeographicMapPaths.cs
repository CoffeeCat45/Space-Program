using System.IO;
using UnityEngine;

/// <summary>
/// Runtime paths for canonical NASA geographic PNG (2K vs 8K dev toggle).
/// </summary>
public static class EarthGeographicMapPaths
{
    public const int StandardMaxLoadWidth = 4096;
    public const int HighMaxLoadWidth = 8192;

    public static bool UseHighResolutionMap => DevSettings.EarthGeographicMapHigh8K;

    public static string ActivePngFileName =>
        UseHighResolutionMap ?
            PlanetSurfaceConstants.GeographicPng8kFileName :
            PlanetSurfaceConstants.GeographicPng2kFileName;

    public static string ActivePngPath =>
        Path.Combine(PlanetDataPaths.MapModeFolder, ActivePngFileName);

    public static string StandardPngPath =>
        Path.Combine(PlanetDataPaths.MapModeFolder, PlanetSurfaceConstants.GeographicPng2kFileName);

    public static string HighPngPath =>
        Path.Combine(PlanetDataPaths.MapModeFolder, PlanetSurfaceConstants.GeographicPng8kFileName);

    public static int ActiveMaxLoadWidth
    {
        get
        {
            int requested = UseHighResolutionMap ? HighMaxLoadWidth : StandardMaxLoadWidth;
            int gpuCap = SystemInfo.maxTextureSize;

            if (gpuCap > 0 && requested > gpuCap)
            {
                Debug.LogWarning(
                    "EarthGeographicMapPaths: clamping load width " + requested +
                    " to GPU maxTextureSize " + gpuCap + "."
                );
                return gpuCap;
            }

            return requested;
        }
    }

    public static string ActiveSourceTiffFileName =>
        UseHighResolutionMap ?
            PlanetSurfaceConstants.ColorTiffHighFileName :
            PlanetSurfaceConstants.ColorTiffStandardFileName;

    public static string ActiveSourceTiffPath =>
        Path.Combine(PlanetDataPaths.MapModeFolder, ActiveSourceTiffFileName);
}
