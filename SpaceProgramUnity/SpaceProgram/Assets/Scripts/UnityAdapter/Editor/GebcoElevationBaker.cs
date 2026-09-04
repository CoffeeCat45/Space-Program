using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Bakes GEBCO GeoTIFF elevation into deterministic SPEL binary for SimulationCore/Unity.
/// Menu: Space Program / Bake GEBCO Elevation (.bin)
/// </summary>
public static class GebcoElevationBaker
{
    const string MenuPath = "Space Program/Bake GEBCO Elevation (.bin)";

    public static string StreamingAssetsPlanetFolder =>
        Path.Combine(Application.dataPath, "StreamingAssets", "Planet");

    public static string DefaultSourceTiffPath =>
        Path.Combine(
            Application.dataPath,
            "StreamingAssets",
            "MapMode",
            PlanetSurfaceConstants.SourceElevationTiff21600FileName
        );

    public static string DefaultOutputBinPath =>
        Path.Combine(StreamingAssetsPlanetFolder, PlanetSurfaceConstants.ElevationBinaryFileName);

    [MenuItem(MenuPath)]
    public static void BakeFromDefaultPaths()
    {
        string sourcePath = DefaultSourceTiffPath;

        if (!File.Exists(sourcePath))
        {
            sourcePath = Path.Combine(
                Application.dataPath,
                "StreamingAssets",
                "MapMode",
                PlanetSurfaceConstants.SourceElevationTiff5400FileName
            );
        }

        Bake(
            sourcePath,
            DefaultOutputBinPath,
            PlanetGeographicCoords.SimulationElevationWidth,
            PlanetGeographicCoords.SimulationElevationHeight
        );
    }

    public static void Bake(
        string sourceTiffAbsolutePath,
        string outputBinAbsolutePath,
        int targetWidth,
        int targetHeight
    )
    {
        try
        {
            GeotiffElevationReader.Raster source =
                GeotiffElevationReader.ReadElevationMeters(sourceTiffAbsolutePath);

            float[] downsampled = DownsampleBoxFilter(
                source.ElevationsMeters,
                source.Width,
                source.Height,
                targetWidth,
                targetHeight
            );

            string outputDirectory = Path.GetDirectoryName(outputBinAbsolutePath);

            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            using (FileStream stream = File.Create(outputBinAbsolutePath))
            {
                PlanetElevationBinaryFormat.Write(stream, downsampled, targetWidth, targetHeight);
            }

            AssetDatabase.Refresh();
            Debug.Log(
                "GebcoElevationBaker: wrote " + outputBinAbsolutePath +
                " (" + targetWidth + "x" + targetHeight + ", float32 meters) from " +
                source.Width + "x" + source.Height + " source."
            );
        }
        catch (Exception exception)
        {
            Debug.LogError("GebcoElevationBaker failed: " + exception.Message);
            throw;
        }
    }

    /// <summary>
    /// Box-filter downsample. Source and target both use V=0 south row, V=1 north row.
    /// </summary>
    public static float[] DownsampleBoxFilter(
        float[] source,
        int sourceWidth,
        int sourceHeight,
        int targetWidth,
        int targetHeight
    )
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (source.Length != sourceWidth * sourceHeight)
        {
            throw new ArgumentException("Source length must equal width * height.", nameof(source));
        }

        var target = new float[targetWidth * targetHeight];
        double scaleX = sourceWidth / (double)targetWidth;
        double scaleY = sourceHeight / (double)targetHeight;

        for (int targetY = 0; targetY < targetHeight; targetY++)
        {
            double sourceY0 = targetY * scaleY;
            double sourceY1 = (targetY + 1) * scaleY;
            int yStart = Math.Max(0, (int)Math.Floor(sourceY0));
            int yEnd = Math.Min(sourceHeight, (int)Math.Ceiling(sourceY1));

            for (int targetX = 0; targetX < targetWidth; targetX++)
            {
                double sourceX0 = targetX * scaleX;
                double sourceX1 = (targetX + 1) * scaleX;
                int xStart = Math.Max(0, (int)Math.Floor(sourceX0));
                int xEnd = Math.Min(sourceWidth, (int)Math.Ceiling(sourceX1));

                double sum = 0.0;
                int count = 0;

                for (int sourceY = yStart; sourceY < yEnd; sourceY++)
                {
                    int rowOffset = sourceY * sourceWidth;

                    for (int sourceX = xStart; sourceX < xEnd; sourceX++)
                    {
                        sum += source[rowOffset + sourceX];
                        count++;
                    }
                }

                target[(targetY * targetWidth) + targetX] = count > 0 ?
                    (float)(sum / count) :
                    0f;
            }
        }

        return target;
    }
}
