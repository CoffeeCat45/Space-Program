using System;

/// <summary>
/// Lat/lon to pixel coordinates for <see cref="PlanetElevationRawFormat"/> (86400×43200).
/// Row 0 = +90°N, row Height-1 = -90°S. Lon −180°..+180°, Greenwich at X = Width/2.
/// Same longitude convention as <see cref="PlanetGeographicCoords"/> (Clean Canon).
/// </summary>
public static class PlanetElevationRawCoords
{
    public static void LatitudeLongitudeRadiansToContinuousPixel(
        double latitudeRadians,
        double longitudeRadians,
        out double pixelX,
        out double pixelY
    )
    {
        double latitudeDegrees = latitudeRadians * (180.0 / Math.PI);
        double longitudeDegrees = longitudeRadians * (180.0 / Math.PI);
        LatitudeLongitudeDegreesToContinuousPixel(
            latitudeDegrees,
            longitudeDegrees,
            out pixelX,
            out pixelY
        );
    }

    public static void LatitudeLongitudeDegreesToContinuousPixel(
        double latitudeDegrees,
        double longitudeDegrees,
        out double pixelX,
        out double pixelY
    )
    {
        LatitudeLongitudeDegreesToContinuousPixel(
            latitudeDegrees,
            longitudeDegrees,
            PlanetElevationRawFormat.Width,
            PlanetElevationRawFormat.Height,
            out pixelX,
            out pixelY
        );
    }

    public static void LatitudeLongitudeRadiansToContinuousPixel(
        double latitudeRadians,
        double longitudeRadians,
        int gridWidth,
        int gridHeight,
        out double pixelX,
        out double pixelY
    )
    {
        double latitudeDegrees = latitudeRadians * (180.0 / Math.PI);
        double longitudeDegrees = longitudeRadians * (180.0 / Math.PI);
        LatitudeLongitudeDegreesToContinuousPixel(
            latitudeDegrees,
            longitudeDegrees,
            gridWidth,
            gridHeight,
            out pixelX,
            out pixelY
        );
    }

    public static void LatitudeLongitudeDegreesToContinuousPixel(
        double latitudeDegrees,
        double longitudeDegrees,
        int gridWidth,
        int gridHeight,
        out double pixelX,
        out double pixelY
    )
    {
        longitudeDegrees = WrapLongitudeDegrees(longitudeDegrees);
        latitudeDegrees = ClampLatitudeDegrees(latitudeDegrees);

        double maxY = gridHeight - 1;

        pixelX = ((longitudeDegrees + 180.0) / 360.0) * gridWidth;
        pixelY = ((90.0 - latitudeDegrees) / 180.0) * maxY;
    }

    public static void ContinuousPixelToLatitudeLongitudeDegrees(
        double pixelX,
        double pixelY,
        out double latitudeDegrees,
        out double longitudeDegrees
    )
    {
        double maxY = PlanetElevationRawFormat.Height - 1;

        pixelY = Clamp(pixelY, 0.0, maxY);

        longitudeDegrees = (pixelX / PlanetElevationRawFormat.Width) * 360.0 - 180.0;
        latitudeDegrees = 90.0 - ((pixelY / maxY) * 180.0);
    }

    public static double WrapLongitudeDegrees(double longitudeDegrees)
    {
        double wrapped = longitudeDegrees % 360.0;

        if (wrapped <= -180.0)
        {
            wrapped += 360.0;
        }
        else if (wrapped > 180.0)
        {
            wrapped -= 360.0;
        }

        return wrapped;
    }

    static double ClampLatitudeDegrees(double latitudeDegrees)
    {
        if (latitudeDegrees < -90.0)
        {
            return -90.0;
        }

        if (latitudeDegrees > 90.0)
        {
            return 90.0;
        }

        return latitudeDegrees;
    }

    static double Clamp(double value, double min, double max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }
}
