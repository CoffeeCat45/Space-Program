using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Bakes NASA Blue Marble GeoTIFF into runtime PNGs for map + flight macro color.
/// Menu: Space Program / Bake Earth Color Map 2K (.png) | 8K (.png)
/// </summary>
public static class EarthColorMapBaker
{
    const string MenuPath2k = "Space Program/Bake Earth Color Map 2K (.png)";
    const string MenuPath8k = "Space Program/Bake Earth Color Map 8K (.png)";

    public static string StandardSourceTiffPath =>
        Path.Combine(
            Application.dataPath,
            "StreamingAssets",
            "MapMode",
            PlanetSurfaceConstants.ColorTiffStandardFileName
        );

    public static string HighSourceTiffPath =>
        Path.Combine(
            Application.dataPath,
            "StreamingAssets",
            "MapMode",
            PlanetSurfaceConstants.ColorTiffHighFileName
        );

    public static string StandardOutputPngPath =>
        Path.Combine(
            Application.dataPath,
            "StreamingAssets",
            "MapMode",
            PlanetSurfaceConstants.GeographicPng2kFileName
        );

    public static string HighOutputPngPath =>
        Path.Combine(
            Application.dataPath,
            "StreamingAssets",
            "MapMode",
            PlanetSurfaceConstants.GeographicPng8kFileName
        );

    [MenuItem(MenuPath2k)]
    public static void Bake2kFromDefaultPaths()
    {
        Bake(
            StandardSourceTiffPath,
            StandardOutputPngPath,
            EarthGeographicMapPaths.StandardMaxLoadWidth
        );
    }

    [MenuItem(MenuPath8k)]
    public static void Bake8kFromDefaultPaths()
    {
        Bake(
            HighSourceTiffPath,
            HighOutputPngPath,
            EarthGeographicMapPaths.HighMaxLoadWidth
        );
    }

    public static void Bake(string sourceTiffAbsolutePath, string outputPngAbsolutePath, int maxWidth)
    {
        try
        {
            if (!File.Exists(sourceTiffAbsolutePath))
            {
                EditorUtility.DisplayDialog(
                    "Earth Color Bake",
                    "Source TIFF not found:\n" + sourceTiffAbsolutePath +
                    "\n\nPlace NASA Blue Marble in StreamingAssets/MapMode/.",
                    "OK"
                );
                return;
            }

            GeotiffRgbReader.Raster raster = GeotiffRgbReader.ReadRgb(
                sourceTiffAbsolutePath,
                maxWidth,
                progress =>
                {
                    EditorUtility.DisplayProgressBar(
                        "Earth Color Bake",
                        "Decoding NASA GeoTIFF…",
                        progress
                    );
                }
            );
            EditorUtility.ClearProgressBar();
            Texture2D texture = GeotiffRgbReader.ToTexture(raster, "EarthColorBake");

            string outputDirectory = Path.GetDirectoryName(outputPngAbsolutePath);

            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            File.WriteAllBytes(outputPngAbsolutePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.Refresh();

            Debug.Log(
                "EarthColorMapBaker: wrote " + raster.Width + "x" + raster.Height +
                " PNG to " + outputPngAbsolutePath +
                " (canonical geographic UV — no runtime flips)."
            );
            EditorUtility.DisplayDialog(
                "Earth Color Bake",
                "Wrote " + raster.Width + "x" + raster.Height + " map to:\n" +
                outputPngAbsolutePath,
                "OK"
            );
        }
        catch (System.Exception exception)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError("EarthColorMapBaker: " + exception.Message);
            EditorUtility.DisplayDialog(
                "Earth Color Bake Failed",
                exception.Message,
                "OK"
            );
        }
    }

    const string ValidateMenuPath = "Space Program/Validate Earth Color Map Orientation (.png)";

    [MenuItem(ValidateMenuPath)]
    public static void ValidateExistingPngOrientation()
    {
        string pngPath = DevSettings.EarthGeographicMapHigh8K ?
            HighOutputPngPath :
            StandardOutputPngPath;

        if (!File.Exists(pngPath))
        {
            EditorUtility.DisplayDialog(
                "Validate Earth Color",
                "PNG not found:\n" + pngPath,
                "OK"
            );
            return;
        }

        try
        {
            EnsureGebcoLoadedForBake();
            byte[] bytes = File.ReadAllBytes(pngPath);
            Texture2D texture = PlanetEquirectangularTextureLoader.LoadRgbFromImageBytes(
                bytes,
                "EarthColorValidate"
            );

            if (texture == null)
            {
                EditorUtility.DisplayDialog(
                    "Validate Earth Color",
                    "Failed to decode PNG.",
                    "OK"
                );
                return;
            }

            ValidateCanonicalSites(texture);
            Object.DestroyImmediate(texture);

            EditorUtility.DisplayDialog(
                "Validate Earth Color",
                "Clean Canon pipeline:\n" +
                "• PNG on disk = canonical (no runtime flip).\n" +
                "• CPU and GPU: texU = geographicU.\n\n" +
                "See Console for GEBCO vs color at Cape / Morocco / Tibet.",
                "OK"
            );
        }
        catch (System.Exception exception)
        {
            Debug.LogError("EarthColorMapBaker: validate failed — " + exception.Message);
            EditorUtility.DisplayDialog(
                "Validate Earth Color Failed",
                exception.Message,
                "OK"
            );
        }
    }

    static void ValidateCanonicalSites(Texture2D texture)
    {
        ValidateSite(texture, "Cape", 28.57, -80.65);
        ValidateSite(texture, "Morocco", 31.63, -7.99);
        ValidateSite(texture, "Tibet", 31.48, 91.10);
        ValidateSite(texture, "Pacific", 0.0, -160.0, expectOcean: true);
    }

    static void ValidateSite(
        Texture2D texture,
        string label,
        double latitudeDegrees,
        double longitudeDegrees,
        bool expectOcean = false
    )
    {
        Color sample = PlanetEquirectangularTextureLoader.SampleColorAtGeographic(
            texture,
            latitudeDegrees,
            longitudeDegrees
        );
        bool colorLooksLikeOcean = sample.b > sample.g + 0.04f && sample.b > 0.12f;
        bool colorLooksLikeLand = sample.g > 0.12f && sample.b < sample.g * 0.85f;

        if (PlanetElevationCatalog.IsHighResLoaded || PlanetElevationCatalog.IsLoaded)
        {
            double gebcoMeters = PlanetElevationCatalog.GetElevationAtLatitudeLongitudeDegrees(
                latitudeDegrees,
                longitudeDegrees
            );
            bool gebcoLand = gebcoMeters > 0.0;

            if (gebcoLand && colorLooksLikeOcean)
            {
                Debug.LogWarning(
                    "EarthColorMapBaker: color disagrees with GEBCO at " + label +
                    " (GEBCO=" + gebcoMeters.ToString("F0") + "m)."
                );
            }
            else if (!gebcoLand && colorLooksLikeLand)
            {
                Debug.LogWarning(
                    "EarthColorMapBaker: color disagrees with GEBCO at " + label +
                    " (GEBCO=" + gebcoMeters.ToString("F0") + "m)."
                );
            }

            return;
        }

        if (expectOcean && !colorLooksLikeOcean)
        {
            Debug.LogWarning("EarthColorMapBaker: expected ocean at " + label + ".");
        }
        else if (!expectOcean && colorLooksLikeOcean)
        {
            Debug.LogWarning("EarthColorMapBaker: expected land at " + label + ".");
        }
    }

    static void EnsureGebcoLoadedForBake()
    {
        if (PlanetElevationCatalog.IsHighResLoaded || PlanetElevationCatalog.IsLoaded)
        {
            return;
        }

        string planetFolder = Path.Combine(Application.streamingAssetsPath, "Planet");
        string rawPath = Path.Combine(planetFolder, PlanetSurfaceConstants.ElevationRawFileName);
        string binPath = Path.Combine(planetFolder, PlanetSurfaceConstants.ElevationBinaryFileName);

        if (File.Exists(rawPath))
        {
            PlanetElevationCatalog.LoadHighResFromFile(rawPath);
            PlanetElevationCatalog.UseHighResElevation = true;
            return;
        }

        if (File.Exists(binPath))
        {
            PlanetElevationCatalog.LoadFromFile(binPath);
            PlanetElevationCatalog.UseHighResElevation = false;
        }
    }

    const string FixLongitudeMenuPath = "Space Program/Fix Earth Color Map Longitude (.png)";

    [MenuItem(FixLongitudeMenuPath)]
    public static void FlipEarthColorMapLongitudeOnDisk()
    {
        string pngPath = DevSettings.EarthGeographicMapHigh8K ?
            HighOutputPngPath :
            StandardOutputPngPath;

        if (!File.Exists(pngPath))
        {
            EditorUtility.DisplayDialog(
                "Fix Earth Color Longitude",
                "PNG not found:\n" + pngPath,
                "OK"
            );
            return;
        }

        if (
            !EditorUtility.DisplayDialog(
                "Fix Earth Color Longitude",
                "Horizontally flip active earth geographic PNG on disk (U → 1-U per row).\n\n" +
                "Use only if GEBCO probe says color is E/W mirrored vs GEBCO.\n" +
                "Restart Play mode after this.",
                "Flip PNG",
                "Cancel"
            )
        )
        {
            return;
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(pngPath);
            Texture2D texture = PlanetEquirectangularTextureLoader.LoadRgbFromImageBytes(
                bytes,
                "EarthColorFlipLongitude"
            );

            if (texture == null)
            {
                EditorUtility.DisplayDialog(
                    "Fix Earth Color Longitude",
                    "Failed to decode PNG.",
                    "OK"
                );
                return;
            }

            int width = texture.width;
            int height = texture.height;
            Color[] pixels = texture.GetPixels();
            var flipped = new Color[pixels.Length];

            for (int y = 0; y < height; y++)
            {
                int row = y * width;

                for (int x = 0; x < width; x++)
                {
                    flipped[row + x] = pixels[row + (width - 1 - x)];
                }
            }

            texture.SetPixels(flipped);
            texture.Apply();
            File.WriteAllBytes(pngPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.Refresh();
            PlanetTerrainTextureResources.ForceReloadWorldMaps();

            Debug.Log(
                "EarthColorMapBaker: horizontally flipped " + pngPath +
                ". Restart Play mode; probe should now recommend colorFlipU=false."
            );
            EditorUtility.DisplayDialog(
                "Fix Earth Color Longitude",
                "PNG flipped on disk.\nRestart Play mode and check Console for layout policy.",
                "OK"
            );
        }
        catch (System.Exception exception)
        {
            Debug.LogError("EarthColorMapBaker: flip longitude failed — " + exception.Message);
            EditorUtility.DisplayDialog(
                "Fix Earth Color Longitude Failed",
                exception.Message,
                "OK"
            );
        }
    }
}
