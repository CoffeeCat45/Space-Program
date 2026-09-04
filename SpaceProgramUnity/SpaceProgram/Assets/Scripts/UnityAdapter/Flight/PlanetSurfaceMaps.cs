using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Resolves RSS Earth surface maps from Assets/Content/Environment/RSS-Textures.
/// </summary>
public static class PlanetSurfaceMaps
{
    public const string EnvironmentFolderName = "Environment";
    public const string RssTexturesFolderName = "RSS-Textures";
    public const string PluginDataFolderName = "PluginData";

    public const string RssTexturesAssetFolder =
        "Assets/Content/Environment/RSS-Textures";

    public const string EarthColorFileName = "EarthColor.dds";
    public const string EarthNormalFileName = "Earth_NRM.dds";
    public const string EarthHeightFileName = "EarthHeight.dds";

    public const string EarthColorAssetPath =
        RssTexturesAssetFolder + "/" + EarthColorFileName;

    public const string EarthNormalAssetPath =
        RssTexturesAssetFolder + "/" + EarthNormalFileName;

    public const string EarthHeightAssetPath =
        RssTexturesAssetFolder + "/" + PluginDataFolderName + "/" + EarthHeightFileName;

    public const string EarthHeightAssetPathFlat =
        RssTexturesAssetFolder + "/" + EarthHeightFileName;

    /// <summary>RSS Kopernicus Earth.cfg VertexHeightMap offset (real meters).</summary>
    public const double EarthHeightOffsetMeters = -10921.0;

    /// <summary>RSS Kopernicus Earth.cfg VertexHeightMap deformity (real meters).</summary>
    public const double EarthHeightDeformityMeters = 19606.0;

    // At the "proportionally correct" 1:10 scale (matching the 1:10 planet radius),
    // Everest's real ~8848m only rises ~868m on a 637km-radius sphere — 0.14% of the
    // radius, the same ratio as on the real Earth. That's physically honest but reads as
    // "the whole planet is a smooth featureless ball" from orbit, and even at ground
    // level a few hundred meters of relief is subtle. KSP's own Kerbin exaggerates its
    // terrain well beyond real proportions specifically so mountains are visually
    // dramatic; bump this the same way for the same reason.
    public const double VisualHeightScale = 0.4;

    public static string ResolveRssTexturesRoot()
    {
        string streamingDirectory = PlayerContentPaths.RssTexturesStreamingDirectory();

        if (Directory.Exists(streamingDirectory))
        {
            return streamingDirectory;
        }

        return Path.Combine(
            Application.dataPath,
            "Content",
            EnvironmentFolderName,
            RssTexturesFolderName
        );
    }

    public static string ResolveEarthColorPath()
    {
        return Path.Combine(ResolveRssTexturesRoot(), EarthColorFileName);
    }

    public static string ResolveEarthNormalPath()
    {
        return Path.Combine(ResolveRssTexturesRoot(), EarthNormalFileName);
    }

    public static string ResolveEarthHeightPath()
    {
        return Path.Combine(
            ResolveRssTexturesRoot(),
            PluginDataFolderName,
            EarthHeightFileName
        );
    }

    public static string ResolveEarthHeightPathFlat()
    {
        return Path.Combine(ResolveRssTexturesRoot(), EarthHeightFileName);
    }

    public static bool TryResolveEarthColorPath(out string resolvedPath)
    {
        return TryExistingFile(ResolveEarthColorPath(), out resolvedPath);
    }

    public static bool TryResolveEarthNormalPath(out string resolvedPath)
    {
        return TryExistingFile(ResolveEarthNormalPath(), out resolvedPath);
    }

    public static bool TryResolveEarthHeightPath(out string resolvedPath)
    {
        if (TryExistingFile(ResolveEarthHeightPath(), out resolvedPath))
        {
            return true;
        }

        return TryExistingFile(ResolveEarthHeightPathFlat(), out resolvedPath);
    }

    public static double SampleEarthHeightMeters(double normalizedHeightSample)
    {
        double realMeters = EarthHeightOffsetMeters +
            normalizedHeightSample * EarthHeightDeformityMeters;
        return realMeters * VisualHeightScale;
    }

    static bool TryExistingFile(string path, out string resolvedPath)
    {
        resolvedPath = path;
        return File.Exists(path);
    }
}
