using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Bakes offline LOD elevation grids (Phase B1) from full-resolution earth_elevation.raw.
/// Menu: Space Program / Bake GEBCO LOD Elevation (.raw coarse + mid + highmid)
/// </summary>
public static class GebcoLodElevationBaker
{
    const string MenuPath = "Space Program/Bake GEBCO LOD Elevation (.raw coarse + mid + highmid)";

    public static string StreamingAssetsPlanetFolder =>
        Path.Combine(Application.dataPath, "StreamingAssets", "Planet");

    public static string DefaultSourceRawPath =>
        Path.Combine(StreamingAssetsPlanetFolder, PlanetElevationRawFormat.DefaultFileName);

    public static string DefaultCoarseOutputPath =>
        Path.Combine(StreamingAssetsPlanetFolder, PlanetElevationLodRawFormat.CoarseFileName);

    public static string DefaultMidOutputPath =>
        Path.Combine(StreamingAssetsPlanetFolder, PlanetElevationLodRawFormat.MidFileName);

    public static string DefaultHighMidOutputPath =>
        Path.Combine(StreamingAssetsPlanetFolder, PlanetElevationLodRawFormat.HighMidFileName);

    [MenuItem(MenuPath)]
    public static void BakeFromDefaultPaths()
    {
        Bake(
            DefaultSourceRawPath,
            DefaultCoarseOutputPath,
            DefaultMidOutputPath,
            DefaultHighMidOutputPath
        );
    }

    public static void Bake(
        string sourceRawAbsolutePath,
        string coarseOutputAbsolutePath,
        string midOutputAbsolutePath,
        string highMidOutputAbsolutePath
    )
    {
        try
        {
            if (!File.Exists(sourceRawAbsolutePath))
            {
                throw new FileNotFoundException(
                    "Full-resolution elevation raw was not found. Stitch GEBCO tiles first: " +
                    sourceRawAbsolutePath
                );
            }

            string outputDirectory = Path.GetDirectoryName(coarseOutputAbsolutePath);

            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            BakeTier(
                sourceRawAbsolutePath,
                coarseOutputAbsolutePath,
                PlanetElevationLodRawFormat.CoarseWidth,
                PlanetElevationLodRawFormat.CoarseHeight,
                "coarse"
            );
            BakeTier(
                sourceRawAbsolutePath,
                midOutputAbsolutePath,
                PlanetElevationLodRawFormat.MidWidth,
                PlanetElevationLodRawFormat.MidHeight,
                "mid"
            );
            BakeTier(
                sourceRawAbsolutePath,
                highMidOutputAbsolutePath,
                PlanetElevationLodRawFormat.HighMidWidth,
                PlanetElevationLodRawFormat.HighMidHeight,
                "high-mid"
            );

            AssetDatabase.Refresh();
            Debug.Log(
                "GebcoLodElevationBaker: wrote coarse (~" +
                (PlanetElevationLodRawFormat.CoarseByteCount / (1024 * 1024)) + " MB), mid (~" +
                (PlanetElevationLodRawFormat.MidByteCount / (1024 * 1024)) + " MB), high-mid (~" +
                (PlanetElevationLodRawFormat.HighMidByteCount / (1024 * 1024)) + " MB)."
            );
        }
        catch (Exception exception)
        {
            Debug.LogError("GebcoLodElevationBaker failed: " + exception.Message);
            throw;
        }
    }

    static void BakeTier(
        string sourceRawAbsolutePath,
        string outputAbsolutePath,
        int targetWidth,
        int targetHeight,
        string label
    )
    {
        Debug.Log(
            "GebcoLodElevationBaker: downsampling " + label + " " +
            targetWidth + "x" + targetHeight + "..."
        );
        PlanetElevationRawDownsampler.DownsampleInt16RawFile(
            sourceRawAbsolutePath,
            outputAbsolutePath,
            targetWidth,
            targetHeight
        );
    }
}
