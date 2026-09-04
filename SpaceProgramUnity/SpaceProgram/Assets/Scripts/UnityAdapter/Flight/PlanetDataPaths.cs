using System.IO;
using UnityEngine;

/// <summary>
/// Runtime paths for NASA/GEBCO planet data in StreamingAssets.
/// </summary>
static class PlanetDataPaths
{
    public static string StreamingAssetsRoot => Application.streamingAssetsPath;

    public static string PlanetFolder =>
        Path.Combine(StreamingAssetsRoot, "Planet");

    public static string MapModeFolder =>
        Path.Combine(StreamingAssetsRoot, "MapMode");

    public static string ElevationBinaryPath =>
        Path.Combine(PlanetFolder, PlanetSurfaceConstants.ElevationBinaryFileName);

    public static string ElevationRawPath =>
        Path.Combine(PlanetFolder, PlanetSurfaceConstants.ElevationRawFileName);

    public static string ColorTiffPath =>
        Path.Combine(MapModeFolder, PlanetSurfaceConstants.ColorTiffHighFileName);

    public static string ColorTiffStandardPath =>
        Path.Combine(MapModeFolder, PlanetSurfaceConstants.ColorTiffStandardFileName);

    public static string SourceElevationTiff21600Path =>
        Path.Combine(MapModeFolder, PlanetSurfaceConstants.SourceElevationTiff21600FileName);

    public static string SourceElevationTiff5400Path =>
        Path.Combine(MapModeFolder, PlanetSurfaceConstants.SourceElevationTiff5400FileName);
}
