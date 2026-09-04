using System;
using System.Globalization;
using System.IO;

/// <summary>
/// Int16 LDEM mmap for Moon flight chunks (same bilinear sampler as GEBCO).
/// </summary>
public static class MoonElevationCatalog
{
    static int gridWidth = MoonSurfaceConstants.DefaultElevationWidth;
    static int gridHeight = MoonSurfaceConstants.DefaultElevationHeight;

    public static bool IsLoaded => PlanetElevationCatalog.IsHighResLoaded;

    public static int Width =>
        PlanetElevationCatalog.IsHighResLoaded ?
            PlanetElevationCatalog.HighResField.Width :
            gridWidth;

    public static int Height =>
        PlanetElevationCatalog.IsHighResLoaded ?
            PlanetElevationCatalog.HighResField.Height :
            gridHeight;

    public static void LoadFromStreamingAssetsFolder(string planetFolder)
    {
        if (string.IsNullOrWhiteSpace(planetFolder))
        {
            return;
        }

        string rawPath = Path.Combine(planetFolder, MoonSurfaceConstants.ElevationRawFileName);
        string sizePath = Path.Combine(planetFolder, MoonSurfaceConstants.ElevationSizeFileName);

        if (!File.Exists(rawPath))
        {
            return;
        }

        TryReadSizeFile(sizePath, out gridWidth, out gridHeight);
        PlanetElevationCatalog.LoadHighResGridFromFile(rawPath, gridWidth, gridHeight);
    }

    public static void Unload()
    {
    }

    public static double GetHeightBilinear(SimVector3 direction)
    {
        if (!PlanetElevationCatalog.IsHighResLoaded)
        {
            return 0.0;
        }

        return PlanetElevationCatalog.GetHeightBilinearNew(direction);
    }

    public static void TryReadSizeFile(string sizePath, out int width, out int height)
    {
        width = MoonSurfaceConstants.DefaultElevationWidth;
        height = MoonSurfaceConstants.DefaultElevationHeight;

        if (!File.Exists(sizePath))
        {
            return;
        }

        string text = File.ReadAllText(sizePath).Trim();
        string[] parts = text.Split(
            new[] { ' ', '\t', '\r', '\n', 'x', 'X', ',' },
            StringSplitOptions.RemoveEmptyEntries
        );

        if (parts.Length < 2)
        {
            return;
        }

        if (
            int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedWidth) &&
            int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedHeight) &&
            parsedWidth > 0 &&
            parsedHeight > 0
        )
        {
            width = parsedWidth;
            height = parsedHeight;
        }
    }
}
